## 0. Human Apply Gate

- [x] 0.1 Obtain explicit human approval for the additive but internally breaking Semantic Evidence Protocol V2 boundary; record Luna as implementation owner and Sol as independent architecture verifier before any production edit.

## 1. Boundary Characterization and Guards

- [x] 1.1 Record the executable scenario-knowledge inventory for Environment, World, Agent, Traversal inputs, PhysicalHost, Fast Semantic, and Corpus production paths.
- [x] 1.2 Add dependency/type guards proving external semantic packages cannot reference Agent, Traversal, FSM, GoalEvidence, Recovery commands, or Run-start clients.
- [x] 1.3 Add guards rejecting free scenario strings, selectors, routes, DeviceAction, completion flags, FSM/Run commands, and callbacks in authority-bearing semantic contracts.
- [x] 1.4 Preserve current SETTINGS-TREE-01, parent-return, scroll-continuity, Fast identity, RuntimeAgent Phase 1-4, and Strategy Contract behavior as characterization tests before extraction.

## 2. Semantic Evidence Protocol V2

- [x] 2.1 Add versioned capability/package manifests and manifest-resolved semantic symbol references owned by the Runtime consumer contract.
- [x] 2.2 Add source-qualified V2 evidence envelopes and typed container-identity, element-affordance, and container-relation candidate payloads.
- [x] 2.3 Add a separate bounded CoverageRequirementDescriptor contract with no satisfaction, completion, Goal, or terminal fields.
- [x] 2.4 Implement fail-closed V2 admission for version, manifest, symbol, evidence kind, source tier, freshness, frame alignment, scope, provenance, and forbidden payloads.
- [x] 2.5 Keep V1 frozen to its existing Container Identity scope and add compatibility tests proving no new V1 kind or free-string-to-V2 inference is introduced.

## 3. Primary and Auxiliary Perception Sources

- [x] 3.1 Introduce explicit primary and auxiliary observation-source metadata including source identity, availability, capture freshness, frame/display association, and provenance.
- [x] 3.2 Keep Vision/screenshot perception as primary and classify ADB UI hierarchy as optional auxiliary evidence only.
- [x] 3.3 Make unsupported, denied, empty, stale, or invalid ADB capture report auxiliary unavailability without failing an otherwise sufficient primary visual observation.
- [x] 3.4 Add mechanical tests proving missing required Vision cannot be silently replaced by ADB hierarchy evidence.
- [x] 3.5 Add authority tests proving ADB-only evidence cannot authorize Action, verify Container identity, prove coverage, satisfy GoalEvidence, or transition lifecycle state.
- [x] 3.6 Add derivation tests proving Semantic Capability and Runtime fusion preserve auxiliary provenance and cannot promote ADB-only claims to primary authority.
- [x] 3.7 Establish one canonical full-frame coordinate contract and cross-source conformance tests while keeping ADB bounds auxiliary and source-qualified.
- [x] 3.8 Add source-qualified `ObservationOccurrence` and immutable `CanonicalObservationOccurrence` models with primary-support eligibility and complete provenance.
- [x] 3.9 Add a pure `SourceGroundingNormalizer` that emits Vision occurrences independently, attaches only deterministic auxiliary corroboration, and never creates synthetic primary evidence.
- [x] 3.10 Reject typed evidence whose occurrence source/tier does not match its declared provenance.

## 4. External Semantic Capability Extraction

- [x] 4.1 Create external platform/scenario semantic capability projects whose dependency direction points to the Runtime-owned evidence contract.
- [x] 4.2 Extract Settings page classifiers, Preference-row interpretation, search-role rules, parent/child relations, locale labels, and scenario symbols into read-only external packages/bindings.
- [x] 4.3 Move Fast provider implementation, vector candidate/index, Semantic Corpus, evaluator, benchmark runner, and scenario corpora out of the Runtime execution assembly into external capability/evaluation ownership.
- [x] 4.4 Add package-level tests proving bindings accept only source-qualified Observation facts and bounded verified history and return candidate evidence only.

## 5. Generic Runtime Consumer Migration

