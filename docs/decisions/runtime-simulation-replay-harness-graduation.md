# Runtime Simulation & Replay Harness — Graduation Record

> Date: 2026-08-11
> Decision: `RUNTIME_SIMULATION_REPLAY_HARNESS = GRADUATED`
> Core baseline: `1bc0505`
> Harness architecture/initial implementation: `3bf182f`
> Supplemental simulation-pressure baseline before closeout: `23bc675`
> Canonical architecture: `docs/decisions/runtime-simulation-replay-harness-architecture.md`

## 1. Graduation boundary

The Harness surrounds the graduated Runtime through test-side `IEnvironment` adapters. It does not own semantic decisions, mutable Runtime state, target selection, execution authority, or completion authority.

Core contracts and owners remain unchanged:

- Agent semantic authority: unchanged.
- Container mutable ownership: unchanged.
- Traversal lowering, dispatch protocol, and fresh verification authority: unchanged.
- `IEnvironment`, `SemanticGoalInput`, `SemanticAction`, and `GoalEvidence`: unchanged.
- Runtime Architecture Contract I-1 through I-14: unchanged.

No file under `src/UniClaw.Runtime/` changed in the Harness implementation or closeout.

## 2. Delivered implementation inventory

| Capability / contract | Status | Repository evidence |
|---|---|---|
| DeviceProfile | IMPLEMENTED | Versioned stable ID, optional device metadata, explicit Unknown values |
| CaptureSession | IMPLEMENTED | Versioned ID, provenance, device/Frame/Trace references |
| Frame | IMPLEMENTED | Versioned `FrameAsset`; Observation and Artifact references; screenshot optional |
| Artifact | IMPLEMENTED | Versioned stable ID, provenance, content hash, source lineage |
| FrameRelation | IMPLEMENTED | Versioned stable relation ID and explicit source/target Frame IDs |
| Trace / TraceEvent | IMPLEMENTED_SCHEMA | Versioned first-class history asset with ordered typed event/reference fields; capture deferred |
| Scenario | IMPLEMENTED | Versioned behavior-oriented input/world/expected contract |
| Provenance | IMPLEMENTED | SYNTHETIC / REALITY_SEEDED / RECORDED_REALITY / LIVE_CAPTURE |
| SimulationMode | IMPLEMENTED | S0 through S4 are explicitly represented |
| ReplayMode | IMPLEMENTED | Observation / Perception / Trace are distinct |
| Observation Replay | IMPLEMENTED | Fail-closed `ReplayEnvironment : IEnvironment`, persistent manifest adapter, real Runtime regression |
| Stateful Simulation | IMPLEMENTED | `SimulationEnvironment : IEnvironment`, deterministic state and bounded fault injection |
| Scenario Runner | PARTIAL | Persistent Scenario drives the permanent Wi-Fi replay regression; no generic runner component was purchased |
| Conformance Suite | IMPLEMENTED | Stateful Runtime conformance plus Observation Replay suites |
| Reality Asset migration | PARTIAL | One honest REALITY_SEEDED Wi-Fi manifest is local; external raw A3/A4/B1 assets were not copied |
| Trace Replay | PARTIAL | Stable schema/consumption boundary; production capture and trace-driven driver deferred |
| Perception Replay | DEFERRED | No callable physical perception adapter exists in this repository |
| Failure Injection | PARTIAL | Rejected, timeout/world unchanged, dispatched/world unchanged, binding loss, persistent index drift, and non-convergence are executable; remaining architecture-table variants are deferred |
| CI/test integration | IMPLEMENTED | Included by the standard test project and full solution regression |

The repository contains no normative SRH-01..SRH-11 task registry. Completion is therefore classified by the actual capability/contracts above rather than inventing slice names. The architecture document's named SRH-00 inventory, SRH-02 serialized contracts, and SRH-03 Observation Replay outcomes are implemented.

## 3. Conceptual boundaries

The graduated schemas preserve:

- Frame is a captured evidence context; Screenshot is an Artifact referenced by a Frame.
- Trace is ordered evidence/decision history; it is not a prose log and never becomes current-world truth.
- Scenario declares behavioral pressure and expected observable outcome; it is not an executed Trace.
- DeviceProfile describes device context; it does not encode a Scenario.
- Replay emits fixed recorded external evidence/responses; Stateful Simulation mutates a modeled world.
- Observation Replay begins from Runtime `Observation`; Perception Replay begins from raw visual assets.
- REALITY_SEEDED is terminal provenance and is never promoted to RECORDED_REALITY by passing tests.
- Scenario assertions avoid private class names, method-call order, and diagnostic text protocols.

## 4. Persistent schema and image association

