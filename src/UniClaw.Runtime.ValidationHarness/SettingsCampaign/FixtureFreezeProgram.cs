// Phase 2.6 — freeze a real campaign's knowledge records into a versioned
// ScenarioKnowledgeFixture asset (validation-side; spec "Knowledge persistence
// and cross-campaign reuse" / design D2). The tool reads the ARCHIVED campaign
// outcome JSON (the evidence artifact), reconstructs each knowledge record with
// the same field constants the RoundKnowledgeExtractor set when it produced
// them (ValidityAssumption/Version/AdmissionOrdinal — not serialized in the
// campaign report; constants mirrored from the extractor source), re-validates
// every record through the REAL KnowledgeAdmission gate (ObservedResult source
// only; provenance failures are reported, never forced), and freezes via the
// graduated ScenarioKnowledgeStore. No Runtime involvement of any kind.
//
// Usage: dotnet run --project src/UniClaw.Runtime.ValidationHarness -- fixturefreeze
//            <campaignJsonPath> <outputRoot> <version> [--supersedes N]
using System.Text.Json;
using UniClaw.Runtime.ValidationHarness.Knowledge;

namespace UniClaw.Runtime.ValidationHarness.SettingsCampaign;

public static class FixtureFreezeProgram
{
    // Mirrors RoundKnowledgeExtractor's record constants (the campaign report
    // serializes content fields; these lifecycle/metadata fields are constant
    // per extractor rule and re-asserted here from the extractor source).
    private const string ValidityAssumption = "stable across frames";
    private const int RecordVersion = 1;

    public static int RunAsync(string[] args)
    {
        if (args.Length < 3
            || !int.TryParse(args[2], out var version)
            || version < 1)
        {
            Console.Error.WriteLine("usage: fixturefreeze <campaignJson> <outputRoot> <version> [--supersedes N]");
            return 2;
        }

        int? supersedes = null;
        for (var i = 3; i < args.Length - 1; i++)
        {
            if (args[i] == "--supersedes" && int.TryParse(args[i + 1], out var s))
                supersedes = s;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(args[0]));
        var root = document.RootElement;

        var runIds = root.GetProperty("rounds")
            .EnumerateArray()
            .Select(r => r.GetProperty("runId").GetString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToArray();

        var scope = new KnowledgeScope(
            ScenarioId: "settings-bounded-traversal",
            ApplicationPackage: SettingsBinding.SettingsStrategyBinding.ApplicationIdentity,
            SemanticCapabilityId: "uni-claw.settings.semantic",
            SemanticCapabilityVersion: "1",
            AndroidAssumptions: "android-35/p26_pixel/arm64-v8a/emulator",
            Locale: "en-US",
            CreatedFromRunIds: runIds);

        var fixture = new ScenarioKnowledgeFixture(scope);
        var admitted = 0;
        var rejected = 0;
        var ordinal = 0;
        foreach (var record in root.GetProperty("adaptation").GetProperty("knowledgeRecords").EnumerateArray())
        {
            ordinal++;
            var candidate = new ScenarioKnowledgeRecord(
                KnowledgeType: Enum.Parse<KnowledgeType>(record.GetProperty("type").GetString()!, true),
                SemanticAnchor: record.GetProperty("anchor").GetString()!,
                SourceRunId: record.GetProperty("sourceRunId").GetString()!,
                EvidenceRefs: record.GetProperty("evidenceRefs").EnumerateArray()
                    .Select(e => e.GetString()!).ToArray(),
                ObservedRole: record.GetProperty("observedRole").GetString()!,
                Scope: scope,
                Disposition: record.GetProperty("disposition").GetString()!,
                Confidence: record.GetProperty("confidence").GetDouble(),
                ValidityAssumption: ValidityAssumption,
                Version: RecordVersion,
                Status: Enum.Parse<KnowledgeStatus>(record.GetProperty("status").GetString()!, true),
                Supersedes: null,
                SupersededBy: null,
                AdmissionOrdinal: ordinal);

            var admission = fixture.Admit(candidate, KnowledgeAdmissionSource.ObservedResult);
            switch (admission)
            {
                case KnowledgeAdmission.Admitted:
                    admitted++;
                    break;
                case KnowledgeAdmission.Rejected r:
                    rejected++;
                    Console.Error.WriteLine($"[fixturefreeze] REJECTED {candidate.SemanticAnchor}: {r.Reason}");
                    break;
            }
        }

        var frozen = ScenarioKnowledgeStore.Freeze(
            fixture,
            scenarioId: scope.ScenarioId,
            version: version,
            rootDirectory: args[1],
            supersedesVersion: supersedes);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            frozen.Directory,
            frozen.RecordsPath,
            frozen.ManifestPath,
            frozen.MarkdownPath,
            frozen.RecordCount,
            frozen.ContentSha256,
            admitted,
            rejected,
            scope.ScenarioId,
            createdFromRuns = runIds,
        }, new JsonSerializerOptions { WriteIndented = true }));
        return rejected == 0 && admitted > 0 ? 0 : 1;
    }
}
