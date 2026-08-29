using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Runtime.ValidationHarness.Knowledge;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-P26-D capability tests: ScenarioKnowledgeStore freeze/load persistence
/// (spec requirement "Human-readable persisted asset": "persisted as
/// human-readable, diffable, deterministic, versioned content with explicit
/// scope — never an opaque blob as the sole knowledge representation"; spec
/// "Knowledge persistence and cross-campaign reuse"; design D2) —
///  1. round-trip fidelity: freeze → load (matching scope) preserves every
///     record incl. lifecycle fields and the Supersedes/SupersededBy pair;
///  2. determinism: same fixture content ⇒ byte-identical records.json /
///     manifest.json / FIXTURE.md (even when the created-from run set is
///     supplied in different orders — set semantics);
///  3. version supersession: v2's manifest declares v1 (supersedesVersion),
///     v1 loads independently and unchanged;
///  4. cross-scope leak: a scope differing in one field rejects every record
///     with a scope-mismatch reason, zero loaded;
///  5. tamper: an altered content field (Confidence) breaks the recomputed
///     SHA-256 RecordId → that record rejected, the others load;
///  6. container tamper: records.json edited without updating the manifest
///     recordsSha256 → whole load throws InvalidOperationException (a tampered
///     container is not per-record rejection);
///  7. admission revalidation: a hand-edited record with identity-consistent
///     but provenance-violating content is rejected by the fixture's gate;
///  8. markdown: FIXTURE.md carries the header, per-record lines, and the
///     lifecycle statistics table — and no absolute paths.
/// Structure follows the C-group tests: build records through the real
/// admission gate, freeze, then gate-checked load; assertions verify
/// capabilities (fidelity, determinism, isolation, tamper detection) — never
/// fixed click counts, coordinates, page text, or UI paths.
/// </summary>
public sealed class ScenarioKnowledgeStoreTests
{
    private const string ScenarioId = "settings-real-emulator";

    private static KnowledgeScope Scope(
        string? scenario = null,
        string? app = null,
        string? capabilityId = null,
        string? capabilityVersion = null,
        string? android = null,
        string? locale = null,
        string[]? runs = null)
        => new(
            ScenarioId: scenario ?? ScenarioId,
            ApplicationPackage: app ?? "com.android.settings",
            SemanticCapabilityId: capabilityId ?? "uni-claw.settings.semantic",
            SemanticCapabilityVersion: capabilityVersion ?? "1",
            AndroidAssumptions: android ?? "emulator google_apis;API 35",
            Locale: locale ?? "en-US",
            CreatedFromRunIds: runs ?? new[] { "run-1" });

    private static ScenarioKnowledgeRecord Observed(
        KnowledgeScope scope,
        string? anchor = null,
        string? runId = null,
        IReadOnlyList<string>? evidenceRefs = null,
        KnowledgeType type = KnowledgeType.KnownContainer,
        KnowledgeStatus status = KnowledgeStatus.Active,
        int version = 1,
        int ordinal = 1,
        double confidence = 0.9,
        string? disposition = "record-only observed",
        string? supersedes = null)
        => new(
            KnowledgeType: type,
            SemanticAnchor: anchor ?? "settings.container:Settings-root",
            SourceRunId: runId ?? "run-1",
            EvidenceRefs: evidenceRefs ?? new[] { "evidence:run-1:obs-1" },
            ObservedRole: "container observed",
            Scope: scope,
            Disposition: disposition ?? "record-only observed",
            Confidence: confidence,
            ValidityAssumption: "stable across frames",
            Version: version,
            Status: status,
            AdmissionOrdinal: ordinal,
            Supersedes: supersedes);

