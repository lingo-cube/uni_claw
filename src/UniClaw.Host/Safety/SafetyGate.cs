using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Host.Scenarios;

namespace UniClaw.Host.Safety;

public sealed record class SafetyCandidate(
    string Action,
    string? Target,
    string? Semantic,
    string? PageIdentity,
    string? PagePath,
    string? PackageName,
    double? Confidence,
    bool CoordinatesTrusted,
    bool IsPreparation,
    int Depth,
    int RemainingSteps,
    int RemainingScrolls,
    string RunId,
    int StepNumber,
    string PageFingerprint,
    string Source);

public sealed record class SafetyDecision(
    string SchemaVersion,
    string PolicyId,
    string PolicyVersion,
    string PolicyHash,
    string Disposition,
    string RuleId,
    string Reason,
    string Action,
    string? NormalizedTarget,
    string? Semantic,
    string? PageIdentity,
    string? PagePath,
    double? Confidence,
    string RunId,
    int StepNumber,
    string PageFingerprint,
    string Source,
    DateTimeOffset Timestamp)
{
    [JsonIgnore]
    public bool Allowed => string.Equals(
        Disposition,
        "allow",
        StringComparison.Ordinal);
}

public interface ISafetyEvaluator
{
    SafetyDecision Evaluate(SafetyCandidate candidate);
}

public interface ISafetyDecisionSink
{
    Task RecordAsync(
        SafetyDecision decision,
        CancellationToken cancellationToken = default);
}

public sealed class SafetyDeniedException : Exception
{
    public SafetyDecision Decision { get; }

    public SafetyDeniedException(SafetyDecision decision)
        : base($"Safety gate denied action '{decision.Action}' ({decision.RuleId}).")
    {
        Decision = decision;
    }
}

public sealed class SettingsSafetyEvaluator : ISafetyEvaluator
{
    private const string SchemaVersion = "1";

    private readonly AndroidSettingsScenario _scenario;
    private readonly SettingsSafetyPolicy _policy;
    private readonly string _policyHash;

    public SettingsSafetyEvaluator(ScenarioSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _scenario = snapshot.Scenario;
        _policy = snapshot.Policy;
        _policyHash = snapshot.PolicyHash;
    }

