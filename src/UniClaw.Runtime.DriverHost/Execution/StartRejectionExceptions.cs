namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Deterministic start rejection: the request was validated and refused BEFORE any
/// run identity, observability entry, device reservation leak, or execution was
/// created. Semantically distinct from RUN_ACCEPTED_THEN_FAILED, which is
/// observable through existing Kernel truth (snapshot/events) after a runId exists.
/// </summary>
public sealed class RequestRejectedException(string reason) : Exception(reason)
{
    /// <summary>Deterministic, non-fabricated rejection reason.</summary>
    public string Reason => Message;
}

/// <summary>
/// Thrown by the composition-root device factory when a selector has no supported
/// composition mapping (unknown/unsupported device). The execution coordinator
/// maps this to <see cref="RequestRejectedException"/> (REQUEST_REJECTED).
/// </summary>
public sealed class DeviceSelectorUnsupportedException(string selector, string reason)
    : Exception($"device selector '{selector}' is not supported: {reason}")
{
    /// <summary>The unsupported selector key.</summary>
    public string Selector { get; } = selector;
}
