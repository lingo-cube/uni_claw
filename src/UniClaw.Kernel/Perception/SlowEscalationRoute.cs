using UniClaw.Kernel.Perception.Fusion;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Kernel.Perception;

/// <summary>
/// Maps the existing bounded escalation decision to the route hint surface.
/// The Focused crop implementation remains an independent prerequisite; this
/// helper only closes the deterministic Focused → Slow hand-off.
/// </summary>
internal static class SlowEscalationRoute
{
    public static ObservationDepth ToObservationDepth(EscalationAction action) => action switch
    {
        EscalationAction.FocusedRescan => ObservationDepth.Focused,
        EscalationAction.DeepPerception => ObservationDepth.Slow,
        _ => ObservationDepth.Normal,
    };
}
