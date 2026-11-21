using System.Text;
using Aeron.Contracts;
using Xunit;

namespace Aeron.Security.Tests;

public sealed class OperationBindingTests
{
    [Fact]
    public void ExactOperationChangesInvalidateBindingEvenWhenDisplaysRedactTheSame()
    {
        using var binding = new OperationBinding();
        var id = Guid.NewGuid();
        var original = Encoding.UTF8.GetBytes("token=synthetic-alpha");
        var modified = Encoding.UTF8.GetBytes("token=synthetic-bravo");
        var digest = binding.Compute(id, SecurityAction.DiscloseData, "agent-a", "destination-a", "policy-1", original);

        Assert.True(binding.Verify(digest, id, SecurityAction.DiscloseData, "agent-a", "destination-a", "policy-1", original));
        Assert.False(binding.Verify(digest, id, SecurityAction.DiscloseData, "agent-a", "destination-a", "policy-1", modified));
        Assert.False(binding.Verify(digest, Guid.NewGuid(), SecurityAction.DiscloseData, "agent-a", "destination-a", "policy-1", original));
        Assert.False(binding.Verify(digest, id, SecurityAction.ModifyExternalAccount, "agent-a", "destination-a", "policy-1", original));
        Assert.False(binding.Verify(digest, id, SecurityAction.DiscloseData, "agent-b", "destination-a", "policy-1", original));
        Assert.False(binding.Verify(digest, id, SecurityAction.DiscloseData, "agent-a", "destination-b", "policy-1", original));
        Assert.False(binding.Verify(digest, id, SecurityAction.DiscloseData, "agent-a", "destination-a", "policy-2", original));
        Assert.DoesNotContain("synthetic-alpha", Convert.ToHexString(digest), StringComparison.Ordinal);
    }

    [Fact]
    public void LengthFramingPreventsAmbiguousFieldConcatenation()
    {
        using var binding = new OperationBinding();
        var id = Guid.NewGuid();
        var digest = binding.Compute(id, SecurityAction.DiscloseData, "a", "bc", "v1", []);
        Assert.False(binding.Verify(digest, id, SecurityAction.DiscloseData, "ab", "c", "v1", []));
    }

    [Fact]
    public void FreshInstanceCannotVerifyAnotherInstancesBinding()
    {
        using var first = new OperationBinding();
        using var second = new OperationBinding();
        var id = Guid.NewGuid();
        var digest = first.Compute(id, SecurityAction.DiscloseData, "actor", "target", "v1", []);
        Assert.False(second.Verify(digest, id, SecurityAction.DiscloseData, "actor", "target", "v1", []));
    }

    [Fact]
    public void InvalidUnicodeIsRejectedRatherThanCanonicalizedToReplacementCharacters()
    {
        using var binding = new OperationBinding();
        Assert.Throws<EncoderFallbackException>(() =>
            binding.Compute(Guid.NewGuid(), SecurityAction.DiscloseData, "actor", "\ud800", "v1", []));
    }

    [Fact]
    public void DisposalInvalidatesFutureBindings()
    {
        var binding = new OperationBinding();
        binding.Dispose();
        Assert.Throws<ObjectDisposedException>(() =>
            binding.Compute(Guid.NewGuid(), SecurityAction.DiscloseData, "actor", "target", "v1", []));
    }
}
