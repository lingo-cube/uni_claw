using System.Text.Json.Nodes;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Fixtures;

namespace UniClaw.Runtime.ValidationHarness.Emulator;

/// <summary>
/// Typed outcome of one driver dispatch.
/// </summary>
public abstract record DriverDispatchResult
{
    private DriverDispatchResult()
    {
    }

    /// <summary>Only goal prose was supplied — no directive, no strategy was
    /// synthesized (spec scenario "No strategy inference"; design D2). Zero
    /// wire calls; the outcome is recorded in the call log.</summary>
    public sealed record DirectiveRequired : DriverDispatchResult;

    /// <summary>Validation refused the directive before any wire call (spec
    /// scenario "Forbidden directive content is blocked before transport").
    /// <see cref="Category"/> is set for forbidden payload content, null for
    /// closed-vocabulary/shape violations.</summary>
    public sealed record RejectedBeforeTransport(DirectiveForbiddenCategory? Category, string Reason) : DriverDispatchResult;

    /// <summary>The directive was transported via <c>run.strategy.start</c> and
    /// the admission result (Accept+runId, or deterministic Reject(code)) is in
    /// the call log (spec scenario "Legal directive is transported").</summary>
    public sealed record Transported(StrategyRunAdmissionView Admission) : DriverDispatchResult;

    /// <summary>The wire transport failed or the server answered with an RPC
    /// error; recorded in the call log.</summary>
    public sealed record TransportFailed(string Reason) : DriverDispatchResult;
}

/// <summary>
/// Emulator driver (design D2; tasks 3.1-3.3): the harness's directive
/// transport. It accepts a human-readable Goal plus a <see cref="StrategyDirective"/>
/// — authored live by the agent loop (handoff) or loaded from recorded fixture
/// records (deterministic mode) — validates the directive against the closed
/// Strategy vocabulary (zero wire call on rejection), transports it through the
/// EXISTING <c>run.strategy.start</c> method, and appends one immutable
/// call-log entry per dispatch. There is exactly zero strategy-inference code:
/// a missing directive produces <c>DIRECTIVE_REQUIRED</c>, never a synthesized
/// strategy.
/// </summary>
public sealed class EmulatorDriver
{
    /// <summary>The only wire method the driver ever invokes (frozen surface).</summary>
    public const string StartStrategyMethod = "run.strategy.start";

    private readonly IEmulatorTransport _transport;
    private readonly StrategyDirectiveValidator _validator;
    private EmulatorCallLog _callLog;

    /// <summary>Create a driver over one transport (and, optionally, an
    /// injected validator / starting log for testability).</summary>
    public EmulatorDriver(IEmulatorTransport transport, StrategyDirectiveValidator? validator = null, EmulatorCallLog? initialLog = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
        _validator = validator ?? new StrategyDirectiveValidator();
        _callLog = initialLog ?? EmulatorCallLog.Empty;
    }

    /// <summary>The current immutable call log. Each dispatch appends by
    /// REPLACING the instance; previously observed logs never change (design
    /// D5.1, task 3.2).</summary>
    public EmulatorCallLog CallLog => _callLog;

    /// <summary>
    /// Live mode (design D2): agent-authored directive handoff. Accepts the
    /// human-readable goal (never transported, never inferred from) plus the
    /// caller-provided directive object. A null <paramref name="directive"/>
    /// (goal-only input) yields <see cref="DriverDispatchResult.DirectiveRequired"/>.
    /// </summary>
    public Task<DriverDispatchResult> StartAsync(
        string goal,
        StrategyDirective? directive,
        string device,
        CancellationToken cancellationToken = default)
        => DispatchAsync(goal, directive, device, cancellationToken);

