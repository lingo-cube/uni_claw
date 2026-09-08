using UniClaw.Kernel.Run;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// RUN-001：真实 Primary Run Identity。RunId = accepted Contract View 的
/// 内容派生（同构 EvidenceId / ContainerIdentity 内容寻址先例），
/// AdmitContract 首次接受时铸造、此后 immutable。
/// </summary>
public sealed class RunIdentityTests
{
    private static ExecutionContract Contract(string objective = "obj", string version = "v1") => new(
        version,
        objective,
        new HashSet<string> { "app" },
        new HashSet<string> { "effect.ui" },
        new HashSet<string> { "effect.fs" },
        new List<string> { "criterion" });

    [Fact]
    public void SameContract_AcrossInstances_MintsSameRunId()
    {
        var a = new RunModel();
        var b = new RunModel();

        Assert.True(a.AdmitContract(Contract()).Accepted);
        Assert.True(b.AdmitContract(Contract()).Accepted);

        Assert.Equal(a.RunId, b.RunId);
    }

    [Fact]
    public void RunIdIsContentDerived_NotPlaceholder()
    {
        var run = new RunModel();

        Assert.True(run.AdmitContract(Contract()).Accepted);

        Assert.StartsWith("run-", run.RunId);
        Assert.True(run.RunId.Length > "run-".Length);
        Assert.NotEqual("run-1", run.RunId);
    }

    [Fact]
    public void DifferentContract_MintsDifferentRunId()
    {
        var a = new RunModel();
        var b = new RunModel();

        Assert.True(a.AdmitContract(Contract(objective: "obj-a")).Accepted);
        Assert.True(b.AdmitContract(Contract(objective: "obj-b")).Accepted);

        Assert.NotEqual(a.RunId, b.RunId);
    }

    [Fact]
    public void SetInsertionOrder_DoesNotChangeRunId()
    {
        var a = new RunModel();
        var b = new RunModel();

        Assert.True(a.AdmitContract(new ExecutionContract(
            "v1", "obj",
            new HashSet<string> { "app", "web" },
            new HashSet<string> { "effect.ui" },
            new HashSet<string> { "effect.fs" },
            new List<string> { "c" })).Accepted);
        Assert.True(b.AdmitContract(new ExecutionContract(
            "v1", "obj",
            new HashSet<string> { "web", "app" }, // 相同成员、不同插入序
            new HashSet<string> { "effect.ui" },
            new HashSet<string> { "effect.fs" },
            new List<string> { "c" })).Accepted);

        Assert.Equal(a.RunId, b.RunId);
    }

    [Fact]
    public void ReAdmitSameVersion_DoesNotRemint()
    {
        var run = new RunModel();

        Assert.True(run.AdmitContract(Contract()).Accepted);
        var first = run.RunId;

        Assert.True(run.AdmitContract(Contract()).Accepted); // 幂等 re-admit

        Assert.Equal(first, run.RunId);
    }
}
