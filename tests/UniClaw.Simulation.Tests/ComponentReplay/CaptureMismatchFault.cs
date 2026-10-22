namespace UniClaw.Simulation.Tests.ComponentReplay;

internal sealed record FaultInjectionReceipt(int Invocation, string Boundary, string Kind, string Original, string Delivered);

/// <summary>One deterministic protocol-correlation fault, used by both buyers.
/// Mutates only the delivered capture reference; original recording stays immutable.</summary>
internal sealed class CaptureMismatchFault
{
    private int _invocations;
    private readonly List<FaultInjectionReceipt> _receipts = [];
    internal IReadOnlyList<FaultInjectionReceipt> Receipts => _receipts.AsReadOnly();

    internal string Deliver(string captureId, string boundary)
    {
        var invocation = ++_invocations;
        if (invocation != 1) return captureId;
        var delivered = "fault:foreign:" + captureId;
        _receipts.Add(new(invocation, boundary, "capture-mismatch", captureId, delivered));
        return delivered;
    }
}
