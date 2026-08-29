# Development Semantic IR Records — Phase 2.6

One record per implementation task (compiled into UniFlow `semantic_brief`; the Worker
Dispatcher never rewrites these). GapKind uses the closed classification.

---

## IR-B (tasks B.1/B.2 — iterative campaign runner)

- **DesiredReality**: a validation-side campaign runner drives N independent Runtime runs on
  a real environment composition; each round transports exactly ONE directive (exactly one
  `run.strategy.start`), re-asserts single-run autonomy + the four frozen invariants from
  that run's own call-log/result slice, and the loop terminates only on bounded scope
  exhaustion / explicitly unsafe remaining frontier / evidenced Runtime-Contract gap.
- **ClaimUnderTest**: `UPPER_AGENT_CROSS_RUN_PLAN_ADAPTATION`'s execution substrate — N
  independent runs with cross-run isolation — is expressible with the frozen wire surface
  and the graduated Phase 2.5 per-run chain, zero Runtime change.
- **ExistingEvidence**: `ScenarioRunner.RunTierAAsync` (one dispatch → collector → boundary →
  gates → report, prior-call-log chaining for cross-run boundary proof); `EmulatorDriver`
  (single frozen wire method, immutable call log); `ValidationGateEvaluator` G2 (zero driver
  calls after admission); TierBProgram S3 (two chained runs, distinct RunIds, one log).
- **EvidenceGap**: no campaign-level construct asserts per-run independence (distinct
  StrategyId/RunId per round, per-run autonomy re-assertion, per-run invariant re-assertion)
  and bounded loop termination across N rounds.
- **GapKind**: `VALIDATION_ONLY`
- **ObservedReality**: the graduated chain composes ONE run; multi-run exists only as the S3
  demo's hand-chained two-dispatch sequence without per-round invariant re-assertion or a
  termination contract.
- **FirstDivergencePoint**: `ScenarioRunner` is single-run by design (correct); the missing
  artifact is a harness-side loop composition above it — no Runtime behavior diverges.
- **Owner**: Validation Harness / Phase 2.6 validation tooling.
- **ExcludedOwners**: RuntimeAgent, Agent, FSM, Traversal, GoalEvidence, Runtime wire,
  Strategy Contract, SourceIdentity contract, Phase 3 Memory.
- **AllowedChange**: new files `src/UniClaw.Runtime.ValidationHarness/Campaign/**`;
  new tests `tests/UniClaw.Runtime.Tests/ValidationHarness/**`; evidence docs.
- **ForbiddenChange**: any edit under `src/UniClaw.Runtime/**`, `src/UniClaw.Runtime.Adapters/**`,
  `src/UniClaw.Runtime.DriverHost/**`, `src/UniClaw.Runtime.Harness/**`,
  `src/UniClaw.Semantic.*/**`; new wire methods; Strategy Contract changes; mid-run control.
- **AcceptanceEvidence**: `dotnet build` 0 error; new capability tests green (loop
  independence, zero mid-run intervention per run, termination conditions, per-run
  re-assertion of the four invariants); `git diff` shows no Runtime-path edits.
- **StopCondition**: any need to control a run after its single accepted start, or any FDP
  that lands in a Runtime production owner.
- **SemanticResolution**: RESOLVED (validation-only gap; FDP and Owner resolved).

---

## IR-C (tasks C.1–C.4 — ScenarioKnowledgeFixture contract)

- **DesiredReality**: a scenario-scoped, provenance-gated knowledge record model with the
  seven graduated KnowledgeTypes only, full field contract incl. Status lifecycle
  (ACTIVE/STALE/CONTRADICTED/SUPERSEDED/INVALIDATED) and Supersedes/SupersededBy, admission
  that rejects records without SourceRunId+EvidenceRefs and rejects forbidden sources
  (guesswork / hardcoded text-as-truth / coordinates / fixed paths / selectors /
  probe-by-click / runtime-internal guesses), mandatory scope metadata (scenario id, app,
  capability version, Android/emulator assumptions, locale, created-from run set), and
  fresh-evidence-first conflict resolution (downgrade/supersede/invalidate, never force-apply).
- **ClaimUnderTest**: the knowledge fixture can be a pure validation asset
  (`TEST_KNOWLEDGE != RUNTIME_TRUTH != ACTION_AUTHORITY != FORMAL_MEMORY`) enforceable by
  construction — no Runtime input, no service, no DB.
- **ExistingEvidence**: spec Requirement "ScenarioKnowledgeFixture as a validation test asset"
  + design D2/D3; graduated semantic vocabulary in
  `UniClaw.Semantic.Settings.SettingsSemanticCapability` manifest kinds.