    /// <summary>Five active records across five knowledge types, built through
    /// the real admission gate.</summary>
    private static ScenarioKnowledgeFixture BuildBaseFixture(KnowledgeScope scope)
    {
        var fixture = new ScenarioKnowledgeFixture(scope);
        Assert.IsType<KnowledgeAdmission.Admitted>(fixture.Admit(Observed(
            scope, anchor: "settings.container:Network & internet", type: KnowledgeType.KnownContainer, runId: "run-1", ordinal: 1)));
        Assert.IsType<KnowledgeAdmission.Admitted>(fixture.Admit(Observed(
            scope, anchor: "settings.preference-row:Airplane mode", type: KnowledgeType.KnownLocalControl, runId: "run-2", ordinal: 2)));
        Assert.IsType<KnowledgeAdmission.Admitted>(fixture.Admit(Observed(
            scope, anchor: "settings.preference-row:Storage", type: KnowledgeType.KnownRecordOnly, runId: "run-3", ordinal: 3)));
        Assert.IsType<KnowledgeAdmission.Admitted>(fixture.Admit(Observed(
            scope, anchor: "settings.text:Build number", type: KnowledgeType.KnownNonInteractive, runId: "run-4", ordinal: 4)));
        Assert.IsType<KnowledgeAdmission.Admitted>(fixture.Admit(Observed(
            scope, anchor: "settings.container:About phone", type: KnowledgeType.KnownExternalBoundary, runId: "run-5", ordinal: 5)));
        return fixture;
    }

