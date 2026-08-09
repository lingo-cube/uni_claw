# S0 Runtime Roadmap and Scenario Coverage

> Status: Canonical planning baseline | Date: 2026-08-09
> Scope: Runtime evidence maturity and S0 graduation planning.
> Authority boundary: this document prioritizes evidence and Scenario gates. It does not replace the Architecture Contract, approved OpenSpec SHALL, frozen capability closeouts, or task authorization.

## 1. Development Direction

UniClaw graduates by increasing evidence maturity against realistic autonomous UI work:

```text
S0 deterministic UI world simulation
→ S1 recorded reality replay
→ S2 offline integration with production-shaped evidence
→ S3 live emulator
```

The roadmap does not aim to finish every possible Runtime abstraction or maximize Harness automation. Harness H4-1/H4-1.1/H4-2/H4-3 remains the frozen control-loop baseline. Runtime and evidence work must produce repeated blocking evidence before another Harness purchase is considered.

Repository truth at this rebaseline supersedes older planning snapshots: SC-P3-003 is `SC_P3_003_FROZEN_CAPABILITY`; SC-P3-CAND-004 is `SC_P3_CAND_004_FROZEN_CAPABILITY`; SC-P3-CAND-005 is `SC_P3_CAND_005_FROZEN_CAPABILITY`; SC-P3-CAND-006 is `SC_P3_CAND_006_FROZEN_CAPABILITY`; SC-P3-CAND-007 is `SC_P3_CAND_007_FROZEN_CAPABILITY` with tasks 4/4 and independent validation PASS.

## 2. Status Dimensions

Semantic status and evidence maturity are independent.

### Semantic Status

| Status | Meaning |
|---|---|
| `CANDIDATE` | Reality pressure is registered, but no Semantic Gate has approved a purchase. |
| `APPROVED` | Scenario meaning and required behavior are approved; implementation may still be absent. |
| `ACTIVE` | An approved Scenario is moving through OpenSpec/tasks/validation. |
| `FROZEN` | The Scenario capability has an accepted closeout and frozen boundary. |
| `RESEARCH` | Evidence is not yet sufficient to propose one bounded Scenario. |
| `REJECTED` | Evidence was invalid, duplicate, or outside the Runtime direction. |

### Evidence Maturity

| Level | Required evidence |
|---|---|
| `S0` | Synthetic deterministic external-world evidence, including positive, negative/disturbance, and replay proof. |
| `S1` | Recorded legacy, emulator, or real-world evidence replaying the same reality pressure. |
| `S2` | Offline integration using production-shaped Observation parsing, grounding, and semantic evidence while device I/O remains controlled. |
| `S3` | The same high-value capability executed against a live emulator. |

A Scenario may be `FROZEN` semantically while its highest proven evidence remains `S0`.

## 3. S0 Capstone Capability Map