- **EvidenceGap**: no such model exists anywhere in the harness.
- **GapKind**: `VALIDATION_ONLY`
- **ObservedReality**: the harness has directive fixtures (`Fixtures/`) but no knowledge
  record model, no admission gates, no lifecycle, no conflict resolution.
- **FirstDivergencePoint**: absent validation-side asset model — nothing in Runtime diverges.
- **Owner**: Validation Harness / Phase 2.6 validation tooling.
- **ExcludedOwners**: (same as IR-B) + `UniClaw.Runtime.Memory` (this is NOT Memory).
- **AllowedChange**: new files `src/UniClaw.Runtime.ValidationHarness/Knowledge/**`;
  new tests `tests/UniClaw.Runtime.Tests/ValidationHarness/**`.
- **ForbiddenChange**: any Runtime edit; any persistence service/DB/API beyond file assets;
  new runtime semantic vocabulary beyond the seven frozen types.
- **AcceptanceEvidence**: capability tests green: admission/provenance gates, forbidden-source
  rejection, lifecycle transitions, conflict resolution (fresh wins), scope isolation.
- **StopCondition**: pressure to inject fixture content into Runtime truth or to add a Memory
  service surface.
- **SemanticResolution**: RESOLVED.

---

## IR-D (tasks D.1/D.2 — fixture persistence/versioning)

- **DesiredReality**: a frozen fixture persists as human-readable, diffable, deterministic,
  versioned artifacts under `validation/knowledge/settings/<scenario>/v<N>/`; a fresh
  validation session loads it with full round-trip fidelity; supersession across freezes is
  explicit; loading never leaks records across scopes.
- **ClaimUnderTest**: the knowledge asset can be frozen and reused across campaigns without
  any service/DB and without cross-scope contamination.
- **ExistingEvidence**: IR-C model (in flight); repo precedent for human-readable JSON assets
  (evidence files under change folders).
- **EvidenceGap**: no freeze/load/versioning mechanism.
- **GapKind**: `VALIDATION_ONLY`
- **ObservedReality**: knowledge model is in-memory only (by design in IR-C).
- **FirstDivergencePoint**: absence of validation-side persistence — nothing Runtime-side.
- **Owner**: Validation Harness / Phase 2.6 validation tooling.
- **ExcludedOwners**: Runtime (all), Phase 3 Memory service.
- **AllowedChange**: `src/UniClaw.Runtime.ValidationHarness/Knowledge/**` (persistence files),
  `validation/knowledge/**` (generated assets), tests.
- **ForbiddenChange**: Runtime paths; DB/network services; opaque-blob-only artifacts; any
  DateTime/nondeterministic content inside frozen records.
- **AcceptanceEvidence**: round-trip fidelity tests; version-supersession tests; cross-scope
  load-leak tests; assets are JSON+Markdown, deterministic.
- **StopCondition**: pressure for a Memory service or runtime-side store.
- **SemanticResolution**: RESOLVED (depends on IR-C types landing first).

---

## IR-E (tasks E.1–E.3 — PlanDelta recorder)

- **DesiredReality**: each planning round produces
  `{PreviousPlan, ObservedResult, LoadedKnowledge, NewKnowledge, RemainingUnknowns, PlanDelta, NextStrategy}`
  where PlanDelta cites EvidenceRefs/KnowledgeRefs, explains the change, names the knowledge
  used, and lands ONLY in the frozen freedom set (depth, constraints, prohibited effects,
  dispatch policy, objective, typed criterion, scope, completion); no-change rounds record
  `NO_OP_WITH_REASON`; illegal deltas (action sequences, coordinates, selectors, fixed paths,
  mid-run instructions) are rejected by a closed-vocabulary validator.
- **ClaimUnderTest**: plan adaptation is expressible — and machine-checkable — strictly inside
  the existing StrategyDirective freedom; no new contract lever is needed.
- **ExistingEvidence**: `StrategyDirective` model (the eight levers);
  `StrategyDirectiveValidator` (closed vocabulary, Phase 2.5); spec Requirement "PlanDelta contract".
- **EvidenceGap**: no round record, no delta legality validator linking citations → directive diff.
- **GapKind**: `VALIDATION_ONLY`
- **ObservedReality**: nothing exists; S3's demo embedded a result fact into a strategyId only.
- **FirstDivergencePoint**: absent validation-side planning artifact — Runtime untouched.
- **Owner**: Validation Harness / Phase 2.6 validation tooling (the "upper agent" recorder is
  validation-side; the human/leader authors the actual plans during Stages G–K).
