using CSharpBasics.Application.Services;
using CSharpBasics.Domain.Models;
using CSharpBasics.Infrastructure.Backup;
using CSharpBasics.Infrastructure.Database;
using CSharpBasics.Infrastructure.Export;

var databasePath = GetOptionValue(args, "--db-path") ??
                   Path.Combine(AppContext.BaseDirectory, "data", "monitoring.db");

if (args.Contains("--wal-file-info"))
{
    PrintWalFileInfo(databasePath);
    return;
}

var connectionFactory = new SqliteConnectionFactory(databasePath);
var databaseInitializer = new DatabaseInitializer(connectionFactory);
var readingRepository = new HC2A_ReadingRepository(connectionFactory);

var databaseStatus = databaseInitializer.Initialize();

Console.WriteLine($"DB Path: {databaseStatus.DatabasePath}");
Console.WriteLine($"DB Journal Mode: {databaseStatus.JournalMode}");
Console.WriteLine($"DB Integrity Check: {databaseStatus.IntegrityCheck}");
Console.WriteLine($"DB Existing Readings: {readingRepository.Count()}");
Console.WriteLine();

int? sampleLimit = null;
var sampleCountOptionIndex = Array.IndexOf(args, "--sample-count");

if (sampleCountOptionIndex >= 0 &&
    sampleCountOptionIndex + 1 < args.Length &&
    int.TryParse(args[sampleCountOptionIndex + 1], out var parsedSampleLimit) &&
    parsedSampleLimit > 0)
{
    sampleLimit = parsedSampleLimit;
}

if (args.Contains("--db-smoke-test"))
{
    var beforeCount = readingRepository.Count();
    var testReading = new HC2A_Reading(
        Humidity: 45.2,
        Temperature: 22.4,
        RawResponse: "SMOKE_TEST",
        Timestamp: DateTime.Now);

    var insertedId = readingRepository.Insert("CH1", testReading);
    var afterCount = readingRepository.Count();
    var afterStatus = databaseInitializer.Initialize();

    Console.WriteLine("DB smoke test completed.");
    Console.WriteLine($"Inserted Id: {insertedId}");
    Console.WriteLine($"Before Count: {beforeCount}");
    Console.WriteLine($"After Count: {afterCount}");
    Console.WriteLine($"Journal Mode: {afterStatus.JournalMode}");
    Console.WriteLine($"Integrity Check: {afterStatus.IntegrityCheck}");
    return;
}

if (args.Contains("--db-check"))
{
    var checkStatus = databaseInitializer.Initialize();

    Console.WriteLine("DB check completed.");
    Console.WriteLine($"Total Readings: {readingRepository.Count()}");
    Console.WriteLine($"Journal Mode: {checkStatus.JournalMode}");
    Console.WriteLine($"Integrity Check: {checkStatus.IntegrityCheck}");
    return;
}

if (args.Contains("--db-list"))
{
    var limit = GetIntOptionValue(args, "--db-list", 10);
    var rows = readingRepository.GetLatest(limit);

    Console.WriteLine($"Latest {rows.Count} reading row(s)");
    Console.WriteLine("Columns: Id | SensorName | Timestamp | Temperature | Humidity | RawResponse | CreatedAt");

    foreach (var row in rows)
    {
        Console.WriteLine(
            $"{row.Id} | " +
            $"{row.SensorName} | " +
            $"{row.Timestamp} | " +
            $"{row.Temperature} | " +
            $"{row.Humidity} | " +
            $"{NormalizeRawResponse(row.RawResponse)} | " +
            $"{row.CreatedAt}");
    }

    return;
}

if (args.Contains("--db-crash-writer"))
{
    var writeCount = 0;

    while (true)
    {
        var testReading = new HC2A_Reading(
            Humidity: 40 + writeCount % 20,
            Temperature: 20 + writeCount % 10,
            RawResponse: "CRASH_WRITER",
            Timestamp: DateTime.Now);

        readingRepository.Insert("CH1", testReading);
        writeCount++;

        Thread.Sleep(50);
    }
}