Asset schema version is `1`. Every persistent record has an explicit `SchemaVersion`, stable ID where it is independently addressable, and explicit provenance where evidence maturity applies. CLR type names are not persistent identity.

`FrameAsset` has explicit `ScreenshotArtifactId`, `NormalizedScreenshotArtifactId`, and `ArtifactIds`. A Frame may omit screenshots. Raw screenshots require a `sha256:` content hash. Derived artifacts retain `DerivedFromArtifactId` and a transformation description. Explicit Frame relations and FrameSequence ordering replace filename ordering. `ObservedBeforeAction` and `ObservedAfterAction` express action context without claiming semantic page identity; scroll/multi-frame evidence can remain multiple related Frames in one sequence/session.

## 5. Trace and Scenario

`TraceAsset` is first-class, versioned, ordered, and may reference Frame, Observation Artifact, Action, ActionResult, and GoalEvidence through dedicated ID fields. `TraceEventType` is the machine discriminator. `Reason` and `Message` are `DIAGNOSTIC_ONLY`.

`ScenarioAsset` contains only:

- INPUT;
- WORLD / ASSET SOURCES;
- EXTERNAL RESPONSE through its Replay reference;
- EXPECTED OBSERVABLE BEHAVIOR;
- SAFETY ASSERTIONS;
- FINAL RESULT.

It contains no private Runtime class or call-order assertions.

`TRACE_CAPTURE_CAPABILITY = DEFERRED`. The schema and Observation Replay boundary are stable; the graduated Agent is not modified to capture more history.

## 6. Provenance audit

| Evidence | Source | Device | Raw / derived | Maturity | Supported replay | Missing metadata / boundary |
|---|---|---|---|---|---|---|
| A3 | EP-04 sim-replay export | Not established in this repository | External source; normalized/reconstructed fields used by local fixture | REALITY_SEEDED locally | Observation Replay after explicit manifest normalization | Raw export, source device, direct content hashes not local |
| A4 | E-10 TraceReplay fixtures | Not established in this repository | External real-run-derived hierarchy; normalized into fixture | REALITY_SEEDED locally | Observation Replay after explicit manifest normalization | Raw trace/capture metadata not local |
| B1 | PKJ110 real-device golden | OPPO PKJ110 known for external golden | Raw golden remains in sibling uni-claw repository; local semantic use is derived | RECORDED_REALITY at source; REALITY_SEEDED in this corpus | Observation Replay of derived fields | Raw screenshot and hash are not copied into uni-agent |
| RealitySeededSettingsFixture | Curated A3/A4/B1 Settings elements plus manually authored behavior | Mixed/partially unknown | Derived; Wi-Fi detail page and OFF→ON effect are synthetic | REALITY_SEEDED | Existing scenarios and new persistent Observation Replay | No directly recorded OFF→ON pair |
| `settings-wifi-reality-seeded-v1.json` | Minimized manifest derived from the fixture/evidence above | Android source, exact device deliberately Unknown | Derived Observation/Trace/Scenario; screenshotless | REALITY_SEEDED | S2 Observation Replay | Raw images and direct capture times deliberately absent |

No local asset is labeled RECORDED_REALITY. Passing replay tests does not change maturity.

## 7. Reality to regression and failure flow

The permanent lifecycle is:

`A3/A4/B1-derived Settings evidence` → versioned REALITY_SEEDED manifest → Frames/Observations/provenance → behavior Scenario → fail-closed Observation Replay → real Agent/Container/Traversal Runtime → fresh GoalEvidence assertion → permanent regression.

The failure workflow is representable without a production recorder:

`captured failure assets` → immutable/minimized manifest → ordered Frames + recorded external responses → behavior Scenario → fail-closed replay → regression assertion.

A rejected-response replay is executable through the real Runtime. Production capture remains a separate deferred capability.

## 8. Simulation and test classification

| Mode | Status | Evidence class / reason |
|---|---|---|
| S0 COMPONENT | SUPPORTED | Existing pure component tests, SYNTHETIC |
| S1 DETERMINISTIC RUNTIME SIMULATION | SUPPORTED | Real Runtime over stateful `SimulationEnvironment`, SYNTHETIC |
| S2 OBSERVATION REPLAY | SUPPORTED | Real Runtime over versioned manifest + `ReplayEnvironment`, REALITY_SEEDED supported |
| S3 PERCEPTION REPLAY | DEFERRED | Physical perception adapter unavailable |
| S4 LIVE CALIBRATION | DEFERRED | No live adapter/capture loop purchased |

Test taxonomy:

