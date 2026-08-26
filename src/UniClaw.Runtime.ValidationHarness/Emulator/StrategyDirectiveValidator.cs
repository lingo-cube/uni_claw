using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.ValidationHarness.Emulator;

/// <summary>
/// Closed-vocabulary directive validator (task 3.1; validation truth source =
/// the Strategy Contract surface in <c>Planning/StrategyContract.cs</c> +
/// <c>Model/StrategyDirective.cs</c>, mirrored via the frozen
/// <c>run.strategy.start</c> wire parse in DriverHost). It deterministically
/// rejects, BEFORE any wire call:
///
///  1. shape/type violations the frozen wire parse would refuse (unknown
///     fields, missing or mistyped fields);
///  2. closed enum fields whose value is not a defined member of the Strategy
///     vocabulary;
///  3. string fields violating the frozen constraints the Strategy Contract
///     enforces at admission (non-empty identifiers; supported contract
///     version; the finite depth guard from StrategyContractCompiler);
///  4. forbidden payload content (design D5 payload scan): coordinates, UI
///     page paths, click sequences, element locators, action selections,
///     callbacks, unresolved prose in closed fields.
///
/// Rejections are typed <see cref="DirectiveValidationResult.Rejected"/>
/// results, never exceptions. Semantic admission (tuple/boundary consistency,
/// capability resolution) stays with the Runtime compiler and surfaces as a
/// deterministic admission <c>Reject(code)</c> in the call log.
/// </summary>
public sealed class StrategyDirectiveValidator
{
    // ── Forbidden-content marker tables (deterministic). Scan order is fixed:
    //    the first category hit wins, so a given payload always maps to the
    //    same outcome. The tables are deliberate lexical heuristics, documented
    //    per category; they are NOT a semantic parser — the boundary proof they
    //    serve (D5) is "no injected action/coordinate/path content", not
    //    natural-language understanding. ──────────────────────────────────────

    private const string ProseStopWords =
        " the a an of to for with and or in on at by from into within across through during about over ";

    private static readonly Regex CoordinateAxisRegex = new(@"[xXyY]\s*=\s*-?\d+(?:\.\d+)?", RegexOptions.Compiled);
    private static readonly Regex CoordinatePairRegex = new(@"-?\d+(?:\.\d+)?\s*,\s*-?\d+(?:\.\d+)", RegexOptions.Compiled);
    private static readonly Regex PixelUnitRegex = new(@"\d+(?:\.\d+)?\s*px", RegexOptions.Compiled);

    private static readonly Regex PathSegmentRegex = new(@"\S+/\S+", RegexOptions.Compiled);
    private static readonly Regex BreadcrumbSeparatorRegex = new(@"\S+\s*[\\>»›]\s*\S+", RegexOptions.Compiled);

    private static readonly Regex ClickVerbRegex = new(
        @"\b(?:tap|click|double[- ]?tap|dblclick|long[- ]?press|longpress|swipe|scroll|drag)\b",
        RegexOptions.Compiled);
    private static readonly Regex ClickSequenceMarkerRegex = new(
        @"\b(?:then|next|after that|and then|step\s*\d+)\b|->|→",
        RegexOptions.Compiled);

    private static readonly Regex ActionVerbRegex = new(
        @"\b(?:tap|click|press|swipe|scroll|drag|type|enter|toggle|select|submit|navigate|open|close|focus|hover|long[- ]?press|double[- ]?tap)\b",
        RegexOptions.Compiled);

    private static readonly Regex ElementLocatorMarkerRegex = new(
        @"^[#.@][A-Za-z0-9_-]+|\[[A-Za-z-]+\s*=|(?:xpath|css|id|name|class|text|resource-id|testid|data-testid|data-qa|accessibility-id|content-desc|locator|selector)\s*[=:]",
        RegexOptions.Compiled);

    private static readonly Regex CallbackMarkerRegex = new(
        @"\b(?:callback|handler|delegate|function|subscribe|register|trigger|invoke|dispatch|event)\b|\bon[A-Z][A-Za-z0-9]*|=>",
        RegexOptions.Compiled);

