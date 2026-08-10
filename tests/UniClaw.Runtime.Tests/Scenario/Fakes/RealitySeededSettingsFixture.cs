using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// Reality-seeded Settings world variant for Wi-Fi desired-state tests.
/// </summary>
internal enum RealitySeededWifiWorld
{
    /// <summary>Wi‑Fi already ON at Internet page (recorded reality — EP-04 sim-replay).</summary>
    AlreadyOn,

    /// <summary>Wi‑Fi OFF → authorized SetSwitch(true) → fresh ON observation (SYNTHETIC transition).</summary>
    OffToOnSynthetic,

    /// <summary>Ambiguous Wi‑Fi candidates on Internet page — both "Wi‑Fi" and "AndroidWifi" match.</summary>
    AmbiguousCandidates,

    /// <summary>Noisy candidates — empty-text menuitems present alongside real candidates.</summary>
    NoisyCandidates,
}

/// <summary>
/// SC-RS-WIFI-001 reality-seeded deterministic world.
///
/// Pages derived from RECORDED_REALITY assets:
///   A3: EP-04 sim-replay export (run 20260805T083146853Z, 4 real pages)
///   A4: E-10 TraceReplay fixtures (real-run-derived depth=3 hierarchy)
///   B1: Real-device golden (PKJ110, WLAN/Wi‑Fi alias)
///
/// The Wi-Fi state-change transition (OFF → ON) is SYNTHETIC — no recorded
/// OFF→ON pair exists in committed assets. Marked explicitly.
/// </summary>
internal static class RealitySeededSettingsFixture
{
    internal const string Intent = "确保 WiFi 已开启";
    internal const string SettingsApp = "com.android.settings";

    // ── Recorded element text from EP-04 sim-replay ──────────────────────────
    // Settings root (16 elements): real duplicates, empty text, subtitle phantom
    // Network & internet (21 elements): real hierarchy
    // Internet (14 elements): Wi‑Fi row, connected SSID, toggle, real noise

    private static ElementConfig E(string text, bool? switchState = null, TransitionConfig? transition = null)
        => new(text, switchState, transition);

    private static TransitionConfig T(string next, ScreenTransitionAction action = ScreenTransitionAction.Tap)
        => new(action, next);

