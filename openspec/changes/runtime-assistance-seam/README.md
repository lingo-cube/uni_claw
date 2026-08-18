# Change: runtime-assistance-seam

> **BASELINE** for the Runtime-side Assistance seam (L1 CONSULT, External Contract
> Plane 3). Recommended by `docs/decisions/runtime-dsh-architecture-gap-analysis.md`
> and `runtime-external-contract-baseline` (Plane 3 owner gate).

## What it defines

The first Runtime-side seam of the External Contract: an abstract, zero-dependency
`IAssistanceProvider` in `UniClaw.Runtime` (Capabilities/Brain domain), invoked
ONLY at the belief adjudication surface (`LocalPageBeliefState ∈
{Unresolved, Contradicted}`), advice-mode consumption (Agent keeps final decision
authority, I-3), world-version binding/staleness and correlation (contract
primitives), Guard 2 compliance, and null-provider zero regression.

## Scope guardrails

- **BASELINE only**: no code; implementation is the APPLY gate.
- **Belief adjudication only**: BindingUnresolved / StateEvidenceRequired /
  BudgetExhausted are L2+ (F3).
- **No DSH-side provider**: `dsh-intelligence-provider-integration` gate (F7).
- **Advice is never authority**: no state writes, no truth/authorization/completion
  (F2/F4).
- **No new emitters / no fail-closed weakening** (F8/F9).
- **No external types into Runtime** (F1; Guard 2).

## Documents

- `proposal.md` — buyer/gap/scope/falsifiers/authority
- `design.md` — seam shape, call points, advice consumption, world-version +
  correlation, guard compliance, backward compat, terminology mapping
- `specs/runtime-assistance-seam/spec.md` — requirements + scenarios
- `tasks.md` — baseline slices + APPLY implementation plan

## Next gate (planning context)

After BASELINE validates and the buyer confirms: `PROJECT_LEADER_APPLY_RUNTIME_ASSISTANCE_SEAM`
(implementation), then `dsh-intelligence-provider-integration` (DSH-side provider).
