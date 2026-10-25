using System.Text.Json;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

public sealed partial class TraversalExecutionTests
{
    private sealed class OriginalRunJournal(Func<RunHistoryRecord, bool>? fail = null, bool afterCommit = false) : IRunStateJournal
    {
        private readonly List<string> _rows = [];
        public bool CanInitialize => true;
        public string RecordingId { get; } = Guid.NewGuid().ToString("N");
        public IReadOnlyList<RunHistoryRecord> ReadRecords() => _rows.Select(row => JsonSerializer.Deserialize<RunHistoryRecord>(row)!).ToArray();
        public void Append(RunHistoryRecord record)
        {
            var failure = fail?.Invoke(record) == true;
            if (!failure || afterCommit) _rows.Add(JsonSerializer.Serialize(record));
            if (failure) throw new IOException("original commit acknowledgement failed");
        }
    }
    private sealed class ArchivedRunJournal(string recording, IReadOnlyList<RunHistoryRecord> rows) : IRunStateJournal
    {
        public bool CanInitialize => false;
        public string RecordingId => recording;
        public IReadOnlyList<RunHistoryRecord> ReadRecords() => rows;
        public void Append(RunHistoryRecord record) => throw new InvalidOperationException("readonly source");
    }

    [Fact]
    public void OriginalTraversalFactsRestoreWithoutChangingCoreRunProgress()
    {
        var source = new OriginalRunJournal(); var run = new RunModel(source); var consults = 0;
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision)), context =>
            ++consults == 1 ? Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null, [], []))
                : new AgentDecision.NoAction(new(context.DecisionId, "done")), run: run);
        driver.Drive();
        var restored = RunModel.RestoreReadOnly(source, source.ReadRecords()[0].Identity!, source.RecordingId);
        Assert.NotNull(restored.TraversalExecution);
        Assert.Equal(JsonSerializer.Serialize(run.TraversalHistory), JsonSerializer.Serialize(restored.TraversalHistory));
        Assert.True(Assert.Single(restored.TraversalExecution!.Operations!).Completed);
        Assert.Equal(run.History.Count, restored.History.Count);
        Assert.Equal(run.State!.Progress, restored.State!.Progress);
        Assert.Equal(run.HistoryPosition, restored.HistoryPosition);
        Assert.True(restored.IsReadOnly); Assert.Empty(kernel.EffectReceipts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HandlerRunningCommitFailureCannotStartInspectionOrPublishCandidate(bool afterCommit)
    {
        var source = new OriginalRunJournal(row => row.Traversal?.Handler?.Status == PlanHandlerCallStatus.Running, afterCommit);
        var run = new RunModel(source); var calls = 0;
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision)), context =>
            Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null,
                [new("english", "runtime.language-inspector", "1.0", "English")], [])),
            canBind: _ => true, inspectAsync: (_, _, _) => { calls++; return new ValueTask<Finding?>((Finding?)null); }, run: run);
        Assert.Contains("run-state-persistence-failed", driver.Drive().Reason);
        Assert.Empty(run.TraversalHandlers); Assert.Equal(0, calls); Assert.Empty(kernel.EffectReceipts);
        var count = source.ReadRecords().Count;
        Assert.Contains("run-state-persistence-failed", driver.Drive().Reason);
        Assert.Equal(count, source.ReadRecords().Count); Assert.Equal(0, calls);
        var restored = RunModel.RestoreReadOnly(source, source.ReadRecords()[0].Identity!, source.RecordingId);
        Assert.Equal(afterCommit ? 1 : 0, restored.TraversalHandlers.Count);
        if (afterCommit) Assert.Equal(PlanHandlerCallStatus.Running, restored.TraversalHandlers[0].Status);
    }

    [Fact]
    public async Task RunningAndTerminalKeepOriginalCallAndFindingAcrossReadonlyReopen()
    {
        var source = new OriginalRunJournal(); var run = new RunModel(source); var calls = 0;
        var completion = new TaskCompletionSource<Finding?>(); PlanHandlerInput? originalInput = null;
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision)), context =>
            context.DecisionId.EndsWith("-1", StringComparison.Ordinal)
                ? Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null,
                    [new("english", "runtime.language-inspector", "1.0", "English")], []))
                : new AgentDecision.NoAction(new(context.DecisionId, "done")), canBind: _ => true,
            inspectAsync: (_, input, _) => { calls++; originalInput = input; return new(completion.Task); }, run: run);
        Assert.Equal(RunDriveStatus.WaitingForHandler, driver.Drive().Status);
        var running = RunModel.RestoreReadOnly(source, source.ReadRecords()[0].Identity!, source.RecordingId);
        var call = Assert.Single(running.TraversalHandlers);
        Assert.Equal(PlanHandlerCallStatus.Running, call.Status); Assert.False(running.IsTerminal);
        var finding = new Finding("original-finding", new(kernel.RunId!, operationId: originalInput!.OperationId,
            domainCorrelation: originalInput.SourceRevisionId), "1.0", "runtime.language-inspector",
            FindingDisposition.Violation, CapabilityStatus.Completed, "original diagnostic");
        completion.SetResult(finding); await driver.DriveAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var terminal = RunModel.RestoreReadOnly(source, source.ReadRecords()[0].Identity!, source.RecordingId);
        var result = Assert.Single(terminal.TraversalHandlers);
        Assert.Equal(call.CallId, result.CallId); Assert.Equal(PlanHandlerCallStatus.Completed, result.Status);
        Assert.Equal(finding, result.Result); Assert.Equal(1, calls);
        Assert.Equal(PlanHandlerCallStatus.Running, Assert.Single(running.TraversalHandlers).Status);
        Assert.Equal(run.State!.Progress, terminal.State!.Progress); Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public async Task ReplacementPreservesOriginalVersionSnapshotsAndResetDoesNotEraseHistory()
    {
        var source = new OriginalRunJournal(); var run = new RunModel(source); var consultations = 0;
        var (_, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision)), context =>
        {
            consultations++;
            if (consultations > 2) return new AgentDecision.NoAction(new(context.DecisionId, "done"));
            var original = (AgentDecision.Plan)Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null,
                consultations == 1 ? [new("require-pass", "runtime.language-inspector", "1.0", "English", RequirePass: true)] : [], []));
            return original with { Proposal = original.Proposal with { Version = consultations } };
        }, canBind: _ => true, inspectAsync: (_, input, _) => ValueTask.FromResult<Finding?>(new("original-violation",
            new(input.RunId, operationId: input.OperationId, domainCorrelation: input.SourceRevisionId), "1.0",
            "runtime.language-inspector", FindingDisposition.Violation, CapabilityStatus.Completed, "original violation")), run: run);
        await driver.DriveAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var restored = RunModel.RestoreReadOnly(source, source.ReadRecords()[0].Identity!, source.RecordingId);
        Assert.Contains(restored.TraversalHistory, h => h.Version == 1 && h.Handlers.Any());
        Assert.Equal(2, restored.TraversalExecution!.Version);
        Assert.Empty(restored.TraversalHandlers);
        Assert.True(Assert.Single(restored.TraversalOperations).Completed);
        Assert.Equal(JsonSerializer.Serialize(run.TraversalHistory), JsonSerializer.Serialize(restored.TraversalHistory));
        Assert.True(restored.HistoryPosition!.Sequence > restored.History.Count);
        Assert.Throws<InvalidOperationException>(() => restored.RecordCycle());
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("sequence")]
    [InlineData("missing-initialize")]
    [InlineData("missing-operation")]
    [InlineData("unknown-call")]
    [InlineData("duplicate-operation")]
    [InlineData("duplicate-completion")]
    [InlineData("core-progress-change")]
    public void InvalidExecutionSuffixOrMissingDependencyCannotRestore(string fault)
    {
        var source = new OriginalRunJournal(); var run = new RunModel(source);
        var completion = new TaskCompletionSource<Finding?>();
        var (_, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision)), context =>
            Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null,
                [new("handler", "runtime.language-inspector", "1.0", "English")], [])), canBind: _ => true,
            inspectAsync: (_, _, _) => new(completion.Task), run: run);
        Assert.Equal(RunDriveStatus.WaitingForHandler, driver.Drive().Status);
        var rows = source.ReadRecords().ToList(); var identity = rows[0].Identity!;
        var handlerAt = rows.FindIndex(row => row.Traversal?.Handler is not null); var handler = rows[handlerAt];
        if (fault == "schema") rows[handlerAt] = handler with { Traversal = handler.Traversal! with { Schema = "unknown" } };
        if (fault == "sequence") rows[handlerAt] = handler with { Sequence = handler.Sequence + 2 };
        if (fault == "missing-initialize") rows[0] = rows[0] with { Traversal = null };
        if (fault == "missing-operation") rows[handlerAt] = handler with { Traversal = handler.Traversal! with
            { Handler = handler.Traversal.Handler! with { OperationId = "missing" } } };
        if (fault == "unknown-call") rows[handlerAt] = handler with { Traversal = handler.Traversal! with
            { Handler = handler.Traversal.Handler! with { Status = PlanHandlerCallStatus.Completed } } };
        if (fault == "duplicate-operation") rows.Add(rows.First(row => row.Traversal?.Operation is not null) with { Sequence = rows.Count + 1 });
        if (fault == "duplicate-completion")
        {
            var operation = rows.First(row => row.Traversal?.Operation is not null).Traversal!.Operation!;
            var value = new RunTraversalEvent("uniclaw.run.traversal.v1", RunTraversalTransition.Completed, OperationId: operation.OperationId, Work: "original work");
            var last = rows[^1]; rows.Add(new(rows.Count + 1, RunHistoryTransition.Traversal, last.Cycles, last.Acts, Traversal: value));
            rows.Add(rows[^1] with { Sequence = rows.Count + 1 });
        }
        if (fault == "core-progress-change") rows[handlerAt] = handler with { Cycles = handler.Cycles + 1 };
        Assert.Throws<InvalidDataException>(() => RunModel.RestoreReadOnly(new ArchivedRunJournal(source.RecordingId, rows), identity, source.RecordingId));
    }

    [Theory]
    [InlineData(RunTraversalTransition.Reset, false)]
    [InlineData(RunTraversalTransition.Reset, true)]
    [InlineData(RunTraversalTransition.Operation, false)]
    [InlineData(RunTraversalTransition.Operation, true)]
    [InlineData(RunTraversalTransition.Gap, false)]
    [InlineData(RunTraversalTransition.Gap, true)]
    [InlineData(RunTraversalTransition.Completed, false)]
    [InlineData(RunTraversalTransition.Completed, true)]
    [InlineData(RunTraversalTransition.View, false)]
    [InlineData(RunTraversalTransition.View, true)]
    public void EachRecordingOperationAcknowledgesBeforePublicationAndLatchesFailure(RunTraversalTransition transition, bool afterCommit)
    {
        var source = new OriginalRunJournal(row => row.Traversal?.Transition == transition, afterCommit); var run = new RunModel(source);
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision), complete: false),
            context => Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null, [], [])), run: run);
        Assert.Contains("run-state-persistence-failed", driver.Drive().Reason);
        var count = source.ReadRecords().Count; var position = run.HistoryPosition!.Sequence;
        Assert.Equal(afterCommit ? position + 1 : position, count);
        Assert.Contains("run-state-persistence-failed", driver.Drive().Reason); Assert.Equal(count, source.ReadRecords().Count);
        var restored = RunModel.RestoreReadOnly(source, source.ReadRecords()[0].Identity!, source.RecordingId);
        Assert.Equal(count, restored.HistoryPosition!.Sequence); Assert.Empty(kernel.EffectReceipts);
        if (transition == RunTraversalTransition.Operation) Assert.Equal(afterCommit ? run.TraversalOperations.Count + 1 : run.TraversalOperations.Count, restored.TraversalOperations.Count);
        if (transition == RunTraversalTransition.Completed) Assert.Equal(afterCommit ? run.TraversalCompletedWork.Count + 1 : run.TraversalCompletedWork.Count, restored.TraversalCompletedWork.Count);
        if (transition == RunTraversalTransition.Gap) Assert.Equal(afterCommit ? run.TraversalGaps.Count + 1 : run.TraversalGaps.Count, restored.TraversalGaps.Count);
        if (transition == RunTraversalTransition.View) Assert.Equal(afterCommit ? run.TraversalHistory.Count + 1 : run.TraversalHistory.Count, restored.TraversalHistory.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TerminalAcknowledgementFailurePreservesUnknownOriginalCallWithoutRetry(bool afterCommit)
    {
        var source = new OriginalRunJournal(row => row.Traversal?.Handler?.Status == PlanHandlerCallStatus.Completed, afterCommit);
        var run = new RunModel(source); var calls = 0; PlanHandlerInput? input = null; var completion = new TaskCompletionSource<Finding?>();
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision)), context =>
            Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null,
                [new("handler", "runtime.language-inspector", "1.0", "English")], [])), canBind: _ => true,
            inspectAsync: (_, original, _) => { input = original; calls++; return new(completion.Task); }, run: run);
        Assert.Equal(RunDriveStatus.WaitingForHandler, driver.Drive().Status);
        var originalCall = Assert.Single(run.TraversalHandlers).CallId;
        completion.SetResult(new("original-pass", new(input!.RunId, operationId: input.OperationId, domainCorrelation: input.SourceRevisionId),
            "1.0", "runtime.language-inspector", FindingDisposition.Pass, CapabilityStatus.Completed, "original pass"));
        Assert.Contains("run-state-persistence-failed", (await driver.DriveAsync().WaitAsync(TimeSpan.FromSeconds(10))).Reason);
        Assert.Equal(PlanHandlerCallStatus.Running, Assert.Single(run.TraversalHandlers).Status);
        var restored = RunModel.RestoreReadOnly(source, source.ReadRecords()[0].Identity!, source.RecordingId);
        Assert.Equal(originalCall, Assert.Single(restored.TraversalHandlers).CallId);
        Assert.Equal(afterCommit ? PlanHandlerCallStatus.Completed : PlanHandlerCallStatus.Running, restored.TraversalHandlers[0].Status);
        var count = source.ReadRecords().Count; driver.Drive(); Assert.Equal(count, source.ReadRecords().Count);
        Assert.Equal(1, calls); Assert.Empty(kernel.EffectReceipts);
    }
}