if (args.Contains("--db-insert-benchmark"))
{
    var sampleCount = GetIntOptionValue(args, "--samples", 100);
    var channelCount = GetIntOptionValue(args, "--channels", 10);
    var totalInsertCount = sampleCount * channelCount;

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
    {
        for (var channelIndex = 1; channelIndex <= channelCount; channelIndex++)
        {
            var reading = new HC2A_Reading(
                Humidity: 50 + channelIndex * 0.1,
                Temperature: 25 + channelIndex * 0.1,
                RawResponse: $"BENCHMARK_SAMPLE_{sampleIndex}_CH_{channelIndex}",
                Timestamp: DateTime.Now);

            readingRepository.Insert($"CH{channelIndex}", reading);
        }
    }

    stopwatch.Stop();

    var totalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
    var averageInsertMilliseconds = totalMilliseconds / totalInsertCount;
    var averageTenChannelSampleMilliseconds = totalMilliseconds / sampleCount;

    Console.WriteLine("DB insert benchmark completed.");
    Console.WriteLine($"Samples: {sampleCount}");
    Console.WriteLine($"Channels per sample: {channelCount}");
    Console.WriteLine($"Total inserts: {totalInsertCount}");
    Console.WriteLine($"Total elapsed: {totalMilliseconds:F2} ms");
    Console.WriteLine($"Average per insert: {averageInsertMilliseconds:F4} ms");
    Console.WriteLine($"Average per {channelCount}-channel sample: {averageTenChannelSampleMilliseconds:F4} ms");
    Console.WriteLine($"Estimated inserts per second: {1000 / averageInsertMilliseconds:F0}");
    return;
}

if (args.Contains("--backup"))
{
    var backupService = new SqliteBackupService(connectionFactory);
    var backupFile = GetOptionValue(args, "--backup-file");
    var backupResult = backupFile is not null
        ? backupService.BackupToFile(backupFile)
        : backupService.BackupToDirectory(
            GetOptionValue(args, "--backup-dir") ??
            Path.Combine(AppContext.BaseDirectory, "data", "backups"));

    Console.WriteLine("SQLite backup completed.");
    Console.WriteLine($"Backup Path: {backupResult.BackupFilePath}");
    Console.WriteLine($"Backup Size: {backupResult.BackupFileSizeBytes} bytes");
    Console.WriteLine($"Backup Rows: {backupResult.RowCount}");
    Console.WriteLine($"Backup Integrity Check: {backupResult.IntegrityCheck}");
    Console.WriteLine($"Created At: {backupResult.CreatedAt:yyyy-MM-dd HH:mm:ss.fff}");
    return;
}

if (args.Contains("--csv-export"))
{
    var exportService = new CsvExportService(connectionFactory);
    var csvPath = Path.Combine(
        AppContext.BaseDirectory,
        "data",
        "csv",
        $"sensor_readings_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

    var exportResult = exportService.ExportAllSensorReadings(csvPath);

    Console.WriteLine("CSV export completed.");
    Console.WriteLine($"CSV Path: {exportResult.FilePath}");
    Console.WriteLine($"CSV Rows: {exportResult.RowCount}");
    return;
}

using var sensorService = new HC2A_SensorService("COM4");

var threshold = new HC2A_Threshold(
    30,
    60,
    20,
    30,
    0,
    0
);

var alarmService = new HC2A_AlarmService();

Console.WriteLine($"Humidity range: {threshold.HumidityMin} ~ {threshold.HumidityMax}");
Console.WriteLine($"Temperature range: {threshold.TemperatureMin} ~ {threshold.TemperatureMax}");

using var cts = new CancellationTokenSource();

if (sampleLimit is null)
{
    _ = Task.Run(() =>
    {
        Console.WriteLine("Press Enter to stop.");
        Console.ReadLine();
        cts.Cancel();
    });
}
else
{
    Console.WriteLine($"Sampling will stop after {sampleLimit.Value} read(s).");
}

try
{
    sensorService.Open();

    Console.WriteLine($"IsOpen: {sensorService.IsOpen}");

    TimeSpan sampleInterval = TimeSpan.FromSeconds(1);
    DateTimeOffset nextRunAt = DateTimeOffset.UtcNow;

    int count = 0;
    while (!cts.Token.IsCancellationRequested &&
           (sampleLimit is null || count < sampleLimit.Value))
    {
        nextRunAt += sampleInterval;

        var reading = await sensorService.ReadAsync();
        var alarmResult = alarmService.Check(reading, threshold);
        var readingId = readingRepository.Insert("CH1", reading);
        var totalReadingCount = readingRepository.Count();

        Console.WriteLine($"{count + 1} read");
        Console.WriteLine($"DB Insert Id: {readingId}");
        Console.WriteLine($"DB Total Readings: {totalReadingCount}");
        Console.WriteLine($"Humidity: {reading.Humidity}");
        Console.WriteLine($"Temperature: {reading.Temperature}");
        Console.WriteLine($"Measure: {reading.Timestamp}");
        Console.WriteLine($"RawResponse: {reading.RawResponse}");
        Console.WriteLine($"Temperature alarm: {alarmResult.IsTemperatureAlarm}");
        Console.WriteLine($"Humidity alarm: {alarmResult.IsHumidityAlarm}");
        Console.WriteLine($"AlarmMessage: {alarmResult.Message}");

        Console.WriteLine();

        TimeSpan delay = nextRunAt - DateTimeOffset.UtcNow;

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cts.Token);
        }

        count++;
    }

    if (sampleLimit is not null)
    {
        var finalDatabaseStatus = databaseInitializer.Initialize();

        Console.WriteLine($"Sampling completed. Total reads in this run: {count}");
        Console.WriteLine($"DB Integrity After Sampling: {finalDatabaseStatus.IntegrityCheck}");
        Console.WriteLine($"DB Journal Mode After Sampling: {finalDatabaseStatus.JournalMode}");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Sampling stopped.");
}
catch (TimeoutException ex)
{
    Console.WriteLine($"Sensor timeout: {ex.Message}");
}
catch (FormatException ex)
{
    Console.WriteLine($"Parse failed: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Sensor read failed: {ex.Message}");
}

