using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Planning;

/// <summary>
/// Run-local, method-local execution hypothesis ledger. It is a transient derivation,
/// NOT Runtime state: it is created per run inside a Planning entry point, holds the
/// current <see cref="ExecutionHypothesis"/> and an immutable history of the hypothesis
/// sequence, and is discarded when the run method returns. It is never assigned to an
/// Agent / Container / Traversal / Environment field.
/// <para>
/// The ledger holds NO authority. It cannot authorize, decide, complete, or execute:
/// it only records assumptions and lifecycle transitions derived from the Agent's trace
/// evidence and run outcome. The RuntimeAgent keeps sole run-level authority; the DFS
/// engine is unchanged.
/// </para>
/// </summary>
public sealed class ExecutionHypothesisLedger
{
    private ImmutableList<ExecutionHypothesis> _history =
        ImmutableList<ExecutionHypothesis>.Empty;

    private ExecutionHypothesis _current;

    // Reference to the trace consumed by ReviseFromEvidence (decision Reconcile input).
    // Method-local — the ledger is discarded when the run method returns; it is never
    // assigned to an Agent/Container/Traversal/Environment field.
    private IReadOnlyList<TraceEvent>? _trace;

    private RuntimeDecision? _latestDecision;

    // The most recent decision-driven hypothesis adaptation (null until Adapt is
    // called). Run-local: a fresh ledger starts with a null LatestAdaptation and is
    // discarded when the run method returns.
    private HypothesisAdaptation? _latestAdaptation;

    /// <summary>
    /// Creates a ledger from a resolved decomposition and run identity, seeding the
    /// initial hypothesis (Status <see cref="ExecutionHypothesisStatus.Created"/>) from
    /// the directive's declared scope, maximum depth, and completion requirement — never
    /// from scenario knowledge.
    /// </summary>
    public ExecutionHypothesisLedger(DirectiveDecompositionResult.Resolved resolved, string runId)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        var specification = resolved.Specification;
        // Generic, type-directed derivation from the directive's declared boundaries —
        // no scenario strings, no element coordinates, no plan.
        var directiveReference =
            $"{specification.Scope.ApplicationIdentity}/{specification.Scope.SemanticRoot}";
        var objective = "Explore declared scope within bounded depth";
        var expectedTransition = "Discover -> Authorize -> Expand";
        var expectedOutcome = "Exhaustive coverage within declared scope";

