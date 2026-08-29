using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UniClaw.Runtime.ValidationHarness.Knowledge;

/// <summary>
/// Freeze/load persistence for <see cref="ScenarioKnowledgeFixture"> (spec
/// requirement "Human-readable persisted asset": "persisted as human-readable,
/// diffable, deterministic, versioned content with explicit scope — never an
/// opaque blob as the sole knowledge representation"; spec requirement
/// "Knowledge persistence and cross-campaign reuse"; design D2 —
/// "ScenarioKnowledgeFixture is a test asset, not ephemeral-only, not Memory").
///
/// Layout (all under a validation-side asset root, never Runtime, never
/// production resources):
/// <code>&lt;root&gt;/&lt;scenarioId&gt;/v&lt;version&gt;/records.json | FIXTURE.md | manifest.json</code>
///
/// Freeze guarantees:
/// <list type="bullet">
/// <item><b>Human-readable + diffable</b>: records.json is explicit JSON with the
/// canonical property order (RecordId, KnowledgeType, SemanticAnchor,
/// SourceRunId, EvidenceRefs, ObservedRole, Scope, Disposition, Confidence,
/// ValidityAssumption, Version, Status, Supersedes, SupersededBy,
/// AdmissionOrdinal); FIXTURE.md is a generated digest; records ordered by
/// RecordId (ordinal). No opaque blob representation.</item>
/// <item><b>Deterministic</b>: zero DateTime, zero absolute paths, zero machine
/// names; confidence uses invariant "R" round-trip; CreatedFromRunIds serialize
/// SORTED (set semantics, order-independent like <see cref="KnowledgeScope"/>
/// equality); same fixture content ⇒ byte-identical files.</item>
/// </list>
///
/// Load is a GATE, not a restore:
/// <list type="bullet">
/// <item>container integrity first — manifest recordsSha256 must equal the
/// actual records.json bytes; a mismatch (or a missing/corrupt container)
/// throws, because a tampered container is not per-record rejection;</item>
/// <item>per-record revalidation — strict JSON field checks, RecordId
/// recomputed (SHA-256 over canonical content) and compared against the stored
/// value (mismatch ⇒ tampering ⇒ rejected), scope must
/// <see cref="KnowledgeScope.Matches"/> the expected scope (no cross-scope
/// leak), then the fixture's own admission gate re-runs (a hand-edited record
/// that kept identity consistent but violates provenance is still rejected);
/// every rejection is reported in <see cref="LoadRejection"/>, never silently
/// fixed or dropped.</item>
/// </list>
/// </summary>
public static class ScenarioKnowledgeStore
{
    private const string RecordsSchema = "uniclaw.scenarioKnowledge.v1";
    private const string ManifestSchema = "uniclaw.scenarioKnowledge.manifest.v1";
    private const string RecordsFileName = "records.json";
    private const string ManifestFileName = "manifest.json";
    private const string MarkdownFileName = "FIXTURE.md";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Indent 2 spaces, LF line endings, minimal escaping (human-readable
        // asset — not HTML context). Fixed options ⇒ deterministic bytes.
        WriteIndented = true,
        IndentSize = 2,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Freeze a fixture as a human-readable, deterministic, versioned asset set.
    /// Writes <c>&lt;root&gt;/&lt;scenarioId&gt;/v&lt;version&gt;/</c> with
    /// records.json, manifest.json, and FIXTURE.md — all three derived ONLY
    /// from the fixture content, scenario id, version, and supersession link
    /// (no timestamps, no absolute paths, no machine names). Freezing the same
    /// fixture twice produces byte-identical files.
    /// </summary>
    /// <param name="fixture">The validated in-memory fixture (result of
    /// campaign knowledge) to persist.</param>
    /// <param name="scenarioId">Scenario identifier; the asset directory
    /// segment under <paramref name="rootDirectory"/>.</param>
    /// <param name="version">Fixture version (v1 = first freeze).</param>
    /// <param name="rootDirectory">Validation-side asset root
    /// (e.g. <c>validation/knowledge/settings</c>) — the store never writes
    /// into Runtime or production resources.</param>
    /// <param name="supersedesVersion">The frozen version this one supersedes
    /// (explicit supersession chain in the manifest; null for v1). No
    /// automatic merging of historical versions at load.</param>
    /// <returns>The persisted asset locations + deterministic records digest.</returns>
    public static FrozenFixture Freeze(
        ScenarioKnowledgeFixture fixture,
        string scenarioId,
        int version,
        string rootDirectory,
        int? supersedesVersion = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("scenarioId is required.", nameof(scenarioId));
        }

        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("rootDirectory is required.", nameof(rootDirectory));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Fixture version must be >= 1.");
        }