| Capability / reality pressure | Status | Repository evidence | Capstone implication |
|---|---|---|---|
| Uncertain post-action outcome | `COVERED` | SC-P3-001 closeout: TimedOut → fresh Observation → evidence-based verdict; no blind redispatch. | Reuse when a Capstone action outcome is uncertain. |
| Local Popup obstruction and Container continuity | `COVERED` | SC-P3-002 closeout: bounded local handling, fresh continuity evidence, progress preservation or Container-scope escalation. | Inject exactly one local obstruction. |
| Viewport movement with Container identity continuity | `COVERED` | SC-P3-003 closeout: one bounded targetless movement, fresh evidence, same-Container progress preservation. | Viewport movement is no longer an active prerequisite. |
| Repeated same-Container viewport exploration and honest exhaustion | `COVERED` at bounded S0 | SC-P3-CAND-007 closeout: Container-retained accepted evidence, Agent-owned true/false/null decision, exactly one movement per continuation, positive exhaustion, unresolved/bound honesty, GoalEvidence-only completion, replay, tasks 4/4, independent validation PASS. | Attach the frozen bounded capability; no Viewport identity, manager, graph/stack, generic scrolling policy, or dynamic planner is implied. |
| Multi-page and sibling-branch traversal progress | `COVERED` | SC-P3-CAND-004 frozen closeout: one Agent-owned immutable `BranchProgressEvidence`, honest A/B completion, negative branches, replay, tasks 4/4, independent validation PASS. | Attach the frozen S0 capability; no graph, stack, Back action, manager, or refactor is implied. |
| Dangerous-action avoidance | `COVERED` at bounded S0 | SC-P3-CAND-006 frozen closeout: Agent-owned one-Observation authorization criterion, explicit rejected/unresolved zero-dispatch evidence, first-authorized existing Tap, GoalEvidence authority, deterministic replay, tasks 4/4, independent validation PASS. | Attach the frozen bounded capability; no policy engine, universal interception, or generalized discovery/planning is implied. |
| External foreground drift recovery | `COVERED` | SC-P2-001 proves Launcher drift → Agent-scope Trap → anchor restore → Observe → Verify → resume. | Reuse exactly once in the Capstone. |
| Recovery plus higher-level traversal-progress resume | `COVERED` | SC-P3-CAND-005 frozen closeout: exact one-field criterion purchase, Agent-owned true/false/null recovered-world interpretation, bounded no-prefix-replay, GoalEvidence authority, deterministic replay, tasks 4/4, independent validation PASS. | Attach the frozen bounded capability; no validity framework, checkpoint, planner, graph, stack, or refactor is implied. |
| Safe backtracking / parent return | `MECHANICS_WITHIN_SC-P3-CAND-004` | A deterministic S0 world can expose a visible, approved parent-return affordance and existing Tap can execute it. The independent reality distinction is not “Back”; it is retaining honest parent/sibling progress across the return. | Prove child completion → parent return → remaining sibling continuation inside SC-P3-CAND-004. No generic Back, graph, stack, or navigation framework is purchased. |

## 4. Scenario Priority

Priority is a planning input only. It never bypasses Semantic Gate, OpenSpec, task approval, Architecture Gate, or Human Gate.

| Order | Scenario | Priority | Reason |
|---:|---|---|---|
| — | No additional Runtime Candidate is authorized by this closeout | — | SC-P3-CAND-007 is frozen; any new Scenario requires separate evidence and Gate authority. |

Recovery-progress resume, bounded discovered-candidate safety, and repeated viewport exploration are frozen. Safe parent return remains execution mechanics within SC-P3-CAND-004, not a separate Scenario purchase.

### Roadmap / Evidence Work Priority

| Order | Work | Priority | Reason |
|---:|---|---|---|
| 1 | Legacy simulation baseline classification | `HIGH` | Required independently for `S0_BASELINE_READY`; prevents old mechanisms from becoming accidental requirements and supplies S1 promotion inputs. |

## 5. Scenario Coverage Matrix

Legend: `PASS` = proven at that maturity; `SOURCE` = evidence corpus exists but is not a current Runtime proof; `TARGET` = planned graduation target; `—` = not yet evidenced.

