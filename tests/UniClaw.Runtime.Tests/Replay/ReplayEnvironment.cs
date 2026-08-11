using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Replay;

/// <summary>
/// S2 Observation Replay — replays previously captured Observation/ActionResult
/// sequences through the real graduated Runtime via IEnvironment.
///
/// This is the PRIMARY fast replay mode for real Runtime behavior.
/// Perception is skipped. World responses are pre-recorded.
///
/// Observability: ActionHistory, ObservationHistory, and the underlying
/// replay script are accessible for assertion verification.
/// </summary>
public sealed class ReplayEnvironment : IEnvironment
{
    private readonly ImmutableArray<ReplayStep> _script;
    private readonly List<DeviceAction> _actionHistory = [];
    private readonly List<Observation> _observationHistory = [];
    private int _stepIndex;

    /// <summary>Creates a replay environment from a pre-recorded script.</summary>
    /// <param name="script">Ordered replay steps: (expected actions → observation responses).</param>
    public ReplayEnvironment(ImmutableArray<ReplayStep> script)
    {
        _script = script;
    }

    /// <summary>Actions dispatched by the Runtime against this environment.</summary>
    public IReadOnlyList<DeviceAction> ActionHistory => _actionHistory;

    /// <summary>Observations returned to the Runtime, in order.</summary>
    public IReadOnlyList<Observation> ObservationHistory => _observationHistory;

    /// <summary>The full replay script.</summary>
    public IReadOnlyList<ReplayStep> Script => _script;

    /// <summary>Produces the next recorded observation.</summary>
    public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var observation = _stepIndex < _script.Length
            ? _script[_stepIndex].Observation
            : _script[^1].Observation;

        _observationHistory.Add(observation);
        return Task.FromResult(observation);
    }

    /// <summary>Returns the pre-recorded action result. Does NOT dispatch to a real device.</summary>
    public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _actionHistory.Add(action);

        if (_stepIndex < _script.Length)
        {
            var result = _script[_stepIndex].ActionResult;
            _stepIndex++;
            return Task.FromResult(result);
        }

        // Beyond script: all actions succeed by default
        return Task.FromResult(new ActionResult(
            ActionResultOutcome.Dispatched, action.ToString(), "replay: dispatched (beyond script)"));
    }
}

/// <summary>
/// One step in a replay script: the observation the Runtime sees, and the
/// action result to return when the Runtime dispatches an action in response.
/// </summary>
/// <param name="Observation">The observation returned to the Runtime.</param>
/// <param name="ActionResult">The action result returned when Runtime dispatches.</param>
public sealed record ReplayStep(
    Observation Observation,
    ActionResult ActionResult);

/// <summary>
/// Builder for constructing ReplayEnvironment scripts from recorded observations.
/// </summary>
public static class ReplayScript
{
    /// <summary>Creates a replay script from a sequence of recorded observations.
    /// Each observation is paired with a default Dispatched result.</summary>
    public static ImmutableArray<ReplayStep> FromObservations(
        params Observation[] observations)
        => [.. observations.Select((o, i) => new ReplayStep(
            o,
            new ActionResult(ActionResultOutcome.Dispatched, $"step-{i}", "replay: recorded dispatch")))];

    /// <summary>Creates a replay script with explicit action results.</summary>
    public static ImmutableArray<ReplayStep> FromPairs(
        params (Observation Observation, ActionResult ActionResult)[] pairs)
        => [.. pairs.Select(p => new ReplayStep(p.Observation, p.ActionResult))];
}
