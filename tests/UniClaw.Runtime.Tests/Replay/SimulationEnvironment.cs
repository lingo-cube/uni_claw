using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Replay;

/// <summary>
/// S1 Deterministic Stateful Simulation — a minimal modeled external world
/// that mutates in response to Runtime actions.
///
/// Supports: known boolean state, unknown state, action effect, no-effect,
/// timeout, rejection, index shift, binding disappearance.
///
/// Implements IEnvironment — plugs directly into the graduated Runtime
/// without any architecture changes.
/// </summary>
public sealed class SimulationEnvironment : IEnvironment
{
    private readonly Dictionary<string, SimulatedToggle> _toggles;
    private readonly List<DeviceAction> _actionHistory = [];
    private readonly List<Observation> _observationHistory = [];
    private readonly SimulationConfig _config;
    private long _sequenceNumber;

    /// <summary>Creates a simulation with the given toggle definitions.</summary>
    public SimulationEnvironment(
        ImmutableArray<SimulatedToggle> toggles,
        SimulationConfig? config = null)
    {
        _toggles = toggles.ToDictionary(t => t.ObjectIdentity, StringComparer.Ordinal);
        _config = config ?? SimulationConfig.Default;
    }

    /// <summary>Actions dispatched by the Runtime.</summary>
    public IReadOnlyList<DeviceAction> ActionHistory => _actionHistory;

    /// <summary>Observations returned, in order.</summary>
    public IReadOnlyList<Observation> ObservationHistory => _observationHistory;

    /// <summary>Produces an observation of the current simulated world state.</summary>
    public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var seq = ++_sequenceNumber;

        var elements = ImmutableArray.CreateBuilder<ObservedElement>();
        var index = 0;

        foreach (var toggle in _toggles.Values)
        {
            // Apply any pending index shift
            if (_config.ShiftIndicesAfterObservation == seq)
                index = _config.ShiftedIndices.GetValueOrDefault(toggle.ObjectIdentity, index);

            var bounds = toggle.Bounds ?? new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f);

            // Menu item label
            elements.Add(new ObservedElement(
                toggle.Label, null, index, toggle.LabelBounds, "menuItem"));
            index++;

            // Toggle control — SwitchState reflects current simulated world
            elements.Add(new ObservedElement(
                "", toggle.CurrentState, index, bounds, "toggle"));
            index++;
        }

        // Apply fault: hide binding
        if (_config.HideBindingAtObservation == seq)
        {
            elements.Clear();
            elements.Add(new ObservedElement("Other", null, 0));
        }

        var observation = new Observation(
            elements.ToImmutable(),
            _config.ForegroundApplication,
            seq);

        _observationHistory.Add(observation);
        return Task.FromResult(observation);
    }

    /// <summary>Applies the action to the simulated world and returns the outcome.</summary>
    public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _actionHistory.Add(action);

        // Fault injection: action rejected
        if (_config.RejectActionAtCall == _actionHistory.Count)
            return Task.FromResult(new ActionResult(
                ActionResultOutcome.Rejected, action.ToString(), "sim: injected rejection"));

        // Fault injection: action timed out
        if (_config.TimeoutActionAtCall == _actionHistory.Count)
            return Task.FromResult(new ActionResult(
                ActionResultOutcome.TimedOut, action.ToString(), "sim: injected timeout"));

        return Task.FromResult(action switch
        {
            DeviceAction.LaunchApp => new ActionResult(
                ActionResultOutcome.Dispatched, action.ToString(), "sim: launched"),
            DeviceAction.Tap => new ActionResult(
                ActionResultOutcome.Dispatched, action.ToString(), "sim: tap dispatched"),
            DeviceAction.SetSwitch setSwitch => ApplySetSwitch(setSwitch),
            _ => new ActionResult(
                ActionResultOutcome.Dispatched, action.ToString(), "sim: dispatched"),
        });
    }

    private ActionResult ApplySetSwitch(DeviceAction.SetSwitch setSwitch)
    {
        // Find which toggle this action targets by matching the toggle index
        var target = _toggles.Values.FirstOrDefault(t =>
            t.ToggleElementIndex == setSwitch.TargetElementIndex);

        if (target is null)
        {
            // Try matching by label element index range
            target = _toggles.Values.FirstOrDefault(t =>
                t.ToggleElementIndex == setSwitch.TargetElementIndex);
        }

        // Dispatch-success-world-unchanged fault
        if (_config.WorldUnchangedAfterAction == _actionHistory.Count
            || _config.NeverApplyStateChanges)
            return new ActionResult(
                ActionResultOutcome.Dispatched, Describe(setSwitch),
                "sim: dispatched but world unchanged (injected)");

        // Apply the state change
        if (target is not null && target.CurrentState != setSwitch.TargetState)
        {
            _toggles[target.ObjectIdentity] = target with
            {
                CurrentState = setSwitch.TargetState
            };
        }

        return new ActionResult(
            ActionResultOutcome.Dispatched, Describe(setSwitch),
            $"sim: set-switch dispatched, world={target?.CurrentState}");
    }

    private static string Describe(DeviceAction action) => action switch
    {
        DeviceAction.SetSwitch s => $"SetSwitch(idx={s.TargetElementIndex}, state={s.TargetState})",
        _ => action.ToString() ?? action.GetType().Name,
    };
}

