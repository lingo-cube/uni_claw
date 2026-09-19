using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// CORE-013 Slice 2 — EffectBoundary 接入可靠执行源（CORE-012 计划 §3.1：
/// gate 通过、driver 调用之前提交；非 Success 零 driver 调用；追加失败
/// 不抛过已投递边界；null source = 既有行为零变化）。
/// S1–S3 产品版经 scripted double 注入三值提交；S9（本地提交重试保持原
/// Attempt）是执行源契约面——orchestration buyer deferred，在此以 double
/// 契约测试钉住语义。
/// </summary>
public sealed class EffectBoundaryExecutionSourceTests
{
    private sealed class CountingDriver : IEffectDriver
    {
        public int DeliverCount { get; private set; }
        public bool ThrowOnDeliver { get; set; }

        public DispatchResult Deliver(DispatchRequest request)
        {
            DeliverCount++;
            if (ThrowOnDeliver)
                throw new InvalidOperationException("driver-fault");
            return new DispatchResult(
                DispatchOutcome.DeliveryCompleted, "test:delivered",
                new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero));
        }
    }

    /// <summary>三值可控 + 审计的执行源 double（含 Unknown 重试保持原 Attempt）。</summary>
    private sealed class ScriptedExecutionSource : IReliableExecutionSource
    {
        private sealed class Record
        {
            public Record(ExecutionRegistration registration, ExecutionAttemptStatus status) =>
                (Registration, Status) = (registration, status);

            public ExecutionRegistration Registration { get; }
            public ExecutionAttemptStatus Status { get; set; }
            public List<ExecutionJournalEntry> Entries { get; } = new();
        }

        private readonly Dictionary<string, Record> _attempts = new(StringComparer.Ordinal);
        private int _minted;

        public ExecutionCommitOutcome NextOutcome { get; set; } = ExecutionCommitOutcome.Success;
        public bool ThrowOnAppend { get; set; }
        public List<string> Calls { get; } = new();

        public int AttemptCount => _attempts.Count;

        public ExecutionCommit CommitPrepare(ExecutionRegistration registration)
        {
            ArgumentNullException.ThrowIfNull(registration);
            Calls.Add($"commit:{registration.AttemptId ?? "new"}");
            var attemptId = registration.AttemptId ?? $"scripted-{++_minted}";
            if (_attempts.TryGetValue(attemptId, out var existing)
                && existing.Status != ExecutionAttemptStatus.CommitUnknown)
                return new ExecutionCommit(ExecutionCommitOutcome.Failure, null);

            var outcome = NextOutcome;
            var status = outcome switch
            {
                ExecutionCommitOutcome.Success => ExecutionAttemptStatus.CommittedPending,
                ExecutionCommitOutcome.Failure => ExecutionAttemptStatus.CommitFailed,
                _ => ExecutionAttemptStatus.CommitUnknown,
            };
            _attempts[attemptId] = new Record(registration with { AttemptId = attemptId }, status);
            return new ExecutionCommit(outcome, outcome == ExecutionCommitOutcome.Failure ? null : attemptId);
        }

        public IReadOnlyList<PendingExecution> DiscoverPending() =>
            _attempts.Values
                .Where(a => a.Status is ExecutionAttemptStatus.CommittedPending
                    or ExecutionAttemptStatus.CommitUnknown
                    or ExecutionAttemptStatus.Dispatched
                    or ExecutionAttemptStatus.UnknownOutcome)
                .Select(a => new PendingExecution(a.Registration.AttemptId!, a.Registration.EffectRef, a.Status))
                .ToList();

        public ExecutionAttemptView? GetAttempt(string attemptId) =>
            _attempts.TryGetValue(attemptId, out var attempt)
                ? new ExecutionAttemptView(attempt.Registration, attempt.Status, attempt.Entries.ToList())
                : null;

        public void AppendSubmission(string attemptId, string note)
        {
            Calls.Add($"submission:{attemptId}");
            if (ThrowOnAppend)
                throw new IOException("append-fault");
            var attempt = Require(attemptId);
            attempt.Status = ExecutionAttemptStatus.Dispatched;
            attempt.Entries.Add(new ExecutionJournalEntry(ExecutionEntryKind.Submission, 0, note));
        }

        public void AppendReceipt(string attemptId, EffectReceipt? receipt)
        {
            Calls.Add($"receipt:{attemptId}");
            if (ThrowOnAppend)
                throw new IOException("append-fault");
            var attempt = Require(attemptId);
            attempt.Status = receipt is not null && receipt.Outcome == DispatchOutcome.DeliveryCompleted
                ? ExecutionAttemptStatus.Completed
                : ExecutionAttemptStatus.UnknownOutcome;
            attempt.Entries.Add(new ExecutionJournalEntry(
                ExecutionEntryKind.Receipt, 0, receipt?.ReceiptId ?? "no-receipt:pending"));
        }

        public void AppendLateFeedback(string? attemptId, ExecutionFeedback feedback) =>
            Calls.Add($"feedback:{attemptId ?? "held"}");

        public string LinkRetry(string attemptId)
        {
            Calls.Add($"retry:{attemptId}");
            var retryId = $"{attemptId}#r1";
            _attempts[retryId] = new Record(
                Require(attemptId).Registration with { AttemptId = retryId, RetryOf = attemptId },
                ExecutionAttemptStatus.CommittedPending);
            return retryId;
        }

        public ExecutionLink LinkCompensation(string attemptId)
        {
            Calls.Add($"compensation:{attemptId}");
            var original = Require(attemptId);
            var newEffectRef = original.Registration.EffectRef + "::comp1";
            var newAttemptId = $"{attemptId}#c1";
            _attempts[newAttemptId] = new Record(
                original.Registration with
                {
                    AttemptId = newAttemptId, EffectRef = newEffectRef,
                    RetryOf = null, Compensates = original.Registration.EffectRef,
                },
                ExecutionAttemptStatus.CommittedPending);
            return new ExecutionLink(newAttemptId, newEffectRef);
        }

        private Record Require(string attemptId) =>
            _attempts.TryGetValue(attemptId, out var attempt)
                ? attempt
                : throw new InvalidOperationException($"unknown attempt id: {attemptId}");
    }

    private static (CanonicalBinding Binding, AssuranceJudgment Judgment, BindingView View) Grounded(
        string bindingId = "bind-1", string intentId = "intent-1") =>
        (new CanonicalBinding(bindingId, intentId, "tap", "switch.wifi", "true", "rev-1", 1),
            new AssuranceJudgment(intentId, bindingId, "rev-1", IsAdmissible: true,
                Checks: Array.Empty<AssuranceCheck>(), RejectionReason: null,
                Freshness: new FreshnessJudgment(FreshnessSufficiency.Sufficient, "test:sufficient")),
            new BindingView("rev-1", 1, HasTargetSubjectClaim: true));

    [Fact]
    public void CommitSuccess_AllowsExactlyOneDriverCall_AndAppends()
    {
        var driver = new CountingDriver();
        var source = new ScriptedExecutionSource();
        var boundary = new EffectBoundary(driver, source);
        var (binding, judgment, view) = Grounded();

        var (gate, receipt) = boundary.Dispatch(binding, judgment, view);

        Assert.True(gate.Allowed);
        Assert.NotNull(receipt);
        Assert.Equal(1, driver.DeliverCount);
        Assert.Equal(new[] { "commit:new", "submission:scripted-1", "receipt:scripted-1" }, source.Calls);

        var attempt = source.GetAttempt("scripted-1")!;
        Assert.NotNull(attempt);
        Assert.Equal(ExecutionAttemptStatus.Completed, attempt.Status);
        Assert.Equal("bind-1", attempt.Registration.EffectRef); // EffectRef = BindingId 关联
        Assert.Equal("CountingDriver", attempt.Registration.ExecutorId);
    }

    [Fact]
    public void CommitFailure_BlocksDriver_WithExecutionCommitReason()
    {
        var driver = new CountingDriver();
        var source = new ScriptedExecutionSource { NextOutcome = ExecutionCommitOutcome.Failure };
        var boundary = new EffectBoundary(driver, source);
        var (binding, judgment, view) = Grounded();

        var (gate, receipt) = boundary.Dispatch(binding, judgment, view);

        Assert.False(gate.Allowed);
        Assert.Equal("execution-commit-failed", gate.Reason);
        Assert.Null(receipt);
        Assert.Equal(0, driver.DeliverCount); // 零 driver 调用
        Assert.Equal(new[] { "commit:new" }, source.Calls);
    }

    [Fact]
    public void CommitUnknown_BlocksDriver_WithExecutionCommitReason()
    {
        var driver = new CountingDriver();
        var source = new ScriptedExecutionSource { NextOutcome = ExecutionCommitOutcome.Unknown };
        var boundary = new EffectBoundary(driver, source);
        var (binding, judgment, view) = Grounded();

        var (gate, receipt) = boundary.Dispatch(binding, judgment, view);

        Assert.False(gate.Allowed);
        Assert.Equal("execution-commit-unknown", gate.Reason);
        Assert.Null(receipt);
        Assert.Equal(0, driver.DeliverCount);
        var unknown = source.GetAttempt("scripted-1")!;
        Assert.Equal(ExecutionAttemptStatus.CommitUnknown, unknown.Status);
    }

    [Fact]
    public void LocalCommitRetry_KeepsAttemptIdentity_ZeroExternalDelivery()
    {
        // S9 契约面：Unknown 后带显式 AttemptId 重试 → 同一 Attempt，无外部投递
        var source = new ScriptedExecutionSource { NextOutcome = ExecutionCommitOutcome.Unknown };
        var first = source.CommitPrepare(new ExecutionRegistration(
            null, "effect-x", "intent-1", "bind-1", "tap", "switch.wifi", "true",
            "rev-1", 1, "CountingDriver", "admissible"));
        Assert.Equal(ExecutionCommitOutcome.Unknown, first.Outcome);
        Assert.NotNull(first.AttemptId);

        source.NextOutcome = ExecutionCommitOutcome.Success;
        var retry = source.CommitPrepare(new ExecutionRegistration(
            first.AttemptId, "effect-x", "intent-1", "bind-1", "tap", "switch.wifi", "true",
            "rev-1", 1, "CountingDriver", "admissible"));

        Assert.Equal(ExecutionCommitOutcome.Success, retry.Outcome);
        Assert.Equal(first.AttemptId, retry.AttemptId); // 保持原 Attempt
        Assert.Equal(1, source.AttemptCount);
        Assert.Equal(ExecutionAttemptStatus.CommittedPending, source.GetAttempt(first.AttemptId!)!.Status);
    }

    [Fact]
    public void NullSource_LegacyBehavior_Unchanged()
    {
        var driver = new CountingDriver();
        var boundary = new EffectBoundary(driver);
        var (binding, judgment, view) = Grounded();

        var (gate, receipt) = boundary.Dispatch(binding, judgment, view);

        Assert.True(gate.Allowed);
        Assert.NotNull(receipt);
        Assert.Equal(1, driver.DeliverCount);
    }

    [Fact]
    public void AppendFailureAfterDelivery_DoesNotSwallowReceipt()
    {
        var driver = new CountingDriver();
        var source = new ScriptedExecutionSource { ThrowOnAppend = true };
        var boundary = new EffectBoundary(driver, source);
        var (binding, judgment, view) = Grounded();

        var (gate, receipt) = boundary.Dispatch(binding, judgment, view);

        // delivery 已发生：receipt 照常返回，追加失败不向上传播
        Assert.True(gate.Allowed);
        Assert.NotNull(receipt);
        Assert.Equal(1, driver.DeliverCount);
        var attempt = source.GetAttempt("scripted-1")!;
        Assert.Equal(ExecutionAttemptStatus.CommittedPending, attempt.Status); // 停留未决 = 安全方向
        Assert.Empty(attempt.Entries);
    }

    [Fact]
    public void DriverFault_Propagates_AttemptStaysCommittedPending()
    {
        var driver = new CountingDriver { ThrowOnDeliver = true };
        var source = new ScriptedExecutionSource();
        var boundary = new EffectBoundary(driver, source);
        var (binding, judgment, view) = Grounded();

        Assert.Throws<InvalidOperationException>(() => boundary.Dispatch(binding, judgment, view));

        // 提交已成功、driver 已被调用但无 Receipt → 未决未知，无伪造结果
        Assert.Equal(1, driver.DeliverCount);
        var attempt = source.GetAttempt("scripted-1")!;
        Assert.Equal(ExecutionAttemptStatus.CommittedPending, attempt.Status);
        Assert.Empty(attempt.Entries);
        Assert.Single(source.DiscoverPending());
    }
}
