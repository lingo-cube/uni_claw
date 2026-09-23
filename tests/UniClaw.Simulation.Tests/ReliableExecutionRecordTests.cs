using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// CORE-011 — 可靠执行记录仿真验收（S1–S12），执行
/// docs/design/core-execution-record-simulation-contract-v0.1.md §6 验收计划。
///
/// 仿真协议（契约 §3）：Host A = Compose + Admit + Activate + DriveOnce
/// （真实产品路径完成 step1 dispatch，停在不变量 43 屏障等待 post-action
/// 证据——非 terminal、delivery 未关闭）；guarded attempt 经测试侧
/// ReliableExecutionSourceFixture 的受控序列跨越 EffectBoundary 产品
/// seam；crash = 在命名 cut point 确定性停机并丢弃 Host A 内存组合，
/// 仅 fixture 存活；Host B = Compose + Admit + Activate（不 Drive），
/// 不带 Attempt ID 做恢复发现。
///
/// 本文件不证明产品 Runtime 已集成可靠执行源（那是后续单独授权的
/// 实现）；只证明契约行为在单进程崩溃/重启模型下可被确定性验证。
/// </summary>
public sealed class ReliableExecutionRecordTests
{
    private const string EffectRef = "effect-wifi-toggle";

    private static (SimulationHost Host, ReliableExecutionSourceFixture Fixture) BootedHostA(
        TraceArm arm = TraceArm.Enabled)
    {
        var bundle = GoldenScenarioBundles.TwoStepMissingMiddleEvidence();
        var fixture = new ReliableExecutionSourceFixture();
        var host = SimulationHost.Compose(bundle, new RunOptions { TraceArm = arm }, fixture);
        Assert.True(host.KernelCore.AdmitContract(bundle.Contract).Accepted);
        Assert.True(host.Driver.Activate().Accepted);

        // 基线：step1 经真实产品路径 dispatch（driver=1），屏障等待证据，
        // 非 terminal——guarded dispatch 的前置状态。
        var drive = host.DriveOnce();
        Assert.Equal(RunDriveStatus.WaitingForInput, drive.Status);
        Assert.Equal(1, host.EffectDeliveryCount);
        Assert.False(host.Facts.IsRunTerminal);
        return (host, fixture);
    }

    /// <summary>Host B 恢复协议（契约 §3）：Compose + Admit + Activate，不 Drive。</summary>
    private static SimulationHost BootedHostB(ReliableExecutionSourceFixture fixture,
        TraceArm arm = TraceArm.Enabled)
    {
        var bundle = GoldenScenarioBundles.TwoStepMissingMiddleEvidence();
        var host = SimulationHost.Compose(bundle, new RunOptions { TraceArm = arm }, fixture);
        Assert.True(host.KernelCore.AdmitContract(bundle.Contract).Accepted);
        Assert.True(host.Driver.Activate().Accepted);
        return host;
    }

    private static GuardedDispatchResult Guarded(SimulationHost host, string attemptId,
        CrashCutPoint crashAt = CrashCutPoint.None) => host.ExecutionSource!.DispatchGuarded(
        host, attemptId, EffectRef, "tap", "switch.wifi", "true", crashAt);

    // ---- S1 提交成功：允许恰好一次 driver 调用 ----

    [Fact]
    public void S01_CommitSuccess_AllowsExactlyOneDriverDelivery_AndCompletesAttempt()
    {
        var (hostA, fixture) = BootedHostA();

        var result = Guarded(hostA, "attempt-g-1");

        Assert.False(result.Crashed);
        Assert.Equal(ReliableCommitOutcome.Success, result.Commit);
        Assert.NotNull(result.Binding!.Canonical);
        Assert.True(result.Judgment!.IsAdmissible);
        Assert.True(result.Gate!.Allowed);
        Assert.NotNull(result.Receipt);
        Assert.Equal(DispatchOutcome.DeliveryCompleted, result.Receipt!.Outcome);

        // 恰好一次 driver 调用（基线 1 + guarded 1），Receipt 已关联
        Assert.Equal(2, hostA.EffectDeliveryCount);
        Assert.Empty(fixture.FindPending());

        var restored = fixture.GetAttempt("attempt-g-1")!;
        Assert.NotNull(restored);
        Assert.Equal(ReliableAttemptStatus.Completed, restored.Status);
        Assert.Contains(restored.Entries, e =>
            e.Kind == AttemptEntryKind.Receipt && e.Receipt!.ReceiptId == result.Receipt.ReceiptId);
    }