    public SafetyDecision Evaluate(SafetyCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var action = Normalize(candidate.Action);
        var target = action == "input"
            ? candidate.Target is null ? null : "[REDACTED]"
            : NormalizeNullable(candidate.Target);
        var semantic = NormalizeNullable(candidate.Semantic);
        var pageIdentity = candidate.PageIdentity?.Trim();
        var packageName = candidate.PackageName?.Trim();

        if (candidate.RemainingSteps <= 0)
            return Deny(candidate, action, target, "deny.boundary.step_budget", "Step budget is exhausted.");
        if (action == "scroll" && candidate.RemainingScrolls <= 0)
            return Deny(candidate, action, target, "deny.boundary.scroll_budget", "Scroll budget is exhausted.");
        if (candidate.Depth < 0
            || candidate.Depth > Math.Min(_scenario.Boundaries.MaxDepth, _policy.Boundaries.MaxDepth))
        {
            return Deny(candidate, action, target, "deny.boundary.depth", "Candidate depth is outside the configured boundary.");
        }
        if (!string.IsNullOrWhiteSpace(packageName)
            && !_policy.Boundaries.AllowedPackages.Contains(packageName, StringComparer.Ordinal))
        {
            return Deny(candidate, action, target, "deny.boundary.package", "Candidate package is outside the Settings boundary.");
        }
        if (!candidate.IsPreparation
            && (string.IsNullOrWhiteSpace(pageIdentity)
                || !MatchesAllowedPage(pageIdentity)))
        {
            return Deny(candidate, action, target, "deny.boundary.page", "Candidate page is unknown or outside the Settings boundary.");
        }

        if (semantic is not null
            && _policy.DangerousSemantics.Contains(semantic, StringComparer.Ordinal))
        {
            return Deny(candidate, action, target, "deny.dangerous.semantic", $"Dangerous semantic '{semantic}' matched.");
        }
        if (target is not null
            && _policy.DangerousText.Any(term => ContainsToken(target, term)))
        {
            return Deny(candidate, action, target, "deny.dangerous.text", "Target matched configured dangerous text.");
        }

        if (!_scenario.AllowedActions.Contains(action, StringComparer.Ordinal)
            || !_policy.AllowedActions.Contains(action, StringComparer.Ordinal))
        {
            return Deny(candidate, action, target, "deny.allowlist.action", "Action is not present in both scenario and policy allowlists.");
        }

        if (action == "click"
            && (target is null
                || semantic is null
                || !candidate.CoordinatesTrusted
                || candidate.Confidence is null
                || candidate.Confidence < _policy.ConfidenceThresholds.MinimumTarget))
        {
            return Deny(candidate, action, target, "deny.target.untrusted", "Click target identity, coordinates, semantic, or confidence is untrusted.");
        }

        if (candidate.IsPreparation
            && action is "launch" or "wait"
            && string.Equals(packageName, _scenario.AppPackage, StringComparison.Ordinal))
        {
            return Allow(candidate, action, target, "allow.preparation", "Explicit Settings preparation action is allowed.");
        }
        if (action == "back")
            return Allow(candidate, action, target, "allow.back", "Back navigation is allowed within the run boundary.");
        if (action == "scroll")
            return Allow(candidate, action, target, "allow.scroll", "Bounded Settings scrolling is allowed.");
        if (action == "click"
            && semantic is not null
            && _policy.SafeNavigationSemantics.Contains(semantic, StringComparer.Ordinal))
        {
            return Allow(candidate, action, target, "allow.navigation_row", "Trusted Settings navigation row is allowed.");
        }

        return Deny(candidate, action, target, "deny.default", "No explicit safe-navigation rule allowed the candidate.");
    }

    private bool MatchesAllowedPage(string pageIdentity)
    {
        return _scenario.Boundaries.AllowedPages.Any(
                   allowed => pageIdentity.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
               && _policy.Boundaries.AllowedPagePrefixes.Any(
                   allowed => pageIdentity.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));
    }

    private SafetyDecision Allow(
        SafetyCandidate candidate,
        string action,
        string? target,
        string ruleId,
        string reason) =>
        Create(candidate, action, target, "allow", ruleId, reason);

    private SafetyDecision Deny(
        SafetyCandidate candidate,
        string action,
        string? target,
        string ruleId,
        string reason) =>
        Create(candidate, action, target, "deny", ruleId, reason);

    private SafetyDecision Create(
        SafetyCandidate candidate,
        string action,
        string? target,
        string disposition,
        string ruleId,
        string reason) =>
        new(
            SchemaVersion,
            _policy.PolicyId,
            _policy.Version,
            _policyHash,
            disposition,
            ruleId,
            reason,
            action,
            target,
            NormalizeNullable(candidate.Semantic),
            candidate.PageIdentity?.Trim(),
            candidate.PagePath?.Trim(),
            candidate.Confidence,
            candidate.RunId,
            candidate.StepNumber,
            candidate.PageFingerprint,
            candidate.Source,
            DateTimeOffset.UtcNow);

    private static bool ContainsToken(string target, string configuredTerm)
    {
        var term = Normalize(configuredTerm);
        return target.Contains(term, StringComparison.Ordinal);
    }

    private static string Normalize(string value) =>
        string.Join(
            ' ',
            value.Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Normalize(value);
}

public interface ISafetyExecutionContext
{
    SafetyCandidate? Current { get; }

    IDisposable Push(SafetyCandidate candidate);
}

public sealed class SafetyExecutionContext : ISafetyExecutionContext
{
    private readonly AsyncLocal<SafetyCandidate?> _current = new();

    public SafetyCandidate? Current => _current.Value;

