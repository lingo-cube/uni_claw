using System.Collections.Generic;
using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using TraversalJournalEntry = UniClaw.Runtime.Traversal.TraversalJournalEntry;

namespace UniClaw.Runtime.Agent;

public sealed partial class Agent
{
    private enum OpenWorldViewportOutcome
    {
        Exhausted,
        Unresolved,
        Cutoff,
        Transitioned,
    }

    /// <summary>
    /// Run-local FROZEN discovery epoch for one Container: the completeness
    /// evidence, the discovery normalization (the ONLY normalization input for
    /// this inventory generation), and the frozen bounded-revisit budget
    /// (forward-exploration transition count at epoch time). RevisitBudget only
    /// ever decreases; it is never derived from the growing accepted set.
    /// </summary>
    private sealed record DiscoveryEpochState(
        ContainerInventoryCompletenessEvidence Evidence,
        SourceNormalizationResult Normalization,
        int RevisitBudget);

    /// <summary>
    /// U2 opt-in open-world traversal. The Planning seam passes only already-authoritative
    /// primitive/model boundaries; this Agent neither references Planning nor manufactures a
    /// Plan, route, inventory, or completion receipt. Parent continuation is method-local.
    /// </summary>
    internal async Task<RunState> RunOpenWorldAsync(
        Goal goal,
        string applicationIdentity,
        string expectedSemanticEntry,
        int maximumDepth,
        string runId,
        TypeLevelDispatchPolicy? dispatchPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSemanticEntry);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (maximumDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        if (goal.BranchInventoryEvaluator is null || goal.CandidateAuthorizationEvaluator is null)
            throw new ArgumentException("Open-world traversal requires inventory and candidate-authorization criteria.", nameof(goal));
        if (_state != RunState.Idle)
            throw new InvalidOperationException("Agent 已执行过 Run（一个实例恰好对应一次 Run；请新建实例）。");

        _trace.Add(new TraceEvent(runId) { RunState = RunState.Idle });
        _trace.Add(new TraceEvent(runId) { RunState = RunState.Initializing });
        _state = RunState.Initializing;
        var startupResult = await _startup.StartAsync(cancellationToken);
        if (startupResult is StartupResult.NotReady notReady)
            return Fail(runId, notReady.Reason);

        var ready = (StartupResult.Ready)startupResult;
        _recoveryAnchor = ready.Anchor;
        if (!string.Equals(ready.Anchor.ApplicationIdentity, applicationIdentity, StringComparison.Ordinal)
            || !string.Equals(ready.Anchor.ExpectedSemanticEntry, expectedSemanticEntry, StringComparison.Ordinal))
        {
            return Fail(runId, "Open-world specification entry does not match the verified Startup boundary.");
        }

        _trace.Add(new TraceEvent(runId) { RunState = RunState.Running });
        _state = RunState.Running;
        var initial = await _observeInitial(cancellationToken);
        _belief = Reconcile.FromObservation(initial, _resolveSemanticPage);
        if (!string.Equals(_belief.SemanticPage, expectedSemanticEntry, StringComparison.Ordinal))
            return Fail(runId, "Open-world initial Observation does not reconcile to the declared semantic entry.");
        _activeContainer = CreateContainer(expectedSemanticEntry);
        _activeContainer.Bind(initial);
        _trace.Add(new TraceEvent(runId) { ContainerId = expectedSemanticEntry });

        // Execution-local identity safety: no frame type, field, persistent route, or state owner.
        // These sets are scoped to this open-world run and are discarded when the method returns.
        var ancestry = ImmutableHashSet.Create<string>(StringComparer.Ordinal);
        var visited = ImmutableHashSet.Create<string>(StringComparer.Ordinal);
        ancestry = ancestry.Add(expectedSemanticEntry);
        visited = visited.Add(expectedSemanticEntry);
        // CALLER_SOURCE_PROVENANCE_CONTRACT: run-local set of logical sources
        // already claimed by validated branch groundings (PROV-10/PROV-14:
        // duplicate grounding and caller re-assertion are rejected).
        var groundedLogicalSources = new HashSet<string>(StringComparer.Ordinal);
        // COMPLETENESS NON-MONOTONIC EVIDENCE EXTENSION: per-Container FROZEN
        // discovery epoch. Once a Container's first forward exploration proves
        // completeness, its discovery evidence (observations + normalization +
        // proven logical sources) is frozen; later same-Container fresh evidence
        // (parent return / bounded revisit) is validated ONLY for consistency and
        // is never appended to the discovery normalization input. RevisitBudget
        // is frozen at epoch time from the forward-exploration transition count
        // and only ever decremented by bounded backward revisits (run-local,
        // finite; never recomputed from the growing accepted set).
        var discoveryEpoch = new Dictionary<string, DiscoveryEpochState>(StringComparer.Ordinal);
        var parents = new Stack<(RuntimeContainer Parent, string ChildIdentity)>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var container = _activeContainer
                ?? throw new InvalidOperationException("open-world traversal 缺少 active Container。");
            var current = container.CurrentObservation
                ?? throw new InvalidOperationException("open-world traversal Container 缺少当前 Observation。");
            var semanticDepth = parents.Count;

            // Parent completeness path: when deterministic viewport exploration is supplied,
            // require positive exhaustion and Runtime-normalized inventory before trusting caller inventory.
            SourceNormalizationResult? frozenNormalization = null;
            if (goal.ViewportExplorationEvaluator is not null)
            {
                var exploration = await ExploreCurrentContainerViewportsAsync(
                    goal, container, applicationIdentity, runId, cancellationToken);
                if (exploration != OpenWorldViewportOutcome.Exhausted)
                {
                    return Fail(runId,
                        $"Open-world viewport exploration did not prove positive exhaustion (outcome={exploration}). "
                        + "Container inventory completeness cannot be established.");
                }

                // OPEN-WORLD POST-EXPLORATION CURRENT REPAIR: a successful same-
                // Container viewport exploration refreshes the container's
                // CurrentObservation (TryVerifyViewportContinuity /
                // AcceptFreshObservation). The loop's local `current` MUST be
                // reloaded from the container BEFORE completeness / inventory
                // acceptance / branch dispatch / GoalEvidence consume it, so they
                // read the latest accepted evidence, never the pre-exploration
                // Observation. No Bind, no additional AcceptFreshObservation, and
                // no relaxation of the TryAcceptBranchInventory invariant.
                current = container.CurrentObservation
                    ?? throw new InvalidOperationException("open-world post-exploration CurrentObservation lost.");

                if (!discoveryEpoch.TryGetValue(container.SemanticPageName, out var epoch))
                {
                    // ── DISCOVERY EPOCH (first forward exploration) ──
                    // Positive exhaustion -> forward normalization -> unique
                    // logical-source inventory -> completeness. On success the
                    // epoch is FROZEN: the forward ordered-overlap normalizer
                    // consumes ONLY these observations from now on. In a child
                    // traversal context the Agent may contextually resolve the
                    // unique labelled parent-return control (CHILD_AFFORDANCE).
                    var knownParentPage = parents.Count > 0
                        ? parents.Peek().Parent.SemanticPageName
                        : null;
                    if (!TryBuildContainerInventoryCompleteness(
                            container,
                            goal,
                            knownParentPage,
                            out var completeness,
                            out var completenessFailure))
                    {
                        return Fail(runId, completenessFailure!);
                    }
                    var discoveryObservations = container.ViewportExplorationObservations;
                    var epochNormalization = SourceEquivalenceNormalizer.Normalize(discoveryObservations);
                    var withFrozenSources = completeness with
                    {
                        ProvenLogicalSources = PostCompletenessConsistencyValidator.BuildFrozenSources(
                            discoveryObservations, epochNormalization),
                    };
                    epoch = new DiscoveryEpochState(
                        withFrozenSources,
                        epochNormalization,
                        RevisitBudget: discoveryObservations.Length - 1);
                    discoveryEpoch[container.SemanticPageName] = epoch;
                    // Post-completeness acceptance reuses the FROZEN normalization:
                    // the discovery history is never re-normalized with later
                    // (non-monotonic) evidence.
                    frozenNormalization = epoch.Normalization;

                    _trace.Add(new TraceEvent(runId)
                    {
                        ContainerId = container.SemanticPageName,
                        Reason = $"open-world container inventory complete: sources={withFrozenSources.UniqueNavigationSourceIdentities.Length}, "
                            + $"unresolved={withFrozenSources.UnresolvedCandidateCount}, "
                            + $"seq=[{string.Join(",", withFrozenSources.FrozenDiscoveryObservationSequences)}]; "
                            + "discovery epoch FROZEN (post-completeness evidence is consistency-validated only).",
                    });
                }
                else
                {
                    // ── POST-COMPLETENESS CONSISTENCY ──
                    // The current fresh Observation (parent return / bounded
                    // revisit / any same-Container fresh evidence) is validated
                    // against the frozen epoch ONLY — NEVER re-normalized with the
                    // discovery history, NEVER allowed to expand the inventory.
                    // The same-Container continuity verdict is supplied by the
                    // caller path (return/revisit already verified it).
                    var consistency = PostCompletenessConsistencyValidator.Validate(
                        current,
                        epoch.Evidence,
                        continuityVerified: true,
                        BuildParentReturnDisposition(
                            current,
                            parents.Count > 0 ? parents.Peek().Parent.SemanticPageName : null,
                            goal));
                    if (!consistency.Consistent)
                    {
                        return Fail(runId, $"Post-completeness fresh evidence INVALIDATED: {consistency.Reason}");
                    }
                    _trace.Add(new TraceEvent(runId)
                    {
                        ContainerId = container.SemanticPageName,
                        Reason = $"post-completeness consistency PASS (seq={current.SequenceNumber}): {consistency.Reason}",
                    });
                }
            }

            var inventory = goal.BranchInventoryEvaluator(container.ViewportExplorationObservations, semanticDepth)
                ?? throw new InvalidOperationException("BranchInventoryEvaluator 返回 null：必须返回 BranchInventoryEvidence。");
            var outcome = inventory.RequiredBranchEvidence is null ? "unresolved"
                : inventory.RequiredBranchEvidence.Count == 0 ? "bounded-leaf" : "complete";
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = container.SemanticPageName,
                Reason = $"open-world branch inventory {outcome}: depth={semanticDepth}, source-seq={current.SequenceNumber}; {inventory.Reason}",
            });
            // Post-completeness iterations reuse the FROZEN discovery normalization
            // (never re-normalize the growing accepted set with non-monotonic
            // evidence); the first iteration set it at epoch freeze.
            frozenNormalization ??= discoveryEpoch.TryGetValue(container.SemanticPageName, out var existingEpoch)
                ? existingEpoch.Normalization
                : null;
            if (!TryAcceptBranchInventory(container, current, inventory, frozenNormalization, out var progress, out var inventoryFailure))
                return Fail(runId, inventoryFailure!);

            var requiredBranches = inventory.RequiredBranchEvidence
                ?? throw new InvalidOperationException("Accepted inventory must contain required-branch evidence.");
            var pending = progress!.ApprovedSiblingEvidence
                .Where(item => !progress.CompletedSiblingEvidence.ContainsKey(item.Key)
                               && !progress.IsBoundaryVerifiedForSource(item.Key)) // handled boundary: never re-dispatched
                .OrderBy(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .ToArray();
            var subtreeTerminal = requiredBranches.Count == 0 || pending.Length == 0;
            if (subtreeTerminal)
            {
                if (parents.Count == 0)
                {
                    if (requiredBranches.Count == 0)
                        return Fail(runId, "Root bounded inventory is empty; no verified required traversal work supports this U2 execution path.");

                    // VerifiedBoundedTraversalCompletion is derived only at the root, before the existing evaluator.
                    var finalEvidence = goal.EvidenceEvaluator(current);
                    if (finalEvidence.Satisfied)
                        return Complete(runId, finalEvidence);
                    return Fail(runId, $"Verified bounded traversal completion but fresh GoalEvidence remains unsatisfied：{finalEvidence.Reason}");
                }

                var (subtreeReturnState, subtreeReturnedChild) = await TryPerformVerifiedParentReturnAsync(
                    container, current, parents, applicationIdentity, runId, goal, cancellationToken);
                if (subtreeReturnState is not null)
                    return subtreeReturnState.Value;
                if (subtreeReturnedChild is not null)
                    ancestry = ancestry.Remove(subtreeReturnedChild);
                continue;
            }

            if (semanticDepth >= maximumDepth)
            {
                return Fail(runId,
                    $"In-scope inventory requires traversal beyond declared depth={maximumDepth}; bounded cutoff is not exhaustion.");
            }

            // ── BOUNDED SOURCE REVISIT DISPATCH ──
            // Select the FIRST pending branch (deterministic (seq, identity)
            // order) that is: provenance-grounded, CURRENTLY_VISIBLE in the
            // current fresh Observation, and authorization PASS. Explicit path
            // visibility is computed ONLY by fresh logical-source resolution:
            //   pending branch -> grounding -> frozen logical source class;
            //   current fresh Observation -> OccurrencesOf -> resolve each
            //   occurrence against the frozen ProvenLogicalSources;
            // visible iff exactly one current occurrence re-establishes the
            // branch's class. NEVER BranchIdentity text / OCR text / Structured
            // TitleText == identity / historical bounds/index / stale
            // Observation.
            // If no pending branch is visible and the frozen revisit budget
            // remains, execute ONE bounded ScrollBackward (fresh Observe ->
            // same-Container continuity -> post-completeness consistency) and
            // re-evaluate. Budget exhausted -> fail closed (no unbounded search,
            // no infinite loop, no dispatch from historical frames).
            var hasStructuredOccurrences = container.ViewportExplorationObservations
                .Any(o => !SourceEquivalenceNormalizer.OccurrencesOf(o).IsDefaultOrEmpty);
            var revisitBudget = discoveryEpoch.TryGetValue(container.SemanticPageName, out var budgetEpoch)
                ? budgetEpoch.RevisitBudget
                : 0;

            ObservedElement sourceCandidate = null!;
            StructuredElementEvidence? selectedFreshElement = null;
            var branchIdentity = "";
            long sourceSequence = 0;
            string? claimedLogicalSource = null;
            TypeLevelHandling? selectedHandling = null;
            var dispatchSelected = false;
            bool selectedIsBoundary = false;

            bool returnedToParent = false;
            while (!dispatchSelected)
            {
                foreach (var (candidateIdentity, candidateSequence) in pending)
                {
                    var source = container.ViewportExplorationObservations
                        .First(observation => observation.SequenceNumber == candidateSequence);

                    if (hasStructuredOccurrences)
                    {
                        // CALLER_SOURCE_PROVENANCE_CONTRACT: occurrence-grounded
                        // ONLY through the caller's explicit RequiredBranchGrounding,
                        // validated by SourceGroundingValidator (fail closed).
                        if (inventory.RequiredBranchGrounding is null
                            || !inventory.RequiredBranchGrounding.TryGetValue(candidateIdentity, out var explicitReference))
                        {
                            return Fail(runId,
                                $"Required branch '{candidateIdentity}' has no explicit source provenance grounding; zero dispatch.");
                        }
                        var normalization = discoveryEpoch.TryGetValue(container.SemanticPageName, out var groundingEpoch)
                            ? groundingEpoch.Normalization
                            : SourceEquivalenceNormalizer.Normalize(container.ViewportExplorationObservations);
                        var grounding = new BranchSourceGroundingEvidence(candidateIdentity, explicitReference);
                        var groundingResult = SourceGroundingValidator.Validate(
                            container.ViewportExplorationObservations,
                            grounding,
                            normalization,
                            groundedLogicalSources.Count == 0 ? null : groundedLogicalSources.ToImmutableHashSet());
                        if (groundingResult.Status != SourceGroundingValidator.SourceGroundingStatus.Valid
                            || groundingResult.SourceElementIndex is null)
                        {
                            return Fail(runId,
                                $"Required branch '{candidateIdentity}' grounding rejected: {groundingResult.Reason}");
                        }
                        var branchClassSignature = SourceGroundingValidator.TryResolveLogicalSource(
                            SourceEquivalenceNormalizer.OccurrencesOf(source).First(o =>
                                string.Equals(o.OccurrenceIdentity, explicitReference.OccurrenceLocalIdentity, StringComparison.Ordinal)),
                            normalization);
                        // Authorization candidate = the grounded source occurrence
                        // projected from STRUCTURED evidence (never the degraded OCR
                        // array indexed by a structured index).
                        var groundedRaw = source.StructuredElements[groundingResult.SourceElementIndex.Value];
                        sourceCandidate = new ObservedElement(
                            groundedRaw.TitleText ?? candidateIdentity, null,
                            groundingResult.SourceElementIndex.Value, groundedRaw.Bounds, "structured");

                        // CURRENTLY_VISIBLE: exactly one current fresh occurrence
                        // re-establishes this branch's frozen logical source class.
                        var freshElement = ResolveCurrentVisibleElement(current, branchClassSignature!);
                        if (freshElement is null)
                            continue; // not currently visible -> next pending
                        selectedFreshElement = freshElement;
                        claimedLogicalSource = branchClassSignature;
                    }
                    else
                    {
                        // Legacy Elements-only path UNCHANGED: text-based grounding
                        // and visibility against the current Observation.
                        sourceCandidate = source.Elements.First(
                            element => string.Equals(element.Text, candidateIdentity, StringComparison.Ordinal));
                        var legacyVisible = current.Elements
                            .Where(element => string.Equals(element.Text, candidateIdentity, StringComparison.Ordinal))
                            .ToArray();
                        if (legacyVisible.Length != 1)
                            continue; // not uniquely visible -> next pending
                    }

                    var authorization = goal.CandidateAuthorizationEvaluator(source, sourceCandidate)
                        ?? throw new InvalidOperationException("CandidateAuthorizationEvaluator 返回 null evidence。");
                    if (authorization.Authorized is not true)
                    {
                        _trace.Add(new TraceEvent(runId)
                        {
                            ContainerId = container.SemanticPageName,
                            Reason = $"required branch authorization rejected: text={candidateIdentity}; {authorization.Reason}",
                        });
                        continue;
                    }

                    // Conservative identity pre-check: reject before dispatching a
                    // child action if the branch identity is an ancestor or already
                    // visited semantic page identity.
                    if (ancestry.Contains(candidateIdentity))
                    {
                        _trace.Add(new TraceEvent(runId)
                        {
                            ContainerId = container.SemanticPageName,
                            Reason = $"open-world identity safety: ancestry cycle rejected for branch identity '{candidateIdentity}'.",
                        });
                        return Fail(runId,
                            $"Open-world identity safety: ancestry cycle detected for branch identity '{candidateIdentity}'；zero child dispatch。");
                    }
                    if (visited.Contains(candidateIdentity))
                    {
                        _trace.Add(new TraceEvent(runId)
                        {
                            ContainerId = container.SemanticPageName,
                            Reason = $"open-world identity safety: duplicate branch identity '{candidateIdentity}' rejected before dispatch.",
                        });
                        return Fail(runId,
                            $"Open-world identity safety: duplicate semantic page identity for branch '{candidateIdentity}'；zero child dispatch。");
                    }

                    // ── CP-12 type-directed dispatch: classify element → resolve handling ──
                    var category = goal.CategoryClassifier?.Invoke(sourceCandidate);
                    var handling = category is not null && dispatchPolicy is not null
                        ? dispatchPolicy.Resolve(category.Value)
                        : null;
                    if (handling == TypeLevelHandling.Forbidden)
                        return Fail(runId, $"Required branch '{candidateIdentity}' category {category} is forbidden by the dispatch policy；zero dispatch。");
                    if (category is not null && handling is null)
                        return Fail(runId, $"Required branch '{candidateIdentity}' category {category} has no authorized handling in the dispatch policy；zero dispatch。");

                    // Record the branch as an AUTHORIZED CHILD OBLIGATION: a
                    // discovered candidate becomes an obligation only when the
                    // Agent explicitly authorized and dispatched it (denied /
                    // audited candidates never enter this set and never block
                    // the verified parent return). An AUTHORIZED_BOUNDARY
                    // crossing is tracked separately (RequiredBoundaryObligations)
                    // and NEVER enters RequiredChildren / recursive authority.
                    selectedIsBoundary = authorization.Kind == AuthorizationKind.AuthorizedBoundary;
                    if (!selectedIsBoundary)
                    {
                        _branchProgress = _branchProgress.SetItem(
                            container.SemanticPageName,
                            progress!.WithAuthorizedSibling(candidateIdentity, candidateSequence));
                    }
                    branchIdentity = candidateIdentity;
                    sourceSequence = candidateSequence;
                    selectedHandling = handling;
                    dispatchSelected = true;
                    break;
                }

                if (dispatchSelected)
                    break;

                // ── VERIFIED-RETURN TRIGGER (repair) ──
                // The dispatch pass audited every pending candidate and NONE
                // was authorized/dispatched. DISCOVERED candidates are NOT
                // AUTHORIZED CHILD OBLIGATIONS: when the Container is COMPLETE,
                // no authorized recursive obligation remains pending, and a
                // known parent exists, the recursion scope is exhausted and the
                // verified parent return may proceed — WITHOUT requiring the
                // container to be a structural leaf (navigation-candidate
                // count == 0). E.g. Location services: sources=2 discovered,
                // authorized children=0 → RETURN_ELIGIBLE.
                if (IsReturnEligible(
                        parentCount: parents.Count,
                        containerComplete: discoveryEpoch.ContainsKey(container.SemanticPageName),
                        progress: _branchProgress.TryGetValue(container.SemanticPageName, out var eligibilityProgress)
                            ? eligibilityProgress
                            : null))
                {
                    var (triggerReturnState, triggerReturnedChild) = await TryPerformVerifiedParentReturnAsync(
                        container, current, parents, applicationIdentity, runId, goal, cancellationToken);
                    if (triggerReturnState is not null)
                        return triggerReturnState.Value;
                    if (triggerReturnedChild is not null)
                        ancestry = ancestry.Remove(triggerReturnedChild);
                    returnedToParent = true;
                    break;
                }

                // ── ROOT TERMINAL (sibling/subtree completion) ──
                // When all AUTHORIZED children of the Root are completed, the
                // Root is terminal for final GoalEvidence evaluation — even if
                // DISCOVERED-but-DENIED sources remain in the pending set
                // (they are not required children and do not block the
                // terminal completion check). The root has no parent to return
                // to, so this is distinct from the verified-return trigger.
                // No pending branch is CURRENTLY_VISIBLE -> one bounded revisit step.
                if (revisitBudget <= 0)
                {
                    // ROOT TERMINAL: when all AUTHORIZED children of the Root
                    // are completed, all boundary obligations are verified, and
                    // the revisit budget is exhausted, the Root is terminal for
                    // final GoalEvidence evaluation — even if DISCOVERED-but-
                    // DENIED sources remain pending (they are not required
                    // children and do not block the terminal completion check).
                    // A PENDING boundary obligation blocks this terminal (EBD-12/13)
                    // — GoalEvidence can never bypass an unverified boundary.
                    if (parents.Count == 0
                        && _branchProgress.TryGetValue(container.SemanticPageName, out var terminalProgress)
                        && !terminalProgress.HasPendingBoundaryObligation
                        && terminalProgress.AuthorizedSiblingEvidence.Count > 0
                        && terminalProgress.AuthorizedSiblingEvidence.Keys.All(
                            terminalProgress.CompletedSiblingEvidence.ContainsKey))
                    {
                        if (requiredBranches.Count == 0)
                            return Fail(runId, "Root bounded inventory is empty; no verified required traversal work supports this U2 execution path.");
                        var finalEvidence = goal.EvidenceEvaluator(current);
                        if (finalEvidence.Satisfied)
                            return Complete(runId, finalEvidence);
                        return Fail(runId, $"Verified bounded traversal completion but fresh GoalEvidence remains unsatisfied：{finalEvidence.Reason}");
                    }
                    return Fail(runId,
                        $"No required branch is CURRENTLY_VISIBLE and the bounded revisit budget is exhausted（budget={budgetEpoch?.RevisitBudget ?? 0}）；zero dispatch。");
                }

                var revisitStep = await _traversal.ExecuteLoweredActionAsync(new DeviceAction.ScrollBackward(), current);
                var revisitEntry = LastJournalEntry();
                if (revisitStep is TraversalStepResult.Failed revisitFailed)
                    return Fail(runId, revisitFailed.Reason, revisitEntry.StepId);
                if (revisitEntry.PostActionObservation is null)
                    return Fail(runId, "bounded revisit ScrollBackward 缺少 fresh Observation。", revisitEntry.StepId);
                RecordDispatchedStep(runId, container, revisitEntry);
                var revisited = revisitEntry.PostActionObservation;
                // POST-SCROLL EVIDENCE-QUALITY SETTLE (ScrollBackward): the same
                // bounded evidence-quality handling as the forward exploration
                // (malformed mid-fling captures are provisional; valid-bounds
                // textless rows remain genuine UNKNOWN; budget exhausted ->
                // fail closed).
                revisited = await SettlePostScrollEvidenceQualityAsync(revisited, cancellationToken)
                    ?? throw new InvalidOperationException("bounded revisit post-scroll evidence quality budget exhausted.");
                _belief = Reconcile.FromObservation(revisited, _resolveSemanticPage);
                if (!container.TryVerifyViewportContinuity(revisited, _belief.SemanticPage, applicationIdentity))
                {
                    return Fail(runId,
                        $"Bounded revisit did not prove same-Container continuity；revisit stopped。",
                        revisitEntry.StepId);
                }
                container.AcceptFreshObservation(revisited);
                current = container.CurrentObservation
                    ?? throw new InvalidOperationException("bounded revisit CurrentObservation lost.");
                revisitBudget--;
                discoveryEpoch[container.SemanticPageName] = discoveryEpoch[container.SemanticPageName]
                    with { RevisitBudget = revisitBudget };

                // POST-COMPLETENESS CONSISTENCY: the revisited fresh evidence is
                // validated against the FROZEN epoch ONLY — the discovery history
                // is never re-normalized with revisit evidence, and the inventory
                // is never expanded. INVALIDATED -> fail closed.
                if (discoveryEpoch.TryGetValue(container.SemanticPageName, out var revisitEpoch))
                {
                    var consistency = PostCompletenessConsistencyValidator.Validate(
                        current,
                        revisitEpoch.Evidence,
                        continuityVerified: true,
                        BuildParentReturnDisposition(
                            current,
                            parents.Count > 0 ? parents.Peek().Parent.SemanticPageName : null,
                            goal));
                    if (!consistency.Consistent)
                    {
                        return Fail(runId, $"Post-completeness fresh evidence INVALIDATED: {consistency.Reason}");
                    }
                    _trace.Add(new TraceEvent(runId)
                    {
                        ContainerId = container.SemanticPageName,
                        Reason = $"bounded revisit step seq={current.SequenceNumber} (budget remaining={revisitBudget}): {consistency.Reason}",
                    });
                }
            }

            if (returnedToParent)
                continue; // verified parent return performed — resume at the parent container

            if (claimedLogicalSource is not null)
                groundedLogicalSources.Add(claimedLogicalSource);

            // ── dispatch the selected branch ──
            PlanStep step;
            switch (selectedHandling)
            {
                case TypeLevelHandling.SetDesiredState:
                    step = new PlanStep(branchIdentity, "SetSwitch true"); break;
                case TypeLevelHandling.Inspect:
                    step = new PlanStep(branchIdentity, "Tap"); break; // Inspect = Tap without child container creation
                default:
                    step = new PlanStep(branchIdentity, "Tap"); break; // EnterAndTraverse or null → Tap
            }

            TraversalJournalEntry entry;
            if (hasStructuredOccurrences)
            {
                // Explicit path: the tap target MUST come from the CURRENT fresh
                // structured occurrence's real bounds (uiautomator) — never OCR
                // text, never historical bounds/index.
                var freshBounds = selectedFreshElement?.Bounds
                    ?? throw new InvalidOperationException("explicit dispatch 缺少当前 fresh structured occurrence bounds。");
                var dispatchAction = (DeviceAction)(selectedHandling switch
                {
                    TypeLevelHandling.SetDesiredState => new DeviceAction.SetSwitch(null, true, freshBounds),
                    _ => new DeviceAction.Tap(null, freshBounds),
                });
                var lowered = await _traversal.ExecuteLoweredActionAsync(dispatchAction, current);
                entry = LastJournalEntry();
                if (lowered is TraversalStepResult.Failed loweredFailed)
                    return Fail(runId, loweredFailed.Reason, entry.StepId);
            }
            else
            {
                var result = container.ExecuteStep(step);
                entry = LastJournalEntry();
                if (result is TraversalStepResult.Failed failedStep)
                    return Fail(runId, failedStep.Reason, entry.StepId);
            }
            RecordDispatchedStep(runId, container, entry);

            // Inspect / SetDesiredState: leaf interaction — stay on current container, mark branch completed.
            if (selectedHandling is TypeLevelHandling.Inspect or TypeLevelHandling.SetDesiredState)
            {
                if (!_branchProgress.TryGetValue(container.SemanticPageName, out var leafProgress))
                    return Fail(runId, "Leaf dispatch lacks accepted progress evidence for the current container.", entry.StepId);
                _branchProgress = _branchProgress.SetItem(
                    container.SemanticPageName,
                    leafProgress.WithCompletedSibling(branchIdentity, entry.PostActionObservation?.SequenceNumber ?? current.SequenceNumber));
                _trace.Add(new TraceEvent(runId)
                {
                    ContainerId = container.SemanticPageName,
                    StepId = entry.StepId,
                    Reason = $"leaf {selectedHandling} dispatched for '{branchIdentity}' (seq={entry.PostActionObservation?.SequenceNumber})",
                });
                continue;
            }

            // ── EXTERNAL BOUNDARY (EBD) ──
            // An AUTHORIZED_BOUNDARY source was tapped. If the fresh post-action
            // foreground differs from the owned foreground, this is an
            // EXTERNAL_FOREGROUND_TRANSITION: create the BoundaryObligation
            // (PENDING, RETURNED_TO_PARENT), execute exactly one authorized
            // SystemBack, and on fresh exact-parent + continuity PASS write
            // VerifiedBoundaryDisposition(RETURNED_TO_PARENT). The external
            // destination NEVER becomes a recursive Container.
            if (selectedIsBoundary)
            {
                var boundaryFirstPost = entry.PostActionObservation
                    ?? throw new InvalidOperationException("bounded boundary Tap Succeeded 但缺少 fresh Observation。");
                var (boundaryState, boundaryHandled) = await TryHandleExternalBoundaryAsync(
                    container, boundaryFirstPost, applicationIdentity, branchIdentity,
                    sourceSequence, claimedLogicalSource, runId, goal, cancellationToken);
                if (boundaryState is not null && boundaryState != RunState.Running)
                    return boundaryState.Value;
                if (boundaryHandled)
                    continue; // boundary returned to parent; resume at the parent container
                return Fail(runId, $"Authorized boundary source '{branchIdentity}' was not handled; fail closed.", entry.StepId);
            }

            // EnterAndTraverse (or null/Tap): enter child container for subtree traversal.
            var firstPostAction = entry.PostActionObservation
                ?? throw new InvalidOperationException("bounded branch Tap Succeeded 但缺少 fresh Observation。");
            // POST-ACTION SETTLE (branch dispatch): the first post-action
            // Observation is PROVISIONAL — it must NOT update CurrentObservation,
            // append as accepted evidence, mutate identity safety, or freeze
            // completeness. Only the confirmed fresh Observation whose reconciled
            // page differs from the pre-action parent may enter identity safety /
            // child-container creation. One action dispatch -> N bounded
            // observations; zero redispatch.
            var settle = await SettlePostActionObservationAsync(
                firstPostAction,
                applicationIdentity,
                obs =>
                {
                    var page = _resolveSemanticPage(obs);
                    if (page is null)
                        return new TransitionCheck(false, null);
                    if (string.Equals(page, container.SemanticPageName, StringComparison.Ordinal))
                        return new TransitionCheck(false, null); // still on the pre-action parent
                    return new TransitionCheck(true, page);
                },
                runId,
                cancellationToken);
            if (settle.Confirmed is null)
                return Fail(runId, settle.Failure!, entry.StepId);
            var childObs = settle.Confirmed!;
            _belief = Reconcile.FromObservation(childObs, _resolveSemanticPage);
            var childPage = _belief.SemanticPage;
            if (childPage is null
                || string.Equals(childPage, container.SemanticPageName, StringComparison.Ordinal)
                || container.IsStillMine(childObs))
            {
                return Fail(runId, $"Required branch '{branchIdentity}' dispatch did not prove a fresh child Container transition；不 blind redispatch。", entry.StepId);
            }

            if (ancestry.Contains(childPage))
            {
                _trace.Add(new TraceEvent(runId)
                {
                    ContainerId = childPage,
                    StepId = entry.StepId,
                    Reason = $"open-world identity safety: ancestry cycle rejected for semantic page '{childPage}'.",
                });
                return Fail(runId, $"Open-world identity safety: cycle detected for semantic page '{childPage}' in current ancestry；zero child dispatch。", entry.StepId);
            }

            if (visited.Contains(childPage))
            {
                _trace.Add(new TraceEvent(runId)
                {
                    ContainerId = childPage,
                    StepId = entry.StepId,
                    Reason = $"open-world identity safety: duplicate semantic page identity rejected for '{childPage}'.",
                });
                return Fail(runId, $"Open-world identity safety: duplicate semantic page identity '{childPage}' across branches；fail closed。", entry.StepId);
            }

            parents.Push((container, branchIdentity));
            ancestry = ancestry.Add(childPage);
            visited = visited.Add(childPage);
            _activeContainer = CreateContainer(childPage);
            _activeContainer.Bind(childObs);
            _trace.Add(new TraceEvent(runId) { ContainerId = childPage });
        }
    }

    private async Task<OpenWorldViewportOutcome> ExploreCurrentContainerViewportsAsync(
        Goal goal,
        RuntimeContainer container,
        string applicationIdentity,
        string runId,
        CancellationToken cancellationToken)
    {
        const int MaxViewportSteps = 5;
        for (int stepIndex = 0; stepIndex < MaxViewportSteps; stepIndex++)
        {
            var current = container.CurrentObservation
                ?? throw new InvalidOperationException("open-world viewport exploration requires current Observation.");

            var decision = EvaluateViewportExploration(goal, container, runId, stepId: null);
            if (decision.ContinueExploration is null)
                return OpenWorldViewportOutcome.Unresolved;
            if (decision.ContinueExploration is false)
                return OpenWorldViewportOutcome.Exhausted;

            var step = await _traversal.ExecuteLoweredActionAsync(
                new DeviceAction.ScrollForward(), current);
            var entry = _traversal.Journal[^1];
            if (step is TraversalStepResult.Failed || entry.PostActionObservation is null)
                return OpenWorldViewportOutcome.Unresolved;

            var fresh = entry.PostActionObservation;
            // POST-SCROLL EVIDENCE-QUALITY SETTLE (ScrollForward): the immediate
            // post-scroll Observation is accepted only when evidence-quality-valid
            // (every interaction-relevant structured element has valid non-empty
            // bounds). A malformed mid-fling capture is PROVISIONAL — bounded
            // re-observe (ONE scroll -> N observations); budget exhausted ->
            // fail closed. The provisional never enters continuity / accepted
            // evidence / normalization / exhaustion. Valid-bounds textless rows
            // remain genuine UNKNOWN.
            fresh = await SettlePostScrollEvidenceQualityAsync(fresh, cancellationToken);
            if (fresh is null)
                return OpenWorldViewportOutcome.Unresolved;

            _belief = Reconcile.FromObservation(fresh, _resolveSemanticPage);
            if (!container.TryVerifyViewportContinuity(
                    fresh,
                    _belief.SemanticPage,
                    applicationIdentity))
            {
                if (_belief.SemanticPage is not null
                    && !string.Equals(_belief.SemanticPage, container.SemanticPageName, StringComparison.Ordinal))
                {
                    return OpenWorldViewportOutcome.Transitioned;
                }
                return OpenWorldViewportOutcome.Unresolved;
            }

            // FRESH-CONTAINER-EVIDENCE: same-Container continuity verified ->
            // refresh CurrentObservation (scroll freshness repair).
            container.AcceptFreshObservation(fresh);
            RecordDispatchedStep(runId, container, entry);
        }

        return OpenWorldViewportOutcome.Cutoff;
    }

    private bool TryBuildContainerInventoryCompleteness(
        RuntimeContainer container,
        Goal goal,
        string? knownParentPage,
        out ContainerInventoryCompletenessEvidence evidence,
        out string failure)
    {
        evidence = null!;
        failure = null!;

        var accepted = container.ViewportExplorationObservations;
        if (accepted.IsDefaultOrEmpty)
        {
            failure = "No accepted viewport observations; completeness cannot be proven.";
            return false;
        }

        var normalized = SourceEquivalenceNormalizer.Normalize(accepted);
        // LEAF-CHILD CASE (CHILD_AFFORDANCE): an accepted observation with ZERO
        // navigation candidates is a valid EMPTY child-navigation inventory ONLY
        // when every interactive element is either absent or contextually resolved
        // (the Agent-owned parent-return control). Any OTHER normalization failure
        // (duplicate signatures / ambiguous overlap) still fails closed — the
        // normalizer itself is untouched.
        var allCandidateFree = accepted.All(o => SourceEquivalenceNormalizer.OccurrencesOf(o).IsDefaultOrEmpty);
        if (!normalized.IsResolved && !allCandidateFree)
        {
            failure = "Source normalization is unresolved; completeness cannot be proven.";
            return false;
        }

        var unknownCount = 0;
        foreach (var observation in accepted)
        {
            foreach (var affordance in InteractionAffordanceAnalyzer.Analyze(observation))
            {
                if (affordance.Classification != InteractionAffordanceKind.Unknown)
                    continue;
                // CONTEXTUAL PARENT-RETURN RESOLUTION: an interactive UNKNOWN that
                // is the unique, authorized, labelled parent-return control is
                // RESOLVED_PARENT_RETURN_CONTROL — it is not a child-navigation
                // source and does not block completeness. Ordinary UNKNOWNs keep
                // blocking (fail closed; never UNKNOWN -> ignore).
                if (IsResolvedParentReturnControl(observation, affordance.SourceElementIndex, goal, knownParentPage))
                    continue;
                unknownCount++;
            }
        }

        if (unknownCount > 0)
        {
            failure = "Unknown interaction affordances remain; completeness cannot be proven.";
            return false;
        }

        evidence = new ContainerInventoryCompletenessEvidence(
            container.SemanticPageName,
            accepted.Select(o => o.SequenceNumber).ToImmutableArray(),
            normalized.UniqueSourceSignatures,
            ExplorationExhausted: true,
            UnresolvedCandidateCount: unknownCount,
            Reason: $"Deterministic viewport exhaustion and source normalization completed for '{container.SemanticPageName}'.");
        return true;
    }

    private static bool HasStructuredEvidence(RuntimeContainer container)
        => container.ViewportExplorationObservations
            .Any(o => !o.StructuredElements.IsDefaultOrEmpty);

    /// <summary>
    /// POST-COMPLETENESS CONTEXTUAL DISPOSITION (data flow per the consistency
    /// contract): BEFORE the Validator runs, the Agent — the sole contextual
    /// semantic authority — resolves the parent-return control in the CURRENT
    /// fresh Observation (context-free affordance analysis → Agent contextual
    /// parent-return resolution) and produces the occurrence-scoped
    /// disposition. The Validator consumes ONLY this explicit disposition; it
    /// never performs its own parent-return interpretation, and the
    /// context-free analyzer is never widened. No known parent (or a failed /
    /// ambiguous / unauthorized resolution) produces NO disposition — the
    /// occurrence then remains an UNRESOLVED UNKNOWN and fails closed in the
    /// Validator. The disposition is occurrence-scoped (ObservationSequence +
    /// StructuredElementIndex) and is rebuilt per fresh Observation — never
    /// cached or reused across Observations.
    /// </summary>
    private static ImmutableArray<ContextualInteractionDisposition> BuildParentReturnDisposition(
        Observation fresh,
        string? knownParentPage,
        Goal goal)
    {
        if (knownParentPage is null)
            return [];
        if (!TryResolveUniqueParentReturnControl(fresh, knownParentPage, goal, out var control, out _))
            return [];
        return ImmutableArray.Create(new ContextualInteractionDisposition(
            fresh.SequenceNumber,
            control.ElementIndex,
            ContextualInteractionDispositionKind.ParentReturnControl));
    }

    /// <summary>
    /// VERIFIED-RETURN ELIGIBILITY (the repair of the parent-return trigger):
    /// a Container may perform the verified parent return when
    ///   (1) ContainerComplete(Current) — the frozen discovery epoch exists,
    ///   (2) PendingAuthorizedChildren(Current) == 0 — every AUTHORIZED child
    ///       obligation has completed (a discovered-but-audited candidate is
    ///       NOT an obligation and never blocks the return),
    ///   (3) a known parent exists.
    /// Structural leaf-ness (NavigationCandidateCount == 0) is NOT required:
    /// Location services (sources=2 discovered, authorized children=0) is
    /// RETURN_ELIGIBLE. This is the Agent's decision (the Agent owns recursive
    /// authorization and whether authorized obligations remain); the Traversal
    /// executes only authorized actions; Container completeness grants no new
    /// recursion authority.
    /// </summary>
    internal static bool IsReturnEligible(
        int parentCount,
        bool containerComplete,
        BranchProgressEvidence? progress)
        => containerComplete
            && parentCount > 0
            && progress is not null
            && !progress.HasPendingBoundaryObligation
            && progress.AuthorizedSiblingEvidence.Keys.All(progress.CompletedSiblingEvidence.ContainsKey);

    /// <summary>
    /// VERIFIED PARENT RETURN (shared by the subtree-terminal path and the
    /// verified-return trigger): resolves the fresh parent-return control,
    /// dispatches EXACTLY ONE Tap at FRESH structured bounds, settles the
    /// bounded post-action transition, and requires the fresh destination to
    /// reconcile to the EXACT expected parent identity + same-Container
    /// continuity. Tap receipt alone is never return truth. On success the
    /// child is popped from the ancestry (the parent container resumes with
    /// the returned fresh evidence; the child remains visited). Returns null
    /// to continue the main loop at the parent, or the terminal RunState.
    /// </summary>
    private async Task<(RunState? State, string? ReturnedChildPage)> TryPerformVerifiedParentReturnAsync(
        RuntimeContainer container,
        Observation current,
        Stack<(RuntimeContainer Parent, string ChildIdentity)> parents,
        string applicationIdentity,
        string runId,
        Goal goal,
        CancellationToken cancellationToken)
    {
        var (parent, childIdentity) = parents.Peek();
        TraversalJournalEntry returnEntry;
        if (HasStructuredEvidence(container))
        {
            // CONTEXTUAL PARENT-RETURN RESOLUTION (Agent-owned; the analyzer
            // stays context-free — Button/ImageButton remains UNKNOWN in
            // isolation). The Agent interprets the unique interactive element
            // whose action label / action-role matches the known parent-return
            // intent as the parent-return control. TitleText/ContentDescription
            // are action-label evidence only — never PageIdentity /
            // SourceIdentity. The control's FRESH structured bounds form the
            // Tap; return success is proven ONLY by the fresh post-action
            // Observation reconciling to the expected parent.
            if (!TryResolveUniqueParentReturnControl(
                    current, parent.SemanticPageName, goal,
                    out var returnControl, out var returnResolutionFailure))
            {
                return (Fail(runId, returnResolutionFailure!), null);
            }

            var returnAction = new DeviceAction.Tap(null, returnControl.Bounds);
            var returnLowered = await _traversal.ExecuteLoweredActionAsync(returnAction, current);
            returnEntry = LastJournalEntry();
            if (returnLowered is TraversalStepResult.Failed returnFailed)
                return (Fail(runId, returnFailed.Reason, returnEntry.StepId), null);
        }
        else
        {
            // Legacy Elements-only path (unchanged): OCR text match + step dispatch.
            var returnCandidates = current.Elements
                .Where(element => string.Equals(element.Text, parent.SemanticPageName, StringComparison.Ordinal))
                .ToArray();
            if (returnCandidates.Length != 1)
                return (Fail(runId, $"Parent return is not uniquely grounded for '{parent.SemanticPageName}'；零 return dispatch。"), null);
            var legacyReturnAuthorization = goal.CandidateAuthorizationEvaluator(current, returnCandidates[0])
                ?? throw new InvalidOperationException("CandidateAuthorizationEvaluator 返回 null evidence。");
            if (legacyReturnAuthorization.Authorized is not true)
                return (Fail(runId, $"Parent return is not authorized for '{parent.SemanticPageName}'；零 return dispatch。"), null);

            var returnResult = container.ExecuteStep(new PlanStep(parent.SemanticPageName, "Tap"));
            returnEntry = LastJournalEntry();
            if (returnResult is TraversalStepResult.Failed failed)
                return (Fail(runId, failed.Reason, returnEntry.StepId), null);
        }
        RecordDispatchedStep(runId, container, returnEntry);
        var firstReturn = returnEntry.PostActionObservation
            ?? throw new InvalidOperationException("parent return Succeeded 但缺少 fresh Observation。");
        // POST-ACTION SETTLE (parent return): the first post-action
        // Observation is PROVISIONAL. Only the confirmed fresh Observation
        // reconciling to the EXPECTED parent (parents stack — the existing
        // parent identity authority) may be accepted. Button label / Tap
        // receipt are never return truth.
        var returnSettle = await SettlePostActionObservationAsync(
            firstReturn,
            applicationIdentity,
            obs =>
            {
                var page = _resolveSemanticPage(obs);
                if (page is null)
                    return new TransitionCheck(false, null);
                return new TransitionCheck(
                    string.Equals(page, parent.SemanticPageName, StringComparison.Ordinal),
                    page);
            },
            runId,
            cancellationToken);
        if (returnSettle.Confirmed is null)
            return (Fail(runId, returnSettle.Failure!, returnEntry.StepId), null);
        var returned = returnSettle.Confirmed!;
        _belief = Reconcile.FromObservation(returned, _resolveSemanticPage);
        if (returned.SequenceNumber <= current.SequenceNumber
            || !string.Equals(_belief.SemanticPage, parent.SemanticPageName, StringComparison.Ordinal)
            || !parent.TryVerifyViewportContinuity(returned, _belief.SemanticPage, applicationIdentity))
        {
            return (Fail(runId, $"Parent return did not prove fresh exact reconciliation to '{parent.SemanticPageName}'；no child completion.", returnEntry.StepId), null);
        }
        // FRESH-CONTAINER-EVIDENCE: exact parent reconciliation + continuity
        // verified -> the parent container's CurrentObservation MUST become
        // this fresh returned Observation (stale-current defect repair).
        parent.AcceptFreshObservation(returned);

        if (!_branchProgress.TryGetValue(parent.SemanticPageName, out var parentProgress))
            return (Fail(runId, "Verified parent return lacks accepted parent progress evidence.", returnEntry.StepId), null);
        _branchProgress = _branchProgress.SetItem(
            parent.SemanticPageName,
            parentProgress.WithCompletedSibling(childIdentity, current.SequenceNumber));
        var returnedChildPage = container.SemanticPageName;
        parents.Pop();
        _activeContainer = parent;
        _trace.Add(new TraceEvent(runId)
        {
            ContainerId = parent.SemanticPageName,
            StepId = returnEntry.StepId,
            Reason = $"verified parent return; child '{childIdentity}' progress retained (seq={current.SequenceNumber})",
        });
        return (null, returnedChildPage);
    }

    /// <summary>
    /// EXTERNAL BOUNDARY handling (EBD). Consumes an AUTHORIZED_BOUNDARY
    /// dispatch whose fresh post-action foreground differs from the owned
    /// foreground. Establishes the ExternalBoundary relation + a PENDING
    /// BoundaryObligation (RequiredDisposition = RETURNED_TO_PARENT), executes
    /// EXACTLY ONE authorized SystemBack, and writes
    /// VerifiedBoundaryDisposition(RETURNED_TO_PARENT) only when the fresh
    /// post-back evidence reconciles to the EXACT expected parent with parent
    /// continuity. The external destination is NEVER a recursive Container and
    /// NEVER holds recursive authority. SystemBack dispatch receipt is never
    /// the truth — only fresh world evidence is.
    /// </summary>
    private async Task<(RunState? FailState, bool Handled)> TryHandleExternalBoundaryAsync(
        RuntimeContainer parent,
        Observation firstPostAction,
        string applicationIdentity,
        string branchIdentity,
        long sourceSequence,
        string? claimedLogicalSource,
        string runId,
        Goal goal,
        CancellationToken cancellationToken)
    {
        // BOUNDARY_OBSERVED only from fresh post-action evidence (never a tap receipt).
        var postForeground = firstPostAction.ForegroundApplication;
        var boundaryObserved = !string.Equals(postForeground, applicationIdentity, StringComparison.Ordinal);
        if (!boundaryObserved)
            return (Fail(runId, $"Authorized boundary source '{branchIdentity}' did not produce an external foreground (post={postForeground}); fail closed."), false);

        var sourceRef = $"{branchIdentity}@{sourceSequence}";
        var relation = new BoundaryRelation(
            parent.SemanticPageName,   // ParentContainerIdentity
            sourceRef,                 // SourceOccurrenceReference (bound to triggering occurrence; never BranchIdentity/source-text/destination-title)
            applicationIdentity,       // PreActionForeground
            postForeground,            // ExternalForeground
            parent.SemanticPageName,   // ExpectedReturnParent (exact)
            sourceSequence);           // SourceObservationSequence
        var obligation = new BoundaryObligation(relation);

        if (!_branchProgress.TryGetValue(parent.SemanticPageName, out var parentBoundaryProgress))
            return (Fail(runId, "External boundary lacks accepted parent progress evidence."), false);
        _branchProgress = _branchProgress.SetItem(
            parent.SemanticPageName,
            parentBoundaryProgress.WithBoundaryObligation(obligation));
        _trace.Add(new TraceEvent(runId)
        {
            ContainerId = parent.SemanticPageName,
            Reason = $"EXTERNAL_BOUNDARY_OBSERVED: {branchIdentity} → {postForeground} (owned={applicationIdentity}); obligation PENDING",
        });

        // SystemBack exactly once.
        var backLowered = await _traversal.ExecuteLoweredActionAsync(new DeviceAction.SystemBack(), firstPostAction);
        var backEntry = LastJournalEntry();
        if (backLowered is TraversalStepResult.Failed backFailed)
            return (Fail(runId, backFailed.Reason, backEntry.StepId), false);
        RecordDispatchedStep(runId, parent, backEntry);
        var firstReturn = backEntry.PostActionObservation
            ?? throw new InvalidOperationException("SystemBack Succeeded 但缺少 fresh Observation。");

        // Settle post-back → must reconcile to the EXACT expected parent.
        var settle = await SettlePostActionObservationAsync(
            firstReturn,
            applicationIdentity,
            obs =>
            {
                var page = _resolveSemanticPage(obs);
                if (page is null)
                    return new TransitionCheck(false, null);
                return new TransitionCheck(
                    string.Equals(page, parent.SemanticPageName, StringComparison.Ordinal),
                    page);
            },
            runId,
            cancellationToken);
        if (settle.Confirmed is null)
            return (Fail(runId, settle.Failure!), false);
        var returned = settle.Confirmed!;
        _belief = Reconcile.FromObservation(returned, _resolveSemanticPage);

        // EBD-8/9/10/11: fresh exact parent + foreground + continuity.
        if (returned.SequenceNumber <= firstPostAction.SequenceNumber
            || !string.Equals(_belief.SemanticPage, parent.SemanticPageName, StringComparison.Ordinal)
            || !string.Equals(returned.ForegroundApplication, applicationIdentity, StringComparison.Ordinal)
            || !parent.TryVerifyViewportContinuity(returned, _belief.SemanticPage, applicationIdentity))
        {
            return (Fail(runId, $"External boundary return did not prove fresh exact reconciliation to '{parent.SemanticPageName}'; no disposition.", backEntry.StepId), false);
        }
        parent.AcceptFreshObservation(returned);

        // Write VerifiedBoundaryDisposition (RETURNED_TO_PARENT).
        var disposition = new VerifiedBoundaryDisposition(relation, parent.SemanticPageName, returned.SequenceNumber);
        _branchProgress = _branchProgress.SetItem(
            parent.SemanticPageName,
            _branchProgress[parent.SemanticPageName].WithVerifiedBoundaryDisposition(disposition));
        _trace.Add(new TraceEvent(runId)
        {
            ContainerId = parent.SemanticPageName,
            StepId = backEntry.StepId,
            Reason = $"EXTERNAL_BOUNDARY_RETURNED_TO_PARENT: {branchIdentity} → verified exact parent '{parent.SemanticPageName}' (seq={returned.SequenceNumber})",
        });
        return (null, true);
    }

    /// <summary>
    /// The Agent's contextual parent-return control: the projected authorization
    /// candidate (label as action-label evidence), the FRESH structured bounds
    /// used to form the real Tap, and the structured element index.
    /// </summary>
    private sealed record ParentReturnControl(
        ObservedElement Projected,
        ElementBounds? Bounds,
        int ElementIndex);

    /// <summary>
    /// PARENT-RETURN ACTION-ROLE LABEL — Android platform accessibility
    /// action-label evidence: the standard toolbar "up" control reports
    /// content-desc "Navigate up" (the accessibility action label of the
    /// action-bar home/up button) with NO TitleText and NO resource-id on real
    /// Settings child pages (ImageButton, clickable, valid top-left app-bar
    /// bounds, inside the action bar). It is ACTION-ROLE evidence ONLY — it
    /// establishes a parent/up-return affordance, never a page identity, source
    /// identity, or destination identity (the destination is verified
    /// exclusively by the fresh post-action world evidence). Exact-label match
    /// only; no keyword expansion ("back"/"up" fragments are not matched
    /// without an independent evidence contract).
    ///
    /// KNOWN-ENVIRONMENT-DEPENDENT (待优化): the exact content-desc label is
    /// locale/platform dependent (English AOSP "Navigate up"; other locales /
    /// vendor ROMs may localize or omit it). Moving to another locale / ROM
    /// requires maintaining this label (candidate optimizations: a
    /// locale-independent platform action-role contract sourced from the
    /// semantic layer, or a configurable action-label list with an evidence
    /// contract — never a keyword expansion).
    /// See openspec/changes/settings-full-tree-enumeration-integration/KNOWN_LIMITATIONS.md.
    /// </summary>
    private const string ParentReturnActionRoleLabel = "Navigate up";

    /// <summary>
    /// PARENT-RETURN ACTION-ROLE EVIDENCE (Agent-owned; the analyzer stays
    /// context-free — an ImageButton remains UNKNOWN in isolation). The element
    /// carries the stable Android toolbar up-control accessibility action label
    /// content-desc "Navigate up". ContentDescription is used here as
    /// PARENT_RETURN_ACTION_LABEL_EVIDENCE only — never as PageIdentity /
    /// SourceIdentity / DestinationIdentity.
    /// </summary>
    private static bool IsParentReturnActionRole(StructuredElementEvidence raw)
        => string.Equals(raw.ContentDescription, ParentReturnActionRoleLabel, StringComparison.Ordinal);

    /// <summary>
    /// CONTEXTUAL PARENT-RETURN RESOLUTION (Agent-owned; the analyzer stays
    /// context-free — a Button/ImageButton is still UNKNOWN in isolation). An
    /// interactive element is the parent-return candidate iff it carries
    /// parent-return evidence, the candidate is UNIQUE in the current fresh
    /// Observation, and authorization PASSES:
    ///   A. destination-labelled return: TitleText equals the Agent's known
    ///      expected parent page name (fixture-style "Return" control);
    ///   B. action-role return: structured evidence establishes a parent/up-
    ///      return affordance — the stable Android action label content-desc
    ///      "Navigate up" (real toolbar Up control; carries no destination).
    /// Both kinds additionally require: known parent exists (the caller passes
    /// it), the candidate is interactive, FRESH actionable bounds are present,
    /// and the candidate is unique. Missing / ambiguous / non-actionable /
    /// unknown-destination candidates fail closed. TitleText / ContentDescription
    /// are action-label evidence only — never PageIdentity / SourceIdentity /
    /// DestinationIdentity; the destination is verified only by the fresh
    /// post-action world evidence.
    /// </summary>
    private static bool TryResolveUniqueParentReturnControl(
        Observation current,
        string knownParentPage,
        Goal goal,
        out ParentReturnControl control,
        out string? failure)
    {
        control = null!;
        failure = null;
        int? matchedIndex = null;
        foreach (var affordance in InteractionAffordanceAnalyzer.Analyze(current))
        {
            if (affordance.Classification is not (
                    InteractionAffordanceKind.Unknown or InteractionAffordanceKind.NavigationCandidate))
            {
                continue;
            }
            if (affordance.SourceElementIndex < 0
                || affordance.SourceElementIndex >= current.StructuredElements.Length)
            {
                continue;
            }
            var raw = current.StructuredElements[affordance.SourceElementIndex];
            var isDestinationLabelled = string.Equals(raw.TitleText, knownParentPage, StringComparison.Ordinal);
            var isActionRole = IsParentReturnActionRole(raw);
            if (!isDestinationLabelled && !isActionRole)
                continue;
            if (matchedIndex is not null)
            {
                failure = $"Parent-return candidate is ambiguous: multiple interactive elements match the parent-return evidence（label '{knownParentPage}' / action-role）；fail closed。";
                return false;
            }
            matchedIndex = affordance.SourceElementIndex;
        }
        if (matchedIndex is null)
        {
            failure = $"Parent-return candidate is absent for '{knownParentPage}'；fail closed。";
            return false;
        }

        var matched = current.StructuredElements[matchedIndex.Value];
        // FRESH ACTIONABLE BOUNDS REQUIRED: the resolved control must carry
        // valid positive-area bounds in the CURRENT fresh observation (the
        // eventual return Tap is formed from them). No actionable bounds ->
        // not actionable -> fail closed.
        if (matched.Bounds is not { IsValid: true }
            || matched.Bounds.Width <= 0f
            || matched.Bounds.Height <= 0f)
        {
            failure = $"Parent-return candidate '{knownParentPage}' lacks fresh actionable bounds；fail closed。";
            return false;
        }
        // Project the ACTION label: TitleText for destination-labelled returns,
        // the action-role label for real Android Up controls. The projected
        // text is the candidate's action label for the authorization policy —
        // never page/source/destination identity.
        var projectedText = matched.TitleText
            ?? (IsParentReturnActionRole(matched) ? matched.ContentDescription : null);
        var projected = new ObservedElement(projectedText, null, -1, matched.Bounds, "structured");
        var authorization = goal.CandidateAuthorizationEvaluator(current, projected)
            ?? throw new InvalidOperationException("CandidateAuthorizationEvaluator 返回 null evidence。");
        if (authorization.Authorized is not true)
        {
            failure = $"Parent-return candidate '{knownParentPage}' is not authorized；fail closed。";
            return false;
        }

        control = new ParentReturnControl(projected, matched.Bounds, matchedIndex.Value);
        return true;
    }

    /// <summary>
    /// True when the interactive UNKNOWN at <paramref name="elementIndex"/> of
    /// <paramref name="observation"/> IS the unique authorized labelled
    /// parent-return control (contextual resolution). Requires a known parent.
    /// </summary>
    private static bool IsResolvedParentReturnControl(
        Observation observation,
        int elementIndex,
        Goal goal,
        string? knownParentPage)
    {
        if (knownParentPage is null)
            return false;
        return TryResolveUniqueParentReturnControl(
                   observation, knownParentPage, goal, out var control, out _)
               && control.ElementIndex == elementIndex;
    }

    private RunState RunBoundedCrossPageDiscovery(Goal goal, Plan plan, string runId)
    {
        var semanticDepth = 0;
        var nextViewportStep = 0;
        var viewportSteps = plan.Steps
            .Where(step => IsScrollForwardAction(step.ActionDescription))
            .ToImmutableArray();

        while (true)
        {
            var container = _activeContainer
                ?? throw new InvalidOperationException("bounded discovery 缺少 active Container。");
            var current = container.CurrentObservation
                ?? throw new InvalidOperationException("bounded discovery Container 缺少当前 Observation。");
            var accepted = container.ViewportExplorationObservations;
            var evaluator = goal.BranchInventoryEvaluator
                ?? throw new InvalidOperationException("BranchInventoryEvaluator 缺失：调用方必须先检查 optional criterion。");
            var inventory = evaluator(accepted, semanticDepth)
                ?? throw new InvalidOperationException("BranchInventoryEvaluator 返回 null：必须返回 BranchInventoryEvidence。");

            var inventoryOutcome = inventory.RequiredBranchEvidence switch
            {
                null => "unresolved",
                { Count: 0 } => "leaf",
                _ => "complete",
            };
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = container.SemanticPageName,
                Reason = $"branch inventory {inventoryOutcome}: depth={semanticDepth}, "
                    + $"source-seq={current.SequenceNumber}; {inventory.Reason}",
            });

            if (inventory.RequiredBranchEvidence is null)
            {
                if (nextViewportStep >= viewportSteps.Length
                    || goal.ViewportExplorationEvaluator is null)
                {
                    return Fail(
                        runId,
                        $"Required branch inventory unresolved at depth={semanticDepth}：{inventory.Reason}；零 discovered-branch dispatch。");
                }

                var viewportDecision = EvaluateViewportExploration(goal, container, runId, stepId: null);
                if (viewportDecision.ContinueExploration is not true)
                {
                    var outcome = viewportDecision.ContinueExploration is false ? "exhausted" : "unresolved";
                    return Fail(
                        runId,
                        $"Branch inventory unresolved and viewport exploration {outcome}：{viewportDecision.Reason}；不 dispatch discovered branch。");
                }

                var viewportStep = viewportSteps[nextViewportStep++];
                var viewportResult = container.ExecuteStep(viewportStep);
                var viewportEntry = LastJournalEntry();
                if (viewportResult is TraversalStepResult.Failed viewportFailed)
                    return Fail(runId, viewportFailed.Reason, viewportEntry.StepId);

                RecordDispatchedStep(runId, container, viewportEntry);
                var viewportObservation = viewportEntry.PostActionObservation
                    ?? throw new InvalidOperationException("viewport step Succeeded 但缺少 fresh Observation。");
                _belief = Reconcile.FromObservation(viewportObservation, _resolveSemanticPage);
                if (!container.TryVerifyViewportContinuity(
                        viewportObservation,
                        _belief.SemanticPage,
                        _recoveryAnchor!.ApplicationIdentity))
                {
                    EmitViewportEscalation(
                        runId,
                        viewportEntry,
                        container,
                        viewportObservation,
                        "Bounded discovery viewport evidence cannot prove same-Container continuity.");
                    return Fail(
                        runId,
                        $"Viewport movement 后无法证明同一 Container continuity（seq={viewportObservation.SequenceNumber}）。",
                        viewportEntry.StepId);
                }
                // FRESH-CONTAINER-EVIDENCE: same-Container continuity verified ->
                // refresh CurrentObservation (bounded-discovery viewport freshness).
                container.AcceptFreshObservation(viewportObservation);

                // Same-Container accepted evidence extends the criterion input but does not change
                // semanticDepth. Re-evaluate inventory from the refreshed evidence next iteration.
                continue;
            }

            if (!TryAcceptBranchInventory(container, current, inventory, frozenNormalization: null, out var progress, out var invalidReason))
                return Fail(runId, invalidReason!);

            if (inventory.RequiredBranchEvidence.Count == 0)
            {
                var leafGoalEvidence = goal.EvidenceEvaluator(current);
                if (leafGoalEvidence.Satisfied)
                    return Complete(runId, leafGoalEvidence);
                return Fail(
                    runId,
                    $"Bounded leaf positively proven but GoalEvidence remains unsatisfied：{leafGoalEvidence.Reason}");
            }

            var pendingBranches = progress!.ApprovedSiblingEvidence
                .Where(entry => !progress.CompletedSiblingEvidence.ContainsKey(entry.Key))
                .OrderBy(entry => entry.Value)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .ToArray();
            if (pendingBranches.Length == 0)
            {
                var exhaustedGoalEvidence = goal.EvidenceEvaluator(current);
                if (exhaustedGoalEvidence.Satisfied)
                    return Complete(runId, exhaustedGoalEvidence);
                return Fail(
                    runId,
                    "Required branch inventory contains no unresolved work, but independent GoalEvidence remains unsatisfied；不 redispatch proven branch。");
            }

            var authorizationEvaluator = goal.CandidateAuthorizationEvaluator;
            if (authorizationEvaluator is null)
            {
                return Fail(
                    runId,
                    "Required branch inventory exists but bounded candidate authorization is unresolved because no criterion was supplied；零 dispatch。");
            }

            ObservedElement? selected = null;
            foreach (var (branchIdentity, sourceSequence) in pendingBranches)
            {
                var sourceObservation = accepted.First(observation => observation.SequenceNumber == sourceSequence);
                var sourceCandidate = sourceObservation.Elements.First(element =>
                    string.Equals(element.Text, branchIdentity, StringComparison.Ordinal));
                var authorization = authorizationEvaluator(sourceObservation, sourceCandidate)
                    ?? throw new InvalidOperationException("CandidateAuthorizationEvaluator 返回 null evidence。");
                var outcome = authorization.Authorized switch
                {
                    true => "authorized",
                    false => "rejected",
                    null => "unresolved",
                };
                _trace.Add(new TraceEvent(runId)
                {
                    ContainerId = container.SemanticPageName,
                    Reason = $"required branch authorization {outcome}: text={branchIdentity}, "
                        + $"source-seq={sourceSequence}; {authorization.Reason}",
                });
                if (authorization.Authorized is true)
                {
                    selected = current.Elements.FirstOrDefault(element =>
                        string.Equals(element.Text, branchIdentity, StringComparison.Ordinal));
                    if (selected is null)
                    {
                        return Fail(
                            runId,
                            $"Required authorized branch '{branchIdentity}' is absent from the current fresh Observation；零 dispatch。");
                    }
                    break;
                }
            }

            if (selected is null)
            {
                return Fail(
                    runId,
                    $"No required branch is independently authorized at depth={semanticDepth}；零 discovered-branch dispatch。");
            }

            var selectedStep = new PlanStep(selected.Text, "Tap");
            var parentPage = container.SemanticPageName;
            var result = container.ExecuteStep(selectedStep);
            var entry = LastJournalEntry();
            if (result is TraversalStepResult.Failed failed)
                return Fail(runId, failed.Reason, entry.StepId);

            RecordDispatchedStep(runId, container, entry);
            var postObservation = entry.PostActionObservation
                ?? throw new InvalidOperationException("bounded branch Tap Succeeded 但缺少 fresh Observation。");
            _belief = Reconcile.FromObservation(postObservation, _resolveSemanticPage);
            var childPage = _belief.SemanticPage;
            if (childPage is null
                || string.Equals(childPage, parentPage, StringComparison.Ordinal)
                || container.IsStillMine(postObservation))
            {
                return Fail(
                    runId,
                    $"Required branch '{selected.Text}' dispatch did not prove a fresh child Container transition；不 blind redispatch。",
                    entry.StepId);
            }

            _activeContainer = CreateContainer(childPage);
            _activeContainer.Bind(postObservation);
            semanticDepth = checked(semanticDepth + 1);
            _trace.Add(new TraceEvent(runId) { ContainerId = childPage });
        }
    }

    /// <summary>
    /// Explicit-path CURRENTLY_VISIBLE resolution: returns the CURRENT fresh
    /// structured element iff EXACTLY ONE current fresh NAVIGATION_CANDIDATE
    /// occurrence re-establishes the branch's frozen logical source class
    /// (signature resolution key into the frozen classes — never BranchIdentity
    /// text, never OCR text, never historical bounds/index). Null = not
    /// currently visible (or ambiguous).
    /// </summary>
    private static StructuredElementEvidence? ResolveCurrentVisibleElement(
        Observation current,
        string branchClassSignature)
    {
        int matches = 0;
        StructuredElementEvidence? matched = null;
        foreach (var affordance in InteractionAffordanceAnalyzer.Analyze(current))
        {
            if (affordance.Classification != InteractionAffordanceKind.NavigationCandidate)
                continue;
            if (affordance.SourceElementIndex < 0
                || affordance.SourceElementIndex >= current.StructuredElements.Length)
            {
                continue;
            }
            var raw = current.StructuredElements[affordance.SourceElementIndex];
            if (string.Equals(
                    SourceEquivalenceNormalizer.BuildSignature(raw),
                    branchClassSignature,
                    StringComparison.Ordinal))
            {
                matches++;
                matched = raw;
            }
        }
        return matches == 1 ? matched : null;
    }

    /// <summary>
    /// POST-ACTION SETTLE — bounded deterministic evidence polling after a
    /// semantic-transition action (branch dispatch / parent return).
    ///
    /// The first post-action Observation is PROVISIONAL: until a settle
    /// succeeds it must NOT update CurrentObservation, append as accepted
    /// viewport evidence, mutate ancestry/visited, freeze/invalidate
    /// completeness, feed GoalEvidence, or complete branch progress. Only a
    /// candidate -> confirmation -> SETTLED sequence of fresh Observations that
    /// all satisfy the transition predicate becomes the authoritative
    /// post-action Observation (stability against old->transient->target and
    /// old->target->transient single frames).
    ///
    /// The settle loop ONLY observes + reconciles + verifies — it never
    /// redispatchs the action (one action dispatch -> N bounded observations).
    /// The budget is a COMPOSITION_POLICY, not a semantic contract; exhaustion
    /// fails closed. The Environment merely acquires fresh evidence — it never
    /// judges page identity / action success / navigation success.
    /// </summary>
    private const int MaxPostActionSettleObservations = 3;

    private sealed record SettleOutcome(Observation? Confirmed, string? Failure)
    {
        public static SettleOutcome Settled(Observation observation) => new(observation, null);
        public static SettleOutcome BudgetExhausted(string failure) => new(null, failure);
    }

    private sealed record TransitionCheck(bool Satisfies, string? Identity);

    /// <summary>
    /// Bounded post-action settle: candidate -> confirmation -> SETTLED.
    /// Returns the CONFIRMED fresh Observation (authoritative) or fails closed.
    /// </summary>
    /// <param name="firstPostAction">The action's own first post-action Observation (provisional).</param>
    /// <param name="applicationIdentity">Foreground ownership expected for settled evidence.</param>
    /// <param name="transitionPredicate">Agent-owned transition predicate (reconcile-based; returns the reconciled identity when satisfied).</param>
    private async Task<SettleOutcome> SettlePostActionObservationAsync(
        Observation firstPostAction,
        string applicationIdentity,
        Func<Observation, TransitionCheck> transitionPredicate,
        string runId,
        CancellationToken cancellationToken)
    {
        var observationsSeen = 1;
        var observation = firstPostAction;
        Observation? candidate = null;
        string? candidateIdentity = null;

        while (true)
        {
            var check = EvaluateSettledObservation(observation, applicationIdentity, transitionPredicate);
            if (candidate is null)
            {
                if (check.Satisfies)
                {
                    // CANDIDATE: the transition predicate first holds.
                    candidate = observation;
                    candidateIdentity = check.Identity;
                }
            }
            else
            {
                // CONFIRMATION attempt: the predicate must STILL hold with the
                // SAME reconciled identity, or the candidate is rejected.
                if (check.Satisfies
                    && string.Equals(check.Identity, candidateIdentity, StringComparison.Ordinal))
                {
                    // SETTLED: only this confirmed fresh Observation is authoritative.
                    return SettleOutcome.Settled(observation);
                }
                candidate = check.Satisfies ? observation : null;
                candidateIdentity = check.Satisfies ? check.Identity : null;
            }

            if (observationsSeen >= MaxPostActionSettleObservations)
            {
                return SettleOutcome.BudgetExhausted(
                    $"post-action transition did not settle within {MaxPostActionSettleObservations} fresh observations；"
                    + "fail closed（composition policy；zero redispatch）。");
            }
            observationsSeen++;
            observation = await _observeInitial(cancellationToken);
        }
    }

    private static TransitionCheck EvaluateSettledObservation(
        Observation observation,
        string applicationIdentity,
        Func<Observation, TransitionCheck> transitionPredicate)
    {
        // Foreground ownership must still hold for settled evidence.
        if (!string.Equals(observation.ForegroundApplication, applicationIdentity, StringComparison.Ordinal))
            return new TransitionCheck(false, null);
        return transitionPredicate(observation);
    }

    /// <summary>
    /// POST-SCROLL EVIDENCE-QUALITY SETTLE — bounded re-observe after ONE scroll
    /// dispatch (ScrollForward / ScrollBackward). The immediate post-scroll
    /// Observation is accepted only when it is evidence-quality-valid: every
    /// interaction-relevant structured element carries VALID non-empty
    /// actionable bounds. A malformed mid-fling capture — an interaction-
    /// relevant element with invalid/empty bounds — is
    /// INCOMPLETE_POST_SCROLL_EVIDENCE: the Observation is PROVISIONAL (it
    /// never updates CurrentObservation, never appends as accepted viewport
    /// evidence, never enters normalization / exhaustion / the discovery epoch)
    /// and a bounded re-observe is performed. Valid-bounds textless elements are
    /// NOT treated as transient — they remain genuine UNKNOWN (fail closed).
    /// Budget exhausted -> null (fail closed; ZERO scroll redispatch — one
    /// scroll -> N observations). COMPOSITION_POLICY, not a semantic contract;
    /// no fixed sleep as correctness. Settle owner: the Agent (the Environment
    /// only re-observes).
    /// </summary>
    private const int MaxPostScrollEvidenceObservations = 3;

    private static bool HasIncompletePostScrollEvidence(Observation observation)
    {
        // QUALITY PREDICATE SCOPING (viewport eligibility contract): the
        // predicate targets only elements ELIGIBLE to enter viewport interaction
        // evidence. The structured admission boundary (AdbUiHierarchySource)
        // already excludes NON_ACTIONABLE_STRUCTURAL_ARTIFACTS (invalid / zero-
        // area / off-viewport interactive nodes, e.g. persistent RecyclerView
        // recycled containers with negative-height bounds), so those artifacts
        // can no longer make a capture permanently provisional. The settle
        // mechanism and its bounded budget are retained unchanged; any genuinely
        // actionable incomplete evidence still fails closed.
        foreach (var raw in observation.StructuredElements)
        {
            var interactive = raw.Clickable == true
                || raw.Checkable == true
                || raw.HasSwitchChild == true
                || (raw.Class is not null
                    && (raw.Class.Contains("Switch", StringComparison.Ordinal)
                        || raw.Class.Contains("CheckBox", StringComparison.Ordinal)));
            if (!interactive)
                continue;
            if (raw.Bounds is not { IsValid: true })
                return true; // interaction-relevant but invalid/empty actionable bounds -> incomplete
        }
        return false;
    }

    private async Task<Observation?> SettlePostScrollEvidenceQualityAsync(
        Observation firstPostAction,
        CancellationToken cancellationToken)
    {
        if (!HasIncompletePostScrollEvidence(firstPostAction))
            return firstPostAction;
        var seen = 1;
        var observation = firstPostAction;
        while (seen < MaxPostScrollEvidenceObservations)
        {
            observation = await _observeInitial(cancellationToken);
            seen++;
            if (!HasIncompletePostScrollEvidence(observation))
                return observation;
        }
        return null; // budget exhausted -> fail closed
    }

    private bool TryAcceptBranchInventory(
        RuntimeContainer container,
        Observation current,
        BranchInventoryEvidence inventory,
        SourceNormalizationResult? frozenNormalization,
        out BranchProgressEvidence? progress,
        out string? failure)
    {
        progress = null;
        failure = null;
        var required = inventory.RequiredBranchEvidence;
        if (required is null)
        {
            failure = "Unresolved inventory cannot be accepted.";
            return false;
        }

        var accepted = container.ViewportExplorationObservations;
        if (accepted.IsDefaultOrEmpty
            || accepted[^1].SequenceNumber != current.SequenceNumber
            || !ReferenceEquals(current, container.CurrentObservation)
            || !string.Equals(_belief?.SemanticPage, container.SemanticPageName, StringComparison.Ordinal))
        {
            failure = "Inventory source is not the current accepted semantic Container evidence.";
            return false;
        }

        // CALLER_SOURCE_PROVENANCE_CONTRACT: when the accepted Observations carry
        // structured occurrences, branch acceptance is provenance-driven through
        // the caller's explicit RequiredBranchGrounding ONLY:
        //   BranchSourceGroundingEvidence
        //     -> NavigationSourceOccurrenceReference
        //     -> SourceGroundingValidator (Agent-owned)
        //     -> normalized logical source
        //     -> accept / reject.
        // The BranchIdentity is a caller branch LABEL, never a source identity:
        // acceptance MUST NOT require BranchIdentity == source.Elements.Text nor
        // == StructuredElements.TitleText (OCR/title grounding is forbidden; the
        // OCR channel may drop rows the structured occurrence still carries).
        // Environments WITHOUT structured occurrences (legacy Elements-only
        // fakes) keep the pre-contract identity check below, unchanged.
        var hasStructuredOccurrences = accepted
            .Any(o => !SourceEquivalenceNormalizer.OccurrencesOf(o).IsDefaultOrEmpty);
        ImmutableHashSet<string>? claimedLogicalSources = null;
        SourceNormalizationResult? explicitNormalization = null;

        foreach (var (identity, sequence) in required)
        {
            // EXTERNAL BOUNDARY (EBD): a source whose boundary obligation is
            // already VERIFIED was fully handled by the boundary flow — it must
            // NOT be re-grounded as a conflicting recursive branch on inventory
            // re-acceptance. It is excluded from the pending set separately.
            if (_branchProgress.TryGetValue(container.SemanticPageName, out var boundaryHandledPrior)
                && boundaryHandledPrior.IsBoundaryVerifiedForSource(identity))
            {
                continue;
            }
            var source = accepted.FirstOrDefault(observation => observation.SequenceNumber == sequence);
            if (source is null)
            {
                failure = $"Inventory branch '{identity}' does not reference accepted source evidence seq={sequence}.";
                return false;
            }

            if (hasStructuredOccurrences)
            {
                if (inventory.RequiredBranchGrounding is null
                    || !inventory.RequiredBranchGrounding.TryGetValue(identity, out var occurrenceReference))
                {
                    failure = $"Required branch '{identity}' has no explicit source provenance grounding; zero dispatch.";
                    return false;
                }
                // Post-completeness acceptance reuses the FROZEN discovery
                // normalization (non-monotonic evidence extension): the discovery
                // history is never re-normalized together with later fresh
                // evidence; when no epoch exists (legacy path), compute fresh.
                explicitNormalization ??= frozenNormalization ?? SourceEquivalenceNormalizer.Normalize(accepted);
                var groundingResult = SourceGroundingValidator.Validate(
                    accepted,
                    new BranchSourceGroundingEvidence(identity, occurrenceReference),
                    explicitNormalization,
                    claimedLogicalSources);
                if (groundingResult.Status != SourceGroundingValidator.SourceGroundingStatus.Valid
                    || groundingResult.SourceElementIndex is null)
                {
                    failure = $"Required branch '{identity}' grounding rejected: {groundingResult.Reason}";
                    return false;
                }
                // Claim the resolved logical source so a second branch pointing at
                // the SAME world source is rejected (no duplicate grounding).
                var resolved = SourceGroundingValidator.TryResolveLogicalSource(
                    SourceEquivalenceNormalizer.OccurrencesOf(source)
                        .First(o => string.Equals(o.OccurrenceIdentity, occurrenceReference.OccurrenceLocalIdentity, StringComparison.Ordinal)),
                    explicitNormalization);
                claimedLogicalSources = (claimedLogicalSources ?? ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal))
                    .Add(resolved!);
            }
            else
            {
                // Legacy path UNCHANGED: identity must exist in the referenced
                // accepted observation's Elements (Elements-only environments).
                if (!source.Elements.Any(element => string.Equals(element.Text, identity, StringComparison.Ordinal)))
                {
                    failure = $"Inventory branch '{identity}' does not reference accepted source evidence seq={sequence}.";
                    return false;
                }
            }
        }

        var completed = ImmutableDictionary<string, long>.Empty.WithComparers(StringComparer.Ordinal);
        var authorized = ImmutableDictionary<string, long>.Empty.WithComparers(StringComparer.Ordinal);
        if (_branchProgress.TryGetValue(container.SemanticPageName, out var prior))
        {
            completed = prior.CompletedSiblingEvidence
                .Where(entry => required.ContainsKey(entry.Key))
                .ToImmutableDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            // SIBLING/SUBTREE LEDGER: the AUTHORIZED obligations survive the
            // inventory re-acceptance (a sibling continuation must not lose the
            // already-authorized required children).
            authorized = prior.AuthorizedSiblingEvidence
                .Where(entry => required.ContainsKey(entry.Key))
                .ToImmutableDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        }

        progress = new BranchProgressEvidence(container.SemanticPageName, required, completed, authorized);
        // EXTERNAL BOUNDARY (EBD): preserve the required boundary obligations and
        // verified dispositions across inventory re-acceptance (a return to the
        // parent re-triggers inventory acceptance; the boundary ledger must not
        // be wiped — analogous to the sibling-ledger preservation above).
        if (_branchProgress.TryGetValue(container.SemanticPageName, out var boundaryPrior)
            && (boundaryPrior.RequiredBoundaryObligations.Length > 0 || boundaryPrior.VerifiedBoundaryDispositions.Length > 0))
        {
            progress = progress with
            {
                RequiredBoundaryObligations = boundaryPrior.RequiredBoundaryObligations,
                VerifiedBoundaryDispositions = boundaryPrior.VerifiedBoundaryDispositions,
            };
        }
        _branchProgress = _branchProgress.SetItem(container.SemanticPageName, progress);
        return true;
    }
}