    /// <summary>
    /// Deterministic mode (design D2): a recorded goal → directive fixture
    /// record. Only the directive SOURCE differs from live mode; the
    /// validation/transport/log path is identical.
    /// </summary>
    public Task<DriverDispatchResult> StartAsync(DirectiveFixtureRecord fixture, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        return DispatchAsync(fixture.Goal, fixture.Directive, fixture.Device, cancellationToken);
    }

    private async Task<DriverDispatchResult> DispatchAsync(
        string goal,
        StrategyDirective? directive,
        string device,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(goal);

        // Spec "No strategy inference": only goal prose — DIRECTIVE_REQUIRED,
        // zero wire calls, logged. The driver NEVER synthesizes a strategy.
        if (directive is null)
        {
            _callLog = _callLog.Append(EmulatorCallLogEntry.DirectiveRequired(StartStrategyMethod, DateTimeOffset.UtcNow));
            return new DriverDispatchResult.DirectiveRequired();
        }

        if (string.IsNullOrWhiteSpace(device))
        {
            // The frozen wire parse would refuse an empty selector as
            // bad_request; refuse deterministically before transport instead.
            return RejectBeforeTransport(
                "transport device selector must not be empty (the frozen run.strategy.start parse would refuse it).",
                category: null,
                payloadDigest: string.Empty);
        }

        // Canonical payload: exactly what the transport will carry (digest basis).
        var strategyJson = StrategyPayloadJson.Freeze(directive);
        var parameters = StrategyPayloadJson.BuildParameters(strategyJson, device);
        var digest = StrategyPayloadJson.CanonicalDigest(parameters);

        // Closed-vocabulary + forbidden-content validation BEFORE any wire call.
        if (_validator.Validate(strategyJson) is DirectiveValidationResult.Rejected rejected)
        {
            return RejectBeforeTransport(rejected.Reason, rejected.Category, digest);
        }

        // Transport through the existing run.strategy.start (frozen wire method).
        JsonObject response;
        try
        {
            response = await _transport.SendAsync(StartStrategyMethod, parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _callLog = _callLog.Append(EmulatorCallLogEntry.TransportFailed(StartStrategyMethod, digest, ex.Message, DateTimeOffset.UtcNow));
            return new DriverDispatchResult.TransportFailed(ex.Message);
        }

        if (response["error"] is JsonObject error)
        {
            var message = error["message"]?.GetValue<string>() ?? "unrecognized RPC error";
            _callLog = _callLog.Append(EmulatorCallLogEntry.TransportFailed(StartStrategyMethod, digest, message, DateTimeOffset.UtcNow));
            return new DriverDispatchResult.TransportFailed(message);
        }

        StrategyRunAdmissionView admission;
        try
        {
            admission = StrategyRunAdmissionView.FromWire(response["result"] as JsonObject);
        }
        catch (ArgumentException ex)
        {
            _callLog = _callLog.Append(EmulatorCallLogEntry.TransportFailed(StartStrategyMethod, digest, $"malformed admission receipt: {ex.Message}", DateTimeOffset.UtcNow));
            return new DriverDispatchResult.TransportFailed($"malformed admission receipt: {ex.Message}");
        }

        _callLog = admission.Accepted
            ? _callLog.Append(EmulatorCallLogEntry.Accepted(StartStrategyMethod, digest, admission.RunId ?? string.Empty, DateTimeOffset.UtcNow))
            : _callLog.Append(EmulatorCallLogEntry.RejectedByAdmission(StartStrategyMethod, digest, admission.RejectionCode ?? "unknown", DateTimeOffset.UtcNow));
        return new DriverDispatchResult.Transported(admission);

        DriverDispatchResult RejectBeforeTransport(string reason, DirectiveForbiddenCategory? category, string payloadDigest)
        {
            _callLog = _callLog.Append(EmulatorCallLogEntry.RejectedBeforeTransport(StartStrategyMethod, payloadDigest, reason, DateTimeOffset.UtcNow));
            return new DriverDispatchResult.RejectedBeforeTransport(category, reason);
        }
    }
}