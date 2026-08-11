# Capability Module Architecture Final Gate

> Date: 2026-08-11
> Role: Project Leader
> Lane: `SEMANTIC_DISCOVERY` / architecture acceptance
> Result: `FINAL_GATE_NOT_PASSED`
> Production implementation authority: **NOT GRANTED**
> OpenSpec: this gate did not apply or create an OpenSpec change

## 1. Decision

The capability-oriented architecture remains valid as a conceptual vocabulary,
and the graduated Runtime internal components remain accepted. The current
repository is **not** accepted as a final frozen capability-module architecture,
because the post-gate public `ISwitchStateReader` contract was introduced before
its contract, frame lifetime, production composition, and provider boundary
were purchased and proven.

This is an architecture-governance finding, not a behavioral regression. The
current code is fail-safe and all mechanical validation is green.

```text
CAPABILITY_MODULE_ARCHITECTURE_FINAL_GATE
  = FINAL_GATE_NOT_PASSED

FROZEN_RUNTIME_SPINE
  = PRESERVED

GRADUATED_INTERNAL_COMPONENTS
  = ACCEPTED_UNCHANGED

ISWITCHSTATEREADER
  = UNPURCHASED_L2_CONTRACT_CANDIDATE

VISION
  = CONCEPT_ONLY

BRAIN
  = CONCEPT_ONLY

OPERATOR
  = NOT_JUSTIFIED

STATE_CLASSIFIER
  = DEFERRED_NOT_IMPLEMENTED
```

## 2. Accepted architecture truth

The following boundaries remain correct and frozen:

- Agent remains the sole run-level semantic authority: lifecycle, business-
  capability selection, semantic-action authorization, and goal satisfaction.
- Container remains the sole owner of page-local mutable belief/state.
- Traversal remains the local grounding, lowering, execution, retry,
  re-observation, verification, and journal protocol owner.
- Environment remains the external-world observation/action boundary.
- The dependency direction remains
  `Agent -> Container -> Traversal -> Environment`.
- Evidence and belief remain non-authoritative; the external world remains
  authoritative.

The RC2 extractions remain accepted as stateless internal responsibility
boundaries:

- `BindingReconciler` produces immutable binding proposals;
- `StateBeliefReducer` produces immutable belief proposals;
- `SemanticActionLowerer` produces an execution-action proposal only;
- `TargetGrounder` resolves targets without retry or dispatch authority.

No new interface is required for those internal operations. `IEnvironment`
remains the one proven production replacement boundary in the Runtime.

## 3. Three-level model result

The L1/L2/L3 model stays frozen as optional architecture vocabulary, not a
mandatory class or directory hierarchy:

| Level | Final-gate disposition |
|---|---|
| L1 project facade | None purchased. `Vision` and `Brain` remain concepts; `Operator` remains unjustified. |
| L2 capability contract | Responsibility-named, provider-neutral contracts remain allowed only after concrete scenario, contract, and composition purchase. |
| L3 provider/adapter | No production capability provider or capability composition root exists at current HEAD. Test fakes do not freeze provider architecture. |

The terminology guard also remains mandatory: `Model.Capability` is the
immutable business-semantic descriptor selected by Agent, not a provider port,
provider registry, or generic callable capability abstraction.

## 4. Final-gate falsifier: `ISwitchStateReader`

Current HEAD adds a public production interface:

```csharp
ValueTask<bool?> ReadAsync(
    ElementBounds switchBounds,
    CancellationToken cancellationToken = default);
```

The result shape is directionally sound: it is provider-neutral, preserves
`UNKNOWN` as `null`, exposes no model/vendor/platform name, and gives the
reader no semantic-action, belief, completion, or dispatch authority.

The final gate nevertheless cannot freeze this interface for four reasons.

### 4.1 Authority was not purchased

The architecture consolidation receipt classified `SwitchStateReader` as a
future concept and explicitly granted no implementation authority. Its next-
state rule required the minimum L2 contract and composition to be proposed
through OpenSpec when concrete scenario pressure arrived. No active OpenSpec
change purchases this interface, and the separate `StateClassifier` receipt
still says `GATE_REQUIRED`, `Scope: Implementation contract only. No code`, and
`STOP`.

### 4.2 Frame freshness is implicit and under-specified

The interface documentation says a reader instance is bound to one immutable
fresh perception frame, but the only method input is `ElementBounds`. A real
implementation must therefore obtain the frame through constructor-held,
ambient, or mutable-current-frame state. The contract does not define which
lifetime is valid, how same-frame provenance is guaranteed, or how a consumer
prevents stale-frame classification.

That ambiguity is material because the safety contract requires state evidence
to correspond to the current observation used for grounding and verification.
It must be resolved before the API is called stable.

### 4.3 Current tests do not prove the real capability

Roslyn reference analysis finds one implementation only:
`MockSwitchStateReader` in the test project. The mock returns a configured
constant and receives no perception frame.

The integration proof uses a test-local `IEnvironment` decorator and states
that its observations represent what perception **would** produce. It proves
that existing Agent/Container/Traversal behavior safely consumes
`true`/`false`/`null`, and that the seam can remain below Agent. It does not
prove:

- classification of a current immutable frame;
- same-frame bounds/provenance;
- a production provider or adapter composition;
- replacement between two production-shaped providers;
- the real ON/OFF/UNKNOWN visual evidence contract.