    private static ScreenConfig[] BuildScreens(RealitySeededWifiWorld world)
    {
        var wifiSwitchState = world == RealitySeededWifiWorld.AlreadyOn;
        var wifiPageSwitch = world == RealitySeededWifiWorld.AlreadyOn;

        return new[]
        {
            // ── Page 0: Launcher (recorded — EP-04 root page, 5 elements) ──
            new ScreenConfig("Launcher", null, [
                E("", transition: null),           // empty menuitem
                E("GOoQle", transition: null),     // real OCR artifact
                E("", transition: null),           // empty menuitem
                E("Gallery", transition: null),    // real text
                E("", transition: null),           // empty menuitem
            ]),

            // ── Page 1: Settings root (recorded — EP-04, 16 elements) ──
            new ScreenConfig("SettingsRoot", SettingsApp, [
                E("Settings", transition: null),                    // [0] title text
                E("QSearch settings", transition: null),            // [1] search text ×3
                E("QSearch settings", transition: null),            // [2]
                E("QSearch settings", transition: null),            // [3]
                E("Network&internet", transition: T("NetworkInternet")), // [4] ← real target, duplicate!
                E("", transition: null),                            // [5] empty menuitem
                E("Network&internet", transition: T("NetworkInternet")), // [6] ← DUPLICATE — CP-12 multi-candidate
                E("Connected devices", transition: null),           // [7]
                E("", transition: null),                            // [8] empty menuitem
                E("Bluetooth, pairing", transition: null),          // [9] ← SUBTITLE PHANTOM (VE-05) — menuitem but not navigable!
                E("Apps", transition: null),                        // [10]
                E("", transition: null),                            // [11] empty menuitem
                E("Recent apps,default apps", transition: null),    // [12] OCR variant
                E("Notifications", transition: null),               // [13]
                E("", transition: null),                            // [14] empty menuitem
                E("Notification history, conversations", transition: null), // [15]
            ]),

            // ── Page 2: Network & Internet (recorded — EP-04, 21 elements) ──
            new ScreenConfig("NetworkInternet", SettingsApp, [
                E("Network & internet", transition: null),          // [0] title
                E("Internet", transition: T("InternetPage")),       // [1] ← real target
                E("", transition: null),                            // [2] empty
                E("Internet", transition: T("InternetPage")),       // [3] ← DUPLICATE
                E("SIMs", transition: null),                        // [4]
                E("SIMs", transition: null),                        // [5]
                E("", transition: null),                            // [6] empty
                E("SIMs", transition: null),                        // [7]
                E("SIMs", transition: null),                        // [8]
                E("", false, transition: null),                     // [9] toggle — empty text!
                E("", transition: null),                            // [10] empty menuitem
                E("Airplane mode", transition: null),               // [11]
                E("Hotspot & tethering", transition: null),         // [12]
                E("Hotspot & tethering", transition: null),         // [13]
                E("Off", transition: null),                         // [14]
                E("Data Saver", transition: null),                  // [15]
                E("Data Saver", transition: null),                  // [16]
                E("", transition: null),                            // [17] empty
                E("VPN", transition: null),                         // [18]
                E("", transition: null),                            // [19] empty
                E("VPN", transition: null),                         // [20]
            ]),

            // ── Page 3: Internet (recorded — EP-04, 14 elements + SYNTHETIC wifi page) ──
            new ScreenConfig("InternetPage", SettingsApp, [
                E("Internet", transition: null),                    // [0] title
                E("T-Mobile", transition: null),                   // [1]
                E("", transition: null),                            // [2] empty
                E("", transition: null),                            // [3] empty
                E("T-Mobile", transition: null),                   // [4]
                E("", false, transition: null),                     // [5] toggle — empty text! Mobile data?
                E("Wi‑Fi", transition: T("WifiPage")),             // [6] ← "Wi‑Fi" entry! REAL
                E("", transition: null),                            // [7] empty
                E("AndroidWifi", transition: T("WifiPage")),       // [8] ← connected SSID, also contains "Wi‑Fi"!
                E("", transition: null),                            // [9] empty
                E("Add network", transition: null),                 // [10]
                E("Networkpreferences", transition: null),          // [11]
                E("Wi-Fi doesn't turn backon automatically", transition: null), // [12] ← real text, contains "Wi‑Fi"!
                E("Non-carrier data usage", transition: null),      // [13]
            ]),

            // ── Page 4: Wi‑Fi detail page (SYNTHETIC — no recorded page exists) ──
            // SYNTHETIC_STATE_TRANSITION_PENDING_REALITY_CALIBRATION
            new ScreenConfig("WifiPage", SettingsApp, [
                E("Wi‑Fi", wifiPageSwitch, transition: null),       // [0] Wi‑Fi switch with state
                E("AndroidWifi", transition: null),                 // [1] connected SSID
                E("Auto-connect", true, transition: null),          // [2]
                E("Network preferences", transition: null),         // [3]
            ]),

            // ── Page 5: Wi‑Fi ON (post-toggle, SYNTHETIC) ──
            // SYNTHETIC_STATE_TRANSITION_PENDING_REALITY_CALIBRATION
            new ScreenConfig("WifiOnPage", SettingsApp, [
                E("Wi‑Fi", true, transition: null),                 // [0] ← NOW ON
                E("AndroidWifi", transition: null),                 // [1]
                E("Auto-connect", true, transition: null),          // [2]
                E("Connected devices", transition: null),           // [3]
            ]),
        };
    }

    // ── SetSwitch transition: WifiPage → WifiOnPage ──
    // SYNTHETIC_STATE_TRANSITION_PENDING_REALITY_CALIBRATION
    private static readonly TransitionConfig WifiSetSwitchOn = new(
        ScreenTransitionAction.SetSwitch, "WifiOnPage", TargetState: true);

