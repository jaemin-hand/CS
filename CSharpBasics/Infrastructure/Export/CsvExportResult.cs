namespace CSharpBasics.Infrastructure.Export;

public record CsvExportResult(
    string FilePath,
    long RowCount
);
