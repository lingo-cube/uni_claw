# Legacy Simulation Baseline Classification — Capability Closeout

> Status: Frozen Classification | Date: 2026-08-09
> Scope: the roadmap §7 legacy-simulation-baseline classification evidence capability only — this is not `S0_BASELINE_READY`, not a Phase 3 freeze, not S0 graduation, and not Capstone authorization.
> Authority: independent validation receipt for `docs/system/scenarios/07-legacy-simulation-baseline-classification.md`; it does not replace the approved Scenarios, their Specs, the Architecture Contract, prior frozen capability closeouts, or the remaining roadmap gates.

## Capability

**Legacy Simulation Baseline Classification** — the roadmap §7 nine-field classification of the high-value legacy simulation corpus (uni-claw `feature/agent-runtime` designated corpus; evidence read at `feature/refactor`), with every selected case assigned a disposition and zero `UNKNOWN` remaining.

## Proven Content

```text
47 classified cases: B-01…B-19 (Baseline), T-01…T-23 (TraceReplay/emulator), P-01…P-05 (policies)
Nine-field taxonomy per case: Source | Intent | Initial World | Disturbance | Observed Failure
  | Required Behavior | Reality Distinction | Mapped Scenario | Disposition
Dispositions: ATTACH 38 | RESEARCH 6 | REJECT 3 | CANDIDATE 0 | UNKNOWN 0
Primary mapped-scenario coverage (first-listed per case, 38 ATTACH rows):
  SC-P1-001 (2) | SC-P1-005 (8) | SC-P3-001 (1) | SC-P3-CAND-004 (10)
  | SC-P3-CAND-006 (3) | SC-P3-CAND-007 (12) | SC-S0-CAPSTONE-001 (2)
  + 9 secondary mentions (B-04→P1-001, B-05→CAPSTONE, B-07→CAND-007, B-09→CAND-007,
    T-02→P1-005, T-18→P2-003, P-01/P-03/P-04→CAPSTONE)
Every ATTACH maps to a frozen Scenario or the registered Capstone; zero unapproved mappings.
```

The accepted slice proves:

- The corpus is classified with the roadmap §7 taxonomy; no high-value evidence remains `UNKNOWN` (S0_BASELINE_READY items 1–2).
- Core Runtime boundaries have deterministic Scenario pressure through frozen Scenarios or the registered Capstone (item 3); the S0 simulation remains external-world-only — legacy FSM names, handlers, Frames, graph/stack objects, DynamicMatch/DynamicChildManager templates, deleted jump/adaptive-step pipelines, and old completion enums are evidence only and never imported as requirements (item 4; `Non-Normative Mechanisms` section).
- Key positive, negative/disturbance, and replay evidence exists: positive contract baselines (B-04…B-19, T-23); negative/disturbance (B-01…B-03, T-01…T-07, T-13, T-14, T-16, T-18, T-21, T-22, P-02, P-03); replay-liveness (T-08/T-09/T-11/T-12) with T-17 as the S1 design caution (item 5).
- PASS claims in classified cases are honest (`NONE (PASS; …)` with report/contract evidence); FAIL claims match the documented TDD-red state (MultiBranch 3 cases) or recorded real-run failures (max_steps/settings_home_not_restored, D1 false success, search-box-stuck).
- `RESEARCH` items are queued for S1 replay, not implemented; `REJECT` items (T-10 viewer export, T-19 detached-emulator triage, T-20 duration-budget triage) are genuine tooling/device/harness-boundary cases and pressure no core boundary.
- The classification itself does not claim `S0_BASELINE_READY`: the remaining requirements are the separate Capstone registration/authorization boundary and any gate decisions the roadmap requires.

## Validation History

