# Agent Capability Architecture Consolidation Gate

> Date: 2026-08-11
> Role: Project Leader
> Lane: `SEMANTIC_DISCOVERY` / architecture consolidation
> Result: `CONCEPTUAL_MODEL_FREEZE_APPROVED`
> Implementation authority: **NOT GRANTED**
> OpenSpec: this review did not apply or create an OpenSpec change

## 1. Scope and preserved truth

This gate consolidates vocabulary and composition boundaries only. It adds no
semantic capability, creates no production component, does not implement
`StateClassifier`, and does not authorize a Runtime refactor.

The following truth remains frozen:

- Agent remains the sole autonomous semantic decision authority. It owns the
  run-level semantic lifecycle, business-capability selection, semantic-action
  authorization, and goal-satisfaction decision.
- Container remains the sole owner of page-local mutable world belief/state.
- Traversal remains responsible for target grounding, lowering, and local
  execution mechanics.
- Environment remains responsible for physical observation/action dispatch.
- Stateless evidence producers own no mutable Runtime state.
- The dependency spine remains
  `Agent -> Container -> Traversal -> Environment`.
- The external world is authoritative. Observation, evidence, belief,
  reasoning, and model output are not world truth.

Repository support:

- `src/UniClaw.Runtime/AGENTS.md:28-66` defines the current responsibility map
  and invariant reminders.
- `docs/system/greenfield-runtime-charter.md:1422-1468` freezes dependency
  direction and ports-before-adapters for external capabilities.
- `docs/system/greenfield-runtime-charter.md:1546-1567` requires every
  interface to prove a real external or replaceable boundary.
- `docs/system/greenfield-runtime-charter.md:1585-1595` forbids Traversal from
  taking Agent work, Container from replanning, and Vision providers from
  mutating Runtime state.

## 2. Three-level capability model

### 2.1 Decision

`THREE_LEVEL_CAPABILITY_MODEL_FROZEN`

The following conceptual model is accepted as an architecture vocabulary. It
is orthogonal to, and must not replace, the ownership/dependency spine.

| Level | Meaning | May own | Must not own |
|---|---|---|---|
| **L1 Project Capability Facade** | Optional, thin, human-readable Agent-facing grouping of already-approved capability contracts | Composition and delegation only | Semantic authority, mutable world state, provider state, truth, or a second execution loop |
| **L2 Capability Contract** | Provider-neutral statement of **what** can be requested and what semantic result/evidence is returned | Its input/output contract and failure/uncertainty semantics | Provider/model/platform identity, business authorization, duplicate mutable state, or hidden physical dispatch |
| **L3 Provider / Adapter** | Mechanism-specific implementation of an L2 contract | Mechanism-local resources and bounded implementation state | Runtime belief, Agent authority, target choice, goal completion, or world-truth declaration |

The levels are not mandatory classes or directories. A pure internal operation
may remain a static function. An interface or facade is introduced only when a
real external boundary, replacement need, or composition need is demonstrated.

### 2.2 Mandatory terminology guard

The repository already has `Model.Capability`, an immutable **business semantic
capability descriptor** selected by Agent. It is not a provider port or service.

Therefore architecture discussions and future code must distinguish:

- **Business capability**: the existing `Model.Capability` domain contract,
  such as an object category plus state dimension that Agent selects.
- **Capability contract/port**: an L2 callable operation such as reading switch
  state or producing page evidence.

The unqualified term `Capability` in domain-model code continues to mean the
existing business capability. A future L2 interface must use a responsibility
name (`ISwitchStateReader`, `IPageEvidenceProducer`, and similar), not a generic
`ICapability` or `CapabilityProvider` name.

### 2.3 Composition rules

1. Agent may see an approved L1 facade or an individual L2 contract, never an
   L3 provider type or provider configuration.
2. Facades delegate; they do not reinterpret evidence, authorize actions, or
   retain mutable Runtime belief.
3. L2 contracts return evidence, hypotheses, candidates, or structured
   mechanism results. Their outputs remain subject to the existing owner.
