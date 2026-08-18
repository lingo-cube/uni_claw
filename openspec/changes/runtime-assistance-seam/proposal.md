# Proposal: runtime-assistance-seam

## Buyer

The Runtime ↔ External Intelligence Harness architecture needs **L1 CONSULT**
(Plane 3 — Assistance): the `Runtime.Agent` must be able to *request external
information* when its semantic adjudication cannot decide, while **keeping final
decision authority** (I-3). This is the first Runtime-side seam of the External
Contract (baseline: `runtime-external-contract-baseline`).

This is the gate recommended by `docs/decisions/runtime-dsh-architecture-gap-analysis.md`
(NextGate after RUNTIME_EXTERNAL_CONTRACT_GATE) and the contract baseline
(Plane 3 owner gate).

## Gap

- **No Runtime-initiated external request exists** (verified: no
  `AssistanceRequest`/`ConsultAsync`/provider abstraction in `src/UniClaw.Runtime`).
- The Agent's adjudication surface today is **fail-closed only**: at
  `container.LocalPageBeliefState == SemanticBeliefState.Contradicted` the Agent
  immediately fails with `SemanticContradiction`
  (`Agent.SemanticRun.cs:124`); `Unresolved` has no explicit adjudication point
  (it falls through to binding/state fail-closed paths).
- The mother-doc seam
  (`docs/decisions/outer-intelligence-integration-architecture.md` §3 —
  `IIntelligenceProvider.ConsultAsync(AdjudicationContext) → IntelligenceAdvice`,
  advice-mode, adjudication-point only, construction injection) is **design-only**:
  no code exists.
- The External Contract baseline pre-defined the Assistance plane boundary
  (capability-gap expression, not an LLM call; advice, Kernel decides;
  correlation + world-version binding) but implemented nothing.

**Earliest missing system link: `RUNTIME_SIDE_ASSISTANCE_PROVIDER_SEAM`.**

## What this change does (BASELINE gate)

**Design + spec only (no code in this gate; APPLY later).**

1. Defines the Runtime-side Assistance seam: an **abstract, zero-dependency
   provider interface** in `UniClaw.Runtime` (construction-injected, optional),
   aligned with the mother-doc `IIntelligenceProvider` shape and the contract's
   Assistance terminology.
2. Fixes the **adjudication call points**: ONLY the belief adjudication surface
   (`LocalPageBeliefState ∈ {Unresolved, Contradicted}`) — per buyer decision,
   NOT BindingUnresolved / StateEvidenceRequired / BudgetExhausted (those remain
   fail-closed, deferred to later levels).
3. Defines **advice-mode consumption**: the advice is candidate information; the
   Agent decides; advice never writes belief/binding/state (I-2/I-3); advice is
   never truth/authorization/goal-completion.
4. Defines the **world-version binding**: the context carries the observation
   sequence anchor (`WorldBelief.SourceObservationSequence` /
   `Observation.SequenceNumber`); advice bound to an advanced world is stale and
   discarded; the Agent re-observes fresh evidence before applying anything
   (contract primitive).
5. Defines **correlation** (request identity on the context; response echoes it)
   as a reserved field — synchronous L1 consult; async correlation is deferred.
6. Defines **Guard 2 compliance**: the interface lives in `UniClaw.Runtime`
   (`Capabilities/Brain/` — the charter's reserved capability-purchase domain),
   references only BCL + `UniClaw.Runtime.Model`; the DSH-side provider
   implementation is NOT part of this change (deferred to
   `dsh-intelligence-provider-integration`).
7. Defines **backward compatibility**: `null` provider = today's fail-closed
   behavior, zero regression.

## Non-goals (explicitly out of scope)

- Implementing the seam code (APPLY gate, after this baseline validates).
- Implementing the DSH-side provider / wire transport (`perception.ask`,
  `intelligence.consult`, escalation protocol) — deferred to
  `dsh-intelligence-provider-integration`.
- Adjudication points beyond belief state (BindingUnresolved /
  StateEvidenceRequired / BudgetExhausted) — L2+ scope.
- Guidance plane (Plane 4) / Execution Handoff (Plane 5) — separate far-term gates.
- Async request/response correlation channel — deferred (reserved field only).
- TaskSpec / AgentProfile / intelligence settings — not assumed to exist.
- New RuntimeEvent kinds / emitters — none.
- Changing current fail-closed semantics (consult failure still fails closed).

## Required output

`PROJECT_LEADER_RUNTIME_ASSISTANCE_SEAM_BASELINE_RESULT` with Decision
`BUYER_CONFIRMED` (verified gap) or `BUYER_ALREADY_SATISFIED`, the OpenSpec change
(proposal/design/spec/tasks) created and validated, and `NEXT_GATE =
PROJECT_LEADER_APPLY_RUNTIME_ASSISTANCE_SEAM` (after buyer confirmation).

## Authority (unchanged from the External Contract baseline)

- `AgentKeepsSemanticDecisionAuthority = MUST_HOLD` (I-3): advice is candidate
  information only.
- `AdviceDoesNotWriteRuntimeState = MUST_HOLD`: no belief/binding/state mutation
  by advice.
- `AdviceIsNotTruthOrAuthorizationOrCompletion = MUST_HOLD`.
- `AssistanceIsCapabilityGapExpression = MUST_HOLD`: the request expresses a
  missing capability; the interface is not an LLM/VLM reference (Guard 2).
- `DirectDSHPhysicalAuthority / DirectDSHGoalEvidenceAuthority / DirectDSHBindingAuthority / DirectDSHStateBeliefAuthority = MUST_BE_NO` (unchanged).
- `NullProviderZeroRegression = MUST_HOLD`.

## Falsifiers

| # | Falsifier | Fails if |
|---|---|---|
| F1 | external types into Runtime | the seam interface depends on DSH/Cordis/model/harness types |
| F2 | advice writes state | advice mutates belief/binding/Container/state (bypasses I-2/I-3) |
| F3 | scope creep on call points | the seam is invoked at BindingUnresolved/StateEvidenceRequired/BudgetExhausted (outside the adjudication surface) |
| F4 | advice-as-authority | advice is treated as truth, authorization, or goal completion |
| F5 | stale advice applied | advice bound to an advanced world version is applied instead of discarded |
| F6 | null-provider regression | a null provider changes today's fail-closed behavior |
| F7 | DSH-side implementation | this change implements the DSH provider/wire (deferred) |
| F8 | new emitters | new RuntimeEvent kinds or emitters are introduced |
| F9 | fail-closed weakening | consult failure converts to success/progress instead of failing closed |
| F10 | fabricated claims | any repository-reality statement contradicts verified source |

## Validation

- `openspec validate runtime-assistance-seam --strict --no-interactive`
- `scripts/check-consistency.sh`
- Cross-check against `runtime-external-contract-baseline` (Plane 3) and
  `docs/decisions/outer-intelligence-integration-architecture.md` §3.