- **Pass 1** (independent runtime-validator, fresh context, 2026-08-09): `FAIL` — three count-errors confined to the artifact's summary statistics (ATTACH 36 vs table 37; RESEARCH 7 vs 6 enumerated; mapped-scenario counts fitting no convention), plus two cosmetic mislabels (T-14 class name, B-04 transition count) and one coverage gap (L8 unclassified). All case-level content, taxonomy fidelity, disposition honesty, scenario-mapping integrity, and evidence claims were verified accurate; zero fabricated evidence.
- **Correction round** (2026-08-09): summary statistics recomputed from the case table and corrected; T-23 (`SubtitleDegraded_NoDoubleClick_SamePage`, L8) added from source-verified detail; T-14 Source and B-04 transition count corrected; Flash-notifications root cause recorded as an explicit out-of-scope corpus-boundary note (unit coverage lives in `StateMachine/TextTargetResolutionTests.cs`, outside the classified corpus).
- **Pass 2** (independent runtime-validator, fresh context, 2026-08-09): `PASS` — all eight audit items confirmed; programmatic recount: 47 rows, 0 duplicate/missing IDs, dispositions sum to 47, 0 UNKNOWN; primary mapping counts 2/8/1/10/3/12/2 = 38; no violations, no follow-up.

## Ownership and Authority

- Ownership delta: **NONE** — an evidence artifact only; zero production, test, OpenSpec, or harness changes were made by this closeout.
- Authority delta: **NONE** — no Runtime capability, Scenario, Spec, or Architecture Contract is purchased, changed, or superseded.
- The corpus remains an evidence corpus, not a migration source; the classified dispositions bind how legacy evidence may pressure S0/S1 work, and nothing else.

## Frozen Boundary

| Evidence / decision | Frozen meaning |
|---|---|
| 47-case classification, 9-field taxonomy | The corpus classification is frozen as recorded; no case remains `UNKNOWN`. |
| Dispositions ATTACH 38 / RESEARCH 6 / REJECT 3 | ATTACH pressures apply only to the named frozen Scenario or registered Capstone; RESEARCH items are S1-queued, not implemented; REJECT items remain out of scope. |
| Primary mapped-scenario coverage (2/8/1/10/3/12/2 = 38) + 9 secondaries | The only mapping convention used by the artifact (primary = first-listed scenario); all mapped Scenarios are frozen or Capstone-registered. |
| External-world-only rule | Legacy FSM/Frame/mechanism/completion-enum content stays evidence-only and is never imported as a requirement. |
| S0_BASELINE_READY contribution | Items 1–5 contributions are as recorded; the status itself is **not** achieved by this artifact. |
| S1 promotion queue | T-08/T-09/T-11/T-12/T-13/T-14/T-17/P-05 are queued; no S1 implementation is authorized by this closeout. |

## Explicitly Not Purchased

- `S0_BASELINE_READY`, `S0_GRADUATED`, `PHASE_3_FROZEN`, `CAPSTONE READY`, or `PHASE_COMPLETE` status;
- Capstone authorization or execution of SC-S0-CAPSTONE-001 (remains `CANDIDATE` / readiness `PREREQUISITES_MAPPED`);
- Any S1/S2/S3 replay implementation, Runtime production delta, Scenario/Spec/Architecture change, migration of legacy mechanisms, or Harness change;
- Any new Scenario, Gate, or authority created by this classification;
- Reclassification of any case outside a future authorized roadmap workflow.

## Acceptance Receipt

- Classification artifact: `docs/system/scenarios/07-legacy-simulation-baseline-classification.md` — 47 cases, 9-field taxonomy, dispositions and mapping counts internally consistent, 0 `UNKNOWN`.
- Independent validation: Pass 1 FAIL (summary count-errors only, all corrected) → Pass 2 PASS (zero violations).
- Production delta: NONE. Build/test/guard/consistency state: unchanged (evidence document only).
- Semantic drift: NONE — no Scenario, Spec, or Architecture Contract meaning changed.

## State

```text
S0_LEGACY_CLASSIFICATION_FROZEN
```

This state does **not** mean `S0_BASELINE_READY`, `S0_GRADUATED`, `PHASE_3_FROZEN`, `CAPSTONE READY`, or `PHASE_COMPLETE`. Capstone authorization/execution, S1/S2/S3 work, OpenSpec archive, and any new Scenario or roadmap gate remain separate authorities.
