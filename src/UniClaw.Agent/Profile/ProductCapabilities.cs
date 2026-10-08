namespace UniClaw.Agent.Profile;

/// <summary>Named Product capability vocabulary. Every legal capability is a
/// named constant here so an unreviewed string cannot enter a manifest.</summary>
public static class ProductCapabilities
{
    /// <summary>The single headless Product output: one Agent task envelope
    /// (initialization plus AgentDecision payload) per consultation.</summary>
    public const string SubmitDecision = "submit_decision";

    /// <summary>Capability names a Product profile must never expose; kept as a
    /// named set so a forbidden capability cannot be smuggled in as an
    /// unreviewed string in an adapter.</summary>
    public static readonly IReadOnlySet<string> ForbiddenProductCapabilities =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "webserver", "interactive-ui", "interactive_ui", "shell", "filesystem",
            "browser", "device", "device-effect", "adb", "click", "swipe", "workflow",
            "subagent", "todo", "task-manager", "task_manager", "ask-user", "ask_user",
            "steer", "inject", "approval", "approval-ui", "approval_ui",
        };
}
