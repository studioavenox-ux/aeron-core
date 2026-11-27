using System.IO;
using System.Security.Cryptography;
using System.Text;
using Aeron.Contracts;
using Aeron.Security;
using Xunit;

namespace Aeron.TestSupport;

/// <summary>Settable clock shared by kernel tests.</summary>
public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

/// <summary>
/// One fully-wired security kernel plus a temp directory that is deleted on dispose.
/// Every test gets an independent ledger, checkpoint, approval store, and identity root.
/// </summary>
public sealed class KernelHarness : IDisposable
{
    public const string OwnerSid = "S-1-5-21-4000-5000-6000-1001";

    /// <summary>The synthetic Owner subject used by the harness and its test factor policy.</summary>
    public const string OwnerSubject = TestApprovedFactorPolicy.TestOwnerSubject;

    private readonly string _root;

    public KernelHarness(
        bool authenticateOwner = true,
        IOwnerFactorPolicy? factorPolicy = null,
        string? rootOverride = null)
    {
        _root = rootOverride ?? Path.Combine(Path.GetTempPath(), $"aeron-m2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        Clock = new TestClock();
        Accounts = new FixedAccountSource(OwnerSid);
        Policy = factorPolicy ?? new TestApprovedFactorPolicy();
        Kernel = SecurityKernel.Create(CreateOptions(_root, Clock, Policy, Accounts));

        OwnerMarker = RandomNumberGenerator.GetBytes(48);
        Kernel.Approvers.TryRegisterApprover(OwnerMarker);

        OwnerSessionId = Guid.Empty;

        if (authenticateOwner)
        {
            EnrollAndAuthenticateOwner();
        }
    }

    public TestClock Clock { get; }

    public FixedAccountSource Accounts { get; }

    public IOwnerFactorPolicy Policy { get; }

    public SecurityKernel Kernel { get; }

    public byte[] OwnerMarker { get; }

    public Guid OwnerSessionId { get; private set; }

    public string Root => _root;

    /// <summary>Builds explicit, fully-specified kernel options for a test sandbox.</summary>
    public static SecurityKernelOptions CreateOptions(
        string rootDirectory,
        IClock clock,
        IOwnerFactorPolicy? factorPolicy = null,
        IWindowsAccountIdentitySource? accountSource = null,
        IAgentSignatureVerifier? signatureVerifier = null,
        SessionPolicy? sessionPolicy = null,
        byte[]? bindingKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(clock);

        Directory.CreateDirectory(rootDirectory);

        return new SecurityKernelOptions
        {
            AuditFilePath = Path.Combine(rootDirectory, "audit.auditlog"),
            ApprovalDirectory = Path.Combine(rootDirectory, "approvals"),
            CheckpointStore = new FileAuditCheckpointStore(Path.Combine(rootDirectory, "audit.head")),
            AccountSource = accountSource ?? new FixedAccountSource(OwnerSid),
            FactorPolicy = factorPolicy ?? new TestApprovedFactorPolicy(),
            SignatureVerifier = signatureVerifier ?? new EcdsaAgentSignatureVerifier(),
            NonceStore = new InMemoryNonceStore(),
            Clock = clock,
            SessionPolicy = sessionPolicy ?? SessionPolicy.Default,
            BindingKey = bindingKey
        };
    }

    /// <summary>
    /// Session evidence is the raw 16 bytes of the session id: the authority reparses it
    /// into a Guid and rejects any other length, so encoding it as text would never work.
    /// </summary>
    public static byte[] SessionEvidence(Guid sessionId) => sessionId.ToByteArray();

    public byte[] OwnerEvidence => SessionEvidence(OwnerSessionId);

    public void EnrollAndAuthenticateOwner()
    {
        var enrollment = Kernel.IdentityAuthority.EnrollOwner(
            new OwnerEnrollmentRequest(OwnerSubject, OwnerSid));
        Assert.Equal("ENROLLED_NO_SESSION", enrollment.ErrorCode);

        var session = AuthenticateOwner();
        OwnerSessionId = session.SessionId;
    }

    public AuthenticatedSession AuthenticateOwner(string factor = TestApprovedFactorPolicy.TestFactorValue)
    {
        var request = new OwnerAuthenticationRequest(
            new WindowsAccountIdentity(OwnerSid),
            [new OwnerFactorEvidence(TestApprovedFactorPolicy.TestFactorId, Encoding.UTF8.GetBytes(factor))],
            SecurityPolicyEvaluator.M2PolicyVersion);

        var result = Kernel.IdentityAuthority.AuthenticateOwner(request);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return result.Value;
    }

    public void Grant(string subjectId, IdentityKind kind, SecurityCapability capability)
    {
        var result = new PermissionAdministration(Kernel.Permissions, Kernel.IdentityAuthority, Kernel.Audit)
            .Grant(OwnerEvidence, subjectId, kind, capability);
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    public SecurityKernel RestartKernel()
    {
        Kernel.Dispose();
        var restarted = SecurityKernel.Create(CreateOptions(_root, Clock, Policy, Accounts));
        restarted.Approvers.TryRegisterApprover(OwnerMarker);
        return restarted;
    }

    public void Dispose()
    {
        Kernel.Dispose();

        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }
}

/// <summary>Test/support account source that reports a fixed OS account observation.</summary>
public sealed class FixedAccountSource : IWindowsAccountIdentitySource
{
    private readonly string _sid;

    public FixedAccountSource(string sid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sid);
        _sid = sid;
    }

    public bool TryGetCurrentAccount(out WindowsAccountIdentity? account)
    {
        account = new WindowsAccountIdentity(_sid);
        return true;
    }
}

/// <summary>
/// Explicitly-approved test factor policy for automated tests only. It accepts exactly one
/// synthetic factor value; production composition never registers it.
/// </summary>
public sealed class TestApprovedFactorPolicy : IOwnerFactorPolicy
{
    public const string TestFactorId = "test-factor";
    public const string TestFactorValue = "synthetic-approved-factor";

