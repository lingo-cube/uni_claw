using UniClaw.Kernel.Runtime;

namespace UniClaw.Kernel.Control;

/// <summary>Control-owned bounded traversal cursor. It keeps obligations and path
/// state only; World and Run remain the owners of facts and execution records.</summary>
internal sealed class TraversalControl
{
    internal sealed record Work(string InvocationId, TraversalOperation Operation, TraversalCandidate? Candidate,
        string ContainerId, string RevisionId, bool Return, string? ExpectedReturnContainerId = null);

    private sealed class Frame(string containerId, int depth)
    {
        public string ContainerId { get; } = containerId;
        public int Depth { get; } = depth;
        public HashSet<string> Completed = new(StringComparer.Ordinal);
        public bool DiscoveryGapRecorded;
    }

    private readonly Stack<Frame> _path = new();
    private readonly Dictionary<string, int> _maxExpandedDepth = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activePath = new(StringComparer.Ordinal);
    private readonly List<TraversalGap> _gaps = new();
    private readonly List<string> _completed = new();
    private readonly List<PlanHandlerRecord> _handlers = new();
    private readonly List<TraversalOperationRecord> _operationRecords = new();
    private TraversalOperation? _returnOperation;
    private string? _expectedReturnContainerId;
    private string? _currentContainerId;
    private bool _returning;
    private bool _finished;
    private bool _discoveryComplete;
    private int _operations;
    private int _work;
    private int _invocationOrdinal;
    private string? _pause;
    private string? _end;
    private PlanExecutionStatus _status = PlanExecutionStatus.Active;
    private string _planId = string.Empty;
    private int _version;

    public void Start(string planId, int version, string root)
    {
        _path.Clear();
        _maxExpandedDepth.Clear();
        _activePath.Clear();
        _gaps.Clear();
        _completed.Clear();
        _handlers.Clear();
        _operationRecords.Clear();
        _returnOperation = null;
        _expectedReturnContainerId = null;
        _currentContainerId = root;
        _returning = false;
        _finished = false;
        _discoveryComplete = true;
        _operations = 0;
        _work = 0;
        _pause = null;
        _end = null;
        _status = PlanExecutionStatus.Active;
        _planId = planId;
        _version = version;
        _path.Push(new Frame(root, 0));
        _activePath.Add(root);
    }

    public int Depth => _path.Count == 0 ? 0 : _path.Peek().Depth;
    public int WorkUsed => _work;
    public bool Finished => _finished;
    public bool Paused => _status == PlanExecutionStatus.Paused;
    public void Spend() => _work++;

    public Work? Select(TraversalObservation observation, int maxDepth)
    {
        if (_finished || _status == PlanExecutionStatus.Paused || _path.Count == 0) return null;
        var frame = _path.Peek();
        _currentContainerId = observation.Graph.CurrentContainerId;
        _discoveryComplete &= observation.DiscoveryComplete;
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
                if (frame.Depth >= maxDepth && rule.Operation.Kind == TraversalOperationKind.Navigate)
                {
                    frame.Completed.Add(obligation);
                    Gap(frame.ContainerId, candidate.Target.Key, "max-depth", candidate.Target.EvidenceRefs);
                    break;
                }
                frame.Completed.Add(obligation);
                return NewWork(rule.Operation, candidate, frame.ContainerId,
                    observation.Graph.SourceRevisionId, false);
            }
            if (!matched)
            {
                frame.Completed.Add($"unmatched|{candidate.Target.Key}");
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
            _end = _gaps.Count == 0 ? "completed" : "partial";
            return null;
        }
        _expectedReturnContainerId = _path.Skip(1).First().ContainerId;
        _returning = true;
        return null;
    }

    private Work NewWork(TraversalOperation operation, TraversalCandidate? candidate,
        string container, string revision, bool returning, string? expectedReturnContainer = null) => new(
            $"{_planId}:{_version}:traversal-{++_invocationOrdinal}", operation, candidate,
            container, revision, returning, expectedReturnContainer);

    public bool Returned(Work work, string? currentContainerId)
    {
        var expected = work.ExpectedReturnContainerId;
        if (expected is null || currentContainerId != expected)
        {
            Gap(expected ?? work.ContainerId, null, "return-target-not-current", Array.Empty<string>());
            Pause("traversal:return-failed");
            return false;
        }
        _operations++;
        CompleteOperation(work.InvocationId);
        _completed.Add($"return:{work.ContainerId}->{expected}");
        if (_path.Peek().ContainerId != expected)
        {
            var child = _path.Pop();
            _activePath.Remove(child.ContainerId);
        }
        _currentContainerId = currentContainerId;
        _expectedReturnContainerId = null;
        return true;
    }

    private IReadOnlyList<TraversalRule>? _rules;
    public void ConfigureRules(IReadOnlyList<TraversalRule> rules) => _rules = rules;

    public void Navigated(Work work, string targetContainerId, TraversalOperation returnOperation)
    {
        _operations++;
        CompleteOperation(work.InvocationId);
        _completed.Add($"{work.ContainerId}->{targetContainerId}:{work.Operation.OperationId}");
        _currentContainerId = targetContainerId;
        _returnOperation = returnOperation;
        if (_activePath.Contains(targetContainerId))
        {
            Gap(work.ContainerId, work.Candidate?.Target.Key, "navigation-cycle", work.Candidate?.Target.EvidenceRefs ?? Array.Empty<string>());
            return;
        }
        var nextDepth = Depth + 1;
        if (_maxExpandedDepth.TryGetValue(targetContainerId, out var prior) && prior <= nextDepth)
        {
            _expectedReturnContainerId = work.ContainerId;
            _returning = true;
            return;
        }
        _maxExpandedDepth[targetContainerId] = nextDepth;
        _path.Push(new Frame(targetContainerId, nextDepth));
        _activePath.Add(targetContainerId);
    }

    public void ReadOnlyCompleted(Work work)
    {
        _operations++;
        CompleteOperation(work.InvocationId);
        _completed.Add($"{work.ContainerId}:{work.Operation.OperationId}");
    }
    public void RecordOperation(TraversalOperationRecord record) => _operationRecords.Add(record);
    public void CompleteOperation(string invocationId)
    {
        var index = _operationRecords.FindLastIndex(record => record.OperationId == invocationId);
        if (index >= 0) _operationRecords[index] = _operationRecords[index] with { Completed = true };
    }
    public void Handler(PlanHandlerRecord record) => _handlers.Add(record);
    public void Pause(string reason) { _status = PlanExecutionStatus.Paused; _pause = reason; }
    public void Resume() { if (!_finished) { _status = PlanExecutionStatus.Active; _pause = null; } }
    public void Abandon(string reason) { _status = PlanExecutionStatus.Abandoned; _finished = true; _end = reason; }
    public void Fail(string reason) { Gap(_path.Count == 0 ? string.Empty : _path.Peek().ContainerId, null, reason, Array.Empty<string>()); Pause(reason); }
    public void Gap(string container, string? candidate, string reason, IReadOnlyList<string> evidence) =>
        _gaps.Add(new TraversalGap(container, candidate, reason, evidence.ToArray()));

    public TraversalExecutionView View() => new(_planId, _version, _status,
        _currentContainerId, Depth, _operations, _work,
        _pause, _handlers.ToArray(), _gaps.ToArray(), _completed.ToArray(),
        _discoveryComplete, _end, _operationRecords.ToArray());
}
