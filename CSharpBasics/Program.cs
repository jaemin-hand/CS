using System.Runtime.CompilerServices;
using CSharpBasics.Application.Services;
using CSharpBasics.Domain.Models;

using var sensorService = new HC2A_SensorService("COM4");

var threshold = new HC2A_Threshold(
    30,
    60,
    20,
    30,
    0,
    0
);

var alarmResult = new HC2A_AlarmResult(
    false,
    false,
    false,
    "Normal"
);

Console.WriteLine($"Humidity range: {threshold.HumidityMin} ~ {threshold.HumidityMax}");
Console.WriteLine($"Temperature range: {threshold.TemperatureMin} ~ {threshold.TemperatureMax}");


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
        Console.WriteLine($"Temperature alarm: {alarmResult.IsTemperatureAlarm}");
        Console.WriteLine($"Humidity alarm: {alarmResult.IsHumidityAlarm}");
        Console.WriteLine($"AlarmMessage: {alarmResult.Message}");

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
            alarmResult = alarmResult with { Message = "HumidityAlarm!" };
            if (reading.Humidity > threshold.HumidityMax)
            {
                threshold = threshold with { PlusDiff = reading.Humidity - threshold.HumidityMax };
            }
            else if (reading.Humidity < threshold.HumidityMin)
            {
                threshold = threshold with { MinusDiff = threshold.HumidityMin - reading.Humidity };
            }
            else
            {
                threshold = threshold with { MinusDiff = 0 };
                threshold = threshold with { PlusDiff = 0 };
            }
        }
        else
        {
            alarmResult = alarmResult with { IsHumidityAlarm = false };
            alarmResult = alarmResult with { Message = "Normal" };

            threshold = threshold with { MinusDiff = 0 };
            threshold = threshold with { PlusDiff = 0 };
        }
        if (reading.Temperature > threshold.TemperatureMax || reading.Temperature < threshold.TemperatureMin)
        {
            alarmResult = alarmResult with { IsTemperatureAlarm = true};
            alarmResult = alarmResult with { Message = "TemperatureAlarm!" };

            if (reading.Temperature > threshold.TemperatureMax)
            {
                threshold = threshold with { PlusDiff = reading.Temperature - threshold.TemperatureMax };
            }
            else if (reading.Temperature < threshold.TemperatureMin)
            {
                threshold = threshold with { MinusDiff = threshold.TemperatureMin - reading.Temperature };
            }
            else
            {
                threshold = threshold with { MinusDiff = 0 };
                threshold = threshold with { PlusDiff = 0 };
            }
        }
        else
        {
            alarmResult = alarmResult with { IsTemperatureAlarm = false };
            if (alarmResult.Message != "HumidityAlarm!")
            alarmResult = alarmResult with { Message = "Normal" };
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