| Scenario / reality pressure | Semantic Status | Priority | Priority Reason | S0 | S1 | S2 | S3 | Legacy Evidence | Dependency / prerequisite | Disposition / next action |
|---|---|---|---|---|---|---|---|---|---|---|
| SC-P1-001 — Normal deterministic navigation | `FROZEN` | `MEDIUM` | Base lifecycle and GoalEvidence prerequisite; already covered. | `PASS` | `SOURCE` | — | — | Settings full-traversal simulation corpus | None | `ATTACH` to Capstone baseline |
| SC-P1-005 — Target disambiguation | `FROZEN` | `MEDIUM` | Safe action requires correct target grounding; existing S0 proof reduces duplication. | `PASS` | `SOURCE` | — | — | `TextTargetResolutionTests` and recorded OCR normalization cases | SC-P1-001 | `ATTACH`; promote with production-shaped grounding at S2 |
| SC-P2-001 — External Launcher drift recovery | `FROZEN` | `HIGH` | Required Capstone disturbance and Agent authority proof. | `PASS` | `SOURCE` | — | — | Recovery and emulator failure corpus | SC-P1 lifecycle | `ATTACH` to Capstone |
| SC-P2-003 — Recovery verification failure | `FROZEN` | `MEDIUM` | Prevents action dispatch from being mistaken for verified recovery. | `PASS` | `SOURCE` | — | — | Recovery failure tests/traces | SC-P2-001 | `ATTACH` as negative Capstone regression |
| SC-P3-001 — Uncertain post-action outcome | `FROZEN` | `MEDIUM` | Valuable safety boundary; already covered and not the active blocker. | `PASS` | `SOURCE` | — | — | Legacy timeout/action traces where available | SC-P2-002 retry boundary | `ATTACH` |
| SC-P3-002 — Popup local obstruction | `FROZEN` | `HIGH` | Required Capstone local disturbance. | `PASS` | `SOURCE` | — | — | Popup handler/simulation tests | Container continuity | `ATTACH` |
| SC-P3-003 — Viewport identity continuity | `FROZEN` | `HIGH` | Direct traversal prerequisite, now proven. | `PASS` | `SOURCE` | — | — | Scrollable baseline and scroll-loop corpus | SC-P3-003 closeout | `ATTACH`; do not rerun lifecycle |
| SC-P3-CAND-004 — Multi-page/sibling branch progress | `FROZEN` | `HIGH` | Frozen capability prevents false completion across bounded A/B sibling traversal and preserves Agent-owned evidence. | `PASS` | `SOURCE` | — | — | `MultiBranchNavigationTests`, `SimulationBaselineTests`, `SettingsEnumerateRegression` | SC-P3-CAND-004 closeout | `ATTACH`; do not rerun lifecycle |
| SC-P3-CAND-005 — Evidence-validated progress resume after Agent Recovery | `FROZEN` | `HIGH` | Frozen capability distinguishes historical progress from freshly revalidated, contradicted, and unresolved recovered-world evidence without blind prefix replay. | `PASS` | `SOURCE` | — | — | Current SC-P2 Recovery traces plus legacy resume/backtracking and traversal-context corpus | SC-P3-CAND-005 closeout; tasks 4/4; independent validation PASS | `ATTACH`; do not rerun lifecycle |
| SC-P3-CAND-006 — Bounded safety classification of newly discovered Settings candidates | `FROZEN` | `HIGH` | Frozen capability distinguishes observed candidates from Agent-authorized execution with explicit rejected/unresolved zero-dispatch evidence and GoalEvidence-only completion. | `PASS` | `SOURCE` | — | — | Settings read-only policy and deterministic denial corpus | SC-P3-CAND-006 closeout; tasks 4/4; independent validation PASS | `ATTACH`; do not expand into a safety framework or generalized discovery |
| SC-P3-CAND-007 — Evidence-based repeated viewport exploration and honest exhaustion | `FROZEN` | `HIGH` | Frozen capability prevents both premature Container exhaustion and blind repeated movement while preserving explicit unresolved/bound evidence. | `PASS` | `SOURCE` | — | — | Scroll-loop termination, ROI end detection, enumerate regression, and scrollable baseline corpus | SC-P3-003 plus SC-P3-CAND-007 closeout; tasks 4/4; independent validation PASS | `ATTACH`; do not expand into Viewport identity, graph/stack, manager, or generic scrolling framework |
| SC-S0-CAPSTONE-001 — Four-level Settings traversal with safety and recovery | `CANDIDATE` (`CAPSTONE`) | `HIGH` | Defines `S0_GRADUATED`; integrates rather than directly purchases capabilities. | `TARGET` | — | — | — | Settings simulation, safety-policy, replay, and emulator failure corpus | Independent legacy-baseline classification | Readiness `PREREQUISITES_MAPPED`; no implementation yet |

## 6. S0 External-World Direction

S0 evolves the existing deterministic Fake as an external world:

```text
Initial World
→ Observation
→ Runtime Action
→ deterministic world transition
→ new Observation
```

Simulation owns only:

- the external world and visible elements;
- dispatch outcome and world transition;
- external disturbances;
- deterministic Observation production.

Production Runtime owns interpretation, semantic identity, action choice, progress, Recovery decisions, GoalEvidence, and final RunState. Simulation must not encode conclusions such as “same Container”, “Agent should recover”, or “Popup handling succeeded”.

## 7. Legacy Evidence Strategy

