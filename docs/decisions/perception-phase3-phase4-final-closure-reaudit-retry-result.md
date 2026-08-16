# PERCEPTION_PHASE3_PHASE4_FINAL_CLOSURE_REAUDIT_RETRY — Result

> Authority: `PROJECT_LEADER_PERCEPTION_PHASE3_PHASE4_FINAL_CLOSURE_REAUDIT_RETRY`
> Mode: **FRESH AUDIT ONLY** — no code changes; prior GAP-004 correction result NOT trusted;
> only executable behavior of the current working tree verified.
> Result: `PERCEPTION_PHASE3_PHASE4_FINAL_CLOSURE_REAUDIT_RESULT`
> Status: **CLOSED**
> Date: 2026-08-14

## 1. Independent executable evidence (fresh probe `/tmp/audit_gap004_closure_retry.py`, 23 checks)

All attack families implemented independently of the repo's own falsifier
battery, run against the current working tree:

| Family | Attack | Verdict |
|--------|--------|---------|
| A. Original attack (S0) | public `BaselineReport(...)` with fabricated assetCount/scoredCount/safetyScorecard/sufficiency + forged `ProvenanceBoundScorecard` (scoringResultCount=0) → `persist_baseline` | **FAIL — `NON_AUTHORITATIVE_BASELINE`** |
| B. Mutation | canonical report, then `assetCount` / `coverage` / `safetyScorecard` / `evidenceSufficiency` / `taskSlices` mutated → persist | **FAIL — `DERIVED_REPORT_MISMATCH`** (all 5) |
| C. Proof forgery | fake receipt; fake context requestId; fake receipt+context; empty context → persist | **FAIL — `FAKE_DERIVATION_RECEIPT` / `DERIVATION_RECEIPT_MISMATCH` / `NON_AUTHORITATIVE_BASELINE`** |
| D. Proof reuse | report A's receipt+context applied to report B (both directions) | **FAIL — `DERIVATION_RECEIPT_MISMATCH`** |
| E. Source deletion | terminal result / request / suite deleted after `create()` | **FAIL — `CANONICAL_EVIDENCE_UNAVAILABLE`** (truthful unavailable state) |
| F. Canonical path | `BaselineReport.create()` → persist; persisted content == `to_json()` incl. receipt; identity recomputes from persisted content; write-once no-op re-persist; incremental read pattern | **PASS** (4 checks) |
| G. Mint-path inventory | only `first_baseline.execute_baseline` persists; zero direct `BaselineReport(...)` construction in production code; zero alternate JSON writers into baselines dir | **PASS** (3 checks) |

**Closure counts: S0 = 0 · S1 = 0 · AuthoritativeBypassableCount = 0 · PublicCallerMintPaths = 0**

Repo's own battery (test_gap004_persist + test_rm_met + test_suite_baseline):
**39 passed** — GAP004-FINAL-PERSIST-01..08, FINAL-MET-01..12, RM-MET-01..10
all green. FINAL-MET-12 (`persist_baseline` signature exactly `[report, out_dir]`)
intact.

## 2. GAP-006 / GAP-008 re-verification (executable probes, unchanged code)

- GAP-006: **20/20 PASS** (`/tmp/audit_gap006_probe.py`)
- GAP-008: **16/16 PASS** (`/tmp/audit_gap008_probe.py`)

## 3. Regression (fresh runs of current working tree)

| Suite | Result |
|-------|--------|
| Full perception pytest | **326 passed / 0 failed** |
| GAP-004 battery (persist + rm_met + suite_baseline) | 39 passed |
| GAP-006 probe | 20/20 |
| GAP-008 probe | 16/16 |
| .NET full (dotnet test src/UniClaw.Runtime.sln) | 904 passed / 2 failed / 906, 0 warnings/errors |
| — PF01_ProcessRunner_TimeoutKillsShortLivedChild… | passes 2/2 isolated (real child-process flake; zero .NET files in perception diff) |
| — Vision.CORR_HOST04_RestartReverifiesRealChild | passes 2/2 isolated (real child-process flake; zero .NET files in perception diff) |

## 4. Known non-gaps (documented, not closure blockers)

- Prior probe artifact `task-denominator-10` (stale expectation using slice keys
  `n/scored`; real keys `denominator/scoredAssets` verified by FINAL-MET-01/11)
  is a probe-side issue, not a semantic gap.
- `.NET` full-suite 2 failures recur on every full run (real-child-process
  integration tests) and pass in isolation; perception diff contains zero
  .NET files.
- The two .NET failures are unrelated to this audit and to the perception
  correction; reported transparently.

## 5. Result block

```
PERCEPTION_PHASE3_PHASE4_FINAL_CLOSURE_REAUDIT_RESULT

Status: CLOSED

GAP004: PASS
  — public mint (original attack) → NON_AUTHORITATIVE_BASELINE (S0=0)
  — mutation (assetCount/coverage/safetyScorecard/sufficiency/taskSlices)
    → DERIVED_REPORT_MISMATCH
  — proof forgery (fake receipt/context) → FAKE_DERIVATION_RECEIPT /
    DERIVATION_RECEIPT_MISMATCH
  — proof reuse A→B (both directions) → DERIVATION_RECEIPT_MISMATCH
  — source deletion → CANONICAL_EVIDENCE_UNAVAILABLE (truthful
    unavailable state)
  — canonical create() path persists identically (receipt + identity
    recompute verified)

GAP006: PASS (20/20 executable probe)

GAP008: PASS (16/16 executable probe)

PublicCallerMintPaths: 0
  — only first_baseline.execute_baseline → persist_baseline writes the
    baselines dir; zero direct BaselineReport(...) construction in
    production code; zero alternate JSON writers

RemainingBypass: NONE (S1 = 0, AuthoritativeBypassableCount = 0)

Regression:
  perception full: 326 passed / 0 failed
  GAP-004 battery: 39 passed
  GAP-006 probe: 20/20 · GAP-008 probe: 16/16
  .NET: 904 passed / 2 failed / 906 (both failures are real-child-process
    integration tests that pass 2/2 in isolation; zero .NET files in the
    perception diff; 0 warnings/errors)

SemanticClosureDeclarable: YES
```

STOP.
