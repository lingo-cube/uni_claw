namespace UniClaw.Kernel.Evidence;

/// <summary>Historical reference to one actual delivery. This value alone grants no admission authority.</summary>
public sealed record AttemptReference(string OwnerId, string ReceiptId, string IntentId, string BindingId,
    string? ReliableAttemptId = null)
{
    internal bool IsValid => Guid.TryParseExact(OwnerId, "N", out _)
        && !string.IsNullOrWhiteSpace(ReceiptId) && !string.IsNullOrWhiteSpace(IntentId)
        && !string.IsNullOrWhiteSpace(BindingId)
        && (ReliableAttemptId is null || !string.IsNullOrWhiteSpace(ReliableAttemptId));
}

/// <summary>Opaque, purpose-bound correlation for one synchronous acquisition. Producers may carry, never mint it.</summary>
public sealed class AttemptObservationLink
{
    internal AttemptObservationLink(object issuer, AttemptReference attempt, IngressKind kind)
    { Issuer = issuer; Attempt = attempt; Kind = kind; }
    public AttemptReference Attempt { get; }
    internal object Issuer { get; }
    internal IngressKind Kind { get; }
    internal bool IsOpen { get; private set; } = true;
    internal void Close() => IsOpen = false;
}
