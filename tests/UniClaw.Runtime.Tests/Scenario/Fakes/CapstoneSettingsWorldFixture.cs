using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// SC-S0-CAPSTONE-001 Task 1.1 deterministic external-world fixture (test-side; production purchase = zero).
///
/// The world scripts ONLY external evidence and dispatch outcomes: visible elements, world transitions,
/// Observation data, one local Popup/Overlay obstruction, and one external drift to Launcher/desktop,
/// all scheduled at deterministic observation points. It exposes the approved semantic navigation tree
/// (14 safe Settings pages across depths 0..4, fully traversable within the depth bound 4) as world
/// metadata, plus the frozen bounded evidence-expression evaluators (CAND-008 depth-bounded inventory,
/// CAND-006 candidate authorization, CAND-007 bounded viewport exploration) exactly like the frozen
/// capability fixtures.
///
/// The fixture MUST NOT encode production conclusions: it holds no Container identity, no Recovery
/// authority, no progress completion, and no Goal success. Its Plan is empty (no pre-enumerated
/// page/action route). RunAsync executes a fixture-verification walk only — it is NOT the route the
/// Runtime must follow; the integration harness (Task 2.1) discovers its own route from fresh world
/// evidence. The dangerous mutation candidate is visible world data that is denied by the CAND-006
/// authorization evidence and has no approved world transition (visible candidate != approved
/// executable action is a fixture property, not a production conclusion).
///
/// Inputs: traversal intent, allowed scope, depth bound, safety constraints, and the deterministic
/// disturbance schedule (exactly one Popup observation sequence + dismiss target, exactly one Launcher
/// drift observation sequence). Equal inputs replay an equal world.
/// </summary>
public sealed class CapstoneSettingsWorldFixture
{
    public const string DefaultRunId = "sc-s0-capstone-001-fixture-run";
    public const int DefaultDepthBound = 4;
    public const long DefaultPopupObservationSequence = 13;
    public const long DefaultDriftObservationSequence = 17;
    public const string TargetApplication = "Settings";
    public const string DefaultTraversalIntent = "Traverse approved read-only Settings branches within the depth bound.";
    public const string DefaultAllowedScope = "Settings";
    public const string DefaultSafetyConstraint = "No destructive state-mutating dispatch";

    /// <summary>Visible destructive state-mutating mutation candidate (reset/erase equivalent).</summary>
    public const string DangerousCandidateText = "Erase all data (factory reset)";
    public const string RecoveredEvidenceText = "Saved networks: 2 retained";
    public const string PopupOverlayText = "Update available";
    public const string DismissText = "Dismiss";
    public const string IndependentGoalEvidenceText = "Independent goal evidence";
    public const string ViewportContentText = "More saved networks";
    public const string SettingsTraversalSummaryText = "Settings traversal summary";
    public const string NetworkTraversalSummaryText = "Network traversal summary";
    public const string DisplayTraversalSummaryText = "Display traversal summary";
    public const string SystemTraversalSummaryText = "System traversal summary";

    // Approved branch identities — the world's approved semantic navigation tree (keys of the
    // CAND-008 inventory evidence). The dangerous candidate is NOT one of them.
    public const string NetworkInternetText = "Network & Internet";
    public const string DisplayText = "Display";
    public const string SystemResetText = "System & reset";
    public const string WifiText = "Wi-Fi";
    public const string HotspotTetheringText = "Hotspot & tethering";
    public const string PortableHotspotText = "Portable hotspot";
    public const string SavedNetworksText = "Saved networks";
    public const string WifiPreferencesText = "Wi-Fi preferences";
    public const string WifiCallingText = "Wi-Fi calling";
    public const string BrightnessLevelText = "Brightness level";
    public const string FontSizeText = "Font size";
    public const string ResetOptionsText = "Reset options";
    public const string BackupText = "Backup";

    // Semantic page / screen identities (world data). Element Index = position in the screen's
    // element list, stable across Observations. SavedNetworksPage2 is the same semantic page as
    // SavedNetworksPage (one bounded forward viewport movement, SC-P3-003); SettingsRecovered is the
    // re-entered trusted root after Launcher drift (SC-P2-001 world side).
    public const string SettingsRootScreen = "SettingsRoot";
    public const string NetworkScreen = "NetworkPage";
    public const string WifiScreen = "WifiPage";
    public const string SavedNetworksScreen = "SavedNetworksPage";
    public const string WifiPrefsScreen = "WifiPrefsPage";
    public const string WifiCallingScreen = "WifiCallingPage";
    public const string HotspotScreen = "HotspotPage";
    public const string PortableHotspotScreen = "PortableHotspotPage";
    public const string DisplayScreen = "DisplayPage";
    public const string BrightnessScreen = "BrightnessPage";
    public const string FontSizeScreen = "FontSizePage";
    public const string SystemScreen = "SystemPage";
    public const string ResetOptionsScreen = "ResetOptionsPage";
    public const string BackupScreen = "BackupPage";

