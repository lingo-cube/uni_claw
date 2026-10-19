using UniClaw.Kernel.World;

namespace UniClaw.Host;

/// <summary>Read the World-owned interaction anchor, retaining historical pages
/// without assigning their controls to the current frame.</summary>
internal static class CurrentPageAnchor
{
    internal static string? Resolve(WorldBeliefRevision? belief)
    {
        if (belief?.WorldState.TryGetValue(WorldModel.CurrentContainerSubject, out var anchor) == true)
            return !belief.Conflicts.Any(conflict => conflict.Subject == WorldModel.CurrentContainerSubject)
                && belief.Containers?.Count(container => container.Identity.ContainerId == anchor.Value) == 1
                    ? anchor.Value : null;
        return belief?.Containers is { Count: 1 } containers ? containers[0].Identity.ContainerId : null;
    }
}