- **ExcludedOwners**: Runtime Planner (does not exist; must not be created), Strategy Contract.
- **AllowedChange**: `src/UniClaw.Runtime.ValidationHarness/Planning/**` (new; note Runtime's
  own `Planning` namespace is untouched — harness namespace is `UniClaw.Runtime.ValidationHarness.Planning`),
  tests.
- **ForbiddenChange**: Runtime paths; new directive levers; dynamic depth; mid-run instructions.
- **AcceptanceEvidence**: evidenced-delta acceptance tests; illegal-delta rejection tests;
  NO_OP_WITH_REASON recording tests; citation resolution tests.
- **StopCondition**: a real adaptation that cannot be expressed in the eight levers →
  CONTRACT_GAP → STOPPED_AT_RUNTIME_OR_CONTRACT_GAP.
- **SemanticResolution**: RESOLVED.

---

## IR-F (tasks F.1/F.2 — SettingsStrategyBinding)

- **DesiredReality**: a harness-local `SettingsStrategyBinding : IStrategySemanticCapabilityBinding`
  adapts the PRODUCTION `SettingsSemanticCapability` typed output
  (`settings.container`, `settings.preference-row` navigation/local-control affordances,
  `settings.search-role`, `settings.navigate-up`, `settings.parent-container`) into goal
  evaluators, branch inventory, viewport exploration, candidate authorization, and dispatch
  policy — with zero fixture reads, zero UI-text truth injection beyond what the production
  capability itself emits, zero fixed paths/selectors/coordinates, zero new meanings.
- **ClaimUnderTest**: the existing typed capability vocabulary is SUFFICIENT to express the
  real-Settings traversal semantics on the strategy surface (else → Capability Gap stop).
- **ExistingEvidence**: `SettingsSemanticCapability` (production, graduated);
  `RealityFixtureStrategyBinding` (the pattern for expressing evaluators);
  real-device Settings capstone at depth 3 (legacy entry) proves the semantics suffice for
  the Agent-side traversal.
- **EvidenceGap**: no binding maps production Settings evidence to the strategy surface.
- **GapKind**: `VALIDATION_ONLY` (per the approved analysis; if implementation disproves
  sufficiency, GapKind becomes `CAPABILITY_GAP`/`CONTRACT_GAP` → stop).
- **ObservedReality**: only the fixture-app binding exists.
- **FirstDivergencePoint**: absent adapter — Runtime and capability both graduate-level.
- **Owner**: Validation Harness / Phase 2.6 validation tooling.
- **ExcludedOwners**: Runtime, `UniClaw.Semantic.Settings` production sources (read-only
  consumption only), Strategy Contract.
- **AllowedChange**: `src/UniClaw.Runtime.ValidationHarness/SettingsBinding/**`, tests.
- **ForbiddenChange**: editing `src/UniClaw.Semantic.Settings/**`; adding semantic kinds;
  hardcoding page paths/click orders/coordinates; fixture-driven runtime behavior.
- **AcceptanceEvidence**: binding purity tests (no fixture reads, no coordinates/paths/selectors,
  no new meanings — source-level assertions + behavioral); admission acceptance on a
  deterministic synthetic Settings-like observation set (reuse test env patterns).
- **StopCondition**: a necessary semantics the typed vocabulary cannot express → Human Gate.
- **SemanticResolution**: RESOLVED.

---

## IR-G1 (Stage A execution — collector/run lifecycle race on real-emulator pacing)

- **DesiredReality**: one conservative real-Settings run completes with a truthful terminal
  (Completed/Failed + reason) and its event stream collected by the harness.
- **ClaimUnderTest**: the Phase 2.5 collection chain (ResultCollector over TierAReadSurface)
  can observe a real-emulator strategy run to its true terminal at real pacing.
- **ExistingEvidence**: r1c run (scroll-test AVD): events + truthful Failed terminal — the
  chain works when the run fails fast (well under 60s). r1e run (p26_pixel AVD):
  `terminal=Idle reason=null, events=[]` while the observation tap recorded **13 frames** —
  the agent demonstrably executed far longer than the collector's bounded wait
  (`MaxTerminalPolls=600 × 100ms ≈ 60s`; pixel-AVD observe cycles are seconds each).
- **EvidenceGap** (RESOLVED with file:line proof): the leader's initial
  hypothesis (post-release `RunSnapshot.Unknown` → enum-default Idle) is structurally
  possible but REFUTED for r1e: the report classified `runState` as DirectProjection from a
  REGISTERED snapshot. The actual mechanism is the **pinned admission projection**:
  `StartStrategyRun` registers `AgentStateSnapshot.From(graph.Agent)` (a BY-VALUE copy of
  the fresh Idle agent) + an empty placeholder trace; during the whole run `GetRunSnapshot`
  serves ONLY that pinned Idle snapshot and the event store serves the empty projection;
  the truthful terminal + full event stream are materialized only by the finally-block's
  `ReplaceRunProjection`, after which the observability registration persists forever
  (coordinator release removes only the coordinator-side entry). The collector's 60s wait
  therefore can never observe a long run's terminal and final-reads the placeholder.
