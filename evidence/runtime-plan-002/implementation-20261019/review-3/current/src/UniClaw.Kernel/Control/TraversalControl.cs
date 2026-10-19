using UniClaw.Kernel.Runtime;

namespace UniClaw.Kernel.Control;

/// <summary>Control-owned bounded traversal cursor. It keeps obligations and path
/// state only; World and Run remain the owners of facts and execution records.</summary>
internal sealed class TraversalControl
{
    internal sealed record Work(string InvocationId, TraversalOperation Operation, TraversalCandidate? Candidate,
        string ContainerId, string RevisionId, bool Return, string? ExpectedReturnContainerId = null,
        string? RuleId = null);

    private sealed class Frame(string containerId, int depth)
    {
        public string ContainerId { get; } = containerId;
        public int Depth { get; } = depth;
        public HashSet<string> Completed = new(StringComparer.Ordinal);
        public HashSet<string> ObservedCandidates = new(StringComparer.Ordinal);
        public HashSet<string> AssignedCandidates = new(StringComparer.Ordinal);
        public bool DiscoveryGapRecorded;
        public bool? DiscoveryComplete;
    }

    private readonly Stack<Frame> _path = new();
    private readonly Dictionary<string, int> _expandedNavigationDepth = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activePath = new(StringComparer.Ordinal);
    private readonly Queue<TraversalProgressEvent> _progressEvents = new();
    private readonly HashSet<(string Container, string? Candidate, string Reason)> _reportedGaps = new();
    private readonly Dictionary<string, bool> _scopeDiscovery = new(StringComparer.Ordinal);
    private TraversalOperation? _returnOperation;
    private string? _expectedReturnContainerId;
    private string? _currentContainerId;
    private bool _returning;
    private bool _finished;
    private bool _discoveryComplete;
    private int _invocationOrdinal;
    private string? _pause;
    private string? _end;
    private PlanExecutionStatus _status = PlanExecutionStatus.Active;
    private string _planId = string.Empty;
    private int _version;

    public void Start(string planId, int version, string root)
    {
        _path.Clear();
        _expandedNavigationDepth.Clear();
        _activePath.Clear();
        _progressEvents.Clear();
        _reportedGaps.Clear();
        _scopeDiscovery.Clear();
        _returnOperation = null;
        _expectedReturnContainerId = null;
        _currentContainerId = root;
        _returning = false;
        _finished = false;
        _discoveryComplete = false;
        _pause = null;
        _end = null;
        _status = PlanExecutionStatus.Active;
        _planId = planId;
        _version = version;
        _path.Push(new Frame(root, 0));
        _activePath.Add(root);
    }

    public int Depth => _path.Count == 0 ? 0 : _path.Peek().Depth;
    public bool Finished => _finished;
    public bool Paused => _status == PlanExecutionStatus.Paused;
    public bool HasObservedCurrentFrontier => _finished || (_path.Count > 0 && _path.Peek().DiscoveryComplete.HasValue);
    public int KnownPendingWork => _path.Sum(frame => frame.ObservedCandidates.Count -
        frame.ObservedCandidates.Count(frame.AssignedCandidates.Contains));

