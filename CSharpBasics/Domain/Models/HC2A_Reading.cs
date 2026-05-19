namespace CSharpBasics.Domain.Models;

public record HC2A_Reading(
    double Humidity,
    double Temperature,
    string RawResponse,
    DateTime Timestamp
);