4. Provider selection and configuration belong at a composition boundary, not
   inside Agent business decisions.
5. Provider replacement must not change L2 semantics, ownership, authority,
   dependency direction, safety rules, or truth semantics.
6. A provider chain must preserve provenance and uncertainty. Fallback does not
   turn model output into truth.
7. No L1 facade may become a service locator or a second Runtime orchestration
   engine.

## 3. Current capability inventory

| Candidate capability | Current disposition | Architecture interpretation |
|---|---|---|
| `PageAnalysis` | Existing | Stateless page-evidence producer. L2-shaped operation, but no interface/provider extraction is justified now. Prefer `PageEvidenceProducer` over `PageRecognizer`: it emits evidence, not a page verdict. |
| `BindingAnalysis` | Existing | Stateless object-binding evidence producer (the implementation follow-up consolidated the earlier `ElementAnalysis` name). Same rule: evidence, not identity truth. |
| `IntentCompiler` | Existing | Stateless bounded intent-to-`SemanticGoalInput` compiler. It does not observe, route, choose UI, or authorize action. It may remain in Planning without a facade. |
| Target grounding / lowering | Existing inside Traversal | A stable responsibility, not a new provider boundary. It remains inside Traversal and must not be lifted into an Agent-facing facade. |
| Goal evaluation | Existing authority in Agent plus evidence callbacks | Evidence evaluation may be pure, but the goal-satisfaction decision remains Agent authority. No independent `GoalEvaluator` authority is frozen. |
| `SwitchStateReader` | Future concept only | A plausible provider-neutral perception contract. It is not purchased or implemented by this gate. |
| `StateClassifier` | Deferred | Explicitly not implemented. Its mechanism and contract remain subject to a separate capability/OpenSpec gate. |

Current provider truth:

- Production Runtime has no provider implementation or production composition
  root; `IEnvironment` is the sole production external port.
- Test code manually composes concrete Startup, Traversal, Recovery, Container,
  and Agent objects and uses `ScriptedEnvironment` as a deterministic test
  adapter.
- Provider names found on `feature/agent-runtime` are historical naming input
  only. They do not establish current architecture.

## 4. Agent-facing facade decisions

| Facade candidate | Classification | Decision |
|---|---|---|
| **Vision** | `CONCEPT_ONLY` | The responsibility cluster is real, but no stable Agent-facing contract or production provider composition exists yet. |
| **Brain** | `CONCEPT_ONLY` | Optional external reasoning is architecturally valid, but no purchased reasoning contract/provider seam exists now. |
| **Operator** | `NOT_JUSTIFIED` | An Agent-facing action facade would obscure or bypass Traversal grounding/lowering and Environment dispatch. Existing spine boundaries are already the stable action surface. |

### 4.1 Vision — `CONCEPT_ONLY`

The concept is justified because the charter already treats Vision/OCR/device
observation as replaceable external capability and current Runtime has multiple
stateless evidence-producing operations. It is not ready to freeze as a facade
because:

- current observation dispatch is already represented by `IEnvironment`;
- current page/object analysis functions have no provider variants;
- there is no production composition root;
- an Agent-facing `Vision` API could bypass Container belief ownership or
  duplicate the observation path if frozen prematurely.

A future Vision facade may be proposed only when one scenario requires Agent to
request richer perception through a stable contract. It must return
`Observation`, `SemanticEvidence`, or an explicit insufficient result; it must
not return authoritative page/object/world truth or mutate Container.

Possible internal responsibility names remain non-binding:

- `SwitchStateReader`;
- `PageEvidenceProducer`;
- `TextReader`;
- `ElementEvidenceProducer`.

OCR, UI hierarchy, local vision, embedding retrieval, and VLM are L3 mechanism
choices, not stable API vocabulary.

### 4.2 Brain — `CONCEPT_ONLY`

Brain is not Agent. The target architecture permits Agent to request slow
intelligence when deterministic knowledge is insufficient, and I-14 keeps AI
pluggable. That supports the concept, not a frozen facade.