    private readonly IReadOnlyList<ContentRule> _contentRules = BuildContentRules();

    /// <summary>Validate one canonical strategy payload (the <c>strategy</c>
    /// object the driver would transport). Deterministic: the same payload
    /// always returns the same result.</summary>
    public DirectiveValidationResult Validate(JsonObject strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        var structural = CheckTopLevel(strategy);
        if (structural is not null)
            return structural;

        var scan = ScanForForbiddenContent(strategy);
        if (scan is not null)
        {
            var (path, category, label) = scan.Value;
            return new DirectiveValidationResult.Rejected(
                category,
                $"Payload field '{path}' contains forbidden {label}; the directive is refused before transport.");
        }

        return new DirectiveValidationResult.Legal();
    }

    // ── Closed vocabulary + frozen constraints (items 1-3) ─────────────────────

    private DirectiveValidationResult.Rejected? CheckTopLevel(JsonObject strategy)
    {
        if (FindUnknownField(strategy, "strategyId", "contractVersion", "objective", "scope", "exploration", "constraints", "completion", "adaptation") is { } unknownTop)
            return Reject($"unknown directive field '{unknownTop}' (closed StrategyDirective shape)");

        if (!TryString(strategy, "strategyId", out var strategyId))
            return Reject("'strategyId' must be a non-empty string");
        _ = strategyId;

        if (!TryInt(strategy, "contractVersion", out var contractVersion))
            return Reject("'contractVersion' must be an integer");
        if (contractVersion != StrategyContractCompiler.SupportedContractVersion)
            return Reject($"'contractVersion' value {contractVersion} is outside the closed Strategy Contract (supported version: {StrategyContractCompiler.SupportedContractVersion})");

        if (strategy["objective"] is not JsonObject objective)
            return Reject("'objective' must be an object");
        if (CheckObjective(objective) is { } objectiveError)
            return objectiveError;

        if (strategy["scope"] is not JsonObject scope)
            return Reject("'scope' must be an object");
        if (CheckScope(scope) is { } scopeError)
            return scopeError;

        if (CheckClosedEnum<ExplorationIntent>(strategy, "exploration", "exploration", "ExplorationIntent") is { } explorationError)
            return explorationError;
        if (strategy["constraints"] is not JsonObject constraints)
            return Reject("'constraints' must be an object");
        if (CheckConstraints(constraints) is { } constraintsError)
            return constraintsError;

        if (strategy["completion"] is not JsonObject completion)
            return Reject("'completion' must be an object");
        if (FindUnknownField(completion, "kind") is { } unknownCompletion)
            return Reject($"'completion' carries unknown field '{unknownCompletion}'");
        if (CheckClosedEnum<StrategyCompletionKind>(completion, "completion.kind", "kind", "StrategyCompletionKind") is { } completionError)
            return completionError;

        if (strategy["adaptation"] is not JsonObject adaptation)
            return Reject("'adaptation' must be an object");
        if (FindUnknownField(adaptation, "allowedAdaptations") is { } unknownAdaptation)
            return Reject($"'adaptation' carries unknown field '{unknownAdaptation}'");
        if (TryClosedEnumSet<StrategyAdaptationKind>(adaptation, "allowedAdaptations", StrategyAdaptationKindRejectName) is { } adaptationError)
            return adaptationError;

        return null;
    }

    private DirectiveValidationResult.Rejected? CheckObjective(JsonObject objective)
    {
        if (FindUnknownField(objective, "kind", "criterion") is { } unknown)
            return Reject($"'objective' carries unknown field '{unknown}'");
        if (CheckClosedEnum<StrategyObjectiveKind>(objective, "objective.kind", "kind", "StrategyObjectiveKind") is { } kindError)
            return kindError;

        if (objective["criterion"] is null)
            return null;
        if (objective["criterion"] is not JsonObject criterion)
            return Reject("'objective.criterion' must be an object when present");
        if (FindUnknownField(criterion, "capabilityId", "criterionId", "version") is { } unknownCriterion)
            return Reject($"'objective.criterion' carries unknown field '{unknownCriterion}'");
        if (!TryString(criterion, "capabilityId", out _))
            return Reject("'objective.criterion.capabilityId' must be a non-empty string");
        if (!TryString(criterion, "criterionId", out _))
            return Reject("'objective.criterion.criterionId' must be a non-empty string");
        if (!TryInt(criterion, "version", out var version) || version <= 0)
            return Reject("'objective.criterion.version' must be a positive integer");
        return null;
    }

