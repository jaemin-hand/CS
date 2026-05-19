namespace CSharpBasics.Domain.Models;

public record HC2A_AlarmResult(
    bool IsHumidityAlarm,
    bool IsTemperatureAlarm,
    bool HasAlarm,
    string Message
);

