using Microsoft.Data.Sqlite;
using CSharpBasics.Infrastructure.Database;

namespace CSharpBasics.Infrastructure.Backup;

public sealed class SqliteBackupService
{
    private readonly SqliteConnectionFactory _sourceConnectionFactory;

    public SqliteBackupService(SqliteConnectionFactory sourceConnectionFactory)
    {
        _sourceConnectionFactory = sourceConnectionFactory;
    }

    public SqliteBackupResult BackupToDirectory(string backupDirectory)
    {
        var fullBackupDirectory = Path.GetFullPath(backupDirectory);
        Directory.CreateDirectory(fullBackupDirectory);

        var createdAt = DateTime.Now;
        var fileName = $"monitoring_backup_{createdAt:yyyyMMdd_HHmmss_fff}.db";
        return BackupToFile(Path.Combine(fullBackupDirectory, fileName), createdAt);
    }

    public SqliteBackupResult BackupToFile(string backupFilePath)
    {
        return BackupToFile(backupFilePath, DateTime.Now);
    }

    private SqliteBackupResult BackupToFile(string backupFilePath, DateTime createdAt)
    {
        backupFilePath = Path.GetFullPath(backupFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);

        var temporaryBackupFilePath = backupFilePath + ".tmp";

        if (File.Exists(temporaryBackupFilePath))
        {
            File.Delete(temporaryBackupFilePath);
        }

        using (var sourceConnection = _sourceConnectionFactory.OpenConnection())
        {
            var destinationConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = temporaryBackupFilePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString();

            using var destinationConnection = new SqliteConnection(destinationConnectionString);
            destinationConnection.Open();

            sourceConnection.BackupDatabase(destinationConnection);
        }

        var integrityCheck = ExecuteScalarText(temporaryBackupFilePath, "PRAGMA integrity_check;");

        if (!integrityCheck.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(temporaryBackupFilePath);
            throw new InvalidOperationException($"Backup integrity check failed: {integrityCheck}");
        }

        var rowCount = Convert.ToInt64(ExecuteScalarText(temporaryBackupFilePath, "SELECT COUNT(*) FROM SensorReadings;"));

        if (File.Exists(backupFilePath))
        {
            File.Delete(backupFilePath);
        }

        SqliteConnection.ClearAllPools();

        File.Move(temporaryBackupFilePath, backupFilePath);
        DeleteIfExists(temporaryBackupFilePath + "-wal");
        DeleteIfExists(temporaryBackupFilePath + "-shm");

        var backupFileInfo = new FileInfo(backupFilePath);

        return new SqliteBackupResult(
            backupFileInfo.FullName,
            backupFileInfo.Length,
            rowCount,
            integrityCheck,
            createdAt);
    }

    private static string ExecuteScalarText(string databasePath, string commandText)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = commandText;

        return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