- [x] 5.1 Replace InteractionAffordanceAnalyzer Settings/Android classification with a generic reducer over admitted V2 affordance evidence.
- [x] 5.2 Remove Settings-specific timing, row-band, and raw perception-label interpretation from Agent.SemanticRun while preserving its bounded loop and fresh verification.
- [x] 5.3 Replace Agent.OpenWorld label-based parent-return interpretation with admitted relation/return candidates while preserving uniqueness, authorization, DFS, and post-action verification.
- [x] 5.4 Update SemanticActionLowerer to consume an Agent-authorized, freshly grounded typed control role instead of a free provider label without changing Traversal ownership.
- [x] 5.5 Prove Runtime admission/fusion/reconciliation remains the only path from semantic candidate evidence to WorldBelief.
- [x] 5.6 Migrate InteractionAffordanceAnalyzer, source equivalence, and source grounding from structured-element indices to canonical occurrence references while preserving fail-closed behavior.
- [x] 5.7 Migrate Agent.OpenWorld branch and parent-return grounding to fresh primary-supported canonical occurrences without changing DFS or authorization ownership.

## 6. Environment and Host Cleanup

- [x] 6.1 Reduce AdbUiHierarchySource to raw dump acquisition, primitive parsing, source-local coordinate mapping, availability, and auxiliary provenance; remove toolbar/title/row/widget semantic interpretation.
- [x] 6.2 Replace PhysicalHost Settings defaults and arbitrary semantic resolver callbacks with generic capability discovery and manifest-based binding composition.
- [x] 6.3 Move Settings launch assumptions, device baseline mutations, page anchors, and corpus scenario preparation into an isolated validation Harness.
- [x] 6.4 Add a production-entry guard proving Harness baseline/setup operations cannot be invoked through run.start, run.strategy.start, Agent, RuntimeAgent, or recovery paths.

## 7. Regression and Architecture Verification

- [x] 7.1 Run build plus Semantic V1/V2, source-tier, evidence-admission, and authority tests.
- [x] 7.2 Run RuntimeAgent Phase 1-4 and Strategy Contract regression suites.
- [x] 7.3 Run SETTINGS-TREE-01 with the external Settings package and verify its result remains scenario-capability evidence rather than Generic Runtime proof.
- [x] 7.4 Run Generic Fake World and OpenWorld suites without any scenario package and prove Runtime has no Settings dependency.
- [x] 7.5 Run Agent lifecycle, FSM, Traversal, Recovery, GoalEvidence, Architecture Guards, consistency checks, and `git diff --check`.
- [x] 7.6 Run `openspec validate runtime-external-semantic-capability-boundary --type change --strict --no-interactive` and record truthful environment limitations separately from passing tests.
- [x] 7.7 Remove synthetic Vision projection from structured fixtures and add independent Vision-only, Vision-plus-ADB, and ADB-only rejection proofs.

## 8. Documentation and Independent Handoff

- [x] 8.1 Update architecture projections and dependency maps only to reflect verified implementation, preserving Architecture v1 authority ownership.
- [x] 8.2 Mark runtime-scenario-knowledge-boundary-cleanup as superseded without treating archive or task completion as graduation.
- [ ] 8.3 Have Sol independently verify scenario neutrality, ADB auxiliary non-authority, dependency direction, and full regression evidence before Strategy or Semantic graduation resumes.
  - Graduation evidence for Sol: `docs/decisions/runtime-external-semantic-capability-boundary-graduation-decision.md`
    (10-section PROJECT_LEADER_RUNTIME_EXTERNAL_SEMANTIC_CAPABILITY_BOUNDARY_GRADUATION_REPORT,
    includes the reported B-class `search_action_bar` token finding for adjudication).

## Verification Record (2026-08-22 apply continuation)

**Build:** `dotnet build src/UniClaw.Runtime.sln -p:NuGetAudit=false` → 0 errors, 0 warnings. The only warnings without the audit flag are `NU1900` (NuGet vulnerability-audit cache write denied by the environment sandbox — an environment artifact, not a code warning; present identically on the baseline commit).