The `feature/agent-runtime` branch is an evidence corpus, not a migration source. Each selected case is classified as:

```text
Source
Intent
Initial World
Disturbance
Observed Failure
Required Behavior
Reality Distinction
Mapped Scenario
Disposition: ATTACH | CANDIDATE | RESEARCH | REJECT
```

Initial high-value mining queue:

| Source | Pressure | Initial disposition |
|---|---|---|
| `tests/UniClaw.Core.Tests/Baseline/MultiBranchNavigationTests.cs` | Skipped sibling branch, false `AllVisited`, deep parent return | `ATTACH` → SC-P3-CAND-004 approved Semantic Gate |
| `tests/UniClaw.Core.Tests/Baseline/SimulationBaselineTests.cs` | Full Settings traversal and target-search completion evidence | `ATTACH` → Capstone/SC-P3-CAND-004 |
| `tests/UniClaw.Core.Tests/Simulation/TraceReplay/SettingsEnumerateRegression.cs` | Four-level Settings shape and depth-bound failure evidence | `ATTACH` → SC-P3-CAND-004; later S1 replay |
| `scenarios/android-settings/policies/settings-read-only.v1.json` and deterministic safety specs | Visible-but-forbidden actions and zero-device-side-effect denial | `ATTACH` → Capstone safety branch; mechanisms remain non-normative |
| `tests/UniClaw.Core.Tests/Simulation/TraceReplay/*` and retained emulator failures | Recorded mismatch, depth, stale evidence, and target-grounding failures | `RESEARCH` → S1 replay queue |

Legacy FSM names, handlers, Frames, graph/stack objects, and old completion enums are never imported as requirements. Only the external reality pressure and observable behavior may be attached.

## 8. Graduation Definitions

### S0_BASELINE_READY

Defined as all of:

1. the high-value legacy simulation corpus is classified;
2. no high-value evidence remains `UNKNOWN`;
3. core Runtime boundaries have deterministic Scenario pressure;
4. S0 simulation remains external-world-only;
5. key positive, negative/disturbance, and replay evidence exists.

Current status: **NOT YET ACHIEVED** — legacy classification is partial. Recovery-progress validity, bounded discovered-candidate safety, and repeated viewport exploration are frozen S0 capabilities, but the remaining baseline-classification requirement is not satisfied.

### S0_GRADUATED

Defined as SC-S0-CAPSTONE-001 `PASS` with deterministic replay and all completion evidence satisfied.

Current status: **NOT YET ACHIEVED** — the Capstone is registered but not authorized for implementation.

## 9. Promotion Direction

- `S1`: replay the same Scenario pressures using classified recorded legacy/emulator/real-world evidence.
- `S2`: replace synthetic representations with production-shaped Observation parsing, target grounding, and semantic evidence while device I/O remains controlled.
- `S3`: run the same high-value capability against a live emulator.

No S1/S2/S3 implementation is authorized by this roadmap.

## 10. Architecture and Harness Policy

- Agent: `STRUCTURAL_PRESSURE`.
- Container: `COHESIVE`.
- Refactor: `DEFERRED_FOR_ADDITIONAL_S0_EVIDENCE` unless an approved Scenario is directly blocked by the current structure.
- Harness baseline: `HARNESS_CONTROL_LOOP_BASELINE_READY`.
- Harness changes: `NONE`; preserve H4-1/H4-1.1/H4-2/H4-3 and use `AUTO_CONTINUE <scenario>` only after the relevant Scenario Gate exists.
- Architecture Guard count: current `ArchitectureGuardTests` contains eight `[Fact]` guards, matching the Phase 2 `8/8` baseline and SC-P3-001/003 closeouts. The SC-P3-002 closeout's `9/9` is stale reporting, not a deleted or missing guard.

## 11. Next Authority Boundary

```text
STOP_AT_SC_P3_CAND_007_FROZEN
```

Reason: SC-P3-CAND-007 is frozen with its exact one-type/four-field budget, ownership/authority delta NONE, tasks 4/4, and independent validation PASS. Legacy simulation baseline classification remains a separate evidence workflow required before `S0_BASELINE_READY`; this closeout does not authorize that work or Capstone execution.
