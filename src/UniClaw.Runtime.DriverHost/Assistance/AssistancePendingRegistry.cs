using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Brain;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Bounded pending-request registry for the cross-process Assistance seam
/// (dsh-assistance-provider-adapter). Owns ONLY pending-request lifecycle:
/// PENDING → RESOLVED (matched resolve) or PENDING → EXPIRED (provider timeout /
/// cancellation / provider-side removal). It owns NO intelligence, no Runtime
/// truth, no belief/binding/state.
///
/// Lifecycle safety (A1.1):
///  - resolve validates requestId (entry key), worldVersion (stale → reject), and
///    the Recommendation whitelist (invalid → reject); the request stays pending
///    until timeout on any rejection.
///  - a successful resolve consumes AND removes the entry atomically: a duplicate
///    or late resolve then finds no entry (resolved:false) — it cannot resurrect
///    or mutate anything.
///  - repeated assistance.pending polls are read-only and harmless.
///
/// Capacity and timeout are COMPOSITION_POLICY (not External Contract semantics):
/// capacity defaults to 8; the consult timeout is enforced by the provider
/// (default 30s, injectable for tests).
/// </summary>
public sealed class AssistancePendingRegistry : IAssistanceWireSurface
{
    /// <summary>COMPOSITION_POLICY default: maximum concurrent pending requests.</summary>
    public const int DefaultCapacity = 8;

    /// <summary>Recommendations the Runtime Agent accepts (whitelist mirror).</summary>
    public static readonly ImmutableArray<string> AllowedRecommendations =
        ["re-observe", "rebind", "dismiss-obstruction"];

    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>One pending request (registry-owned; no live Runtime references).</summary>
    public sealed record Entry(
        string RequestId,
        AssistanceContext Context,
        TaskCompletionSource<AssistanceAdvice> Completion,
        DateTimeOffset CreatedAtUtc);

    /// <summary>Construct with the composition-policy capacity (default 8).</summary>
    public AssistancePendingRegistry(int? capacity = null)
        => _capacity = capacity ?? DefaultCapacity;

    /// <summary>Read-only pending digest (repeated polls are harmless).</summary>
    public ImmutableArray<AssistanceRequestDigest> Pending()
    {
        lock (_gate)
        {
            return [.. _entries.Values
                .OrderBy(e => e.CreatedAtUtc)
                .Select(e => AssistanceRequestDigest.From(e.Context))];
        }
    }

    /// <summary>Register a pending request. Returns null when capacity is exhausted
    /// (the provider then fails closed — ConsultAsync returns null).</summary>
    public Entry? TryRegister(AssistanceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_gate)
        {
            if (_entries.Count >= _capacity)
            {
                return null;
            }

            var entry = new Entry(
                context.RequestId,
                context,
                new TaskCompletionSource<AssistanceAdvice>(
                    TaskCreationOptions.RunContinuationsAsynchronously),
                DateTimeOffset.UtcNow);
            _entries[context.RequestId] = entry;
            return entry;
        }
    }

    /// <summary>Remove a pending entry (idempotent; called by the provider on all
    /// terminal paths). A removed entry can never be resolved.</summary>
    public void Remove(string requestId)
    {
        lock (_gate)
        {
            _entries.Remove(requestId);
        }
    }

    /// <summary>
    /// Consume a resolve for one pending request. Validates requestId (entry
    /// existence), worldVersion (stale rejection) and the Recommendation
    /// whitelist. A successful resolve completes the awaiting ConsultAsync with
    /// the advice and removes the entry atomically.
    /// </summary>
    public AssistanceResolveResult Resolve(AssistanceResolveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (!_entries.TryGetValue(request.RequestId, out var entry))
            {
                return AssistanceResolveResult.Rejected(
                    $"unknown or already-terminal request '{request.RequestId}'");
            }

            if (entry.Completion.Task.IsCompleted)
            {
                return AssistanceResolveResult.Rejected(
                    $"request '{request.RequestId}' is already terminal (duplicate/late resolve)");
            }

            if (request.WorldVersion != entry.Context.WorldVersion)
            {
                return AssistanceResolveResult.Rejected(
                    $"stale resolve: world version {request.WorldVersion} != pending {entry.Context.WorldVersion}");
            }

            if (request.Recommendation is not null
                && !AllowedRecommendations.Contains(request.Recommendation, StringComparer.Ordinal))
            {
                return AssistanceResolveResult.Rejected(
                    $"invalid recommendation '{request.Recommendation}' (whitelist: {string.Join(", ", AllowedRecommendations)} or null)");
            }

            // Atomic consume: complete the await AND remove the entry.
            _entries.Remove(request.RequestId);
            var advice = new AssistanceAdvice(
                RequestId: request.RequestId,
                WorldVersion: request.WorldVersion,
                Recommendation: request.Recommendation,
                AdditionalEvidence: request.AdditionalEvidence,
                Reason: request.Reason ?? "harness assistance");
            entry.Completion.TrySetResult(advice);
            return AssistanceResolveResult.Accepted();
        }
    }

    /// <summary>Diagnostic count of pending entries (tests).</summary>
    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }
}

/// <summary>Wire-friendly digest of one pending AssistanceContext (capability-gap
/// context — element summary, never a model prompt, never raw pixels).</summary>
public sealed record AssistanceRequestDigest(
    string RequestId,
    string RunId,
    string SemanticPage,
    SemanticBeliefState BeliefState,
    long WorldVersion,
    long ObservationSequence,
    string? ForegroundApplication,
    int ElementCount,
    ImmutableArray<string> ElementTexts)
{
    public static AssistanceRequestDigest From(AssistanceContext context)
    {
        var observation = context.Observation;
        return new AssistanceRequestDigest(
            context.RequestId,
            context.RunId,
            context.SemanticPage,
            context.BeliefState,
            context.WorldVersion,
            observation.SequenceNumber,
            observation.ForegroundApplication,
            observation.Elements.Length,
            [.. observation.Elements.Select(e => e.Text ?? "")]);
    }
}

/// <summary>One resolve request (wire-shaped).</summary>
public sealed record AssistanceResolveRequest(
    string RequestId,
    long WorldVersion,
    string? Recommendation,
    string? AdditionalEvidence,
    string? Reason);

/// <summary>Result of a resolve attempt (business result, not an RPC error).</summary>
public sealed record AssistanceResolveResult(bool Resolved, string? Diagnostic)
{
    public static AssistanceResolveResult Accepted() => new(true, null);
    public static AssistanceResolveResult Rejected(string diagnostic) => new(false, diagnostic);
}

/// <summary>DriverHost-side wire surface for the two additive assistance methods
/// (implemented by the pending registry).</summary>
public interface IAssistanceWireSurface
{
    ImmutableArray<AssistanceRequestDigest> Pending();

    AssistanceResolveResult Resolve(AssistanceResolveRequest request);
}