        _current = new ExecutionHypothesis(
            runId: runId,
            directiveReference: directiveReference,
            objective: objective,
            expectedTransition: expectedTransition,
            expectedOutcome: expectedOutcome,
            confidence: 1f,
            revisionReason: null,
            createdAtObservation: null,
            status: ExecutionHypothesisStatus.Created);
        _history = _history.Add(_current);
    }

    /// <summary>The latest hypothesis in the sequence.</summary>
    public ExecutionHypothesis Current => _current;

    /// <summary>Immutable snapshot of the full hypothesis sequence in creation order.</summary>
    public IReadOnlyList<ExecutionHypothesis> History => _history;

    /// <summary>
    /// Marks execution as begun under the current hypothesis (Status → Active). The
    /// Created → Active transition is appended to the history as a new snapshot.
    /// </summary>
    public void Activate()
    {
        if (_current.Status != ExecutionHypothesisStatus.Created)
            return;
        Append(_current with { Status = ExecutionHypothesisStatus.Active });
    }

    /// <summary>
    /// Revises the hypothesis sequence from the Agent's trace evidence + run outcome.
    /// Trace inflection points are mapped to lifecycle transitions:
    /// <list type="bullet">
    /// <item>an external boundary observation contradicts the in-scope expectation → the
    /// Active hypothesis becomes <see cref="ExecutionHypothesisStatus.Revised"/> with the boundary reason;</item>
    /// <item>a continuation past the boundary (verified return / returned-to-parent / sibling
    /// inventory) supersedes a revised hypothesis → it becomes <see cref="ExecutionHypothesisStatus.Replaced"/>
    /// and a new "continue siblings" hypothesis (Status Created) is appended;</item>
    /// <item>matching in-scope observations (inventory complete / verified return without a
    /// prior contradiction) confirm the hypothesis → <see cref="ExecutionHypothesisStatus.Confirmed"/>.</item>
    /// </list>
    /// The final status is derived from the run outcome.
    /// </summary>
    /// <remarks>Trace-derived (post-run), evidence-driven; the DFS loop is never modified.</remarks>
    public void ReviseFromEvidence(IReadOnlyList<TraceEvent> trace, RunState outcome)
    {
        ArgumentNullException.ThrowIfNull(trace);
        _trace = trace;

        bool boundaryObserved = false;
        bool continuedPastBoundary = false;

        foreach (var entry in trace)
        {
            var reason = entry.Reason;
            if (string.IsNullOrWhiteSpace(reason))
                continue;

            if (reason.Contains("EXTERNAL_BOUNDARY_OBSERVED", StringComparison.Ordinal)
                && !boundaryObserved)
            {
                boundaryObserved = true;
                // An external-boundary observation contradicts the in-scope expansion
                // expectation — even a hypothesis already Confirmed by matching
                // inventory evidence is Revised (a boundary is a fresh contradiction).
                if (_current.Status is not (ExecutionHypothesisStatus.Replaced
                    or ExecutionHypothesisStatus.Revised))
                {
                    var revised = _current with
                    {
                        Status = ExecutionHypothesisStatus.Revised,
                        RevisionReason = reason,
                        Confidence = _current.Confidence * 0.5f,
                    };
                    Append(revised);
                }
                continue;
            }

            var isBoundaryReturn = reason.Contains("EXTERNAL_BOUNDARY_RETURNED_TO_PARENT", StringComparison.Ordinal)
                || reason.Contains("verified parent return", StringComparison.Ordinal);

            if (boundaryObserved && !continuedPastBoundary && isBoundaryReturn)
            {
                continuedPastBoundary = true;
                // The revised hypothesis is superseded: it becomes Replaced, and a new
                // "continue siblings" hypothesis (Status Created) begins the next phase.
                if (_current.Status == ExecutionHypothesisStatus.Revised)
                {
                    Append(_current with { Status = ExecutionHypothesisStatus.Replaced });
                    var siblingHypothesis = _current with
                    {
                        Status = ExecutionHypothesisStatus.Created,
                        Objective = "Continue remaining siblings within declared scope",
                        ExpectedTransition = "Expand remaining in-scope branches",
                        ExpectedOutcome = "Exhaustive coverage within declared scope",
                        RevisionReason = null,
                        Confidence = 1f,
                    };
                    Append(siblingHypothesis);
                }
                continue;
            }

            var isInventoryComplete = reason.Contains("open-world container inventory complete", StringComparison.Ordinal)
                || reason.Contains("open-world branch inventory complete", StringComparison.Ordinal)
                || reason.Contains("open-world branch inventory bounded-leaf", StringComparison.Ordinal);

            if (isInventoryComplete
                && _current.Status is ExecutionHypothesisStatus.Active
                    or ExecutionHypothesisStatus.Created)
            {
                Append(_current with { Status = ExecutionHypothesisStatus.Confirmed });
                continue;
            }
        }

        ApplyOutcome(outcome);
    }

    /// <summary>
    /// The most recent <see cref="RuntimeDecision"/> produced by <see cref="Reconcile"/>,
    /// or null until Reconcile is called. Run-local: a fresh ledger starts with a null
    /// LatestDecision and is discarded when the run method returns.
    /// </summary>
    public RuntimeDecision? LatestDecision => _latestDecision;

    /// <summary>
    /// Reconciles the current hypothesis against the observed world and the stored trace
    /// reference, delegating to the stateless <see cref="HypothesisReconciler"/>. Stores
    /// the produced <see cref="RuntimeDecision"/> in <see cref="LatestDecision"/> and
    /// returns it. The belief is optional (may be null when no WorldBelief is available).
    /// This records a decision state; it never performs, authorizes, or executes the decision.
    /// </summary>
    public RuntimeDecision Reconcile(WorldBelief? belief)
    {
        var decision = HypothesisReconciler.Reconcile(
            _current,
            belief,
            _trace ?? Array.Empty<TraceEvent>());
        _latestDecision = decision;
        return decision;
    }

    /// <summary>
    /// The most recent <see cref="HypothesisAdaptation"/> produced by <see cref="Adapt"/>,
    /// or null until Adapt is called. Run-local: a fresh ledger starts with a null
    /// LatestAdaptation and is discarded when the run method returns.
    /// </summary>
    public HypothesisAdaptation? LatestAdaptation => _latestAdaptation;

    /// <summary>
    /// Applies the latest <see cref="RuntimeDecision"/> (see <see cref="LatestDecision"/>)
    /// to the current hypothesis, delegating to the stateless pure
    /// <see cref="HypothesisAdapter"/>. The adapted hypothesis becomes the new current
    /// hypothesis and is appended to the immutable history — history is NEVER rewritten
    /// or deleted. For a <see cref="HypothesisAdaptationType.Replace"/> adaptation the
    /// superseded current hypothesis is first recorded as
    /// <see cref="ExecutionHypothesisStatus.Replaced"/> (the same append-only replacement
    /// pattern as <see cref="ReviseFromEvidence"/>) so the full sequence
    /// (initial → revised → replaced → adapted) remains observable. The adaptation is
    /// stored in <see cref="LatestAdaptation"/> and returned.
    /// <para>
    /// This records a bounded hypothesis modification; it never authorizes, decides,
    /// executes, recovers, or dispatches anything. Replace records a boundary-aware
    /// objective (the DFS loop already handled the boundary); Escalate records inability.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Reconcile"/> has not been
    /// called, so there is no decision to adapt.</exception>
    public HypothesisAdaptation Adapt()
    {
        if (_latestDecision is null)
            throw new InvalidOperationException(
                "No RuntimeDecision to adapt; call Reconcile before Adapt.");

        var adaptation = HypothesisAdapter.Adapt(_latestDecision, _current);

        // A Replace adaptation supersedes the current hypothesis: record it as Replaced
        // before applying the boundary-aware replacement (append-only).
        if (adaptation.AdaptationType == HypothesisAdaptationType.Replace
            && _current.Status != ExecutionHypothesisStatus.Replaced)
        {
            Append(_current with { Status = ExecutionHypothesisStatus.Replaced });
        }

        Append(adaptation.AdaptedHypothesis);
        _latestAdaptation = adaptation;
        return adaptation;
    }

    /// <summary>
    /// Derives the final hypothesis status from the run outcome. A Completed outcome
    /// Confirms the terminal hypothesis; a non-completing (Failed) outcome leaves the
    /// recorded revision (never fabricates completion). If the trace already derived the
    /// terminal status, no duplicate snapshot is appended. The completion check is
    /// delegated to <see cref="ExecutionHypothesis.Completes"/> so RunState member access
    /// stays within the Model/Agent boundary (I-2).
    /// </summary>
    private void ApplyOutcome(RunState outcome)
    {
        if (ExecutionHypothesis.Completes(outcome))
        {
            if (_current.Status is ExecutionHypothesisStatus.Active
                or ExecutionHypothesisStatus.Created)
            {
                Append(_current with { Status = ExecutionHypothesisStatus.Confirmed });
            }
        }
        else
        {
            if (_current.Status == ExecutionHypothesisStatus.Active)
            {
                // A non-completing run did not observe its expected outcome; record the
                // revision without a scenario string (the trace carries the reason).
                Append(_current with
                {
                    Status = ExecutionHypothesisStatus.Revised,
                    RevisionReason = "Run terminated before the expected outcome was observed",
                });
            }
        }
    }

    private void Append(ExecutionHypothesis hypothesis)
    {
        _current = hypothesis;
        _history = _history.Add(hypothesis);
    }
}
