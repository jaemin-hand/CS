namespace CSharpBasics.Infrastructure.Database;

public record SensorReadingRow(
    long Id,
    string SensorName,
    string Timestamp,
    double Temperature,
    double Humidity,
    string RawResponse,
    string CreatedAt
);