    private const string SavedNetworksViewportScreen = "SavedNetworksPage2";
    private const string SettingsRootViewportScreen = "SettingsRootSummary";
    private const string PopupOverlayScreen = "PopupOverlay";
    private const string LauncherScreen = "Launcher";
    private const string SettingsRecoveredScreen = "SettingsRecovered";

    private static readonly ImmutableArray<string> ApprovedBranchTexts = ImmutableArray.Create(
        NetworkInternetText, DisplayText, SystemResetText, WifiText, HotspotTetheringText,
        PortableHotspotText, SavedNetworksText, WifiPreferencesText, WifiCallingText,
        BrightnessLevelText, FontSizeText, ResetOptionsText, BackupText);

    /// <summary>
    /// Approved semantic navigation tree metadata: page identity → semantic depth → required branch
    /// identities. Leaves have an empty required-branch list. Depth 4 (Wi-Fi calling) is the deepest
    /// approved page; the depth bound 4 therefore traverses every approved branch.
    /// </summary>
    private static readonly ImmutableArray<S0SemanticPage> TreePages = ImmutableArray.Create(
        new S0SemanticPage(SettingsRootScreen, 0, [NetworkInternetText, DisplayText, SystemResetText]),
        new S0SemanticPage(NetworkScreen, 1, [WifiText, HotspotTetheringText]),
        new S0SemanticPage(DisplayScreen, 1, [BrightnessLevelText, FontSizeText]),
        new S0SemanticPage(SystemScreen, 1, [ResetOptionsText, BackupText]),
        new S0SemanticPage(WifiScreen, 2, [SavedNetworksText, WifiPreferencesText]),
        new S0SemanticPage(HotspotScreen, 2, [PortableHotspotText]),
        new S0SemanticPage(BrightnessScreen, 2, []),
        new S0SemanticPage(FontSizeScreen, 2, []),
        new S0SemanticPage(ResetOptionsScreen, 2, []),
        new S0SemanticPage(BackupScreen, 2, []),
        new S0SemanticPage(SavedNetworksScreen, 3, []),
        new S0SemanticPage(WifiPrefsScreen, 3, [WifiCallingText]),
        new S0SemanticPage(PortableHotspotScreen, 3, []),
        new S0SemanticPage(WifiCallingScreen, 4, []));

    /// <summary>
    /// Fixture-verification walk (29 actions, 30 Observations). Deterministic and schedule-consistent
    /// (Popup at seq 13, Launcher drift at seq 17): root → Network → Hotspot → Portable hotspot →
    /// Wi-Fi → Saved networks (+ viewport) → Wi-Fi preferences → Wi-Fi calling (deepest, depth 4) →
    /// [Popup] → Wi-Fi preferences → Wi-Fi → Network → root → [Launcher drift] → recovered root →
    /// Display subtree → System & reset subtree (dangerous candidate visible, never dispatched) → root.
    /// This walk is fixture-side verification data, NOT the route the Runtime must take (Task 2.1).
    /// </summary>
    private static readonly ImmutableArray<DeviceAction> VerificationActions = ImmutableArray.Create<DeviceAction>(
        new DeviceAction.Tap(0),   //  1 SettingsRoot → NetworkPage
        new DeviceAction.Tap(1),   //  2 NetworkPage → HotspotPage
        new DeviceAction.Tap(0),   //  3 HotspotPage → PortableHotspotPage
        new DeviceAction.Tap(1),   //  4 PortableHotspotPage → HotspotPage
        new DeviceAction.Tap(1),   //  5 HotspotPage → NetworkPage
        new DeviceAction.Tap(0),   //  6 NetworkPage → WifiPage
        new DeviceAction.Tap(0),   //  7 WifiPage → SavedNetworksPage
        new DeviceAction.ScrollForward(), //  8 viewport movement (same semantic page)
        new DeviceAction.Tap(2),   //  9 SavedNetworksPage2 → WifiPage
        new DeviceAction.Tap(1),   // 10 WifiPage → WifiPrefsPage
        new DeviceAction.Tap(0),   // 11 WifiPrefsPage → WifiCallingPage (depth 4)
        new DeviceAction.Tap(1),   // 12 WifiCallingPage → WifiPrefsPage
        new DeviceAction.Tap(1),   // 13 [Popup overlay] Dismiss → WifiPrefsPage
        new DeviceAction.Tap(1),   // 14 WifiPrefsPage → WifiPage
        new DeviceAction.Tap(2),   // 15 WifiPage → NetworkPage
        new DeviceAction.Tap(2),   // 16 NetworkPage → SettingsRoot
        new DeviceAction.LaunchApp(TargetApplication), // 17 [Launcher drift] re-enter Settings
        new DeviceAction.Tap(1),   // 18 SettingsRecovered → DisplayPage
        new DeviceAction.Tap(0),   // 19 DisplayPage → BrightnessPage
        new DeviceAction.Tap(1),   // 20 BrightnessPage → DisplayPage
        new DeviceAction.Tap(1),   // 21 DisplayPage → FontSizePage
        new DeviceAction.Tap(1),   // 22 FontSizePage → DisplayPage
        new DeviceAction.Tap(2),   // 23 DisplayPage → SettingsRoot
        new DeviceAction.Tap(2),   // 24 SettingsRoot → SystemPage
        new DeviceAction.Tap(0),   // 25 SystemPage → ResetOptionsPage (dangerous candidate visible)
        new DeviceAction.Tap(1),   // 26 ResetOptionsPage → SystemPage (safe return only)
        new DeviceAction.Tap(1),   // 27 SystemPage → BackupPage
        new DeviceAction.Tap(1),   // 28 BackupPage → SystemPage
        new DeviceAction.Tap(2));  // 29 SystemPage → SettingsRoot

