using Aeron.Contracts;
using Aeron.Infrastructure;
using Aeron.Security;
using Xunit;

namespace Aeron.Core.Tests;

public sealed class SecurityCompositionTests
{
    [Fact]
    public async Task FoundationHost_ExposesExplicitlyUnavailableFailClosedPolicy()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aeron-policy-test-{Guid.NewGuid():N}");
        await using var host = AeronFoundationHost.Create(AppPaths.FromRoot(path));

        var result = host.SecurityPolicy.Evaluate(
            new SecurityRequest(SecurityAction.ReadApplicationState, DataSensitivity.Routine, PolicyDecisionEngine.CurrentPolicyVersion),
            new byte[] { 1 });

        Assert.Equal(PolicyDisposition.Deny, result.Disposition);
        Assert.Equal(PolicyReason.IdentityProviderUnavailable, result.Reason);
    }
}
