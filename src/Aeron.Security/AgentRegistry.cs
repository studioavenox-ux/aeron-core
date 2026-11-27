using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Volatile agent registry for M2. Registrations are Owner-provisioned at runtime and
/// revoked by the Owner; restart clears every registration (default deny). Revocation is
/// terminal for the current process: a revoked agent id cannot be re-registered without a
/// restart, which forces an explicit Owner review.
/// </summary>
public sealed class InMemoryAgentRegistry : IAgentRegistry
{
    public const int DefaultCapacity = 256;

    private readonly Lock _sync = new();
    private readonly Dictionary<string, AgentRegistration> _agents = new(StringComparer.Ordinal);
    private readonly HashSet<string> _revoked = new(StringComparer.Ordinal);
    private readonly int _capacity;

    public InMemoryAgentRegistry(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public bool TryGet(string agentId, out AgentRegistration? registration)
    {
        registration = null;

        if (!AgentRegistration.IsValidAgentId(agentId))
        {
            return false;
        }

        lock (_sync)
        {
            if (!_agents.TryGetValue(agentId, out var stored))
            {
                return false;
            }

            registration = stored with { IsRevoked = _revoked.Contains(agentId) };
            return true;
        }
    }

    public bool TryRegister(AgentRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!AgentRegistration.IsValidAgentId(registration.AgentId) ||
            !AgentRegistration.IsValidDisplayName(registration.DisplayName) ||
            registration.PublicKey.Length == 0 ||
            registration.PublicKey.Length > AgentRegistration.MaxPublicKeyBytes)
        {
            return false;
        }

        lock (_sync)
        {
            if (_revoked.Contains(registration.AgentId) || _agents.Count >= _capacity)
            {
                return false;
            }

            return _agents.TryAdd(registration.AgentId, registration with { IsRevoked = false });
        }
    }

    public bool TryRevoke(string agentId)
    {
        if (!AgentRegistration.IsValidAgentId(agentId))
        {
            return false;
        }

        lock (_sync)
        {
            if (!_agents.ContainsKey(agentId))
            {
                return false;
            }

            _revoked.Add(agentId);
            return true;
        }
    }

    public IReadOnlyList<string> ListAgentIds()
    {
        lock (_sync)
        {
            return _agents.Keys.OrderBy(static id => id, StringComparer.Ordinal).ToArray();
        }
    }

    internal int CountRevoked()
    {
        lock (_sync)
        {
            return _revoked.Count;
        }
    }
}
