using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Runtime.ValidationHarness.Results;

namespace UniClaw.Runtime.ValidationHarness.Reporting;

/// <summary>
/// The rendered unit (task 6.1): the aggregated <see cref="Result"/> plus the
/// four <see cref="Gates"/> as explicit report fields (design D7) plus the
/// derived <see cref="Boundary"/> proof (design D5). The renderers
/// (<see cref="ValidationReportRenderer"/>) are PURE functions over this —
/// no new collection, no Runtime surface, no invention: unavailable facts
/// render as <c>unavailable</c> with their reason, partial facts as
/// <c>partial</c>, and wire-tier ledger coverage renders unavailable exactly
/// as the collector recorded it.
/// </summary>
public sealed record ValidationReport(
    ValidationResult Result,
    ValidationGates Gates,
    BoundaryVerification Boundary);

/// <summary>
/// Pure renderers (task 6.1): render one <see cref="ValidationReport"/> to
/// JSON and to Markdown with all eight sections (Admission / Lifecycle /
/// Snapshot / Trap / Evidence / Coverage / Terminal / Boundary) plus the G1–G4
/// gate fields. Every classified field renders value + classification +
/// truth-source; Unavailable renders as <c>unavailable</c> with its reason;
/// <see cref="IClassifiedField.IsPartial"/> renders as <c>partial</c>.
/// Rendering is read-only over already-collected facts — it can never change
/// the Result, the Gates, or the Boundary.
/// </summary>
public static class ValidationReportRenderer
{
    private const int MaxSerializationDepth = 16;

    // ── JSON ─────────────────────────────────────────────────────────────────

    /// <summary>Render the report as a JSON object (deterministic key order).</summary>
    public static JsonObject ToJson(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sections = new JsonObject
        {
            ["admission"] = RenderAdmissionSection(report.Result.Admission),
            ["lifecycle"] = RenderLifecycleSection(report.Result.Lifecycle),
            ["snapshot"] = RenderSnapshotSection(report.Result.Snapshot),
            ["trap"] = RenderTrapSection(report.Result.Trap),
            ["evidence"] = RenderEvidenceSection(report.Result.Evidence),
            ["coverage"] = RenderCoverageSection(report.Result.Coverage),
            ["terminal"] = RenderTerminalSection(report.Result.Terminal),
            ["boundary"] = RenderBoundarySection(report.Boundary),
        };

        return new JsonObject
        {
            ["validationReport"] = new JsonObject
            {
                ["sections"] = sections,
                ["gates"] = RenderGates(report.Gates),
            },
        };
    }

    private static JsonObject RenderAdmissionSection(AdmissionSection section)
        => new()
        {
            ["runId"] = RenderField(section.RunId),
            ["strategyId"] = RenderField(section.StrategyId),
            ["accepted"] = RenderField(section.Accepted),
            ["rejectionCode"] = RenderField(section.RejectionCode),
            ["rejectionReason"] = RenderField(section.RejectionReason),
            ["declaredMaximumDepth"] = RenderField(section.DeclaredMaximumDepth),
        };

    private static JsonObject RenderLifecycleSection(LifecycleSection section)
        => new() { ["events"] = RenderField(section.Events) };

    private static JsonObject RenderSnapshotSection(SnapshotSection section)
        => new()
        {
            ["runId"] = RenderField(section.RunId),
            ["runState"] = RenderField(section.RunState),
            ["currentSemanticPage"] = RenderField(section.CurrentSemanticPage),
            ["activeTrap"] = RenderField(section.ActiveTrap),
            ["currentGoal"] = RenderField(section.CurrentGoal),
            ["lastDecision"] = RenderField(section.LastDecision),
            ["lastAction"] = RenderField(section.LastAction),
            ["recoveryState"] = RenderField(section.RecoveryState),
            ["latestGoalEvidence"] = RenderField(section.LatestGoalEvidence),
            ["currentObservationSequence"] = RenderField(section.CurrentObservationSequence),
            ["currentContainerSummary"] = RenderField(section.CurrentContainerSummary),
            ["bindingsSummary"] = RenderField(section.BindingsSummary),
            ["stateBeliefsSummary"] = RenderField(section.StateBeliefsSummary),
            ["diagnostics"] = RenderField(section.Diagnostics),
        };

    private static JsonObject RenderTrapSection(TrapSection section)
        => new()
        {
            ["found"] = RenderField(section.Found),
            ["trap"] = RenderField(section.Trap),
            ["diagnostic"] = RenderField(section.Diagnostic),
        };

