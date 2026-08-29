> **Gate state**: DESIGN/SPEC ONLY — implementation requires a separate explicit Human
> Gate (ruling §10). Phase 2.6 remains STOPPED and must not compensate in-harness.
> Test strategy is capability-based throughout; real Settings text and fixed click
> counts are forbidden as core assertions (ruling §7).

## D. Design / Spec (this stage)

- [x] D.1 Rebuild IR-G1 Evidence → FDP → Owner from STOP-2 + reentry runs (design §1).
- [x] D.2 Freeze the closed three-way window semantics + minimal sufficient confirmation conditions (design §2–§3; spec).
- [x] D.3 Freeze the invariant set and no-scenario-truth rule (design §4; spec).
- [x] D.4 Owner analysis A vs B with the truthfulness criterion; single choice recorded (design §5).
- [x] D.5 Spec counter-example scenarios (10) + non-claims.
- [x] D.6 Test strategy + spec→symbol→test mapping (design mapping table).
- [x] D.7 OpenSpec strict validation PASS → stop and await Implementation Human Gate.

## I. Implementation (NOT AUTHORIZED — separate gate required)

- [ ] I.1 `SourceNormalizationResult` additive window classification (enum + per-window records).
- [ ] I.2 `SourceEquivalenceNormalizer.Normalize`: classification loop per spec (EXTENDING unchanged; confirmation predicates; bounded constant `MaxConsecutiveConfirmationWindows = 2`).
- [ ] I.3 Completeness seam: confirmation-backed resolution accepted; evidence records confirmation backing; zero authority surface.
- [ ] I.4 Capability tests (synthetic deterministic): classification unit suite incl. every counter-example scenario; per-condition negative tests; bound test; authority-invariant test.
- [ ] I.5 STOP-2 deterministic reproduction test (windows: extend×5 → identical terminal pair; old contract red → new green — test encodes the new contract).
- [ ] I.6 Existing normalization + traversal/exhaustion regressions green (no behavior change for extension-only sequences).
- [ ] I.7 Phase 2 / 2.5 full deterministic Runtime regression green; architecture guards; consistency; `git diff --check`.
- [ ] I.8 Independent graduation review (fresh reviewer, artifacts only).

## R. Phase 2.6 resume (after this change graduates)

- [ ] R.1 Fresh reentry campaign from the STOP-2 layer (Stage A restart; never mid-stage), under the standing Gate #2 conditions.

## Design Docs

| Concern | Doc |
|---|---|
| STOP-2 evidence | `../runtime-iterative-full-traversal-acceptance/evidence/STOP-2-viewport-union-exhaustion-edge.md` |
| Reentry runs 1–6 | `../runtime-iterative-full-traversal-acceptance/evidence/G-stage-a/reentry/` |
| Human Gate ruling | this change's README (verbatim summary) |
| Decisions / mapping / deltas | `design.md` |
