# S4. Validators Wiring — Acceptance Evidence

## Leader's independent verification

- S4 tests: 26 passed + 68 subtests (veto/downgrade semantics; absent-channel trivial
  pass; vlm offline + default-False + not-in-pipeline/not-in-RUNNERS; corpus 34/34
  zero-veto; candidates byte-unchanged).
- After leader's 6 topology-assert updates (3-op→5-op, same S2iii precedent):
  wiring + relation-head + S4 = **57 passed + 68 subtests**.
- Full suite: **220 passed + 93 subtests, 1 failed (RPER-06 pre-existing)**; governance
  48 + 1 (RSI08 pre-existing); equivalence gate **byte-green**.
- Purity: new operators + registry + contracts (additive ADVISOR-enabled) + S4 tests +
  leader's assert updates; no engine/governance/config.py/S1-asset/C#/Runtime edits by
  the worker.

## Accepted design summary

- `text-relation-check` (VALIDATOR): veto only on empty/too-short head text and
  verbatim duplicate head at same position; same-text-different-position ⇒ annotate.
- `structured-corroboration` (VALIDATOR): optional adapter-side structured channel
  (absent in executed pipeline ⇒ trivial pass); downgrade/corroborate annotations;
  veto only on strong fully-available contradiction.
- `vlm-annotation` (ADVISOR): offline-only deterministic no-op stub; registered
  `enabled=False`; NEVER in pipeline or RUNNERS (asserted).
- Annotate-only byte contract: validator outputs live in decision records; candidates
  byte-untouched (equivalence gate green by construction, asserted on 34 cases).
- Pipeline: exactly [uniform-list-row-grouping, row-relation-head, spacing-verifier,
  text-relation-check, structured-corroboration]; ADVISOR-enabled contracts extension
  is additive (VALIDATOR hard-no-enabled unchanged).

## Batch status: S1 + S2 + S4 ALL PASS

Authorized batch complete. Remaining re-entry preconditions (Gate #2): real-frame
candidate-per-provable-row confirmation + no Runtime/CURRENT-ACTIVE change (working
tree verified: Runtime files untouched this batch; receipt untouched).