    // ---- S2 提交失败：driver 调用次数为零，无可发送 Attempt ----

    [Fact]
    public void S02_CommitFailure_BlocksDriver_NoSendableAttempt()
    {
        var (hostA, fixture) = BootedHostA();
        fixture.NextCommitOutcome = ReliableCommitOutcome.Failure;

        var result = Guarded(hostA, "attempt-g-2");

        Assert.False(result.Crashed);
        Assert.Equal(ReliableCommitOutcome.Failure, result.Commit);
        Assert.Null(result.Receipt);
        Assert.Equal(1, hostA.EffectDeliveryCount);

        // 无可发送 Attempt：未决发现不含提交失败记录
        Assert.Empty(fixture.FindPending());
        var failed = fixture.GetAttempt("attempt-g-2")!;
        Assert.NotNull(failed);
        Assert.Equal(ReliableAttemptStatus.CommitFailed, failed.Status);
    }

    // ---- S3 提交未知：不调用 driver，原关联可查询 ----

    [Fact]
    public void S03_CommitUnknown_BlocksDriver_OriginalCorrelationQueryable()
    {
        var (hostA, fixture) = BootedHostA();
        fixture.NextCommitOutcome = ReliableCommitOutcome.Unknown;

        var result = Guarded(hostA, "attempt-g-3");

        Assert.Equal(ReliableCommitOutcome.Unknown, result.Commit);
        Assert.Null(result.Receipt);
        Assert.Equal(1, hostA.EffectDeliveryCount);

        var unknown = fixture.GetAttempt("attempt-g-3")!;
        Assert.NotNull(unknown);
        Assert.Equal(ReliableAttemptStatus.CommitUnknown, unknown.Status);
        Assert.NotNull(unknown.Preparation.Binding);
        Assert.True(unknown.Preparation.Judgment.IsAdmissible);

        // 未决可发现（沿原关联查询/协调路径）
        var pending = Assert.Single(fixture.FindPending());
        Assert.Equal("attempt-g-3", pending.AttemptId);
    }

    // ---- S4 BeforePrepareCommit 重启：无可发送 Attempt ----

    [Fact]
    public void S04_CrashBeforePrepareCommit_NoSendableAttemptAfterRestart()
    {
        var (hostA, fixture) = BootedHostA();

        var result = Guarded(hostA, "attempt-g-4", CrashCutPoint.BeforePrepareCommit);

        Assert.True(result.Crashed);
        Assert.Equal(CrashCutPoint.BeforePrepareCommit, result.StoppedAt);
        Assert.Null(result.Commit);
        Assert.Equal(1, hostA.EffectDeliveryCount);
        hostA = null!; // 丢弃 Host A 的 Kernel/Runtime/Trace 内存组合

        // 崩溃发生在可靠提交之前：fixture 无可发送 Attempt（不得解释为成功/失败/已发送）
        Assert.Null(fixture.GetAttempt("attempt-g-4"));
        Assert.Empty(fixture.FindPending());
        Assert.Equal(0, fixture.AttemptCount);

        var hostB = BootedHostB(fixture);
        Assert.Empty(fixture.FindPending(hostB.KernelCore.RunId));
        Assert.Equal(0, hostB.EffectDeliveryCount);
    }

    // ---- S5 AfterPrepareCommitBeforeDriver 重启：无 ID 发现未决，driver 次数为零 ----

