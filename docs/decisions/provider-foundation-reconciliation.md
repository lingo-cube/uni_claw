# PROVIDER FOUNDATION RECONCILIATION — Provider Matrix + First Vertical Slice Plan

- **Authority**: `PROJECT_LEADER_UNICLAW_PROVIDER_FOUNDATION_RECONCILIATION`
- **Date**: 2026-08-12
- **Mode**: READ-ONLY reconciliation / planning. **No production code changes made.**
- **Predecessors (closed)**: Perception Platform CLOSED (user-declared, `perception-phase3-phase4-semantic-enforcement-closure.md`); S0 graduated (`s0-graduation.md`, HUMAN_AUTHORIZE_S0_GRADUATION 2026-08-09); ML Governance sufficient (not extended).
- **Roadmap position**: Provider Foundation = NEXT. Candidate-vs-ACTIVE / ReleasePolicy explicitly deferred.
- **Method**: first-hand source reads (port/consumers/adapters/translator/dispatch/lowerer/capability/scenario tests) + 3 independent background inventories (contracts+implementations; integration chain; reality proof + scenario assets). All verdicts below carry file:line evidence.

---

## 1. Current chain state — Goal → Capability → Provider → Physical Reality

| Hop | Status | Evidence |
|---|---|---|
| Agent Goal → Intent | ✅ Implemented, deterministic (Phase 1, no LLM) | `Goal` / `SemanticGoalInput` (Model); `Agent.RunSemanticGoalAsync` (Agent/Agent.SemanticRun.cs:29-35); `BusinessIntent → IntentCompiler.Compile → Resolved(SemanticGoalInput)` (IntentCompiler.cs:34-49) |
| Agent reasoning (READ → DECIDE) | ✅ Implemented | `Agent.SemanticRun.cs` READ belief → DECIDE → SELECT → AUTHORIZE → binding check → LOWER → execute → observe → verify → re-evaluate; terminates SATISFIED / STATE_EVIDENCE_REQUIRED / BINDING_UNRESOLVED / SEMANTIC_CONTRADICTION / BUDGET_EXHAUSTED / EXECUTION_FAILED |
| Capability selection | ✅ Implemented, declarative | `Capability` record (Model/Capability.cs:19); `SelectCapability` matches `ApplicableToCategory` + `StateDimension`, must be exactly one (Agent.SemanticRun.cs:111-117, :185-191). Agent never selects a provider — only a capability. |
| SemanticAction → DeviceAction | ✅ Implemented | `SemanticActionLowerer.Lower` (Traversal/SemanticActionLowerer.cs:78-83); unknown switch → NO DISPATCH; already-satisfied → NoOp; SetEnabled = idempotent desired-world semantics, **not** a physical toggle. |
| DeviceAction dispatch port | ✅ Implemented (port + one production impl) | `IEnvironment` (Environment/IEnvironment.cs:22,33) — pure port; consumers: `Traversal` (Traversal.cs:56, :115, :228), `Startup` (Startup.cs:33, :61), `Recovery` (Recovery.cs:33, :71, :83). Agent core deliberately does not reference IEnvironment (Agent/Agent.cs:25; initial observation injected, post-action observation from Traversal journal). |
| Production composition | ❌ **MISSING — the entire gap** | No `Program`/`Main`/DI anywhere in `src/` (4 projects: Runtime / Adapters / Harness / Vision.Host). `PhysicalEnvironment` (Adapters/PhysicalEnvironment.cs:32) constructed in exactly one file — `PhysicalEnvironmentCompositionTests.cs` (:40,:61,:87,:110,:133,:157,:178,:197,:236) — always with stub sources, **never run through an Agent**. Every Agent run in the repo injects a test-side fake (`ScriptedEnvironment`, `ReplayEnvironment`, `SimulationEnvironment`). No Fake-vs-Physical switch, no registry (design explicitly forbids one: "No Provider framework, registry, or plugin system" — trace-capture-scenario-catalog-foundation/proposal.md:33; runtime-observability-trace-foundation/design.md:26,135; switch-state-reading/proposal.md:62). |
| Physical Reality | ❌ Not exercised by any test | No test invokes real `adb` or a device. Real subprocess invocations: `/bin/sleep` for kill semantics (Pf01ConcreteAdbMechanismTests.cs:151-167); real `python3` production perception server (VisionHostFactoryCompositionTests.cs:82-107,185-212); real `python3` fixture server (/tmp/vh_test_server.py). |

