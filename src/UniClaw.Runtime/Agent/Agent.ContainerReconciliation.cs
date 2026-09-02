using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using RuntimeContainer = UniClaw.Runtime.Container.Container;

namespace UniClaw.Runtime.Agent;

public sealed partial class Agent
{
    /// <summary>
    /// Replaces the sole live V2 state from one already reconciled immutable
    /// belief. The operation is pure at the model boundary: all structural
    /// validation is delegated to the stateless V2 facade and a rejection
    /// leaves the prior state reference unchanged.
    /// </summary>
    private bool TryInitializeV2Belief(WorldBelief candidateBelief)
    {
        if (TryPrepareV2Belief(
                candidateBelief,
                runRef: null,
                legacyTransition: null,
                restoredEntryContext: null,
                out var replacement))
        {
            _containerRuntimeV2State = replacement;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Builds the next immutable V2 state without mutating any Agent or
    /// Container owner.  Reconciliation commit uses this validation-only
    /// seam before accepting local observation evidence.
    /// </summary>
    private bool TryPrepareV2Belief(
        WorldBelief candidateBelief,
        string? runRef,
        ContainerTransition? legacyTransition,
        ContainerEntryContext? restoredEntryContext,
        out ContainerRuntimeV2State? replacement,
        ContainerTransitionClassificationInput? classificationInput = null,
        bool sameContainerContinuity = false)
    {
        replacement = null;
        ArgumentNullException.ThrowIfNull(candidateBelief);
        var prior = _containerRuntimeV2State;
        var sequence = candidateBelief.SourceObservationSequence
            ?? (prior?.EvidenceRevision.Value + 1 ?? 1);
        if (sequence <= 0)
            return false;

        var revision = new SemanticEvidenceRevision(sequence);
        if (prior is not null && revision.Value <= prior.EvidenceRevision.Value)
            return false;

        var priorNode = prior?.CurrentContainer?.NodeRef;
        var priorNodeSemantic = priorNode is { } node
            ? prior?.Graph.Nodes.FirstOrDefault(item => item.NodeRef == node)?.SemanticIdentityCandidate
            : null;
        var matchingNodes = candidateBelief.SemanticPage is null
            ? []
            : prior?.Graph.Nodes
                .Where(node => string.Equals(
                    node.SemanticIdentityCandidate,
                    candidateBelief.SemanticPage,
                    StringComparison.Ordinal))
                .ToArray() ?? [];
        var isVerifiedReturn = classificationInput?.IsVerifiedReturn == true
            || legacyTransition?.Kind == ContainerTransitionKind.VERIFIED_RETURN_TO_ACTIVE_PARENT;
        var isAuthorizedChild = classificationInput?.IsAuthorizedChildEntry == true
            || legacyTransition?.Kind == ContainerTransitionKind.ENTER_CHILD;
        var occurrenceRefValue = legacyTransition?.TransitionRef
            ?? (runRef is { Length: > 0 }
                ? ContainerTransition.DeriveTransitionRef(runRef, $"observation:{sequence}")
                : $"agent-occurrence:{sequence}");
        var returnParentNode = isVerifiedReturn
            && prior?.CurrentContainer?.EntryContext is { } entry
            && matchingNodes.Any(node => node.NodeRef == entry.SourceNodeRef)
            ? entry.SourceNodeRef
            : (ContainerNodeRef?)null;
        var reusesCurrentNode = priorNode is { } existing
            && (string.Equals(priorNodeSemantic, candidateBelief.SemanticPage, StringComparison.Ordinal)
                || (sameContainerContinuity && candidateBelief.SemanticPage is null));
        var destinationNode = returnParentNode
            ?? (reusesCurrentNode
            ? priorNode!.Value
            : matchingNodes.Length == 1 && (legacyTransition is not null || classificationInput is not null)
                ? matchingNodes[0].NodeRef
                : new ContainerNodeRef($"agent-node:{sequence}:{candidateBelief.SemanticPage ?? "unknown"}"));
        var sourceNode = priorNode ?? destinationNode;
        var occurrenceRef = new TransitionOccurrenceRef(
            occurrenceRefValue);
        var triggerRef = classificationInput?.EvidenceRef is { Length: > 0 } inputEvidenceRef
            ? inputEvidenceRef
            : legacyTransition?.EvidenceRef is { Length: > 0 } evidenceRef
            ? evidenceRef
            : $"trigger-occurrence:{occurrenceRef.Value}";
        var observationRef = $"observation:{sequence}";
        var sliceRef = new ContainerSliceRef($"agent-slice:{sequence}");
        var affordanceRef = $"entry-affordance:{occurrenceRef.Value}";
        if (legacyTransition is not null
            && !string.Equals(legacyTransition.FreshObservationRef, observationRef, StringComparison.Ordinal))
            return false;
        var boundary = sourceNode == destinationNode
            ? ContainerTransitionBoundary.SAME_CONTAINER
            : ContainerTransitionBoundary.NEW_CONTAINER;
        var occurrence = new ContainerTransitionOccurrence(
            occurrenceRef,
            observationRef,
            revision,
            boundary,
            isCompleted: true,
            sourceNode,
            triggerRef,
            destinationNode,
            new[] { $"belief:{sequence}" },
            affordanceRef);
        var current = new CurrentContainer(
            destinationNode,
            sliceRef,
            sourceNode == destinationNode
                ? prior?.CurrentContainer?.EntryContext
                : isVerifiedReturn
                    ? restoredEntryContext
                    : new ContainerEntryContext(sourceNode, occurrenceRef));
        var nodes = prior?.Graph.Nodes.Any(node => node.NodeRef == destinationNode) == true
            ? Enumerable.Empty<ContainerGraphNode>()
            : new[] { new ContainerGraphNode(destinationNode, candidateBelief.SemanticPage, evidenceRefs: [$"belief:{sequence}"]) };
        var context = new ContainerRuntimeV2EvidenceContext(
            runRef ?? "agent-live",
            observationRef,
            sequence,
            revision,
            occurrenceRef,
            triggerRef,
            sourceNode,
            destinationNode,
            sliceRef);
        var fastRequest = new FastContainerResolutionRequest(
            revision,
            sliceRef,
            sequence,
            boundary == ContainerTransitionBoundary.SAME_CONTAINER
                ? FastActionPriorKind.STRONG_SAME
                : FastActionPriorKind.MAY_ENTER,
            sourceNode,
            destinationNode,
            independentBoundarySupport: boundary == ContainerTransitionBoundary.NEW_CONTAINER,
            freshSameContainerSupport: boundary == ContainerTransitionBoundary.SAME_CONTAINER,
            triggerDestinationSemanticMatch: candidateBelief.SemanticPage is not null,
            graphCandidates: prior?.Graph.Nodes);
        var slowRequest = new SlowContainerSemanticRequest(
            observationRef,
            revision,
            destinationNode,
            sourceNode,
            triggerRef,
            occurrenceRef);
        var relation = isAuthorizedChild
            ? new ContainerGraphRelation(
                new ContainerRelationRef($"relation:{occurrenceRef.Value}"),
                sourceNode,
                destinationNode,
                affordanceRef)
            : null;
        var input = new ContainerRuntimeV2LifecycleInput(
            prior ?? ContainerRuntimeV2State.Empty,
            context,
            new ContainerRuntimeV2ReductionInput(
                occurrence,
                nodes,
                current,
                relation,
                relation is null
                    ? ContainerRelationEligibility.NOT_ELIGIBLE
                    : ContainerRelationEligibility.ELIGIBLE),
            fastRequest,
            SlowContainerSemanticMode.Disabled,
            slowRequest);
        var started = ContainerRuntimeV2.Start(input);
        if (!started.Accepted || started.SlowAcquisition is null)
            return false;

        var completed = ContainerRuntimeV2.CompleteDisabled(started, started.State);
        if (completed.Accepted)
        {
            replacement = completed.State;
            return true;
        }

        return false;
    }

    /// <summary>Projects the compatibility WorldBelief from accepted V2 evidence.</summary>
    private static WorldBelief? ProjectV2Belief(ContainerRuntimeV2State? state)
    {
        if (state?.CurrentContainer is not { } current)
            return null;
        var node = state.Graph.Nodes.FirstOrDefault(item => item.NodeRef == current.NodeRef);
        var sequence = state.TransitionOccurrences
            .LastOrDefault()
            ?.FreshObservationRef is { } observationRef
            && observationRef.StartsWith("observation:", StringComparison.Ordinal)
            && long.TryParse(observationRef["observation:".Length..], out var parsed)
                ? parsed
                : (long?)null;
        return node?.SemanticIdentityCandidate is { } semanticPage
            ? new (
                semanticPage,
                1f,
                $"语义页面解析为「{semanticPage}」（观测 seq={sequence ?? 0}）。",
                sequence)
            : new (
                null,
                0f,
                $"语义页面 Unknown：观测（seq={sequence ?? 0}）无匹配的语义解析规则（§10 证据不足不得假装确定）。",
                sequence);
    }

    /// <summary>
    /// Immutable outcome of consuming one Runtime V2 semantic correction.
    /// This is a read projection of the Agent-owned progress replacement; it
    /// emits no action, recovery, completion, or authorization decision.
    /// NEW_SYMBOL_JUSTIFICATION: no existing Agent result describes the
    /// exact assessment-bound correction consumption and its idempotent,
    /// fail-closed outcome. Extending a transition result would conflate
    /// world reconciliation with obligation progress.
    /// </summary>
    public sealed record AgentSemanticCorrectionConsumptionResult
    {
        internal AgentSemanticCorrectionConsumptionResult(
            bool accepted,
            bool isIdempotentNoChange,
            bool requiresSeparateOwnerAuthorization,
            bool hasAppliedObligationMutation,
            string? rejectionReason,
            ImmutableDictionary<string, BranchProgressEvidence> progressSnapshot,
            string? intendedPendingCandidate,
            string? observedActualCandidate,
            string? correctionRef)
        {
            Accepted = accepted;
            IsIdempotentNoChange = isIdempotentNoChange;
            RequiresSeparateOwnerAuthorization = requiresSeparateOwnerAuthorization;
            HasAppliedObligationMutation = hasAppliedObligationMutation;
            RejectionReason = rejectionReason;
            ProgressSnapshot = progressSnapshot;
            IntendedPendingCandidate = intendedPendingCandidate;
            ObservedActualCandidate = observedActualCandidate;
            CorrectionRef = correctionRef;
        }

        /// <summary>Gets whether the correction was accepted.</summary>
        public bool Accepted { get; }
        /// <summary>Gets whether acceptance made no progress change.</summary>
        public bool IsIdempotentNoChange { get; }
        /// <summary>Gets whether a separate owner decision is still required.</summary>
        public bool RequiresSeparateOwnerAuthorization { get; }
        /// <summary>Gets whether the Agent progress ledger changed.</summary>
        public bool HasAppliedObligationMutation { get; }
        /// <summary>Gets the explicit fail-closed rejection reason.</summary>
        public string? RejectionReason { get; }
        /// <summary>Gets the immutable Agent progress projection after consumption.</summary>
        public IReadOnlyDictionary<string, BranchProgressEvidence> ProgressSnapshot { get; }
        /// <summary>Gets the owner-supplied intended pending candidate.</summary>
        public string? IntendedPendingCandidate { get; }
        /// <summary>Gets the assessment-derived observed candidate, when available.</summary>
        public string? ObservedActualCandidate { get; }
        /// <summary>Gets the pure correction reference shared with the Runtime view.</summary>
        public string? CorrectionRef { get; }
        /// <summary>Gets the derived consumption reference for this correction.</summary>
        public string? CorrectionConsumptionRef => CorrectionRef;
        /// <summary>Gets whether no action was emitted.</summary>
        public bool HasAction => false;
        /// <summary>Gets whether no recovery effect was emitted.</summary>
        public bool HasRecovery => false;
        /// <summary>Gets whether no goal-evidence mutation was emitted.</summary>
        public bool HasGoalEvidenceMutation => false;
        /// <summary>Gets whether no completion effect was emitted.</summary>
        public bool HasCompletion => false;
    }

    /// <summary>
    /// Consumes one exact Runtime V2 correction at the Agent-owned obligation
    /// boundary. Traversal corrections retract only the exact completion
    /// attribution supplied by the owner; directed wrong-branch evidence is
    /// accepted as a reevaluation input and never marks the observed branch
    /// complete. Duplicate consumption is an immutable no-op.
    /// </summary>
    /// <param name="projection">Unified Runtime V2 read projection containing the correction.</param>
    /// <param name="currentState">Latest immutable state used to reject stale current effects.</param>
    /// <returns>An immutable consumption outcome.</returns>
    public AgentSemanticCorrectionConsumptionResult ConsumeContainerSemanticCorrection(
        ContainerRuntimeV2ReadProjection projection,
        ContainerRuntimeV2State currentState)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(currentState);
        var correction = projection.Correction;
        var ownerContext = projection.EvidenceContext.OwnerContext;

        AgentSemanticCorrectionConsumptionResult Reject(string reason)
            => new(false, false, false, false, reason, _branchProgress, null, null, projection.CorrectionRef);

        if (correction is null || ownerContext is null)
            return Reject("Current V2 projection has no correction and owner context.");

        if (!ownerContext.HasExactEventBinding
            || ownerContext.AttributedCompletionObservationSequence is < 0
            || !string.Equals(ownerContext.RunRef, projection.EvidenceContext.RunRef, StringComparison.Ordinal)
            || correction.ObservationRef != ownerContext.ObservationRef
            || correction.EvidenceRevision != ownerContext.EvidenceRevision
            || correction.TransitionOccurrenceRef != ownerContext.TransitionOccurrenceRef
            || !string.Equals(correction.TriggerOccurrenceRef, ownerContext.TriggerOccurrenceRef, StringComparison.Ordinal)
            || correction.SourceNodeRef != ownerContext.ParentNodeRef
            || correction.NodeRef != ownerContext.DestinationNodeRef
            || ownerContext.CurrentSliceRef != projection.EvidenceContext.CurrentSliceRef)
            return Reject("Correction and owner context are not exactly event-bound.");

        var occurrence = currentState.TransitionOccurrences.FirstOrDefault(item =>
            item.OccurrenceRef == correction.TransitionOccurrenceRef);
        var projectedOccurrence = projection.State.TransitionOccurrences.FirstOrDefault(item =>
            item.OccurrenceRef == correction.TransitionOccurrenceRef);
        if (occurrence is null
            || projectedOccurrence is null
            || !occurrence.IsCompleted
            || !projectedOccurrence.IsCompleted
            || occurrence.EvidenceRevision != correction.EvidenceRevision
            || projectedOccurrence.EvidenceRevision != correction.EvidenceRevision
            || !string.Equals(occurrence.FreshObservationRef, correction.ObservationRef, StringComparison.Ordinal)
            || !string.Equals(projectedOccurrence.FreshObservationRef, correction.ObservationRef, StringComparison.Ordinal)
            || occurrence.SourceNodeRef != correction.SourceNodeRef
            || projectedOccurrence.SourceNodeRef != correction.SourceNodeRef
            || occurrence.DestinationNodeRef != correction.NodeRef
            || projectedOccurrence.DestinationNodeRef != correction.NodeRef
            || !string.Equals(occurrence.TriggerOccurrenceRef, correction.TriggerOccurrenceRef, StringComparison.Ordinal)
            || !string.Equals(projectedOccurrence.TriggerOccurrenceRef, correction.TriggerOccurrenceRef, StringComparison.Ordinal)
            || currentState.EvidenceRevision.CompareTo(correction.EvidenceRevision) < 0)
            return Reject("Correction occurrence is not an accepted exact current occurrence.");

        var identity = ownerContext.IntendedSemantic;
        var correctionRef = projection.CorrectionRef;
        var observedCandidate = ownerContext.Kind == ContainerObligationContextKind.TraversalMisclick
            ? correction.ActualTriggerSemantic
            : correction.CorrectedIdentityCandidate ?? correction.ObservedContainerSemantic;

        if (ownerContext.Kind == ContainerObligationContextKind.DirectedEntryWrongBranch)
        {
            return new AgentSemanticCorrectionConsumptionResult(
                true,
                true,
                true,
                false,
                null,
                _branchProgress,
                identity,
                observedCandidate,
                correctionRef);
        }

        if (correction.AssessmentKind != SlowContainerSemanticAssessmentKind.Correct
            || string.IsNullOrWhiteSpace(correction.ActualTriggerSemantic)
            || string.Equals(correction.ActualTriggerSemantic, identity, StringComparison.Ordinal))
            return new AgentSemanticCorrectionConsumptionResult(
                true,
                true,
                false,
                false,
                null,
                _branchProgress,
                identity,
                observedCandidate,
                correctionRef);

        if (!_branchProgress.TryGetValue(ownerContext.ParentSemanticPage!, out var progress))
            return Reject("Owner obligation scope is not present in Agent progress.");
        if (!progress.ApprovedSiblingEvidence.ContainsKey(identity)
            || !progress.AuthorizedSiblingEvidence.ContainsKey(identity))
            return Reject("Owner intended obligation is not approved and authorized.");

        if (!progress.CompletedSiblingEvidence.TryGetValue(identity, out var completionSequence))
        {
            return new AgentSemanticCorrectionConsumptionResult(
                true,
                true,
                false,
                false,
                null,
                _branchProgress,
                identity,
                observedCandidate,
                correctionRef);
        }

        if (ownerContext.AttributedCompletionObservationSequence is not { } attributed
            || completionSequence != attributed)
            return Reject("Completion attribution does not match the exact owner event.");

        var nextProgress = progress.WithoutCompletedSibling(identity);
        _branchProgress = _branchProgress.SetItem(ownerContext.ParentSemanticPage!, nextProgress);
        return new AgentSemanticCorrectionConsumptionResult(
            true,
            false,
            false,
            true,
            null,
            _branchProgress,
            identity,
            observedCandidate,
            correctionRef);
    }

    /// <summary>
    /// The complete immutable candidate for one accepted fresh observation.
    /// Every ordinary rejection is resolved before this value reaches the
    /// commit seam.  The record contains no transaction owner and is never
    /// retained by the Agent.
    /// </summary>
    private sealed record ContainerReconciliationPreparation(
        string RunId,
        WorldBelief CandidateBelief,
        ContainerRuntimeV2State V2State,
        ActiveContainerContext CandidateContext,
        RuntimeContainer? ObservationContainer,
        Observation? ObservationToAccept,
        bool RecordViewportObservation,
        ImmutableDictionary<string, BranchProgressEvidence>? CandidateProgress,
        ContainerTransition Transition);

    /// <summary>
    /// Validation-only preparation for a fresh location.  This method may
    /// inspect existing evidence, but it never changes Agent, Container,
    /// progress, or trace state.
    /// </summary>
    private bool TryPrepareContainerReconciliation(
        string runId,
        Observation fresh,
        WorldBelief? preparedBelief,
        ActiveContainerContext currentContext,
        ContainerTransitionClassificationInput classificationInput,
        RuntimeContainer? observationContainer,
        bool recordViewportObservation,
        ActiveContainerContext? candidateContext,
        ImmutableDictionary<string, BranchProgressEvidence>? candidateProgress,
        string? expectedEnteredChildObligationIdentity,
        ContainerProgressReplacementIntent progressReplacementIntent,
        out ContainerReconciliationPreparation? preparation,
        out string? failure)
        => TryPrepareContainerReconciliationCore(
            runId,
            fresh,
            preparedBelief,
            currentContext,
            classificationInput,
            observationContainer,
            recordViewportObservation,
            candidateContext,
            candidateProgress,
            expectedEnteredChildObligationIdentity,
            progressReplacementIntent,
            out preparation,
            out failure,
            allowAlreadyAcceptedObservation: false);

    private bool TryPrepareContainerReconciliationCore(
        string runId,
        Observation fresh,
        WorldBelief? preparedBelief,
        ActiveContainerContext currentContext,
        ContainerTransitionClassificationInput classificationInput,
        RuntimeContainer? observationContainer,
        bool recordViewportObservation,
        ActiveContainerContext? candidateContext,
        ImmutableDictionary<string, BranchProgressEvidence>? candidateProgress,
        string? expectedEnteredChildObligationIdentity,
        ContainerProgressReplacementIntent progressReplacementIntent,
        out ContainerReconciliationPreparation? preparation,
        out string? failure,
        bool allowAlreadyAcceptedObservation)
    {
        preparation = null;
        failure = null;
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(fresh);
        ArgumentNullException.ThrowIfNull(currentContext);
        ArgumentNullException.ThrowIfNull(classificationInput);

        if (fresh.SequenceNumber <= 0)
        {
            failure = "Fresh reconciliation observation must have a positive sequence.";
            return false;
        }

        var candidateBelief = preparedBelief ?? Reconcile.FromObservation(fresh, _resolveSemanticPage);
        var verifiedContinuityBelief = preparedBelief is
            { SemanticPage: not null, Confidence: 1f, Evidence: { } continuityEvidence }
            && continuityEvidence.StartsWith("VERIFIED_LOCAL_CONTINUITY:", StringComparison.Ordinal);
        if (preparedBelief is not null
            && (preparedBelief.SourceObservationSequence != fresh.SequenceNumber
                || (preparedBelief.SemanticPage is null && preparedBelief.Confidence != 0f)
                || (preparedBelief.SemanticPage is not null && preparedBelief.Confidence != 1f)
                || !string.Equals(
                    preparedBelief.Evidence,
                    preparedBelief.SemanticPage is null
                        ? $"语义页面 Unknown：观测（seq={fresh.SequenceNumber}）无匹配的语义解析规则（§10 证据不足不得假装确定）。"
                        : $"语义页面解析为「{preparedBelief.SemanticPage}」（观测 seq={fresh.SequenceNumber}）。",
                    StringComparison.Ordinal))
            && !verifiedContinuityBelief)
        {
            failure = "Prepared belief does not match the fresh observation evidence.";
            return false;
        }

        var expectedObservationRef = $"observation:{fresh.SequenceNumber}";
        if (!string.Equals(classificationInput.RunId, runId, StringComparison.Ordinal)
            || !string.Equals(classificationInput.FreshObservationRef, expectedObservationRef, StringComparison.Ordinal)
            || !string.Equals(
                classificationInput.ActiveExecutionContainer,
                currentContext.ActiveExecutionContainer.SemanticPageName,
                StringComparison.Ordinal))
        {
            failure = "Reconciliation classification input does not match the live run, observation, or execution context.";
            return false;
        }

        var expectedParent = currentContext.ActiveAncestorPath.IsDefaultOrEmpty
            ? null
            : currentContext.ActiveAncestorPath[^1].ParentExecutionContainer.SemanticPageName;
        var inputParent = classificationInput.IsAuthorizedChildEntry
            ? currentContext.ActiveExecutionContainer.SemanticPageName
            : expectedParent;
        if (!string.Equals(classificationInput.ActiveParentAtObservation, inputParent, StringComparison.Ordinal)
            || (!classificationInput.IsExternalExit
                && !string.Equals(
                    classificationInput.ToObservedLocation,
                    candidateBelief.SemanticPage,
                    StringComparison.Ordinal))
            || (classificationInput.IsExternalExit
                && !string.Equals(
                    classificationInput.ToObservedLocation,
                    fresh.ForegroundApplication,
                    StringComparison.Ordinal)))
        {
            failure = "Reconciliation classification input does not match the candidate belief or active path.";
            return false;
        }

        var activeObservation = currentContext.ActiveExecutionContainer.CurrentObservation;
        if (activeObservation is not null
            && fresh.SequenceNumber <= activeObservation.SequenceNumber
            && !(allowAlreadyAcceptedObservation
                && fresh.SequenceNumber == activeObservation.SequenceNumber))
        {
            failure = $"Fresh reconciliation observation is stale (seq={fresh.SequenceNumber} <= active seq={activeObservation.SequenceNumber}).";
            return false;
        }

        if (observationContainer is not null)
        {
            var containerObservation = observationContainer.CurrentObservation;
            if (containerObservation is null
                || fresh.SequenceNumber <= containerObservation.SequenceNumber
                || !string.Equals(
                    fresh.ForegroundApplication,
                    _recoveryAnchor?.ApplicationIdentity ?? fresh.ForegroundApplication,
                    StringComparison.Ordinal))
            {
                failure = "Fresh Container observation failed sequence or foreground validation.";
                return false;
            }

            if (recordViewportObservation
                && (!observationContainer.IsStillMine(fresh)
                    || !string.Equals(
                        candidateBelief.SemanticPage,
                        observationContainer.SemanticPageName,
                        StringComparison.Ordinal)))
            {
                failure = "Fresh viewport observation failed same-Container continuity validation.";
                return false;
            }
        }

        var resolvedContext = candidateContext ?? currentContext;
        // V2 owns the accepted occurrence first.  The legacy transition below
        // is only a typed projection of this already-prepared fresh evidence.
        if (!TryPrepareV2Belief(
                candidateBelief,
                runId,
                legacyTransition: null,
                restoredEntryContext: classificationInput.IsVerifiedReturn
                    && !currentContext.ActiveAncestorPath.IsDefaultOrEmpty
                    ? currentContext.ActiveAncestorPath[^1].ParentEntryContext
                    : null,
                out var v2State,
                classificationInput,
                sameContainerContinuity: observationContainer is not null
                    && recordViewportObservation)
            || v2State is null)
        {
            failure = "V2 lifecycle candidate was rejected; reconciliation remains uncommitted.";
            return false;
        }

        var transitionPreparation = ContainerTransitionClassifier.Prepare(classificationInput);
        if (!transitionPreparation.CanCommit)
        {
            failure = transitionPreparation.FailureReason;
            return false;
        }
        var preparedOccurrence = v2State.TransitionOccurrences.LastOrDefault();
        var preparedSourceSemantic = preparedOccurrence?.SourceNodeRef is { } preparedSource
            ? v2State.Graph.Nodes.FirstOrDefault(node => node.NodeRef == preparedSource)?.SemanticIdentityCandidate
            : null;
        var preparedDestinationSemantic = preparedOccurrence?.DestinationNodeRef is { } preparedDestination
            ? v2State.Graph.Nodes.FirstOrDefault(node => node.NodeRef == preparedDestination)?.SemanticIdentityCandidate
            : null;
        if (preparedOccurrence is null
            || !string.Equals(
                preparedOccurrence.OccurrenceRef.Value,
                transitionPreparation.Transition.TransitionRef,
                StringComparison.Ordinal)
            || !string.Equals(
                preparedOccurrence.FreshObservationRef,
                transitionPreparation.Transition.FreshObservationRef,
                StringComparison.Ordinal)
            || preparedOccurrence.EvidenceRevision.Value != fresh.SequenceNumber
            || !string.Equals(
                preparedOccurrence.TriggerOccurrenceRef,
                transitionPreparation.Transition.EvidenceRef,
                StringComparison.Ordinal)
            || (preparedSourceSemantic is not null
                && !string.Equals(
                    preparedSourceSemantic,
                    transitionPreparation.Transition.FromObservedLocation,
                    StringComparison.Ordinal))
            || (!classificationInput.IsExternalExit
                && preparedDestinationSemantic is not null
                && !string.Equals(
                    preparedDestinationSemantic,
                    transitionPreparation.Transition.ToObservedLocation,
                    StringComparison.Ordinal)))
        {
            failure = $"Legacy transition projection does not bind to the accepted V2 occurrence (trigger={preparedOccurrence?.TriggerOccurrenceRef}/{transitionPreparation.Transition.EvidenceRef}; source={preparedSourceSemantic}/{transitionPreparation.Transition.FromObservedLocation}; destination={preparedDestinationSemantic}/{transitionPreparation.Transition.ToObservedLocation}; boundary={preparedOccurrence?.Boundary}).";
            return false;
        }

        if (!IsValidProgressReplacement(
                progressReplacementIntent,
                candidateProgress,
                classificationInput,
                transitionPreparation.Transition,
                currentContext,
                resolvedContext))
        {
            failure = "Candidate progress replacement intent does not match the exact transition and evidence contract.";
            return false;
        }
        var preservesContext = !classificationInput.IsAuthorizedChildEntry
            && !classificationInput.IsVerifiedReturn;
        if (preservesContext && !SameActiveContext(currentContext, resolvedContext))
        {
            failure = "Preserved transition changed the active execution context or path.";
            return false;
        }

        if (classificationInput.IsAuthorizedChildEntry
            && !IsExactChildContext(
                currentContext,
                resolvedContext,
                expectedEnteredChildObligationIdentity,
                classificationInput.ToObservedLocation))
        {
            failure = "Child transition candidate is not exactly the current path plus the authorized child handle.";
            return false;
        }

        if (classificationInput.IsVerifiedReturn
            && !IsExactReturnedContext(currentContext, resolvedContext))
        {
            failure = "Verified return candidate is not exactly one pop to the immediate parent handle.";
            return false;
        }

        var expectedExecution = classificationInput.IsAuthorizedChildEntry
            ? classificationInput.ToObservedLocation
            : classificationInput.IsVerifiedReturn
                ? expectedParent
                : currentContext.ActiveExecutionContainer.SemanticPageName;
        if (!string.Equals(
                resolvedContext.ActiveExecutionContainer.SemanticPageName,
                expectedExecution,
                StringComparison.Ordinal))
        {
            failure = "Candidate execution context does not match the validated transition disposition.";
            return false;
        }

        preparation = new ContainerReconciliationPreparation(
            runId,
            candidateBelief,
            v2State,
            resolvedContext,
            observationContainer,
            observationContainer is null ? null : fresh,
            recordViewportObservation,
            candidateProgress,
            transitionPreparation.Transition);
        return true;
    }

    /// <summary>
    /// The only Agent-owned reconciliation commit seam.  Its inputs have
    /// already passed all ordinary validation; it performs no I/O, awaits,
    /// action, recovery, policy selection, or fallible classification.
    /// </summary>
    private void CommitContainerReconciliation(
        ContainerReconciliationPreparation preparation,
        bool appendStandaloneTrace = true)
    {
        // Build the V2 replacement before mutating Container-local evidence,
        // execution context, progress, or trace.  A stale/invalid candidate
        // therefore fails closed with zero commit across all owners.
        preparation.ObservationContainer?.AcceptPreparedObservation(
            preparation.ObservationToAccept!,
            preparation.RecordViewportObservation);

        _containerRuntimeV2State = preparation.V2State;
        _activeContainerContext = preparation.CandidateContext;
        if (preparation.CandidateProgress is not null)
            _branchProgress = preparation.CandidateProgress;

        // Ordinary same-Container acceptance already has the existing action
        // or viewport evidence record; do not add a second causal/container
        // event.  A boundary return remains visible because its immutable
        // progress replacement carries the verified disposition ledger.
        var retainStandaloneTransition = preparation.Transition.Kind != ContainerTransitionKind.SAME_CONTAINER
            || preparation.CandidateProgress is not null;
        if (appendStandaloneTrace
            && retainStandaloneTransition
            && !_trace.Any(entry => entry.ContainerTransition?.TransitionRef
                == preparation.Transition.TransitionRef))
        {
            _trace.Add(new DecisionRecord(preparation.RunId)
            {
                ContainerTransition = preparation.Transition,
            });
        }
    }

    private bool IsValidProgressReplacement(
        ContainerProgressReplacementIntent intent,
        ImmutableDictionary<string, BranchProgressEvidence>? candidateProgress,
        ContainerTransitionClassificationInput input,
        ContainerTransition transition,
        ActiveContainerContext currentContext,
        ActiveContainerContext resolvedContext)
    {
        if (!Enum.IsDefined(intent))
            return false;
        if (intent == ContainerProgressReplacementIntent.None)
            return candidateProgress is null;
        if (candidateProgress is null
            || string.IsNullOrWhiteSpace(input.CompletenessRef)
            || string.IsNullOrWhiteSpace(input.EvidenceRef))
            return false;
        if (!ProgressLedgerKeysMatch(candidateProgress))
            return false;

        return intent switch
        {
            ContainerProgressReplacementIntent.VerifiedChildReturn
                => input.IsVerifiedReturn
                   && transition.Kind == ContainerTransitionKind.VERIFIED_RETURN_TO_ACTIVE_PARENT
                   && IsContainerCompletenessReference(input.CompletenessRef)
                   && IsObservationReference(input.EvidenceRef)
                   && !currentContext.ActiveAncestorPath.IsDefaultOrEmpty
                   && ReferenceEquals(resolvedContext.ActiveExecutionContainer, currentContext.ActiveAncestorPath[^1].ParentExecutionContainer)
                   && IsExactCompletedSiblingReplacement(currentContext, candidateProgress),
            ContainerProgressReplacementIntent.ExternalBoundaryObserved
                => input.IsExternalExit
                   && transition.Kind == ContainerTransitionKind.EXTERNAL_EXIT
                   && IsBoundaryReference(input.CompletenessRef)
                   && IsObservationReference(input.EvidenceRef)
                   && ReferenceEquals(resolvedContext.ActiveExecutionContainer, currentContext.ActiveExecutionContainer)
                   && IsExactBoundaryObservationReplacement(input, transition, currentContext, candidateProgress),
            ContainerProgressReplacementIntent.ExternalBoundaryReturned
                => !input.IsVerifiedReturn
                   && !input.IsAuthorizedChildEntry
                   && !input.IsExternalExit
                   && transition.Kind == ContainerTransitionKind.SAME_CONTAINER
                   && string.Equals(input.ToObservedLocation, input.ActiveExecutionContainer, StringComparison.Ordinal)
                   && IsBoundaryReference(input.CompletenessRef)
                   && IsObservationReference(input.EvidenceRef)
                   && ReferenceEquals(resolvedContext.ActiveExecutionContainer, currentContext.ActiveExecutionContainer)
                   && IsExactBoundaryReturnReplacement(input, transition, currentContext, candidateProgress),
            _ => false,
        };
    }

    private static bool IsContainerCompletenessReference(string reference)
        => reference.StartsWith("container:", StringComparison.Ordinal)
           && reference.EndsWith(":local-completeness", StringComparison.Ordinal);

    private static bool IsBoundaryReference(string reference)
        => reference.StartsWith("branch-progress:", StringComparison.Ordinal)
           && reference.EndsWith(":boundary", StringComparison.Ordinal);

    private static bool IsObservationReference(string reference)
        => reference.StartsWith("observation:", StringComparison.Ordinal);

    private bool ProgressLedgerKeysMatch(ImmutableDictionary<string, BranchProgressEvidence> candidateProgress)
        => candidateProgress.Count == _branchProgress.Count
           && candidateProgress.Keys.All(_branchProgress.ContainsKey);

    private bool IsExactCompletedSiblingReplacement(
        ActiveContainerContext currentContext,
        ImmutableDictionary<string, BranchProgressEvidence> candidateProgress)
    {
        if (currentContext.ActiveAncestorPath.IsDefaultOrEmpty)
            return false;
        var parentIdentity = currentContext.ActiveAncestorPath[^1].ParentExecutionContainer.SemanticPageName;
        if (!_branchProgress.TryGetValue(parentIdentity, out var current)
            || !candidateProgress.TryGetValue(parentIdentity, out var candidate)
            || !SameStableProgress(current, candidate, includeCompletedSiblings: false))
            return false;
        if (!OnlyProgressEntryMayChange(candidateProgress, parentIdentity))
            return false;
        if (candidate.CompletedSiblingEvidence.Count != current.CompletedSiblingEvidence.Count + 1)
            return false;
        return current.CompletedSiblingEvidence.All(pair =>
                   candidate.CompletedSiblingEvidence.TryGetValue(pair.Key, out var sequence)
                   && sequence == pair.Value)
               && candidate.CompletedSiblingEvidence.Keys
                   .Except(current.CompletedSiblingEvidence.Keys, StringComparer.Ordinal)
                   .All(identity => current.AuthorizedSiblingEvidence.ContainsKey(identity));
    }

    private bool IsExactBoundaryObservationReplacement(
        ContainerTransitionClassificationInput input,
        ContainerTransition transition,
        ActiveContainerContext currentContext,
        ImmutableDictionary<string, BranchProgressEvidence> candidateProgress)
    {
        var parentIdentity = currentContext.ActiveExecutionContainer.SemanticPageName;
        if (!_branchProgress.TryGetValue(parentIdentity, out var current)
            || !candidateProgress.TryGetValue(parentIdentity, out var candidate)
            || candidate.RequiredBoundaryObligations.Length != current.RequiredBoundaryObligations.Length + 1
            || candidate.VerifiedBoundaryDispositions.Length != current.VerifiedBoundaryDispositions.Length)
            return false;
        if (!OnlyProgressEntryMayChange(candidateProgress, parentIdentity))
            return false;
        if (!SameStableProgress(current, candidate, includeBoundaryObligations: false))
            return false;
        var added = candidate.RequiredBoundaryObligations
            .Where(obligation => !current.RequiredBoundaryObligations.Contains(obligation))
            .ToArray();
        return added.Length == 1
               && added[0].State == BoundaryObligationState.Pending
               && added[0].Relation.ParentContainerIdentity == parentIdentity
               && added[0].Relation.ExternalForeground == transition.ToObservedLocation
               && added[0].Relation.SourceObservationSequence > 0
               && added[0].Relation.SourceOccurrenceReference.EndsWith(
                   $"@{added[0].Relation.SourceObservationSequence}", StringComparison.Ordinal)
               && string.Equals(input.ActiveExecutionContainer, parentIdentity, StringComparison.Ordinal);
    }

    private bool IsExactBoundaryReturnReplacement(
        ContainerTransitionClassificationInput input,
        ContainerTransition transition,
        ActiveContainerContext currentContext,
        ImmutableDictionary<string, BranchProgressEvidence> candidateProgress)
    {
        var parentIdentity = currentContext.ActiveExecutionContainer.SemanticPageName;
        if (!_branchProgress.TryGetValue(parentIdentity, out var current)
            || !candidateProgress.TryGetValue(parentIdentity, out var candidate)
            || !TryObservationSequence(transition.FreshObservationRef, out var sequence)
            || candidate.VerifiedBoundaryDispositions.Length != current.VerifiedBoundaryDispositions.Length + 1
            || !SameStableProgress(current, candidate, includeBoundaryObligations: false, includeVerifiedDispositions: false))
            return false;
        if (!OnlyProgressEntryMayChange(candidateProgress, parentIdentity))
            return false;
        var changedToVerified = candidate.RequiredBoundaryObligations
            .Where(obligation => obligation.State == BoundaryObligationState.Verified)
            .Where(obligation => current.RequiredBoundaryObligations.Any(previous =>
                previous.Relation == obligation.Relation && previous.State == BoundaryObligationState.Pending))
            .ToArray();
        var addedDisposition = candidate.VerifiedBoundaryDispositions
            .Where(disposition => !current.VerifiedBoundaryDispositions.Contains(disposition))
            .ToArray();
        return changedToVerified.Length == 1
               && addedDisposition.Length == 1
               && addedDisposition[0].Relation == changedToVerified[0].Relation
               && addedDisposition[0].ReturnedParentIdentity == parentIdentity
               && addedDisposition[0].EvidenceSequence == sequence
               && string.Equals(input.ToObservedLocation, parentIdentity, StringComparison.Ordinal);
    }

    private bool OnlyProgressEntryMayChange(
        ImmutableDictionary<string, BranchProgressEvidence> candidateProgress,
        string changedIdentity)
        => _branchProgress.All(pair =>
            pair.Key == changedIdentity
            || (candidateProgress.TryGetValue(pair.Key, out var candidate)
                && Equals(pair.Value, candidate)));

    private static bool SameStableProgress(
        BranchProgressEvidence current,
        BranchProgressEvidence candidate,
        bool includeBoundaryObligations = true,
        bool includeVerifiedDispositions = true,
        bool includeCompletedSiblings = true)
        => current.ParentSemanticPage == candidate.ParentSemanticPage
           && current.ApprovedSiblingEvidence.SequenceEqual(candidate.ApprovedSiblingEvidence)
           && (!includeCompletedSiblings
               || current.CompletedSiblingEvidence.SequenceEqual(candidate.CompletedSiblingEvidence))
           && current.AuthorizedSiblingEvidence.SequenceEqual(candidate.AuthorizedSiblingEvidence)
           && (!includeBoundaryObligations
               || current.RequiredBoundaryObligations.SequenceEqual(candidate.RequiredBoundaryObligations))
           && (!includeVerifiedDispositions
               || current.VerifiedBoundaryDispositions.SequenceEqual(candidate.VerifiedBoundaryDispositions));

    private static bool TryObservationSequence(string reference, out long sequence)
    {
        if (!reference.StartsWith("observation:", StringComparison.Ordinal))
        {
            sequence = 0;
            return false;
        }
        return long.TryParse(reference["observation:".Length..], out sequence);
    }

    private static bool SameActiveContext(ActiveContainerContext current, ActiveContainerContext candidate)
        => ReferenceEquals(current.ActiveExecutionContainer, candidate.ActiveExecutionContainer)
           && SamePath(current.ActiveAncestorPath, candidate.ActiveAncestorPath);

    private static bool IsExactChildContext(
        ActiveContainerContext current,
        ActiveContainerContext candidate,
        string? expectedObligation,
        string? expectedChildIdentity)
    {
        if (string.IsNullOrWhiteSpace(expectedObligation)
            || string.IsNullOrWhiteSpace(expectedChildIdentity)
            || candidate.ActiveAncestorPath.Length != current.ActiveAncestorPath.Length + 1
            || !SamePathPrefix(current.ActiveAncestorPath, candidate.ActiveAncestorPath))
            return false;

        var entry = candidate.ActiveAncestorPath[^1];
        return ReferenceEquals(entry.ParentExecutionContainer, current.ActiveExecutionContainer)
               && string.Equals(entry.EnteredChildObligationIdentity, expectedObligation, StringComparison.Ordinal)
               && string.Equals(candidate.ActiveExecutionContainer.SemanticPageName, expectedChildIdentity, StringComparison.Ordinal);
    }

    private static bool IsExactReturnedContext(ActiveContainerContext current, ActiveContainerContext candidate)
    {
        if (current.ActiveAncestorPath.IsDefaultOrEmpty
            || candidate.ActiveAncestorPath.Length != current.ActiveAncestorPath.Length - 1
            || !SamePathPrefix(candidate.ActiveAncestorPath, current.ActiveAncestorPath))
            return false;

        return ReferenceEquals(
            candidate.ActiveExecutionContainer,
            current.ActiveAncestorPath[^1].ParentExecutionContainer);
    }

    private static bool SamePathPrefix(
        ImmutableArray<ActiveAncestorPathEntry> prefix,
        ImmutableArray<ActiveAncestorPathEntry> candidate)
    {
        if (candidate.Length < prefix.Length)
            return false;
        for (var index = 0; index < prefix.Length; index++)
        {
            if (!ReferenceEquals(
                    prefix[index].ParentExecutionContainer,
                    candidate[index].ParentExecutionContainer)
                || !string.Equals(
                    prefix[index].EnteredChildObligationIdentity,
                    candidate[index].EnteredChildObligationIdentity,
                    StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static bool SamePath(
        ImmutableArray<ActiveAncestorPathEntry> first,
        ImmutableArray<ActiveAncestorPathEntry> second)
        => first.Length == second.Length && SamePathPrefix(first, second);

    private bool TryCommitFreshContainerObservation(
        string runId,
        Observation fresh,
        RuntimeContainer container,
        string? activeParentAtObservation,
        bool recordViewportObservation,
        string? stepId,
        out string? failure)
    {
        var currentContext = _activeContainerContext
            ?? throw new InvalidOperationException("Fresh Container reconciliation requires an active execution context.");
        var candidateBelief = Reconcile.FromObservation(fresh, _resolveSemanticPage);
        var currentPage = Belief?.SemanticPage ?? currentContext.ActiveExecutionContainer.SemanticPageName;
        var preparationInput = new ContainerTransitionClassificationInput
        {
            RunId = runId,
            FromObservedLocation = currentPage,
            ToObservedLocation = _resolveSemanticPage(fresh),
            ActiveExecutionContainer = currentContext.ActiveExecutionContainer.SemanticPageName,
            ActiveParentAtObservation = activeParentAtObservation,
            FreshObservationRef = $"observation:{fresh.SequenceNumber}",
            CompletenessRef = $"container:{container.SemanticPageName}:local-completeness",
            EvidenceRef = $"observation:{fresh.SequenceNumber}",
        };
        if (!TryPrepareContainerReconciliation(
                runId,
                fresh,
                candidateBelief,
                currentContext,
                preparationInput,
                container,
                recordViewportObservation,
                currentContext,
                candidateProgress: null,
                expectedEnteredChildObligationIdentity: null,
                progressReplacementIntent: ContainerProgressReplacementIntent.None,
                out var preparation,
                out failure))
            return false;

        CommitContainerReconciliation(preparation!);
        return true;
    }

    /// <summary>
    /// Commits a fresh observed-location projection when the local Container
    /// owner has already accepted the same frame.  This is a narrow extension
    /// of the existing reconciliation seam for legacy paths that cannot own a
    /// Container viewport receipt; it is not a second current-state owner.
    /// </summary>
    private bool TryCommitFreshObservedLocation(
        string runId,
        Observation fresh,
        WorldBelief candidateBelief,
        bool sameContainerContinuity,
        out string? failure)
    {
        var currentContext = _activeContainerContext
            ?? throw new InvalidOperationException("Fresh observed-location reconciliation requires an active execution context.");
        var active = currentContext.ActiveExecutionContainer;
        var input = new ContainerTransitionClassificationInput
        {
            RunId = runId,
            FromObservedLocation = Belief?.SemanticPage ?? active.SemanticPageName,
            ToObservedLocation = candidateBelief.SemanticPage,
            ActiveExecutionContainer = active.SemanticPageName,
            ActiveParentAtObservation = currentContext.ActiveAncestorPath.IsDefaultOrEmpty
                ? null
                : currentContext.ActiveAncestorPath[^1].ParentExecutionContainer.SemanticPageName,
            FreshObservationRef = $"observation:{fresh.SequenceNumber}",
            CompletenessRef = $"container:{active.SemanticPageName}:local-completeness",
            EvidenceRef = $"observation:{fresh.SequenceNumber}",
        };
        if (!TryPrepareContainerReconciliationCore(
                runId,
                fresh,
                candidateBelief,
                currentContext,
                input,
                observationContainer: null,
                recordViewportObservation: false,
                candidateContext: currentContext,
                candidateProgress: null,
                expectedEnteredChildObligationIdentity: null,
                progressReplacementIntent: ContainerProgressReplacementIntent.None,
                out var preparation,
                out failure,
                allowAlreadyAcceptedObservation: true))
        {
            return false;
        }

        CommitContainerReconciliation(preparation!, appendStandaloneTrace: false);
        return true;
    }
}