    [Fact]
    public void S05_CrashAfterPrepareCommitBeforeDriver_PendingDiscoveredWithoutAttemptId()
    {
        var (hostA, fixture) = BootedHostA();
        var hostARunId = hostA.KernelCore.RunId;
        var hostARevisionId = hostA.WorldCore.Current!.RevisionId;

        var result = Guarded(hostA, "attempt-g-5", CrashCutPoint.AfterPrepareCommitBeforeDriver);

        Assert.True(result.Crashed);
        Assert.Equal(CrashCutPoint.AfterPrepareCommitBeforeDriver, result.StoppedAt);
        Assert.Equal(ReliableCommitOutcome.Success, result.Commit);
        Assert.Equal(1, hostA.EffectDeliveryCount); // guarded attempt：零 driver 调用
        hostA = null!;

        var hostB = BootedHostB(fixture);

        // 不带 Attempt ID 的未决发现（声明范围 = RunId；同 bundle → 同 RunId）
        var pending = Assert.Single(fixture.FindPending(hostB.KernelCore.RunId));
        Assert.Equal("attempt-g-5", pending.AttemptId);
        Assert.Equal(ReliableAttemptStatus.CommittedPending, pending.Status);
        Assert.Equal(EffectRef, pending.EffectRef);
        Assert.Equal(hostARunId, pending.RunId);
        Assert.Equal(hostB.KernelCore.RunId, pending.RunId);

        // 完整准备集合可还原（Effect/Attempt/请求/Binding/依据/执行端/准入依据）
        var restored = fixture.GetAttempt("attempt-g-5")!;
        Assert.Equal(EffectRef, restored.Preparation.EffectRef);
        Assert.Equal("attempt-g-5", restored.Preparation.AttemptId);
        Assert.Equal("tap", restored.Preparation.Binding.EffectClass);
        Assert.Equal("switch.wifi", restored.Preparation.Binding.TargetSubject);
        Assert.Equal("true", restored.Preparation.Binding.TargetValue);
        Assert.Equal(hostARevisionId, restored.Preparation.Binding.RevisionId);
        Assert.True(restored.Preparation.Judgment.IsAdmissible);
        Assert.Equal("DeterministicEffectDriver", restored.Preparation.ExecutorId);
        Assert.Empty(restored.Entries); // 未发送：无 submission/receipt 痕迹

        // 恢复发现零投递、零新 Attempt（不得盲重发）
        Assert.Equal(0, hostB.EffectDeliveryCount);
        Assert.Equal(1, fixture.AttemptCount);
    }

    // ---- S6 AfterDriverBeforeReceipt 重启：原 Attempt 保持未知，不自动重发 ----

    [Fact]
    public void S06_CrashAfterDriverBeforeReceipt_AttemptStaysUnknown_NoAutoRedispatch()
    {
        var (hostA, fixture) = BootedHostA();

        var result = Guarded(hostA, "attempt-g-6", CrashCutPoint.AfterDriverBeforeReceipt);

        Assert.True(result.Crashed);
        Assert.Equal(CrashCutPoint.AfterDriverBeforeReceipt, result.StoppedAt);
        Assert.True(result.Gate!.Allowed);
        Assert.Null(result.Receipt);
        Assert.Equal(2, hostA.EffectDeliveryCount); // driver 已调用（崩溃前观察）
        var preCrash = fixture.GetAttempt("attempt-g-6")!;
        Assert.Equal(ReliableAttemptStatus.Dispatched, preCrash.Status);
        hostA = null!;

        var hostB = BootedHostB(fixture);

        // 原 Attempt 保持未知：非成功、非失败、非提交失败
        var pending = Assert.Single(fixture.FindPending(hostB.KernelCore.RunId));
        Assert.Equal("attempt-g-6", pending.AttemptId);
        Assert.Equal(ReliableAttemptStatus.Dispatched, pending.Status);
        var restored = fixture.GetAttempt("attempt-g-6")!;
        Assert.DoesNotContain(restored.Entries, e => e.Kind == AttemptEntryKind.Receipt);

        // 不自动新投递：Host B 零 driver 调用、零新 Attempt
        Assert.Equal(0, hostB.EffectDeliveryCount);
        Assert.Equal(1, fixture.AttemptCount);

        // Host B 协调动作：显式登记「仍无 Receipt」的未决声明（不伪造失败）
        fixture.AppendReceipt("attempt-g-6", null);
        var declared = fixture.GetAttempt("attempt-g-6")!;
        Assert.Equal(ReliableAttemptStatus.UnknownOutcome, declared.Status);
        Assert.Single(fixture.FindPending(hostB.KernelCore.RunId));
    }

