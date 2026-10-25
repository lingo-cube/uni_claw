using UniClaw.Host.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Runtime;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using System.Diagnostics;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class WorldHistoryRecoveryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("world-history-").FullName;
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void ProductionHostPreservesOriginalWorldOperationsFromInitialization()
    {
        var result = RunResourceRecoveryTests.Run(_root);
        Assert.True(File.Exists(Path.Combine(result.RunDir, "world.journal.jsonl")),
            "The original World operations need a durable source, not a final navigation projection.");
        Assert.True(File.Exists(Path.Combine(result.RunDir, "world.journal.jsonl.head")));
    }

    private static ObservationProposal Seed(string value = "on", string subject = "switch") => new(new(subject, value), IngressKind.Observation,
        ObservationContext.External, new("world-original", DateTimeOffset.Parse("2026-10-24T00:00:00Z"), "original", ["synthetic"]));
    private static ContinuityDemand Demand(string id = "demand-original") => new(id,
        ContinuityDemandSourceKind.EntityScopedObligation, null, "button", "original demand", null, null, null);

    private sealed class InterceptJournal(IWorldJournal inner) : IWorldJournal
    {
        public Action<WorldHistoryRecord>? BeforeAppend { get; set; }
        public Action<WorldHistoryRecord>? AfterAppend { get; set; }
        public bool CanInitialize => inner.CanInitialize;
        public string RecordingId => inner.RecordingId;
        public IReadOnlyList<WorldHistoryRecord> ReadRecords() => inner.ReadRecords();
        public void Append(WorldHistoryRecord row) { BeforeAppend?.Invoke(row); inner.Append(row); AfterAppend?.Invoke(row); }
    }

    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput _) => new(FreshnessSufficiency.Sufficient, "controlled"); }
    private sealed class NoDelivery : IEffectDriver
    { public DispatchResult Deliver(DispatchRequest _) => throw new Exception("No Effect expected"); }

    [Fact]
    public void FailureDuringOriginalDriveReturnsBlockedWithoutConfirmationOrRetry()
    {
        using var e = new FileEvidenceJournal(Path.Combine(_root, "evidence"));
        using var w = new FileWorldJournal(Path.Combine(_root, "world"));
        var journal = new InterceptJournal(w); var plan = new AgentPlanPolicy();
        var world = new WorldModel(new HashSet<string> { "switch" }, history: new(journal, Guid.NewGuid().ToString("N"), e.RecordingId));
        var kernel = new UniKernel(new EvidenceLedger(e), world, DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(plan), new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoDelivery()));
        var admission = kernel.AdmitContract(new("v1", "original world interruption", new HashSet<string> { "switch" },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["controlled"], MaxConsultations: 4, MaxTotalSteps: 10,
            TaskInstanceId: "world-history-task", ProductSessionId: "world-history-session", ContractRef: "world-history-contract"));
        Assert.True(admission.Accepted, admission.RejectionReason);
        var inputs = 0; var consults = 0;
        var driver = new KernelRunDriver(kernel, plan, new RunDriverInputs
        { NextInput = _ => { inputs++; return new RunDriverInput.Observation([Seed()]); },
            ConsultAgent = _ => { consults++; throw new Exception("must not consult"); } });
        journal.BeforeAppend = row => { if (row.Operation == "reconcile") throw new IOException("original World failure"); };
        Assert.True(driver.Activate().Accepted);
        RunDriveResult? result = null;
        var failure = Record.Exception(() => result = driver.Drive());
        Assert.Null(failure);
        Assert.Contains("world-persistence-failed", result!.Reason);
        Assert.Contains("world-persistence-failed", driver.Drive().Reason);
        Assert.Equal(1, inputs); Assert.Equal(0, consults); Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public void CommitCallbackCannotConsumeCandidateOrAdmitReentrantEvidence()
    {
        using var evidence = new FileEvidenceJournal(Path.Combine(_root, "evidence"));
        using var original = new FileWorldJournal(Path.Combine(_root, "world"));
        var journal = new InterceptJournal(original); var ledger = new EvidenceLedger(evidence);
        var world = new WorldModel(new HashSet<string> { "switch" }, history: new(journal, Guid.NewGuid().ToString("N"), evidence.RecordingId));
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance);
        var previous = world.RevisionHistory;
        var checkedCommit = false;
        journal.BeforeAppend = row =>
        {
            if (row.Operation != "reconcile") return;
            checkedCommit = true;
            Assert.Null(world.Current); Assert.Empty(previous); Assert.Empty(world.RevisionHistory);
            Assert.Throws<InvalidOperationException>(() => kernel.Process(Seed("must-not-admit")));
            Assert.Single(ledger.AdmissionLog);
        };
        kernel.Process(Seed());
        Assert.True(checkedCommit);
        Assert.Single(world.RevisionHistory); Assert.Empty(previous);
    }

    [Fact]
    public void OriginalRevisionAndDemandOnlyOperationReadWithoutReconciliation()
    {
        var ep = Path.Combine(_root, "evidence"); var wp = Path.Combine(_root, "world");
        var run = Guid.NewGuid().ToString("N"); string worldId, evidenceId;
        using (var evidence = new FileEvidenceJournal(ep))
        using (var journal = new FileWorldJournal(wp))
        {
            worldId = journal.RecordingId; evidenceId = evidence.RecordingId;
            var ledger = new EvidenceLedger(evidence);
            var world = new WorldModel(new HashSet<string> { "switch" }, history: new(journal, run, evidenceId));
            var record = ledger.Admit(Seed()).Record!;
            world.Reconcile(record, world.JudgeRelevance(record));
            world.RegisterContinuityDemand(Demand());
            Assert.Single(world.RevisionHistory);
            Assert.Equal(4, world.HistoryPosition!.Sequence);
        }
        using var evidenceRead = new FileEvidenceJournal(ep, true);
        using var worldRead = new FileWorldJournal(wp, true);
        var original = EvidenceLedger.RestoreReadOnly(evidenceRead, evidenceId);
        var history = WorldHistoryRead.ReadOriginal(worldRead, worldId, run, original);
        Assert.False(history.ContinuationSupported);
        Assert.Equal(4, history.Position.Sequence);
        var revision = Assert.Single(history.HistoricalRevisions);
        Assert.Equal("rev-1", revision.GetProperty("revisionId").GetString());
        Assert.Equal("on", revision.GetProperty("worldState").GetProperty("switch").GetProperty("value").GetString());
        Assert.Equal("demand-original", history.Operations[^1].Data.GetProperty("continuityDemands")[0].GetProperty("demandId").GetString());
        Assert.Empty(history.Operations[^1].Data.GetProperty("revisions").EnumerateArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureBeforeOrAfterAcknowledgementBlocksOwnerKernelAndDriver(bool afterCommit)
    {
        var ep = Path.Combine(_root, "evidence"); var wp = Path.Combine(_root, "world");
        var run = Guid.NewGuid().ToString("N"); string wid, eid;
        using (var evidence = new FileEvidenceJournal(ep))
        using (var source = new FileWorldJournal(wp))
        {
            wid = source.RecordingId; eid = evidence.RecordingId;
            var journal = new InterceptJournal(source); var ledger = new EvidenceLedger(evidence);
            var world = new WorldModel(new HashSet<string> { "switch" }, history: new(journal, run, eid));
            var plan = new AgentPlanPolicy();
            var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(plan));
            void Fail(WorldHistoryRecord row) { if (row.Operation == "reconcile") throw new IOException("original-commit-interruption"); }
            if (afterCommit) journal.AfterAppend = Fail; else journal.BeforeAppend = Fail;
            var published = world.RevisionHistory;
            Assert.ThrowsAny<IOException>(() => kernel.Process(Seed()));
            Assert.Empty(published); Assert.Empty(world.RevisionHistory);
            Assert.Equal(2, world.HistoryPosition!.Sequence);
            Assert.ThrowsAny<IOException>(() => world.Current);
            Assert.ThrowsAny<IOException>(() => world.RegisterContinuityDemand(Demand()));
            Assert.ThrowsAny<IOException>(() => kernel.Process(Seed("later")));
            Assert.Single(ledger.AdmissionLog);
            var calls = 0;
            var driver = new KernelRunDriver(kernel, plan, new RunDriverInputs
            { NextInput = _ => { calls++; throw new Exception("must not execute"); },
                ConsultAgent = _ => { calls++; throw new Exception("must not consult"); } });
            Assert.False(driver.Activate().Accepted); driver.Drive(); Assert.Equal(0, calls);
        }
        using var er = new FileEvidenceJournal(ep, true); using var wr = new FileWorldJournal(wp, true);
        var history = WorldHistoryRead.ReadOriginal(wr, wid, run, EvidenceLedger.RestoreReadOnly(er, eid));
        Assert.Equal(afterCommit ? 1 : 0, history.HistoricalRevisions.Count);
    }

    private sealed record Native(string Run, string World, string Evidence, string WorldPath, string EvidencePath);
    private Native WriteOriginal(bool empty = false)
    {
        var ep = Path.Combine(_root, "evidence"); var wp = Path.Combine(_root, "world");
        var run = Guid.NewGuid().ToString("N");
        using var evidence = new FileEvidenceJournal(ep); using var journal = new FileWorldJournal(wp);
        var ledger = new EvidenceLedger(evidence);
        var world = new WorldModel(new HashSet<string> { "switch" }, history: new(journal, run, evidence.RecordingId));
        if (!empty)
        {
            var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance); kernel.Process(Seed());
            world.RegisterContinuityDemand(Demand());
            var before = world.HistoryPosition;
            world.RegisterContinuityDemand(Demand()); world.RevokeContinuityDemand("absent");
            Assert.Equal(before, world.HistoryPosition);
            world.ResolveContinuity(new("demand-original")); // decision without a new revision
            world.RevokeContinuityDemand("demand-original");
        }
        return new(run, journal.RecordingId, evidence.RecordingId, wp, ep);
    }

    private static WorldHistoryRead ReadNative(Native n)
    {
        using var er = new FileEvidenceJournal(n.EvidencePath, true); using var wr = new FileWorldJournal(n.WorldPath, true);
        return WorldHistoryRead.ReadOriginal(wr, n.World, n.Run, EvidenceLedger.RestoreReadOnly(er, n.Evidence));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalEmptyAndNonemptyReadsAreRepeatableWithoutSourceWrites(bool empty)
    {
        var n = WriteOriginal(empty);
        var hashes = Directory.GetFiles(_root).ToDictionary(p => p, p => SHA256.HashData(File.ReadAllBytes(p)));
        for (var i = 0; i < 2; i++)
        {
            var read = ReadNative(n); Assert.False(read.ContinuationSupported);
            Assert.Equal(empty ? 0 : 1, read.HistoricalRevisions.Count);
            if (!empty)
            {
                Assert.Empty(read.Operations[^1].Data.GetProperty("continuityDemands").EnumerateArray());
                var decision = Assert.Single(read.Operations.Where(r => r.Operation == "continuity"));
                Assert.Single(decision.Data.GetProperty("continuityLog").EnumerateArray());
                Assert.Empty(decision.Data.GetProperty("revisions").EnumerateArray());
            }
        }
        foreach (var (p, hash) in hashes) Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(p)));
        using var source = new FileWorldJournal(n.WorldPath, true);
        Assert.Throws<IOException>(() => source.Append(source.ReadRecords()[0]));
        Assert.Throws<InvalidOperationException>(() => new WorldModel(new HashSet<string>(), history: new(source, n.Run, n.Evidence)));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("missing-head")]
    [InlineData("empty")]
    [InlineData("torn")]
    [InlineData("corrupt")]
    [InlineData("head-suffix")]
    [InlineData("rollback")]
    [InlineData("suffix")]
    [InlineData("unknown-version")]
    public void IncompleteOrChangedNativeSourceNeverBecomesEmptyOrRecoveredWorld(string fault)
    {
        var n = WriteOriginal(); var p = n.WorldPath;
        if (fault == "missing") File.Delete(p);
        if (fault == "missing-head") File.Delete(p + ".head");
        if (fault == "empty") File.WriteAllText(p, "");
        if (fault == "torn") File.WriteAllBytes(p, File.ReadAllBytes(p)[..^1]);
        if (fault == "corrupt") File.WriteAllText(p, File.ReadAllText(p).Replace("rev-1", "rev-9", StringComparison.Ordinal));
        if (fault == "head-suffix") File.AppendAllText(p + ".head", "{}\n");
        if (fault == "rollback") File.WriteAllLines(p, File.ReadAllLines(p)[..^1]);
        if (fault == "suffix") File.AppendAllText(p, File.ReadAllLines(p)[^1] + "\n");
        if (fault == "unknown-version") File.WriteAllText(p, File.ReadAllText(p).Replace("uniclaw.world.history.v3", "uniclaw.world.history.v9"));
        var failure = Record.Exception(() => ReadNative(n));
        Assert.True(failure is IOException or InvalidDataException, failure?.ToString());
    }

    private sealed class AlteredRead(IWorldJournal source, Func<WorldHistoryRecord, WorldHistoryRecord> alter) : IWorldJournal
    {
        public bool CanInitialize => false;
        public string RecordingId => source.RecordingId;
        public IReadOnlyList<WorldHistoryRecord> ReadRecords() => source.ReadRecords().Select(alter).ToArray();
        public void Append(WorldHistoryRecord _) => throw new IOException("read-only");
    }

    private sealed class ProposedUnknowns : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => new(AssociationDispositionKind.New, null,
            [new("original", [input.Current.EvidenceId], []), new("unproven", ["ev-unproven-proposal"], [])], [], "original proposal");
    }

    private Native WriteOriginalDecisionLogs()
    {
        var ep = Path.Combine(_root, "evidence"); var wp = Path.Combine(_root, "world");
        var run = Guid.NewGuid().ToString("N");
        using var e = new FileEvidenceJournal(ep); using var w = new FileWorldJournal(wp);
        var subjects = new[] { "switch", PageInterpretationCodec.Subject, ContainerContextCodec.Subject, SemanticVerificationCodec.Subject };
        var world = new WorldModel(subjects.ToHashSet(), new ProposedUnknowns(), history: new(w, run, e.RecordingId));
        var kernel = new UniKernel(new EvidenceLedger(e), world, DisabledRunTrace.Instance);
        kernel.Process(Seed());
        var next = Seed("off");
        kernel.Process(next with { Provenance = next.Provenance with { CaptureTime = next.Provenance.CaptureTime.AddSeconds(1), Scope = "next-original" } });
        kernel.Process(Seed(PageInterpretationCodec.Encode(new("ev-unproven-capture", "ev-unproven-identity",
            new("capture", "cycle", "session", DateTimeOffset.Parse("2026-10-24T00:00:00Z"), "page", "Page"),
            ["ev-unproven-support"], "original-source")), PageInterpretationCodec.Subject));
        kernel.Process(Seed("{}", ContainerContextCodec.Subject));
        kernel.Process(Seed(SemanticVerificationCodec.Encode(new("ev-unproven-semantic", "ev-unproven-capture",
            "ev-unproven-identity", "ev-unproven-context", new string('a', 64))), SemanticVerificationCodec.Subject));
        Assert.NotEmpty(world.ClaimEvolutionLog);
        return new(run, w.RecordingId, e.RecordingId, wp, ep);
    }

    [Fact]
    public void RejectedOriginalProposalsKeepUnprovenReferencesWithoutBecomingAcceptedDependencies()
    {
        var read = ReadNative(WriteOriginalDecisionLogs());
        var data = read.Operations.Where(r => r.Operation != "initialize").Select(r => r.Data).ToArray();
        var correction = Assert.Single(data.SelectMany(d => d.GetProperty("pageCorrections").EnumerateArray().ToArray()));
        Assert.False(correction.GetProperty("applied").GetBoolean());
        Assert.Equal("ev-unproven-capture", correction.GetProperty("targetCaptureEvidenceId").GetString());
        var semantic = Assert.Single(data.SelectMany(d => d.GetProperty("semanticVerifications").EnumerateArray().ToArray()));
        Assert.False(semantic.GetProperty("applied").GetBoolean());
        Assert.Equal("ev-unproven-semantic", semantic.GetProperty("observation").GetProperty("semanticEvidenceId").GetString());
        Assert.Contains(data.SelectMany(d => d.GetProperty("associationLog").EnumerateArray().ToArray()),
            log => log.GetProperty("candidates").EnumerateArray().Any(c => c.GetProperty("supportingEvidenceIds")
                .EnumerateArray().Any(id => id.GetString() == "ev-unproven-proposal")));
    }

    [Theory]
    [InlineData("pageCorrections", "correctionEvidenceId")]
    [InlineData("pageCorrections", "targetCaptureEvidenceId")]
    [InlineData("pageCorrections", "supersededIdentityEvidenceId")]
    [InlineData("pageCorrections", "supportingEvidenceIds")]
    [InlineData("pageCorrections", "revokedInterpretationEvidenceIds")]
    [InlineData("contextDecisions", "evidenceId")]
    [InlineData("semanticVerifications", "evidenceId")]
    [InlineData("semanticVerifications", "semanticEvidenceId")]
    [InlineData("semanticVerifications", "sourceCaptureEvidenceId")]
    [InlineData("semanticVerifications", "sourceIdentityEvidenceId")]
    [InlineData("semanticVerifications", "nativeContextEvidenceId")]
    [InlineData("associationLog", "evidenceId")]
    [InlineData("claimEvolutionLog", "establishingEvidenceId")]
    [InlineData("claimEvolutionLog", "supersededEvidenceId")]
    public void ReaderRejectsChangedTypedDecisionLogSourcesAndOriginalProposalReferences(string field, string reference)
    {
        var n = WriteOriginalDecisionLogs(); using var er = new FileEvidenceJournal(n.EvidencePath, true);
        using var wr = new FileWorldJournal(n.WorldPath, true);
        var touched = false;
        var altered = new AlteredRead(wr, row =>
        {
            if (row.Operation == "initialize" || row.Data.GetProperty(field).GetArrayLength() == 0) return row;
            var data = JsonNode.Parse(row.Data.GetRawText())!;
            var log = data[field]![0]!;
            if (field == "semanticVerifications" && reference != "evidenceId") log = log["observation"]!;
            log[reference] = reference.EndsWith("Ids", StringComparison.Ordinal) ? new JsonArray("ev-history-replaced") : JsonValue.Create("ev-history-replaced");
            touched = true;
            return row with { Data = JsonSerializer.SerializeToElement(data) };
        });
        Assert.Throws<InvalidDataException>(() => WorldHistoryRead.ReadOriginal(altered, n.World, n.Run, EvidenceLedger.RestoreReadOnly(er, n.Evidence)));
        Assert.True(touched);
    }

    [Theory]
    [InlineData("pageCorrections")]
    [InlineData("semanticVerifications")]
    public void OriginalUnprovenProposalCannotBeRelabeledAsAnAppliedAcceptedDependency(string field)
    {
        var n = WriteOriginalDecisionLogs(); using var er = new FileEvidenceJournal(n.EvidencePath, true);
        using var wr = new FileWorldJournal(n.WorldPath, true);
        var altered = new AlteredRead(wr, row =>
        {
            if (row.Operation == "initialize" || row.Data.GetProperty(field).GetArrayLength() == 0) return row;
            var data = JsonNode.Parse(row.Data.GetRawText())!; data[field]![0]!["applied"] = true;
            return row with { Data = JsonSerializer.SerializeToElement(data) };
        });
        Assert.Throws<InvalidDataException>(() => WorldHistoryRead.ReadOriginal(altered, n.World, n.Run, EvidenceLedger.RestoreReadOnly(er, n.Evidence)));
    }

    [Theory]
    [InlineData("revoked-unprocessed")]
    [InlineData("revoked-unrelated-processed")]
    [InlineData("relevance-wrong-accepted-source")]
    public void AcceptedIdentityAloneCannotForgeRevocationOrRelabelAnotherRelevanceOperation(string fault)
    {
        var ep = Path.Combine(_root, "evidence"); var wp = Path.Combine(_root, "world");
        var run = Guid.NewGuid().ToString("N"); string wid, eid, relevant, irrelevant;
        using (var e = new FileEvidenceJournal(ep))
        using (var w = new FileWorldJournal(wp))
        {
            wid = w.RecordingId; eid = e.RecordingId;
            var world = new WorldModel(new HashSet<string> { "switch" }, history: new(w, run, eid));
            var kernel = new UniKernel(new EvidenceLedger(e), world, DisabledRunTrace.Instance);
            relevant = kernel.Process(Seed()).Admission.EvidenceId!;
            irrelevant = kernel.Process(Seed("original irrelevant", "outside-scope")).Admission.EvidenceId!;
        }
        var n = new Native(run, wid, eid, wp, ep);
        var original = ReadNative(n);
        Assert.Single(original.HistoricalRevisions);
        Assert.False(original.Operations[^1].Data.GetProperty("relevanceLog")[0].GetProperty("isRelevant").GetBoolean());
        using var er = new FileEvidenceJournal(ep, true); using var wr = new FileWorldJournal(wp, true);
        var altered = new AlteredRead(wr, row =>
        {
            var data = JsonNode.Parse(row.Data.GetRawText())!;
            if (row.Operation == "reconcile" && fault.StartsWith("revoked", StringComparison.Ordinal))
                data["revokedInterpretations"] = new JsonArray(fault == "revoked-unprocessed" ? irrelevant : relevant);
            if (row.Operation == "relevance" && row.Data.GetProperty("input").GetProperty("evidenceId").GetString() == relevant
                && fault == "relevance-wrong-accepted-source") data["relevanceLog"]![0]!["evidenceId"] = irrelevant;
            return row with { Data = JsonSerializer.SerializeToElement(data) };
        });
        Assert.Throws<InvalidDataException>(() => WorldHistoryRead.ReadOriginal(altered, wid, run, EvidenceLedger.RestoreReadOnly(er, eid)));
    }

    [Theory]
    [InlineData("wrong-world")]
    [InlineData("wrong-run")]
    [InlineData("wrong-evidence")]
    [InlineData("sequence")]
    [InlineData("parent")]
    [InlineData("root-field")]
    [InlineData("side-state")]
    [InlineData("unaccepted")]
    [InlineData("unprocessed")]
    [InlineData("version")]
    public void ReaderRejectsWrongOriginalSourcesAndIncompleteOwnerData(string fault)
    {
        var n = WriteOriginal(); using var er = new FileEvidenceJournal(n.EvidencePath, true);
        using var wr = new FileWorldJournal(n.WorldPath, true);
        var evidence = EvidenceLedger.RestoreReadOnly(er, n.Evidence);
        var journal = new AlteredRead(wr, row =>
        {
            var data = JsonNode.Parse(row.Data.GetRawText())!;
            if (row.Operation == "initialize")
            {
                if (fault == "wrong-evidence") data["evidenceRecordingId"] = Guid.NewGuid().ToString("N");
                if (fault == "version") data["schema"] = "unknown-owner-schema";
            }
            if (row.Operation == "reconcile")
            {
                if (fault == "sequence") return row with { Sequence = 99 };
                if (fault == "parent") data["revisions"]![0]!["parentRevisionId"] = "wrong-parent";
                if (fault == "root-field") data["revisions"]![0]!.AsObject().Remove("observationNeed");
                if (fault == "side-state") data.AsObject().Remove("containerObservations");
                if (fault == "unaccepted") data["revisions"]![0]!["worldState"]!["switch"]!["evidenceId"] = "ev-absent";
                if (fault == "unprocessed") data["processedEvidenceIds"] = new JsonArray();
            }
            return row with { Data = JsonSerializer.SerializeToElement(data) };
        });
        Assert.Throws<InvalidDataException>(() => WorldHistoryRead.ReadOriginal(journal,
            fault == "wrong-world" ? Guid.NewGuid().ToString("N") : n.World,
            fault == "wrong-run" ? Guid.NewGuid().ToString("N") : n.Run, evidence));
    }

    [Fact]
    public void NativeReopenKeepsAdditionalSameVersionRevisionFieldsAndTheirOriginalOrder()
    {
        var n = WriteOriginal(); var extendedPath = Path.Combine(_root, "extended-world");
        string extendedId; JsonElement expected = default;
        using (var original = new FileWorldJournal(n.WorldPath, true))
        using (var extended = new FileWorldJournal(extendedPath))
        {
            extendedId = extended.RecordingId;
            foreach (var row in original.ReadRecords())
            {
                if (row.Operation != "reconcile") { extended.Append(row); continue; }
                var data = JsonNode.Parse(row.Data.GetRawText())!;
                var revision = data["revisions"]![0]!.AsObject();
                revision["originalExtension"] = new JsonObject { ["evidenceId"] = "opaque-unknown", ["values"] = new JsonArray(3, 1, 2) };
                expected = JsonSerializer.SerializeToElement(revision);
                extended.Append(row with { Data = JsonSerializer.SerializeToElement(data) });
            }
        }
        File.WriteAllText(Path.Combine(_root, "unknown-original.json"), expected.GetRawText());
        var files = Directory.GetFiles(_root).ToDictionary(p => p, p => SHA256.HashData(File.ReadAllBytes(p)));
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var revision = Assert.Single(ReadNative(n with { World = extendedId, WorldPath = extendedPath }).HistoricalRevisions);
            Assert.Equal(expected.GetRawText(), revision.GetRawText());
            Assert.Equal("originalExtension", revision.EnumerateObject().Last().Name);
            Assert.Equal(expected.GetRawText(), new WorldHistoryVersionReader(extendedPath, extendedId).ReadLatest()!.Revision.GetRawText());
        }
        foreach (var (path, hash) in files) Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(path)));
        Export("unknown-fields", _root);
    }

    [Theory]
    [InlineData("evidenceId")]
    [InlineData("evidenceBasis")]
    [InlineData("sourceEvidenceIds")]
    public void ClaimSubjectNamesNeverBecomeTypedEvidenceReferenceFields(string subject)
    {
        var ep = Path.Combine(_root, "evidence"); var wp = Path.Combine(_root, "world");
        var run = Guid.NewGuid().ToString("N"); string eid, wid;
        using (var e = new FileEvidenceJournal(ep))
        using (var w = new FileWorldJournal(wp))
        {
            eid = e.RecordingId; wid = w.RecordingId;
            var world = new WorldModel(new HashSet<string> { subject }, history: new(w, run, eid));
            new UniKernel(new EvidenceLedger(e), world, DisabledRunTrace.Instance).Process(Seed("on", subject));
        }
        var read = ReadNative(new(run, wid, eid, wp, ep));
        Assert.Equal("on", Assert.Single(read.HistoricalRevisions).GetProperty("worldState").GetProperty(subject).GetProperty("value").GetString());
    }

    [Fact]
    public void TransitionPriorIsPreservedAsOriginalInputWithoutMintingEvidenceOrEffectAuthority()
    {
        var ep = Path.Combine(_root, "evidence"); var wp = Path.Combine(_root, "world");
        var run = Guid.NewGuid().ToString("N"); string wid, eid;
        var prior = new TransitionContext("scroll", "original-attempt", TransitionStrength.Attempt);
        using (var e = new FileEvidenceJournal(ep))
        using (var w = new FileWorldJournal(wp))
        {
            wid = w.RecordingId; eid = e.RecordingId; var ledger = new EvidenceLedger(e);
            var world = new WorldModel(new HashSet<string> { "switch" }, history: new(w, run, eid));
            new UniKernel(ledger, world, DisabledRunTrace.Instance).Process(Seed(), prior);
            Assert.Single(ledger.CanonicalRecords);
            Assert.Equal(ObservationOrigin.External, ledger.CanonicalRecords.Values.Single().Origin);
        }
        var read = ReadNative(new(run, wid, eid, wp, ep));
        var op = Assert.Single(read.Operations.Where(r => r.Operation == "reconcile"));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(prior, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            op.Data.GetProperty("input").GetProperty("transitionContext")));
    }

    [Fact]
    public void AuxiliaryReconciliationKeepsPostPublishDecisionsAndOriginalUnknowns()
    {
        var ep = Path.Combine(_root, "evidence"); var wp = Path.Combine(_root, "world");
        var run = Guid.NewGuid().ToString("N"); string wid, eid;
        using (var e = new FileEvidenceJournal(ep))
        using (var w = new FileWorldJournal(wp))
        {
            wid = w.RecordingId; eid = e.RecordingId;
            var subjects = new[] { PageInterpretationCodec.Subject, ContainerContextCodec.Subject,
                SemanticVerificationCodec.Subject, OverlayObservationCodec.Subject };
            var world = new WorldModel(subjects.ToHashSet(), history: new(w, run, eid));
            var kernel = new UniKernel(new EvidenceLedger(e), world, DisabledRunTrace.Instance);
            foreach (var subject in subjects) kernel.Process(Seed("{}", subject));
            Assert.Single(world.PageCorrectionLog); Assert.Single(world.ContextDecisionLog); Assert.Single(world.SemanticVerificationLog);
            Assert.False(world.PageCorrectionLog[0].Applied); Assert.False(world.ContextDecisionLog[0].Applied);
        }
        var read = ReadNative(new(run, wid, eid, wp, ep));
        Assert.Equal(4, read.HistoricalRevisions.Count);
        var correction = Assert.Single(read.Operations.SelectMany(r => r.Operation == "initialize" ? [] : r.Data.GetProperty("pageCorrections").EnumerateArray().ToArray()));
        Assert.False(correction.GetProperty("applied").GetBoolean());
        Assert.Equal("malformed-correction", correction.GetProperty("reason").GetString());
        foreach (var field in new[] { "contextDecisions", "semanticVerifications" })
            Assert.Single(read.Operations.SelectMany(r => r.Operation == "initialize" ? [] : r.Data.GetProperty(field).EnumerateArray().ToArray()));
        Assert.All(read.HistoricalRevisions, r => Assert.Equal(JsonValueKind.Null, r.GetProperty("currentPageObservation").ValueKind));
    }

    private sealed class Occurrences : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            [new(null, "button", "original", Native: new("android.resource-id", "original.switch"))
            { SupportingEvidenceIds = previous?.Conflicts.Count > 0 ? previous.Conflicts.Select(c => c.ChallengingEvidenceId).Distinct().ToArray() : null }];
    }
    private sealed class Continuity : IContinuityStrategy
    {
        public ContinuityProposal Propose(ContinuityAdjudicationInput input) => new(
            ContinuityProposedOutcomeKind.ReferenceEstablished, input.Candidates[0].OccurrenceId, null,
            input.Current.EvidenceBasis.ToArray(), [], [], "original accepted basis");
    }

    [Fact]
    public void ContinuityCommitKeepsItsPostRevisionDecisionAndDemandRevocation()
    {
        var ep = Path.Combine(_root, "evidence"); var wp = Path.Combine(_root, "world");
        var run = Guid.NewGuid().ToString("N"); string wid, eid;
        using (var e = new FileEvidenceJournal(ep))
        using (var w = new FileWorldJournal(wp))
        {
            wid = w.RecordingId; eid = e.RecordingId;
            var world = new WorldModel(new HashSet<string> { "switch" }, observationStrategy: new Occurrences(),
                continuityStrategy: new Continuity(), history: new(w, run, eid));
            new UniKernel(new EvidenceLedger(e), world, DisabledRunTrace.Instance).Process(Seed());
            world.RegisterContinuityDemand(Demand());
            Assert.Equal(ContinuityResolutionOutcomeKind.ReferenceEstablished, world.ResolveContinuity(new("demand-original")).Outcome.Kind);
            world.RevokeContinuityDemand("demand-original");
            Assert.Equal(2, world.RevisionHistory.Count); Assert.Single(world.Current!.LogicalItems!);
        }
        var read = ReadNative(new(run, wid, eid, wp, ep));
        var operation = Assert.Single(read.Operations.Where(r => r.Operation == "continuity"));
        Assert.Single(operation.Data.GetProperty("revisions").EnumerateArray());
        Assert.Single(operation.Data.GetProperty("continuityLog").EnumerateArray());
        Assert.Single(read.HistoricalRevisions[^1].GetProperty("logicalItems").EnumerateArray());
        Assert.Empty(read.Operations[^1].Data.GetProperty("continuityDemands").EnumerateArray());
    }

    [Fact]
    public void OriginalDriverConflictResolutionIsStoredWithoutAnotherEvidenceAdmission()
    {
        var ep = Path.Combine(_root, "evidence"); var wp = Path.Combine(_root, "world");
        var run = Guid.NewGuid().ToString("N"); string wid, eid;
        using (var e = new FileEvidenceJournal(ep))
        using (var w = new FileWorldJournal(wp))
        {
            wid = w.RecordingId; eid = e.RecordingId; var ledger = new EvidenceLedger(e); var plan = new AgentPlanPolicy();
            var world = new WorldModel(new HashSet<string> { "ui.node.*", "fixture.occurrence" }, observationStrategy: new Occurrences(), history: new(w, run, eid));
            var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(plan),
                new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoDelivery()));
            ObservationProposal Claim(string field, string value, string producer) => new(new("ui.node.original#0." + field, value),
                IngressKind.Observation, ObservationContext.External, new(producer, DateTimeOffset.Parse("2026-10-24T00:00:00Z"),
                    "typed:" + field, ["typed-hierarchy:v1"], new("original", 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
                        "fixture-v1", UiHierarchyFormat.UiAutomatorXml, "device", "session", "cycle", DateTimeOffset.Parse("2026-10-24T00:00:00Z"),
                        null, HierarchyCapability.CheckedTriState, CoverageCompleteness.Partial, "viewport", 0, null, field)));
            var claims = new[] { Claim("resource_id", "original.switch", "platform.uiautomator"),
                Claim("checkable", "true", "platform.uiautomator"), Claim("checked", "checked", "platform.uiautomator"),
                Claim("checked", "unchecked", "other-source"), Seed("observed", "fixture.occurrence") };
            var seen = false;
            var driver = new KernelRunDriver(kernel, plan, new RunDriverInputs
            { NextInput = _ => { if (seen) return null; seen = true; return new RunDriverInput.Observation(claims); },
                ConsultAgent = context => new AgentDecision.NoAction(new(context.DecisionId, "bounded original fixture")) });
            var a = kernel.AdmitContract(new("v1", "original conflict", new HashSet<string> { "ui.node.*", "fixture.occurrence" },
                new HashSet<string> { "tap" }, new HashSet<string>(), ["controlled"],
                Obligations: [new("observed", RunObligationKind.Objective, "fixture.occurrence", "observed", true)],
                MaxConsultations: 4, MaxTotalSteps: 10,
                TaskInstanceId: "original-conflict-task", ProductSessionId: "original-conflict-session", ContractRef: "original-conflict-contract"));
            Assert.True(a.Accepted, a.RejectionReason); Assert.True(driver.Activate().Accepted); var drive = driver.Drive();
            Assert.Equal(5, ledger.AdmissionLog.Count);
            Assert.True(world.Current!.Conflicts.Count == 0, JsonSerializer.Serialize(new { drive, world.Current.Occurrences, world.Current.WorldState }));
        }
        var read = ReadNative(new(run, wid, eid, wp, ep));
        var resolution = Assert.Single(read.Operations.Where(r => r.Operation == "conflict-resolution"));
        Assert.Single(resolution.Data.GetProperty("revisions").EnumerateArray());
        Assert.Equal("checked", resolution.Data.GetProperty("conflictResolutions")[0].GetProperty("resolvedValue").GetString());
        Assert.Empty(resolution.Data.GetProperty("processedEvidenceIds").EnumerateArray());
    }

    [Fact]
    public void OriginalHostWorldBindsTheActualRunAndEvidenceRecordingSources()
    {
        var result = RunResourceRecoveryTests.Run(_root);
        using var run = new FileRunStateJournal(Path.Combine(result.RunDir, "run-state.journal.jsonl"), true);
        using var evidence = new FileEvidenceJournal(Path.Combine(result.RunDir, "evidence.journal.jsonl"), true);
        using var world = new FileWorldJournal(Path.Combine(result.RunDir, "world.journal.jsonl"), true);
        var read = WorldHistoryRead.ReadOriginal(world, world.RecordingId, run.RecordingId, EvidenceLedger.RestoreReadOnly(evidence, evidence.RecordingId));
        Assert.NotEmpty(read.HistoricalRevisions);
        Assert.Equal(run.RecordingId, read.RunRecordingId); Assert.Equal(evidence.RecordingId, read.EvidenceRecordingId);
        var graph = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.RunDir, "world-navigation.json"))).RootElement;
        Assert.Equal(graph.GetProperty("sourceRevisionId").GetString(), read.HistoricalRevisions[^1].GetProperty("revisionId").GetString());
        Export("production", result.RunDir);
    }

    [Fact]
    public void AppliedCorrectionKeepsOriginalAttributionRevocationAndPostPublishLog()
    {
        var ep = Path.Combine(_root, "evidence"); var wp = Path.Combine(_root, "world");
        var run = Guid.NewGuid().ToString("N"); string wid, eid, target, captureId, oldIdentity;
        using (var e = new FileEvidenceJournal(ep))
        using (var w = new FileWorldJournal(wp))
        {
            wid = w.RecordingId; eid = e.RecordingId;
            var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject,
                PageInterpretationCodec.Subject, "ui.node.*" }, new ProductAssociationStrategy(), history: new(w, run, eid));
            var kernel = new UniKernel(new EvidenceLedger(e), world, DisabledRunTrace.Instance);
            PageCaptureObservation Capture(string id, int second) => new(id, "cycle:" + id, "session",
                DateTimeOffset.Parse("2026-10-24T00:00:00Z").AddSeconds(second), CoordinateSpace.DeviceViewport(100, 100, captureId: id),
                [new("title", "text", "Settings", new UiBounds(0, 0, 90, 20)),
                 new("row", "ui.element", "Settings", new UiBounds(0, 30, 90, 60))]) { DeviceId = "fixture-device" };
            string Identify(PageCaptureObservation c, string signature)
            {
                var source = kernel.Process(new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(c)),
                    IngressKind.Observation, ObservationContext.External, new("recorded-layout", c.CaptureTime, "capture:" + c.CaptureId, ["controlled"]))).Admission.EvidenceId!;
                kernel.Process(new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(new PageIdentityObservation(
                    c.CaptureId, c.ObservationCycleId, c.SessionCorrelation, c.CaptureTime, signature, "Settings"))), IngressKind.Observation,
                    ObservationContext.External, new("identity-supply", c.CaptureTime, "identity:" + c.CaptureId, ["controlled"]))
                { DerivedFromEvidenceIds = [source], TransitiveEvidenceBasis = new HashSet<string> { source }, FusionRule = "page.identity.observed-title-layout", FusionVersion = "1" });
                return source;
            }
            Identify(Capture("known-b", 1), "native:page:B"); target = world.Current!.CurrentPageObservation!.ContainerId!;
            var original = Capture("original-a", 2); captureId = Identify(original, "page:A");
            oldIdentity = world.Current!.CurrentPageObservation!.IdentityEvidenceId!;
            var descriptor = new HierarchyCaptureDescriptor(original.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
                "controlled-native-profile:v1", UiHierarchyFormat.UiAutomatorXml, "fixture-device", original.SessionCorrelation,
                original.ObservationCycleId, original.CaptureTime, null, HierarchyCapability.SemanticText, CoverageCompleteness.Partial,
                "viewport", 0, null, "resource_id", original.Space);
            var support = kernel.Process(new(new("ui.node." + original.CaptureId + "#0.resource_id", "native:page:B"), IngressKind.Observation,
                ObservationContext.External, new("platform.uiautomator", original.CaptureTime, "context:" + original.CaptureId,
                    ["typed-hierarchy:v1", PageInterpretationCodec.NativeContextProfile], descriptor))).Admission.EvidenceId!;
            var correction = new PageInterpretationCorrection(captureId, oldIdentity,
                new(original.CaptureId, original.ObservationCycleId, original.SessionCorrelation, original.CaptureTime, "native:page:B", "Settings"),
                [support], "controlled-native-context:v1");
            kernel.Process(new(new(PageInterpretationCodec.Subject, PageInterpretationCodec.Encode(correction)), IngressKind.Observation,
                ObservationContext.External, new("history-interpreter", original.CaptureTime, "reinterpret:" + captureId, ["controlled"]))
            { DerivedFromEvidenceIds = [captureId, support], TransitiveEvidenceBasis = new HashSet<string> { captureId, support },
                FusionRule = "page.interpretation", FusionVersion = "1" });
            Assert.True(world.PageCorrectionLog[^1].Applied, world.PageCorrectionLog[^1].Reason);
            Assert.Equal(target, world.Current!.CurrentPageObservation!.ContainerId);
        }
        var read = ReadNative(new(run, wid, eid, wp, ep));
        var op = Assert.Single(read.Operations.Where(r => r.Operation == "reconcile" && r.Data.GetProperty("pageCorrections").GetArrayLength() == 1));
        Assert.True(op.Data.GetProperty("pageCorrections")[0].GetProperty("applied").GetBoolean());
        Assert.Equal(target, read.HistoricalRevisions[^1].GetProperty("currentPageObservation").GetProperty("containerId").GetString());
        Assert.Contains(op.Data.GetProperty("revokedInterpretations").EnumerateArray(), id => id.GetString() == oldIdentity);
        Assert.Contains(read.HistoricalRevisions, r => r.GetProperty("currentPageObservation").ValueKind == JsonValueKind.Object
            && r.GetProperty("currentPageObservation").GetProperty("identityEvidenceId").GetString() == oldIdentity);
        using (var er = new FileEvidenceJournal(ep, true))
        using (var wr = new FileWorldJournal(wp, true))
        {
            var missingRevocation = new AlteredRead(wr, row =>
            {
                if (row.Operation == "initialize") return row;
                var data = JsonNode.Parse(row.Data.GetRawText())!; data["revokedInterpretations"] = new JsonArray();
                return row with { Data = JsonSerializer.SerializeToElement(data) };
            });
            Assert.Throws<InvalidDataException>(() => WorldHistoryRead.ReadOriginal(missingRevocation, wid, run, EvidenceLedger.RestoreReadOnly(er, eid)));
        }
        Export("applied-correction", _root);
    }

    private static void Export(string name, string directory)
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_WORLD_HISTORY_EXPORT") is not { Length: > 0 } root) return;
        var target = Path.Combine(root, name); Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(directory)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
    }

    [Fact]
    public void OriginalHistoryWritesNewRevisionAndLogSuffixesRatherThanTheirEntirePrefixes()
    {
        var ep = Path.Combine(_root, "evidence"); var wp = Path.Combine(_root, "world");
        var run = Guid.NewGuid().ToString("N"); string wid, eid;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        using (var e = new FileEvidenceJournal(ep))
        using (var w = new FileWorldJournal(wp))
        {
            wid = w.RecordingId; eid = e.RecordingId;
            var world = new WorldModel(new HashSet<string> { "switch" }, history: new(w, run, eid));
            var kernel = new UniKernel(new EvidenceLedger(e), world, DisabledRunTrace.Instance);
            for (var i = 0; i < 200; i++)
            {
                var seed = Seed(); kernel.Process(seed with { Provenance = seed.Provenance with { CaptureTime = seed.Provenance.CaptureTime.AddSeconds(i) } });
            }
            Assert.Equal(401, world.HistoryPosition!.Sequence);
        }
        var writeMs = watch.Elapsed.TotalMilliseconds; watch.Restart();
        var read = ReadNative(new(run, wid, eid, wp, ep)); var readMs = watch.Elapsed.TotalMilliseconds;
        Assert.Equal(200, read.HistoricalRevisions.Count);
        Assert.Equal(200, read.Operations.Skip(1).Sum(r => r.Data.GetProperty("revisions").GetArrayLength()));
        Assert.All(read.Operations.Skip(1), r => Assert.InRange(r.Data.GetProperty("revisions").GetArrayLength(), 0, 1));
        if (Environment.GetEnvironmentVariable("UNICLAW_WORLD_HISTORY_EXPORT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "cost.json"), JsonSerializer.Serialize(new { observations = 200, operations = read.Position.Sequence,
                revisions = read.HistoricalRevisions.Count, worldBytes = new FileInfo(wp).Length, writeMs, readMs,
                limitation = "Owner candidate collections are copied in memory; revision aggregates still contain their original growing basis. Not a long-run performance certificate." }));
        }
    }

    [Theory]
    [InlineData("world")]
    [InlineData("world-empty")]
    public async Task ActualNonterminalProcessKillPreservesOnlyOriginalHistoricalWorld(string mode)
    {
        using var writer = StartProbe("write", _root, mode);
        try
        {
            var timeout = DateTime.UtcNow.AddSeconds(35);
            var marker = Path.Combine(_root, "ready.json");
            while (!File.Exists(marker) && !writer.HasExited && DateTime.UtcNow < timeout) await Task.Delay(25);
            Assert.True(File.Exists(marker), writer.HasExited ? await writer.StandardError.ReadToEndAsync() : "original writer timeout");
            using var document = JsonDocument.Parse(File.ReadAllText(marker)); var ready = document.RootElement;
            Assert.False(ready.GetProperty("Terminal").GetBoolean()); Assert.Equal("WaitingForInput", ready.GetProperty("Status").GetString());
            Assert.Equal(writer.Id, ready.GetProperty("ProcessId").GetInt32());
            writer.Kill(true); await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.NotEqual(0, writer.ExitCode);
            var files = Directory.GetFiles(_root).ToDictionary(p => p, p => SHA256.HashData(File.ReadAllBytes(p)));
            File.WriteAllText(Path.Combine(_root, "original-files-before-read.json"), JsonSerializer.Serialize(
                files.ToDictionary(p => Path.GetFileName(p.Key), p => Convert.ToHexString(p.Value))));
            for (var repeat = 0; repeat < 2; repeat++)
            {
                using var reader = StartProbe("read-world", _root, ready.GetProperty("RunId").GetString()!, ready.GetProperty("WorldRecordingId").GetString()!);
                try
                {
                    await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
                    Assert.Equal(0, reader.ExitCode);
                    using var result = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "world-reader-result.json")));
                    var read = result.RootElement;
                    Assert.NotEqual(writer.Id, read.GetProperty("ProcessId").GetInt32());
                    Assert.False(read.GetProperty("IsTerminal").GetBoolean()); Assert.False(read.GetProperty("ContinuationSupported").GetBoolean());
                    Assert.True(read.GetProperty("RunIsReadOnly").GetBoolean()); Assert.True(read.GetProperty("EvidenceIsReadOnly").GetBoolean());
                    Assert.Equal(mode == "world-empty" ? 0 : 1, read.GetProperty("HistoricalRevisions").GetArrayLength());
                    using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "world-original-expected.json")));
                    Assert.True(JsonElement.DeepEquals(expected.RootElement.GetProperty("revisions"), read.GetProperty("HistoricalRevisions")));
                    if (mode == "world")
                    {
                        var last = read.GetProperty("Operations").EnumerateArray().Last().GetProperty("Data");
                        Assert.True(JsonElement.DeepEquals(expected.RootElement.GetProperty("demands"), last.GetProperty("continuityDemands")));
                        Assert.True(JsonElement.DeepEquals(expected.RootElement.GetProperty("continuity"), last.GetProperty("continuityLog")));
                    }
                    Assert.Equal(ready.GetProperty("WorldPosition").GetProperty("Sequence").GetInt32(), read.GetProperty("Position").GetProperty("Sequence").GetInt32());
                    using var versionProcess = StartProbe("read-world-version", _root, ready.GetProperty("WorldRecordingId").GetString()!);
                    await versionProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
                    Assert.Equal(0, versionProcess.ExitCode);
                    using var versionDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "world-version-reader-result.json")));
                    var versionRead = versionDocument.RootElement;
                    Assert.False(versionRead.GetProperty("ContinuationSupported").GetBoolean());
                    Assert.False(versionRead.GetProperty("FullWorldHistoryValidated").GetBoolean());
                    if (mode == "world-empty") Assert.Equal(JsonValueKind.Null, versionRead.GetProperty("Version").ValueKind);
                    else Assert.True(JsonElement.DeepEquals(read.GetProperty("HistoricalRevisions")[0], versionRead.GetProperty("Version").GetProperty("Revision")));
                    foreach (var (file, hash) in files) Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(file)));
                    File.Copy(Path.Combine(_root, "world-reader-result.json"), Path.Combine(_root, "world-reader-result-" + repeat + ".json"), true);
                }
                finally { if (!reader.HasExited) { reader.Kill(true); await reader.WaitForExitAsync(); } }
            }
            Export(mode, _root);
        }
        finally { if (!writer.HasExited) { writer.Kill(true); await writer.WaitForExitAsync(); } }
    }

    private static Process StartProbe(params string[] arguments)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools/RunRecoveryProbe/RunRecoveryProbe.csproj"))) root = root.Parent;
        if (root is null) throw new DirectoryNotFoundException("original probe missing");
        var config = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Path.Combine(root.FullName, "tools/RunRecoveryProbe/bin", config, "net10.0/UniClaw.RunRecoveryProbe.dll"));
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return Process.Start(info)!;
    }
}
