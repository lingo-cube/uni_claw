# Design: runtime-assistance-seam

> BASELINE design (no code). Source-verified repository baseline: 2026-08-17.
> Contract frame: `runtime-external-contract-baseline` (Plane 3 — Assistance).
> Mother-doc alignment: `docs/decisions/outer-intelligence-integration-architecture.md` §3.

---

## 1. Verified source baseline

| Fact | Source |
|---|---|
| Belief state is three-valued: `Supported / Unresolved / Contradicted`, fused by pure `SemanticReconciliation.FuseBelief` (≥1 Support ∧ ≥1 Contradict → Contradicted; ≥1 Support → Supported; else Unresolved) | `src/UniClaw.Runtime/Model/SemanticBeliefState.cs`, `Model/SemanticReconciliation.cs` |
| Agent adjudication is fail-closed ONLY: `LocalPageBeliefState == Contradicted` → immediate `SemanticContradiction` ("refusing to act on local binding"); `Unresolved` has no explicit adjudication point (falls through to binding/state fail-closed paths) | `src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs:124-126` |
| World-version anchor exists: `WorldBelief.SourceObservationSequence` (long?) + `Observation.SequenceNumber` (monotonic) | `src/UniClaw.Runtime/Model/WorldBelief.cs`; `Environment/IEnvironment.ObserveAsync` |
| Agent is fully construction-injected (Startup/Traversal/observeInitial/resolveSemanticPage/containerFactory/Recovery/criteria); optional criteria params already set the nullable-param precedent | `src/UniClaw.Runtime/Agent/Agent.cs` |
| Mother-doc seam (design-only): `IIntelligenceProvider.ConsultAsync(AdjudicationContext) → IntelligenceAdvice`; advice-mode; adjudication-point only; construction injection; "Kernel only depends on the abstract interface, zero LLM/VLM reference (Guard 2)" | `docs/decisions/outer-intelligence-integration-architecture.md` §3 |
| Charter reserves the capability-purchase domain: `Capabilities/Brain/` — "reasoning/interpretation capability domain — currently no concrete types, awaiting future capability purchase; Agent remains the only semantic authority" | `docs/system/greenfield-runtime-charter.md` §55; `src/UniClaw.Runtime/AGENTS.md` |
| Guard 2: `UniClaw.Runtime` carries zero LLM/VLM/DSH references (mechanical: `PluginIntegrationGuardTests.GuardA/B`, `ArchitectureGuardTests` Guard 2/10b) | `tests/UniClaw.Runtime.Tests/Architecture/` |
| External Contract Plane 3 (Assistance): Runtime-initiated; capability-gap expression (not an LLM call); external output is advice, Kernel decides (I-3); response carries correlation + bound world version; wire format NOT frozen | `openspec/changes/runtime-external-contract-baseline/design.md` §3.3 |

---

## 2. Seam shape (aligned with mother-doc, mapped to contract terms)

```csharp
// UniClaw.Runtime/Capabilities/Brain/ — zero external dependency (BCL + Model only).
namespace UniClaw.Runtime.Capabilities.Brain;

/// <summary>
/// L1 CONSULT seam (External Contract Plane 3 — Assistance): the Runtime requests
/// external INFORMATION when semantic adjudication cannot decide. This is a
/// capability-gap expression, NOT an LLM invocation. The Agent keeps final
/// decision authority (I-3); the advice is candidate information only.
/// Null-safe optional injection: absent provider ⇒ today's fail-closed behavior.
/// </summary>
public interface IAssistanceProvider
{
    Task<AssistanceAdvice?> ConsultAsync(AssistanceContext context, CancellationToken cancellationToken);
}
```

**Naming decision**: the seam uses the contract's Assistance vocabulary
(`IAssistanceProvider` / `AssistanceContext` / `AssistanceAdvice`). The mother-doc
name `IIntelligenceProvider`/`IntelligenceAdvice` describes the same seam from the
DSH-side integration perspective; the future
`dsh-intelligence-provider-integration` gate implements the DSH adapter of THIS
interface. (Terminology mapping recorded in §9.)

**Placement**: `Capabilities/Brain/` — the charter's reserved capability-purchase
domain. The interface is the first purchased capability. It holds NO decision
authority (charter: Agent remains the only semantic authority).

### 2.1 Context (what the Runtime truthfully knows at the adjudication point)

```csharp
public sealed record AssistanceContext(
    string RequestId,               // correlation identity (reserved; echoed by advice)
    string RunId,                   // run identity (Agent run)
    string SemanticPage,            // current container semantic page (when known)
    SemanticBeliefState BeliefState,// Unresolved | Contradicted (the adjudication trigger)
    long WorldVersion,              // Observation.SequenceNumber / belief.SourceObservationSequence
    Observation Observation);       // the fresh observation evidence (immutable)
```

No invented fields: every field is already on the Agent/Container public surface or
the observation model.

### 2.2 Advice (candidate information — never authority)

```csharp
public sealed record AssistanceAdvice(
    string RequestId,               // echoes the context correlation
    long WorldVersion,              // MUST equal the context world version (staleness check)
    string? Recommendation,         // optional: e.g. "re-observe", "rebind", "dismiss surface"
    string? AdditionalEvidence,     // optional: supplementary recognition knowledge
    string Reason);                 // human-auditable rationale
```

