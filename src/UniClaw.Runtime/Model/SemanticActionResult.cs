namespace UniClaw.Runtime.Model;

/// <summary>
/// Result of attempting to lower a SemanticAction to an ExecutionAction.
///
/// This is the outcome of Traversal grounding/lowering — NOT the world effect.
/// Dispatch ≠ Effect. The world must be re-observed to verify the desired state.
///
/// Discriminated union: Dispatched | NoOp | StateUnknown | Unresolved | Invalid.
/// </summary>
public abstract record SemanticActionResult
{
    /// <summary>Successfully lowered to a concrete ExecutionAction. Dispatch ≠ world effect.</summary>
    /// <param name="Action">The DeviceAction to dispatch.</param>
    public sealed record Dispatched(DeviceAction Action) : SemanticActionResult;

    /// <summary>Desired state is already satisfied — no physical action needed.</summary>
    /// <param name="Reason">Why no action is needed.</param>
    public sealed record NoOp(string Reason) : SemanticActionResult;

    /// <summary>Current state is unknown — cannot safely dispatch. State evidence required.</summary>
    /// <param name="Reason">What state evidence is missing.</param>
    public sealed record StateUnknown(string Reason) : SemanticActionResult;

    /// <summary>Grounding is ambiguous — multiple candidates, no unique selection possible.</summary>
    /// <param name="Reason">Why grounding is unresolved.</param>
    public sealed record Unresolved(string Reason) : SemanticActionResult;

    /// <summary>The SemanticAction itself is invalid (missing fields, category mismatch, etc.).</summary>
    /// <param name="Reason">Why the action is invalid.</param>
    public sealed record Invalid(string Reason) : SemanticActionResult;

    private SemanticActionResult() { }
}
