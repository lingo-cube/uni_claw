# S1E. Equivalence-Baseline Capture — Acceptance Evidence

## Leader's independent verification

- Test re-run (leader): `test_row_composition_equivalence.py` +
  `test_navigation_row_composition.py` → **28 passed, 3 subtests passed**.
- Purity: worker additions = untracked `tests/corpus/`, `tests/test_row_composition_equivalence.py`,
  `evidence/s1-equivalence-baseline/` only. `uniclaw_perception/` untouched. The
  `governance/pipeline_revision.py` + `fusion/engine|heuristics.py` modifications and the
  untracked `row_grouping.py`/`test_navigation_row_composition.py`/fixtures are the
  PRE-EXISTING retained-candidate worktree (perception repair's uncommitted work) —
  verified by diff attribution.
- Determinism: fresh capture SHA-256 === checked-in baseline (worker-proven; leader
  re-ran compare mode green).

## Worker WorkResult (module-worker-s1e) — accepted summary

- Corpus: 28 deterministic fusion-level cases covering: IR-G0 duplicate-box shapes
  (scale-stable), basic composition, fail-closed ambiguity, legacy crop path, the FULL
  uniform-list family (brackets/continuations/duplicate absorption/every fail-closed
  shape), control/icon/artifact edges, and the **v1n low-anchor viewport counterexample**
  (subtitle `Volume, vibration, Do Not Disturb` never a menu_item; exactly 3 menu_items;
  real titles fail-closed as text_block).
- Captured pipeline entry: `fuse_evidence` / `fuse_evidence_from_crops` →
  `evidence["candidates"]` (post-heuristics + row grouping — what the runtime consumes).
- Resident gate: per-case canonical payload equality + WHOLE-FILE byte gate; baseline
  absent ⇒ hard FAIL with regen instructions; `P26_REGEN_BASELINE=1` opt-in regen with
  round-trip stability assertion.

## Gate status

S1.7's reference is now frozen: **S1B's port must keep this test byte-identical** —
any difference = S1 hard-gate failure (stop).

DEVIATIONS (accepted): sort key adds deterministic `id` tiebreaker (superset);
v1n case's Display-row OCR-token absorption frozen as empirical current behavior
(does not affect the fail-closed pin). BLOCKED: none.