    private static JsonObject RenderEvidenceSection(EvidenceSection section)
        => new() { ["entries"] = RenderField(section.Entries) };

    private static JsonObject RenderCoverageSection(CoverageSection section)
        => new()
        {
            ["availability"] = RenderField(section.Availability),
            ["ledger"] = RenderField(section.Ledger),
            ["scopes"] = RenderField(section.Scopes),
            ["ledgerDigest"] = RenderField(section.LedgerDigest),
        };

    private static JsonObject RenderTerminalSection(TerminalSection section)
        => new()
        {
            ["terminalState"] = RenderField(section.TerminalState),
            ["terminalReason"] = RenderField(section.TerminalReason),
            ["goalEvidenceBacksCompletion"] = RenderField(section.GoalEvidenceBacksCompletion),
        };

    private static JsonObject RenderBoundarySection(BoundaryVerification boundary)
    {
        var prohibitions = new JsonArray();
        foreach (var prohibition in boundary.Prohibitions)
        {
            var violations = new JsonArray();
            foreach (var violation in prohibition.Violations)
            {
                violations.Add(new JsonObject
                {
                    ["prohibition"] = violation.Prohibition.ToString(),
                    ["offendingRecord"] = violation.OffendingRecord,
                    ["reason"] = violation.Reason,
                });
            }

            var evidenceRefs = new JsonArray();
            foreach (var reference in prohibition.EvidenceRefs)
            {
                evidenceRefs.Add(JsonValue.Create(reference));
            }

            prohibitions.Add(new JsonObject
            {
                ["prohibition"] = prohibition.Prohibition.ToString(),
                ["positive"] = prohibition.Positive,
                ["evidenceRefs"] = evidenceRefs,
                ["violations"] = violations,
            });
        }

        return new JsonObject
        {
            ["passed"] = boundary.Passed,
            ["prohibitions"] = prohibitions,
        };
    }

    private static JsonObject RenderGates(ValidationGates gates)
        => new()
        {
            ["g1"] = RenderGate(gates.G1),
            ["g2"] = RenderGate(gates.G2),
            ["g3"] = RenderGate(gates.G3),
            ["g4"] = RenderGate(gates.G4),
        };

    private static JsonObject RenderGate(GateOutcome gate)
    {
        var evidenceRefs = new JsonArray();
        foreach (var reference in gate.EvidenceRefs)
        {
            evidenceRefs.Add(JsonValue.Create(reference));
        }

        return new JsonObject
        {
            ["passed"] = gate.Passed,
            ["evidenceRefs"] = evidenceRefs,
            ["offendingEvidence"] = gate.OffendingEvidence is null ? null : JsonValue.Create(gate.OffendingEvidence),
        };
    }

    /// <summary>One classified field: value + classification + truth-source;
    /// Unavailable renders as <c>unavailable</c> WITH its reason;
    /// IsPartial renders as <c>partial</c>.</summary>
    private static JsonObject RenderField(IClassifiedField field)
    {
        var rendered = new JsonObject
        {
            ["classification"] = field.Classification.ToString(),
            ["truthSource"] = field.TruthSource,
        };
        if (field.IsPartial)
        {
            rendered["partial"] = true;
        }

        if (field.Classification == ResultFieldClassification.Unavailable)
        {
            rendered["value"] = "unavailable";
            rendered["reason"] = field.TruthSource;
        }
        else
        {
            rendered["value"] = ToJsonNode(field.RawValue);
        }

        return rendered;
    }

    /// <summary>Recursive, cycle-guarded value serializer (deterministic;
    /// enums by name; value types by property).</summary>
    private static JsonNode? ToJsonNode(object? value, int depth = 0, HashSet<object>? visited = null)
    {
        if (value is null)
        {
            return null;
        }

        if (depth > MaxSerializationDepth)
        {
            return JsonValue.Create("…(truncated)");
        }

        switch (value)
        {
            case string text:
                return JsonValue.Create(text);
            case bool boolean:
                return JsonValue.Create(boolean);
            case int number:
                return JsonValue.Create(number);
            case long number:
                return JsonValue.Create(number);
            case double number:
                return JsonValue.Create(number);
            case Enum enumValue:
                return JsonValue.Create(enumValue.ToString());
        }

        visited ??= [];
        if (value is IEnumerable sequence and not string)
        {
            if (!visited.Add(sequence))
            {
                return JsonValue.Create("…(cycle)");
            }

            var array = new JsonArray();
            foreach (var item in sequence)
            {
                array.Add(ToJsonNode(item, depth + 1, visited));
            }

            visited.Remove(sequence);
            return array;
        }

        if (!visited.Add(value))
        {
            return JsonValue.Create("…(cycle)");
        }

        var obj = new JsonObject();
        foreach (var property in value.GetType()
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod is not null)
                     .OrderBy(p => p.MetadataToken))
        {
            object? propertyValue = null;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch (Exception)
            {
                // a throwing accessor is rendered as absent, never fatal
            }

            obj[property.Name] = ToJsonNode(propertyValue, depth + 1, visited);
        }