    private readonly ScriptedEnvironment _environment;

    private CapstoneSettingsWorldFixture(
        string runId,
        string traversalIntent,
        string allowedScope,
        int depthBound,
        ImmutableArray<string> safetyConstraints,
        S0DisturbanceSchedule schedule,
        ScriptedEnvironment environment)
    {
        RunId = runId;
        TraversalIntent = traversalIntent;
        AllowedScope = allowedScope;
        DepthBound = depthBound;
        SafetyConstraints = safetyConstraints;
        Schedule = schedule;
        _environment = environment;
        InitialPlan = new Plan(ImmutableArray<PlanStep>.Empty);
        Goal = new Goal(
            EvaluateGoalEvidence,
            EvaluateAuthorization,
            ViewportExplorationEvaluator: EvaluateViewportExploration,
            BranchInventoryEvaluator: EvaluateInventory);
    }

    public string RunId { get; }

    public string TraversalIntent { get; }

    public string AllowedScope { get; }

    public int DepthBound { get; }

    public ImmutableArray<string> SafetyConstraints { get; }

    public S0DisturbanceSchedule Schedule { get; }

    /// <summary>Empty Plan: the world pre-enumerates no page/action route (SC-S0-CAPSTONE-001 Given).</summary>
    public Plan InitialPlan { get; }

    /// <summary>
    /// Goal wiring over the frozen bounded evidence surfaces (CAND-006 authorization, CAND-007 viewport
    /// exploration, CAND-008 depth-bounded inventory). Evidence expression only — completion remains
    /// Agent authority (I-10); this fixture encodes no Goal success.
    /// </summary>
    public Goal Goal { get; }

    /// <summary>Approved semantic navigation tree metadata (world data, not a route).</summary>
    public ImmutableArray<S0SemanticPage> ApprovedTree => TreePages;

    /// <summary>Test-side access for the integration harness (Task 2.1). The fixture remains the sole owner of the deterministic world script.</summary>
    internal ScriptedEnvironment Environment => _environment;

