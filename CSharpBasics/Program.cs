using CSharpBasics.Application.Services;

using var sensorService = new HC2A_SensorService("COM4");

try
{
    sensorService.Open();
    
    Console.WriteLine($"IsOpen: {sensorService.IsOpen}");

    for (int i = 0; i < 10; i++)
    {
        var reading = await sensorService.ReadAsync();

        Console.WriteLine($"{i + 1} 번째 read");
        Console.WriteLine($"Humidity: {reading.Humidity}");
        Console.WriteLine($"Temperature: {reading.Temperature}");
        Console.WriteLine($"Measure: {reading.Timestamp}");
        Console.WriteLine($"RawResponse: {reading.RawResponse}");

        await Task.Delay(500);
    }
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