    /// <summary>
    /// The subject this policy reports as verified. It must equal the enrolled Owner
    /// subject; a mismatch is a legitimate authentication failure, so the constant is
    /// shared rather than duplicated as a literal.
    /// </summary>
    public const string TestOwnerSubject = "owner-subject";

    public string PolicyId => "test-only";

    public bool IsApproved => true;

    public bool TryVerifyFactors(
        WindowsAccountIdentity account,
        IReadOnlyList<OwnerFactorEvidence> factors,
        out string? verifiedSubjectId)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(factors);

        verifiedSubjectId = null;

        foreach (var factor in factors)
        {
            if (string.Equals(factor.FactorId, TestFactorId, StringComparison.Ordinal) &&
                Encoding.UTF8.GetString(factor.Evidence) == TestFactorValue)
            {
                verifiedSubjectId = TestOwnerSubject;
                return true;
            }
        }

        return false;
    }
}

/// <summary>Agent identity with its own signing key so isolation is exercised for real.</summary>
public sealed class TestAgent : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public TestAgent(string agentId)
    {
        AgentId = agentId;
    }

    public string AgentId { get; }

    public byte[] PublicKey => _key.ExportSubjectPublicKeyInfo();

    public AgentRegistration Registration => new(
        AgentId,
        "Test agent " + AgentId,
        PublicKey,
        new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
        IsRevoked: false);

    public AgentAuthenticationRequest Authenticate(byte[]? nonce = null)
    {
        var challenge = nonce ?? RandomNumberGenerator.GetBytes(32);
        var signature = _key.SignData(challenge, HashAlgorithmName.SHA256);

        return new AgentAuthenticationRequest(AgentId, challenge, signature, SecurityPolicyEvaluator.M2PolicyVersion);
    }

    public void Dispose() => _key.Dispose();
}