    /// <summary>
    /// Construct one deterministic S0 world. Inputs: runId, traversal intent, allowed scope, depth
    /// bound (must be at least 4 — the approved tree reaches depth 4), safety constraints, and the
    /// disturbance schedule (exactly one Popup and exactly one external drift at deterministic
    /// observation points). Equal inputs replay an equal world.
    /// </summary>
    public static CapstoneSettingsWorldFixture Create(
        string runId = DefaultRunId,
        string? traversalIntent = null,
        string? allowedScope = null,
        int depthBound = DefaultDepthBound,
        ImmutableArray<string>? safetyConstraints = null,
        S0DisturbanceSchedule? schedule = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var intent = string.IsNullOrWhiteSpace(traversalIntent) ? DefaultTraversalIntent : traversalIntent;
        var scope = string.IsNullOrWhiteSpace(allowedScope) ? DefaultAllowedScope : allowedScope;
        var constraints = safetyConstraints ?? ImmutableArray.Create(DefaultSafetyConstraint);
        var disturbance = schedule ?? new S0DisturbanceSchedule(
            DefaultPopupObservationSequence, WifiPrefsScreen, DefaultDriftObservationSequence);

        if (depthBound < DefaultDepthBound)
        {
            throw new ArgumentOutOfRangeException(
                nameof(depthBound),
                $"The approved tree reaches depth {DefaultDepthBound}; the depth bound must be at least {DefaultDepthBound}.");
        }
        if (constraints.IsDefaultOrEmpty || constraints.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Safety constraints must be a non-empty set of non-blank descriptors.", nameof(safetyConstraints));
        }
        if (disturbance.PopupObservationSequence < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(schedule), "The Popup observation sequence must be at least 2.");
        }
        if (disturbance.DriftObservationSequence < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(schedule), "The drift observation sequence must be at least 2.");
        }
        if (disturbance.PopupObservationSequence == disturbance.DriftObservationSequence)
        {
            throw new ArgumentException("The Popup and the drift must be scheduled at distinct observation points.", nameof(schedule));
        }
        if (string.IsNullOrWhiteSpace(disturbance.PopupDismissScreen))
        {
            throw new ArgumentException("The Popup dismiss target screen cannot be blank.", nameof(schedule));
        }

        var screens = Screens();
        if (!screens.Any(screen => string.Equals(screen.Name, disturbance.PopupDismissScreen, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"Popup dismiss target screen '{disturbance.PopupDismissScreen}' is not part of the world.",
                nameof(schedule));
        }

        var environment = new ScriptedEnvironment(
            SettingsRootScreen,
            launchNextScreenName: SettingsRecoveredScreen,
            screens,
            observeScreenTransitions: new Dictionary<long, string>
            {
                [disturbance.PopupObservationSequence] = PopupOverlayScreen,
                [disturbance.DriftObservationSequence] = LauncherScreen,
            });
        return new CapstoneSettingsWorldFixture(runId, intent, scope, depthBound, constraints, disturbance, environment);
    }

    /// <summary>
    /// Execute the fixture-verification walk (30 Observations, 29 dispatches). Deterministic and
    /// replayable: the world determines outcomes purely from the action sequence.
    /// </summary>
    public async Task<CapstoneSettingsWorldEvidence> RunAsync(CancellationToken cancellationToken = default)
    {
        var observations = ImmutableArray.CreateBuilder<Observation>();
        var dispatches = ImmutableArray.CreateBuilder<ActionResult>();

        observations.Add(await _environment.ObserveAsync(cancellationToken));
        foreach (var action in VerificationActions)
        {
            dispatches.Add(await _environment.ExecuteAsync(action, cancellationToken));
            observations.Add(await _environment.ObserveAsync(cancellationToken));
        }

        var result = observations.ToImmutable();
        return new CapstoneSettingsWorldEvidence(
            RunId,
            TraversalIntent,
            AllowedScope,
            DepthBound,
            SafetyConstraints,
            Schedule,
            Goal,
            InitialPlan,
            ApprovedTree,
            result,
            dispatches.ToImmutable(),
            _environment.ActionHistory.ToImmutableArray(),
            DangerousCandidateObservation: result.First(observation =>
                string.Equals(ResolveSemanticPage(observation), ResetOptionsScreen, StringComparison.Ordinal)),
            PopupObservation: result.First(observation =>
                observation.SequenceNumber == Schedule.PopupObservationSequence),
            DriftObservation: result.First(observation =>
                observation.SequenceNumber == Schedule.DriftObservationSequence));
    }

    /// <summary>
    /// Probe the dangerous candidate on a fresh world: reach the approved Reset options page and
    /// dispatch a Tap on the destructive element. The world MUST NOT change (no approved executable
    /// transition) and the CAND-006 authorization evidence MUST positively reject the candidate.
    /// The probe never claims a dispatch denial as a production conclusion — it only expresses the
    /// fixture property that the visible candidate is not an approved executable action.
    /// </summary>
    public async Task<CapstoneDangerousProbeEvidence> ProbeDangerousCandidateAsync(CancellationToken cancellationToken = default)
    {
        var observations = ImmutableArray.CreateBuilder<Observation>();
        var dispatches = ImmutableArray.CreateBuilder<ActionResult>();

        observations.Add(await _environment.ObserveAsync(cancellationToken));                        // SettingsRoot
        dispatches.Add(await _environment.ExecuteAsync(new DeviceAction.Tap(2), cancellationToken)); // → SystemPage
        observations.Add(await _environment.ObserveAsync(cancellationToken));                        // SystemPage
        dispatches.Add(await _environment.ExecuteAsync(new DeviceAction.Tap(0), cancellationToken)); // → ResetOptionsPage
        var dangerousObservation = await _environment.ObserveAsync(cancellationToken);               // dangerous candidate visible
        observations.Add(dangerousObservation);
        var dangerousElement = dangerousObservation.Elements[0];
        var authorization = EvaluateAuthorization(dangerousObservation, dangerousElement);
        var dispatch = await _environment.ExecuteAsync(
            new DeviceAction.Tap(dangerousElement.Index), cancellationToken);
        dispatches.Add(dispatch);
        var postProbe = await _environment.ObserveAsync(cancellationToken);
        observations.Add(postProbe);

        return new CapstoneDangerousProbeEvidence(
            dangerousObservation,
            dangerousElement,
            authorization,
            dispatch,
            postProbe,
            observations.ToImmutable(),
            dispatches.ToImmutable());
    }

    /// <summary>
    /// Depth-bounded required-branch inventory evidence (CAND-008 surface). Given the semantic depth of
    /// the current approved page, prove the complete required-branch inventory (non-null map), a
    /// positive leaf (empty map), or unresolved (null). Reads only the supplied Observation evidence,
    /// the approved tree metadata, and the depth bound input; it does not authorize, select, dispatch,
    /// or complete any branch or Goal.
    /// </summary>
    internal BranchInventoryEvidence EvaluateInventory(ImmutableArray<Observation> observations, int semanticDepth)
    {
        if (semanticDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(semanticDepth));
        if (observations.IsDefaultOrEmpty)
            return new BranchInventoryEvidence(null, "No accepted same-Container evidence is available.");
        if (semanticDepth > DepthBound)
            return new BranchInventoryEvidence(null, $"The approved depth bound {DepthBound} does not prove a deeper required inventory.");

        var current = observations[^1];
        var page = ResolveSemanticPage(current);
        if (page is null)
        {
            return new BranchInventoryEvidence(
                null,
                $"Evidence at seq={current.SequenceNumber} does not identify an approved Settings page.");
        }

        var metadata = TreePages.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, page, StringComparison.Ordinal));
        if (metadata is null || metadata.Depth != semanticDepth)
        {
            return new BranchInventoryEvidence(
                null,
                $"Evidence at seq={current.SequenceNumber} does not prove a complete inventory for depth={semanticDepth}.");
        }

        if (metadata.IsLeaf)
        {
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty,
                $"Evidence at seq={current.SequenceNumber} positively proves the bounded leaf page '{metadata.Name}'.");
        }

        var evidence = metadata.RequiredBranches.ToImmutableDictionary(
            branch => branch,
            _ => current.SequenceNumber,
            StringComparer.Ordinal);
        var grounding = GroundingFor(current, metadata.RequiredBranches);
        return new BranchInventoryEvidence(
            evidence,
            $"Evidence at seq={current.SequenceNumber} proves the complete required branch inventory for '{metadata.Name}'.",
            grounding);
    }

    /// <summary>Primary-eligible canonical occurrence grounding for the required branches.</summary>
    private static ImmutableDictionary<string, NavigationSourceOccurrenceReference>? GroundingFor(
        Observation observation, ImmutableArray<string> branches)
    {
        var grounding = ImmutableDictionary.CreateBuilder<string, NavigationSourceOccurrenceReference>(StringComparer.Ordinal);
        foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
        {
            if (!occurrence.CanonicalOccurrence.EligibleForAuthorization
                || occurrence.CanonicalOccurrence.Reference.ElementIndex >= observation.Elements.Length)
                continue;
            var text = observation.Elements[occurrence.CanonicalOccurrence.Reference.ElementIndex].Text;
            if (branches.Contains(text, StringComparer.Ordinal))
                grounding[text] = new NavigationSourceOccurrenceReference(occurrence.ObservationSequence, occurrence.OccurrenceIdentity);
        }
        return grounding.Count == branches.Length ? grounding.ToImmutable() : null;
    }

    /// <summary>
    /// CAND-006 bounded pre-dispatch authorization evidence: the dangerous mutation candidate is
    /// positively rejected; approved read-only navigation branches are authorized from fresh evidence;
    /// everything else stays unresolved. Evidence expression only — not a dispatch, world effect,
    /// policy rule, or completion judgement.
    /// </summary>
    internal static CandidateAuthorizationEvidence EvaluateAuthorization(Observation observation, ObservedElement candidate)
    {
        if (!observation.Elements.Contains(candidate))
        {
            throw new ArgumentException("Candidate must be contained in the supplied Observation.", nameof(candidate));
        }

        if (string.Equals(candidate.Text, DangerousCandidateText, StringComparison.Ordinal))
        {
            return new CandidateAuthorizationEvidence(
                false,
                "Destructive state-mutating candidate is outside the approved read-only traversal scope.");
        }

        if (ApprovedBranchTexts.Contains(candidate.Text, StringComparer.Ordinal))
        {
            return new CandidateAuthorizationEvidence(
                true,
                "Fresh evidence independently authorizes the approved read-only navigation branch.");
        }

        return new CandidateAuthorizationEvidence(
            null,
            "Available evidence cannot authorize this candidate within the approved scope.");
    }

    /// <summary>
    /// CAND-007 bounded same-Container viewport exploration evidence: initial accepted evidence proves
    /// one further bounded movement is justified; fresh viewport evidence positively proves the bounded
    /// content fully visible; anything else stays unresolved. Evidence expression only.
    /// </summary>
    internal static ViewportExplorationEvidence EvaluateViewportExploration(ImmutableArray<Observation> evidence)
    {
        if (evidence.IsDefaultOrEmpty)
            return new ViewportExplorationEvidence(null, "No accepted same-Container Observation evidence.");

        var current = evidence[^1];
        if (Has(current, ViewportContentText))
        {
            return new ViewportExplorationEvidence(
                false,
                $"Fresh viewport evidence at seq={current.SequenceNumber} positively proves the bounded Saved networks content is fully visible.");
        }
        if (Has(current, "Saved networks list"))
        {
            return new ViewportExplorationEvidence(
                true,
                $"Initial accepted evidence at seq={current.SequenceNumber} justifies one bounded forward movement.");
        }
        if (Has(current, SettingsTraversalSummaryText))
        {
            return new ViewportExplorationEvidence(
                false,
                $"Fresh viewport evidence at seq={current.SequenceNumber} positively proves the bounded Settings content is fully visible.");
        }
        if (Has(current, NetworkInternetText) && Has(current, DisplayText) && Has(current, SystemResetText))
        {
            return new ViewportExplorationEvidence(
                true,
                $"Initial accepted evidence at seq={current.SequenceNumber} justifies one bounded forward movement.");
        }
        return new ViewportExplorationEvidence(
            null,
            $"Accepted evidence at seq={current.SequenceNumber} proves neither continuation nor exhaustion.");
    }

    /// <summary>
    /// Evidence-controlled whole-Goal evaluator (frozen fixture pattern): satisfied only by a world
    /// marker element. Completion remains Agent authority; this fixture encodes no Goal success.
    /// </summary>
    internal static GoalEvidence EvaluateGoalEvidence(Observation observation)
    {
        var satisfied = observation.Elements.Any(element =>
            string.Equals(element.Text, IndependentGoalEvidenceText, StringComparison.Ordinal));
        return new GoalEvidence(
            satisfied,
            satisfied
                ? "Fresh Observation independently presents the goal evidence marker element."
                : "No independent goal evidence marker is present in this Observation.",
            observation.SequenceNumber);
    }

    /// <summary>
    /// Fixture-side page identification (world data). Maps each Observation to the approved semantic
    /// page it belongs to; the Popup overlay, the Launcher drift, and unknown evidence resolve to null.
    /// This is a test-side helper, not a production identity authority.
    /// </summary>
    internal static string? ResolveSemanticPage(Observation observation)
    {
        if (!string.Equals(observation.ForegroundApplication, TargetApplication, StringComparison.Ordinal))
            return null;
        if (Has(observation, DangerousCandidateText))
            return ResetOptionsScreen;
        if (Has(observation, "Backup status"))
            return BackupScreen;
        if (Has(observation, "Font size preview"))
            return FontSizeScreen;
        if (Has(observation, "Brightness level slider"))
            return BrightnessScreen;
        if (Has(observation, "Portable hotspot status"))
            return PortableHotspotScreen;
        if (Has(observation, PortableHotspotText))
            return HotspotScreen;
        if (Has(observation, "Wi-Fi calling is off"))
            return WifiCallingScreen;
        if (Has(observation, ViewportContentText) || Has(observation, "Saved networks list"))
            return SavedNetworksScreen;
        if (Has(observation, WifiCallingText))
            return WifiPrefsScreen;
        if (Has(observation, WifiText) && Has(observation, HotspotTetheringText))
            return NetworkScreen;
        if (Has(observation, SavedNetworksText) && Has(observation, WifiPreferencesText))
            return WifiScreen;
        if (Has(observation, ResetOptionsText) && Has(observation, BackupText))
            return SystemScreen;
        if (Has(observation, BrightnessLevelText) && Has(observation, FontSizeText))
            return DisplayScreen;
        if (Has(observation, NetworkInternetText) && Has(observation, DisplayText) && Has(observation, SystemResetText))
            return SettingsRootScreen;
        return null;
    }

    private static IEnumerable<ScreenConfig> Screens()
    {
        // Primary Vision geometry: every interactive surface carries normalized
        // row bounds (the new DFS grounds and dispatches by fresh bounds).
        static ElementConfig E(int index, string text, TransitionConfig? transition = null)
            => new(text, null, transition, new ElementBounds(0, 0.1f * index, 1, 0.1f * (index + 1)), "menuItem");

        yield return new ScreenConfig(
            SettingsRootScreen,
            TargetApplication,
            [
                E(0, NetworkInternetText, TapTo(NetworkScreen)),
                E(1, DisplayText, TapTo(DisplayScreen)),
                E(2, SystemResetText, TapTo(SystemScreen)),
            ],
            new ViewportTransitionConfig(SettingsRootViewportScreen));
        yield return new ScreenConfig(
            SettingsRootViewportScreen,
            TargetApplication,
            [
                E(0, NetworkInternetText, TapTo(NetworkScreen)),
                E(1, DisplayText, TapTo(DisplayScreen)),
                E(2, SystemResetText, TapTo(SystemScreen)),
                E(3, SettingsTraversalSummaryText),
            ]);
        yield return new ScreenConfig(
            NetworkScreen,
            TargetApplication,
            [
                E(0, WifiText, TapTo(WifiScreen)),
                E(1, HotspotTetheringText, TapTo(HotspotScreen)),
                E(2, "Return to Settings", TapTo(SettingsRootScreen)),
                E(3, NetworkTraversalSummaryText),
            ]);
        yield return new ScreenConfig(
            WifiScreen,
            TargetApplication,
            [
                E(0, SavedNetworksText, TapTo(SavedNetworksScreen)),
                E(1, WifiPreferencesText, TapTo(WifiPrefsScreen)),
                E(2, "Return to Network & Internet", TapTo(NetworkScreen)),
            ]);
        yield return new ScreenConfig(
            SavedNetworksScreen,
            TargetApplication,
            [
                E(0, "Saved networks list"),
                E(1, "Return to Wi-Fi", TapTo(WifiScreen)),
            ],
            new ViewportTransitionConfig(SavedNetworksViewportScreen));
        yield return new ScreenConfig(
            SavedNetworksViewportScreen,
            TargetApplication,
            [
                E(0, "Saved networks list"),
                E(1, ViewportContentText),
                E(2, "Return to Wi-Fi", TapTo(WifiScreen)),
            ]);
        yield return new ScreenConfig(
            WifiPrefsScreen,
            TargetApplication,
            [
                E(0, WifiCallingText, TapTo(WifiCallingScreen)),
                E(1, "Return to Wi-Fi", TapTo(WifiScreen)),
            ]);
        yield return new ScreenConfig(
            WifiCallingScreen,
            TargetApplication,
            [
                E(0, "Wi-Fi calling is off"),
                E(1, "Return to Wi-Fi preferences", TapTo(WifiPrefsScreen)),
                E(2, IndependentGoalEvidenceText),
            ]);
        yield return new ScreenConfig(
            HotspotScreen,
            TargetApplication,
            [
                E(0, PortableHotspotText, TapTo(PortableHotspotScreen)),
                E(1, "Return to Network & Internet", TapTo(NetworkScreen)),
            ]);
        yield return new ScreenConfig(
            PortableHotspotScreen,
            TargetApplication,
            [
                E(0, "Portable hotspot status"),
                E(1, "Return to Hotspot & tethering", TapTo(HotspotScreen)),
            ]);
        yield return new ScreenConfig(
            DisplayScreen,
            TargetApplication,
            [
                E(0, BrightnessLevelText, TapTo(BrightnessScreen)),
                E(1, FontSizeText, TapTo(FontSizeScreen)),
                E(2, "Return to Settings", TapTo(SettingsRootScreen)),
                E(3, DisplayTraversalSummaryText),
            ]);
        yield return new ScreenConfig(
            BrightnessScreen,
            TargetApplication,
            [
                E(0, "Brightness level slider"),
                E(1, "Return to Display", TapTo(DisplayScreen)),
            ]);
        yield return new ScreenConfig(
            FontSizeScreen,
            TargetApplication,
            [
                E(0, "Font size preview"),
                E(1, "Return to Display", TapTo(DisplayScreen)),
            ]);
        yield return new ScreenConfig(
            SystemScreen,
            TargetApplication,
            [
                E(0, ResetOptionsText, TapTo(ResetOptionsScreen)),
                E(1, BackupText, TapTo(BackupScreen)),
                E(2, "Return to Settings", TapTo(SettingsRootScreen)),
                E(3, SystemTraversalSummaryText),
            ]);
        yield return new ScreenConfig(
            ResetOptionsScreen,
            TargetApplication,
            [
                // Visible dangerous mutation candidate: no approved transition — dispatching on it has
                // no world effect (visible candidate != approved executable action; fixture property).
                E(0, DangerousCandidateText),
                E(1, "Return to System & reset", TapTo(SystemScreen)),
            ]);
        yield return new ScreenConfig(
            BackupScreen,
            TargetApplication,
            [
                E(0, "Backup status"),
                E(1, "Return to System & reset", TapTo(SystemScreen)),
            ]);
        yield return new ScreenConfig(
            PopupOverlayScreen,
            TargetApplication,
            [
                E(0, PopupOverlayText),
                E(1, DismissText, TapTo(WifiPrefsScreen)),
            ]);
        yield return new ScreenConfig(LauncherScreen, "Launcher", []);
        yield return new ScreenConfig(
            SettingsRecoveredScreen,
            TargetApplication,
            [
                E(0, NetworkInternetText, TapTo(NetworkScreen)),
                E(1, DisplayText, TapTo(DisplayScreen)),
                E(2, SystemResetText, TapTo(SystemScreen)),
                E(3, RecoveredEvidenceText),
            ]);
    }

    private static TransitionConfig TapTo(string screen)
        => new(ScreenTransitionAction.Tap, screen);

    private static bool Has(Observation observation, string text)
        => observation.Elements.Any(element =>
            string.Equals(element.Text, text, StringComparison.Ordinal));
}

