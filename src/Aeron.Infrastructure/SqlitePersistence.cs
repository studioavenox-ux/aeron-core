using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Aeron.Contracts;
using Microsoft.Data.Sqlite;

namespace Aeron.Infrastructure;

/// <summary>
/// Represents a single forward-only SQLite migration verified by SHA-256 checksum.
/// </summary>
public sealed record SqliteMigration
{
    public SqliteMigration(int version, string name, string sql)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Migration version must be >= 1.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        Version = version;
        Name = name.Trim();
        Sql = NormalizeLineEndings(sql);
        ChecksumSha256 = ComputeChecksum(Sql);
    }

    public int Version { get; }
    public string Name { get; }
    public string Sql { get; }
    public string ChecksumSha256 { get; }

    public static string ComputeChecksum(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var normalized = NormalizeLineEndings(sql);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hash);
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
}

/// <summary>
/// Thrown when an already-applied migration's recorded SHA-256 checksum no longer matches
/// the bundled migration definition (fail-closed tamper/drift guard).
/// </summary>
public sealed class MigrationChecksumMismatchException : InvalidOperationException
{
    public MigrationChecksumMismatchException(
        int version,
        string name,
        string expectedChecksum,
        string recordedChecksum)
        : base($"SQLite migration v{version.ToString(CultureInfo.InvariantCulture)} ('{name}') failed checksum verification. Expected '{expectedChecksum}', found '{recordedChecksum}'.")
    {
        Version = version;
        MigrationName = name;
        ExpectedChecksum = expectedChecksum;
        RecordedChecksum = recordedChecksum;
    }

    public int Version { get; }
    public string MigrationName { get; }
    public string ExpectedChecksum { get; }
    public string RecordedChecksum { get; }
}

/// <summary>
/// Opens configured local SQLite connections (WAL mode, foreign_keys=ON, busy_timeout,
/// non-pooled deterministic handle release, and current-user Windows ACL protection).
/// </summary>
public sealed class SqliteConnectionFactory
{
    public const int DefaultBusyTimeoutMs = 5000;

    private readonly IAppPaths _paths;

    public SqliteConnectionFactory(IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    public IAppPaths Paths => _paths;

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreatedAndSecured();

        var existedBefore = File.Exists(_paths.DatabaseFilePath);
        var connectionString = BuildConnectionString(_paths.DatabaseFilePath, SqliteOpenMode.ReadWriteCreate);
        var connection = new SqliteConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using (var pragmaCmd = connection.CreateCommand())
            {
                pragmaCmd.CommandText = $"""
                    PRAGMA foreign_keys = ON;
                    PRAGMA journal_mode = WAL;
                    PRAGMA synchronous = NORMAL;
                    PRAGMA busy_timeout = {DefaultBusyTimeoutMs.ToString(CultureInfo.InvariantCulture)};
                    """;
                await pragmaCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!existedBefore && OperatingSystem.IsWindows() && File.Exists(_paths.DatabaseFilePath))
            {
                WindowsAclHelper.SecureFile(_paths.DatabaseFilePath);
            }

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal static string BuildConnectionString(string databaseFilePath, SqliteOpenMode mode)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            Mode = mode,
            Cache = SqliteCacheMode.Default,
            ForeignKeys = true,
            DefaultTimeout = 5,
            Pooling = false
        };

        return builder.ToString();
    }
}

/// <summary>
/// Executes checksum-verified, transactional, forward-only SQLite schema migrations
/// with automatic pre-upgrade backup snapshots.
/// </summary>
public sealed class SqliteMigrationRunner
{
    private const string MigrationResourcePrefix = "Aeron.Infrastructure.Migrations.";

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IReadOnlyList<SqliteMigration> _migrations;
    private readonly IClock _clock;
    private readonly IAeronLogger? _logger;

