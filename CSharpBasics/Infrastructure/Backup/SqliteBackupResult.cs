namespace CSharpBasics.Infrastructure.Backup;

public record SqliteBackupResult(
    string BackupFilePath,
    long BackupFileSizeBytes,
    long RowCount,
    string IntegrityCheck,
    DateTime CreatedAt
);