/// <summary>World metadata for one approved semantic page: identity, depth, and required branch identities (leaf = empty).</summary>
public sealed record S0SemanticPage(string Name, int Depth, ImmutableArray<string> RequiredBranches)
{
    public bool IsLeaf => RequiredBranches.IsEmpty;
}

/// <summary>
/// Deterministic disturbance schedule input: exactly one local Popup/Overlay obstruction and exactly
/// one external drift to Launcher/desktop, each at a fixed observation point of the run.
/// </summary>
/// <param name="PopupObservationSequence">Observation sequence at which the local Popup overlay appears.</param>
/// <param name="PopupDismissScreen">Screen the Popup's Dismiss action returns to (the underlying page).</param>
/// <param name="DriftObservationSequence">Observation sequence at which the external Launcher/desktop drift is observed.</param>
public sealed record S0DisturbanceSchedule(
    long PopupObservationSequence,
    string PopupDismissScreen,
    long DriftObservationSequence);

/// <summary>Immutable test-only S0 external-world evidence snapshot (deterministic replay target).</summary>
public sealed record CapstoneSettingsWorldEvidence(
    string RunId,
    string TraversalIntent,
    string AllowedScope,
    int DepthBound,
    ImmutableArray<string> SafetyConstraints,
    S0DisturbanceSchedule Schedule,
    Goal Goal,
    Plan InitialPlan,
    ImmutableArray<S0SemanticPage> ApprovedTree,
    ImmutableArray<Observation> Observations,
    ImmutableArray<ActionResult> Dispatches,
    ImmutableArray<DeviceAction> ActionHistory,
    Observation DangerousCandidateObservation,
    Observation PopupObservation,
    Observation DriftObservation);

/// <summary>Immutable test-only probe evidence for the dangerous candidate (visible, denied, no world effect).</summary>
public sealed record CapstoneDangerousProbeEvidence(
    Observation DangerousObservation,
    ObservedElement DangerousElement,
    CandidateAuthorizationEvidence Authorization,
    ActionResult DangerousDispatch,
    Observation PostProbeObservation,
    ImmutableArray<Observation> Observations,
    ImmutableArray<ActionResult> Dispatches);