**Root-cause statement**: the provider/adapter layer is complete and isolation-tested, but from the Runtime's perspective it is **unreachable dead code** — the single missing artifact is a production composition root that wires `Agent/Startup/Traversal/Recovery → IEnvironment → PhysicalEnvironment → (AdbScreenshotSource, LocalVisionPerceptionSource, ImageSwitchStateProvider, AdbDispatchTarget)`. Design explicitly defers this: "真实 attach 由 Phase 4 Adapter 接入 — I-12" (Startup.cs:57; `AttachAsync` is a literal no-op at :99-104); sync-over-async seams in Traversal (Traversal.cs:39-41 — "Phase 4 接入真实 IO 时改为异步形状").

---

## 2. Provider Matrix (deliverable)

Columns: **Contract** (a declared boundary the Runtime/adapters can program against) | **Implemented** (working production code exists) | **Integrated** (reachable from a real Agent run) | **Reality Proven** (proven against real process/device/world behavior, not fakes).

| Provider | Contract | Implemented | Integrated | Reality Proven | Key evidence |
|---|---|---|---|---|---|
| **Screenshot** | ✅ `IScreenshotSource` (Adapters/PhysicalEnvironment.cs:182) | ✅ `AdbScreenshotSource` — real `adb -s <serial> exec-out screencap -p` (Device/AdbScreenshotSource.cs:25) over `AdbProcessRunner` (real `Process.Start`, bounded 64 MiB capture, timeout, tree-kill; AdbProcessRunner.cs:28-129) | ❌ — only reachable inside `PhysicalEnvironment` (test-composed); no production root | ❌ — argv + failure-mode proofs vs `FakeRunner` (Pf01ConcreteAdbMechanismTests.cs:59-88); only real-process proof is `/bin/sleep` kill semantics (:151-167); real PNGs only as **committed recorded assets** (Perception/Assets/settings-home-api35-full.png, wifi-on/off-emulator-5554.png) | |
| **ADB Dispatch** | ✅ `IAdbDispatchTarget` (PhysicalEnvironment.cs:225) + `DeviceActionTranslator` (Operator/DeviceActionTranslator.cs) | ✅ `AdbDispatchTarget` — `shell input tap/swipe`, `shell monkey -p <pkg> 1` (Operator/AdbDispatchTarget.cs:39-45); device selection: `AdbDeviceResolver` + `AdbDevicePreflight` (Device/) | ❌ — never wired (AdbDeviceResolver/Preflight not even wired into PhysicalEnvironment by any root; exercised only by tests); Runtime port consumers only ever see fakes | ❌ — command strings proven vs `FakeRunner` (Pf01ConcreteAdbMechanismTests.cs:90-138,169-198); resolver/preflight proven against synthetic listings; **no test runs the real `adb` binary**; receipts honestly marked "world effect is unverified" (AdbDispatchTarget.cs:36) | |
| **Vision** | ✅ `IPerceptionSource` (PhysicalEnvironment.cs:201) + Runtime evidence port `ISwitchStateReader` (Capabilities/Perception/Vision/ISwitchStateReader.cs:36) + `SwitchStateValidation` | ✅ `LocalVisionPerceptionSource` (real YOLO/OCR), `ImageSwitchStateProvider` (switch-state reading), `VisionServiceHost` + `CanonicalVisionHostFactory` (spawns real `python3 platforms/perception/uniclaw_perception/server.py`; Vision.Host/VisionServiceHost.cs:139-163) | 🟡 PARTIAL — provider chain wired inside `PhysicalEnvironment.ObserveAsync` (PhysicalEnvironment.cs:101-121) but reachable only when PhysicalEnvironment is composed (tests only); Runtime `ISwitchStateReader` has no production consumer outside PhysicalEnvironment; switch-state enrichment reaches runs only via test-only `PerceptionEnvironment` decorator (SwitchStateReaderIntegrationTests.cs:21-22, :205-210) | 🟡 PARTIAL — **strongest reality proof in the repo**: real python3 production server reaches Healthy, restarts re-verify, receipt-mutation fails closed (VisionHostFactoryCompositionTests.cs:82-107, CORR_HOST03/04/09); switch-state reading proven on recorded real screenshots (RealImageClassifierTests.cs:48-58; LiveCalibrationTests.cs:45-140 — emulator-5554, independently verified states); **no live device, no live-frame loop** | |
| **WiFi** | ✅ semantic/capability level only — Goal/`SetSwitch` on ConnectivitySetting (`SemanticObject.cs:22`); **no physical WiFi provider contract** | 🟡 PARTIAL — semantic loop + `SetSwitch` lowering exist; physical action = **ADB tap at switch coordinates** (DeviceActionTranslator.cs:61-74; asserted PhysicalEnvironmentCompositionTests.cs:148); **no `svc wifi`/`cmd wifi`/WifiController class anywhere in `src/`** | ❌ — SC-P1-001 explicitly Fake Environment ("场景全程不依赖真实设备、不依赖 LLM" — normal-wifi-scenario/spec.md:38; catalog.md:263-264); only `ScriptedEnvironment.SetSwitch` fake-screen mutation (Scenario/Fakes/ScriptedEnvironment.cs:192-213) | ❌ — "Reality-seeded" = **recorded element text on the fake world**; the OFF→ON transition is explicitly **SYNTHETIC** — "no recorded OFF→ON pair exists" (RealitySeededWifiScenarioTests.cs:7-13; RealitySeededSettingsFixture.cs:30-40, markers `SYNTHETIC_STATE_TRANSITION_PENDING_REALITY_CALIBRATION` at :138,:147,:158) | |
| **App Launch** | ✅ `DeviceAction.LaunchApp` (Model/Actions/DeviceAction.cs) + `AdbOperation.Launch` (Operator/DeviceActionTranslator.cs:96) | ✅ full path: `Startup.ExecuteAsync` issues `LaunchApp` (Startup.cs:61) → `TranslateLaunch` (DeviceActionTranslator.cs:40-46) → `adb -s <serial> shell monkey -p <pkg> 1` (AdbDispatchTarget.cs:41) | ❌ — semantic path runs only against fakes; monkey command never executes | ❌ — argv-string proof only (Pf01ConcreteAdbMechanismTests.cs:101-102); scenario-level "launches" are fake-world screen switches (NormalWifiHappyPathTests.cs:139) | |