        if (supersedesVersion is < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(supersedesVersion), supersedesVersion, "supersedesVersion must be >= 1 when given.");
        }

        var directory = ResolveVersionDirectory(rootDirectory, scenarioId, version);
        Directory.CreateDirectory(directory);

        // Stable record order: by RecordId (ordinal) — diffable and deterministic.
        var records = fixture.Records.OrderBy(r => r.RecordId, StringComparer.Ordinal).ToArray();

        var recordsJson = BuildRecordsJson(records);
        var recordsBytes = Utf8NoBom.GetBytes(recordsJson);
        var recordsSha256 = Convert.ToHexStringLower(SHA256.HashData(recordsBytes));

        var activeCount = records.Count(r => r.Status == KnowledgeStatus.Active);
        var manifestJson = BuildManifestJson(scenarioId, version, supersedesVersion, records.Length, activeCount, recordsSha256, fixture.OwnerScope);
        var markdown = BuildMarkdown(scenarioId, version, supersedesVersion, fixture.OwnerScope, records);

        var recordsPath = Path.Combine(directory, RecordsFileName);
        var manifestPath = Path.Combine(directory, ManifestFileName);
        var markdownPath = Path.Combine(directory, MarkdownFileName);

        File.WriteAllText(recordsPath, recordsJson, Utf8NoBom);
        File.WriteAllText(manifestPath, manifestJson, Utf8NoBom);
        File.WriteAllText(markdownPath, markdown, Utf8NoBom);

        return new FrozenFixture(directory, recordsPath, manifestPath, markdownPath, records.Length, recordsSha256);
    }

    /// <summary>
    /// Load a frozen version directory, revalidating EVERY record against the
    /// expected scope (no cross-scope leakage) and the admission gate.
    /// Container-level tampering (recordsSha256 mismatch, corrupt/missing
    /// container, schema mismatch) throws <see cref="InvalidOperationException"/>
    /// — a tampered container fails the whole load; per-record problems are
    /// reported in <see cref="LoadedFixture.RejectedRecords"/> and never
    /// silently fixed.
    /// </summary>
    /// <param name="versionDirectory">The exact directory of ONE frozen version
    /// (see <see cref="ResolveVersionDirectory"/>), e.g.
    /// <c>&lt;root&gt;/&lt;scenarioId&gt;/v2</c>.</param>
    /// <param name="expectedScope">The current session's reuse scope; ONLY
    /// records matching it (all six context fields, created-from run set
    /// excluded, per <see cref="KnowledgeScope.Matches"/>) load — a mismatched
    /// record is rejected, so knowledge never leaks across scenario/app/
    /// capability-version/locale/android contexts.</param>
    /// <returns>The rebuilt fixture + the load ledger.</returns>
    public static LoadedFixture Load(string versionDirectory, KnowledgeScope expectedScope)
    {
        ArgumentNullException.ThrowIfNull(expectedScope);
        if (string.IsNullOrWhiteSpace(versionDirectory))
        {
            throw new ArgumentException("versionDirectory is required.", nameof(versionDirectory));
        }

        var recordsPath = Path.Combine(versionDirectory, RecordsFileName);
        var manifestPath = Path.Combine(versionDirectory, ManifestFileName);
        if (!File.Exists(recordsPath))
        {
            throw new InvalidOperationException(
                $"Frozen fixture container is incomplete: '{RecordsFileName}' not found under '{versionDirectory}'. "
                + "Refusing to load an incomplete container.");
        }

        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException(
                $"Frozen fixture container is incomplete: '{ManifestFileName}' not found under '{versionDirectory}'. "
                + "Refusing to load an incomplete container.");
        }

        // ── Container-level integrity FIRST (a tampered container is not per-record rejection) ──
        var manifest = ParseContainerObject(manifestPath, ManifestFileName);
        RequireContainerSchema(manifest, ManifestSchema, ManifestFileName);

        var recordsBytes = File.ReadAllBytes(recordsPath);
        var actualRecordsHash = Convert.ToHexStringLower(SHA256.HashData(recordsBytes));
        var declaredRecordsHash = ReadRequiredString(manifest, "recordsSha256", ManifestFileName);
        if (!string.Equals(declaredRecordsHash, actualRecordsHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"recordsSha256 mismatch: manifest.json declares {declaredRecordsHash} but {RecordsFileName} hashes to "
                + $"{actualRecordsHash}. The frozen container was tampered — refusing the whole load "
                + "(a tampered container is not per-record rejection).");
        }

        var recordsJson = Utf8NoBom.GetString(recordsBytes);
        if (ParseJsonObject(recordsJson, recordsPath) is not JsonObject recordsRoot)
        {
            throw new InvalidOperationException(
                $"{RecordsFileName} is not a JSON object — corrupt frozen container; refusing the whole load.");
        }

        RequireContainerSchema(recordsRoot, RecordsSchema, RecordsFileName);

        if (recordsRoot["records"] is not JsonArray recordsArray)
        {
            throw new InvalidOperationException(
                $"{RecordsFileName} is missing the 'records' array — corrupt frozen container; refusing the whole load.");
        }

        var declaredRecordCount = ReadRequiredInt(manifest, "recordCount", ManifestFileName);
        if (declaredRecordCount != recordsArray.Count)
        {
            throw new InvalidOperationException(
                $"manifest.json declares recordCount {declaredRecordCount} but {RecordsFileName} contains "
                + $"{recordsArray.Count} records — tampered container; refusing the whole load.");
        }

        // ── Per-record revalidation gate ──
        var fixture = new ScenarioKnowledgeFixture(expectedScope);
        var rejections = new List<LoadRejection>();
        var recordsLoaded = 0;

        for (var index = 0; index < recordsArray.Count; index++)
        {
            if (recordsArray[index] is not JsonObject recordNode)
            {
                rejections.Add(new LoadRejection(
                    $"<record[{index}]>",
                    $"malformed record: records[{index}] is not a JSON object."));
                continue;
            }

            if (!TryParseRecord(recordNode, out var parsedRecord, out var storedRecordId, out var parseReason))
            {
                rejections.Add(new LoadRejection(storedRecordId ?? $"<record[{index}]>", parseReason ?? "malformed record."));
                continue;
            }

            // TryParseRecord returns true only with a non-null record.
            var record = parsedRecord!;

            // Identity gate: recompute the SHA-256 RecordId from the canonical
            // content and compare — an altered record fails here (tampering),
            // and is NEVER loaded silently.
            if (!string.Equals(record.RecordId, storedRecordId, StringComparison.Ordinal))
            {
                rejections.Add(new LoadRejection(
                    storedRecordId!,
                    $"RecordId mismatch — recomputed SHA-256 {record.RecordId} != declared {storedRecordId}; "
                    + "the record's content was altered after freezing. Refusing to load."));
                continue;
            }

            // Scope gate: the no-cross-scope-leak guarantee.
            if (!record.Scope.Matches(expectedScope))
            {
                rejections.Add(new LoadRejection(
                    record.RecordId,
                    $"scope mismatch: {FirstScopeMismatchField(record.Scope, expectedScope)}"));
                continue;
            }

            // Admission gate re-run (revalidation): provenance, vocabularies,
            // confidence range, scope completeness, duplicates. A hand-edited
            // record that kept identity consistent but violates the gate is
            // rejected here.
            var admission = fixture.Admit(record);
            if (admission is KnowledgeAdmission.Rejected rejected)
            {
                rejections.Add(new LoadRejection(record.RecordId, $"admission rejected: {rejected.Reason}"));
                continue;
            }

            recordsLoaded++;
        }

        return new LoadedFixture(fixture, recordsLoaded, rejections);
    }

    /// <summary>
    /// Resolve the version directory for (root, scenarioId, version):
    /// <c>&lt;root&gt;/&lt;scenarioId&gt;/v&lt;version&gt;</c> — the layout
    /// contract of the frozen asset set.
    /// </summary>
    public static string ResolveVersionDirectory(string root, string scenarioId, int version)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("root is required.", nameof(root));
        }

        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("scenarioId is required.", nameof(scenarioId));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Fixture version must be >= 1.");
        }

        return Path.Combine(root, scenarioId, $"v{version}");
    }

    // ── Freeze writers ────────────────────────────────────────────────────────

    private static string BuildRecordsJson(IReadOnlyList<ScenarioKnowledgeRecord> records)
        => new JsonObject
        {
            ["schema"] = RecordsSchema,
            ["records"] = new JsonArray(records.Select(RecordNode).ToArray()),
        }.ToJsonString(JsonOptions);

    /// <summary>Explicit JsonNode in the canonical property order — stable
    /// serialization is the diffability contract.</summary>
    private static JsonObject RecordNode(ScenarioKnowledgeRecord record) => new()
    {
        ["RecordId"] = record.RecordId,
        ["KnowledgeType"] = record.KnowledgeType.ToString(),
        ["SemanticAnchor"] = record.SemanticAnchor,
        ["SourceRunId"] = record.SourceRunId,
        ["EvidenceRefs"] = EvidenceRefsNode(record.EvidenceRefs),
        ["ObservedRole"] = record.ObservedRole,
        ["Scope"] = ScopeNode(record.Scope),
        ["Disposition"] = record.Disposition,
        // Invariant "R" round-trip formatting; parsed as a number node so the
        // emitted raw text is exactly the round-trip string (byte-deterministic).
        ["Confidence"] = JsonNode.Parse(record.Confidence.ToString("R", CultureInfo.InvariantCulture)),
        ["ValidityAssumption"] = record.ValidityAssumption,
        ["Version"] = record.Version,
        ["Status"] = record.Status.ToString(),
        ["Supersedes"] = record.Supersedes is null ? null : JsonValue.Create(record.Supersedes),
        ["SupersededBy"] = record.SupersededBy is null ? null : JsonValue.Create(record.SupersededBy),
        ["AdmissionOrdinal"] = record.AdmissionOrdinal,
    };

    /// <summary>EvidenceRefs keep their ORIGINAL order (provenance order is
    /// meaningful; identity hashing already normalizes it).</summary>
    private static JsonArray EvidenceRefsNode(IReadOnlyList<string> evidenceRefs)
        => new(evidenceRefs.Select(refId => (JsonNode?)JsonValue.Create(refId)).ToArray());

    /// <summary>Scope serializes with CreatedFromRunIds SORTED — set semantics,
    /// order-independent exactly like <see cref="KnowledgeScope"/> equality, so
    /// two fixtures differing only in run-set order freeze byte-identically.</summary>
    private static JsonObject ScopeNode(KnowledgeScope scope) => new()
    {
        ["ScenarioId"] = scope.ScenarioId,
        ["ApplicationPackage"] = scope.ApplicationPackage,
        ["SemanticCapabilityId"] = scope.SemanticCapabilityId,
        ["SemanticCapabilityVersion"] = scope.SemanticCapabilityVersion,
        ["AndroidAssumptions"] = scope.AndroidAssumptions,
        ["Locale"] = scope.Locale,
        ["CreatedFromRunIds"] = new JsonArray(
            scope.CreatedFromRunIds.Order(StringComparer.Ordinal).Select(runId => (JsonNode?)JsonValue.Create(runId)).ToArray()),
    };

    private static string BuildManifestJson(
        string scenarioId,
        int version,
        int? supersedesVersion,
        int recordCount,
        int activeCount,
        string recordsSha256,
        KnowledgeScope scope)
        => new JsonObject
        {
            ["schema"] = ManifestSchema,
            ["scenarioId"] = scenarioId,
            ["version"] = version,
            ["supersedesVersion"] = supersedesVersion is null ? null : JsonValue.Create(supersedesVersion.Value),
            ["recordCount"] = recordCount,
            ["activeCount"] = activeCount,
            ["recordsSha256"] = recordsSha256,
            ["scope"] = new JsonObject
            {
                ["ScenarioId"] = scope.ScenarioId,
                ["ApplicationPackage"] = scope.ApplicationPackage,
                ["SemanticCapabilityId"] = scope.SemanticCapabilityId,
                ["SemanticCapabilityVersion"] = scope.SemanticCapabilityVersion,
                ["AndroidAssumptions"] = scope.AndroidAssumptions,
                ["Locale"] = scope.Locale,
            },
        }.ToJsonString(JsonOptions);

    private static string BuildMarkdown(
        string scenarioId,
        int version,
        int? supersedesVersion,
        KnowledgeScope scope,
        IReadOnlyList<ScenarioKnowledgeRecord> records)
    {
        var lines = new List<string>
        {
            $"# ScenarioKnowledgeFixture — {scenarioId}",
            string.Empty,
            $"- Scenario: {scenarioId}",
            $"- Version: v{version}",
            $"- Supersedes: {(supersedesVersion is null ? "none" : $"v{supersedesVersion}")}",
            $"- Record count: {records.Count} (active: {records.Count(r => r.Status == KnowledgeStatus.Active)})",
            string.Empty,
            "## Scope",
            string.Empty,
            $"- ScenarioId: {scope.ScenarioId}",
            $"- ApplicationPackage: {scope.ApplicationPackage}",
            $"- SemanticCapabilityId: {scope.SemanticCapabilityId}",
            $"- SemanticCapabilityVersion: {scope.SemanticCapabilityVersion}",
            $"- AndroidAssumptions: {scope.AndroidAssumptions}",
            $"- Locale: {scope.Locale}",
            string.Empty,
            "## Records (sorted by RecordId)",
            string.Empty,
        };

        foreach (var record in records)
        {
            lines.Add(
                $"- [{record.Status}] {record.KnowledgeType} {SingleLine(record.SemanticAnchor)} "
                + $"(run={record.SourceRunId}, conf={record.Confidence.ToString("R", CultureInfo.InvariantCulture)}) "
                + $"— {SingleLine(record.Disposition)}");
        }

        lines.Add(string.Empty);
        lines.Add("## Lifecycle statistics");
        lines.Add(string.Empty);
        lines.Add("| KnowledgeType | Status | Count |");
        lines.Add("|---|---|---|");
        foreach (var group in records
                     .GroupBy(r => (r.KnowledgeType, r.Status))
                     .OrderBy(g => g.Key.KnowledgeType.ToString(), StringComparer.Ordinal)
                     .ThenBy(g => g.Key.Status.ToString(), StringComparer.Ordinal))
        {
            lines.Add($"| {group.Key.KnowledgeType} | {group.Key.Status} | {group.Count()} |");
        }

        return string.Join("\n", lines) + "\n";
    }

    private static string SingleLine(string? value)
        => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');

    // ── Load parsers ──────────────────────────────────────────────────────────

    private static JsonObject ParseContainerObject(string path, string fileName)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"{fileName} is not valid JSON ({ex.Message}) — corrupt frozen container; refusing the whole load.", ex);
        }

        if (node is not JsonObject obj)
        {
            throw new InvalidOperationException(
                $"{fileName} is not a JSON object — corrupt frozen container; refusing the whole load.");
        }

        return obj;
    }

    private static JsonNode? ParseJsonObject(string json, string path)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(path)} is not valid JSON ({ex.Message}) — corrupt frozen container; refusing the whole load.", ex);
        }
    }

    private static void RequireContainerSchema(JsonObject container, string expectedSchema, string fileName)
    {
        var schema = ReadOptionalString(container, "schema");
        if (!string.Equals(schema, expectedSchema, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{fileName} schema is '{(schema ?? "(missing)")}' — expected '{expectedSchema}'. Different container "
                + "format — refusing the whole load (not per-record rejection).");
        }
    }

    private static string ReadRequiredString(JsonObject obj, string name, string containerName)
        => ReadOptionalString(obj, name)
           ?? throw new InvalidOperationException(
               $"{containerName} is missing required field '{name}' — tampered container; refusing the whole load.");

    private static int ReadRequiredInt(JsonObject obj, string name, string containerName)
        => ReadOptionalInt(obj, name)
           ?? throw new InvalidOperationException(
               $"{containerName} is missing required field '{name}' — tampered container; refusing the whole load.");

    private static string? ReadOptionalString(JsonObject obj, string name)
        => obj.TryGetPropertyValue(name, out var node) && node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static int? ReadOptionalInt(JsonObject obj, string name)
        => obj.TryGetPropertyValue(name, out var node) && node is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

    /// <summary>Strict per-record parse, canonical field order. Returns false
    /// with a deterministic <paramref name="reason"/> on the FIRST missing or
    /// malformed REQUIRED field (lifecycle links are nullable/optional).</summary>
    private static bool TryParseRecord(JsonObject obj, out ScenarioKnowledgeRecord? record, out string? storedRecordId, out string? reason)
    {
        record = null;
        storedRecordId = null;
        reason = null;

        if (!TryGetStringRequired(obj, "RecordId", out storedRecordId, out reason))
        {
            return false;
        }

        if (!TryGetEnumRequired<KnowledgeType>(obj, "KnowledgeType", out var type, out reason))
        {
            return false;
        }

        if (!TryGetStringRequired(obj, "SemanticAnchor", out var semanticAnchor, out reason))
        {
            return false;
        }

        if (!TryGetStringRequired(obj, "SourceRunId", out var sourceRunId, out reason))
        {
            return false;
        }

        if (!TryGetStringArrayRequired(obj, "EvidenceRefs", out var evidenceRefs, out reason))
        {
            return false;
        }

        if (!TryGetStringRequired(obj, "ObservedRole", out var observedRole, out reason))
        {
            return false;
        }

        if (!TryParseScope(obj, out var scope, out reason))
        {
            return false;
        }

        if (!TryGetStringRequired(obj, "Disposition", out var disposition, out reason))
        {
            return false;
        }

        if (!TryGetDoubleRequired(obj, "Confidence", out var confidence, out reason))
        {
            return false;
        }

        if (!TryGetStringRequired(obj, "ValidityAssumption", out var validityAssumption, out reason))
        {
            return false;
        }

        if (!TryGetIntRequired(obj, "Version", out var version, out reason))
        {
            return false;
        }

        if (!TryGetEnumRequired<KnowledgeStatus>(obj, "Status", out var status, out reason))
        {
            return false;
        }

        if (!TryGetIntRequired(obj, "AdmissionOrdinal", out var admissionOrdinal, out reason))
        {
            return false;
        }

        // Lifecycle links are nullable by contract; absent entry = null.
        TryGetOptionalString(obj, "Supersedes", out var supersedes);
        TryGetOptionalString(obj, "SupersededBy", out var supersededBy);

        record = new ScenarioKnowledgeRecord(
            type,
            semanticAnchor!,
            sourceRunId!,
            evidenceRefs!,
            observedRole!,
            scope!,
            disposition!,
            confidence,
            validityAssumption!,
            version,
            status,
            admissionOrdinal,
            supersedes,
            supersededBy);
        return true;
    }

    private static bool TryParseScope(JsonObject obj, out KnowledgeScope? scope, out string? reason)
    {
        scope = null;
        reason = null;

        if (!obj.TryGetPropertyValue("Scope", out var node) || node is null)
        {
            reason = "missing required field: Scope";
            return false;
        }

        if (node is not JsonObject scopeObj)
        {
            reason = "invalid Scope: expected an object";
            return false;
        }

        if (!TryGetStringRequired(scopeObj, "ScenarioId", out var scenarioId, out reason))
        {
            return false;
        }

        if (!TryGetStringRequired(scopeObj, "ApplicationPackage", out var applicationPackage, out reason))
        {
            return false;
        }

        if (!TryGetStringRequired(scopeObj, "SemanticCapabilityId", out var semanticCapabilityId, out reason))
        {
            return false;
        }

        if (!TryGetStringRequired(scopeObj, "SemanticCapabilityVersion", out var semanticCapabilityVersion, out reason))
        {
            return false;
        }

        if (!TryGetStringRequired(scopeObj, "AndroidAssumptions", out var androidAssumptions, out reason))
        {
            return false;
        }

        if (!TryGetStringRequired(scopeObj, "Locale", out var locale, out reason))
        {
            return false;
        }

        if (!TryGetStringArrayRequired(scopeObj, "CreatedFromRunIds", out var createdFromRunIds, out reason))
        {
            return false;
        }

        reason = null;
        scope = new KnowledgeScope(
            scenarioId!,
            applicationPackage!,
            semanticCapabilityId!,
            semanticCapabilityVersion!,
            androidAssumptions!,
            locale!,
            createdFromRunIds!);
        return true;
    }

    private static bool TryGetStringRequired(JsonObject obj, string name, out string? value, out string? reason)
        => TryGetString(obj, name, out value, out reason, required: true);

    private static bool TryGetOptionalString(JsonObject obj, string name, out string? value)
    {
        if (TryGetString(obj, name, out value, out _, required: false))
        {
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetString(JsonObject obj, string name, out string? value, out string? reason, bool required)
    {
        if (!obj.TryGetPropertyValue(name, out var node) || node is null)
        {
            value = null;
            reason = required ? $"missing required field: {name}" : null;
            return !required;
        }

        if (node is JsonValue v && v.TryGetValue<string>(out var s))
        {
            value = s;
            reason = null;
            return true;
        }

        value = null;
        reason = required ? $"invalid {name}: expected a string" : null;
        return !required;
    }

    private static bool TryGetStringArrayRequired(JsonObject obj, string name, out IReadOnlyList<string>? value, out string? reason)
    {
        if (!obj.TryGetPropertyValue(name, out var node) || node is null)
        {
            value = null;
            reason = $"missing required field: {name}";
            return false;
        }

        if (node is not JsonArray array)
        {
            value = null;
            reason = $"invalid {name}: expected an array of strings";
            return false;
        }

        var items = new List<string>(array.Count);
        foreach (var item in array)
        {
            if (item is not JsonValue v || !v.TryGetValue<string>(out var s))
            {
                value = null;
                reason = $"invalid {name}: expected an array of strings";
                return false;
            }

            items.Add(s);
        }

        value = items;
        reason = null;
        return true;
    }

    private static bool TryGetEnumRequired<TEnum>(JsonObject obj, string name, out TEnum value, out string? reason)
        where TEnum : struct, Enum
    {
        if (!obj.TryGetPropertyValue(name, out var node) || node is null)
        {
            value = default;
            reason = $"missing required field: {name}";
            return false;
        }

        if (node is JsonValue v
            && v.TryGetValue<string>(out var s)
            && Enum.TryParse(s, ignoreCase: false, out TEnum parsed)
            && Enum.IsDefined(typeof(TEnum), parsed))
        {
            value = parsed;
            reason = null;
            return true;
        }

        value = default;
        reason = $"invalid {name}: not a registered {typeof(TEnum).Name} value";
        return false;
    }

    private static bool TryGetDoubleRequired(JsonObject obj, string name, out double value, out string? reason)
    {
        if (!obj.TryGetPropertyValue(name, out var node) || node is null)
        {
            value = default;
            reason = $"missing required field: {name}";
            return false;
        }

        if (node is JsonValue v && v.TryGetValue<double>(out var d))
        {
            value = d;
            reason = null;
            return true;
        }

        value = default;
        reason = $"invalid {name}: expected a number";
        return false;
    }

    private static bool TryGetIntRequired(JsonObject obj, string name, out int value, out string? reason)
    {
        if (!obj.TryGetPropertyValue(name, out var node) || node is null)
        {
            value = default;
            reason = $"missing required field: {name}";
            return false;
        }

        if (node is JsonValue v && v.TryGetValue<int>(out var i))
        {
            value = i;
            reason = null;
            return true;
        }

        value = default;
        reason = $"invalid {name}: expected an integer";
        return false;
    }

    /// <summary>Name of the first context field that differs between the
    /// record scope and the expected scope (matches <see cref="KnowledgeScope.Matches"/>
    /// field order); only called when Matches() returned false, so a context
    /// field is always found.</summary>
    private static string FirstScopeMismatchField(KnowledgeScope recordScope, KnowledgeScope expectedScope)
    {
        if (!string.Equals(recordScope.ScenarioId, expectedScope.ScenarioId, StringComparison.Ordinal))
        {
            return nameof(KnowledgeScope.ScenarioId);
        }

        if (!string.Equals(recordScope.ApplicationPackage, expectedScope.ApplicationPackage, StringComparison.Ordinal))
        {
            return nameof(KnowledgeScope.ApplicationPackage);
        }

        if (!string.Equals(recordScope.SemanticCapabilityId, expectedScope.SemanticCapabilityId, StringComparison.Ordinal))
        {
            return nameof(KnowledgeScope.SemanticCapabilityId);
        }

        if (!string.Equals(recordScope.SemanticCapabilityVersion, expectedScope.SemanticCapabilityVersion, StringComparison.Ordinal))
        {
            return nameof(KnowledgeScope.SemanticCapabilityVersion);
        }

        if (!string.Equals(recordScope.AndroidAssumptions, expectedScope.AndroidAssumptions, StringComparison.Ordinal))
        {
            return nameof(KnowledgeScope.AndroidAssumptions);
        }

        if (!string.Equals(recordScope.Locale, expectedScope.Locale, StringComparison.Ordinal))
        {
            return nameof(KnowledgeScope.Locale);
        }

        // Unreachable when Matches() is false (all six context fields equal);
        // defensive fallback keeps the reason deterministic.
        return nameof(KnowledgeScope.CreatedFromRunIds);
    }
}