### 4.4 Provider and facade pressure remains insufficient

There is no production provider, production composition root, provider
selection policy, facade, registry, or provider factory. The real image
classifier is explicitly still blocked/deferred. A deterministic fake is
necessary for testing but is not by itself concrete replacement pressure.

Therefore the interface remains a contract candidate; it does not purchase
`Vision`, a generic provider layer, or any other capability port.

### 4.5 The Runtime build-zone map is not reconciled

`src/UniClaw.Runtime/AGENTS.md` maps external capability ports under
`Capabilities/` and does not define a `Perception/` responsibility, owner, or
dependency boundary. The new public directory therefore has no accepted place
in the authoritative build-zone map. This gate does not update the map merely
to normalize an unpurchased design; the location must be decided with the
contract and attachment point.

## 5. Facade and provider decisions

| Candidate | Final classification | Reason |
|---|---|---|
| `Vision` | `CONCEPT_ONLY` | One unproven switch-state seam is not a stable Agent-facing cluster; observation still belongs behind `IEnvironment`. |
| `Brain` | `CONCEPT_ONLY` | No approved reasoning contract, provider, or composition pressure exists. Brain remains optional evidence/hypothesis production, never Agent authority. |
| `Operator` | `NOT_JUSTIFIED` | Traversal plus Environment already provide the cohesive authorized action path. |
| `StateClassifier` | `DEFERRED_NOT_IMPLEMENTED` | No production implementation exists, and this gate grants none. |
| `ISwitchStateReader` | `UNPURCHASED_L2_CONTRACT_CANDIDATE` | Safe result semantics, but contract lifetime, production provider, composition, and authority remain unresolved. |

No `ICapability`, provider registry, service locator, or facade is justified.

## 6. Delta audit

| Question | Result |
|---|---|
| Runtime behavior changed by this gate? | **NO** |
| Mutable-state ownership changed? | **NO** |
| Semantic decision authority changed? | **NO** |
| Dependency direction changed? | **NO** |
| Safety/action authorization changed? | **NO** |
| External-world authority changed? | **NO** |
| New facade frozen? | **NO** |
| New provider architecture frozen? | **NO** |
| Public L2 contract accepted by this gate? | **NO** |

## 7. Required evidence before re-opening the final gate

A bounded follow-up may re-open only the switch-state capability contract. It
must not implement `Vision`, `Brain`, `Operator`, or `StateClassifier` under
this receipt.

The follow-up must provide:

1. an approved scenario/OpenSpec purchase for the minimum L2 contract and its
   attachment point;
2. an explicit current-frame contract and lifetime model that cannot silently
   read a stale or ambient frame;
3. a production-shaped adapter/provider boundary outside Agent, Container, and
   Traversal;
4. real or reviewed golden ON/OFF/UNKNOWN evidence proving the same-frame
   contract rather than constant fake output;
5. a deterministic fake that exercises the exact accepted contract;
6. proof that provider replacement preserves uncertainty, provenance,
   ownership, authority, dependency direction, and no-dispatch safety;
7. a fresh decision on whether the interface belongs in core Runtime or remains
   adapter-local;
8. reconciliation of the accepted location into the Runtime build-zone map.

Until then, no additional provider port or facade may use
`ISwitchStateReader` as precedent.

## 8. Verification

**Implementation:** Documentation-only final gate. No production or test source
was modified.

**Semantic inspection:** C# semantic tooling located one production declaration,
six references, and one implementation; every reference/implementation is in
the test project. The project graph has zero Runtime project references and no
type dependency cycles were reported.

**Targeted tests:** 31/31 passed for switch-state reader,
runtime-internal-componentization, and retry safety.

**Full regression:** 723/723 passed.

**Architecture guards:** 9/9 passed.

**Consistency:** C1-C10 passed.

**OpenSpec strict validation:** 14/14 passed; none purchases the new perception
contract.

**Build:** 0 warnings, 0 errors.

**Working tree before this receipt:** clean.

## 9. Final return

```text
CAPABILITY_MODULE_ARCHITECTURE_FINAL_GATE_RESULT

ThreeLevelCapabilityModel:
  FROZEN_WITH_TERMINOLOGY_GUARD

RuntimeSpine:
  ACCEPTED_UNCHANGED

InternalComponents:
  GRADUATED_ACCEPTED_UNCHANGED

Vision:
  CONCEPT_ONLY

Brain:
  CONCEPT_ONLY

Operator:
  NOT_JUSTIFIED

SwitchStateReader:
  UNPURCHASED_L2_CONTRACT_CANDIDATE

StateClassifier:
  DEFERRED_NOT_IMPLEMENTED

ProviderLayer:
  NOT_FROZEN_NO_PRODUCTION_PROVIDER_OR_COMPOSITION_ROOT

FacadeLayer:
  NOT_FROZEN

BehaviorDelta:
  NONE

OwnershipDelta:
  NONE

AuthorityDelta:
  NONE

DependencyDelta:
  NONE

TargetedTests:
  PASS_31_OF_31

FullRegression:
  PASS_723_OF_723

ArchitectureGuards:
  PASS_9_OF_9_C1_TO_C10_OPENSPEC_14_OF_14

FinalGate:
  NOT_PASSED

Next:
  PURCHASE_AND_PROVE_CURRENT_FRAME_SWITCH_STATE_CONTRACT_BEFORE_REOPENING_GATE
```
