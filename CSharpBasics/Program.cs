using System.Runtime.CompilerServices;
using CSharpBasics.Application.Services;
using CSharpBasics.Domain.Models;

using var sensorService = new HC2A_SensorService("COM4");

var threshold = new HC2A_Threshold(
    30,
    60,
    20,
    30
);

var alarmResult = new HC2A_AlarmResult(
    false,
    false,
    false,
    "Normal"
);

Console.WriteLine($"Humidity range: {threshold.HumidityMin} ~ {threshold.HumidityMax}");
Console.WriteLine($"Temperature range: {threshold.TemperatureMin} ~ {threshold.TemperatureMax}");

Console.WriteLine($"Alarm: {alarmResult.HasAlarm}");
Console.WriteLine($"Alarm Message: {alarmResult.Message}");

using var cts = new CancellationTokenSource();

_ = Task.Run(() =>
{
    Console.WriteLine("Press Enter to stop.");
    Console.ReadLine();
    cts.Cancel();
});

try
{
    sensorService.Open();
    
    Console.WriteLine($"IsOpen: {sensorService.IsOpen}");

    TimeSpan sampleInterval = TimeSpan.FromSeconds(1);
    DateTimeOffset nextRunAt = DateTimeOffset.UtcNow;

    int count = 0;
    while(!cts.Token.IsCancellationRequested)
    {
        nextRunAt += sampleInterval;

        var reading = await sensorService.ReadAsync();

        Console.WriteLine($"{count + 1}번째 read");
        Console.WriteLine($"Humidity: {reading.Humidity}");
        Console.WriteLine($"Temperature: {reading.Temperature}");
        Console.WriteLine($"Measure: {reading.Timestamp}");
        Console.WriteLine($"RawResponse: {reading.RawResponse}");
        Console.WriteLine();

        TimeSpan delay = nextRunAt - DateTimeOffset.UtcNow;

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cts.Token);
        }

        count++;

        if (reading.Humidity > threshold.HumidityMax || reading.Humidity < threshold.HumidityMin)
        {
            alarmResult = alarmResult with { IsHumidityAlarm = true };
            alarmResult = alarmResult with { HasAlarm = true };
        }
        else if (reading.Temperature > threshold.TemperatureMax || reading.Temperature < threshold.HumidityMin)
        {
            alarmResult = alarmResult with { IsTemperatureAlarm = true};
            alarmResult = alarmResult with { HasAlarm = true };
        }
        
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Sampling stopped.");
}
catch (TimeoutException ex)
{
    Console.WriteLine($"Sensor timeout: {ex.Message}");
}
catch (FormatException ex)
{
    Console.WriteLine($"Parse failed: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Sensor read failed: {ex.Message}");
}