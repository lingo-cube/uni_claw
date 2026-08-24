using System.Collections.Immutable;
using UniClaw.Runtime.Tests.Scenario.Fakes;

namespace UniClaw.Runtime.Tests.Evidence;

/// <summary>
/// PHASE 4 — Settings as an EXTERNAL EVIDENCE FIXTURE.
///
/// The Settings-shaped world is expressed with the SAME generic
/// <see cref="EvidenceFixture"/> model as the generic tree/diamond worlds.
/// Settings vocabulary appears ONLY as fixture data (screens/relations/
/// signals) — never as an execution path, expected click, or hidden answer.
/// The Runtime and the evaluator remain fully scenario-neutral.
///
///   SettingsRoot
///     ├─ Network
///     │    └─ Wi‑Fi (switch-state control; ON/OFF container variants)
///     └─ System
///
/// The goal signal is the root's Wi‑Fi status line shown ON — the Runtime must
/// enter Network, set the Wi‑Fi switch, return, and observe the status change
/// through evidence (mirrors the production type-directed OFF/ON pattern).
/// </summary>
public static class SettingsEvidenceFixture
{
    public const string App = "com.android.settings";
    public const string Root = "SettingsRoot";
    public const string Network = "Network";
    public const string System = "System";
    public const string Wifi = "Wi‑Fi";
    public const string WifiStatus = "Wi‑Fi status";

    public static EvidenceFixture Create() => new(
        RootContainerIdentity: Root,
        Screens:
        [
            new EvidenceScreen("RootOff", IsLaunchTarget: true,
                [
                    new EvidenceElement(Network, TransitionTo: "NetworkOff"),
                    new EvidenceElement(System, TransitionTo: "SystemOff"),
                ], ForegroundApplication: App, ContainerIdentity: Root),
            new EvidenceScreen("RootOn", IsLaunchTarget: false,
                [
                    new EvidenceElement(Network, TransitionTo: "NetworkOn"),
                    new EvidenceElement(System, TransitionTo: "SystemOn"),
                    new EvidenceElement(WifiStatus, SwitchState: true),
                ], ForegroundApplication: App, ContainerIdentity: Root),
            new EvidenceScreen("NetworkOff", IsLaunchTarget: false,
                [
                    new EvidenceElement(Root, TransitionTo: "RootOff"),
                    new EvidenceElement(Wifi, SwitchState: false,
                        TransitionTo: "WifiOn", TransitionAction: ScreenTransitionAction.SetSwitch, TransitionToState: true),
                ], ForegroundApplication: App, ContainerIdentity: Network),
            new EvidenceScreen("WifiOn", IsLaunchTarget: false,
                [
                    new EvidenceElement(Root, TransitionTo: "RootOn"),
                    new EvidenceElement(Wifi, SwitchState: true),
                ], ForegroundApplication: App, ContainerIdentity: Network),
            new EvidenceScreen("SystemOff", IsLaunchTarget: false,
                [
                    new EvidenceElement(Root, TransitionTo: "RootOff"),
                    new EvidenceElement("Device details"),
                ], ForegroundApplication: App, ContainerIdentity: System),
            new EvidenceScreen("SystemOn", IsLaunchTarget: false,
                [
                    new EvidenceElement(Root, TransitionTo: "RootOn"),
                    new EvidenceElement("Device details"),
                ], ForegroundApplication: App, ContainerIdentity: System),
        ],
        ChildRelations:
        [
            new EvidenceRelation(Root, [Network, System]),
            new EvidenceRelation(Network, []),
            new EvidenceRelation(System, []),
        ],
        GoalSignals:
        [
            new EvidenceGoalSignal(Root, WifiStatus),
        ]);

    public static ExpectedSpecification Specification() => new(
        ApplicationIdentity: App,
        RootContainerIdentity: Root,
        RequiredCoverage: ImmutableHashSet.Create(Root, Network, System),
        MaximumDepth: 2,
        RequireGoalEvidenceSatisfied: true,
        IncludeStateChangingControls: true);
}