A future Brain contract may return interpretations, hypotheses,
recommendations, candidate plans, rankings, or semantic evidence. All outputs
remain proposals/evidence. Agent still:

- decides whether reasoning is needed;
- selects the business capability;
- authorizes semantic action;
- adjudicates contradictory evidence;
- decides goal satisfaction.

Brain must not receive or retain mutable Container state, issue physical
actions, silently select providers from business semantics, or present model
output as world truth. Vendor/model names belong only to L3 adapters.

`IntentCompiler` is not automatically part of Brain: it is currently a bounded
deterministic Planning capability. `GoalEvaluator` is not delegated to Brain:
only evidence/hypothesis production may be delegated; final evaluation remains
Agent authority.

### 4.3 Operator — `NOT_JUSTIFIED`

The current action path already has two cohesive stable boundaries:

```text
Agent authorizes semantic action
  -> Traversal grounds, lowers, executes, and verifies locally
  -> Environment performs physical dispatch
  -> fresh observation proves or disproves world effect
```

An Agent-facing Operator would either duplicate Traversal, hide the required
execution/verification protocol, or expose Environment directly. All three
reduce clarity and weaken the frozen spine.

Android/emulator/device-specific implementations may later be Environment L3
adapters. That does not justify an L1 `Operator` facade.

## 5. Ownership, authority, and dependency verification

| Question | Result |
|---|---|
| Mutable state ownership changed? | **NO** |
| Semantic decision authority changed? | **NO** |
| Dependency direction changed? | **NO** |
| Safety/action authorization changed? | **NO** |
| External-world authority changed? | **NO** |
| New semantic capability admitted? | **NO** |
| New facade/API frozen? | **NO** |

The three-level model is therefore architecture-compatible as vocabulary. Any
future facade or provider implementation must pass a fresh Architecture Fit
check against its concrete contract and wiring.

## 6. Facade admission gate

An L1 facade may move from `CONCEPT_ONLY` to `FREEZE_FACADE` only when repository
evidence demonstrates all of the following:

1. At least one approved scenario requires the Agent-facing use case.
2. Two or more cohesive L2 operations need a stable grouped surface, or a
   provider-composition boundary demonstrably benefits from that surface.
3. The L2 contracts and uncertainty/failure results are stable.
4. Ownership, authority, dependency direction, and external-world truth remain
   unchanged.
5. The facade has no mutable Runtime state and no policy beyond bounded
   delegation/composition.
6. Agent cannot observe provider/model/platform types through the facade.
7. Deterministic fake providers can exercise the complete contract.
8. An OpenSpec change purchases the minimum interface, composition, and
   falsifying scenarios.

## 7. Gate result and authorized next state

```text
AGENT_CAPABILITY_ARCHITECTURE_CONSOLIDATION_GATE
  = CONCEPTUAL_MODEL_FREEZE_APPROVED

THREE_LEVEL_CAPABILITY_MODEL
  = FROZEN_WITH_TERMINOLOGY_GUARD

VISION
  = CONCEPT_ONLY

BRAIN
  = CONCEPT_ONLY

OPERATOR
  = NOT_JUSTIFIED

FACADE_IMPLEMENTATION
  = NOT_AUTHORIZED

STATE_CLASSIFIER
  = DEFERRED_NOT_IMPLEMENTED

PRODUCTION_REFACTOR
  = NOT_AUTHORIZED
```

The next valid action is not to create empty facade/provider abstractions. When
a scenario supplies concrete pressure, propose the minimum L2 contract and its
composition through OpenSpec, then re-run the facade admission gate.

## 8. Verification

**Implementation:** Documentation-only architecture consolidation. No
production or test source was modified by this gate.

**Invariant Verification:** `scripts/check-consistency.sh` passed C1-C10,
including all 14 Architecture Contract invariants, zero ProjectReference, zero
legacy namespace references, shared routing, and MCP configuration consistency.

**Test Verification:** `dotnet test src/UniClaw.Runtime.sln --no-restore`
passed 661/661 tests on the pre-existing working tree.
