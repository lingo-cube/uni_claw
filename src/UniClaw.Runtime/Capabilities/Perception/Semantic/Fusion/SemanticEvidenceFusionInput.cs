using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;

/// <summary>
/// Input to Runtime Evidence Fusion. Carries only evidence/context inputs —
/// Current Observation, Vision Evidence, SemanticEvidence, Container History,
/// and Existing Belief Context. It never carries Goal, Action command, Expected
/// state, or Planning context (falsifier F6). This is evidence input only;
/// it produces no Action, Goal decision, Plan, or World mutation.
/// </summary>
public sealed record SemanticEvidenceFusionInput
{
    /// <summary>The current observation this fusion is evaluating.</summary>
    public Observation CurrentObservation { get; }

    /// <summary>Vision evidence from the current observation (What exists?).</summary>
    public ImmutableArray<ObservedElement> VisionEvidence { get; }

    /// <summary>Semantic evidence to validate (What might this mean?).</summary>
    public ImmutableArray<SemanticEvidence> SemanticEvidence { get; }

    /// <summary>Container history used for freshness/historical-context admission.</summary>
    public ImmutableArray<Observation> ContainerHistory { get; }

    /// <summary>The existing belief context (may be null when no belief exists yet).</summary>
    public WorldBelief? ExistingBelief { get; }

    /// <summary>Known observation sequences (current + container history) for
    /// Observation reference / freshness validation.</summary>
    public ImmutableArray<long> KnownObservationSequences { get; }

    /// <summary>Known Trace ids for Trace reference validation.</summary>
    public ImmutableArray<string> KnownTraceIds { get; }

    /// <summary>Creates a Semantic Evidence Fusion input.</summary>
    public SemanticEvidenceFusionInput(
        Observation currentObservation,
        ImmutableArray<ObservedElement>? visionEvidence = null,
        ImmutableArray<SemanticEvidence>? semanticEvidence = null,
        ImmutableArray<Observation>? containerHistory = null,
        WorldBelief? existingBelief = null,
        ImmutableArray<long>? knownObservationSequences = null,
        ImmutableArray<string>? knownTraceIds = null)
    {
        ArgumentNullException.ThrowIfNull(currentObservation);
        CurrentObservation = currentObservation;
        VisionEvidence = visionEvidence ?? ImmutableArray<ObservedElement>.Empty;
        SemanticEvidence = semanticEvidence ?? ImmutableArray<SemanticEvidence>.Empty;
        ContainerHistory = containerHistory ?? ImmutableArray<Observation>.Empty;
        ExistingBelief = existingBelief;

        ImmutableArray<long> known;
        if (knownObservationSequences is { } provided)
        {
            known = provided;
        }
        else
        {
            var builder = ImmutableArray.CreateBuilder<long>();
            if (containerHistory is not null)
            {
                foreach (var obs in containerHistory)
                {
                    builder.Add(obs.SequenceNumber);
                }
            }
            builder.Add(currentObservation.SequenceNumber);
            known = builder.ToImmutable();
        }
        KnownObservationSequences = known;
        KnownTraceIds = knownTraceIds ?? ImmutableArray<string>.Empty;
    }
}
