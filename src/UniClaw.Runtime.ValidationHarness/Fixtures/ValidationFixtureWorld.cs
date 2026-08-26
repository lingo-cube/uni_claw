using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.ValidationHarness.Fixtures;

/// <summary>
/// Fixture transition action that triggers a screen change in the deterministic
/// fixture world (Environment-side configuration data; never a Runtime decision).
/// </summary>
public enum FixtureTransitionAction
{
    /// <summary>A Tap on the element changes the world to <see cref="FixtureTransition.NextScreenName"/>.</summary>
    Tap = 0,
}

/// <summary>
/// Element-level transition configuration: "acting on this element switches the
/// world to NextScreenName". The fixture environment applies the physical effect by
/// element identity; it never selects an element for the Runtime.
/// </summary>
/// <param name="Action">Transition-triggering action type (fixture world only supports Tap).</param>
/// <param name="NextScreenName">Screen switched to when the transition triggers.</param>
/// <param name="DispatchOutcome">Fixture-configured dispatch outcome; Delivered keeps the default
/// variant behaviour. TimedOut changes only the transport result, never the world transition.</param>
public sealed record FixtureTransition(
    FixtureTransitionAction Action,
    string NextScreenName,
    ActionResultOutcome DispatchOutcome = ActionResultOutcome.Dispatched);

/// <summary>One screen config: text + optional transition + optional spatial evidence.
/// Element Index = position in the list (0-based stable order — never coordinates).</summary>
public sealed record FixtureElementConfig(
    string Text,
    FixtureTransition? Transition = null,
    ElementBounds? Bounds = null,
    string? PerceptionType = null);

/// <summary>One screen configuration.</summary>
/// <param name="Name">Screen name (transition targets reference this).</param>
/// <param name="ForegroundApplication">Foreground application while this screen is visible; null = unknown.</param>
/// <param name="Elements">Element configuration in stable list order.</param>
public sealed record FixtureScreenConfig(
    string Name,
    string? ForegroundApplication,
    IReadOnlyList<FixtureElementConfig> Elements);

/// <summary>
/// Deterministic fixture IEnvironment for the Validation Harness Tier-A world
/// (modeled on the tests' ScriptedEnvironment; this is a harness-local equivalent
/// and never travels into the test assembly or any production project).
///
/// Guarantees:
/// <list type="bullet">
/// <item>The same action sequence always yields the same observation sequence
/// (deterministic, replayable).</item>
/// <item>Mutable world state (current screen / observation sequence / action
/// history / observation history) is exclusively owned by this fixture
/// (I-2 — fixture-side state, mirroring the Scenario fake ownership).</item>
/// <item>Dispatch outcome ≠ world success: a transition still switches the world; the
/// outcome is reported independently (SC-P3-001 semantics preserved).</item>
/// </list>
///
/// S2 anomaly-injection hooks exist as simple public mutators of fixture graph
/// state (<see cref="InjectUnclassifiableNode"/>, <see cref="InjectPopup"/>,
/// <see cref="InjectExternalBoundary"/>, <see cref="InjectUnexpectedNavigation"/>).
/// Existence only in this increment — they are exercised by a later WorkItem.
/// </summary>
public sealed class ValidationFixtureWorld : IEnvironment
{
    private sealed record ScreenState(string Name, string? ForegroundApplication, List<FixtureElementConfig> Elements);

    private readonly string? _launchNextScreenName;
    private readonly Dictionary<string, ScreenState> _screens;
    private readonly List<DeviceAction> _actionHistory = [];
    private readonly List<Observation> _observationHistory = [];
    private readonly Dictionary<string, List<FixtureElementConfig>> _injectedElements = new(StringComparer.Ordinal);
    private readonly List<FixtureElementConfig> _pendingPopupOverlays = [];
    private readonly Dictionary<long, string> _scheduledScreenTransitions = new();
    private string _currentScreenName;
    private long _sequenceNumber;

