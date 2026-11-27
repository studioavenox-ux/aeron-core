using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Durable approval store.
///
/// Each approval is one JSON document under the approval directory, written atomically
/// (temp file + flush + replace) and bound to the exact operation through
/// <see cref="ApprovalRecord.BindingHash"/>. State transitions use compare-and-set under
/// a process lock, so a single-use transition can win exactly once even under concurrency.
///
/// Deliberate limitations, stated rather than hidden:
/// * Raw payloads are never stored; only the keyed binding hash.
/// * The store is process-local serialized. Cross-process exclusion and role separation
///   between approve and consume are design decisions reserved for the Owner (register
///   entry D2-08); until then durability protects against restart, not hostile processes
///   running as the same user.
/// * A record that cannot be parsed fails the entry closed (treated as missing) rather
///   than being silently repaired.
/// </summary>
public sealed class DurableApprovalStore : IApprovalStore
{
    private const int MaxRecords = 4096;
    private const string FileExtension = ".approval.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        AllowTrailingCommas = false
    };

    private readonly Lock _sync = new();
    private readonly string _directory;

    public DurableApprovalStore(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        _directory = directoryPath;
        Directory.CreateDirectory(_directory);
    }

    public bool TryInsert(ApprovalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.ApprovalId == Guid.Empty ||
            !Enum.IsDefined(record.Action) || record.Action == SecurityAction.Unknown ||
            !Enum.IsDefined(record.Sensitivity) || record.Sensitivity == DataSensitivity.Unknown ||
            string.IsNullOrWhiteSpace(record.Target) || record.Target.Length > OperationIdentity.MaxTargetLength ||
            string.IsNullOrWhiteSpace(record.ActorSubject) || record.ActorSubject.Length > VerifiedActor.MaxSubjectLength ||
            record.ActorKind is not (IdentityKind.Owner or IdentityKind.Agent) ||
            string.IsNullOrWhiteSpace(record.PolicyVersion) || record.PolicyVersion.Length > SecurityPolicyVersion.MaxLength ||
            record.BindingHash.Length != 32 ||
            record.ExpiresAtUtc <= record.CreatedAtUtc ||
            record.State != ApprovalLifecycleState.Pending)
        {
            return false;
        }

        lock (_sync)
        {
            try
            {
                if (Directory.EnumerateFiles(_directory, "*" + FileExtension).Take(MaxRecords).Count() >= MaxRecords)
                {
                    return false;
                }

                var path = PathFor(record.ApprovalId);

                if (File.Exists(path))
                {
                    return false;
                }

                return WriteAtomic(path, record);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public bool TryGet(Guid approvalId, out ApprovalRecord? record)
    {
        record = null;

        if (approvalId == Guid.Empty)
        {
            return false;
        }

        lock (_sync)
        {
            return TryReadLocked(approvalId, out record);
        }
    }

    public bool TryTransition(Guid approvalId, ApprovalLifecycleState expected, ApprovalLifecycleState next)
    {
        if (approvalId == Guid.Empty || !Enum.IsDefined(expected) || !Enum.IsDefined(next) || expected == next)
        {
            return false;
        }

        lock (_sync)
        {
            if (!TryReadLocked(approvalId, out var record) || record is null || record.State != expected)
            {
                return false;
            }

            return WriteAtomic(PathFor(approvalId), record with { State = next });
        }
    }

    public bool TryConsume(Guid approvalId, Guid consumerSessionId)
    {
        if (approvalId == Guid.Empty || consumerSessionId == Guid.Empty)
        {
            return false;
        }

        lock (_sync)
        {
            if (!TryReadLocked(approvalId, out var record) || record is null ||
                record.State != ApprovalLifecycleState.Approved)
            {
                return false;
            }

            return WriteAtomic(
                PathFor(approvalId),
                record with { State = ApprovalLifecycleState.Consumed, ConsumedBySessionId = consumerSessionId });
        }
    }

    public IReadOnlyList<ApprovalRecord> ListPending(DateTimeOffset utcNow, int maxRecords)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRecords, 1);

        lock (_sync)
        {
            var results = new List<ApprovalRecord>();

            try
            {
                foreach (var path in Directory.EnumerateFiles(_directory, "*" + FileExtension))
                {
                    if (results.Count >= maxRecords)
                    {
                        break;
                    }

                    if (TryReadPath(path, out var record) &&
                        record is not null &&
                        record.State is ApprovalLifecycleState.Pending or ApprovalLifecycleState.Approved &&
                        record.ExpiresAtUtc > utcNow)
                    {
                        results.Add(record);
                    }
                }
            }
            catch (Exception)
            {
                return [];
            }

            return results.OrderBy(static record => record.ExpiresAtUtc).ToArray();
        }
    }

    private bool TryReadLocked(Guid approvalId, out ApprovalRecord? record)
    {
        record = null;
        var path = PathFor(approvalId);

        if (!File.Exists(path))
        {
            return false;
        }

        return TryReadPath(path, out record) && record is not null;
    }

    private static bool TryReadPath(string path, out ApprovalRecord? record)
    {
        record = null;

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);

            if (json.Length > 8192)
            {
                return false;
            }

            var stored = JsonSerializer.Deserialize<StoredApproval>(json, SerializerOptions);

            if (stored is null)
            {
                return false;
            }

            record = stored.ToRecord();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool WriteAtomic(string path, ApprovalRecord record)
    {
        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N")[..8];

        try
        {
            var json = JsonSerializer.Serialize(StoredApproval.From(record), SerializerOptions);

            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, overwrite: true);
            return true;
        }
        catch (Exception)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (Exception)
            {
                // Best-effort cleanup only.
            }

            return false;
        }
    }

    private string PathFor(Guid approvalId) =>
        Path.Combine(_directory, approvalId.ToString("N") + FileExtension);

    private sealed class StoredApproval
    {
        public Guid ApprovalId { get; set; }
        public int Action { get; set; }
        public int Sensitivity { get; set; }
        public string Target { get; set; } = string.Empty;
        public string ActorSubject { get; set; } = string.Empty;
        public int ActorKind { get; set; }
        public string PolicyVersion { get; set; } = string.Empty;
        public string BindingHash { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string ExpiresAtUtc { get; set; } = string.Empty;
        public int State { get; set; }
        public Guid? ConsumedBySessionId { get; set; }

        public static StoredApproval From(ApprovalRecord record) => new()
        {
            ApprovalId = record.ApprovalId,
            Action = (int)record.Action,
            Sensitivity = (int)record.Sensitivity,
            Target = record.Target,
            ActorSubject = record.ActorSubject,
            ActorKind = (int)record.ActorKind,
            PolicyVersion = record.PolicyVersion,
            BindingHash = Convert.ToBase64String(record.BindingHash),
            CreatedAtUtc = record.CreatedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            ExpiresAtUtc = record.ExpiresAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            State = (int)record.State,
            ConsumedBySessionId = record.ConsumedBySessionId
        };

        public ApprovalRecord ToRecord()
        {
            var binding = Convert.FromBase64String(BindingHash);

            if (binding.Length != 32)
            {
                throw new InvalidDataException("Approval binding hash has an invalid length.");
            }

            var createdAt = DateTimeOffset.Parse(CreatedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var expiresAt = DateTimeOffset.Parse(ExpiresAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            return new ApprovalRecord(
                ApprovalId,
                (SecurityAction)Action,
                (DataSensitivity)Sensitivity,
                Target,
                ActorSubject,
                (IdentityKind)ActorKind,
                PolicyVersion,
                binding,
                createdAt,
                expiresAt,
                (ApprovalLifecycleState)State,
                ConsumedBySessionId);
        }
    }
}

/// <summary>
/// Process-local approve/consume role separation registry.
///
/// The approved Owner UI registers its trust marker through the desktop composition; the
/// approval engine requires that marker to accept an Owner decision. Background services
/// and agents do not possess it, so they cannot approve their own requests. This is a
/// same-user, same-process boundary — it is not OS isolation, and the Owner isolation
/// mechanism decision (register entry D2-01) remains open.
/// </summary>
public sealed class ApproverTrustRegistry
{
    private readonly Lock _sync = new();
    private byte[]? _approvedMarkerHash;

    public bool TryRegisterApprover(ReadOnlySpan<byte> marker)
    {
        if (marker.Length < 32 || marker.Length > 512)
        {
            return false;
        }

        lock (_sync)
        {
            if (_approvedMarkerHash is not null)
            {
                return false;
            }

            _approvedMarkerHash = SHA256.HashData(marker);
            return true;
        }
    }

    public bool IsRegisteredApprover(ReadOnlySpan<byte> marker)
    {
        if (marker.Length < 32 || marker.Length > 512)
        {
            return false;
        }

        byte[]? expected;

        lock (_sync)
        {
            expected = _approvedMarkerHash;
        }

        if (expected is null)
        {
            return false;
        }

        var actual = SHA256.HashData(marker);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public bool HasRegisteredApprover
    {
        get
        {
            lock (_sync)
            {
                return _approvedMarkerHash is not null;
            }
        }
    }
}
