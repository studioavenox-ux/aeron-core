using System.Globalization;
using System.Text;
using System.Text.Json;
using Aeron.Contracts;
using Aeron.Security;

namespace Aeron.Infrastructure;

/// <summary>
/// Immutable redacted log entry stored in the bounded in-memory ring buffer.
/// </summary>
public sealed record RedactedLogEntry(
    DateTimeOffset TimestampUtc,
    AeronLogLevel Level,
    string Category,
    string Message);

/// <summary>
/// Unconditionally redacting structured JSON-lines (JSONL) logger with size-based file rotation
/// and current-user Windows file ACL enforcement.
/// </summary>
public sealed class RedactingLogger : IAeronLogger
{
    public const int MinimumTestRotationBytes = 512;
    private const int DefaultRecentCapacity = 100;

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock _sync = new();
    private readonly IAppPaths _paths;
    private readonly IRedactionFilter _redactionFilter;
    private readonly IClock _clock;
    private readonly Queue<RedactedLogEntry> _recentEntries = new(DefaultRecentCapacity);

    private FileStream? _activeStream;
    private AeronLogLevel _minimumLevel;
    private long _maxFileBytes;
    private int _maxRetainedFiles;
    private bool _disposed;

    public RedactingLogger(
        IAppPaths paths,
        IRedactionFilter redactionFilter,
        IClock? clock = null,
        AeronLogLevel minimumLevel = AeronLogLevel.Information,
        long maxFileBytes = LoggingPreferences.DefaultMaxFileBytes,
        int maxRetainedFiles = LoggingPreferences.DefaultMaxRetainedFiles)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(redactionFilter);

