using System.Globalization;
using System.Text;
using CSharpBasics.Infrastructure.Database;

namespace CSharpBasics.Infrastructure.Export;

public sealed class CsvExportService
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public CsvExportService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public CsvExportResult ExportAllSensorReadings(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                Id,
                SensorName,
                Timestamp,
                Temperature,
                Humidity,
                RawResponse
            FROM SensorReadings
            ORDER BY Id;
            """;

        using var reader = command.ExecuteReader();
        using var writer = new StreamWriter(fullPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        writer.WriteLine("Id,SensorName,Timestamp,Temperature,Humidity,RawResponse");

        long rowCount = 0;

        while (reader.Read())
        {
            writer.Write(reader.GetInt64(0).ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(Escape(reader.GetString(1)));
            writer.Write(',');
            writer.Write(Escape(reader.GetString(2)));
            writer.Write(',');
            writer.Write(reader.GetDouble(3).ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(reader.GetDouble(4).ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(Escape(reader.GetString(5)));
            writer.WriteLine();

            rowCount++;
        }

        return new CsvExportResult(fullPath, rowCount);
    }

    private static string Escape(string value)
    {
        value = value
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");

        if (!value.Contains(',') &&
            !value.Contains('"') &&
            !value.Contains('\r') &&
            !value.Contains('\n'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
