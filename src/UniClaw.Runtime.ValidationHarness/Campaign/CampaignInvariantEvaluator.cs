using System.Collections.Immutable;
using System.Text.Json.Nodes;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Reporting;
using UniClaw.Runtime.ValidationHarness.Results;
using UniClaw.Runtime.ValidationHarness.Scenarios;

namespace UniClaw.Runtime.ValidationHarness.Campaign;

/// <summary>
/// The four-frozen-invariant per-run evaluator (Phase 2.6, spec "Four frozen
/// invariants"). Each invariant is asserted as an explicit boolean outcome with
/// evidence references DRAWN FROM THIS ROUND's own result and call log — never
/// copied from a prior round's conclusion. The assertions are structural and
/// evidence-shaped: they check what the round actually shows (call-log slice,
/// result field classifications / truth sources, rendered report shape) and
/// never fabricate a verdict the report does not carry (e.g. no scenario
/// verdict is invented where the single-run report keeps scenario acceptance
/// external).
/// </summary>
public static class CampaignInvariantEvaluator
{
    /// <summary>Rendered-report property names that would merge a scenario
    /// acceptance verdict into the report (I3): the single-run report keeps
    /// runtime terminal and scenario acceptance as distinct verdicts, so any
    /// such key is a conflation — fail-closed.</summary>
    private static readonly string[] ForbiddenMergedScenarioVerdictKeys =
        ["scenarioVerdict", "scenarioPass", "scenarioAcceptance"];

    /// <summary>Rendered-report property names that would assert a universal
    /// recovery verdict (I4): the harness never claims recovery, so any such
    /// key is a conflation — fail-closed.</summary>
    private static readonly string[] ForbiddenUniversalRecoveryKeys =
        ["universalRecovery", "recoveryVerdict", "autoRecovered"];