**Consumption contract**: the Agent MAY use the advice to decide between the
existing deterministic actions (re-observe, rebind via existing container
semantics, dismiss obstruction, or fail closed). The advice itself NEVER writes
belief/binding/state. Applying advice is still an Agent decision (I-3).

---

## 3. Adjudication call points (buyer-confirmed: belief adjudication ONLY)

| Trigger | Today (verified) | With seam (design) |
|---|---|---|
| `LocalPageBeliefState == Contradicted` | fail closed: `SemanticContradiction` ("refusing to act") | consult FIRST (advice-mode); if advice yields no deterministic resolution → fail closed exactly as today; if advice recommends a bounded deterministic action (e.g. re-observe with fresh evidence / rebind) → Agent performs it and re-evaluates the SAME goal (bounded, evidence-driven) |
| `LocalPageBeliefState == Unresolved` | no explicit adjudication point (falls through) | explicit consult point: request external interpretation of the unresolved evidence; advice → bounded deterministic action or fail closed (current semantics preserved when no provider or no actionable advice) |

Both call points are bounded: a consult must not loop indefinitely (bounded
consult attempts per adjudication, then fail closed — mirroring the existing
bounded re-observe / budget discipline).

NOT in scope (remain fail-closed, L2+): `BindingUnresolved`, `StateEvidenceRequired`,
`BudgetExhausted`, viewport-exploration `unresolved` — the buyer scoped this gate to
the belief adjudication surface only (F3).

---

## 4. World-version binding and staleness (contract primitive)

- `AssistanceContext.WorldVersion` = the observation sequence the adjudication is
  based on (`WorldBelief.SourceObservationSequence`, falling back to the current
  `Observation.SequenceNumber`).
- `AssistanceAdvice.WorldVersion` MUST equal the context world version; otherwise
  the advice is **stale and discarded** (F5).
- After any bounded deterministic action suggested by advice, the Agent
  **re-observes fresh evidence** (sequence advanced) before re-evaluating the same
  goal — the world is authoritative (I-4); stale advice never mutates current
  belief (contract staleness rule).

---

## 5. Correlation

- `AssistanceContext.RequestId` is a correlation identity (deterministic, per
  consult). `AssistanceAdvice.RequestId` must echo it; mismatched responses are
  discarded.
- L1 is **synchronous** `ConsultAsync` (await); the async correlation channel is a
  deferred reservation (Plane 3 extension) — no channel is built in this seam.

---

## 6. Guard 2 / isolation compliance

- The interface + context + advice live in `UniClaw.Runtime` and reference only BCL
  + `UniClaw.Runtime.Model` (`SemanticBeliefState`, `Observation`). Zero
  DSH/Cordis/LLM/VLM/model types (F1; Guard 2; `PluginIntegrationGuardTests.GuardA/B`).
- The DSH-side provider (wire transport, harness adapter) is NOT this change (F7;
  deferred to `dsh-intelligence-provider-integration`).
- Tests use a Fake provider (test-side), never a real model (project convention:
  "fake 在测试侧").

---

## 7. Injection and backward compatibility

- `Agent` constructor gains an optional parameter
  `IAssistanceProvider? assistanceProvider = null` (precedent: existing optional
  criteria params).
- `null` ⇒ today's fail-closed behavior, zero regression (F6).
- Consult failure (provider throws/times out) ⇒ fail closed, never progress (F9);
  the provider is optional and its failure is an Agent-side decision input, not a
  process fault (bounded catch → fail closed).

---

## 8. Behavior-preserving guarantees

- No change to: fail-closed semantics, Trap/Recovery, GoalEvidence, completion,
  drift handling, popup handling, deferred reconciliation, event vocabulary.
- No new RuntimeEvent kinds/emitters (F8).
- The seam adds a consult opportunity; every path still terminates in the existing
  deterministic outcome set (Satisfied / StateEvidenceRequired /
  BindingUnresolved / SemanticContradiction / BudgetExhausted / ExecutionFailed).

---

## 9. Terminology mapping (records, does not decide)

| Contract (Assistance Plane) | This seam | Mother-doc (§3) |
|---|---|---|
| `AssistanceRequest` | `AssistanceContext` | `AdjudicationContext` |
| `AssistanceAdvice` | `AssistanceAdvice` | `IntelligenceAdvice` |
| Assistance provider | `IAssistanceProvider` | `IIntelligenceProvider` |
| consult | `ConsultAsync` | `ConsultAsync` |

The mother-doc's `IIntelligenceProvider` naming may be adopted later by the DSH
integration gate as an adapter alias; the Runtime-side contract vocabulary is
fixed by this seam.

---

## 10. Deferred (explicitly NOT this change)

- Seam implementation code (APPLY gate).
- DSH-side provider + wire (`intelligence.consult` / `perception.ask`,
  escalation protocol) — `dsh-intelligence-provider-integration`.
- Non-adjudication call points (BindingUnresolved / StateEvidenceRequired /
  BudgetExhausted) — L2+.
- Guidance (Plane 4) and Execution Handoff (Plane 5) — far-term gates.
- Async correlation channel — reserved field only.
