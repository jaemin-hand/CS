using CSharpBasics.Domain.Models;

namespace CSharpBasics.Application.Services;

public class HC2A_AlarmService
{
    public HC2A_AlarmResult Check(HC2A_Reading reading, HC2A_Threshold threshold)
    {
        bool IsHumidityAlarm = 
            reading.Humidity < threshold.HumidityMin ||
            reading.Humidity > threshold.HumidityMax;

        bool IsTemperatureAlarm = 
            reading.Temperature < threshold.TemperatureMin ||
            reading.Temperature > threshold.TemperatureMax;

        bool hasAlarm = IsHumidityAlarm || IsTemperatureAlarm;

        string message = hasAlarm ? "Alarm" : "Normal";

        return new HC2A_AlarmResult(
            IsHumidityAlarm,
            IsTemperatureAlarm,
            hasAlarm,
            message
        );
    }
}