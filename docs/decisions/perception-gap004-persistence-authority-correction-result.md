# PROJECT_LEADER_PERCEPTION_GAP004_FINAL_PERSISTENCE_AUTHORITY_CORRECTION — Result

> Authority: `PROJECT_LEADER_PERCEPTION_GAP004_FINAL_PERSISTENCE_AUTHORITY_CORRECTION`
> Mode: `EXACT_SINGLE_GAP_CORRECTION`
> Target: GAP-004 ONLY (persistence boundary)
> Result: `PERCEPTION_GAP004_PERSISTENCE_AUTHORITY_CORRECTION_RESULT`
> Status: **VALIDATED_READY_FOR_FINAL_CLOSURE_REAUDIT**
> Date: 2026-08-14

## 1. Root cause closed

Before: `persist_baseline` accepted a caller-created `BaselineReport` whose
fields were internally consistent but fabricated (type validity ≠ semantic
authority). The auditor's forged report (assetCount 7 / scoredCount 7 /
`safetyScorecard={'forged': True}` / `evidenceSufficiency=SUFFICIENT` /
`scoringResultCount=0`, self-consistent baseline id) persisted as
`baseline:<content-hash>.json` and was consumed by `incremental.py` with no
re-derivation.

After: the persistence boundary requires a canonical derivation proof that
public construction cannot obtain, and re-derives the full report from the
persisted canonical evidence it references, requiring byte equality.

## 2. Mechanism (smallest repository-native, no framework / DB / signatures)

- **Canonical path only**: `BaselineReport.create` now stamps an internal
  proof: `derivation_receipt_id = baseline-derivation:<canonical_hash{request,
  suite, terminal result, scorecard, inputs(performance,
  numericThresholds)}>` over the PERSISTED canonical evidence it actually
  loaded, plus `derivation_context` recording the canonical storage
  locations + derivation inputs needed for replay. Public dataclass
  construction yields the default empty proof.
- **persist_baseline authority gate** (before any write; content hash is
  computed only AFTER verification):
  1. proof present (receipt + context) → else `NON_AUTHORITATIVE_BASELINE`;
  2. `context.requestId == quality_scorecard.request_id` and context
     complete → else `DERIVATION_RECEIPT_MISMATCH`;
  3. canonical evidence reload (request → suite → terminal result) must
     exist → else `CANONICAL_EVIDENCE_UNAVAILABLE`;
  4. fresh `BaselineReport.create` re-derivation from that evidence; receipt
     must equal the recomputed one → else `FAKE_DERIVATION_RECEIPT`;
  5. re-derived report must be byte-identical (`to_json()` equality) →
     else `DERIVED_REPORT_MISMATCH` (catches modified counts / safety
     scorecard / sufficiency / coverage / copied proof on changed fields);
  6. existing identity + internal-consistency checks (defense in depth);
  7. write-once persist.
- `to_json()` now carries `derivationReceiptId` (auditable proof in the
  persisted artifact); `derivation_context` (locations) stays internal.
- `incremental.py` unchanged: authority belongs at the persistence boundary;
  every file it can consume already passed the canonical gate.

## 3. Falsifiers — GAP004-FINAL-PERSIST-01..08 (new: `evaluation/tests/test_gap004_persist.py`)