**Corrections to the sketched matrix (user sketch → verified)**:

| Provider | Sketch | Verified | Delta |
|---|---|---|---|
| Screenshot | yes / yes / partial / no | yes / yes / **NO** / no | Integrated is NO, not partial — nothing wires it |
| ADB Dispatch | yes / yes / partial / no | yes / yes / **NO** / no | same |
| Vision | yes / yes / yes / partial | yes / yes / **PARTIAL** / **PARTIAL** | Integrated only partial (provider chain is wired inside PhysicalEnvironment but that composition is test-only); Reality Proven partial via real python3 host + recorded assets — the strongest row |
| WiFi | ? / ? / no / no | **yes (capability-level contract)** / **PARTIAL** / no / no | a contract exists at the semantic/capability level; physical mechanism = tap-at-coordinates, not a wifi provider |
| App Launch | ? / ? / no / no | **yes** / **yes** / no / no | fully implemented path (monkey) — the user's "?" were overly pessimistic |

**Completeness sweep**: zero TODO / NotImplementedException / NotSupportedException / stub markers in `src/**/*.cs`. Deliberate partials: index-only tap/SetSwitch translation rejected without bounds (DeviceActionTranslator.cs:57-58, :73); `AdbOperation.KeyEvent` never produced by any translator (AdbDispatchTarget.cs:44, :99); ScrollForward hard-coded as 70%→30% swipe (DeviceActionTranslator.cs:76-85); Container/Observation Fingerprint DEFER (Container.cs:15-16, :37; Observation.cs:8).

---

## 3. First vertical slice — evaluation of `PHYSICAL_WIFI_OFF_TO_ON_MINIMUM_SEMANTIC_LOOP`

Chain under test: **Goal → Agent reasoning → Capability selection → Provider dispatch → Physical change → Screenshot → Perception → State verification**.