- **GapKind**: `TEST_HARNESS_GAP` (CONFIRMED — composition-layer collection timing; Runtime
  projection semantics are correct and graduated)
- **ObservedReality**: admitted ✓, autonomy ✓, invariants ✓, 13 observations consumed,
  collector output Idle/null/empty, gates false.
- **FirstDivergencePoint**: campaign executor's collection timing vs coordinator's
  run-record lifecycle (release-at-terminal) — the graduated ScenarioRunner chain was
  calibrated for fast runs; nothing in Runtime diverges.
- **Owner**: Validation Harness (SettingsCampaign executor/composition).
- **ExcludedOwners**: RuntimeAgent, RunExecutionCoordinator (production, read-only),
  DriverHost wire.
- **AllowedChange**: `src/UniClaw.Runtime.ValidationHarness/SettingsCampaign/**`;
  evidence docs. NOT ResultCollector (shared graduated file — if a wait extension is truly
  needed there, return BLOCKED with the exact proposal).
- **ForbiddenChange**: Runtime/DriverHost production edits; weakening autonomy/boundary
  assertions; fabricating terminal state.
- **AcceptanceEvidence**: a real-emulator run whose collected terminal matches the run's
  actual outcome (events non-empty; state Completed or Failed with truthful reason); gates
  evaluated over the REAL terminal; per-round autonomy/invariants still pass.
- **StopCondition**: proof shows the coordinator/observability cannot truthfully serve
  post-terminal reads to an external collector → Runtime-owner FDP →
  STOPPED_AT_RUNTIME_OR_CONTRACT_GAP.
- **SemanticResolution**: RESOLVED — proven (pinned admission projection), fixed
  (`SettingsCampaignProgram` pre-collection terminal wait: `host.Runs` release +
  observability terminal state, 40-min bound, never fabricates), and VERIFIED on the real
  emulator: real terminal (Failed: normalization-unresolved — the separate IR-G0
  phenomenon), 4 non-empty events, gates evaluated on the real terminal, autonomy +
  invariants pass, exactly one `run.strategy.start`.

---

## IR-G0 (Stage A — real-vision duplicate detections × open-world normalization precondition)

- **DesiredReality**: a conservative real-Settings run normalizes its root viewport inventory
  and proceeds to bounded exhaustive traversal.
- **ClaimUnderTest**: the graduated open-world machine normalizes REAL Settings observations
  on the strategy wire.
- **ExistingEvidence**: r1c/r1e runs (both AVDs): runtime correctly fail-closes at
  `TryBuildContainerInventoryCompleteness` → "Source normalization is unresolved; completeness
  cannot be proven." Frames (`/tmp/p26-frames.json`): same-frame duplicate detections —
  scroll-test AVD: `Passwords, passkeys & accounts` ×2, `About emulated device` ×3;
  p26_pixel AVD: `Network & internet` ×3, `Notifications` ×4 — i.e. NOT an AVD rendering issue
  (ENVIRONMENT_GAP hypothesis REFUTED by cross-AVD evidence).
- **Mechanism (proven at source)**: YOLO emits multiple same-class boxes per visual row
  (fusion dedup exists only for switch/toggle/raw-pixel regions —
  `platforms/perception/uniclaw_perception/fusion/heuristics.py:359,542`; menu_item rows are
  not deduplicated). Each duplicate box = a distinct vision occurrence → production
  `SettingsSemanticCapability` emits one NavigationCandidate evidence per occurrence
  (`GroupBy(OccurrenceId)`) → `InteractionAffordanceAnalyzer` reduces each to a
  NavigationCandidate occurrence → `SourceEquivalenceNormalizer.ExtractNavigationSignatures`
  produces identical `Text|PerceptionType` signatures in one frame → `Normalize` rejects
  duplicate in-frame signatures (graduated fail-closed contract, PROV repair).
- **Why the graduated precedents did not hit this**: the real-device Settings Phase-2 test
  walked a FIXED 31-step plan with its own first-seen-dict inventory (no open-world
  normalization); the fixture capstone used a purpose-built app whose rows produce single
  detections; the TREE capstone used a synthetic perfect world. The proposal itself states
  the recursive machine "has never executed on the strategy wire against a real unknown
  tree" — this is exactly that gap, now observed as a real composition boundary.
