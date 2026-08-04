using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UniClaw.Core.Observability;
using UniClaw.Host.Scenarios;

namespace UniClaw.Host.Artifacts;

public static class RunAssetVocabulary
{
    public const string SchemaVersion = "2";

    public static readonly ImmutableHashSet<string> ResultStatuses =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "running",
            "success",
            "incomplete",
            "blocked",
            "failure",
            "cancelled",
            "pending_verification");

    public static readonly ImmutableHashSet<string> IssueCategories =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "device",
            "perception",
            "planning",
            "safety",
            "action",
            "verification",
            "traversal",
            "provider",
            "reporting");
}

/// <summary>
/// Verification criteria snapshot written at run end.
/// Consumed by TraceTool verify command.
/// </summary>
public sealed record class VerificationCriteria(
    ImmutableArray<string> ExpectedPageIdentities,
    string Mode);

public sealed record class RunManifest(
    string SchemaVersion,
    string RunId,
    string? IterationId,
    string? ParentRunId,
    string ScenarioId,
    string ScenarioHash,
    string PolicyId,
    string PolicyVersion,
    string PolicyHash,
    string? SourceRevision,
    string DeviceSerial,
    string? AndroidIdentity,
    string AppPackage,
    string ProviderId,
    string? Model,
    string Mode,
    ImmutableDictionary<string, string> AssetSchemas,
    DateTimeOffset StartedAt,
    ImmutableDictionary<string, string> OutputPaths,
    string? Purpose = null,
    string? TaskId = null,
    RunSystemInfo? SystemInfo = null,
    RunMachineInfo? MachineInfo = null);

public sealed record class RunResult(
    string SchemaVersion,
    string RunId,
    string Status,
    string CompletionReason,
    int DiscoveredEntries,
    int VisitedEntries,
    int SkippedEntries,
    int FailedEntries,
    int ActionsAttempted,
    int ActionsSucceeded,
    int SafetyAllowed,
    int SafetyDenied,
    int StepsConsumed,
    int ScrollsConsumed,
    long DurationMs,
    string TracePath,
    ImmutableArray<string> IssueFingerprints,
    bool SuccessCriteriaSatisfied,
    ImmutableArray<string> SuccessEvidence,
    DateTimeOffset UpdatedAt);

public sealed record class RunSystemInfo(
    string? SdkLevel,
    string? ReleaseVersion,
    string? BuildFingerprint,
    string? Codename,
    string? Arch);

public sealed record class RunMachineInfo(
    string Os,
    string Arch,
    string Runtime,
    string Hostname);

public sealed record class StepEnvelope<T>(
    string SchemaVersion,
    string RunId,
    int StepNumber,
    DateTimeOffset Timestamp,
    string PageFingerprint,
    string Phase,
    string Status,
    T? Payload,
    string? MissingReason = null);

public sealed record class RunIssue(
    string SchemaVersion,
    string IssueId,
    string Fingerprint,
    string Category,
    string Phase,
    string Severity,
    string Summary,
    string RunId,
    int? StepNumber,
    ImmutableArray<string> EvidencePaths,
    DateTimeOffset FirstSeenAt,
    int OccurrenceCount,
    string? RepeatsIssueId,
    string Disposition);

public sealed record class AggregateChildRun(
    string RunId,
    string Status,
    long DurationMs,
    int SafetyAllowed,
    int SafetyDenied,
    string ScenarioHash,
    string PolicyHash,
    ImmutableArray<string> IssueFingerprints,
    ImmutableDictionary<string, long> PhaseLatencyMs);

public sealed record class IterationAggregate(
    string SchemaVersion,
    string AggregateId,
    ImmutableArray<AggregateChildRun> Children,
    double SuccessRate,
    int LongestConsecutiveSuccesses,
    long TotalDurationMs,
    int SafetyAllowed,
    int SafetyDenied,
    ImmutableDictionary<string, long> PhaseLatencyMs,
    ImmutableArray<string> NewIssueFingerprints,
    ImmutableArray<string> RepeatedIssueFingerprints,
    ImmutableArray<string> DisappearedIssueFingerprints,
    DateTimeOffset CreatedAt);

public interface IAssetRedactor
{
    string Redact(string value);
}

public sealed partial class AssetRedactor : IAssetRedactor
{
    private const string Marker = "[REDACTED]";
    private readonly ImmutableArray<string> _secrets;