    public IDisposable Push(SafetyCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var previous = _current.Value;
        _current.Value = candidate;
        return new Scope(() => _current.Value = previous);
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}

public sealed class SafeActionExecutor : IActionExecutor
{
    private readonly IActionExecutor _inner;
    private readonly ISafetyEvaluator _evaluator;
    private readonly ISafetyDecisionSink _sink;
    private readonly ISafetyExecutionContext _context;

    public SafeActionExecutor(
        IActionExecutor inner,
        ISafetyEvaluator evaluator,
        ISafetyDecisionSink sink,
        ISafetyExecutionContext context)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<bool> TapAsync(
        double x,
        double y,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync("click", token => _inner.TapAsync(x, y, token), cancellationToken);

    public Task<bool> SwipeAsync(
        double startX,
        double startY,
        double endX,
        double endY,
        int durationMs,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "scroll",
            token => _inner.SwipeAsync(startX, startY, endX, endY, durationMs, token),
            cancellationToken);

    public Task<bool> PressBackAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync("back", _inner.PressBackAsync, cancellationToken);

    public Task<bool> InputTextAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync("input", token => _inner.InputTextAsync(text, token), cancellationToken);

    public Task<bool> LongPressAsync(
        double x,
        double y,
        int durationMs,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "long_press",
            token => _inner.LongPressAsync(x, y, durationMs, token),
            cancellationToken);

    public async Task WaitAsync(
        int milliseconds,
        CancellationToken cancellationToken = default)
    {
        var decision = await DecideAsync("wait", cancellationToken);
        if (decision.Allowed)
            await _inner.WaitAsync(milliseconds, cancellationToken);
    }

    public List<ActionRecord> GetHistory() => _inner.GetHistory();

    private async Task<bool> ExecuteAsync(
        string action,
        Func<CancellationToken, Task<bool>> execute,
        CancellationToken cancellationToken)
    {
        var decision = await DecideAsync(action, cancellationToken);
        return decision.Allowed && await execute(cancellationToken);
    }

    private async Task<SafetyDecision> DecideAsync(
        string action,
        CancellationToken cancellationToken)
    {
        var candidate = _context.Current is { } current
            ? current with { Action = action }
            : new SafetyCandidate(
                action,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false,
                0,
                0,
                0,
                "unscoped",
                0,
                "unknown",
                "unscoped");
        var decision = _evaluator.Evaluate(candidate);
        await _sink.RecordAsync(decision, cancellationToken);
        return decision;
    }
}

public sealed class SafeEntryActionDriver : IEntryActionDriver
{
    private readonly IEntryActionDriver _inner;
    private readonly ISafetyEvaluator _evaluator;
    private readonly ISafetyDecisionSink _sink;
    private readonly ISafetyExecutionContext _context;

