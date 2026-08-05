using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace UniClaw.Host.Scenarios;

public sealed class ScenarioCatalog
{
    private static readonly Regex ScenarioIdPattern =
        new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public ScenarioSnapshot LoadSnapshot(string scenarioPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioPath);
        var fullScenarioPath = Path.GetFullPath(scenarioPath);
        var scenario = ReadScenario(fullScenarioPath);
        var policyPath = ResolvePolicyPath(fullScenarioPath, scenario.SafetyPolicy.Path);
        var policy = ReadPolicy(policyPath);

        if (!string.Equals(
                scenario.SafetyPolicy.PolicyId,
                policy.PolicyId,
                StringComparison.Ordinal))
        {
            throw Invalid(
                "safetyPolicy.policyId",
                scenario.SafetyPolicy.PolicyId,
                $"does not match policy file id '{policy.PolicyId}'");
        }

        var normalizedScenario = NormalizeAndValidate(scenario);
        var normalizedPolicy = NormalizeAndValidate(policy);
        var scenarioJson = JsonSerializer.Serialize(normalizedScenario, JsonOptions);
        var policyJson = JsonSerializer.Serialize(normalizedPolicy, JsonOptions);

        return new ScenarioSnapshot(
            normalizedScenario,
            ComputeHash(scenarioJson),
            scenarioJson,
            normalizedPolicy,
            ComputeHash(policyJson),
            policyJson);
    }

    public ImmutableArray<ScenarioSnapshot> LoadDirectory(string scenarioDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioDirectory);
        var fullDirectory = Path.GetFullPath(scenarioDirectory);
        if (!Directory.Exists(fullDirectory))
            throw new DirectoryNotFoundException(fullDirectory);

        var snapshots = Directory
            .EnumerateFiles(fullDirectory, "*.v1.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(LoadSnapshot)
            .ToImmutableArray();

        var duplicate = snapshots
            .GroupBy(snapshot => snapshot.Scenario.ScenarioId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw Invalid(
                "scenarioId",
                duplicate.Key,
                "duplicate scenario id in catalog");
        }

        return snapshots;
    }

    public static string ComputeHash(string normalizedJson)
    {
        ArgumentNullException.ThrowIfNull(normalizedJson);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(normalizedJson)))
            .ToLowerInvariant();
    }

    private static AndroidSettingsScenario ReadScenario(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AndroidSettingsScenario>(json, JsonOptions)
                   ?? throw Invalid("$", null, "scenario JSON was null");
        }
        catch (JsonException ex)
        {
            throw new ScenarioValidationException(
                ex.Path ?? "$",
                null,
                "invalid or unsupported scenario JSON",
                ex);
        }
    }

    private static SettingsSafetyPolicy ReadPolicy(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SettingsSafetyPolicy>(json, JsonOptions)
                   ?? throw Invalid("$", null, "policy JSON was null");
        }
        catch (JsonException ex)
        {
            throw new ScenarioValidationException(
                ex.Path ?? "$",
                null,
                "invalid or unsupported safety-policy JSON",
                ex);
        }
    }

    private static AndroidSettingsScenario NormalizeAndValidate(
        AndroidSettingsScenario scenario)
    {
        RequireSchema(scenario.SchemaVersion, "schemaVersion");
        var scenarioId = NormalizeId(scenario.ScenarioId, "scenarioId");
        if (!ScenarioIdPattern.IsMatch(scenarioId))
            throw Invalid("scenarioId", scenario.ScenarioId, "must be kebab-case");

        var description = RequireText(scenario.Description, "description");
        var appPackage = RequireText(scenario.AppPackage, "appPackage");
        var entryStrategy = RequireVocabulary(
            scenario.EntryStrategy,
            "entryStrategy",
            ScenarioVocabulary.EntryStrategies);
        var mode = RequireVocabulary(
            scenario.Mode,
            "mode",
            ScenarioVocabulary.Modes);
        var allowedActions = NormalizeVocabularyArray(
            scenario.AllowedActions,
            "allowedActions",
            ScenarioVocabulary.Actions,
            requireNonEmpty: true);

        var boundaries = scenario.Boundaries
                         ?? throw Invalid("boundaries", null, "is required");
        RequirePositive(boundaries.MaxDepth, "boundaries.maxDepth");
        RequirePositive(boundaries.MaxSteps, "boundaries.maxSteps");
        RequirePositive(boundaries.MaxScrolls, "boundaries.maxScrolls");
        RequirePositive(boundaries.MaxDurationSeconds, "boundaries.maxDurationSeconds");
        var normalizedBoundaries = boundaries with
        {
            AllowedPages = NormalizeTextArray(
                boundaries.AllowedPages,
                "boundaries.allowedPages",
                requireNonEmpty: true),
        };

        ScenarioTarget? target = null;
        if (scenario.Target is not null)
        {
            target = scenario.Target with
            {
                Label = RequireText(scenario.Target.Label, "target.label"),
                Aliases = NormalizeTextArray(
                    scenario.Target.Aliases,
                    "target.aliases",
                    requireNonEmpty: false),
            };
        }

        if (mode == "locate_one_item" && target is null)
            throw Invalid("target", null, "is required for locate_one_item");

        var policy = scenario.SafetyPolicy
                     ?? throw Invalid("safetyPolicy", null, "is required");
        var policyId = NormalizeId(policy.PolicyId, "policyId");
        var policyPath = RequireSafeRelativePath(policy.Path, "safetyPolicy.path");

        var success = scenario.SuccessCriteria
                      ?? throw Invalid("successCriteria", null, "is required");
        var successKind = RequireVocabulary(
            success.Kind,
            "successCriteria.kind",
            ScenarioVocabulary.SuccessKinds);
        if (mode == "locate_one_item" && successKind != "target_page_identity")
            throw Invalid("successCriteria.kind", success.Kind, "does not match locate mode");
        if (mode == "enumerate_first_level" && successKind != "verified_end_of_list")
            throw Invalid("successCriteria.kind", success.Kind, "does not match enumeration mode");
        var normalizedSuccess = success with
        {
            Kind = successKind,
            ExpectedPageIdentities = NormalizeTextArray(
                success.ExpectedPageIdentities,
                "successCriteria.expectedPageIdentities",
                requireNonEmpty: true),
        };

        var reset = scenario.ResetProcedure
                    ?? throw Invalid("resetProcedure", null, "is required");
        RequirePositive(reset.TimeoutSeconds, "resetProcedure.timeoutSeconds");
        var normalizedReset = reset with
        {
            Actions = NormalizeVocabularyArray(
                reset.Actions,
                "resetProcedure.actions",
                ScenarioVocabulary.Actions,
                requireNonEmpty: true),
            ExpectedPageIdentity = RequireText(
                reset.ExpectedPageIdentity,
                "resetProcedure.expectedPageIdentity"),
        };

        var excludePatterns = scenario.ExcludePatterns.IsDefault
            ? ImmutableArray<string>.Empty
            : NormalizeTextArray(scenario.ExcludePatterns, "excludePatterns", requireNonEmpty: false);

        return scenario with
        {
            SchemaVersion = ScenarioVocabulary.SchemaVersion,
            ScenarioId = scenarioId,
            Description = description,
            AppPackage = appPackage,
            EntryStrategy = entryStrategy,
            Mode = mode,
            Target = target,
            Boundaries = normalizedBoundaries,
            AllowedActions = allowedActions,
            SafetyPolicy = policy with { PolicyId = policyId, Path = policyPath },
            SuccessCriteria = normalizedSuccess,
            ResetProcedure = normalizedReset,
            ExcludePatterns = excludePatterns,
        };
    }

    private static SettingsSafetyPolicy NormalizeAndValidate(
        SettingsSafetyPolicy policy)
    {
        RequireSchema(policy.SchemaVersion, "schemaVersion");
        var policyId = NormalizeId(policy.PolicyId, "policyId");
        if (!ScenarioIdPattern.IsMatch(policyId))
            throw Invalid("policyId", policy.PolicyId, "must be kebab-case");

        var version = RequireText(policy.Version, "version");
        var allowedActions = NormalizeVocabularyArray(
            policy.AllowedActions,
            "allowedActions",
            ScenarioVocabulary.Actions,
            requireNonEmpty: true);
        var safeSemantics = NormalizeTokenArray(
            policy.SafeNavigationSemantics,
            "safeNavigationSemantics",
            requireNonEmpty: true);
        var dangerousSemantics = NormalizeTokenArray(
            policy.DangerousSemantics,
            "dangerousSemantics",
            requireNonEmpty: true);
        var dangerousText = NormalizeTokenArray(
            policy.DangerousText,
            "dangerousText",
            requireNonEmpty: true);

        var aliases = policy.Aliases.IsDefault
            ? ImmutableArray<PolicyAlias>.Empty
            : policy.Aliases
                .Select((alias, index) =>
                {
                    if (alias is null)
                        throw Invalid($"aliases[{index}]", null, "is required");
                    return alias with
                    {
                        Canonical = NormalizeToken(
                            RequireText(alias.Canonical, $"aliases[{index}].canonical")),
                        Values = NormalizeTokenArray(
                            alias.Values,
                            $"aliases[{index}].values",
                            requireNonEmpty: true),
                    };
                })
                .OrderBy(alias => alias.Canonical, StringComparer.Ordinal)
                .ToImmutableArray();
        EnsureDistinct(
            aliases.Select(alias => alias.Canonical),
            "aliases.canonical");

        var confidence = policy.ConfidenceThresholds
                         ?? throw Invalid("confidenceThresholds", null, "is required");
        RequireUnitInterval(
            confidence.MinimumTarget,
            "confidenceThresholds.minimumTarget");
        RequireUnitInterval(
            confidence.MinimumPageIdentity,
            "confidenceThresholds.minimumPageIdentity");

        var boundaries = policy.Boundaries
                         ?? throw Invalid("boundaries", null, "is required");
        RequirePositive(boundaries.MaxDepth, "boundaries.maxDepth");
        var normalizedBoundaries = boundaries with
        {
            AllowedPackages = NormalizeTextArray(
                boundaries.AllowedPackages,
                "boundaries.allowedPackages",
                requireNonEmpty: true),
            AllowedPagePrefixes = NormalizeTextArray(
                boundaries.AllowedPagePrefixes,
                "boundaries.allowedPagePrefixes",
                requireNonEmpty: true),
        };

        return policy with
        {
            SchemaVersion = ScenarioVocabulary.SchemaVersion,
            PolicyId = policyId,
            Version = version,
            AllowedActions = allowedActions,
            SafeNavigationSemantics = safeSemantics,
            DangerousSemantics = dangerousSemantics,
            DangerousText = dangerousText,
            Aliases = aliases,
            ConfidenceThresholds = confidence,
            Boundaries = normalizedBoundaries,
        };
    }

    private static string ResolvePolicyPath(
        string scenarioPath,
        string relativePolicyPath)
    {
        var safePath = RequireSafeRelativePath(
            relativePolicyPath,
            "safetyPolicy.path");
        var scenarioDirectory = Path.GetDirectoryName(scenarioPath)
                                ?? throw Invalid(
                                    "scenarioPath",
                                    scenarioPath,
                                    "has no parent directory");
        var candidate = Path.GetFullPath(Path.Combine(scenarioDirectory, safePath));
        var root = Path.GetFullPath(scenarioDirectory) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(root, StringComparison.Ordinal))
            throw Invalid("safetyPolicy.path", relativePolicyPath, "escapes scenario directory");
        return candidate;
    }

    private static string RequireSafeRelativePath(string value, string field)
    {
        var normalized = RequireText(value, field).Replace(
            Path.AltDirectorySeparatorChar,
            Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized)
            || normalized.Split(Path.DirectorySeparatorChar).Contains(".."))
        {
            throw Invalid(field, value, "must be a safe relative path");
        }

        return normalized;
    }

    private static void RequireSchema(string? value, string field)
    {
        if (!string.Equals(value?.Trim(), ScenarioVocabulary.SchemaVersion, StringComparison.Ordinal))
            throw Invalid(field, value, "unsupported schema version");
    }

    private static string RequireVocabulary(
        string? value,
        string field,
        ImmutableHashSet<string> vocabulary)
    {
        var normalized = NormalizeToken(RequireText(value, field));
        if (!vocabulary.Contains(normalized))
            throw Invalid(field, value, "unsupported vocabulary value");
        return normalized;
    }

    private static ImmutableArray<string> NormalizeVocabularyArray(
        ImmutableArray<string> values,
        string field,
        ImmutableHashSet<string> vocabulary,
        bool requireNonEmpty)
    {
        var normalized = NormalizeTokenArray(values, field, requireNonEmpty);
        foreach (var value in normalized)
        {
            if (!vocabulary.Contains(value))
                throw Invalid(field, value, "unsupported vocabulary value");
        }

        return normalized;
    }

    private static ImmutableArray<string> NormalizeTokenArray(
        ImmutableArray<string> values,
        string field,
        bool requireNonEmpty)
    {
        var source = values.IsDefault ? ImmutableArray<string>.Empty : values;
        var normalized = source
            .Select((value, index) => NormalizeToken(
                RequireText(value, $"{field}[{index}]")))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToImmutableArray();
        EnsureDistinct(normalized, field);
        if (requireNonEmpty && normalized.IsEmpty)
            throw Invalid(field, null, "must contain at least one value");
        return normalized;
    }

    private static ImmutableArray<string> NormalizeTextArray(
        ImmutableArray<string> values,
        string field,
        bool requireNonEmpty)
    {
        var source = values.IsDefault ? ImmutableArray<string>.Empty : values;
        var normalized = source
            .Select((value, index) => RequireText(value, $"{field}[{index}]"))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();
        EnsureDistinct(normalized, field, StringComparer.OrdinalIgnoreCase);
        if (requireNonEmpty && normalized.IsEmpty)
            throw Invalid(field, null, "must contain at least one value");
        return normalized;
    }

    private static void EnsureDistinct(
        IEnumerable<string> values,
        string field,
        StringComparer? comparer = null)
    {
        var seen = new HashSet<string>(comparer ?? StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!seen.Add(value))
                throw Invalid(field, value, "contains a duplicate value");
        }
    }

    private static string RequireText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Invalid(field, value, "is required");
        return string.Join(' ', value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string NormalizeToken(string value) =>
        RequireText(value, "value")
            .Replace('-', '_')
            .ToLowerInvariant();

    private static string NormalizeId(string? value, string field) =>
        RequireText(value, field).ToLowerInvariant();

    private static void RequirePositive(int value, string field)
    {
        if (value <= 0)
            throw Invalid(field, value, "must be positive");
    }

    private static void RequireUnitInterval(double value, string field)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw Invalid(field, value, "must be between 0 and 1");
    }

    private static ScenarioValidationException Invalid(
        string field,
        object? value,
        string message) =>
        new(field, value, message);
}
