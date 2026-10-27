using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

public sealed partial class KernelRunDriverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalAct_WaitingResumeAndCancellationNeverRedispatchOrRunAnUnsettledTail(bool cancel)
    {
        var container = ProbeContainerId("switch:primary@off");
        var plan = new AgentPlanPolicy();
        var releaseFirstPost = false;
        var firstPostConsumed = false;
        var cancelRequested = false;
        var consults = 0;
        var inputs = new RunDriverInputs
        {
            NextInput = directive =>
            {
                if (directive.Context == ObservationContext.External)
                    return new RunDriverInput.Observation([UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0)]);
                if (!releaseFirstPost || firstPostConsumed) return null;
                firstPostConsumed = true;
                return new RunDriverInput.Observation(
                    [PostActionObservation("switch:primary@on", UIWorldDoubles.T1),
                     CheckedGuardFixture.Checkable(TypedCheckedClaim("checked", T1)), TypedCheckedClaim("checked", T1)]);
            },
            PollCancellation = () => cancelRequested ? new RunDriverInput.Cancel("fixture cancel", T1) : null,
            ConsultAgent = context =>
            {
                consults++;
                return new AgentDecision.Act(new(context.DecisionId,
                    [new("switch", "primary", "toggle", "on"), new("switch", "primary", "toggle", "off")],
                    "two local actions with independent verification"));
            },
        };
        var effects = new EffectBoundary(new OkDriver());
        var kernel = new UniKernel(new EvidenceLedger(),
            new WorldModel(new HashSet<string> { UIWorldDoubles.Observed, TypedSubject, CheckedGuardFixture.SubjectFor(TypedSubject) },
                new SeedContainerAssociationStrategy(), new StatefulObservationStrategy(container), new RoleContinuityStrategy()),
            DisabledRunTrace.Instance, new RunModel(), new ControlLoop(plan), new RuntimeAssurance(new AlwaysFresh()), effects);
        Assert.True(kernel.AdmitContract(new ExecutionContract(
            "v1", "local-act-wait", new HashSet<string> { UIWorldDoubles.Observed, TypedSubject, CheckedGuardFixture.SubjectFor(TypedSubject) },
            new HashSet<string> { "toggle" }, new HashSet<string>(), ["switch-on"],
            [new("obl-switch-on", RunObligationKind.Objective, "ui.role.switch.checked", "checked", true,
                EntityScope: new TargetDescriptor("switch", "primary"))],
            TaskInstanceId: "task-local-wait", ProductSessionId: "session-local-wait", ContractRef: "contract-local-wait")).Accepted);
        await using var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);

        Assert.Equal(RunDriveStatus.WaitingForInput, (await driver.DriveAsync()).Status);
        var firstReceipt = Assert.Single(effects.ReceiptLog).ReceiptId;
        Assert.Equal(RunDriveStatus.WaitingForInput, (await driver.DriveAsync()).Status);
        Assert.Single(effects.ReceiptLog);
        Assert.Equal(1, consults);

        if (cancel)
        {
            cancelRequested = true;
            Assert.Equal(RunDriveStatus.CancelledWithoutSafeStopPath, (await driver.DriveAsync()).Status);
            Assert.Single(effects.ReceiptLog);
        }
        else
        {
            releaseFirstPost = true;
            Assert.Equal(RunDriveStatus.WaitingForInput, (await driver.DriveAsync()).Status);
            Assert.Equal(2, effects.ReceiptLog.Count);
            Assert.Equal(firstReceipt, effects.ReceiptLog[0].ReceiptId);
            Assert.NotEqual(firstReceipt, effects.ReceiptLog[1].ReceiptId);
            Assert.Equal(RunDriveStatus.WaitingForInput, (await driver.DriveAsync()).Status);
            Assert.Equal(2, effects.ReceiptLog.Count);
            Assert.Equal(1, consults);
        }
    }
}
