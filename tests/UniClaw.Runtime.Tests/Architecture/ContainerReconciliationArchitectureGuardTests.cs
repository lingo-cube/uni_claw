using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

public sealed class ContainerReconciliationArchitectureGuardTests
{
    [Fact]
    public void PreparedContainerAcceptanceHasExactlyOneAgentCommitCallsite()
    {
        var containerSource = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Container/Container.cs"));
        var agentSource = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.ContainerReconciliation.cs"));

        Assert.Equal(1, containerSource.Split("AcceptPreparedObservation(", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, agentSource.Split("AcceptPreparedObservation(", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void CommitSeamContainsNoFallibleContainerValidationOrAsyncWork()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.ContainerReconciliation.cs"));
        var start = source.IndexOf("private void CommitContainerReconciliation", StringComparison.Ordinal);
        var end = source.IndexOf("    private bool TryCommitFreshContainerObservation", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var commit = source[start..end];

        Assert.DoesNotContain("TryVerify", commit, StringComparison.Ordinal);
        Assert.DoesNotContain("await", commit, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteLoweredAction", commit, StringComparison.Ordinal);
        Assert.DoesNotContain("Classify(", commit, StringComparison.Ordinal);
    }

    private static string RepoPath(string relative)
    {
        var directory = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(directory, ".git")))
        {
            var parent = Directory.GetParent(directory)?.FullName
                ?? throw new DirectoryNotFoundException("Unable to locate repository root.");
            directory = parent;
        }

        return Path.Combine(directory, relative);
    }
}
