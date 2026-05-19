namespace CSharpBasics.Domain.Models;

public record HC2A_Threshold(
    double HumidityMin,
    double HumidityMax,
    double TemperatureMin,
    double TemperatureMax
);