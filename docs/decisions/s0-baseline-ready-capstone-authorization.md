# S0_BASELINE_READY & Capstone Authorization — Human Gate Decision

> Date: 2026-08-09 | Status: APPROVED (HUMAN) | Decision: `ACCEPT_S0_BASELINE_READY_AUTHORIZE_CAPSTONE_OPENSPEC`
> Basis: `docs/decisions/s0-baseline-ready-capstone-authorization-review.md` — independent validation PASS (round 2, zero violations).
> Scope: `S0_BASELINE_READY` declaration and Capstone OpenSpec purchase authorization only. Runtime implementation, Capstone execution, `READY_FOR_S0_RUN`, `S0_GRADUATED`, Phase freeze, and completion are not authorized by this decision.

## Findings Accepted

From the validated review (all 9 audit items confirmed after correction round):

1. 13 frozen S0 capabilities (11 roadmap matrix `FROZEN` rows + SC-P3-CAND-008/009 by closeout); SC-P1-005 frozen via matrix/Phase-1 slice.
2. Legacy simulation baseline classification frozen (2026-08-09; 47 cases, 0 `UNKNOWN`).
3. Capstone registration `CANDIDATE` / readiness `PREREQUISITES_MAPPED`; 12-row prerequisites table with 11 `COVERED` plus the safe-parent-return/backtracking mechanics note.
4. Capstone integration mapping complete — every Required Integration Behavior maps to a frozen capability; no new candidate extraction required at pre-authorization (the prior run's last gap, SC-P3-CAND-009, is frozen).
5. The Capstone's only missing authority surface is its OpenSpec purchase: no `openspec/changes/*` exists for it; "Capstone" appears in the tree only as forbidden/deferred scope.

## Declared

1. **`S0_BASELINE_READY`** — declared 2026-08-09. Roadmap §8 status line synced to ACHIEVED.
2. **Capstone OpenSpec purchase authorized** — create `openspec/changes/phase3-s0-capstone-settings-traversal/` (`.openspec.yaml` + proposal + design + specs + scenario + tasks) as an **integration Scenario with zero production delta** (0 types/fields/enums/interfaces/components/mutable state; ownership/authority delta NONE), carrying: the Fake-world construction contract (S0 World Boundary), the disturbance schedule (exactly one Popup + one external Launcher drift), the completion-evidence-7 contract, the deterministic replay contract, and the stop-extract-gate clause ("if execution exposes a new Reality Distinction → stop → extract one bounded Candidate → Semantic Gate → prove/freeze → return").
3. **R4 doc-rot cleanup executed** with this decision: roadmap §5 matrix gains SC-P3-CAND-008/009 rows and an updated Capstone row; roadmap §11 boundary refreshed; AGENTS.md phase statements synced.

## Next Authority

```text
PROJECT_LEADER_SEMANTIC_GATE_SC_S0_CAPSTONE_001
```

The HUMAN Semantic Gate reviews the generated OpenSpec change. Decision options: `AUTHORIZE_CAPSTONE_INTEGRATION` (approve as-is) | amendments | hold. Runtime implementation and execution require that gate.

## Explicitly Not Authorized

- Runtime implementation or tests, Harness changes, new production purchase;
- `READY_FOR_S0_RUN`, Capstone execution, `S0_GRADUATED`, `PHASE_3_FROZEN`, `PHASE_COMPLETE`;
- reopening or expanding any frozen capability without new evidence and Gate authority;
- Runtime refactor, graph/stack/navigation manager, safety framework, S1/S2/S3 work.

## State

```text
S0_BASELINE_READY_DECLARED_CAPSTONE_OPENSPEC_AUTHORIZED
```

STOP after declaration — the next authority is the Semantic Gate.