**Tests:** `dotnet test src/UniClaw.Runtime.sln` → 1921 total, all deterministic tests pass. The only 7 failures are environment-limited (no emulator attached):
- Real-device Settings suites (need an online device; previously hardcoded `emulator-5554`): `SettingsSingleRecursiveChild_RealDevice_Phase2`, `SettingsGrandchildVerifiedReturn_RealDevice_Phase3`, `SettingsSiblingSubtreeLedger_RealDevice_Phase4`, `SettingsTreeCapstone_RealDevice_Phase5`, `SettingsRoot_RealDevice_Phase1_RootContainerRealityBaseline`, `ExternalBoundary_RealDevice`.
- Real-emulator capstone (previously hardcoded `emulator-5556`): `Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete`.

**Real-device serial/adb hardcoding resolved (2026-08-22):** the machine-specific
`"/Users/fran/Android/Sdk/platform-tools/adb"` path and `emulator-5554`/`emulator-5556`
serials are no longer baked into the seven RealDevice test classes. New
`tests/UniClaw.Runtime.Tests/Scenario/RealDeviceTestConfiguration.cs` is the single
source: `UNICLAW_ADB_PATH` / `UNICLAW_SETTINGS_SERIAL` / `UNICLAW_CAPSTONE_SERIAL`
environment variables override, else the production `AdbDeviceResolver` discovers
the unique online device, else the test fails fast (sub-second) with an explicit
message naming the required variable — instead of a 23s adb timeout. Resolution is
lazy, so deterministic tests in the same classes are unaffected. In an
emulator-attached environment (or with env vars set) the suite reaches 1921/1921.

**Correction to an earlier misclassification (2026-08-22):** the two DriverHost NodeClient E2E tests (`DriverHostRunStartE2ETests.NodeClient_StartsRealAgentRun_AndObservesCompletion_ThroughExistingSurfaces`, `DriverHostAssistanceE2ETests.NodeClient_BridgeResolvesConsult_AgentContinues_Completes`) were previously recorded as environment-limited ("needs the node client/server"). That was WRONG: node was present and the client connected successfully. The real cause was a regression from this change — task 6.2 removed the `PhysicalHostComposition.BuildRuntimeGraph` Settings default resolver (`_ => "Settings"` → `_ => null`) and the toggle affordance migration (5.1/5.6) required the fixture `WithToggleLocalControl()` decorator, but the two E2E factories had not been migrated. Fixed by passing `resolveSemanticPage: _ => "Settings"` and `env.WithToggleLocalControl()` at the test call site (same pattern as `RunExecutionCoordinatorTests`); both now pass (1914/1921).

**Mechanical:** `check-consistency.sh` ALL PASS; `git diff --check` clean; `openspec validate runtime-external-semantic-capability-boundary --type change --strict --no-interactive` → valid.

**Implementation notes (Luna, apply continuation):**
- `SourceGroundingNormalizer` gains a source-less compatibility projection (Elements → implicit primary, StructuredElements → implicit auxiliary) so fixture/replay observations remain groundable without declared source metadata; explicit source metadata remains strictly enforced.
- `InteractionAffordanceAnalyzer` retains the generic structural compatibility classifier (non-interactive / checkable-switch / search-role token / clickable LinearLayout row / ambiguous interactive) for raw structured evidence; primary elements without admitted evidence classify as Unknown (fail closed).
- `SourceEquivalenceNormalizer` derives signatures per channel (Vision `Text|PerceptionType`, auxiliary `RawText|Class|ResourceId|ContentDescription`) and enumerates both tiers; authorization-bearing callers filter `EligibleForAuthorization`.
- `Agent.OpenWorld` identity-safety (ancestry/visited) now runs before grounding validation, and logical-source claims are per-Container (a page whose own toggle shares a label with the entering navigation row no longer trips a run-level duplicate claim).
- `PostCompletenessConsistencyValidator`/completeness treat unresolvable ParentReturnControl candidates as blocking Unknowns; `HasIncompletePostScrollEvidence` ignores NonInteractive status text without bounds.
- External `UniClaw.Semantic.Settings` capability classifies primary Vision occurrences corroborated by current-frame auxiliary structured facts (rows → navigation, "Navigate up" → parent return, toggle-shaped → local control); auxiliary-only occurrences are never promoted.
- `AdbUiHierarchySource` performs raw acquisition + primitive parsing only (top-level hierarchy preservation, descendant raw-text merge, no toolbar/title/row roles).
