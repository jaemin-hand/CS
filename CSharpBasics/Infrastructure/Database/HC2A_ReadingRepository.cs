using CSharpBasics.Domain.Models;

namespace CSharpBasics.Infrastructure.Database;

public sealed class HC2A_ReadingRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public HC2A_ReadingRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public long Insert(string sensorName, HC2A_Reading reading)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO SensorReadings (
                SensorName,
                Timestamp,
                Temperature,
                Humidity,
                RawResponse
            )
            VALUES (
                $sensorName,
                $timestamp,
                $temperature,
                $humidity,
                $rawResponse
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$sensorName", sensorName);
        command.Parameters.AddWithValue("$timestamp", reading.Timestamp.ToString("O"));
        command.Parameters.AddWithValue("$temperature", reading.Temperature);
        command.Parameters.AddWithValue("$humidity", reading.Humidity);
        command.Parameters.AddWithValue("$rawResponse", reading.RawResponse);

        return Convert.ToInt64(command.ExecuteScalar());
    }

    public long Count()
    {
        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM SensorReadings;";

        return Convert.ToInt64(command.ExecuteScalar());
    }

    public IReadOnlyList<SensorReadingRow> GetLatest(int limit)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                Id,
                SensorName,
                Timestamp,
                Temperature,
                Humidity,
                RawResponse,
                CreatedAt
            FROM SensorReadings
            ORDER BY Id DESC
            LIMIT $limit;
            """;

        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var rows = new List<SensorReadingRow>();

        while (reader.Read())
        {
            rows.Add(new SensorReadingRow(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetString(5),
                reader.GetString(6)));
        }

        return rows;
    }
}
