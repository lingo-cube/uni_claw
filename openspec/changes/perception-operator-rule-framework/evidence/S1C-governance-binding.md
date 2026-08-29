# S1C. Governance Binding (S1.5) — Acceptance Evidence

## Leader's independent verification

- `tests/test_ruleset_governance.py` + equivalence byte gate → **19 passed** (18 new + gate).
- Full suite `tests/` + `uniclaw_perception/tests/` → 165 passed + 1 pre-existing red
  (RPER-06 only) — zero new failures.
- `governance/tests/` → 48 passed + 1 pre-existing red (RSI08 only — expected
  convergence rejection; receipt untouched).
- Purity: zero changes under `governance/artifacts/` (no CURRENT-ACTIVE, no
  deployments/config-manifests content edits).

## Worker WorkResult (module-worker-s1c) — accepted summary

- `PerceptionConfig.ruleset_content` (None = default root); `compute_config_hash`
  incorporates the ruleset axis (absent → stable `DEFAULT_RULESET_MARKER`; any byte
  change → different hash); `resolve_active_rule_set` fail-closed (invalid ruleset
  aborts startup naming diagnostics); `get_active_rule_set()` is the runtime surface
  for S2/S4.
- Manifest `ruleset` axis `{contentHash, evidenceRelevant}` inside `_identity_content()`
  (ConfigId-owned, IDR-03 pattern — no second deployment axis); legacy manifests parse
  unchanged.
- Unpromoted candidate rule sets can never affect resolution unless embedded in the
  loaded config identity (tested incl. context-matched non-interference).
- Embedded shape: optional top-level `"ruleset"` string in label-mapping.json.

DEVIATIONS: none. BLOCKED: none.

## Slice completion

**S1 (all of S1.1–S1.8) = PASS with ZERO BEHAVIOR DIFFERENCE** (equivalence byte gate
green through every slice; full-suite parity at each step; no CURRENT-ACTIVE change).
Next: S2 (acceptance protocol frozen in `S2-acceptance-protocol.md`).