    public AssetRedactor(IEnumerable<string>? configuredSecrets = null)
    {
        _secrets = configuredSecrets?
            .Where(secret => !string.IsNullOrWhiteSpace(secret))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(secret => secret.Length)
            .ToImmutableArray()
            ?? ImmutableArray<string>.Empty;
    }

    public string Redact(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var redacted = AuthorizationHeaderRegex().Replace(
            value,
            match => $"{match.Groups[1].Value}{Marker}");
        redacted = CredentialAssignmentRegex().Replace(
            redacted,
            match => $"{match.Groups[1].Value}{Marker}{match.Groups[3].Value}");
        foreach (var secret in _secrets)
            redacted = redacted.Replace(secret, Marker, StringComparison.Ordinal);
        return redacted;
    }

    [GeneratedRegex(
        @"(?i)(authorization\s*[:=]\s*(?:bearer\s+|basic\s+)?)([^\s"",;\\]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex(
        @"(?i)((?:api[_-]?key|access[_-]?token|client[_-]?secret|provider[_-]?credential)\s*[""']?\s*[:=]\s*[""']?)([^""'\s,;}]+)([""']?)",
        RegexOptions.CultureInvariant)]
    private static partial Regex CredentialAssignmentRegex();
}

public sealed class RunAssetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly IAssetRedactor _redactor;

    public RunAssetStore(IAssetRedactor? redactor = null)
    {
        _redactor = redactor ?? new AssetRedactor();
    }

    public static string AllocateRunId(DateTimeOffset? now = null)
    {
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return $"{timestamp:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}"[..35];
    }

    public async Task<RunAssetSession> CreateAsync(
        string outputRoot,
        ScenarioSnapshot snapshot,
        object compiledPlan,
        RunManifestInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(compiledPlan);
        ArgumentNullException.ThrowIfNull(input);

        var runId = string.IsNullOrWhiteSpace(input.RunId)
            ? AllocateRunId()
            : ValidatePathSegment(input.RunId, nameof(input.RunId));
        var scenarioId = ValidatePathSegment(
            snapshot.Scenario.ScenarioId,
            "scenarioId");
        var scenarioRoot = Path.Combine(Path.GetFullPath(outputRoot), scenarioId);
        Directory.CreateDirectory(scenarioRoot);
        var finalPath = Path.Combine(scenarioRoot, runId);
        if (Directory.Exists(finalPath))
            throw new IOException($"Run directory already exists: {finalPath}");

        var stagingPath = Path.Combine(
            scenarioRoot,
            $".{runId}.creating-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingPath);

        try
        {
            // V2 layout: trace/{runId}/ and assets/{runId}/steps/
            Directory.CreateDirectory(Path.Combine(stagingPath, "trace", runId));
            Directory.CreateDirectory(Path.Combine(stagingPath, "assets", runId, "steps"));

            var manifest = BuildManifest(runId, snapshot, input);
            var initialResult = new RunResult(
                RunAssetVocabulary.SchemaVersion,
                runId,
                "running",
                "run_initialized",
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                $"trace/{runId}/trace.jsonl",
                [],
                false,
                [],
                DateTimeOffset.UtcNow);

            await WriteJsonAsync(
                Path.Combine(stagingPath, "manifest.json"),
                manifest,
                cancellationToken);
            await WriteTextAsync(
                Path.Combine(stagingPath, "scenario.snapshot.json"),
                snapshot.NormalizedScenarioJson,
                cancellationToken);
            await WriteJsonAsync(
                Path.Combine(stagingPath, "plan.json"),
                compiledPlan,
                cancellationToken);
            await WriteJsonAsync(
                Path.Combine(stagingPath, "result.json"),
                initialResult,
                cancellationToken);
            await WriteTextAsync(
                Path.Combine(stagingPath, "issues.jsonl"),
                string.Empty,
                cancellationToken);

            Directory.Move(stagingPath, finalPath);
            return new RunAssetSession(
                finalPath,
                manifest,
                snapshot,
                _redactor,
                JsonOptions);
        }
        catch
        {
            if (Directory.Exists(stagingPath))
                Directory.Delete(stagingPath, recursive: true);
            throw;
        }
    }

    private async Task WriteJsonAsync(
        string path,
        object value,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await WriteTextAsync(path, json, cancellationToken);
    }

    private async Task WriteTextAsync(
        string path,
        string value,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(
            path,
            _redactor.Redact(value),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static RunManifest BuildManifest(
        string runId,
        ScenarioSnapshot snapshot,
        RunManifestInput input) =>
        new(
            RunAssetVocabulary.SchemaVersion,
            runId,
            input.IterationId,
            input.ParentRunId,
            snapshot.Scenario.ScenarioId,
            snapshot.ScenarioHash,
            snapshot.Policy.PolicyId,
            snapshot.Policy.Version,
            snapshot.PolicyHash,
            input.SourceRevision,
            input.DeviceSerial,
            input.AndroidIdentity,
            snapshot.Scenario.AppPackage,
            input.ProviderId,
            input.Model,
            input.Mode,
            ImmutableDictionary<string, string>.Empty
                .Add("manifest", RunAssetVocabulary.SchemaVersion)
                .Add("step", RunAssetVocabulary.SchemaVersion)
                .Add("issue", RunAssetVocabulary.SchemaVersion)
                .Add("result", RunAssetVocabulary.SchemaVersion)
                .Add("criteria", RunAssetVocabulary.SchemaVersion),
            DateTimeOffset.UtcNow,
            ImmutableDictionary<string, string>.Empty
                .Add("scenario", "scenario.snapshot.json")
                .Add("plan", "plan.json")
                .Add("steps", $"assets/{runId}/steps")
                .Add("trace", $"trace/{runId}")
                .Add("issues", "issues.jsonl")
                .Add("result", "result.json")
                .Add("criteria", "criteria.json"),
            input.Purpose,
            input.TaskId,
            input.SystemInfo,
            input.MachineInfo);

    private static string ValidatePathSegment(string value, string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, field);
        if (!string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal)
            || value is "." or "..")
        {
            throw new ArgumentException("Must be one safe path segment.", field);
        }
        return value;
    }
}