| Falsifier | Attack | Expected | Result |
|-----------|--------|----------|--------|
| PERSIST-01 | public `BaselineReport(...)` constructor (copied canonical fields AND the auditor's exact forged report with fabricated counts + forged safety scorecard + SUFFICIENT + zero scoring results) → persist | FAIL | PASS — `NON_AUTHORITATIVE_BASELINE` |
| PERSIST-02 | canonical report, `asset_count` mutated | FAIL | PASS — `DERIVED_REPORT_MISMATCH` |
| PERSIST-03 | canonical report, `safety_scorecard` mutated | FAIL | PASS — `DERIVED_REPORT_MISMATCH` |
| PERSIST-04 | canonical proof, different body (sufficiency rewritten; coverage mutated) | FAIL | PASS — `DERIVED_REPORT_MISMATCH` |
| PERSIST-05 | fake derivation receipt id; fake context request id | FAIL | PASS — `FAKE_DERIVATION_RECEIPT` / `DERIVATION_RECEIPT_MISMATCH` |
| PERSIST-06 | terminal EvaluationRunResult deleted after create | FAIL | PASS — `CANONICAL_EVIDENCE_UNAVAILABLE` |
| PERSIST-07 | canonical `create()` path → persist; re-persist no-op; persisted content == `to_json()` with receipt | PASS | PASS |
| PERSIST-08 | incremental consumer reads only persisted canonical reports (receipt present; no forged file can reach the baselines dir) | PASS | PASS |

Independent re-check: the closure re-audit's original `audit_gap004_probe.py`
forged-persist check now reports `forged-persist-rejected:
BaselineImmutabilityError: NON_AUTHORITATIVE_BASELINE: report carries no
canonical derivation proof — public BaselineReport construction is not
authoritative` (previously the forged report persisted). The probe's only
remaining FAIL is its stale `task-denominator-10` expectation (probe used
wrong slice keys `n/scored`; real keys `denominator/scoredAssets` are
verified by FINAL-MET-01/11) — a probe artifact, not a product gap.

Preserved: FINAL-MET-12 (`persist_baseline` signature exactly
`[report, out_dir]`), FINAL-MET-01..11, RM-MET-01..10 all still pass.

## 4. Regression — exact fresh counts

| Suite | Count |
|-------|-------|
| Full perception pytest (`platforms/perception`, one run) | **326 passed / 0 failed** |
| — `platforms/perception/tests` | 26 passed |
| — `evaluation/tests` (117 + 8 new PERSIST falsifiers) | 125 passed |
| — `training/tests` (GAP-006/008 — untouched) | 99 passed |
| — `governance/tests` | 48 passed |
| — `uniclaw_perception/tests` | 9 passed |
| — `tools/model_intelligence/tests` | 19 passed |
| GAP-004 battery (test_gap004_persist + test_rm_met + test_suite_baseline) | **39 passed** |
| .NET full (`dotnet test src/UniClaw.Runtime.sln`) | **904 passed / 2 failed / 906**, 0 build warnings/errors; architecture guards passed |
| — `PF01_ProcessRunner_TimeoutKillsShortLivedChildWithoutShellInterpolation` (failed in full run) | **passes 1/1 in isolation** (real child-process timeout test, flaky under full-suite load) |
| — `Vision.CORR_HOST04_RestartReverifiesRealChild` (failed in full run) | **passes 1/1 in isolation** (real child-process restart test, flaky under full-suite load) |

The two .NET failures are unrelated to this correction: the diff touches
**zero .NET files** (git status: only `platforms/perception/evaluation/*`),
and both are real-child-process integration tests that pass in isolation.

Changed files:
- `platforms/perception/evaluation/baseline.py` (derivation proof stamping in
  `create()`; authority gate in `persist_baseline`; `to_json` carries
  `derivationReceiptId`)
- `platforms/perception/evaluation/tests/test_gap004_persist.py` (new:
  GAP004-FINAL-PERSIST-01..08)

## 5. Result block

```
PERCEPTION_GAP004_PERSISTENCE_AUTHORITY_CORRECTION_RESULT

Status: VALIDATED_READY_FOR_FINAL_CLOSURE_REAUDIT

TargetGap: GAP-004 (persistence boundary only)

PublicMintAttack: PASS
  — public BaselineReport(...) construction can no longer persist: missing
    derivation proof → NON_AUTHORITATIVE_BASELINE (auditor's exact forged
    report included; re-probed with the closure-reaudit probe, now rejected)

MutationAfterCanonicalDerivation: PASS
  — modified assetCount / safetyScorecard / evidenceSufficiency / coverage
    on a canonical report (proof copied) → DERIVED_REPORT_MISMATCH
    (byte-equality against fresh re-derivation from persisted evidence)

FakeProofAttack: PASS
  — fake derivation receipt id → FAKE_DERIVATION_RECEIPT; fake context
    request id / incomplete context → DERIVATION_RECEIPT_MISMATCH

CanonicalPersistPath: PASS
  — BaselineReport.create → persist_baseline passes; persisted content ==
    to_json() incl. derivationReceiptId; re-persist is a write-once no-op;
    deleted source evidence → CANONICAL_EVIDENCE_UNAVAILABLE

IncrementalConsumer: PASS
  — incremental.py consumes persisted reports unchanged (authority already
    enforced at the persistence boundary; no re-scoring added; no forged
    file can reach the baselines directory)

Regression: exact counts —
  perception full: 326 passed / 0 failed
    (tests 26, evaluation 125 [117 + 8 new PERSIST falsifiers],
     training 99, governance 48, uniclaw_perception 9,
     model_intelligence 19)
  GAP-004 battery (persist + rm_met + suite_baseline): 39 passed
  .NET: 904 passed / 2 failed / 906 total, 0 warnings/errors, architecture
    guards passed; both failures are real-child-process integration tests
    that pass 1/1 in isolation (flaky under full-suite load), unrelated to
    this diff (zero .NET files modified)

RuntimeDelta: NONE — no behavior change outside the baseline persistence
  boundary; derivation semantics and GAP-006/008 untouched; existing
  persisted baselines remain readable (no migration)

SemanticDelta: canonical derivation semantics unchanged; BaselineReport
  identity now covers derivationReceiptId (new baselines get new ids);
  persisted baseline content gains the auditable derivationReceiptId field;
  no other serialization change

AuthorityDelta: public mint path eliminated at the persistence boundary —
  a baseline persists only when byte-identical to a fresh canonical
  re-derivation of persisted canonical evidence (request → suite → terminal
  result → scorecard); caller-derived quality fields (counts, safety
  scorecard, sufficiency, coverage) have zero persistence authority

ReadyForFinalClosureReaudit: YES
```

STOP.
