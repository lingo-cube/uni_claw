using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Host;
using UniClaw.Host.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

internal static class ControlPlanGrowthProbe
{
    internal static async Task Run(string directory, string mode, int count)
    {
        if (mode is not ("control" or "plan-distinct" or "plan-versioned") || count < 1)
            throw new ArgumentException("mode must be control, plan-distinct, or plan-versioned; count must be positive");
        Directory.CreateDirectory(directory);
        var watch = Stopwatch.StartNew();
        var allocatedBefore = GC.GetTotalAllocatedBytes(true);
        await using var states = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"));
        await using var controlSource = new FileControlTraversalJournal(Path.Combine(directory, "control-traversal.journal.jsonl"));
        await using var planSource = new FileRuntimePlanJournal(Path.Combine(directory, "runtime-plan.journal.jsonl"));
        using var consultationSource = new FileConsultationJournal(Path.Combine(directory, "consultations.journal.jsonl"));
        using var execution = new FileExecutionJournal(Path.Combine(directory, "exec.journal"));
        using var resources = new FileRunResourceJournal(Path.Combine(directory, "resource.journal.jsonl"),
            consultationPath: Path.Combine(directory, "consultations.journal.jsonl"), executionPath: Path.Combine(directory, "exec.journal"));
        var run = new RunModel(states); var policy = new AgentPlanPolicy();
        var control = new ControlLoop(policy, controlSource);
        var seed = new ObservationProposal(new("ui.container.observed", "switch:entry@ready"), IngressKind.Observation,
            ObservationContext.External, new("storage-growth", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["offline"]));
        var evidenceId = new EvidenceLedger().Admit(seed).Admission.EvidenceId!; var container = "ctr-" + evidenceId[3..15];
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "ui.container.observed" },
            new Seed(), new Occurrence(container)), DisabledRunTrace.Instance, run, control,
            new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoEffect(), execution));
        var consultations = 0; var adoptions = 0; var finalBlocked = false;
        var archive = new FileRunRecoveryArchive(directory);
        await using var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            RuntimePlanJournal = planSource, ResourceJournal = resources, ConsultationJournal = consultationSource,
            RecoveryArchive = archive,
            NextInput = directive => directive.Context == ObservationContext.External ? new RunDriverInput.Observation([seed]) : null,
            AvailablePlanSources = new Dictionary<string, string> { ["current"] = "original page" },
            ProjectTraversal = _ =>
            {
                var revision = kernel.CurrentBelief!; var occurrence = revision.Occurrences!.Single();
                var candidates = Enumerable.Range(0, mode == "control" ? count : 1).Select(i => new TraversalCandidate(
                    new PlanCandidate($"candidate-{i:D4}", occurrence.Role, occurrence.SemanticDescriptor!, PlanCandidateDisposition.Ready, occurrence.EvidenceBasis),
                    new Dictionary<string, PolicyTruth> { ["always"] = PolicyTruth.Satisfied })).ToArray();
                return new(new(revision.RevisionId, $"{kernel.RunId}:{kernel.RunView!.Version}", container,
                    revision.Containers!, revision.Relations!, []), candidates, false, revision.EvidenceBasis.ToArray());
            },
            ConsultAgent = context =>
            {
                consultations++;
                if (mode == "control")
                {
                    if (consultations > 1) { finalBlocked = true; return new AgentDecision.Blocked(context.DecisionId, "growth-complete"); }
                    return new AgentDecision.Plan(context.DecisionId, new([new PlanItem.TraverseItem("current",
                        [new("read", "always", new("inspect", TraversalOperationKind.ReadOnly, null, [], []))], 0,
                        new("switch", "entry", "tap", null))], "control candidate growth", "control-growth", 1, MaxLocalOperations: count + 1));
                }
                if (adoptions >= count) { finalBlocked = true; return new AgentDecision.Blocked(context.DecisionId, "growth-complete"); }
                var version = adoptions + 1;
                var planId = mode == "plan-distinct" ? $"growth-plan-{version:D4}" : "growth-plan-versioned";
                adoptions++;
                return new AgentDecision.Plan(context.DecisionId,
                    new([new PlanItem.CheckpointItem("pause")], "actual checkpoint adoption", planId, version, MaxLocalOperations: 1));
            },
            CanBindPlanLanguage = _ => true,
            InspectPlanLanguageAsync = (_, _, _) => throw new InvalidOperationException("read-only growth probe must not call language handlers"),
        });
        var contract = await kernel.AdmitContractAsync(new("storage-growth-v1", "actual driver growth probe",
            new HashSet<string> { "ui.container.observed" }, new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"],
            MaxConsultations: Math.Max(8, count + 3), MaxTotalSteps: Math.Max(8, count + 3),
            TaskInstanceId: "storage-growth-task", ProductSessionId: "storage-growth-session", ContractRef: "storage-growth-contract"));
        if (!contract.Accepted || !driver.Activate().Accepted) throw new InvalidOperationException("growth-probe admission failed");
        var result = await driver.DriveAsync().WaitAsync(TimeSpan.FromMinutes(2));
        var driverAllocatedBytes = GC.GetTotalAllocatedBytes(false) - allocatedBefore;
        watch.Stop();
        if (result.Status != RunDriveStatus.TerminalNotProven || result.Reason != "agent-blocked:growth-complete")
            throw new InvalidOperationException($"growth scenario ended unexpectedly: {result.Status}:{result.Reason}");
        var controlRows = controlSource.ReadRecords(); var planRows = planSource.ReadRecords();
        var stoppedReaderAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stoppedReaderWatch = Stopwatch.StartNew();
        var controlReader = new FileControlTraversalJournal(Path.Combine(directory, "control-traversal.journal.jsonl"), true);
        var planReader = new FileRuntimePlanJournal(Path.Combine(directory, "runtime-plan.journal.jsonl"), true);
        var readerConstructorsMs = stoppedReaderWatch.Elapsed.TotalMilliseconds;
        var constructorAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - stoppedReaderAllocated;
        stoppedReaderAllocated = GC.GetAllocatedBytesForCurrentThread();
        stoppedReaderWatch.Restart();
        var stoppedControlRows = controlReader.ReadRecords(); var stoppedPlanRows = planReader.ReadRecords();
        var readerReadRecordsMs = stoppedReaderWatch.Elapsed.TotalMilliseconds;
        var readRecordsAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - stoppedReaderAllocated;
        await controlReader.DisposeAsync(); await planReader.DisposeAsync();
        var versionQueries = new
        {
            Control = MeasureVersionQueries(controlRows,
                () => { using var reader = new FileControlTraversalJournal(Path.Combine(directory, "control-traversal.journal.jsonl"), true); var value = reader.ReadLatestVersion(); return (value.Record, value.ReadCost); },
                (sequence, direction) => { var value = controlSource.ReadVersion(sequence, direction); return (value.Record, value.ReadCost); }),
            Plan = MeasureVersionQueries(planRows,
                () => { using var reader = new FileRuntimePlanJournal(Path.Combine(directory, "runtime-plan.journal.jsonl"), true); var value = reader.ReadLatestVersion(); return (value.Record, value.ReadCost); },
                (sequence, direction) => { var value = planSource.ReadVersion(sequence, direction); return (value.Record, value.ReadCost); })
        };
        var equivalentLegacy = MeasureEquivalentLegacy(directory, controlSource.RecordingId, controlRows, planSource.RecordingId, planRows);
        var sourceFiles = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path)).Order(StringComparer.Ordinal).ToArray();
        var hashesBeforeRead = sourceFiles.ToDictionary(name => name, name =>
        {
            var path = Path.Combine(directory, name);
            return File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : null;
        });
        var restoredRun = archive.RestoreRunState(run.RunId);
        var fullControl = archive.ReadControlTraversalHistory(run.RunId);
        var fullPlan = archive.ReadRuntimePlanHistory(run.RunId);
        var controlJson = JsonSerializer.Serialize(controlRows, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var planJson = JsonSerializer.Serialize(planRows, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var graphJsonBytes = planRows.Where(row => row.Graph.HasValue).Sum(row => Encoding.UTF8.GetByteCount(row.Graph!.Value.GetRawText()));
        var controlStateRowBytes = controlRows.Select(row => Encoding.UTF8.GetByteCount(row.State.GetRawText())).ToArray();
        var versionsRowBytes = planRows.Select(row => Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(row.Versions,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)))).ToArray();
        var controlStateJsonBytes = controlStateRowBytes.Sum();
        var versionsJsonBytes = versionsRowBytes.Sum();
        if (mode == "control" && controlRows.Count(row => row.Operation == "read-only-completed") != count)
            throw new InvalidOperationException($"control growth incomplete: expected {count} completed read-only candidates, observed {controlRows.Count(row => row.Operation == "read-only-completed")}");
        if (mode != "control" && planRows.Count(row => row.Operation == "adopt") != count)
            throw new InvalidOperationException($"plan growth incomplete: expected {count} adoptions, observed {planRows.Count(row => row.Operation == "adopt")}");
        if (!finalBlocked) throw new InvalidOperationException("growth scenario did not finish with the intended Blocked consultation");
        var source = sourceFiles.ToDictionary(name => name, name =>
        {
            var path = Path.Combine(directory, name);
            return File.Exists(path) ? new { Exists = true, Bytes = new FileInfo(path).Length,
                Sha256 = (string?)Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) } : new { Exists = false, Bytes = 0L, Sha256 = (string?)null };
        });
        var sourceAfter = sourceFiles.ToDictionary(name => name, name =>
        {
            var path = Path.Combine(directory, name);
            return File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : null;
        });
        var controlRecordsMatch = JsonSerializer.Serialize(fullControl.Records, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            == JsonSerializer.Serialize(controlRows, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var planRecordsMatch = JsonSerializer.Serialize(fullPlan.Records, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            == JsonSerializer.Serialize(planRows, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var sourceHashesPreserved = sourceFiles.ToDictionary(name => name, name => hashesBeforeRead[name] == sourceAfter[name]);
        if (!controlRecordsMatch || !planRecordsMatch || sourceHashesPreserved.Values.Any(preserved => !preserved))
            throw new InvalidDataException("read-only recovery mismatch or source changed during verification");
        var report = new
        {
            Mode = mode, RequestedCount = count, CompletedWork = run.TraversalOperations.Count(operation => operation.Completed),
            Consultations = consultations, FinalDecisionBlocked = finalBlocked, Adoptions = planRows.Count(row => row.Operation == "adopt"),
            Replacements = planRows.Count(row => row.Operation == "replacement-begin"),
            VersionMapSizes = planRows.Where(row => row.Operation is "adopt" or "replacement-begin").Select(row => row.Versions.Count).ToArray(),
            ControlRows = controlRows.Count, PlanRows = planRows.Count, ControlState = fullControl.State,
            ControlRecordsSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(controlJson))),
            PlanRecordsSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(planJson))),
            PlanVersions = fullPlan.LatestCommittedAdoption?.Versions,
            JournalAndHead = source, HistoryWriteCounts = new { States = states.WriteStatistics,
                Control = controlSource.WriteStatistics, Plan = planSource.WriteStatistics,
                Consultations = consultationSource.WriteStatistics, Resources = resources.WriteStatistics, Archive = archive.WriteStatistics },
            SnapshotWriteCounts = new { Control = controlSource.VersionWriteStatistics, Plan = planSource.VersionWriteStatistics },
            VersionQueries = versionQueries, EquivalentLegacy = equivalentLegacy,
            LogicalJsonBytes = new { ControlRecords = Encoding.UTF8.GetByteCount(controlJson), ControlStateCumulative = controlStateJsonBytes,
                ControlStateLatest = controlStateRowBytes.LastOrDefault(), PlanRecords = Encoding.UTF8.GetByteCount(planJson), PlanGraphs = graphJsonBytes,
                PlanVersionsCumulative = versionsJsonBytes, PlanVersionsLatest = versionsRowBytes.LastOrDefault() },
            CompositionAndDrive = new { DurationMilliseconds = watch.Elapsed.TotalMilliseconds,
                ManagedAllocatedBytes = driverAllocatedBytes, ProcessId = Environment.ProcessId,
                Status = result.Status.ToString(), Reason = result.Reason },
            StoppedReader = new { ConstructorMilliseconds = readerConstructorsMs, ReadRecordsMilliseconds = readerReadRecordsMs,
                ConstructorAllocatedBytes = constructorAllocatedBytes, ReadRecordsAllocatedBytes = readRecordsAllocatedBytes,
                ReadRecordCounts = new { Control = stoppedControlRows.Count, Plan = stoppedPlanRows.Count } },
            FullReadOnlyDomainSourceChecks = new { RunReadOnly = restoredRun.IsReadOnly,
                ControlRecords = fullControl.Records.Count, ControlPosition = fullControl.Position,
                PlanRecords = fullPlan.Records.Count, PlanPosition = fullPlan.Position,
                ControlStateMatches = JsonElement.DeepEquals(fullControl.State, controlRows[^1].State),
                ControlRecordsMatch = controlRecordsMatch, PlanRecordsMatch = planRecordsMatch, ContinuationSupported = fullControl.ContinuationSupported || fullPlan.ContinuationSupported,
                NoLatestOnlyClaim = true },
            SourceSha256Preserved = sourceHashesPreserved,
        };
        File.WriteAllText(Path.Combine(directory, "growth.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record VersionQueryMeasurement(int ComparedSequences, HistoryVersionReadCounts LatestReadCost,
        long LatestAllocatedBytes, double LatestMilliseconds, bool RecordsMatch,
        bool AllocationAndTimeIncludeReaderConstruction = true, bool ReadCostIsQueryOnly = true);

    private static VersionQueryMeasurement MeasureVersionQueries<T>(IReadOnlyList<T> rows,
        Func<(T Record, HistoryVersionReadCounts Cost)> latest,
        Func<int, HistoryVersionDirection, (T Record, HistoryVersionReadCounts Cost)> version)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        var value = latest();
        watch.Stop();
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var sequences = new[] { 1, (rows.Count + 1) / 2, rows.Count }.Distinct().ToArray();
        if (JsonSerializer.Serialize(rows[^1], json) != JsonSerializer.Serialize(value.Record, json))
            throw new InvalidDataException("latest-data-record-mismatch");
        foreach (var sequence in sequences)
            foreach (var direction in new[] { HistoryVersionDirection.Forward, HistoryVersionDirection.Reverse })
                if (JsonSerializer.Serialize(rows[sequence - 1], json) != JsonSerializer.Serialize(version(sequence, direction).Record, json))
                    throw new InvalidDataException("historical-data-record-mismatch");
        return new(sequences.Length, value.Cost, allocated, watch.Elapsed.TotalMilliseconds, true);
    }

    private static object MeasureEquivalentLegacy(string directory, string controlId, IReadOnlyList<ControlTraversalHistoryRecord> controlRows,
        string planId, IReadOnlyList<RuntimePlanHistoryRecord> planRows)
    {
        // Identical original records/identity in the supported v1 encoding, for cost only.
        // These alternate files never enter the original archive and are removed after consumption.
        var temporary = directory + ".legacy-comparison-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(temporary);
        try
        {
            var controlPath = Path.Combine(temporary, "control");
            var planPath = Path.Combine(temporary, "plan");
            WriteLegacy(controlPath, "uniclaw.control.traversal", controlId, controlRows);
            WriteLegacy(planPath, "uniclaw.runtime.plan", planId, planRows);
            using var control = new FileControlTraversalJournal(controlPath, true);
            using var plan = new FileRuntimePlanJournal(planPath, true);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            if (JsonSerializer.Serialize(control.ReadRecords(), json) != JsonSerializer.Serialize(controlRows, json)
                || JsonSerializer.Serialize(plan.ReadRecords(), json) != JsonSerializer.Serialize(planRows, json))
                throw new InvalidDataException("equivalent-legacy-records-mismatch");
            return new
            {
                Control = new { LegacySourceAndHeadBytes = StoredBytes(controlPath), CurrentSourceHeadAndBaselinesBytes = StoredBytes(Path.Combine(directory, "control-traversal.journal.jsonl")) },
                Plan = new { LegacySourceAndHeadBytes = StoredBytes(planPath), CurrentSourceHeadAndBaselinesBytes = StoredBytes(Path.Combine(directory, "runtime-plan.journal.jsonl")) },
                IdenticalOriginalRowsAndRecordingIds = true, LegacyReaderRecordsMatch = true, TemporaryComparisonSourcesRemovedOnReturn = true
            };
        }
        finally { Directory.Delete(temporary, true); }
    }

    private static long StoredBytes(string path) => new FileInfo(path).Length + new FileInfo(path + ".head").Length
        + (Directory.Exists(path + ".versions") ? Directory.GetFiles(path + ".versions", "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length) : 0);

    private static void WriteLegacy<T>(string path, string schemaPrefix, string recordingId, IReadOnlyList<T> rows)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var previous = "";
        using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            foreach (var row in rows)
            {
                var payload = JsonSerializer.Serialize(row, json);
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previous + "\n" + recordingId + "\n" + payload)));
                writer.WriteLine(JsonSerializer.Serialize(new { schema = schemaPrefix + ".history.v1", recordingId, previous, payload, hash }, json));
                previous = hash;
            }
        File.WriteAllText(path + ".head", JsonSerializer.Serialize(new { schema = schemaPrefix + ".history-head.v1", recordingId, sequence = rows.Count, hash = previous }, json) + "\n");
    }

    private sealed class Fresh : IFreshnessEvaluator { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "offline"); }
    private sealed class NoEffect : IEffectDriver { public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("Effect prohibited"); }
    private sealed class Occurrence(string container) : IUiObservationStrategy { public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) => [new(container, "switch", "entry", "ready")]; }
    private sealed class Seed : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => input.Previous is null
            ? new(AssociationDispositionKind.New, null, [new("(new)", [input.Current.EvidenceId], [])], [], "original seed")
            : new(AssociationDispositionKind.Insufficient, null, [], [], "seed once");
    }
}