    public Work? Select(TraversalObservation observation, int maxDepth)
    {
        if (_finished || _status == PlanExecutionStatus.Paused || _path.Count == 0) return null;
        var frame = _path.Peek();
        _currentContainerId = observation.Graph.CurrentContainerId;
        if (!_returning)
        {
            foreach (var candidate in observation.Candidates)
                frame.ObservedCandidates.Add(candidate.Target.Key);
            frame.DiscoveryComplete = observation.DiscoveryComplete;
            _scopeDiscovery[frame.ContainerId] = observation.DiscoveryComplete;
            _discoveryComplete = _scopeDiscovery.Count > 0 && _scopeDiscovery.Values.All(value => value);
        }
        if (_returning)
        {
            _returning = false;
            return NewWork(_returnOperation!, null, observation.Graph.CurrentContainerId!,
                observation.Graph.SourceRevisionId, true, _expectedReturnContainerId);
        }

        foreach (var candidate in observation.Candidates)
        {
            if (frame.Completed.Contains($"blocked|{candidate.Target.Key}")) continue;
            if (frame.Completed.Contains($"unmatched|{candidate.Target.Key}")) continue;
            if (candidate.Target.Disposition == PlanCandidateDisposition.Unknown)
            {
                Gap(frame.ContainerId, candidate.Target.Key, "candidate-unknown", candidate.Target.EvidenceRefs);
                Pause($"traversal:candidate-unknown:{candidate.Target.Key}");
                return null;
            }
            if (candidate.Target.Disposition == PlanCandidateDisposition.Blocked)
            {
                frame.Completed.Add($"blocked|{candidate.Target.Key}");
                frame.AssignedCandidates.Add(candidate.Target.Key);
                Gap(frame.ContainerId, candidate.Target.Key,
                    candidate.Target.Reason ?? "candidate-blocked", candidate.Target.EvidenceRefs);
                continue;
            }
            var rules = _rules!;
            var matched = false;
            foreach (var rule in rules)
            {
                var truth = rule.Condition == "always" ? PolicyTruth.Satisfied
                    : candidate.Conditions.TryGetValue(rule.Condition, out var value) ? value : PolicyTruth.Unknown;
                if (truth == PolicyTruth.Unknown)
                {
                    Gap(frame.ContainerId, candidate.Target.Key, $"condition-unknown:{rule.Condition}", candidate.Target.EvidenceRefs);
                    Pause($"traversal:condition-unknown:{rule.Condition}");
                    return null;
                }
                if (truth != PolicyTruth.Satisfied) continue;
                matched = true;
                var obligation = $"{frame.ContainerId}|{rule.RuleId}|{candidate.Target.Key}|{rule.Operation.OperationId}";
                if (frame.Completed.Contains(obligation)) break;
                var navigationObligation = NavigationObligation(frame.ContainerId, rule.RuleId,
                    candidate.Target.Key, rule.Operation.OperationId);
                if (rule.Operation.Kind == TraversalOperationKind.Navigate
                    && _expandedNavigationDepth.TryGetValue(navigationObligation, out var expandedDepth)
                    && expandedDepth <= frame.Depth)
                {
                    frame.Completed.Add(obligation);
                    frame.AssignedCandidates.Add(candidate.Target.Key);
                    break;
                }
                if (frame.Depth >= maxDepth && rule.Operation.Kind == TraversalOperationKind.Navigate)
                {
                    frame.Completed.Add(obligation);
                    frame.AssignedCandidates.Add(candidate.Target.Key);
                    Gap(frame.ContainerId, candidate.Target.Key, "max-depth", candidate.Target.EvidenceRefs);
                    break;
                }
                frame.Completed.Add(obligation);
                frame.AssignedCandidates.Add(candidate.Target.Key);
                return NewWork(rule.Operation, candidate, frame.ContainerId,
                    observation.Graph.SourceRevisionId, false, ruleId: rule.RuleId);
            }
            if (!matched)
            {
                frame.Completed.Add($"unmatched|{candidate.Target.Key}");
                frame.AssignedCandidates.Add(candidate.Target.Key);
                Gap(frame.ContainerId, candidate.Target.Key, "no-rule-match", candidate.Target.EvidenceRefs);
            }
        }

        if (!observation.DiscoveryComplete && !frame.DiscoveryGapRecorded)
        {
            Gap(frame.ContainerId, null, "discovery-incomplete", observation.DiscoveryEvidenceRefs);
            frame.DiscoveryGapRecorded = true;
        }

        if (_path.Count == 1)
        {
            _finished = true;
            _status = PlanExecutionStatus.Completed;
            _end = "completed";
            _discoveryComplete = _scopeDiscovery.Count > 0 && _scopeDiscovery.Values.All(value => value);
            return null;
        }
        _expectedReturnContainerId = _path.Skip(1).First().ContainerId;
        _returning = true;
        return null;
    }

    private Work NewWork(TraversalOperation operation, TraversalCandidate? candidate,
        string container, string revision, bool returning, string? expectedReturnContainer = null,
        string? ruleId = null) => new(
            $"{_planId}:{_version}:traversal-{++_invocationOrdinal}", operation, candidate,
            container, revision, returning, expectedReturnContainer, ruleId);