        if (maxFileBytes < MinimumTestRotationBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFileBytes),
                maxFileBytes,
                $"maxFileBytes must be at least {MinimumTestRotationBytes} bytes.");
        }

        if (maxRetainedFiles < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRetainedFiles),
                maxRetainedFiles,
                "maxRetainedFiles must be at least 1.");
        }

        _paths = paths;
        _redactionFilter = redactionFilter;
        _clock = clock ?? SystemClock.Instance;
        _minimumLevel = minimumLevel;
        _maxFileBytes = maxFileBytes;
        _maxRetainedFiles = maxRetainedFiles;
    }

    public bool IsActive
    {
        get
        {
            lock (_sync)
            {
                return !_disposed;
            }
        }
    }

    public IReadOnlyList<RedactedLogEntry> GetRecentEntries()
    {
        lock (_sync)
        {
            return _recentEntries.ToArray();
        }
    }

    public void Configure(AeronLogLevel minimumLevel, long maxFileBytes, int maxRetainedFiles)
    {
        if (!Enum.IsDefined(minimumLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLevel));
        }

        if (maxFileBytes < MinimumTestRotationBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
        }

        if (maxRetainedFiles < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetainedFiles));
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _minimumLevel = minimumLevel;
            _maxFileBytes = maxFileBytes;
            _maxRetainedFiles = maxRetainedFiles;
        }
    }

    public void Log(
        AeronLogLevel level,
        string category,
        string message,
        IReadOnlyDictionary<string, string?>? properties = null,
        Exception? exception = null)
    {
        lock (_sync)
        {
            if (_disposed || level < _minimumLevel)
            {
                return;
            }

            var timestamp = _clock.UtcNow;
            var safeCategory = string.IsNullOrWhiteSpace(category)
                ? "General"
                : _redactionFilter.Redact(category);
            var safeMessage = _redactionFilter.Redact(message);

            var lineBytes = SerializeJsonLine(timestamp, level, safeCategory, safeMessage, properties, exception);

            RecordInMemory(new RedactedLogEntry(timestamp, level, safeCategory, safeMessage));
            WriteLineWithRotation(lineBytes);
        }
    }

    public void Flush()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _activeStream?.Flush(flushToDisk: true);
            }
            catch (IOException)
            {
                // Logger flush must never crash host shutdown.
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CloseStreamSafe();
        }
    }

    private byte[] SerializeJsonLine(
        DateTimeOffset timestamp,
        AeronLogLevel level,
        string safeCategory,
        string safeMessage,
        IReadOnlyDictionary<string, string?>? properties,
        Exception? exception)
    {
        using var buffer = new MemoryStream(512);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("timestampUtc", timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("level", level.ToString());
            writer.WriteString("category", safeCategory);
            writer.WriteString("message", safeMessage);

            if (properties is { Count: > 0 })
            {
                writer.WriteStartObject("properties");
                foreach (var pair in properties)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    var key = _redactionFilter.Redact(pair.Key);
                    var value = RedactionFilter.IsSensitiveKeyName(pair.Key)
                        ? RedactionFilter.RedactedPlaceholder
                        : _redactionFilter.Redact(pair.Value);

                    writer.WriteString(key, value);
                }

                writer.WriteEndObject();
            }

            if (exception is not null)
            {
                writer.WritePropertyName("exception");
                WriteRedactedException(writer, exception, depth: 0);
            }

            writer.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        return buffer.ToArray();
    }

    private void WriteRedactedException(Utf8JsonWriter writer, Exception exception, int depth)
    {
        const int MaxExceptionDepth = 8;

        writer.WriteStartObject();
        writer.WriteString("type", _redactionFilter.Redact(exception.GetType().FullName ?? exception.GetType().Name));
        writer.WriteString("message", _redactionFilter.Redact(exception.Message));

        if (!string.IsNullOrWhiteSpace(exception.StackTrace))
        {
            writer.WriteString("stackTrace", _redactionFilter.Redact(exception.StackTrace));
        }

        if (depth < MaxExceptionDepth)
        {
            if (exception is AggregateException aggregate && aggregate.InnerExceptions.Count > 0)
            {
                writer.WriteStartArray("innerExceptions");
                foreach (var inner in aggregate.InnerExceptions)
                {
                    WriteRedactedException(writer, inner, depth + 1);
                }

                writer.WriteEndArray();
            }
            else if (exception.InnerException is not null)
            {
                writer.WritePropertyName("innerException");
                WriteRedactedException(writer, exception.InnerException, depth + 1);
            }
        }

        writer.WriteEndObject();
    }

    private void WriteLineWithRotation(byte[] lineBytes)
    {
        try
        {
            EnsureStreamOpen();
            if (_activeStream is null)
            {
                return;
            }

            if (_activeStream.Length > 0 && _activeStream.Length + lineBytes.Length > _maxFileBytes)
            {
                RotateLogFiles();
                EnsureStreamOpen();
                if (_activeStream is null)
                {
                    return;
                }
            }

            _activeStream.Write(lineBytes, 0, lineBytes.Length);
            _activeStream.Flush();
        }
        catch (IOException)
        {
            CloseStreamSafe();
        }
        catch (UnauthorizedAccessException)
        {
            CloseStreamSafe();
        }
    }

    private void EnsureStreamOpen()
    {
        if (_activeStream is not null)
        {
            return;
        }

        _paths.EnsureCreatedAndSecured();

        if (!File.Exists(_paths.ActiveLogFilePath))
        {
            using (File.Create(_paths.ActiveLogFilePath))
            {
            }

            if (OperatingSystem.IsWindows())
            {
                WindowsAclHelper.SecureFile(_paths.ActiveLogFilePath);
            }
        }

        _activeStream = new FileStream(
            _paths.ActiveLogFilePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.None);

        _ = Utf8NoBom;
    }

    private void RotateLogFiles()
    {
        CloseStreamSafe();

        var baseName = Path.GetFileNameWithoutExtension(AppPaths.ActiveLogFileName);
        var extension = Path.GetExtension(AppPaths.ActiveLogFileName);

        string ArchivePath(int index) =>
            Path.Combine(_paths.LogsDirectory, $"{baseName}.{index.ToString(CultureInfo.InvariantCulture)}{extension}");

        // Delete any archives beyond retention ceiling.
        var oldestAllowed = ArchivePath(_maxRetainedFiles);
        if (File.Exists(oldestAllowed))
        {
            File.Delete(oldestAllowed);
        }

        for (var i = _maxRetainedFiles - 1; i >= 1; i--)
        {
            var source = ArchivePath(i);
            var destination = ArchivePath(i + 1);
            if (File.Exists(source))
            {
                File.Move(source, destination, overwrite: true);
            }
        }

        if (File.Exists(_paths.ActiveLogFilePath))
        {
            var firstArchive = ArchivePath(1);
            File.Move(_paths.ActiveLogFilePath, firstArchive, overwrite: true);
            if (OperatingSystem.IsWindows())
            {
                WindowsAclHelper.SecureFile(firstArchive);
            }
        }

        // Prune stale overflow files if MaxRetainedFiles was lowered via reconfiguration.
        foreach (var file in Directory.GetFiles(_paths.LogsDirectory, $"{baseName}.*{extension}"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var suffix = fileName[(baseName.Length + 1)..];
            if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var archiveNumber) &&
                archiveNumber > _maxRetainedFiles)
            {
                File.Delete(file);
            }
        }
    }

    private void RecordInMemory(RedactedLogEntry entry)
    {
        if (_recentEntries.Count >= DefaultRecentCapacity)
        {
            _recentEntries.Dequeue();
        }

        _recentEntries.Enqueue(entry);
    }

    private void CloseStreamSafe()
    {
        if (_activeStream is null)
        {
            return;
        }

        try
        {
            _activeStream.Flush();
            _activeStream.Dispose();
        }
        catch (IOException)
        {
            // Ignore close-time disk errors.
        }
        finally
        {
            _activeStream = null;
        }
    }
}