    private DirectiveValidationResult.Rejected? CheckScope(JsonObject scope)
    {
        if (FindUnknownField(scope, "applicationIdentity", "semanticRoot", "maximumDepth") is { } unknown)
            return Reject($"'scope' carries unknown field '{unknown}'");
        if (!TryString(scope, "applicationIdentity", out _))
            return Reject("'scope.applicationIdentity' must be a non-empty string");
        if (!TryString(scope, "semanticRoot", out _))
            return Reject("'scope.semanticRoot' must be a non-empty string");
        if (!TryInt(scope, "maximumDepth", out var depth))
            return Reject("'scope.maximumDepth' must be an integer");
        if (depth < 0 || depth > StrategyContractCompiler.MaximumSupportedDepth)
            return Reject($"'scope.maximumDepth' value {depth} exceeds the frozen finite depth guard ({StrategyContractCompiler.MaximumSupportedDepth}); larger requests fail closed as unbounded");
        return null;
    }

    private DirectiveValidationResult.Rejected? CheckConstraints(JsonObject constraints)
    {
        if (FindUnknownField(constraints, "allowedInteractionCategories", "prohibitedEffects") is { } unknown)
            return Reject($"'constraints' carries unknown field '{unknown}'");
        if (TryClosedEnumSet<TypeLevelElementCategory>(constraints, "allowedInteractionCategories", TypeLevelElementCategoryRejectName, requireNonEmpty: true) is { } categoriesError)
            return categoriesError;
        if (TryClosedEnumSet<StrategyProhibitedEffect>(constraints, "prohibitedEffects", StrategyProhibitedEffectRejectName) is { } effectsError)
            return effectsError;
        return null;
    }

    private static string StrategyAdaptationKindRejectName(string value)
        => $"'adaptation.allowedAdaptations' value '{value}' is outside the closed StrategyAdaptationKind vocabulary";

    private static string TypeLevelElementCategoryRejectName(string value)
        => $"'constraints.allowedInteractionCategories' value '{value}' is outside the closed TypeLevelElementCategory vocabulary";

    private static string StrategyProhibitedEffectRejectName(string value)
        => $"'constraints.prohibitedEffects' value '{value}' is outside the closed StrategyProhibitedEffect vocabulary";

    // ── Forbidden payload content scan (item 4) ────────────────────────────────

    private (string Path, DirectiveForbiddenCategory Category, string Label)? ScanForForbiddenContent(JsonObject root)
        => ScanNode(root, "strategy");

    private (string, DirectiveForbiddenCategory, string)? ScanNode(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    var hit = ScanNode(property.Value, $"{path}.{property.Key}");
                    if (hit is not null)
                        return hit;
                }