    /// <summary>Create a deterministic fixture world.</summary>
    /// <param name="initialScreenName">Screen observed before any LaunchApp.</param>
    /// <param name="launchNextScreenName">Screen reached after LaunchApp; null = launch does not change the screen.</param>
    /// <param name="screens">All screen configs (unique by name).</param>
    public ValidationFixtureWorld(
        string initialScreenName,
        string? launchNextScreenName,
        IEnumerable<FixtureScreenConfig> screens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initialScreenName);
        _launchNextScreenName = launchNextScreenName;
        _screens = (screens ?? throw new ArgumentNullException(nameof(screens)))
            .Select(screen => new ScreenState(
                screen.Name,
                screen.ForegroundApplication,
                (screen.Elements ?? []).ToList()))
            .ToDictionary(screen => screen.Name, StringComparer.Ordinal);
        if (!_screens.ContainsKey(initialScreenName))
            throw new ArgumentException($"Initial screen '{initialScreenName}' is not configured.", nameof(initialScreenName));
        if (_launchNextScreenName is not null && !_screens.ContainsKey(_launchNextScreenName))
            throw new ArgumentException($"Launch target '{_launchNextScreenName}' is not configured.", nameof(launchNextScreenName));
        _currentScreenName = initialScreenName;
    }

    /// <summary>Executed actions in dispatch order (append-only; includes Rejected).</summary>
    public IReadOnlyList<DeviceAction> ActionHistory => _actionHistory;

    /// <summary>Returned Observations in order (deterministic replay evidence).</summary>
    public IReadOnlyList<Observation> ObservationHistory => _observationHistory;

    /// <summary>Screen currently visible in the fixture world.</summary>
    public string CurrentScreenName => _currentScreenName;

    /// <summary>
    /// Number of delivered Taps whose target element has no transition — i.e.
    /// dispatch against a record-only leaf (S1 evidence: record-only leaves must
    /// never be dispatched). Environment-side dispatch record: exactly what the
    /// Runtime dispatched against this world, incremented at delivery time so the
    /// current-screen context at dispatch is preserved (a post-run walk cannot
    /// reconstruct which screen a historical tap targeted).
    /// </summary>
    public int DispatchedTransitionlessTapCount { get; private set; }

    // ── S2 anomaly-injection hooks ───────────────────────────────────────────
    // Each mutates deterministic fixture graph state. They carry no Runtime
    // authority: the injected world facts are observed like any other evidence.

    /// <summary>Adds a persistent element that the fixture semantic capability
    /// refuses to classify (text not in the fixture vocabulary) to the CURRENT
    /// screen. The Runtime sees an unclassifiable node (fail-closed classification).</summary>
    public void InjectUnclassifiableNode(string text = "unclassifiable-control")
    {
        AddInjectedElement(_currentScreenName, new FixtureElementConfig(
            text, null, new ElementBounds(0.05f, 0.50f, 0.45f, 0.60f)));
    }

    /// <summary>Adds a persistent labelled element to the CURRENT screen (S2
    /// popup scenario: an overlay element appears in the world without any
    /// Runtime action).</summary>
    public void InjectPopup(string overlayText = "popup-overlay")
    {
        AddInjectedElement(_currentScreenName, new FixtureElementConfig(
            overlayText, null, new ElementBounds(0.30f, 0.30f, 0.70f, 0.45f)));
    }

    /// <summary>Guarantees an external-foreground screen exists and adds a
    /// labelled element on the CURRENT screen whose Tap transitions to it. The
    /// Runtime boundary path observes an external foreground only when a binding
    /// authorizes the crossing (later WorkItem); here the world fact is simply
    /// present.</summary>
    public void InjectExternalBoundary(
        string elementText = "external-link",
        string externalScreenName = "ExternalApp",
        string externalForeground = "com.external.validation")
    {
        _screens[externalScreenName] = new ScreenState(
            externalScreenName, externalForeground, [new FixtureElementConfig("external-child")]);
        AddInjectedElement(_currentScreenName, new FixtureElementConfig(
            elementText,
            new FixtureTransition(FixtureTransitionAction.Tap, externalScreenName),
            new ElementBounds(0.05f, 0.70f, 0.45f, 0.80f)));
    }

    /// <summary>Schedules an external world navigation: the NEXT observation is
    /// served from a different fixture screen (no Runtime action caused it). The
    /// target screen is created with the fixture foreground when absent.</summary>
    public void InjectUnexpectedNavigation(string unexpectedScreenName = "UnexpectedScreen")
    {
        if (!_screens.ContainsKey(unexpectedScreenName))
        {
            var foreground = _screens[_currentScreenName].ForegroundApplication;
            _screens[unexpectedScreenName] = new ScreenState(
                unexpectedScreenName, foreground, [new FixtureElementConfig("unexpected-leaf")]);
        }

        // Mid-run placement: +1 collides with the launch/initial-grounding
        // observations (the anomaly would surface as a startup foreground
        // mismatch rather than a Run-internal exception). Scheduling further
        // out lands the anomaly after the run's initial reconciliation, inside
        // the autonomous exploration.
        _scheduledScreenTransitions[_sequenceNumber + 4] = unexpectedScreenName;
    }

    /// <inheritdoc />
    public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sequence = ++_sequenceNumber;
        if (_scheduledScreenTransitions.Remove(sequence, out var nextScreen))
        {
            if (!_screens.ContainsKey(nextScreen))
                throw new InvalidOperationException($"Scheduled fixture transition target '{nextScreen}' is not configured.");
            _currentScreenName = nextScreen;
        }

        var screen = _screens[_currentScreenName];
        var elements = new List<ObservedElement>();
        var index = 0;
        foreach (var element in screen.Elements)
        {
            elements.Add(new ObservedElement(element.Text, null, index, element.Bounds, element.PerceptionType));
            index++;
        }

        if (_injectedElements.TryGetValue(_currentScreenName, out var injected))
        {
            foreach (var element in injected)
            {
                elements.Add(new ObservedElement(element.Text, null, index, element.Bounds, element.PerceptionType));
                index++;
            }
        }

        if (_pendingPopupOverlays.Count > 0)
        {
            foreach (var element in _pendingPopupOverlays)
            {
                elements.Add(new ObservedElement(element.Text, null, index, element.Bounds, element.PerceptionType));
                index++;
            }

            _pendingPopupOverlays.Clear();
        }

        var observation = new Observation(elements.ToImmutableArray(), screen.ForegroundApplication, sequence);
        _observationHistory.Add(observation);
        return Task.FromResult(observation);
    }

    /// <inheritdoc />
    public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _actionHistory.Add(action);
        return Task.FromResult(action switch
        {
            DeviceAction.LaunchApp launch => Launch(launch),
            DeviceAction.Tap { TargetElementIndex: { } targetIndex } tap => Tap(tap, targetIndex),
            DeviceAction.Tap { TargetElementIndex: null, TargetBounds: { } bounds } tap =>
                ResolveByBounds(bounds) is int resolvedIndex
                    ? Tap(tap, resolvedIndex)
                    : Rejected(tap, "Tap bounds did not identify exactly one current fixture element."),
            _ => Rejected(action, $"'{action.GetType().Name}' is not a supported fixture action."),
        });
    }

    private ActionResult Launch(DeviceAction.LaunchApp launch)
    {
        if (_launchNextScreenName is { } next && _screens.ContainsKey(next))
            _currentScreenName = next;
        return new ActionResult(ActionResultOutcome.Dispatched, Describe(launch), "launch dispatched");
    }

    private ActionResult Tap(DeviceAction.Tap tap, int targetElementIndex)
    {
        var element = ElementAt(targetElementIndex);
        if (element is null)
        {
            return new ActionResult(
                ActionResultOutcome.Rejected, Describe(tap), $"fixture element index {targetElementIndex} out of range.");
        }

        if (element.Transition is { Action: FixtureTransitionAction.Tap } transition)
        {
            if (_screens.ContainsKey(transition.NextScreenName))
                _currentScreenName = transition.NextScreenName;
            return new ActionResult(
                transition.DispatchOutcome, Describe(tap), DescribeDispatch("tap", transition.DispatchOutcome));
        }

        // Record-only leaf dispatch record (S1): a delivered tap on an element
        // with no transition targets a record-only leaf. The Runtime must never
        // dispatch those; the world records the truth of what was dispatched.
        var result = new ActionResult(ActionResultOutcome.Dispatched, Describe(tap), "tap dispatched");
        DispatchedTransitionlessTapCount++;
        return result;
    }

    private FixtureElementConfig? ElementAt(int index)
    {
        var screen = _screens[_currentScreenName];
        var list = new List<FixtureElementConfig>(screen.Elements);
        if (_injectedElements.TryGetValue(_currentScreenName, out var injected))
            list.AddRange(injected);
        return index >= 0 && index < list.Count ? list[index] : null;
    }

    private int? ResolveByBounds(ElementBounds bounds)
    {
        var screen = _screens[_currentScreenName];
        var list = new List<FixtureElementConfig>(screen.Elements);
        if (_injectedElements.TryGetValue(_currentScreenName, out var injected))
            list.AddRange(injected);
        var matches = list
            .Select((element, index) => (element, index))
            .Where(pair => pair.element.Bounds is { } candidate
                && candidate.X1 == bounds.X1 && candidate.Y1 == bounds.Y1
                && candidate.X2 == bounds.X2 && candidate.Y2 == bounds.Y2)
            .Select(pair => pair.index)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private void AddInjectedElement(string screenName, FixtureElementConfig element)
    {
        if (!_injectedElements.TryGetValue(screenName, out var list))
        {
            list = [];
            _injectedElements[screenName] = list;
        }

        list.Add(element);
    }

    private static ActionResult Rejected(DeviceAction action, string reason) =>
        new(ActionResultOutcome.Rejected, Describe(action), reason);

    private static string Describe(DeviceAction action) => action switch
    {
        DeviceAction.LaunchApp launch => $"LaunchApp({launch.ApplicationId ?? "<unspecified>"})",
        DeviceAction.Tap tap => $"Tap({tap.TargetElementIndex?.ToString() ?? "<unspecified>"})",
        DeviceAction.SetSwitch setSwitch =>
            $"SetSwitch({setSwitch.TargetElementIndex?.ToString() ?? "<unspecified>"}, {setSwitch.TargetState})",
        DeviceAction.ScrollForward => "ScrollForward",
        DeviceAction.ScrollBackward => "ScrollBackward",
        DeviceAction.SystemBack => "SystemBack",
        _ => action.GetType().Name,
    };

    private static string DescribeDispatch(string action, ActionResultOutcome outcome)
        => outcome == ActionResultOutcome.TimedOut
            ? $"{action} timed out after dispatch attempt"
            : $"{action} dispatched";
}