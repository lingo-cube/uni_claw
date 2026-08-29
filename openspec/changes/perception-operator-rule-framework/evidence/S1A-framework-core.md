# S1A. Framework Core (S1.1–S1.4) — Acceptance Evidence

## Leader's independent verification

- New suite re-run (leader): `test_operator_rules.py` → **41/41** (incl. seeded property
  test: 30 random rule sets × 3 permutations — lint/serialization/resolution
  permutation-stable).
- Full perception suite (leader run, incl. governance): 180 passed, 3 subtests, 2 failed —
  both failures PRE-EXISTING and documented by the perception repair's own report:
  `test_rper_06` (Adapter switch→toggle assertion; repair does not modify the Adapter)
  and `test_RSI08_active_convergence` (expected canonical convergence rejection while
  the candidate is unpromoted). Zero new failures from S1A → zero behavior change.
- Purity: worker delta = `uniclaw_perception/operators/` (5 modules) +
  `tests/test_operator_rules.py` only.

## Worker WorkResult (module-worker-s1a) — accepted summary

- Contracts: GENERATOR/VALIDATOR/ADVISOR; bounded ParameterSpec with `tighten_only`
  safe direction (VALIDATOR+numeric only, value-domain ≥ default); built-in `enabled`
  on GENERATOR only (validator disable → lint `validator_disable`); registry with
  pipeline declaration storage (topology wiring is S1B).
- Selector: exact five canonical dims + tags; missing → `default`; per-pin equality +
  tags-subset matching; specificity = pins + tags count.
- Resolver: per-value provenance (rule_id, pins, tags, specificity); intersection-scoped
  conflict detection (mutually exclusive pins → no conflict; uncovered reachable
  intersection → `specificity_conflict`; covering higher-specificity rule → resolved);
  pair order canonicalized (permutation-stable); `ResolutionConflictError` fail-closed
  backstop at resolve time.
- Ruleset: schemaVersion 1, sorted stable serialization, strict deserialize, linter with
  11 diagnostic kinds (unknown param, bounds, enum, validator disable/unsafe direction,
  dead rule, complexity budget default 32, conflict).

DEVIATIONS (accepted): full-suite "all green" replaced by baseline-parity evidence
(shared working tree has the retained candidate's documented red tests); conservative
rejection documented (detection exact over validated selectors). BLOCKED: none.