        visited.Remove(value);
        return obj;
    }

    // ── Markdown ──────────────────────────────────────────────────────────────

    /// <summary>Render the report as Markdown: eight sections + gates.</summary>
    public static string ToMarkdown(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var builder = new StringBuilder();
        builder.AppendLine("# Validation Report");
        builder.AppendLine();

        builder.AppendLine("## Admission");
        foreach (var line in RenderSectionLines(report.Result.Admission))
        {
            builder.AppendLine(line);
        }

        builder.AppendLine("## Lifecycle");
        builder.AppendLine(RenderFieldLine("", "events", report.Result.Lifecycle.Events));
        builder.AppendLine("## Snapshot");
        foreach (var line in RenderSectionLines(report.Result.Snapshot))
        {
            builder.AppendLine(line);
        }

        builder.AppendLine("## Trap");
        foreach (var line in RenderSectionLines(report.Result.Trap))
        {
            builder.AppendLine(line);
        }

        builder.AppendLine("## Evidence");
        builder.AppendLine(RenderFieldLine("", "entries", report.Result.Evidence.Entries));
        builder.AppendLine("## Coverage");
        foreach (var line in RenderSectionLines(report.Result.Coverage))
        {
            builder.AppendLine(line);
        }

        builder.AppendLine("## Terminal");
        foreach (var line in RenderSectionLines(report.Result.Terminal))
        {
            builder.AppendLine(line);
        }

        builder.AppendLine("## Boundary");
        builder.AppendLine($"- passed: {report.Boundary.Passed}");
        foreach (var prohibition in report.Boundary.Prohibitions)
        {
            var status = prohibition.Positive ? "POSITIVE" : "VIOLATED";
            builder.AppendLine($"- {prohibition.Prohibition}: {status}");
            foreach (var reference in prohibition.EvidenceRefs)
            {
                builder.AppendLine($"    - {SingleLine(reference)}");
            }

            foreach (var violation in prohibition.Violations)
            {
                builder.AppendLine($"    - VIOLATION: {SingleLine(violation.OffendingRecord)} — {SingleLine(violation.Reason)}");
            }
        }

        builder.AppendLine("## Gates");
        builder.AppendLine(RenderGateLine("G1", "directive-legal", report.Gates.G1));
        builder.AppendLine(RenderGateLine("G2", "end-to-end autonomy", report.Gates.G2));
        builder.AppendLine(RenderGateLine("G3", "result evidence-backed", report.Gates.G3));
        builder.AppendLine(RenderGateLine("G4", "boundary clean", report.Gates.G4));
        return builder.ToString();
    }

    private static IEnumerable<string> RenderSectionLines(AdmissionSection section)
    {
        yield return RenderFieldLine("", "runId", section.RunId);
        yield return RenderFieldLine("", "strategyId", section.StrategyId);
        yield return RenderFieldLine("", "accepted", section.Accepted);
        yield return RenderFieldLine("", "rejectionCode", section.RejectionCode);
        yield return RenderFieldLine("", "rejectionReason", section.RejectionReason);
        yield return RenderFieldLine("", "declaredMaximumDepth", section.DeclaredMaximumDepth);
    }

    private static IEnumerable<string> RenderSectionLines(SnapshotSection section)
    {
        yield return RenderFieldLine("", "runId", section.RunId);
        yield return RenderFieldLine("", "runState", section.RunState);
        yield return RenderFieldLine("", "currentSemanticPage", section.CurrentSemanticPage);
        yield return RenderFieldLine("", "activeTrap", section.ActiveTrap);
        yield return RenderFieldLine("", "currentGoal", section.CurrentGoal);
        yield return RenderFieldLine("", "lastDecision", section.LastDecision);
        yield return RenderFieldLine("", "lastAction", section.LastAction);
        yield return RenderFieldLine("", "recoveryState", section.RecoveryState);
        yield return RenderFieldLine("", "latestGoalEvidence", section.LatestGoalEvidence);
        yield return RenderFieldLine("", "currentObservationSequence", section.CurrentObservationSequence);
        yield return RenderFieldLine("", "currentContainerSummary", section.CurrentContainerSummary);
        yield return RenderFieldLine("", "bindingsSummary", section.BindingsSummary);
        yield return RenderFieldLine("", "stateBeliefsSummary", section.StateBeliefsSummary);
        yield return RenderFieldLine("", "diagnostics", section.Diagnostics);
    }

    private static IEnumerable<string> RenderSectionLines(TrapSection section)
    {
        yield return RenderFieldLine("", "found", section.Found);
        yield return RenderFieldLine("", "trap", section.Trap);
        yield return RenderFieldLine("", "diagnostic", section.Diagnostic);
    }

    private static IEnumerable<string> RenderSectionLines(CoverageSection section)
    {
        yield return RenderFieldLine("", "availability", section.Availability);
        yield return RenderFieldLine("", "ledger", section.Ledger);
        yield return RenderFieldLine("", "scopes", section.Scopes);
        yield return RenderFieldLine("", "ledgerDigest", section.LedgerDigest);
    }

    private static IEnumerable<string> RenderSectionLines(TerminalSection section)
    {
        yield return RenderFieldLine("", "terminalState", section.TerminalState);
        yield return RenderFieldLine("", "terminalReason", section.TerminalReason);
        yield return RenderFieldLine("", "goalEvidenceBacksCompletion", section.GoalEvidenceBacksCompletion);
    }

    private static string RenderGateLine(string gateId, string label, GateOutcome gate)
    {
        var refs = gate.EvidenceRefs.IsDefaultOrEmpty
            ? string.Empty
            : " (refs: " + string.Join("; ", gate.EvidenceRefs.Select(SingleLine)) + ")";
        return gate.Passed
            ? $"- {gateId} {label}: PASS{refs}"
            : $"- {gateId} {label}: FAIL — offending: {SingleLine(gate.OffendingEvidence ?? "no offending evidence recorded")}{refs}";
    }

    /// <summary>One field line: value + classification + truth-source;
    /// Unavailable renders as <c>unavailable</c> with its reason; partial
    /// renders as <c>(partial)</c>; collections expand per item.</summary>
    private static string RenderFieldLine(string indent, string name, IClassifiedField field)
    {
        var classification = field.Classification.ToString();
        var partial = field.IsPartial ? " (partial)" : string.Empty;
        if (field.Classification == ResultFieldClassification.Unavailable)
        {
            return $"{indent}- {name}: unavailable [{classification}]{partial} — {SingleLine(field.TruthSource)}";
        }

        var raw = field.RawValue;
        if (raw is null)
        {
            return $"{indent}- {name}: null [{classification}]{partial} — {SingleLine(field.TruthSource)}";
        }

        if (raw is IEnumerable sequence and not string)
        {
            var items = sequence.Cast<object?>().ToArray();
            var builder = new StringBuilder();
            builder.AppendLine($"{indent}- {name}: {items.Length} item(s) [{classification}]{partial} — {SingleLine(field.TruthSource)}");
            foreach (var item in items)
            {
                builder.AppendLine($"{indent}    - {SingleLine(DisplayItem(item))}");
            }

            return builder.ToString().TrimEnd('\r', '\n');
        }

        return $"{indent}- {name}: {SingleLine(DisplayItem(raw))} [{classification}]{partial} — {SingleLine(field.TruthSource)}";
    }

    /// <summary>Compact human-auditable display of a typed value.</summary>
    private static string DisplayItem(object? item)
        => item switch
        {
            null => "null",
            string text => text,
            bool boolean => boolean ? "true" : "false",
            System.Collections.IEnumerable sequence when sequence is not string
                => CollectSequence(sequence),
            _ => CompactJson(item),
        };

    private static string CollectSequence(IEnumerable sequence)
    {
        var parts = sequence.Cast<object?>().Select(DisplayItem).ToArray();
        return $"[{string.Join(", ", parts)}]";
    }

    private static string CompactJson(object value)
        => ToJsonNode(value)?.ToJsonString(JsonSerializerOptions.Web) ?? "null";

    private static string SingleLine(string text)
        => text.Replace("\r", " ").Replace("\n", " ").Trim();
}