- **GapKind**: composition gap spanning PRODUCTION perception output × RUNTIME frozen
  normalization precondition. No single owner is wrong: perception truthfully reports its
  detections; the Runtime truthfully refuses ambiguous normalization.
- **ObservedReality**: both AVDs fail-closed at the root page; 13-frame evidence on pixel AVD.
- **FirstDivergencePoint**: the first observation's in-frame signature set contains
  duplicates (perception output), which collides with the Runtime's frozen
  one-signature-per-source-per-frame precondition.
- **Owner**: UNRESOLVED between (a) production perception pipeline (`platforms/perception`,
  NOT authorized by this change), (b) Runtime normalization contract (FROZEN), (c) harness
  scope freedom (AUTHORIZED: directive scope root + launch intent are harness-composition
  levers).
- **ExcludedOwners**: none excluded yet — this IR records the boundary honestly.
- **AllowedChange (harness side only)**: probe whether a SMALLER real Settings subtree
  (e.g. a directly-launched subpage via `android.settings.WIRELESS_SETTINGS` whose rows
  produce fewer/no duplicate detections) normalizes cleanly. If yes: Stage A/B proceed with
  scope-root adaptation as the FIRST genuine knowledge-driven PlanDelta (root KnownUnresolved
  → scope收紧), which is precisely the capability Phase 2.6 validates; the root-page
  phenomenon stays recorded as honest KnownUnresolved knowledge. If no subtree normalizes:
  full traversal is blocked by a production-layer composition gap.
- **ForbiddenChange**: editing `platforms/perception/**` (production perception infra,
  outside this change's authorization), `src/UniClaw.Semantic.Settings/**`, Runtime.
- **AcceptanceEvidence**: subpage probe frames showing either clean single-detection rows
  (normalize-able) or duplicates (stop evidence).
- **StopCondition**: if NO reachable real Settings scope normalizes under the frozen
  contract with the production perception pipeline → STOPPED_AT_RUNTIME_OR_CONTRACT_GAP with
  this IR as FDP evidence, awaiting Human Gate (possible human resolutions: authorize
  perception dedup work, authorize a perception deployment switch with evidence, or accept
  a simulator-boundary conclusion).
- **SemanticResolution**: PARTIAL — mechanism proven; subpage probe pending (blocked on the
  G1 worker's concurrent edit of SettingsCampaignProgram.cs; probe to follow its landing).

---

## IR-G2 (Stage B — evidence-informed adaptation planner, validation-side "upper agent")

- **DesiredReality**: a harness-side planner (UniAgent emulator, mirroring Phase 2.5's
  "agent loop acting as UniAgent emulator") consumes prior CampaignRoundOutcomes +
  ScenarioKnowledgeFixture, admits knowledge from evidence (provenance-gated), authors the
  next round's directive through contract-legal PlanDeltas only, and records every round
  via the PlanningRound/PlanDeltaValidator artifacts.
- **ClaimUnderTest**: `UPPER_AGENT_CROSS_RUN_PLAN_ADAPTATION` is achievable with the frozen
  directive freedom + the landed B/C/E components — no new levers, no Runtime input.
- **ExistingEvidence**: IterativeCampaignRunner planner seam; ScenarioKnowledgeFixture
  admission/conflict; PlanDeltaValidator closed-vocabulary checks; Settings binding
  Compile acceptance.
- **EvidenceGap**: no composition turns round outcomes → knowledge → validated PlanDelta →
  next directive.
- **GapKind**: `VALIDATION_ONLY`
- **ObservedReality**: planner in SettingsCampaignProgram is a fixed conservative ladder.
- **FirstDivergencePoint**: absent validation-side planning composition — Runtime untouched.
- **Owner**: Validation Harness.
- **ExcludedOwners**: Runtime Planner (must not be created), Strategy Contract.
- **AllowedChange**: new files `src/UniClaw.Runtime.ValidationHarness/SettingsCampaign/Adaptation/**`
  + tests `tests/UniClaw.Runtime.Tests/ValidationHarness/**`. NO edits to existing files.
- **ForbiddenChange**: Runtime paths; new directive levers; dynamic depth; UI
  action/coordinate/selector outputs; fixture→runtime injection.
- **AcceptanceEvidence**: unit tests over a deterministic fake round history: ≥3 legal
  adaptation rounds with resolvable citations; illegal deltas rejected; NO_OP_WITH_REASON
  path; knowledge admission provenance-gated.
- **StopCondition**: an adaptation the eight freedoms cannot express → CONTRACT_GAP → stop.
- **SemanticResolution**: RESOLVED.
