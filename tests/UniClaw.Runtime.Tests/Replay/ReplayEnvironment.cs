using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Replay;

/// <summary>
/// S2 Observation Replay. Observations and external dispatch responses advance independently:
/// observing never consumes a dispatch, and dispatching never fabricates a new observation.
/// Script exhaustion or action divergence fails closed.
/// </summary>
public sealed class ReplayEnvironment : IEnvironment
{
    private readonly ReplayScript _script;
    private readonly List<DeviceAction> _actionHistory = [];
    private readonly List<Observation> _observationHistory = [];
    private int _observationIndex;
    private int _dispatchIndex;

    public ReplayEnvironment(ReplayScript script)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (script.Observations.IsDefaultOrEmpty)
            throw new ArgumentException("Replay requires at least one recorded Observation.", nameof(script));
        _script = script;
    }

    public IReadOnlyList<DeviceAction> ActionHistory => _actionHistory;
    public IReadOnlyList<Observation> ObservationHistory => _observationHistory;
    public ReplayScript Script => _script;

    public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_observationIndex >= _script.Observations.Length)
            throw new InvalidOperationException("Replay observation script exhausted; refusing to fabricate world evidence.");

        var observation = _script.Observations[_observationIndex++];
        _observationHistory.Add(observation);
        return Task.FromResult(observation);
    }

    public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_dispatchIndex >= _script.Dispatches.Length)
            throw new InvalidOperationException("Replay dispatch script exhausted; refusing to fabricate an external response.");

        var dispatch = _script.Dispatches[_dispatchIndex];
        if (dispatch.ExpectedAction != action)
        {
            throw new InvalidOperationException(
                $"Replay action divergence at dispatch {_dispatchIndex}: expected {dispatch.ExpectedAction}, observed {action}.");
        }

        _dispatchIndex++;
        _actionHistory.Add(action);
        return Task.FromResult(dispatch.Result);
    }
}

/// <summary>Immutable executable replay input, compiled only from authoritative manifest fields.</summary>
public sealed record ReplayScript(
    ImmutableArray<Observation> Observations,
    ImmutableArray<ReplayDispatch> Dispatches);

public sealed record ReplayDispatch(DeviceAction ExpectedAction, ActionResult Result);

/// <summary>Bounded adapter from persistent asset contracts to the executable IEnvironment script.</summary>
public static class ReplayScriptFactory
{
    public static ReplayScript FromManifest(HarnessAssetManifest manifest, string replayId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(replayId);

        var errors = HarnessAssetManifestValidator.Validate(manifest);
        if (!errors.IsDefaultOrEmpty)
            throw new InvalidDataException(string.Join(System.Environment.NewLine, errors));

        var replay = manifest.Replays.SingleOrDefault(x => x.ReplayId == replayId)
            ?? throw new InvalidDataException($"Replay '{replayId}' was not found in manifest '{manifest.ManifestId}'.");
        if (replay.Mode != ReplayMode.Observation)
            throw new InvalidDataException($"Replay '{replayId}' is {replay.Mode}; only Observation replay is executable.");

        var frames = manifest.Frames.ToDictionary(x => x.FrameId, StringComparer.Ordinal);
        var observations = replay.FrameIds.Select(frameId =>
            frames[frameId].Observation
            ?? throw new InvalidDataException($"Observation Replay frame '{frameId}' has no Observation.")).ToImmutableArray();
        var dispatches = replay.Dispatches.Select(ToExecutableDispatch).ToImmutableArray();
        return new ReplayScript(observations, dispatches);
    }

    private static ReplayDispatch ToExecutableDispatch(RecordedDispatchAsset dispatch)
    {
        DeviceAction action = dispatch.ExpectedActionKind switch
        {
            "LaunchApp" => new DeviceAction.LaunchApp(dispatch.ApplicationId),
            "Tap" => new DeviceAction.Tap(dispatch.TargetElementIndex, dispatch.TargetBounds),
            "SetSwitch" when dispatch.TargetState is bool targetState
                => new DeviceAction.SetSwitch(dispatch.TargetElementIndex, targetState, dispatch.TargetBounds),
            "ScrollForward" => new DeviceAction.ScrollForward(),
            _ => throw new InvalidDataException(
                $"Dispatch '{dispatch.DispatchId}' has unsupported or incomplete action kind '{dispatch.ExpectedActionKind}'."),
        };
        return new ReplayDispatch(
            action,
            new ActionResult(dispatch.Outcome, dispatch.ActionDescription, dispatch.Info));
    }
}