static string? GetOptionValue(string[] args, string optionName)
{
    var optionIndex = Array.IndexOf(args, optionName);

    if (optionIndex < 0 || optionIndex + 1 >= args.Length)
    {
        return null;
    }

    return args[optionIndex + 1];
}

static int GetIntOptionValue(string[] args, string optionName, int defaultValue)
{
    var value = GetOptionValue(args, optionName);

    if (int.TryParse(value, out var parsedValue) && parsedValue > 0)
    {
        return parsedValue;
    }

    return defaultValue;
}

static string NormalizeRawResponse(string rawResponse)
{
    return rawResponse
        .Replace("\r", "\\r")
        .Replace("\n", "\\n");
}

static void PrintWalFileInfo(string databasePath)
{
    var fullDatabasePath = Path.GetFullPath(databasePath);
    var walPath = fullDatabasePath + "-wal";
    var shmPath = fullDatabasePath + "-shm";

    Console.WriteLine("WAL file inspection");
    Console.WriteLine("This command does not open SQLite. It only reads file metadata/header.");
    Console.WriteLine();

    PrintFileInfo("DB", fullDatabasePath);
    PrintFileInfo("WAL", walPath);
    PrintFileInfo("SHM", shmPath);

    if (!File.Exists(walPath))
    {
        Console.WriteLine();
        Console.WriteLine("WAL file does not currently exist.");
        Console.WriteLine("This can happen after a clean checkpoint. Run while writing, or after a forced process kill.");
        return;
    }

    var walInfo = new FileInfo(walPath);

    if (walInfo.Length < 32)
    {
        Console.WriteLine();
        Console.WriteLine("WAL file exists, but it is smaller than the 32-byte WAL header.");
        return;
    }

    var header = ReadFirstBytes(walPath, 32);
    var magic = ReadUInt32BigEndian(header, 0);
    var version = ReadUInt32BigEndian(header, 4);
    var pageSize = ReadUInt32BigEndian(header, 8);
    var checkpointSequence = ReadUInt32BigEndian(header, 12);

    Console.WriteLine();
    Console.WriteLine($"WAL Header Hex: {Convert.ToHexString(header)}");
    Console.WriteLine($"WAL Magic: 0x{magic:X8}");
    Console.WriteLine($"WAL Version: {version}");
    Console.WriteLine($"WAL Page Size: {pageSize}");
    Console.WriteLine($"WAL Checkpoint Sequence: {checkpointSequence}");
    Console.WriteLine();
    Console.WriteLine("If WAL length is greater than 32 bytes, committed frames may exist in the WAL file.");
    Console.WriteLine("SQLite applies those frames automatically when the DB is opened.");
}

static void PrintFileInfo(string label, string path)
{
    var fileInfo = new FileInfo(path);

    if (!fileInfo.Exists)
    {
        Console.WriteLine($"{label}: not found ({path})");
        return;
    }

    Console.WriteLine($"{label}: {fileInfo.FullName}");
    Console.WriteLine($"  Size: {fileInfo.Length} bytes");
    Console.WriteLine($"  LastWriteTime: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss.fff}");
}

static byte[] ReadFirstBytes(string path, int byteCount)
{
    var buffer = new byte[byteCount];

    using var stream = new FileStream(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete);

    var totalRead = 0;

    while (totalRead < buffer.Length)
    {
        var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);

        if (read == 0)
        {
            break;
        }

        totalRead += read;
    }

    return buffer;
}

static uint ReadUInt32BigEndian(byte[] bytes, int offset)
{
    return ((uint)bytes[offset] << 24) |
           ((uint)bytes[offset + 1] << 16) |
           ((uint)bytes[offset + 2] << 8) |
           bytes[offset + 3];
}
