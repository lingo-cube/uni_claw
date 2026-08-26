# Runtime Exploration Roadmap Phase 2 Consistency Analysis

DocumentType: `CONSISTENCY_ANALYSIS`  
Status: `HUMAN_GATE_REQUIRED` for the roadmap depth examples; Phase 2 graduation remains `GRADUATED / ACTIVE / NOT_ARCHIVED`.  
Authority: analysis only. It does not amend the roadmap, graduation decision, approved Specs, ownership, contract, or lifecycle.  
Base: `e2d8dd44214632f50777992d58fb4fe318ad45f0`

## Analysis Scope

This is a source-linked audit of the roadmap against the higher-authority graduation decision and approved Phase 2 Specs. The roadmap is guidance and cannot override the approved D1 table.

## Capability Consistency Matrix

| Capability | Expected Reality | Observed Reality | Result |
|---|---|---|---|
| Exploration Ledger | Unified immutable evidence projection with discovered/visited/pending/unresolved/frontier | Graduation decision and ledger Spec define and evidence this projection | CONSISTENT / COMPLETED |
| Visited semantics | Rule satisfaction, not click/dispatch; RecordOnly needs fresh observation; expansion needs verified return/boundary | Graduation decision, semantic-admission Spec, and real-path tests state these exact boundaries | CONSISTENT / COMPLETED |
| Coverage/frontier | Revisit and boundary evidence correlate; unknown frontier overlaps record-only visited and does not create a new primary disposition | Graduation decision and ledger tests preserve identity-correct accounting and fail-closed contradictions | CONSISTENT / COMPLETED |
| Fail-closed exploration | Unavailable classification is unresolved; exhaustive depth overflow fails closed; contradictions do not clamp counts | Approved Specs, graduation evidence, and guards/tests cover these cases | CONSISTENT / COMPLETED |
| Depth control | Run-immutable D1 table: depth 0 root record-only; depth 1 root expansion/direct-child record-only; N≥2 exhaustive cutoff or bounded-record frontier | Graduation decision and semantic-admission Spec contain this exact table | ROADMAP EXAMPLE DIVERGENCE |
| Exploration Memory | New Memory/Safety Knowledge owner and model | Graduation decision explicitly marks Phase 3 Exploration Memory not authorized | NOT COMPLETED / NOT AUTHORIZED |
| Safety Knowledge | Persistent safety knowledge and policy model | No such owner, schema, or implementation is authorized | NOT COMPLETED / NOT AUTHORIZED |
| Dynamic Depth | Mid-Run or adaptive depth adjustment | Graduation decision and approved Spec explicitly prohibit dynamic depth | NOT COMPLETED / NOT AUTHORIZED |
| UniAgent Planner | Runtime-side Planner or automatic strategy generation | Roadmap says RuntimeAgent does not generate plans; Phase 3/4 remain outside scope | NOT COMPLETED / NOT AUTHORIZED |

## Depth Example Audit

### Expected Reality

The normative D1 table in `openspec/changes/runtime-exploration-semantic-admission-remediation/specs/runtime-exploration-semantic-admission-remediation/spec.md` requires:

1. depth `0`: root-scope inventory record-only, no child expansion;
2. depth `1`: expand root containers and process direct-child scope inventory record-only;
3. depth `N >= 2` exhaustive: bounded recursive expansion with fail-closed cutoff;
4. depth `N >= 2` match inspection: bounded recursive expansion with boundary record-only and unknown frontier.

### Observed Reality

`docs/decisions/runtime-exploration-roadmap.md` §4 currently states:

- `Depth = 1` — `Root only`;
- `Depth = 2` — `Root + children`;
- `Depth = N` — `Full exploration`.

### Reality Gap

The roadmap examples are not merely a formatting variation: their numeric labels and boundary descriptions disagree with the graduated D1 table. The roadmap also omits the approved depth-0 row and does not express the exhaustive-versus-match-inspection distinction at N≥2. The graduation decision is higher authority and must remain the operative baseline until a human gate decides how to reconcile the roadmap text.

### First Divergence

The first divergence is the roadmap §4 `Depth = 1 / Root only` example versus the approved D1 `depth 1 / expand root containers and process direct-child scope inventory record-only` row. This is recorded as a factual inconsistency, not explained away as equivalence.

### Owner

Human architecture/governance owner must decide whether and how to reconcile the roadmap with the graduated D1 table. No worker may revise either source under this task.

## Gate Decision

`HUMAN_GATE_REQUIRED`: do not begin Phase 3 Preparation, create a Phase 3 OpenSpec, define Memory ownership, revise the roadmap, or reinterpret the graduated depth semantics until the authorized human gate resolves the first divergence.

The minimally disruptive candidate is a documentation-only Roadmap alignment to
the already-approved D1 table. Changing D1 or the graduated implementation would
instead reopen the frozen Phase 2 architecture/Spec and is not authorized by
this analysis. The Human must select the disposition; this analysis does not.

## Evidence Reference

- `docs/decisions/runtime-exploration-phase2-final-graduation-decision.md`, exact graduated claim and lifecycle disposition.
- `docs/decisions/runtime-exploration-roadmap.md`, §§4–6, current depth examples and Phase 3/4 descriptions.
- `openspec/changes/runtime-exploration-semantic-admission-remediation/specs/runtime-exploration-semantic-admission-remediation/spec.md`, D1 closed interpretation table and authority boundaries.
- `openspec/changes/runtime-exploration-ledger-and-depth-control/specs/runtime-exploration-ledger-and-depth-control/spec.md`, ledger, visited, depth, completion, and neutrality requirements.
- `docs/system/constitution/runtime-architecture-contract.md`, I-1–I-14.
