# U1_WIFI_MINIMUM_USABLE_AGENT_SLICE_RESULT

> Date: 2026-08-10
> Development lane: `CAPABILITY_DELIVERY_FAST`
> Task: `确保 WiFi 已开启`
> Status: `VALIDATED`

## Canonical Inputs

- CP-12 production-shaped validation: `VALIDATED`
- the earlier CP-12 zero-production-delta result: `SUPERSEDED`
- RM-10 and GC-03
- immutable `TargetGroundingEvidence` and `TargetGroundingCriterion`
- Runtime Architecture Invariants I-1 through I-14

## Capability Path Classification

| Stage | Pre-scenario classification | Repository disposition |
|---|---|---|
| 1. Task input boundary | `EXISTING_AND_PROVEN` | Existing structured `Agent.RunAsync(Goal, Plan, runId, cancellationToken)` boundary is used. Natural-language synthesis is not claimed. |
| 2. Intent / Goal representation | `EXISTING_AND_PROVEN` | Immutable injected Goal predicate and Plan hypothesis represent this closed-world task. |
| 3. Startup / foreground verification | `EXISTING_AND_PROVEN` | Existing Startup launch, fresh Observe, foreground verification, semantic resolution, and Ready path are used unchanged. |
| 4. Initial Observation | `EXISTING_AND_PROVEN` | Agent obtains a fresh post-Startup Observation before execution. |
| 5. Semantic page / Container | `EXISTING_AND_PROVEN` | Existing injected semantic resolver and Container factory establish and rebind local context. |
| 6. WiFi work discovery | `EXISTING_BUT_NEEDS_COMPOSITION` | Initial GoalEvidence decides whether work exists; the already-supported structured Plan supplies the bounded local work. U1 now proves the composition. No general discovery or Planner is purchased. |
| 7. Target grounding | `EXISTING_AND_PROVEN` | CP-12 evaluates both Wi-Fi text candidates and uses additional current non-text evidence to support exactly one. |
| 8. Safety authorization | `EXISTING_AND_PROVEN` | Existing Agent-owned immutable receipts independently authorize both safe navigation candidates; Traversal enforces the selected receipt. |
| 9. Action execution | `EXISTING_AND_PROVEN` | Traversal dispatches exactly one required Tap and one required `SetSwitch(true)` in the OFF world. |
| 10. Fresh effect verification | `EXISTING_AND_PROVEN` | CP-12 verifies the first fresh navigation effect; existing Traversal freshness and Goal evaluation verify the switch effect. |
| 11. GoalEvidence | `EXISTING_AND_PROVEN` | Only fresh Observation evidence showing the Wi-Fi switch ON satisfies the Goal. |
| 12. Final completion | `EXISTING_AND_PROVEN` | Agent alone converts satisfied GoalEvidence into `RunState.Completed`. |

`ARCHITECTURE_FIT_CONFIRMED`

## CP-14 Boundary

U1 consumes the current structured Goal + Plan task boundary. It does not parse
the Chinese task text or synthesize a Goal/Plan from natural language.

CP-14 Intent → Goal / Plan remains `FUTURE_CAPABILITY`; no Planner, LLM/VLM,
Intent model, or synthesis semantic is introduced. Repository roadmap evidence
explicitly records CP-14 as not blocking U1 or U2.

## Executable Scenario Evidence

The production-shaped U1 scenario runs through real Runtime components with a
deterministic `ScriptedEnvironment`:

- **Already ON:** initial fresh GoalEvidence is satisfied; the non-empty Plan
  causes zero Tap and zero SetSwitch dispatch.
- **OFF → ON:** both `Wi-Fi` and `Wi-Fi Calling` share the bounded Wi-Fi text
  predicate. Additional current state-bearing evidence supports only `Wi-Fi`;
  independent safety receipts authorize both safe navigation candidates.
  Traversal dispatches one Tap, obtains fresh Wi-Fi settings evidence, then
  dispatches one `SetSwitch(true)`, obtains another fresh Observation, and Agent
  completes only from Wi-Fi ON GoalEvidence.
- **Ambiguous:** two supported candidates remain ambiguous; zero Tap/SetSwitch
  and no fabricated completion.
- **TimedOut + unchanged world:** one Tap, fresh unconfirmed Observation, no
  SetSwitch, no redispatch, and no completion.
- **Wrong Wi-Fi Calling destination:** one Tap, fresh contradictory evidence,
  no SetSwitch, no redispatch, and no completion.
- **Replay:** equal structured task and world inputs produce equal Trace,
  journal, actions, Observations, GoalEvidence, safety order, grounding order,
  and final RunState.

## Exact Delta

```text
Runtime production delta from validated CP-12 baseline: NONE
New model/type/field/enum/interface/component: 0
New mutable state: 0
Ownership delta: NONE
Authority delta: NONE
Dependency-direction delta: NONE
Architecture-invariant delta: NONE
Test delta: 1 production-shaped scenario test file, 6 tests
Documentation delta: this validation receipt
OpenSpec change: NONE — test-only composition of existing frozen capabilities
```

## Validation

```text
dotnet build src/UniClaw.Runtime.sln --no-restore
PASS — 0 warnings, 0 errors

U1 targeted tests
PASS — 6/6

CP-12 targeted regression
PASS — 42/42

Startup / Recovery / Safety / GoalEvidence relevant regression
PASS — 88/88

dotnet test src/UniClaw.Runtime.sln --no-build --no-restore
PASS — 444/444

ArchitectureGuardTests
PASS — 8/8

scripts/check-consistency.sh
PASS — C1-C9

openspec validate --all --strict
PASS — 13/13 changes
```

## Result

```text
U1_WIFI_MINIMUM_USABLE_AGENT_SLICE_RESULT
Status: VALIDATED
```

Remaining gap inside the authorized U1 slice: `NONE`.

Outside this slice, CP-14 natural-language Intent → Goal / Plan synthesis remains
explicitly deferred. Recommended next task:
`PROJECT_LEADER_CP14_INTENT_SEMANTICS_ENTRY_REVIEW`.

STOP.
