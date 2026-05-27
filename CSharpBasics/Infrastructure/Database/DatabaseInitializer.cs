namespace CSharpBasics.Infrastructure.Database;

public sealed class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public DatabaseInitializer(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public DatabaseStatus Initialize()
    {
        using var connection = _connectionFactory.OpenConnection();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;

                CREATE TABLE IF NOT EXISTS SensorReadings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SensorName TEXT NOT NULL,
                    Timestamp TEXT NOT NULL,
                    Temperature REAL NOT NULL,
                    Humidity REAL NOT NULL,
                    RawResponse TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                );

                CREATE INDEX IF NOT EXISTS IX_SensorReadings_Timestamp
                ON SensorReadings (Timestamp);
                """;

            command.ExecuteNonQuery();
        }

        var journalMode = ExecuteScalarText("PRAGMA journal_mode;");
        var integrityCheck = ExecuteScalarText("PRAGMA integrity_check;");

        return new DatabaseStatus(
            _connectionFactory.DatabasePath,
            journalMode,
            integrityCheck);
    }

    private string ExecuteScalarText(string commandText)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = commandText;

        return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
    }
}