    public SafeEntryActionDriver(
        IEntryActionDriver inner,
        ISafetyEvaluator evaluator,
        ISafetyDecisionSink sink,
        ISafetyExecutionContext context)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<bool> OpenDeepLinkAsync(
        string target,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync("launch", target, token => _inner.OpenDeepLinkAsync(target, token), cancellationToken);

    public Task<bool> ColdLaunchAsync(
        string targetApp,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync("launch", targetApp, token => _inner.ColdLaunchAsync(targetApp, token), cancellationToken);

    public async Task WaitAsync(
        int milliseconds,
        CancellationToken cancellationToken = default)
    {
        var decision = await DecideAsync("wait", null, cancellationToken);
        if (decision.Allowed)
            await _inner.WaitAsync(milliseconds, cancellationToken);
    }

    public Task<bool> CheckConditionAsync(
        IReadOnlyDictionary<string, object>? waitCondition,
        CancellationToken cancellationToken = default) =>
        _inner.CheckConditionAsync(waitCondition, cancellationToken);

    private async Task<bool> ExecuteAsync(
        string action,
        string target,
        Func<CancellationToken, Task<bool>> execute,
        CancellationToken cancellationToken)
    {
        var decision = await DecideAsync(action, target, cancellationToken);
        return decision.Allowed && await execute(cancellationToken);
    }

    private async Task<SafetyDecision> DecideAsync(
        string action,
        string? target,
        CancellationToken cancellationToken)
    {
        var candidate = _context.Current is { } current
            ? current with { Action = action, Target = target ?? current.Target, IsPreparation = true }
            : new SafetyCandidate(
                action,
                target,
                "settings_home",
                null,
                null,
                null,
                1,
                true,
                true,
                0,
                0,
                0,
                "unscoped",
                0,
                "unknown",
                "entry");
        var decision = _evaluator.Evaluate(candidate);
        await _sink.RecordAsync(decision, cancellationToken);
        return decision;
    }
}

public sealed class InMemorySafetyDecisionSink : ISafetyDecisionSink
{
    private readonly List<SafetyDecision> _decisions = [];
    private readonly object _gate = new();

    public ImmutableArray<SafetyDecision> Decisions
    {
        get
        {
            lock (_gate)
                return [.. _decisions];
        }
    }

    public Task RecordAsync(
        SafetyDecision decision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
            _decisions.Add(decision);
        return Task.CompletedTask;
    }
}

public sealed class SafetyDecisionJournal : ISafetyDecisionSink
{
    private readonly Dictionary<(string RunId, int StepNumber), SafetyDecision>
        _latest = new();
    private readonly object _gate = new();

    public Task RecordAsync(
        SafetyDecision decision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
            _latest[(decision.RunId, decision.StepNumber)] = decision;
        return Task.CompletedTask;
    }

    public SafetyDecision? GetLatest(string runId, int stepNumber)
    {
        lock (_gate)
        {
            return _latest.TryGetValue((runId, stepNumber), out var decision)
                ? decision
                : null;
        }
    }
}

public sealed class TraceSafetyDecisionSink : ISafetyDecisionSink
{
    private readonly ITraceRecorder _traceRecorder;

    public TraceSafetyDecisionSink(ITraceRecorder traceRecorder)
    {
        _traceRecorder = traceRecorder
                         ?? throw new ArgumentNullException(nameof(traceRecorder));
    }

    public Task RecordAsync(
        SafetyDecision decision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["policyId"] = decision.PolicyId,
            ["policyVersion"] = decision.PolicyVersion,
            ["policyHash"] = decision.PolicyHash,
            ["ruleId"] = decision.RuleId,
            ["reason"] = decision.Reason,
            ["pageFingerprint"] = decision.PageFingerprint,
            ["source"] = decision.Source,
        };
        if (decision.NormalizedTarget is not null)
            metadata["normalizedTarget"] = decision.NormalizedTarget;
        if (decision.PageIdentity is not null)
            metadata["pageIdentity"] = decision.PageIdentity;
        if (decision.Confidence is not null)
            metadata["confidence"] = decision.Confidence.Value;

        return _traceRecorder.RecordExecutionAsync(
            new ExecutionRecord(
                Action: $"safety.{decision.Action}",
                Status: decision.Disposition,
                SpanType: decision.Allowed ? SpanType.StateDecision : SpanType.SkipDangerous,
                Context: new TraceContext(
                    StepNumber: decision.StepNumber,
                    TraceId: decision.RunId),
                PageId: decision.PageFingerprint,
                TargetValue: decision.NormalizedTarget,
                Timestamp: decision.Timestamp,
                Metadata: metadata));
    }
}

public sealed class CompositeSafetyDecisionSink(
    params ISafetyDecisionSink[] sinks) : ISafetyDecisionSink
{
    private readonly ImmutableArray<ISafetyDecisionSink> _sinks =
        [.. sinks ?? throw new ArgumentNullException(nameof(sinks))];

    public async Task RecordAsync(
        SafetyDecision decision,
        CancellationToken cancellationToken = default)
    {
        foreach (var sink in _sinks)
            await sink.RecordAsync(decision, cancellationToken);
    }
}