| Link | Readiness | Notes |
|---|---|---|
| Goal → Agent reasoning | ✅ Ready | Deterministic semantic loop, no LLM (Phase 1 charter) |
| Capability selection | ✅ Ready | Declarative, exactly-one match |
| Provider dispatch | 🟡 Code ready, **composition missing** | Full path exists (IEnvironment → PhysicalEnvironment.ExecuteAsync → DeviceActionTranslator → AdbDispatchTarget → adb); requires the missing production composition root + device/emulator access |
| Physical change (WiFi OFF→ON) | ❌ **Hardest link** | No physical WiFi provider; physical mechanism = ADB tap at switch coordinates, which requires Vision to ground the switch element first; the OFF→ON transition has **no recorded reality pair** (`SYNTHETIC_STATE_TRANSITION_PENDING_REALITY_CALIBRATION`) |
| Screenshot | 🟡 Code ready, unproven on device | `AdbScreenshotSource` complete; device-verified only via committed recorded assets |
| Perception → Vision | 🟡 Host proven; content unproven live | Real python3 server proven Healthy (CORR_HOST03/04); `ImageSwitchStateProvider` proven on recorded assets only |
| State verification | ❌ Least-proven live | `ISwitchStateReader` must read the post-toggle state from a live screencap — LiveCalibrationTests (emulator-5554) are the only partial calibration |

**Verdict**: the WiFi slice is the **right semantic target** — it exercises every hop of the chain the user wants proven (Goal → … → State verification) and maps exactly to the Main Roadmap's "Agent Semantic Loop". But as literally the *first* slice it bundles the two hardest, least-proven links (physical toggle reality calibration + live switch-state verification) on top of the missing composition root.

**Recommendation — keep the slice, sequence it** (composition-root-first within the same slice):
1. **Task A — Production composition root** (the shared prerequisite for every future slice): new entry point / host project wiring `AdbDeviceResolver/Preflight → PhysicalEnvironment(AdbScreenshotSource, LocalVisionPerceptionSource, ImageSwitchStateProvider, AdbDispatchTarget) → Agent/Startup/Traversal/Recovery`. Must also lift the Phase-1 sync-over-async seam in Traversal (Traversal.cs:39-41) and the `AttachAsync` no-op (Startup.cs:99-104) — both carry explicit "Phase 4 接入真实 IO" markers, i.e., this slice is where those deferred seams land.
2. **Task B — Emulator reality calibration** (not real device): record the missing OFF→ON WiFi transition pair on emulator (committed assets already come from emulator-5554 — LiveCalibrationTests.cs:12-25), replacing the `SYNTHETIC_STATE_TRANSITION_PENDING_REALITY_CALIBRATION` markers. Emulator is the defensible middle ground: charter §33 forbids real phones in Phase 1 ("第一阶段不要连接真实手机", charter:1707-1732) but the 12 deterministic-scenario criteria (charter:2105-2116) are met since S0 graduated (2026-08-09) — **the §33 gate decision (emulator vs device) needs an explicit authority statement in the slice's OpenSpec proposal**.
3. **Task C — End-to-end loop**: Goal("WiFi ON") → reasoning → capability → dispatch (tap at grounded switch) → screencap → perception → `ISwitchStateReader` verification → SATISFIED. SetSwitch stays idempotent desired-world semantics (not a toggle); dispatch success never counts as world-effect evidence (I-4 / 裁决 10 — the loop must re-observe to verify).

**Process requirement**: this slice is new spec-driven work → must be proposed as an **OpenSpec change** (repo is system of record; e.g. `openspec/changes/phase4-provider-foundation/` or similar) with proposal/design/specs/tasks, per the OpenSpec lifecycle. This reconciliation document is planning-only and does not authorize implementation.

**Deferred (explicit)**: real-device expansion beyond emulator; Candidate-vs-ACTIVE / ReleasePolicy; any provider registry/plugin framework (design-forbidden); ML Governance extension.

---

## 4. Status

- Status: **PLANNING_COMPLETE_AWAITING_AUTHORITY** — reconciliation delivered (matrix + slice plan); implementation requires the user's go-ahead for the OpenSpec change proposal.
- Provider Matrix verdicts: Screenshot ✅✅❌❌ · ADB Dispatch ✅✅❌❌ · Vision ✅✅🟡🟡 · WiFi ✅(capability)🟡❌❌ · App Launch ✅✅❌❌
- Single root-cause gap: **no production composition root** — providers are complete, isolation-tested, unreachable.
- First slice: `PHYSICAL_WIFI_OFF_TO_ON_MINIMUM_SEMANTIC_LOOP` confirmed as the semantic target, sequenced composition-root → emulator reality calibration → end-to-end loop.
