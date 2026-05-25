using CSharpBasics.Domain.Models;

namespace CSharpBasics.Application.Services;

public class HC2A_AlarmService
{
    public HC2A_AlarmResult Check(HC2A_Reading reading, HC2A_Threshold threshold)
    {
        bool isHumidityAlarm = 
            reading.Humidity < threshold.HumidityMin ||
            reading.Humidity > threshold.HumidityMax;

        bool isTemperatureAlarm = 
            reading.Temperature < threshold.TemperatureMin ||
            reading.Temperature > threshold.TemperatureMax;

        bool hasAlarm = isHumidityAlarm || isTemperatureAlarm;

        string message;
        if (isHumidityAlarm && isTemperatureAlarm)
        {
            message = "Humidity and temperature";
        }
        else if (isHumidityAlarm)
        {
            message = "Humidity alarm";
        }
        else if (isTemperatureAlarm)
        {
            message = "Temperature alarm";
        }
        else
        {
            message = "Normal";
        }

        return new HC2A_AlarmResult(
            isHumidityAlarm,
            isTemperatureAlarm,
            hasAlarm,
            message
        );
    }
}