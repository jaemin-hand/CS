namespace CSharpBasics.Infrastructure.Database;

public record DatabaseStatus(
    string DatabasePath,
    string JournalMode,
    string IntegrityCheck
);
