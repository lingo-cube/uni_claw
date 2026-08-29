# S1 Checkpoint Reverification — 2026-08-28

Change: `perception-operator-rule-framework`
Scope: S1.5–S1.8 only
Base revision: `e6c6f4b5eb927d05338128f86058d391cc23a3ba`
Worktree: dirty; existing changes were preserved and no S1 production file was edited by this reverification.

## Implementation verification

- S1.5 governance binding remains present through `PerceptionConfig.ruleset_content`, deterministic ruleset hashing, receipt-bound active resolution and fail-closed invalid-ruleset handling.
- S1.6 remains wired as `uniform-list-row-grouping` GENERATOR followed by mandatory `spacing-verifier` VALIDATOR.
- S1.7 retains the 28-case corpus and whole-file byte-equivalence gate.
- S1.8 retains deterministic operator trace and offline replay behavior.

## Invariant verification

- No `governance/artifacts/`, CURRENT-ACTIVE receipt, Runtime, Strategy Contract, GoalEvidence or SourceIdentity file was modified by this reverification.
- `spacing-verifier` remains non-disableable through configuration.
- The checkpoint did not enter S2, S3, S5 or Phase 2.6 R.1.
- `scripts/check-consistency.sh`: `ALL PASS`.
- `git diff --check`: `PASS`.

## Test verification

Command from `platforms/perception`:

```text
../../.venv-local-vision/bin/python -m pytest tests/test_row_composition_equivalence.py tests/test_navigation_row_composition.py tests/test_operator_pipeline_wiring.py tests/test_ruleset_governance.py -q
```

Result: `61 passed, 1 warning, 3 subtests passed`.

The warning is a sandbox-only pytest cache write denial and does not affect test results.

## Checkpoint result

`S1_CHECKPOINT_REVERIFIED_PASS` — S1.5–S1.8 remain verified, including the zero-behavior-difference hard gate. No task checkbox changed because all four tasks were already complete when this session began.

Separate consistency gap, not resolved in this checkpoint: `tasks.md` records S2/S4 complete while the older `HANDOFF-STATE.md` still describes S2 as dispatched/interrupted. This does not invalidate S1 evidence but blocks using that handoff file as current lifecycle truth without reconciliation.