public sealed record class RunManifestInput(
    string? RunId,
    string? IterationId,
    string? ParentRunId,
    string? SourceRevision,
    string DeviceSerial,
    string? AndroidIdentity,
    string ProviderId,
    string? Model,
    string Mode,
    string? Purpose = null,
    string? TaskId = null,
    RunSystemInfo? SystemInfo = null,
    RunMachineInfo? MachineInfo = null);

public sealed class RunAssetSession
{
    private readonly IAssetRedactor _redactor;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private int _lastStepNumber;
    private bool _finalized;

    internal RunAssetSession(
        string runDirectory,
        RunManifest manifest,
        ScenarioSnapshot snapshot,
        IAssetRedactor redactor,
        JsonSerializerOptions jsonOptions)
    {
        RunDirectory = runDirectory;
        Manifest = manifest;
        Snapshot = snapshot;
        _redactor = redactor;
        _jsonOptions = jsonOptions;
    }

    public string RunDirectory { get; }

    public RunManifest Manifest { get; }

    public ScenarioSnapshot Snapshot { get; }

    public async Task<StepAssetWriter> BeginStepAsync(
        int stepNumber,
        string pageFingerprint,
        CancellationToken cancellationToken = default)
    {
        if (stepNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(stepNumber));
        ArgumentException.ThrowIfNullOrWhiteSpace(pageFingerprint);

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            if (_finalized)
                throw new InvalidOperationException("Run is already finalized.");
            if (stepNumber != _lastStepNumber + 1)
            {
                throw new InvalidOperationException(
                    $"Step {stepNumber} is not causally next after {_lastStepNumber}.");
            }

            var relativeDirectory = Path.Combine("assets", Manifest.RunId, "steps", stepNumber.ToString("D4"));
            var absoluteDirectory = Path.Combine(RunDirectory, relativeDirectory);
            Directory.CreateDirectory(absoluteDirectory);
            _lastStepNumber = stepNumber;
            return new StepAssetWriter(
                this,
                stepNumber,
                pageFingerprint,
                relativeDirectory,
                absoluteDirectory);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task AppendIssueAsync(
        RunIssue issue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (!RunAssetVocabulary.IssueCategories.Contains(issue.Category))
            throw new ArgumentException("Unsupported issue category.", nameof(issue));
        if (!string.Equals(issue.RunId, Manifest.RunId, StringComparison.Ordinal))
            throw new ArgumentException("Issue run ID does not match the session.", nameof(issue));

        await AppendJsonLineAsync("issues.jsonl", issue, cancellationToken);
    }

    public RunIssue CreateIssue(
        string category,
        string phase,
        string severity,
        string summary,
        int? stepNumber,
        IEnumerable<string>? evidencePaths = null,
        string disposition = "open",
        string? repeatsIssueId = null,
        int occurrenceCount = 1)
    {
        var normalizedSummary = Normalize(summary);
        var fingerprintInput = string.Join(
            "|",
            Normalize(category),
            Normalize(phase),
            normalizedSummary);
        var fingerprint = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)))
            .ToLowerInvariant()[..20];
        return new RunIssue(
            RunAssetVocabulary.SchemaVersion,
            $"issue-{Guid.NewGuid():N}",
            fingerprint,
            Normalize(category),
            Normalize(phase),
            Normalize(severity),
            summary.Trim(),
            Manifest.RunId,
            stepNumber,
            evidencePaths?.ToImmutableArray() ?? [],
            DateTimeOffset.UtcNow,
            occurrenceCount,
            repeatsIssueId,
            Normalize(disposition));
    }

    public async Task WriteCriteriaAsync(
        VerificationCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        await WriteJsonAsync(
            Path.Combine(RunDirectory, RunLayoutV2.CriteriaFileName),
            criteria,
            cancellationToken);
    }

    public async Task FinalizeAsync(
        RunResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!RunAssetVocabulary.ResultStatuses.Contains(result.Status)
            || result.Status == "running")
        {
            throw new ArgumentException("Result requires a terminal status.", nameof(result));
        }
        if (!string.Equals(result.RunId, Manifest.RunId, StringComparison.Ordinal))
            throw new ArgumentException("Result run ID does not match the session.", nameof(result));
        if (result.Status == "success" && !result.SuccessCriteriaSatisfied)
            throw new ArgumentException("Success cannot overstate unmet criteria.", nameof(result));

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            if (_finalized)
                throw new InvalidOperationException("Run is already finalized.");
            await WriteJsonAsync(
                Path.Combine(RunDirectory, "result.json"),
                result,
                cancellationToken);
            _finalized = true;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    internal Task WriteStepJsonAsync<T>(
        string path,
        StepEnvelope<T> envelope,
        CancellationToken cancellationToken) =>
        WriteJsonAsync(path, envelope, cancellationToken);

    internal async Task WriteStepTextAsync(
        string path,
        string text,
        CancellationToken cancellationToken)
    {
        var redacted = _redactor.Redact(text);
        await File.WriteAllTextAsync(
            path,
            redacted,
            new UTF8Encoding(false),
            cancellationToken);
    }

    internal Task WriteStepBytesAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken) =>
        File.WriteAllBytesAsync(path, bytes, cancellationToken);

    private async Task AppendJsonLineAsync(
        string relativePath,
        object value,
        CancellationToken cancellationToken)
    {
        var jsonLineOptions = new JsonSerializerOptions(_jsonOptions)
        {
            WriteIndented = false,
        };
        var json = _redactor.Redact(
            JsonSerializer.Serialize(value, jsonLineOptions)) + Environment.NewLine;
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(
                Path.Combine(RunDirectory, relativePath),
                json,
                new UTF8Encoding(false),
                cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task WriteJsonAsync(
        string path,
        object value,
        CancellationToken cancellationToken)
    {
        var json = _redactor.Redact(
            JsonSerializer.Serialize(value, _jsonOptions));
        var temporaryPath = $"{path}.tmp-{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(
            temporaryPath,
            json,
            new UTF8Encoding(false),
            cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return string.Join(
            ' ',
            value.Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed class StepAssetWriter
{
    private readonly RunAssetSession _session;
    private readonly string _absoluteDirectory;

    internal StepAssetWriter(
        RunAssetSession session,
        int stepNumber,
        string pageFingerprint,
        string relativeDirectory,
        string absoluteDirectory)
    {
        _session = session;
        StepNumber = stepNumber;
        PageFingerprint = pageFingerprint;
        RelativeDirectory = relativeDirectory.Replace(Path.DirectorySeparatorChar, '/');
        _absoluteDirectory = absoluteDirectory;
    }

    public int StepNumber { get; }

    public string PageFingerprint { get; }

    public string RelativeDirectory { get; }

    public async Task WriteBeforeAsync(
        ReadOnlyMemory<byte> screenshot,
        string uiXml,
        CancellationToken cancellationToken = default)
    {
        if (screenshot.IsEmpty)
            throw new ArgumentException("Before screenshot must not be empty.", nameof(screenshot));
        await _session.WriteStepBytesAsync(
            Path.Combine(_absoluteDirectory, "before.png"),
            screenshot,
            cancellationToken);
        await _session.WriteStepTextAsync(
            Path.Combine(_absoluteDirectory, "before.xml"),
            uiXml,
            cancellationToken);
    }

    public async Task WriteAfterAsync(
        ReadOnlyMemory<byte> screenshot,
        string uiXml,
        CancellationToken cancellationToken = default)
    {
        if (screenshot.IsEmpty)
            throw new ArgumentException("After screenshot must not be empty.", nameof(screenshot));
        await _session.WriteStepBytesAsync(
            Path.Combine(_absoluteDirectory, "after.png"),
            screenshot,
            cancellationToken);
        await _session.WriteStepTextAsync(
            Path.Combine(_absoluteDirectory, "after.xml"),
            uiXml,
            cancellationToken);
    }

    public Task WriteAnalysisAsync<T>(
        T? analysis,
        string status,
        string? missingReason = null,
        CancellationToken cancellationToken = default) =>
        WriteEnvelopeAsync(
            "analysis.json",
            "analysis",
            status,
            analysis,
            missingReason,
            cancellationToken);

    public Task WriteStepPlanAsync<T>(
        T? plan,
        string status,
        string? missingReason = null,
        CancellationToken cancellationToken = default) =>
        WriteEnvelopeAsync(
            "step-plan.json",
            "planning",
            status,
            plan,
            missingReason,
            cancellationToken);

    public Task WriteVerificationAsync<T>(
        T? verification,
        string status,
        string? missingReason = null,
        CancellationToken cancellationToken = default) =>
        WriteEnvelopeAsync(
            "verification.json",
            "verification",
            status,
            verification,
            missingReason,
            cancellationToken);

    private Task WriteEnvelopeAsync<T>(
        string fileName,
        string phase,
        string status,
        T? payload,
        string? missingReason,
        CancellationToken cancellationToken)
    {
        if (payload is null && string.IsNullOrWhiteSpace(missingReason))
            throw new ArgumentException("Missing phases require an explicit reason.", nameof(missingReason));
        var envelope = new StepEnvelope<T>(
            RunAssetVocabulary.SchemaVersion,
            _session.Manifest.RunId,
            StepNumber,
            DateTimeOffset.UtcNow,
            PageFingerprint,
            phase,
            status,
            payload,
            missingReason);
        return _session.WriteStepJsonAsync(
            Path.Combine(_absoluteDirectory, fileName),
            envelope,
            cancellationToken);
    }
}

public static class IterationAggregator
{
    public static IterationAggregate Create(
        string aggregateId,
        IEnumerable<AggregateChildRun> childRuns,
        IEnumerable<string>? previousIssueFingerprints = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentNullException.ThrowIfNull(childRuns);
        var children = childRuns.ToImmutableArray();
        var currentIssues = children
            .SelectMany(child => child.IssueFingerprints)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var previousIssues = previousIssueFingerprints?
            .ToImmutableHashSet(StringComparer.Ordinal)
            ?? ImmutableHashSet<string>.Empty;
        var counts = children
            .SelectMany(child => child.IssueFingerprints)
            .GroupBy(value => value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var phaseLatency = children
            .SelectMany(child => child.PhaseLatencyMs)
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .ToImmutableDictionary(
                group => group.Key,
                group => group.Sum(pair => pair.Value),
                StringComparer.Ordinal);

        var longest = 0;
        var current = 0;
        foreach (var child in children)
        {
            current = child.Status == "success" ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return new IterationAggregate(
            RunAssetVocabulary.SchemaVersion,
            aggregateId,
            children,
            children.IsEmpty
                ? 0
                : children.Count(child => child.Status == "success") / (double)children.Length,
            longest,
            children.Sum(child => child.DurationMs),
            children.Sum(child => child.SafetyAllowed),
            children.Sum(child => child.SafetyDenied),
            phaseLatency,
            [.. currentIssues.Except(previousIssues).OrderBy(value => value, StringComparer.Ordinal)],
            [.. counts.Where(pair => pair.Value > 1).Select(pair => pair.Key).OrderBy(value => value, StringComparer.Ordinal)],
            [.. previousIssues.Except(currentIssues).OrderBy(value => value, StringComparer.Ordinal)],
            DateTimeOffset.UtcNow);
    }
}
