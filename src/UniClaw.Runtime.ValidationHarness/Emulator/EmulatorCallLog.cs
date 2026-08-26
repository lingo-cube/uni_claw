using System.Collections.Immutable;

namespace UniClaw.Runtime.ValidationHarness.Emulator;

/// <summary>Admission/transport outcome recorded in one immutable call-log entry.</summary>
public enum EmulatorCallOutcome
{
    /// <summary>The transport was exercised and admission accepted the run;
    /// <see cref="EmulatorCallLogEntry.Detail"/> carries the DriverHost-owned runId.</summary>
    Accepted = 0,

    /// <summary>The transport was exercised and admission deterministically
    /// rejected the directive; <see cref="EmulatorCallLogEntry.Detail"/> carries
    /// the stable rejection code.</summary>
    RejectedByAdmission = 1,

    /// <summary>The harness rejected the directive before any wire call;
    /// <see cref="EmulatorCallLogEntry.Detail"/> carries the reason.</summary>
    RejectedBeforeTransport = 2,

    /// <summary>Only goal prose was supplied (no directive); zero wire calls;
    /// <see cref="EmulatorCallLogEntry.Detail"/> carries the DIRECTIVE_REQUIRED
    /// marker. The driver never synthesizes a strategy.</summary>
    DirectiveRequired = 3,

    /// <summary>The wire transport failed or the server answered with an RPC
    /// error; <see cref="EmulatorCallLogEntry.Detail"/> carries the reason.</summary>
    TransportFailed = 4,
}

/// <summary>
/// One immutable call-log record (design D5.1, task 3.2): method, SHA-256
/// payload digest of the canonical transported params, admission/validation
/// outcome, and timestamp. Digest is <see cref="string.Empty"/> for entries
/// that carried no payload (DIRECTIVE_REQUIRED).
/// </summary>
public sealed record EmulatorCallLogEntry(
    string Method,
    string PayloadDigest,
    EmulatorCallOutcome Outcome,
    string? Detail,
    DateTimeOffset TimestampUtc)
{
    /// <summary>Directive-required entry: only goal prose, zero wire calls.</summary>
    public static EmulatorCallLogEntry DirectiveRequired(string method, DateTimeOffset timestampUtc)
        => new(
            method,
            string.Empty,
            EmulatorCallOutcome.DirectiveRequired,
            "DIRECTIVE_REQUIRED: only goal prose was supplied; the driver never synthesizes a strategy (design D2, spec 'No strategy inference').",
            timestampUtc);

    /// <summary>Validation-failure entry: rejected before any wire call.</summary>
    public static EmulatorCallLogEntry RejectedBeforeTransport(string method, string payloadDigest, string reason, DateTimeOffset timestampUtc)
        => new(method, payloadDigest, EmulatorCallOutcome.RejectedBeforeTransport, $"REJECTED_BEFORE_TRANSPORT: {reason}", timestampUtc);

    /// <summary>Accepted admission entry; detail = DriverHost-owned runId.</summary>
    public static EmulatorCallLogEntry Accepted(string method, string payloadDigest, string runId, DateTimeOffset timestampUtc)
        => new(method, payloadDigest, EmulatorCallOutcome.Accepted, runId, timestampUtc);

    /// <summary>Deterministic admission-rejection entry; detail = stable rejection code.</summary>
    public static EmulatorCallLogEntry RejectedByAdmission(string method, string payloadDigest, string rejectionCode, DateTimeOffset timestampUtc)
        => new(method, payloadDigest, EmulatorCallOutcome.RejectedByAdmission, $"ADMISSION_REJECT({rejectionCode})", timestampUtc);

    /// <summary>Transport/RPC-failure entry.</summary>
    public static EmulatorCallLogEntry TransportFailed(string method, string payloadDigest, string reason, DateTimeOffset timestampUtc)
        => new(method, payloadDigest, EmulatorCallOutcome.TransportFailed, reason, timestampUtc);
}

/// <summary>
/// Immutable, append-only call log (design D5.1, task 3.2), mirrored on the
/// Runtime's <c>ExplorationLedgerView</c> style: a sealed record over an
/// <see cref="ImmutableArray{T}"/> with sequence-based value equality. Building
/// happens only through <see cref="Append"/>, which PRODUCES A NEW INSTANCE —
/// a built log can never be mutated in place; any attempt to grow the exposed
/// functional array surface also yields new instances and leaves the built log
/// unchanged.
/// </summary>
public sealed record EmulatorCallLog
{
    /// <summary>The single empty log instance.</summary>
    public static EmulatorCallLog Empty { get; } = new(ImmutableArray<EmulatorCallLogEntry>.Empty);

    private EmulatorCallLog(ImmutableArray<EmulatorCallLogEntry> entries) => Entries = entries;

    /// <summary>
    /// Build a new immutable log from an entry sequence. Used by multi-Run
    /// scenarios (S3) to bound each Run's <c>run.strategy.start</c> slice for
    /// per-Run gate and boundary evaluation; entries are shared, never copied.
    /// </summary>
    public static EmulatorCallLog FromEntries(IEnumerable<EmulatorCallLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return new EmulatorCallLog(entries.ToImmutableArray());
    }

    /// <summary>Immutable append-only entry sequence (functional surface only).</summary>
    public ImmutableArray<EmulatorCallLogEntry> Entries { get; }

    /// <summary>Number of recorded entries.</summary>
    public int Count => Entries.Length;

    /// <summary>
    /// Immutable append: returns a NEW log containing all existing entries plus
    /// <paramref name="entry"/>; this instance (and every previously observed
    /// instance) is unchanged.
    /// </summary>
    public EmulatorCallLog Append(EmulatorCallLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new EmulatorCallLog(Entries.Add(entry));
    }

    /// <summary>Sequence-based value equality (ImmutableArray equality is
    /// reference-based; mirror ExplorationLedgerView).</summary>
    public bool Equals(EmulatorCallLog? other)
        => other is not null && Entries.SequenceEqual(other.Entries);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var entry in Entries)
            hash.Add(entry);
        return hash.ToHashCode();
    }
}