    // ---- S7 AfterReceiptAppend 重启：Receipt 可还原，不重复投递 ----

    [Fact]
    public void S07_CrashAfterReceiptAppend_ReceiptRestorable_NotRedispatchableViaPending()
    {
        var (hostA, fixture) = BootedHostA();

        var result = Guarded(hostA, "attempt-g-7", CrashCutPoint.AfterReceiptAppend);

        Assert.True(result.Crashed);
        Assert.Equal(CrashCutPoint.AfterReceiptAppend, result.StoppedAt);
        var receiptId = result.Receipt!.ReceiptId;
        Assert.Equal(2, hostA.EffectDeliveryCount);
        hostA = null!;

        var hostB = BootedHostB(fixture);

        // 确认 Receipt → 不在未决集合：重启不得经 pending 路径重复投递
        Assert.Empty(fixture.FindPending(hostB.KernelCore.RunId));
        var restored = fixture.GetAttempt("attempt-g-7")!;
        Assert.Equal(ReliableAttemptStatus.Completed, restored.Status);
        var receiptEntry = Assert.Single(restored.Entries, e => e.Kind == AttemptEntryKind.Receipt);
        Assert.Equal(receiptId, receiptEntry.Receipt!.ReceiptId);
        Assert.Equal(DispatchOutcome.DeliveryCompleted, receiptEntry.Receipt.Outcome);
        Assert.Equal(0, hostB.EffectDeliveryCount);
    }

    // ---- S8 迟到反馈：追加到原 Attempt，不覆盖历史；关联不明保留待关联 ----

    [Fact]
    public void S08_LateFeedback_AppendsWithHistoryPreserved_UnresolvableHeld()
    {
        var (hostA, fixture) = BootedHostA();
        Guarded(hostA, "attempt-g-8");
        var before = fixture.GetAttempt("attempt-g-8")!;

        fixture.AppendLateFeedback("attempt-g-8", new AttemptFeedback("fb-late-1", "late-observer:switch.wifi=true"));

        var after = fixture.GetAttempt("attempt-g-8")!;
        Assert.Equal(before.Preparation, after.Preparation); // 准备集合不被覆盖
        Assert.Equal(before.Entries.Count + 1, after.Entries.Count);
        Assert.Contains(after.Entries, e =>
            e.Kind == AttemptEntryKind.LateFeedback && e.Feedback!.FeedbackId == "fb-late-1");

        // 关联不明确：保留待关联，不猜测归属
        fixture.AppendLateFeedback(null, new AttemptFeedback("fb-unknown-1", "correlation-lost"));
        var held = Assert.Single(fixture.UnassociatedFeedback);
        Assert.Equal("fb-unknown-1", held.FeedbackId);
        Assert.Equal(3, fixture.GetAttempt("attempt-g-8")!.Entries.Count); // 未误挂到已知 Attempt
    }

    // ---- S9 本地提交重试：保持原 Attempt，不增加外部投递 ----

    [Fact]
    public void S09_LocalCommitRetry_KeepsOriginalAttempt_NoExternalDelivery()
    {
        var (hostA, fixture) = BootedHostA();
        fixture.NextCommitOutcome = ReliableCommitOutcome.Unknown;
        Guarded(hostA, "attempt-g-9");
        Assert.Equal(ReliableAttemptStatus.CommitUnknown, fixture.GetAttempt("attempt-g-9")!.Status);

        fixture.NextCommitOutcome = ReliableCommitOutcome.Success;
        var retry = fixture.RetryCommit("attempt-g-9");

        Assert.Equal(ReliableCommitOutcome.Success, retry);
        var retried = fixture.GetAttempt("attempt-g-9")!;
        Assert.Equal(ReliableAttemptStatus.CommittedPending, retried.Status);
        Assert.Equal(1, fixture.AttemptCount); // 同一 Attempt，未新建
        Assert.Equal(1, hostA.EffectDeliveryCount); // 不增加外部投递
    }