    private static string NavigationObligation(string source, string rule, string candidate, string operation) =>
        $"{source}|{rule}|{candidate}|{operation}";

    public bool Returned(Work work, string? currentContainerId)
    {
        if (currentContainerId is not null) _currentContainerId = currentContainerId;
        var expected = work.ExpectedReturnContainerId;
        if (expected is null || currentContainerId != expected)
        {
            Gap(expected ?? work.ContainerId, null, "return-target-not-current", Array.Empty<string>());
            Pause("traversal:return-failed");
            return false;
        }
        Completed(work.InvocationId, $"return:{work.ContainerId}->{expected}");
        if (_path.Peek().ContainerId != expected)
        {
            var child = _path.Pop();
            _activePath.Remove(child.ContainerId);
        }
        _expectedReturnContainerId = null;
        return true;
    }

    private IReadOnlyList<TraversalRule>? _rules;
    public void ConfigureRules(IReadOnlyList<TraversalRule> rules) => _rules = rules;

    public void Navigated(Work work, string targetContainerId, TraversalOperation returnOperation)
    {
        Completed(work.InvocationId, $"{work.ContainerId}->{targetContainerId}:{work.Operation.OperationId}");
        _currentContainerId = targetContainerId;
        _returnOperation = returnOperation;
        if (_activePath.Contains(targetContainerId))
        {
            while (_path.Count > 0 && _path.Peek().ContainerId != targetContainerId)
            {
                var abandoned = _path.Pop();
                _activePath.Remove(abandoned.ContainerId);
            }
            _returning = false;
            _expectedReturnContainerId = null;
            Gap(work.ContainerId, work.Candidate?.Target.Key, "navigation-cycle", work.Candidate?.Target.EvidenceRefs ?? Array.Empty<string>());
            Pause("traversal:navigation-cycle");
            return;
        }
        if (work.Candidate is { } candidate && work.RuleId is { } ruleId)
        {
            var key = NavigationObligation(work.ContainerId, ruleId, candidate.Target.Key,
                work.Operation.OperationId);
            if (!_expandedNavigationDepth.TryGetValue(key, out var prior) || Depth < prior)
                _expandedNavigationDepth[key] = Depth;
        }
        var nextDepth = Depth + 1;
        _discoveryComplete = false;
        _path.Push(new Frame(targetContainerId, nextDepth));
        _activePath.Add(targetContainerId);
    }

    public void ReadOnlyCompleted(Work work)
    {
        Completed(work.InvocationId, $"{work.ContainerId}:{work.Operation.OperationId}");
    }
    private void Completed(string operationId, string work) =>
        _progressEvents.Enqueue(new TraversalProgressEvent.Completed(operationId, work));
    public void Pause(string reason) { _status = PlanExecutionStatus.Paused; _pause = reason; }
    public void Resume() { if (!_finished) { _status = PlanExecutionStatus.Active; _pause = null; } }
    public void Abandon(string reason) { _status = PlanExecutionStatus.Abandoned; _finished = true; _end = reason; }
    public void Fail(string reason) { Gap(_path.Count == 0 ? string.Empty : _path.Peek().ContainerId, null, reason, Array.Empty<string>()); Pause(reason); }
    public void Gap(string container, string? candidate, string reason, IReadOnlyList<string> evidence)
    {
        if (_reportedGaps.Add((container, candidate, reason)))
            _progressEvents.Enqueue(new TraversalProgressEvent.Gap(
                new TraversalGap(container, candidate, reason, evidence.ToArray())));
    }

    public IReadOnlyList<TraversalProgressEvent> DrainProgressEvents()
    {
        var events = _progressEvents.ToArray();
        _progressEvents.Clear();
        return events;
    }

    public TraversalExecutionView View() => new(_planId, _version, _status,
        _currentContainerId, Depth, 0, 0,
        _pause, Array.Empty<PlanHandlerRecord>(), Array.Empty<TraversalGap>(), Array.Empty<string>(),
        _discoveryComplete, _end, Array.Empty<TraversalOperationRecord>(),
        _finished ? 0 : HasObservedCurrentFrontier ? KnownPendingWork : null);
}
