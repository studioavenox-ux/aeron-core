using System.Text;
using System.Text.Json;
using Aeron.Contracts;
using Aeron.Security;

namespace Aeron.Infrastructure;

/// <summary>
/// Persists AERON's secret-free typed configuration using atomic write-replace,
/// Windows current-user ACLs, and fail-closed security policy validation.
/// </summary>
public sealed class SecureConfigStore : ISecureConfigStore
{
    private static readonly Encoding StrictUtf8NoBom = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 16
    };

    private readonly Lock _sync = new();
    private readonly IAppPaths _paths;
    private readonly ConfigPolicyValidator _validator;
    private readonly IRedactionFilter _redactionFilter;
    private readonly IAeronLogger? _logger;

    public SecureConfigStore(
        IAppPaths paths,
        ConfigPolicyValidator validator,
        IRedactionFilter redactionFilter,
        IAeronLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(redactionFilter);

        _paths = paths;
        _validator = validator;
        _redactionFilter = redactionFilter;
        _logger = logger;
    }

    public Result<AeronAppConfig> LoadOrCreateDefault()
    {
        lock (_sync)
        {
            try
            {
                _paths.EnsureCreatedAndSecured();

                if (!File.Exists(_paths.ConfigFilePath))
                {
                    var defaultConfig = AeronAppConfig.CreateDefault();
                    var writeResult = WriteAtomicallyCore(defaultConfig);
                    if (writeResult.IsFailure)
                    {
                        return Result<AeronAppConfig>.Failure(writeResult.ErrorCode, writeResult.ErrorMessage);
                    }

                    _logger?.LogInfo(nameof(SecureConfigStore), "Initialized default AERON configuration.");
                    return Result<AeronAppConfig>.Success(defaultConfig);
                }

                var fileInfo = new FileInfo(_paths.ConfigFilePath);
                if (fileInfo.Length > ConfigPolicyValidator.MaxRawConfigBytes)
                {
                    return FailClosed(
                        ConfigPolicyValidator.ErrorCodes.EmptyOrOversized,
                        "Configuration file exceeds the maximum allowed byte ceiling.");
                }

                if (OperatingSystem.IsWindows())
                {
                    WindowsAclHelper.SecureFile(_paths.ConfigFilePath);
                }

                var rawJson = File.ReadAllText(_paths.ConfigFilePath, StrictUtf8NoBom);

                var rawValidation = _validator.ValidateRawJson(rawJson);
                if (rawValidation.IsFailure)
                {
                    return FailClosed(rawValidation.ErrorCode, rawValidation.ErrorMessage);
                }

                AeronAppConfig? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<AeronAppConfig>(rawJson, SerializerOptions);
                }
                catch (JsonException)
                {
                    return FailClosed(
                        ConfigPolicyValidator.ErrorCodes.InvalidJson,
                        "Configuration file could not be deserialized cleanly.");
                }

                var modelValidation = _validator.ValidateConfig(parsed);
                if (modelValidation.IsFailure)
                {
                    return FailClosed(modelValidation.ErrorCode, modelValidation.ErrorMessage);
                }

                return Result<AeronAppConfig>.Success(parsed!);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                var safeMessage = _redactionFilter.Redact(ex.Message);
                _logger?.LogError(nameof(SecureConfigStore), "Failed to read configuration file safely.", ex);
                return Result<AeronAppConfig>.Failure("CONFIG_IO_ERROR", $"Configuration I/O failure: {safeMessage}");
            }
        }
    }

    public Result Save(AeronAppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (_sync)
        {
            return WriteAtomicallyCore(config);
        }
    }

    private Result WriteAtomicallyCore(AeronAppConfig config)
    {
        var modelValidation = _validator.ValidateConfig(config);
        if (modelValidation.IsFailure)
        {
            _logger?.LogWarning(
                nameof(SecureConfigStore),
                $"Blocked invalid configuration save ({modelValidation.ErrorCode}).");
            return modelValidation;
        }

        var serialized = JsonSerializer.Serialize(config, SerializerOptions) + Environment.NewLine;

        var rawValidation = _validator.ValidateRawJson(serialized);
        if (rawValidation.IsFailure)
        {
            _logger?.LogWarning(
                nameof(SecureConfigStore),
                $"Blocked serialized configuration containing prohibited content ({rawValidation.ErrorCode}).");
            return rawValidation;
        }

        _paths.EnsureCreatedAndSecured();

        var tempFilePath = Path.Combine(
            _paths.ConfigDirectory,
            $"{AppPaths.ConfigFileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            var payload = StrictUtf8NoBom.GetBytes(serialized);

            using (var stream = new FileStream(
                       tempFilePath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                if (OperatingSystem.IsWindows())
                {
                    WindowsAclHelper.SecureFile(tempFilePath);
                }

                stream.Write(payload, 0, payload.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempFilePath, _paths.ConfigFilePath, overwrite: true);

            if (OperatingSystem.IsWindows())
            {
                WindowsAclHelper.SecureFile(_paths.ConfigFilePath);
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var safeMessage = _redactionFilter.Redact(ex.Message);
            _logger?.LogError(nameof(SecureConfigStore), "Atomic configuration write failed.", ex);
            return Result.Failure("CONFIG_WRITE_FAILED", $"Configuration write failed: {safeMessage}");
        }
        finally
        {
            TryDeleteTempFile(tempFilePath);
        }
    }

    private Result<AeronAppConfig> FailClosed(string errorCode, string message)
    {
        var redacted = _redactionFilter.Redact(message);
        _logger?.LogError(
            nameof(SecureConfigStore),
            $"Configuration rejected fail-closed ({errorCode}): {redacted}");
        return Result<AeronAppConfig>.Failure(errorCode, redacted);
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of abandoned temp file.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of abandoned temp file.
        }
    }
}