- T1_COMPONENT: existing World/Planning/Traversal pure component suites.
- T2_RUNTIME_CONFORMANCE: `SimulationConformanceTests` and graduated Scenario suites.
- T3_RECORDED_REALITY_REPLAY: no truthful local corpus yet; the current permanent replay is REALITY_SEEDED.
- Perception Replay: deferred.
- T4_LIVE_CALIBRATION: deferred.

## 9. Conformance / invariant map

| Invariant | Protection |
|---|---|
| I-1 responsibility direction | BOTH — Architecture Guard/dependency isolation plus real-spine conformance/replay |
| I-2 single mutable owner | ARCHITECTURE_GUARD / code review; Harness assets are immutable and adapters own only local histories |
| I-3 single decision authority | BOTH — no-caller-action conformance and unchanged Core boundary |
| I-4 Observation is evidence | EXECUTABLE_SCENARIO — UNKNOWN/refusal and fresh evidence proofs |
| I-5 Plan is hypothesis | EXECUTABLE_SCENARIO — semantic path requires no caller PlanStep |
| I-6 Fingerprint is evidence | NOT_BEHAVIORALLY_TESTABLE in this Harness; frozen contract unchanged |
| I-7 FSM protocol only | ARCHITECTURE_GUARD / source audit; no FSM added |
| I-8 lower scope escalates | EXECUTABLE_SCENARIO — binding/grounding/recovery escalation corpus |
| I-9 Recovery verify loop | EXECUTABLE_SCENARIO — existing Phase 2/3 recovery verification regressions |
| I-10 GoalEvidence completion | BOTH — fresh GoalEvidence replay/conformance plus contract guard |
| I-11 no legacy Runtime structure | ARCHITECTURE_GUARD |
| I-12 no unsupported complexity | BOTH — deferred receipts and source audit |
| I-13 no God context | ARCHITECTURE_GUARD / code review; no Runtime context added |
| I-14 AI not truth/sole path | EXECUTABLE_SCENARIO — all Harness baselines execute without AI |

Canonical conformance covers already-satisfied zero mutation, UNKNOWN refusal, dispatch/effect separation, fresh completion evidence, binding loss, retry/recovery regressions, stale index re-grounding, no caller `PlanStep`/`DeviceAction`, and bounded non-convergence.

## 10. Deferred capability receipts

- `SwitchStateReader = IMPLEMENTATION_DEFERRED`
- `ImageSwitchStateProvider = IMPLEMENTATION_DEFERRED`
- `PhysicalProductionPerceptionAdapter = DEFERRED`
- `PerceptionReplay = DEFERRED_BY_PERCEPTION_ADAPTER`
- `LiveCalibration = DEFERRED`
- `ProductionTraceCapture = DEFERRED`
- `ProviderFramework = NOT_AUTHORIZED`
- `StateClassifier = NOT_AUTHORIZED`
- `VLM = NOT_AUTHORIZED`
- `Memory = NOT_AUTHORIZED`

These are deferred or unauthorized, not failed, and none is implemented by this closeout.

## 11. Temporary debt closeout

- Removed the duplicated toggle-target lookup in `SimulationEnvironment`.
- Corrected index-drift simulation so the drift persists and executable H6 proves fresh re-grounding from index 1 to index 7.
- Replay no longer repeats the final Observation or fabricates successful ActionResult values after script exhaustion.
- Replay validates the exact Runtime-dispatched external action against the recorded expectation.
- `ScriptedEnvironment` remains: it models screen transitions and is not a duplicate of stateful semantic toggle simulation.
- External raw asset migration, production trace capture, unimplemented fault variants, and a generic Scenario runner remain deferred with the receipts above.

## 12. Graduation decision

G1–G14 are satisfied for the bounded graduated Harness:

- S1 and S2 execute through the real graduated Runtime.
- versioned/provenanced persistent contracts are validated;
- Frame/Image, Trace, and Scenario boundaries are explicit;
- at least one honest reality-derived (REALITY_SEEDED) asset participates in permanent replay regression;
- no seeded evidence is mislabeled;
- all unavailable physical/perception/trace/live work is explicitly deferred;
- no production Core or unauthorized capability is changed.

Final validation evidence and the dedicated Harness baseline commit are recorded in the task result that accepted this graduation record.

Validated closeout counts:

- Build: PASS, 0 warnings, 0 errors.
- Targeted Harness: 30/30 PASS (16 stateful simulation/conformance + 14 Observation/REALITY_SEEDED replay and schema proofs).
- Component/Unit: 158/158 PASS.
- Runtime Scenario regression: 509/509 PASS.
- Architecture Guards: 9/9 PASS.
- Full Runtime regression: 706/706 PASS.
- Consistency C1–C10: PASS.
- OpenSpec strict: 14/14 changes PASS.
- `git diff --check`: PASS.

`NO_AUTOMATIC_NEXT_CAPABILITY`