    public SqliteMigrationRunner(
        SqliteConnectionFactory connectionFactory,
        IClock? clock = null,
        IAeronLogger? logger = null,
        IReadOnlyList<SqliteMigration>? migrationsOverride = null)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);

        _connectionFactory = connectionFactory;
        _clock = clock ?? SystemClock.Instance;
        _logger = logger;
        _migrations = ValidateAndSortMigrations(migrationsOverride ?? LoadBuiltInMigrations());
    }

    public static IReadOnlyList<SqliteMigration> LoadBuiltInMigrations()
    {
        var assembly = typeof(SqliteMigrationRunner).Assembly;
        var resourceNames = assembly
            .GetManifestResourceNames()
            .Where(static name =>
                name.StartsWith(MigrationResourcePrefix, StringComparison.Ordinal) &&
                name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        if (resourceNames.Length == 0)
        {
            throw new InvalidOperationException("No embedded SQLite migrations were found in Aeron.Infrastructure.");
        }

        var list = new List<SqliteMigration>(resourceNames.Length);

        foreach (var resourceName in resourceNames)
        {
            var fileStem = Path.GetFileNameWithoutExtension(resourceName[MigrationResourcePrefix.Length..]);
            var separatorIndex = fileStem.IndexOf('_', StringComparison.Ordinal);
            if (separatorIndex <= 0 ||
                !int.TryParse(fileStem[..separatorIndex], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            {
                throw new InvalidOperationException(
                    $"Invalid migration resource name '{resourceName}'. Expected '<NNN>_<name>.sql'.");
            }

            var name = fileStem[(separatorIndex + 1)..];
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Unable to read migration resource '{resourceName}'.");
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var sql = reader.ReadToEnd();

            list.Add(new SqliteMigration(version, name, sql));
        }

        return ValidateAndSortMigrations(list);
    }

    public async Task<int> MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await EnsureSchemaMigrationsTableAsync(connection, cancellationToken).ConfigureAwait(false);

        var recorded = await LoadRecordedMigrationsAsync(connection, cancellationToken).ConfigureAwait(false);

        // Fail-closed: verify no unknown future schema versions or modified checksums exist.
        var knownByVersion = _migrations.ToDictionary(static m => m.Version);
        foreach (var entry in recorded)
        {
            if (!knownByVersion.TryGetValue(entry.Key, out var expected))
            {
                throw new InvalidOperationException(
                    $"Database contains applied migration version '{entry.Key.ToString(CultureInfo.InvariantCulture)}' unknown to this application build.");
            }

            if (!string.Equals(expected.ChecksumSha256, entry.Value.Checksum, StringComparison.Ordinal))
            {
                _logger?.LogCritical(
                    nameof(SqliteMigrationRunner),
                    $"Migration checksum mismatch detected for v{expected.Version.ToString(CultureInfo.InvariantCulture)} ({expected.Name}).");
                throw new MigrationChecksumMismatchException(
                    expected.Version,
                    expected.Name,
                    expected.ChecksumSha256,
                    entry.Value.Checksum);
            }
        }

        var pending = _migrations
            .Where(migration => !recorded.ContainsKey(migration.Version))
            .ToArray();

        if (pending.Length == 0)
        {
            return 0;
        }

        // Create a timestamped backup before applying new migrations onto an existing schema.
        if (recorded.Count > 0)
        {
            var latestApplied = recorded.Keys.Max();
            CreatePreUpgradeBackup(connection, latestApplied);
        }

        var appliedCount = 0;
        foreach (var migration in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var transaction = (SqliteTransaction)await connection
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await using (var applyCmd = connection.CreateCommand())
                {
                    applyCmd.Transaction = transaction;
                    applyCmd.CommandText = migration.Sql;
                    await applyCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await using (var recordCmd = connection.CreateCommand())
                {
                    recordCmd.Transaction = transaction;
                    recordCmd.CommandText = """
                        INSERT INTO schema_migrations (version, name, checksum_sha256, applied_at_utc)
                        VALUES ($version, $name, $checksum, $appliedAtUtc);
                        """;
                    recordCmd.Parameters.AddWithValue("$version", migration.Version);
                    recordCmd.Parameters.AddWithValue("$name", migration.Name);
                    recordCmd.Parameters.AddWithValue("$checksum", migration.ChecksumSha256);
                    recordCmd.Parameters.AddWithValue(
                        "$appliedAtUtc",
                        _clock.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                    await recordCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                appliedCount++;

                _logger?.LogInfo(
                    nameof(SqliteMigrationRunner),
                    $"Applied SQLite migration v{migration.Version.ToString(CultureInfo.InvariantCulture)} ({migration.Name}).");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                _logger?.LogError(
                    nameof(SqliteMigrationRunner),
                    $"Rolled back failed SQLite migration v{migration.Version.ToString(CultureInfo.InvariantCulture)} ({migration.Name}).",
                    ex);
                throw;
            }
        }

        return appliedCount;
    }

    private void CreatePreUpgradeBackup(SqliteConnection sourceConnection, int currentSchemaVersion)
    {
        var stamp = _clock.UtcNow.UtcDateTime.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var backupFileName =
            $"aeron_core.pre-upgrade-from-v{currentSchemaVersion.ToString("D3", CultureInfo.InvariantCulture)}.{stamp}.{Guid.NewGuid():N}.db";
        var backupPath = Path.Combine(_connectionFactory.Paths.BackupsDirectory, backupFileName);

        var backupConnectionString = SqliteConnectionFactory.BuildConnectionString(
            backupPath,
            SqliteOpenMode.ReadWriteCreate);

        using (var backupConnection = new SqliteConnection(backupConnectionString))
        {
            backupConnection.Open();
            sourceConnection.BackupDatabase(backupConnection);
        }

        if (OperatingSystem.IsWindows() && File.Exists(backupPath))
        {
            WindowsAclHelper.SecureFile(backupPath);
        }

        _logger?.LogInfo(
            nameof(SqliteMigrationRunner),
            $"Created pre-migration SQLite backup prior to upgrading from schema v{currentSchemaVersion.ToString(CultureInfo.InvariantCulture)}.");
    }

    private static async Task EnsureSchemaMigrationsTableAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY NOT NULL,
                name TEXT NOT NULL,
                checksum_sha256 TEXT NOT NULL,
                applied_at_utc TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Dictionary<int, (string Name, string Checksum)>> LoadRecordedMigrationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, (string Name, string Checksum)>();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT version, name, checksum_sha256 FROM schema_migrations ORDER BY version ASC;";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var version = reader.GetInt32(0);
            var name = reader.GetString(1);
            var checksum = reader.GetString(2);
            result[version] = (name, checksum);
        }

        return result;
    }

    private static IReadOnlyList<SqliteMigration> ValidateAndSortMigrations(IReadOnlyList<SqliteMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        if (migrations.Count == 0)
        {
            throw new ArgumentException("At least one SQLite migration must be provided.", nameof(migrations));
        }

        var sorted = migrations.OrderBy(static m => m.Version).ToArray();
        var seen = new HashSet<int>();
        foreach (var migration in sorted)
        {
            if (!seen.Add(migration.Version))
            {
                throw new ArgumentException(
                    $"Duplicate SQLite migration version '{migration.Version.ToString(CultureInfo.InvariantCulture)}'.",
                    nameof(migrations));
            }
        }

        return sorted;
    }
}

/// <summary>
/// High-level SQLite initialization, verified health probing, and redacted lifecycle journaling.
/// </summary>
public sealed class SqliteDatabaseService : IDatabaseInitializer, IDatabaseHealthProbe, ILifecycleJournalStore
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteMigrationRunner _migrationRunner;
    private readonly IRedactionFilter _redactionFilter;

    public SqliteDatabaseService(
        SqliteConnectionFactory connectionFactory,
        SqliteMigrationRunner migrationRunner,
        IRedactionFilter redactionFilter)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(migrationRunner);
        ArgumentNullException.ThrowIfNull(redactionFilter);

        _connectionFactory = connectionFactory;
        _migrationRunner = migrationRunner;
        _redactionFilter = redactionFilter;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _migrationRunner.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DatabaseHealthResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_connectionFactory.Paths.DatabaseFilePath))
            {
                return new DatabaseHealthResult(
                    IsHealthy: false,
                    JournalMode: "uninitialized",
                    ForeignKeysEnabled: false,
                    IntegrityOk: false,
                    AppliedMigrationCount: 0,
                    LatestSchemaVersion: 0,
                    StatusMessage: "Database file has not been initialized yet.");
            }

            await using var connection = await _connectionFactory
                .OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);

            string journalMode;
            await using (var journalCmd = connection.CreateCommand())
            {
                journalCmd.CommandText = "PRAGMA journal_mode;";
                journalMode = Convert.ToString(
                                  await journalCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                                  CultureInfo.InvariantCulture) ?? "unknown";
            }

            long foreignKeysValue;
            await using (var fkCmd = connection.CreateCommand())
            {
                fkCmd.CommandText = "PRAGMA foreign_keys;";
                foreignKeysValue = Convert.ToInt64(
                    await fkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                    CultureInfo.InvariantCulture);
            }

            string integrity;
            await using (var integrityCmd = connection.CreateCommand())
            {
                integrityCmd.CommandText = "PRAGMA quick_check(1);";
                integrity = Convert.ToString(
                                await integrityCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                                CultureInfo.InvariantCulture) ?? "failed";
            }

            var migrationCount = 0;
            var latestVersion = 0;
            await using (var migCmd = connection.CreateCommand())
            {
                migCmd.CommandText = "SELECT COUNT(*), COALESCE(MAX(version), 0) FROM schema_migrations;";
                await using var reader = await migCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    migrationCount = reader.GetInt32(0);
                    latestVersion = reader.GetInt32(1);
                }
            }

            var isWal = string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase);
            var fkEnabled = foreignKeysValue == 1;
            var integrityOk = string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase);
            var isHealthy = isWal && fkEnabled && integrityOk && migrationCount >= 1 && latestVersion >= 1;

            var status = isHealthy
                ? $"Healthy (WAL, FK=ON, Schema v{latestVersion.ToString(CultureInfo.InvariantCulture)}, {migrationCount.ToString(CultureInfo.InvariantCulture)} migration(s))"
                : $"Degraded (Journal={journalMode}, FK={fkEnabled}, Integrity={integrity}, Migrations={migrationCount.ToString(CultureInfo.InvariantCulture)})";

            return new DatabaseHealthResult(
                IsHealthy: isHealthy,
                JournalMode: journalMode.ToLowerInvariant(),
                ForeignKeysEnabled: fkEnabled,
                IntegrityOk: integrityOk,
                AppliedMigrationCount: migrationCount,
                LatestSchemaVersion: latestVersion,
                StatusMessage: status);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            return new DatabaseHealthResult(
                IsHealthy: false,
                JournalMode: "error",
                ForeignKeysEnabled: false,
                IntegrityOk: false,
                AppliedMigrationCount: 0,
                LatestSchemaVersion: 0,
                StatusMessage: $"Database probe failed: {_redactionFilter.Redact(ex.Message)}");
        }
    }

    public async Task RecordTransitionAsync(
        string instanceId,
        AppLifecycleState state,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var safeInstanceId = _redactionFilter.Redact(instanceId);
        var safeReason = _redactionFilter.Redact(reason);
        var isoUtc = occurredAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = """
                INSERT INTO lifecycle_journal (instance_id, state, reason, occurred_at_utc)
                VALUES ($instanceId, $state, $reason, $occurredAtUtc);
                """;
            insertCmd.Parameters.AddWithValue("$instanceId", safeInstanceId);
            insertCmd.Parameters.AddWithValue("$state", state.ToString());
            insertCmd.Parameters.AddWithValue("$reason", safeReason);
            insertCmd.Parameters.AddWithValue("$occurredAtUtc", isoUtc);
            await insertCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await UpsertMetaAsync(connection, transaction, "last_instance_id", safeInstanceId, isoUtc, cancellationToken)
            .ConfigureAwait(false);
        await UpsertMetaAsync(connection, transaction, "last_lifecycle_state", state.ToString(), isoUtc, cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertMetaAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        string value,
        string updatedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO app_instance_meta (key, value, updated_at_utc)
            VALUES ($key, $value, $updatedAtUtc)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_at_utc = excluded.updated_at_utc;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.Parameters.AddWithValue("$updatedAtUtc", updatedAtUtc);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
