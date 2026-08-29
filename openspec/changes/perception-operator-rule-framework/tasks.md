> **Batch ruling (Human, 2026-08-27)**: authorized batch after spec clarification =
> **S1 → S2 → S4** with per-stage HARD gates. S1 must be behavior-identical or STOP.
> S2 must pass the v1n counterexample and a cross-UI regression set; shortfall STOPS
> (no automatic S3). S4 provides veto/confidence-downgrade only, never fabricates
> candidates. **S5 DEFERRED** (separate post-S2 decision; does not gate Phase 2.6
> re-entry). **S3 remains a SEPARATE Human Gate.** Phase 2.6 re-enters only when S2 or
> an authorized S3 delivers exactly one navigation candidate per visual row on the
> regression frames.

## S1. Framework Core + Zero-Diff Port — HARD GATE: zero behavior difference or STOP

- [x] S1.1 Operator registry & contract types (id/version/authority class/typed IO/provenance/bounded param schema with validator-safe direction/determinism/fail-closed/trace). *(41/41 incl. permutation property tests; leader re-verified.)*
- [x] S1.2 Selector model: five dimensions + tags, canonical values, `default` semantics, context header supplied by the Adapter layer with the analysis request. *(Framework-side complete in S1A; server-side optional context header with default fallback lands with S1B wiring — C# Adapter-side sending is deferred until a non-root rule set exists, keeping this batch perception-internal.)*
- [x] S1.3 Specificity cascade resolver (subset matching, pin-count specificity, per-value provenance) + intersection-scoped conflict detection (mutually exclusive selectors are not conflicts; uncovered reachable intersections are load errors; conservative rejection allowed) + property tests. *(All four spec scenarios pinned in tests.)*
- [x] S1.4 Deterministic rule-set serialization (schemaVersion, stable ordering) + loader/linter (unknown ops/params, bounds, unsafe validator adjustments, dead rules, complexity budget, conflict detector). *(11 diagnostic kinds.)*
- [x] S1.5 Governance binding: rule-set hash into `configId → deploymentId → receipt`; unpromoted sets never enter runtime; extend governance test family. *(Leader-verified: 19 new+gate green, 165+1/48+1 pre-existing parity, zero CURRENT-ACTIVE/artifact edits — see `S1C-governance-binding.md`.)*
- [x] S1.6 Port `row_grouping.py` as `uniform-list-row-grouping` (GENERATOR) with root defaults = current candidate values; add `spacing-verifier` (VALIDATOR) around it; registry DAG wires verifier as mandatory. *(Leader-verified: 84+3 green, structure review, purity — see `S1B-port-wiring.md`.)*
- [x] S1.7 Frame-level equivalence regression: archived real-frame set (incl. v1n false-positive frames) — S1 output byte-identical to retained candidate; this asset becomes the mandatory regression gate for all later rule/operator changes. **Any difference ⇒ STOP.** *(The 28-case corpus and resident byte gate are frozen; the S1 zero-diff hard gate PASSES.)*
- [x] S1.8 Trace output + offline replay harness (frame + rule-set hash → identical result & trace). *(`operators/trace.py` + wiring tests incl. trace determinism.)*

## S2. Deterministic Relation Head — HARD GATE: v1n counterexample + cross-UI regression or STOP

- [x] S2.1 `row-relation-head` GENERATOR with FROZEN inputs: raw visual regions (uncombined detector boxes + OCR text blocks) and pairwise geometric relation candidates (same-column, vertical adjacency/containment, overlap). MUST NOT consume established row groups (no circularity); text/XML/VLM MUST NOT fabricate row identity. *(16 unit tests; register-only deviation sanctioned.)*
- [x] S2.2 Low-anchor viewport composition path (<4 anchors) with mandatory spacing-verifier validation; no relaxation of four-anchor behavior. *(Routed adapter + verifier envelope; ≥4 frames byte-identical.)*
- [x] S2.3 Acceptance: v1n regression frames no longer misjudge AND remain fail-closed where evidence is insufficient; four-anchor frames unchanged; equivalence suite green; cross-UI regression set (beyond Settings) passes. **Shortfall ⇒ STOP at the fail-closed boundary; S3 is NOT auto-entered.** *(ALL 7 hard gates PASS — `S2-acceptance.md`; both low-anchor deltas leader-sanctioned; baseline regen leader-executed; cross-UI 13 tests + 22 subtests green. S3 NOT entered.)*

## S3. Model-Backed Relation Head — SEPARATE HUMAN GATE (not authorized here)

- [ ] S3.1 Only on explicit later authorization and if S2 acceptance shows insufficient coverage: new model/deployment contract, latency budget, provenance rules, unavailable-behavior, cross-UI falsifier set.

## S4. Validator Wiring — constraint: veto/confidence-downgrade ONLY

- [x] S4.1 `text-relation-check` VALIDATOR (veto/confidence-downgrade only; never emits candidates). *(Annotate-only byte contract; corpus 34/34 zero-veto.)*
- [x] S4.2 `structured-corroboration` VALIDATOR (XML auxiliary only; never identity source; never fabricates candidates). *(Absent-channel trivial pass in executed pipeline.)*
- [x] S4.3 `vlm-annotation` ADVISOR (offline/low-frequency; behind explicit flag; never in authorization path). *(Offline stub, enabled=False, not in pipeline/RUNNERS — asserted.)*

## S5. Learning Loop — DEFERRED (separate post-S2 decision; does NOT gate Phase 2.6 re-entry)

- [ ] S5.1 Deferred with S2 decision: learned-parameter store, supersession thresholds (minimum sample size, evidence intervals), proposal producer — all are open design inputs to fix before authorization.

## Re-entry

- [ ] R.1 Phase 2.6 Stage A→B→C→J→K re-run (under `runtime-iterative-full-traversal-acceptance`) ONLY after S2 or an authorized S3 delivers one navigation candidate per visual row on the regression frames.

## Design Docs

| Concern | Doc |
|---|---|
| Buyer claims / scope / non-claims | `proposal.md` |
| Decisions D1–D7, authority proof, risks | `design.md` |
| Normative behavior | `specs/perception-operator-rule-framework/spec.md` |
| Human clarification ruling (this batch) | `evidence/HUMAN-CLARIFICATION-1.md` |
| IR-G0 evidence & STOP | `../runtime-iterative-full-traversal-acceptance/evidence/STOP-runtime-or-contract-gap.md` |
| Retained candidate & false-positive evidence | `../perception-navigation-row-composition-repair/evidence/` |