    internal static RealitySeededWifiRun Create(RealitySeededWifiWorld world)
    {
        // Override Wi‑Fi page with SetSwitch transition for OFF→ON variant
        ScreenConfig[] BuildScreensWithTransition()
        {
            var screens = BuildScreens(world);
            if (world == RealitySeededWifiWorld.OffToOnSynthetic)
            {
                // Replace WifiPage element [0] "Wi‑Fi" with SetSwitch-capable version
                var wifiPageIdx = 4; // index in screens array
                var wifiPage = screens[wifiPageIdx];
                var elements = wifiPage.Elements.ToArray();
                elements[0] = new ElementConfig("Wi‑Fi", false, WifiSetSwitchOn); // OFF → SetSwitch(true) → WifiOnPage
                screens[wifiPageIdx] = new ScreenConfig(wifiPage.Name, wifiPage.ForegroundApplication, [.. elements]);
            }
            return screens;
        }

        var screens = BuildScreensWithTransition();
        var alreadyOn = world == RealitySeededWifiWorld.AlreadyOn;
        var initialScreen = alreadyOn ? "WifiOnPage" : "Launcher";
        var launchScreen = alreadyOn ? "WifiOnPage" : "SettingsRoot";
        var environment = new ScriptedEnvironment(initialScreen, launchScreen, screens);
        var traversal = new RuntimeTraversal(environment);
        var goalEvidence = new List<GoalEvidence>();
        var groundingOrder = new List<int>();
        var postActionSequences = new List<long>();

        var goal = new Goal(
            observation =>
            {
                var satisfied = observation.Elements.Any(
                    element => string.Equals(element.Text, "Wi‑Fi", StringComparison.Ordinal)
                        && element.SwitchState is true);
                var evidence = new GoalEvidence(
                    satisfied,
                    satisfied ? "Wi‑Fi ON confirmed from fresh observation." : "Wi‑Fi ON remains unproven.",
                    observation.SequenceNumber);
                goalEvidence.Add(evidence);
                return evidence;
            },
            (_, candidate) =>
            {
                var authorized = candidate.Text.Contains("Wi‑Fi", StringComparison.Ordinal)
                    || candidate.Text.Contains("Network", StringComparison.Ordinal)
                    || candidate.Text.Contains("Internet", StringComparison.Ordinal);
                return new CandidateAuthorizationEvidence(
                    authorized,
                    authorized ? "safe navigation receipt" : "outside action authority");
            });

        // ── Navigation grounding criterion (Tap) ──
        // Pre-action: text match. Post-action: verify destination page reached.
        var navCriterion = new TargetGroundingCriterion(
            (_, candidate) =>
            {
                groundingOrder.Add(candidate.Index);
                var textMatches = candidate.Text.Contains("Wi‑Fi", StringComparison.Ordinal);
                return new TargetGroundingEvidence(
                    textMatches,
                    textMatches
                        ? $"text='{candidate.Text}' index={candidate.Index} matches Wi‑Fi target."
                        : $"text='{candidate.Text}' index={candidate.Index} does not match Wi‑Fi.");
            },
            observation =>
            {
                postActionSequences.Add(observation.SequenceNumber);
                // Verify we reached the Wi‑Fi detail page (identified by SwitchState-bearing "Wi‑Fi" element).
                var reachedWifiPage = observation.Elements.Any(
                    element => string.Equals(element.Text, "Wi‑Fi", StringComparison.Ordinal)
                        && element.SwitchState is not null);
                var notWrongPage = !observation.Elements.Any(
                    element => string.Equals(element.Text, "Wi‑Fi Calling Settings", StringComparison.Ordinal));
                return reachedWifiPage && notWrongPage
                    ? new TargetGroundingEvidence(true, "Reached Wi‑Fi detail page — navigation grounded correctly.")
                    : new TargetGroundingEvidence(null, "Post-tap destination does not confirm Wi‑Fi detail page.");
            });

        // ── State-change grounding criterion (SetSwitch) ──
        // Pre-action: text match + SwitchState-bearing. Post-action: verify Wi‑Fi ON.
        var switchCriterion = new TargetGroundingCriterion(
            (_, candidate) =>
            {
                groundingOrder.Add(candidate.Index);
                var textMatches = candidate.Text.Contains("Wi‑Fi", StringComparison.Ordinal);
                var stateBearing = candidate.SwitchState is not null;
                return new TargetGroundingEvidence(
                    textMatches && stateBearing,
                    textMatches && stateBearing
                        ? $"text='{candidate.Text}' index={candidate.Index} has state-bearing support for SetSwitch."
                        : $"text='{candidate.Text}' index={candidate.Index} lacks text+state support for SetSwitch.");
            },
            observation =>
            {
                postActionSequences.Add(observation.SequenceNumber);
                var wifiOn = observation.Elements.Any(
                    element => string.Equals(element.Text, "Wi‑Fi", StringComparison.Ordinal)
                        && element.SwitchState is true);
                return wifiOn
                    ? new TargetGroundingEvidence(true, "Fresh Wi‑Fi ON evidence confirms SetSwitch effect.")
                    : new TargetGroundingEvidence(false, "Wi‑Fi remains OFF — SetSwitch did not produce expected effect.");
            });

        var plan = new Plan([
            new PlanStep("Network&internet", "Tap"),
            new PlanStep("Internet", "Tap"),
            new PlanStep("Wi‑Fi", "Tap", TargetGroundingCriterion: navCriterion),
            new PlanStep("Wi‑Fi", "SetSwitch true", TargetGroundingCriterion: switchCriterion),
        ]);
        var envelope = IntentSemanticEnvelope.Project(
            Intent,
            goal,
            new IntentExecutionRepresentation.ClosedWorldConcrete(plan));

        static string? ResolvePage(Observation observation)
        {
            if (!string.Equals(observation.ForegroundApplication, SettingsApp, StringComparison.Ordinal))
                return null;
            if (observation.Elements.Any(e => e.Text == "Wi‑Fi" && e.SwitchState is true)
                && observation.Elements.Any(e => e.Text == "Connected devices"))
                return "WifiOn";
            if (observation.Elements.Any(e => e.Text == "Wi‑Fi" && e.SwitchState is not null)
                && observation.Elements.Any(e => e.Text == "Auto-connect"))
                return "WifiSettings";
            if (observation.Elements.Any(e => e.Text == "Wi‑Fi" || e.Text == "AndroidWifi")
                && observation.Elements.Any(e => e.Text == "T-Mobile" || e.Text == "Add network"))
                return "InternetPage";
            if (observation.Elements.Any(e => e.Text == "Internet" || e.Text == "Airplane mode")
                && observation.Elements.Any(e => e.Text == "SIMs"))
                return "NetworkInternet";
            if (observation.Elements.Any(e => e.Text == "Network&internet" || e.Text == "Bluetooth, pairing"))
                return "SettingsRoot";
            if (observation.Elements.Any(e => e.Text == "GOoQle" || e.Text == "Gallery"))
                return "Launcher";
            return "SettingsRoot"; // fallback for noisy/partial observation
        }

        RuntimeContainer Factory(string page) => new(
            page,
            observation => ResolvePage(observation) == page,
            traversal.ExecuteStep,
            forwardsAuthorizationReceipts: true);

        var startup = new RuntimeStartup(environment, SettingsApp, ResolvePage);
        var recovery = new RuntimeRecovery(
            environment,
            _ => ImmutableArray<DeviceAction>.Empty,
            (_, _) => null,
            (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            token => environment.ObserveAsync(token),
            ResolvePage,
            Factory,
            recovery);

        return new RealitySeededWifiRun(
            agent, traversal, environment, envelope, plan,
            goalEvidence, groundingOrder, postActionSequences, world);
    }
}

internal sealed record RealitySeededWifiRun(
    RuntimeAgent Agent,
    RuntimeTraversal Traversal,
    ScriptedEnvironment Environment,
    IntentSemanticEnvelope.Resolved Envelope,
    Plan Plan,
    IReadOnlyList<GoalEvidence> GoalEvidence,
    IReadOnlyList<int> GroundingOrder,
    IReadOnlyList<long> PostActionSequences,
    RealitySeededWifiWorld World)
{
    internal Task<RunState> RunAsync(string runId)
    {
        if (Envelope.Representation is not IntentExecutionRepresentation.ClosedWorldConcrete closedWorld)
            throw new InvalidOperationException("Requires closed-world concrete representation.");
        return Agent.RunAsync(Envelope.Goal, closedWorld.Plan, runId, CancellationToken.None);
    }
}