    /// <summary>Per-test temp asset root under Path.GetTempPath(), cleaned up
    /// on dispose (the store never sees real repo paths in these tests).</summary>
    private sealed class TempDirectory : IDisposable
    {
        public string Root { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "scenarioknowledgestore-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    // ── 1. Round-trip fidelity ────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_FreezeThenLoadMatchingScope_AllRecordsStatusesAndLinksPreserved()
    {
        using var temp = new TempDirectory();
        var scope = Scope();

        // ≥5 records spanning ≥3 knowledge types + a superseded lifecycle pair.
        var fixture = BuildBaseFixture(scope);
        var older = Observed(scope, anchor: "settings.preference-row:Wi-Fi", runId: "run-1", version: 1, ordinal: 6, disposition: "replaced by newer observation");
        var newer = Observed(scope, anchor: "settings.preference-row:Wi-Fi", runId: "run-2",
            evidenceRefs: new[] { "evidence:run-2:obs-1" }, version: 2, ordinal: 7, supersedes: older.RecordId,
            disposition: "fresh observation wins");
        Assert.IsType<KnowledgeAdmission.Admitted>(fixture.Admit(older));
        Assert.IsType<KnowledgeAdmission.Admitted>(fixture.Admit(newer));
        var final = fixture.ApplyFreshEvidence(
            "settings.preference-row:Wi-Fi", scope, FreshEvidenceOutcome.Supersedes(older, newer.RecordId));

        var frozen = ScenarioKnowledgeStore.Freeze(final, ScenarioId, version: 1, temp.Root);
        Assert.Equal(final.Records.Count, frozen.RecordCount);
        Assert.True(File.Exists(frozen.RecordsPath));
        Assert.True(File.Exists(frozen.ManifestPath));
        Assert.True(File.Exists(frozen.MarkdownPath));

        var loaded = ScenarioKnowledgeStore.Load(frozen.Directory, scope);
        Assert.Empty(loaded.RejectedRecords);
        Assert.Equal(final.Records.Count, loaded.RecordsLoaded);
        Assert.Equal(final.Records.Count, loaded.Fixture.Records.Count);

        // Lifecycle pair survived the freeze/load: Superseded + SupersededBy link,
        // replacement Active + Supersedes link.
        var loadedOlder = loaded.Fixture.Records.Single(r => r.RecordId == older.RecordId);
        var loadedNewer = loaded.Fixture.Records.Single(r => r.RecordId == newer.RecordId);
        Assert.Equal(KnowledgeStatus.Superseded, loadedOlder.Status);
        Assert.Equal(newer.RecordId, loadedOlder.SupersededBy);
        Assert.Equal(KnowledgeStatus.Active, loadedNewer.Status);
        Assert.Equal(older.RecordId, loadedNewer.Supersedes);

        // Content + lifecycle fields preserved exactly.
        var loadedNetwork = loaded.Fixture.Records.Single(r =>
            r.SemanticAnchor == "settings.container:Network & internet");
        Assert.Equal(1, loadedNetwork.Version);
        Assert.Equal(1, loadedNetwork.AdmissionOrdinal);
        Assert.Equal(0.9, loadedNetwork.Confidence);
        Assert.Equal(new[] { "evidence:run-1:obs-1" }, loadedNetwork.EvidenceRefs);
        Assert.Equal(scope, loadedNetwork.Scope); // incl. created-from run set (set semantics)
        Assert.Equal("stable across frames", loadedNetwork.ValidityAssumption);
    }

    // ── 2. Determinism ────────────────────────────────────────────────────────

    [Fact]
    public void FreezeTwice_SameFixtureContent_AllThreeFilesByteIdentical()
    {
        using var tempA = new TempDirectory();
        using var tempB = new TempDirectory();

        var frozenA = ScenarioKnowledgeStore.Freeze(BuildBaseFixture(Scope()), ScenarioId, version: 1, tempA.Root);
        var frozenB = ScenarioKnowledgeStore.Freeze(BuildBaseFixture(Scope()), ScenarioId, version: 1, tempB.Root);

        Assert.Equal(File.ReadAllBytes(frozenA.RecordsPath), File.ReadAllBytes(frozenB.RecordsPath));
        Assert.Equal(File.ReadAllBytes(frozenA.ManifestPath), File.ReadAllBytes(frozenB.ManifestPath));
        Assert.Equal(File.ReadAllBytes(frozenA.MarkdownPath), File.ReadAllBytes(frozenB.MarkdownPath));
        Assert.Equal(frozenA.ContentSha256, frozenB.ContentSha256);
    }

    [Fact]
    public void Freeze_CreatedFromRunIdsSetOrderDoesNotChangeBytes()
    {
        // KnowledgeScope equality treats CreatedFromRunIds as a SET; the
        // serializer must sort them so order of construction never leaks into
        // the frozen bytes.
        using var tempA = new TempDirectory();
        using var tempB = new TempDirectory();

        var fixtureA = BuildBaseFixture(Scope(runs: new[] { "run-3", "run-1", "run-2" }));
        var fixtureB = BuildBaseFixture(Scope(runs: new[] { "run-1", "run-2", "run-3" }));

        var frozenA = ScenarioKnowledgeStore.Freeze(fixtureA, ScenarioId, version: 1, tempA.Root);
        var frozenB = ScenarioKnowledgeStore.Freeze(fixtureB, ScenarioId, version: 1, tempB.Root);

        Assert.Equal(frozenA.ContentSha256, frozenB.ContentSha256);
        Assert.Equal(File.ReadAllBytes(frozenA.RecordsPath), File.ReadAllBytes(frozenB.RecordsPath));
        Assert.Equal(File.ReadAllBytes(frozenA.ManifestPath), File.ReadAllBytes(frozenB.ManifestPath));
    }

    // ── 3. Version supersession ───────────────────────────────────────────────

    [Fact]
    public void VersionSupersession_FreezeV1ThenV2_ManifestDeclaresChain_V1LoadsIndependently()
    {
        using var temp = new TempDirectory();
        var scope = Scope();

        var v1 = BuildBaseFixture(scope);
        var v1Frozen = ScenarioKnowledgeStore.Freeze(v1, ScenarioId, version: 1, temp.Root);
        var manifestV1 = JsonNode.Parse(File.ReadAllText(v1Frozen.ManifestPath))!.AsObject();
        Assert.Equal(1, manifestV1["version"]!.GetValue<int>());
        Assert.True(manifestV1["supersedesVersion"] is null, "v1 manifest must declare no supersession.");

        // Mutate: supersede one record + add one new record.
        var target = v1.Records[0];
        var replacement = Observed(scope, anchor: target.SemanticAnchor, runId: "run-97",
            evidenceRefs: new[] { "evidence:run-97:obs-1" }, version: target.Version + 1, ordinal: 97,
            disposition: "fresh observation wins");
        var v2 = v1.ApplyFreshEvidence(target.SemanticAnchor, scope, FreshEvidenceOutcome.Supersedes(target, replacement.RecordId));
        Assert.IsType<KnowledgeAdmission.Admitted>(v2.Admit(replacement));
        Assert.IsType<KnowledgeAdmission.Admitted>(v2.Admit(Observed(
            scope, anchor: "settings.preference-row:Newly discovered", type: KnowledgeType.KnownUnresolved,
            runId: "run-98", ordinal: 98, disposition: "honest unknown")));

        var v2Frozen = ScenarioKnowledgeStore.Freeze(v2, ScenarioId, version: 2, temp.Root, supersedesVersion: 1);
        var manifestV2 = JsonNode.Parse(File.ReadAllText(v2Frozen.ManifestPath))!.AsObject();
        Assert.Equal(2, manifestV2["version"]!.GetValue<int>());
        Assert.Equal(1, manifestV2["supersedesVersion"]!.GetValue<int>());

        // v1 still loads independently and unchanged (no auto-merge with v2).
        var loadedV1 = ScenarioKnowledgeStore.Load(v1Frozen.Directory, scope);
        Assert.Empty(loadedV1.RejectedRecords);
        Assert.Equal(v1.Records.Count, loadedV1.RecordsLoaded);
        Assert.All(loadedV1.Fixture.Records, r => Assert.Equal(KnowledgeStatus.Active, r.Status));
        Assert.DoesNotContain(loadedV1.Fixture.Records, r => r.RecordId == replacement.RecordId);
    }

    // ── 4. Cross-scope leak: load is a gate ───────────────────────────────────

    [Fact]
    public void Load_ScopeDiffersInOneField_AllRecordsRejectedWithScopeMismatch_ZeroLoaded()
    {
        using var temp = new TempDirectory();
        var fixture = BuildBaseFixture(Scope());
        var frozen = ScenarioKnowledgeStore.Freeze(fixture, ScenarioId, version: 1, temp.Root);

        var otherCapabilityVersion = Scope(capabilityVersion: "2");
        var loaded = ScenarioKnowledgeStore.Load(frozen.Directory, otherCapabilityVersion);

        Assert.Equal(0, loaded.RecordsLoaded);
        Assert.Empty(loaded.Fixture.Records);
        Assert.Equal(fixture.Records.Count, loaded.RejectedRecords.Count);
        Assert.All(
            loaded.RejectedRecords,
            r => Assert.Contains("scope mismatch: SemanticCapabilityVersion", r.Reason));
    }

    // ── 5. Tamper: RecordId recompute rejects altered content ─────────────────

    [Fact]
    public void Load_TamperedConfidenceBreaksRecordId_ThatRecordRejected_OthersLoad()
    {
        using var temp = new TempDirectory();
        var fixture = BuildBaseFixture(Scope());
        var frozen = ScenarioKnowledgeStore.Freeze(fixture, ScenarioId, version: 1, temp.Root);

        // Alter ONE record's Confidence (a content/identity field) and rewrite
        // records.json with the same canonical writer. The container hash is
        // refreshed exactly as a real hand-editor of this human-readable asset
        // would (recompute the manifest recordsSha256) — the per-record
        // RecordId recompute is what catches the tampered content. The other
        // records are untouched and must load.
        var root = JsonNode.Parse(File.ReadAllText(frozen.RecordsPath))!.AsObject();
        var tamperedTarget = root["records"]!.AsArray()[0]!.AsObject();
        var declaredId = tamperedTarget["RecordId"]!.GetValue<string>();
        tamperedTarget["Confidence"] = JsonNode.Parse("0.5");
        WriteAndRefresh(frozen, root);

        var loaded = ScenarioKnowledgeStore.Load(frozen.Directory, Scope());
        var rejection = Assert.Single(loaded.RejectedRecords);
        Assert.Equal(declaredId, rejection.RecordId);
        Assert.Contains("RecordId mismatch", rejection.Reason);
        Assert.Equal(fixture.Records.Count - 1, loaded.RecordsLoaded);
        Assert.DoesNotContain(loaded.Fixture.Records, r => r.RecordId == declaredId);
    }

    // ── 6. Container tamper: whole load fails ─────────────────────────────────

    [Fact]
    public void Load_RecordsEditedWithoutManifestShaUpdate_ThrowsInvalidOperationException()
    {
        using var temp = new TempDirectory();
        var frozen = ScenarioKnowledgeStore.Freeze(BuildBaseFixture(Scope()), ScenarioId, version: 1, temp.Root);

        // Any byte-level edit to records.json without updating the manifest's
        // recordsSha256 is container tampering → distinct whole-load failure.
        var root = JsonNode.Parse(File.ReadAllText(frozen.RecordsPath))!.AsObject();
        root["records"]!.AsArray()[0]!.AsObject()["Disposition"] = "tampered disposition";
        WriteTampered(root, frozen.RecordsPath);

        var ex = Assert.Throws<InvalidOperationException>(() => ScenarioKnowledgeStore.Load(frozen.Directory, Scope()));
        Assert.Contains("recordsSha256 mismatch", ex.Message);
    }

    // ── 7. Admission revalidation rejects hand-edited provenance gap ──────────

    [Fact]
    public void Load_HandEditedRecordWithConsistentIdButMissingProvenance_RejectedByAdmissionGate()
    {
        using var temp = new TempDirectory();
        var scope = Scope();
        var fixture = BuildBaseFixture(scope);
        var frozen = ScenarioKnowledgeStore.Freeze(fixture, ScenarioId, version: 1, temp.Root);

        // A hand-edit that KEEPS identity consistent (recomputes the RecordId
        // for the new content) but violates the provenance gate — blank
        // SourceRunId. The store must re-run the admission gate and reject it,
        // never silently loading provenance-less knowledge.
        var root = JsonNode.Parse(File.ReadAllText(frozen.RecordsPath))!.AsObject();
        JsonObject? handEditedNode = null;
        foreach (var node in root["records"]!.AsArray())
        {
            var recordNode = node!.AsObject();
            if (recordNode["SemanticAnchor"]!.GetValue<string>() == "settings.container:Network & internet")
            {
                handEditedNode = recordNode;
                break;
            }
        }

        Assert.NotNull(handEditedNode);
        var baseRecord = Observed(scope, anchor: "settings.container:Network & internet",
            type: KnowledgeType.KnownContainer, runId: "run-1", ordinal: 1);
        // A hand-edit with a RECOMPUTED identity for the new content: built via
        // the record's primary constructor so RecordId re-derives from the
        // (blank-provenance) content — exactly what a hand-editor who keeps
        // identity consistent must produce.
        var handEdited = new ScenarioKnowledgeRecord(
            KnowledgeType: baseRecord.KnowledgeType,
            SemanticAnchor: baseRecord.SemanticAnchor,
            SourceRunId: "   ",
            EvidenceRefs: baseRecord.EvidenceRefs,
            ObservedRole: baseRecord.ObservedRole,
            Scope: baseRecord.Scope,
            Disposition: baseRecord.Disposition,
            Confidence: baseRecord.Confidence,
            ValidityAssumption: baseRecord.ValidityAssumption,
            Version: baseRecord.Version,
            Status: KnowledgeStatus.Active,
            AdmissionOrdinal: baseRecord.AdmissionOrdinal);
        Assert.NotEqual(baseRecord.RecordId, handEdited.RecordId);
        handEditedNode!["SourceRunId"] = "   ";
        handEditedNode["RecordId"] = handEdited.RecordId;
        WriteAndRefresh(frozen, root);

        var loaded = ScenarioKnowledgeStore.Load(frozen.Directory, scope);
        var rejection = Assert.Single(loaded.RejectedRecords);
        Assert.Equal(handEdited.RecordId, rejection.RecordId);
        Assert.Contains("admission rejected", rejection.Reason);
        Assert.Contains("SourceRunId", rejection.Reason);
        Assert.Equal(fixture.Records.Count - 1, loaded.RecordsLoaded);
        Assert.DoesNotContain(loaded.Fixture.Records, r => r.RecordId == handEdited.RecordId);
    }

    // ── 8. Markdown digest ────────────────────────────────────────────────────

    [Fact]
    public void Freeze_MarkdownContainsHeaderRecordLinesAndStatistics_NoAbsolutePaths()
    {
        using var temp = new TempDirectory();
        var scope = Scope();
        var fixture = new ScenarioKnowledgeFixture(scope);
        fixture.Admit(Observed(scope, anchor: "settings.container:Settings-root", type: KnowledgeType.KnownContainer,
            runId: "run-1", ordinal: 1, confidence: 0.9));
        var recordOnly = Observed(scope, anchor: "settings.preference-row:Storage", type: KnowledgeType.KnownRecordOnly,
            runId: "run-2", ordinal: 2, confidence: 0.85);
        fixture.Admit(recordOnly);
        fixture.Admit(Observed(scope, anchor: "settings.preference-row:Airplane mode", type: KnowledgeType.KnownLocalControl,
            runId: "run-3", ordinal: 3, confidence: 0.95));
        var final = fixture.ApplyFreshEvidence(recordOnly.SemanticAnchor, scope, FreshEvidenceOutcome.Stales(recordOnly));

        var frozen = ScenarioKnowledgeStore.Freeze(final, ScenarioId, version: 1, temp.Root);
        var markdown = File.ReadAllText(frozen.MarkdownPath);

        // Header + scope block.
        Assert.Contains($"# ScenarioKnowledgeFixture — {ScenarioId}", markdown);
        Assert.Contains($"Scenario: {ScenarioId}", markdown);
        Assert.Contains("Version: v1", markdown);
        Assert.Contains("Supersedes: none", markdown);
        Assert.Contains("ScenarioId: settings-real-emulator", markdown);
        Assert.Contains("SemanticCapabilityVersion: 1", markdown);

        // Per-record digest lines (one per record).
        Assert.Contains("- [Active] KnownContainer settings.container:Settings-root (run=run-1, conf=0.9) — record-only observed", markdown);
        Assert.Contains("- [Stale] KnownRecordOnly settings.preference-row:Storage (run=run-2, conf=0.85) — record-only observed", markdown);
        Assert.Contains("- [Active] KnownLocalControl settings.preference-row:Airplane mode (run=run-3, conf=0.95) — record-only observed", markdown);

        // Lifecycle statistics table.
        Assert.Contains("## Lifecycle statistics", markdown);
        Assert.Contains("| KnowledgeType | Status | Count |", markdown);
        Assert.Contains("| KnownContainer | Active | 1 |", markdown);
        Assert.Contains("| KnownRecordOnly | Stale | 1 |", markdown);

        // No absolute paths / temp dirs inside the asset text.
        Assert.DoesNotContain(temp.Root, markdown);
        Assert.DoesNotContain(Path.GetTempPath(), markdown);
        Assert.DoesNotContain(frozen.RecordsPath, markdown);
    }

    /// <summary>Rewrite a mutated records.json with the SAME canonical writer
    /// the store uses (indent 2, LF, minimal escaping).</summary>
    private static readonly JsonSerializerOptions TamperWriterOptions = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static void WriteTampered(JsonNode root, string path)
        => File.WriteAllText(path, root.ToJsonString(TamperWriterOptions), new UTF8Encoding(false));

    /// <summary>
    /// Hand-edit flow: write the mutated records.json AND refresh the manifest
    /// recordsSha256 to the new bytes — the container hash is recomputed by the
    /// editor (human-readable asset), which is exactly why the per-record
    /// RecordId recompute gate exists: a consistent container hash does NOT
    /// protect an altered record.
    /// </summary>
    private static void WriteAndRefresh(FrozenFixture frozen, JsonNode mutatedRecordsRoot)
    {
        WriteTampered(mutatedRecordsRoot, frozen.RecordsPath);
        var newHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(frozen.RecordsPath)));
        var manifest = JsonNode.Parse(File.ReadAllText(frozen.ManifestPath))!.AsObject();
        manifest["recordsSha256"] = newHash;
        File.WriteAllText(frozen.ManifestPath, manifest.ToJsonString(TamperWriterOptions), new UTF8Encoding(false));
    }
}