/// <summary>A toggle in the simulated world — label + control pair.</summary>
/// <param name="ObjectIdentity">SemanticObject identity (e.g., "WifiConnectivity").</param>
/// <param name="Label">Display text for the menu item (e.g., "Wi‑Fi").</param>
/// <param name="CurrentState">Current ON/OFF/UNKNOWN state.</param>
/// <param name="LabelBounds">Bounds for the label element.</param>
/// <param name="ToggleElementIndex">The element index assigned to the toggle control.</param>
/// <param name="Bounds">Bounds for the toggle control element.</param>
public sealed record SimulatedToggle(
    string ObjectIdentity,
    string Label,
    bool? CurrentState,
    ElementBounds? LabelBounds = null,
    int ToggleElementIndex = 1,
    ElementBounds? Bounds = null);

/// <summary>Configuration for deterministic fault injection in simulation.</summary>
public sealed record SimulationConfig
{
    public static SimulationConfig Default { get; } = new();

    /// <summary>Foreground application reported in observations.</summary>
    public string ForegroundApplication { get; init; } = "settings";

    /// <summary>Reject the Nth ExecuteAsync call.</summary>
    public int? RejectActionAtCall { get; init; }

    /// <summary>Timeout the Nth ExecuteAsync call.</summary>
    public int? TimeoutActionAtCall { get; init; }

    /// <summary>The Nth action dispatches but world state remains unchanged.</summary>
    public int? WorldUnchangedAfterAction { get; init; }

    /// <summary>Shift element indices starting at this observation number.</summary>
    public long? ShiftIndicesAfterObservation { get; init; }

    /// <summary>New indices by object identity after shift.</summary>
    public ImmutableDictionary<string, int> ShiftedIndices { get; init; }
        = ImmutableDictionary<string, int>.Empty;

    /// <summary>Return an empty observation at this sequence number (binding loss).</summary>
    public long? HideBindingAtObservation { get; init; }

    /// <summary>Never apply state changes — world is permanently stuck.</summary>
    public bool NeverApplyStateChanges { get; init; }
}

/// <summary>Pre-built simulation environments for common scenarios.</summary>
public static class SimulationPresets
{
    /// <summary>Wi-Fi connectivity setting, initially OFF.</summary>
    public static SimulationEnvironment WifiOff()
    {
        var wifi = new SimulatedToggle(
            "WifiConnectivity", "Wi‑Fi", false,
            new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f),
            1,
            new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f));
        return new SimulationEnvironment([wifi]);
    }

    /// <summary>Wi-Fi connectivity setting, initially ON.</summary>
    public static SimulationEnvironment WifiOn()
    {
        var wifi = new SimulatedToggle(
            "WifiConnectivity", "Wi‑Fi", true,
            new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f),
            1,
            new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f));
        return new SimulationEnvironment([wifi]);
    }

    /// <summary>Wi-Fi connectivity setting, UNKNOWN state.</summary>
    public static SimulationEnvironment WifiUnknown()
    {
        var wifi = new SimulatedToggle(
            "WifiConnectivity", "Wi‑Fi", null,
            new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f),
            1,
            new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f));
        return new SimulationEnvironment([wifi]);
    }

    /// <summary>Wi-Fi + Bluetooth, both OFF.</summary>
    public static SimulationEnvironment WifiAndBluetoothOff()
    {
        var wifi = new SimulatedToggle(
            "WifiConnectivity", "Wi‑Fi", false,
            new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f),
            1,
            new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f));
        var bluetooth = new SimulatedToggle(
            "BluetoothConnectivity", "Bluetooth", false,
            new ElementBounds(0.05f, 0.40f, 0.50f, 0.50f),
            3,
            new ElementBounds(0.75f, 0.40f, 0.90f, 0.50f));
        return new SimulationEnvironment([wifi, bluetooth]);
    }
}