                return null;

            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    var hit = ScanNode(array[index], $"{path}[{index}]");
                    if (hit is not null)
                        return hit;
                }

                return null;

            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                var text = value.GetValue<string>();
                if (string.IsNullOrWhiteSpace(text))
                    return null;
                foreach (var rule in _contentRules)
                {
                    if (rule.Matches(text))
                        return (path, rule.Category, rule.Label);
                }

                return null;

            default:
                return null;
        }
    }

    // ── Content rules (fixed order: the first category hit wins) ───────────────

    private static IReadOnlyList<ContentRule> BuildContentRules() => new ContentRule[]
    {
        new(
            DirectiveForbiddenCategory.Coordinate,
            "coordinate content (axis values, numeric pairs, pixel units)",
            text => CoordinateAxisRegex.IsMatch(text) || CoordinatePairRegex.IsMatch(text) || PixelUnitRegex.IsMatch(text)),
        new(
            DirectiveForbiddenCategory.UiPagePath,
            "UI page path / breadcrumb content",
            text => PathSegmentRegex.IsMatch(text) || BreadcrumbSeparatorRegex.IsMatch(text)),
        new(
            DirectiveForbiddenCategory.ClickSequence,
            "click/gesture sequence content",
            text => ClickVerbRegex.Matches(text).Count >= 2
                || (ClickVerbRegex.IsMatch(text) && ClickSequenceMarkerRegex.IsMatch(text))),
        new(
            DirectiveForbiddenCategory.ElementLocator,
            "element locator / selector content",
            text => ElementLocatorMarkerRegex.IsMatch(text)),
        new(
            DirectiveForbiddenCategory.ActionSelection,
            "action selection content",
            text => ActionVerbRegex.IsMatch(text)),
        new(
            DirectiveForbiddenCategory.Callback,
            "callback / handler content",
            text => CallbackMarkerRegex.IsMatch(text)),
        new(
            DirectiveForbiddenCategory.UnresolvedProse,
            "unresolved prose in a closed-value field",
            text => IsUnresolvedProse(text)),
    };

    private static bool IsUnresolvedProse(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2)
            return false; // semantic identities are single tokens (e.g. "SettingsRoot")
        if (words.Any(word => ProseStopWords.Contains(" " + word.ToLowerInvariant() + " ", StringComparison.Ordinal)))
            return true;
        if (text.Contains(". ", StringComparison.Ordinal)
            || text.Contains(", ", StringComparison.Ordinal)
            || text.Contains("! ", StringComparison.Ordinal)
            || text.Contains("? ", StringComparison.Ordinal))
        {
            return true;
        }

        return char.IsUpper(text[0]) && words.Length >= 3; // sentence-like start
    }

    private sealed record ContentRule(
        DirectiveForbiddenCategory Category,
        string Label,
        Func<string, bool> Matches);

    // ── JSON access helpers (mirror the frozen wire parse, return reasons) ─────

    private static DirectiveValidationResult.Rejected Reject(string message)
        => new(null, $"Directive field: {message}");

    private static string? FindUnknownField(JsonObject obj, params string[] allowed)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        return obj.Select(property => property.Key).FirstOrDefault(key => !allowedSet.Contains(key));
    }

    private static bool TryString(JsonObject obj, string key, out string value)
    {
        if (obj[key] is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.String)
        {
            value = jsonValue.GetValue<string>();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

    private static bool TryInt(JsonObject obj, string key, out int value)
    {
        if (obj[key] is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.Number
            && jsonValue.TryGetValue<int>(out var result))
        {
            value = result;
            return true;
        }

        value = 0;
        return false;
    }

    private static DirectiveValidationResult.Rejected? CheckClosedEnum<T>(
        JsonObject obj,
        string fieldPath,
        string key,
        string vocabularyName)
        where T : struct, Enum
    {
        if (obj[key] is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
            return Reject($"'{fieldPath}' must be a string");
        var text = value.GetValue<string>();
        if (!Enum.TryParse<T>(text, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            return Reject($"'{fieldPath}' value '{text}' is outside the closed {vocabularyName} vocabulary");
        return null;
    }

    private static DirectiveValidationResult.Rejected? TryClosedEnumSet<T>(
        JsonObject obj,
        string key,
        Func<string, string> rejectReason,
        bool requireNonEmpty = false)
        where T : struct, Enum
    {
        if (obj[key] is not JsonArray array)
            return Reject($"'{key}' must be an array");
        if (requireNonEmpty && array.Count == 0)
            return Reject($"'{key}' must not be empty");
        foreach (var item in array)
        {
            if (item is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
                return Reject($"each '{key}' entry must be a string");
            var text = value.GetValue<string>();
            if (!Enum.TryParse<T>(text, ignoreCase: true, out var enumValue) || !Enum.IsDefined(enumValue))
                return Reject(rejectReason(text));
        }

        return null;
    }
}