    // ---- S10 外部投递重试：新 Attempt，保留重试关系 ----

    [Fact]
    public void S10_ExternalDeliveryRetry_NewAttempt_RetryLinkPreserved()
    {
        var (hostA, fixture) = BootedHostA();
        Guarded(hostA, "attempt-g-10a");

        var retryId = fixture.LinkRetry("attempt-g-10a");

        Assert.NotEqual("attempt-g-10a", retryId);
        var original = fixture.GetAttempt("attempt-g-10a")!;
        var retry = fixture.GetAttempt(retryId)!;
        Assert.Equal(ReliableAttemptStatus.Completed, original.Status); // 原 Attempt 不被改写
        Assert.Equal(original.Preparation.EffectRef, retry.Preparation.EffectRef); // 同一逻辑 Effect
        Assert.Equal("attempt-g-10a", retry.Preparation.RetryOf); // 重试关系保留
        Assert.Equal(ReliableAttemptStatus.CommittedPending, retry.Status);
        Assert.Equal(2, fixture.AttemptCount);
    }

    // ---- S11 补偿：新 Effect、新 Attempt，关联被补偿 Effect ----

    [Fact]
    public void S11_Compensation_NewEffectAndAttempt_LinkedToCompensatedEffect()
    {
        var (hostA, fixture) = BootedHostA();
        Guarded(hostA, "attempt-g-11a");

        var compensation = fixture.LinkCompensation("attempt-g-11a");

        Assert.NotEqual("attempt-g-11a", compensation.NewAttemptId);
        var compensated = fixture.GetAttempt("attempt-g-11a")!;
        var compensating = fixture.GetAttempt(compensation.NewAttemptId)!;
        Assert.Equal(ReliableAttemptStatus.Completed, compensated.Status); // 原 Effect/Attempt 不动
        Assert.Equal(compensation.NewEffectRef, compensating.Preparation.EffectRef);
        Assert.NotEqual(EffectRef, compensation.NewEffectRef); // 新 Effect
        Assert.Equal(EffectRef, compensating.Preparation.Compensates); // 关联被补偿 Effect
        Assert.Null(compensating.Preparation.RetryOf); // 补偿不是重试
        Assert.Equal(2, fixture.AttemptCount);
    }

    // ---- S12 Trace disabled / dropped：恢复结果与可靠执行源一致 ----

    [Fact]
    public void S12_TraceDisabled_RecoveryUnaffectedByTraceArm() =>
        S12_RecoveryUnaffectedByTraceArm(TraceArm.Disabled);

    [Fact]
    public void S12_TraceRecorderFailing_RecoveryUnaffectedByTraceArm() =>
        S12_RecoveryUnaffectedByTraceArm(TraceArm.FailingRecorder);

    private static void S12_RecoveryUnaffectedByTraceArm(TraceArm arm)
    {
        var (hostA, fixture) = BootedHostA(arm);

        var result = Guarded(hostA, "attempt-g-12", CrashCutPoint.AfterPrepareCommitBeforeDriver);
        Assert.True(result.Crashed);
        var crashedBindingId = result.Binding!.Canonical!.BindingId;
        var crashedRevisionId = result.Binding.Canonical.RevisionId;
        hostA = null!;

        var hostB = BootedHostB(fixture, arm);

        // Trace 臂（关闭/写入失败）不改变恢复结果
        var pending = Assert.Single(fixture.FindPending(hostB.KernelCore.RunId));
        Assert.Equal("attempt-g-12", pending.AttemptId);
        Assert.Equal(ReliableAttemptStatus.CommittedPending, pending.Status);
        var restored = fixture.GetAttempt("attempt-g-12")!;
        Assert.Equal(crashedBindingId, restored.Preparation.Binding.BindingId);
        Assert.Equal(crashedRevisionId, restored.Preparation.Binding.RevisionId);
        Assert.Equal("DeterministicEffectDriver", restored.Preparation.ExecutorId);
        Assert.Equal(0, hostB.EffectDeliveryCount);
        Assert.Equal(1, fixture.AttemptCount);
    }
}