    /// <summary>
    /// Assert all four frozen invariants for ONE round. The four assertions are
    /// produced in fixed order I1..I4 over the round's own outputs; the round's
    /// slice, result classifications and rendered report provide every evidence
    /// reference. <paramref name="precedingRunCount"/> (the number of prior
    /// campaign rounds) feeds I2's "prior results are read-only planner inputs"
    /// evidence.
    /// </summary>
    public static ImmutableArray<InvariantAssertion> AssertAll(
        CampaignRoundDirective directive,
        ScenarioRunOutcome run,
        int roundIndex,
        int precedingRunCount)
    {
        ArgumentNullException.ThrowIfNull(directive);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentOutOfRangeException.ThrowIfNegative(roundIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(precedingRunCount);

        var reportPropertyNames = CollectJsonPropertyNames(run.ReportJson);

        return
        [
            AssertHistoricalKnowledgeNotCurrentWorldTruth(directive, run, roundIndex),
            AssertHistoricalResultNotRuntimeActionAuthority(run, roundIndex, precedingRunCount),
            AssertRuntimeCompletedNotValidationScenarioPass(run, roundIndex, reportPropertyNames),
            AssertAutonomousExceptionDispositionNotUniversalRecovery(run, roundIndex, reportPropertyNames),
        ];
    }

    // ── I1: HISTORICAL_KNOWLEDGE != CURRENT_WORLD_TRUTH ───────────────────────

    private static InvariantAssertion AssertHistoricalKnowledgeNotCurrentWorldTruth(
        CampaignRoundDirective directive,
        ScenarioRunOutcome run,
        int roundIndex)
    {
        var evidence = ImmutableArray.CreateBuilder<string>();
        evidence.Add(
            $"round {roundIndex}: knowledge artifact recorded as directive '{directive.StrategyId}' "
            + $"(goal '{Truncate(directive.Goal)}') — authored before start, kept OUTSIDE the Result");

        var fields = run.Result.EnumerateClassifiedFields().ToArray();
        var violationIndex = -1;
        for (var index = 0; index < fields.Length; index++)
        {
            var source = fields[index].TruthSource ?? string.Empty;
            if (source.Contains(directive.Goal, StringComparison.Ordinal))
            {
                violationIndex = index;
                break;
            }
        }

        evidence.Add(
            $"round {roundIndex}: result field walk over {fields.Length} classified field(s); "
            + "truth sources name runtime surfaces only (fresh evidence, never the authored goal)");

        if (violationIndex >= 0)
        {
            return new InvariantAssertion(
                Passed: false,
                CampaignInvariantIds.HistoricalKnowledgeNotCurrentWorldTruth,
                evidence.ToImmutable(),
                Reason: $"round {roundIndex}: result-field[{violationIndex}] truth source quotes the authored goal prose "
                        + $"('{Truncate(fields[violationIndex].TruthSource)}') — historical knowledge presented as current world truth.");
        }

        evidence.Add(
            $"round {roundIndex}: zero result fields cite the authored goal prose as truth — "
            + "knowledge stays historical, current-world-truth stays fresh runtime evidence");
        return new InvariantAssertion(
            Passed: true,
            CampaignInvariantIds.HistoricalKnowledgeNotCurrentWorldTruth,
            evidence.ToImmutable(),
            Reason: null);
    }

    // ── I2: HISTORICAL_RESULT != RUNTIME_ACTION_AUTHORITY ─────────────────────

    private static InvariantAssertion AssertHistoricalResultNotRuntimeActionAuthority(
        ScenarioRunOutcome run,
        int roundIndex,
        int precedingRunCount)
    {
        var evidence = ImmutableArray.CreateBuilder<string>();
        var slice = run.RunCallLog.Entries;
        var acceptedStarts = 0;
        var lastAccepted = -1;
        for (var index = 0; index < slice.Length; index++)
        {
            if (slice[index].Outcome == EmulatorCallOutcome.Accepted)
            {
                acceptedStarts++;
                lastAccepted = index;
            }
        }

        var entriesAfterAdmission = lastAccepted >= 0 ? slice.Length - 1 - lastAccepted : slice.Length;
        evidence.Add(
            $"round {roundIndex}: dispatch authority = THIS round's directive only "
            + $"(single accepted start {acceptedStarts}, driver/wire entries after admission {entriesAfterAdmission})");

        if (precedingRunCount > 0)
        {
            evidence.Add(
                $"round {roundIndex}: {precedingRunCount} prior round result(s) exist — read-only planner inputs; "
                + "none entered this round's dispatch path (call-log proof)");
        }

        var noInjection = run.Boundary[BoundaryProhibitionKind.NoActionInjection];
        evidence.Add(
            $"round {roundIndex}: boundary no-action-injection = {(noInjection.Positive ? "positive" : "VIOLATED")}");

        if (acceptedStarts == 1 && entriesAfterAdmission == 0 && noInjection.Positive)
        {
            evidence.Add(
                $"round {roundIndex}: historical results never authorized an action in this round — the fresh directive alone did");
            return new InvariantAssertion(
                Passed: true,
                CampaignInvariantIds.HistoricalResultNotRuntimeActionAuthority,
                evidence.ToImmutable(),
                Reason: null);
        }

        var problems = new List<string>();
        if (acceptedStarts != 1)
        {
            problems.Add($"accepted starts {acceptedStarts} ≠ 1");
        }

        if (entriesAfterAdmission != 0)
        {
            problems.Add($"{entriesAfterAdmission} driver/wire call(s) after admission");
        }

        if (!noInjection.Positive)
        {
            problems.Add("no-action-injection boundary violated");
        }

        return new InvariantAssertion(
            Passed: false,
            CampaignInvariantIds.HistoricalResultNotRuntimeActionAuthority,
            evidence.ToImmutable(),
            Reason: $"round {roundIndex}: {string.Join("; ", problems)} — dispatch authority cannot be attributed to the fresh directive alone.");
    }

    // ── I3: RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS ─────────────────────

    private static InvariantAssertion AssertRuntimeCompletedNotValidationScenarioPass(
        ScenarioRunOutcome run,
        int roundIndex,
        ImmutableArray<string> reportPropertyNames)
    {
        var evidence = ImmutableArray.CreateBuilder<string>();
        evidence.Add(
            $"round {roundIndex}: single-run report type members: Result, Gates, Boundary — no scenario-verdict member "
            + "(runtime terminal verdict lives in Result.Terminal.TerminalState; gates G1–G4 are separate fields)");

        var terminalPresent = HasJsonSection(run.ReportJson, "terminal");
        if (terminalPresent)
        {
            evidence.Add(
                $"round {roundIndex}: rendered report keeps the runtime terminal verdict under its own 'terminal' section");
        }

        var gatesPresent = HasJsonSection(run.ReportJson, "gates");
        if (gatesPresent)
        {
            evidence.Add(
                $"round {roundIndex}: scenario acceptance (G1–G4 gates) rendered as separate 'gates' fields — not merged into the runtime terminal");
        }

        var mergedVerdictKey = reportPropertyNames.FirstOrDefault(
            name => ForbiddenMergedScenarioVerdictKeys.Contains(name, StringComparer.Ordinal));

        if (!terminalPresent)
        {
            return new InvariantAssertion(
                Passed: false,
                CampaignInvariantIds.RuntimeCompletedNotValidationScenarioPass,
                evidence.ToImmutable(),
                Reason: $"round {roundIndex}: the rendered report carries no 'terminal' section — the runtime terminal verdict is not distinctly recorded.");
        }

        if (mergedVerdictKey is not null)
        {
            return new InvariantAssertion(
                Passed: false,
                CampaignInvariantIds.RuntimeCompletedNotValidationScenarioPass,
                evidence.ToImmutable(),
                Reason: $"round {roundIndex}: rendered report carries the merged scenario-verdict key '{mergedVerdictKey}' — runtime completion and scenario acceptance have been conflated.");
        }

        evidence.Add(
            $"round {roundIndex}: RUNTIME_COMPLETED stays the runtime's own verdict; VALIDATION_SCENARIO_PASS stays external / not-merged — distinction holds");
        return new InvariantAssertion(
            Passed: true,
            CampaignInvariantIds.RuntimeCompletedNotValidationScenarioPass,
            evidence.ToImmutable(),
            Reason: null);
    }

    // ── I4: AUTONOMOUS_EXCEPTION_DISPOSITION != UNIVERSAL_RECOVERY ────────────

    private static InvariantAssertion AssertAutonomousExceptionDispositionNotUniversalRecovery(
        ScenarioRunOutcome run,
        int roundIndex,
        ImmutableArray<string> reportPropertyNames)
    {
        var evidence = ImmutableArray.CreateBuilder<string>();
        var trapFound = run.Result.Trap.Found;
        var terminalState = run.Result.Terminal.TerminalState;
        var recoveryState = run.Result.Snapshot.RecoveryState;

        evidence.Add(
            $"round {roundIndex}: exception disposition recorded as the runtime's own classified facts — "
            + $"trap.found {Describe(trapFound)}, terminal.terminalState {Describe(terminalState)}");
        evidence.Add(
            $"round {roundIndex}: snapshot.recoveryState truth source: '{Truncate(recoveryState.TruthSource)}' "
            + "(runtime-owned recovery state copied verbatim — never a harness claim)");

        var recoveryVerdictKey = reportPropertyNames.FirstOrDefault(
            name => ForbiddenUniversalRecoveryKeys.Contains(name, StringComparer.Ordinal));
        if (recoveryVerdictKey is not null)
        {
            return new InvariantAssertion(
                Passed: false,
                CampaignInvariantIds.AutonomousExceptionDispositionNotUniversalRecovery,
                evidence.ToImmutable(),
                Reason: $"round {roundIndex}: rendered report asserts a universal-recovery verdict key '{recoveryVerdictKey}' — an exception disposition was conflated with recovery.");
        }

        evidence.Add(
            $"round {roundIndex}: the harness recorded the runtime's disposition verbatim and made NO recovery claim — "
            + "UNIVERSAL_RECOVERY is never asserted");
        return new InvariantAssertion(
            Passed: true,
            CampaignInvariantIds.AutonomousExceptionDispositionNotUniversalRecovery,
            evidence.ToImmutable(),
            Reason: null);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>Compact evidence text for one classified field.</summary>
    private static string Describe(IClassifiedField field)
    {
        var value = field.Classification == ResultFieldClassification.Unavailable
            ? "unavailable"
            : field.RawValue?.ToString() ?? "null";
        return $"[{field.Classification}] {value}";
    }

    /// <summary>Every JSON property name in the rendered report (recursive walk).
    /// Returns empty when the JSON is not parseable — the caller treats that as
    /// fail-closed ("cannot be attested").</summary>
    private static ImmutableArray<string> CollectJsonPropertyNames(string reportJson)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(reportJson);
        }
        catch (Exception)
        {
            return ImmutableArray<string>.Empty;
        }

        if (root is null)
        {
            return ImmutableArray<string>.Empty;
        }

        var names = ImmutableArray.CreateBuilder<string>();
        Walk(root, names);
        return names.ToImmutable();

        static void Walk(JsonNode? node, ImmutableArray<string>.Builder builder)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var (name, child) in obj)
                    {
                        builder.Add(name);
                        Walk(child, builder);
                    }

                    break;
                case JsonArray array:
                    foreach (var child in array)
                    {
                        Walk(child, builder);
                    }

                    break;
            }
        }
    }

    /// <summary>Whether the rendered report carries a top-level report section
    /// with the given name (e.g. 'terminal', 'gates'). False on unparseable
    /// JSON — fail-closed.</summary>
    private static bool HasJsonSection(string reportJson, string sectionName)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(reportJson);
        }
        catch (Exception)
        {
            return false;
        }

        if (root?["validationReport"] is not JsonObject report)
        {
            return false;
        }

        if (report[sectionName] is not null)
        {
            return true;
        }

        // gates live at the report root; the eight result sections live under
        // "sections".
        return report["sections"]?[sectionName] is not null;
    }

    /// <summary>Compact evidence text (goal prose / truth sources can be long).</summary>
    private static string Truncate(string text, int maxLength = 64)
        => text.Length <= maxLength ? text : text[..maxLength] + "…";
}