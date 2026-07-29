using System.Collections.Immutable;

namespace UniClaw.Host.Scenarios;

public static class ScenarioVocabulary
{
    public const string SchemaVersion = "1";

    public static readonly ImmutableHashSet<string> Modes =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "locate_one_item", "enumerate_first_level");

    public static readonly ImmutableHashSet<string> EntryStrategies =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "cold_launch", "bind_current_screen", "direct_deeplink");

    public static readonly ImmutableHashSet<string> Actions =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "click", "back", "scroll", "launch", "wait");

    public static readonly ImmutableHashSet<string> SuccessKinds =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "target_page_identity", "verified_end_of_list");
}

public sealed record class AndroidSettingsScenario(
    string SchemaVersion,
    string ScenarioId,
    string Description,
    string AppPackage,
    string EntryStrategy,
    string Mode,
    ScenarioTarget? Target,
    ScenarioBoundaries Boundaries,
    ImmutableArray<string> AllowedActions,
    SafetyPolicyReference SafetyPolicy,
    ScenarioSuccessCriteria SuccessCriteria,
    ScenarioResetProcedure ResetProcedure);

public sealed record class ScenarioTarget(
    string Label,
    ImmutableArray<string> Aliases);

public sealed record class ScenarioBoundaries(
    ImmutableArray<string> AllowedPages,
    int MaxDepth,
    int MaxSteps,
    int MaxScrolls,
    int MaxDurationSeconds);

public sealed record class SafetyPolicyReference(
    string PolicyId,
    string Path);

public sealed record class ScenarioSuccessCriteria(
    string Kind,
    ImmutableArray<string> ExpectedPageIdentities,
    bool RequireEndOfList);

public sealed record class ScenarioResetProcedure(
    ImmutableArray<string> Actions,
    string ExpectedPageIdentity,
    int TimeoutSeconds);

public sealed record class SettingsSafetyPolicy(
    string SchemaVersion,
    string PolicyId,
    string Version,
    ImmutableArray<string> AllowedActions,
    ImmutableArray<string> SafeNavigationSemantics,
    ImmutableArray<string> DangerousSemantics,
    ImmutableArray<string> DangerousText,
    ImmutableArray<PolicyAlias> Aliases,
    PolicyConfidenceThresholds ConfidenceThresholds,
    PolicyBoundaryRules Boundaries);

public sealed record class PolicyAlias(
    string Canonical,
    ImmutableArray<string> Values);

public sealed record class PolicyConfidenceThresholds(
    double MinimumTarget,
    double MinimumPageIdentity);

public sealed record class PolicyBoundaryRules(
    ImmutableArray<string> AllowedPackages,
    ImmutableArray<string> AllowedPagePrefixes,
    int MaxDepth);

public sealed record class ScenarioSnapshot(
    AndroidSettingsScenario Scenario,
    string ScenarioHash,
    string NormalizedScenarioJson,
    SettingsSafetyPolicy Policy,
    string PolicyHash,
    string NormalizedPolicyJson)
{
    public async Task WriteScenarioAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (directory is not null)
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(
            destinationPath,
            NormalizedScenarioJson,
            cancellationToken);
    }
}

public sealed class ScenarioValidationException : Exception
{
    public string FieldName { get; }

    public object? IllegalValue { get; }

    public ScenarioValidationException(
        string fieldName,
        object? illegalValue,
        string message)
        : base($"{fieldName}: {message} (value: {illegalValue ?? "<null>"})")
    {
        FieldName = fieldName;
        IllegalValue = illegalValue;
    }

    public ScenarioValidationException(
        string fieldName,
        object? illegalValue,
        string message,
        Exception innerException)
        : base($"{fieldName}: {message} (value: {illegalValue ?? "<null>"})", innerException)
    {
        FieldName = fieldName;
        IllegalValue = illegalValue;
    }
}
