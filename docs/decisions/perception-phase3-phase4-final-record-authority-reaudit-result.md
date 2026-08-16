# Perception Phase 3 / Phase 4 Final Record-Authority Re-audit Result

> Date: 2026-08-14
> Role: fresh independent Sol adversarial semantic-enforcement re-audit
> Input: `perception-phase3-phase4-semantic-record-minting-correction-result.md`
>   (VALIDATED_READY_FOR_FINAL_FRESH_REAUDIT)
> Result: `PERCEPTION_PHASE3_PHASE4_FINAL_RECORD_AUTHORITY_REAUDIT_RESULT`
> Status: **REPAIR_INCOMPLETE**
> Repair performed: NONE (audit only — REPAIR was FORBIDDEN)
> Audit target: working tree at commit `3f0e774`
>   (the RM-LEAK-06 session-evidence hardening was committed mid-audit;
>   the audited content is unchanged — see §7 DiffCheck)

---

## 0. Verdict

```text
Status: REPAIR_INCOMPLETE
S0Count: 0
S1Count: 3
RemainingS1: GAP-004, GAP-006, GAP-008
AuthoritativeBypassableCount: 3
CriticalUnprovenCount: 0
AmbiguousAuthoritativePaths: 1
PublicCallerMintPaths: 0
SemanticClosureDeclarable: NO
Phase3Phase4SemanticEnforcement: OPEN
ArchitectureReopenRequired: NO
RuntimeDelta: NONE | SemanticDelta: NONE | AuthorityDelta: NONE
```

The correction closed the **object-minting** surfaces (GAP-007 fully; the
receipt/session-evidence mint paths of GAP-006/GAP-008) but left three
**consumer-boundary scope-derivation** seams open. The surviving pattern is
the inverse of the previous round:

> Per-record claims are now verified against persisted canonical evidence —
> but the POPULATION and the derived-record FIELDS remain caller-declared.
> Verifying each claim does not prove the claim set is complete, and
> anchoring config/invocation/admission does not anchor state, base model,
> metrics, environment, or checkpoints.

All survivors are executable against the current production boundaries
(probe transcripts below; no production code was modified).

---

## 1. Gap-by-gap adversarial results

### GAP-004 — quality authority — **STILL BYPASSABLE, S1**

Per-claim verification is real and works: `verify_and_derive_scorecard`
re-loads the persisted Prediction by (request, asset), re-verifies
run/deployment bindings, loads GT, RE-SCORES through
EvaluationScoringContext, and rejects stage/LabelSpace claim mismatches.
Wrong request / wrong deployment / ghost asset / wrong stage claims were
all **rejected** in fresh probes (ATTACK_D1..D4).

But the **scope** of the quality evidence is caller-selected. The
mandatory cherry-picking attack succeeds:

```text
Setup: canonical EvaluationSuite (10 members) + EvaluationRunRequest +
10 persisted Predictions + 10 persisted GTs.

ATTACK_A (cherry-pick): BaselineReport.create(scoring_results = only the
7 favorable verified claims, classified = only those 7) →
  evidenceSufficiency.stance = SUFFICIENT
  coverage.assetCount = 7 / scored 7 / unscored 0
  taskSlices.ELEMENT_DETECTION.aggregate = {mean 1.0, n: 7}

CONTROL (honest, all 10 declared): sufficiency = PARTIAL (7 scored / 10).
```

Ten requested assets with seven favorable supplied assets become a 7/7
fully-assessed SUFFICIENT baseline. The three missing assets vanish
entirely — nothing at the `BaselineReport.create` boundary loads the
canonical EvaluationRunRequest / EvaluationRunResult / EvaluationSuite to
establish the authoritative population. The architecture does NOT define
Baseline scope separately from EvaluationRun scope with a canonical
authority record; scope is whatever the caller passes, and the canonical
producer (`execute_baseline`) is just one caller among many.

Additional surviving probes:

- **ATTACK_B (out-of-scope asset):** a persisted Prediction + GT for an
  asset that is NOT a suite member was accepted into the canonical
  baseline (no suite-membership check exists at the boundary).
- **ATTACK_C1 (declared_tasks):** `declared_tasks=[]` →
  `evidence_sufficiency = SUFFICIENT` with zero declared tasks.
- **ATTACK_C2 (classified dimensions):** fabricated classification
  dimensions (systemFamily/componentClass/criticality) entered the
  canonical coverage slices as ASSESSED 7/7.
- **ATTACK_C3 (safety scorecard):** a caller-fabricated
  `safety_scorecard` (fabricationRate 0.0, coordinateValidityRate 1.0,
  attacker note) was accepted unverified into the canonical baseline body
  and participates in the baseline identity.
- **ATTACK_C4 (denominator lies):** report-level `assetCount=10 /
  scoredCount=10` minted while the derived coverage says 7 — an
  internally inconsistent canonical record.
- **ATTACK_D5 (zero evidence):** an empty baseline (0 assets) is mintable;
  it is honest (INSUFFICIENT, empty slices) — no quality upgrade gained;
  reported for completeness, not counted as a bypass.
- **AMBIGUITY (D6):** the claimed `ground_truth_version` is never
  cross-checked. With two GT versions persisted for one asset, the
  loader picks the first glob match; a scorecard can claim `gt_version=2`
  while the bound result carries `gt_version=1` (version 1 was scored).
  Version selection = filesystem glob order, claim ignored.
  → AmbiguousAuthoritativePaths: 1.

`persist_baseline` accepted the cherry-picked report as an immutable
canonical baseline (write-once path).

### GAP-006 — admission authority — **PARTIALLY CLOSED; ONE SEAM OPEN, S1**

The receipt-minting authority is genuinely closed. Fresh probes:

- **ATTACK_A (recomputable but unpersisted):** canonical admission
  receipt id, receipt never persisted → `TRAINING_ADMISSION_
  PERSISTENCE_MISMATCH`. Blocked.
- **ATTACK_B1 (forged persisted content):** a forged ADMITTED receipt
  persisted via the public `save_training_admission_receipt` for a
  leaky dataset (canonical admission raises EXACT_CONTENT leakage) →
  execute_training re-derived admission and rejected the leakage.
  The public receipt writer cannot mint authority because admission is
  RE-DERIVED at the execution boundary. Blocked.
- **ATTACK_B2 (tampered content under claimed id):** blocked (content
  address + recompute equality).
- **ATTACK_C (stale protected-set snapshot):** receipt for protected set
  A, execution with set B → `TRAINING_ADMISSION_MISMATCH`. Blocked.
- **ATTACK_D1 (dataset substitution):** receipt for dataset A, execution
  with dataset B → `TRAINING_ADMISSION_MISMATCH`. Blocked.
- **ATTACK_E (stale policyVersion):** receipt persisted under policy
  LEAK-01..06-v0 → recompute under the current pinned policy differs →
  mismatch. Blocked.

**ATTACK_D2 (same claimed id, different actual content) — SURVIVES:**

```text
execute_training(dataset=A (valid receipt), data_path=/UNRELATED/data.yaml)
→ EXECUTED; session + terminal TrainingRun claim dataset_version_id=A
while the actual trained bytes come from an unrelated location.
```

`DatasetVersion` is a membership manifest whose asset ids are sha256 of
image bytes — the semantic binding to physical content exists — but the
execution seam does not verify `data_path` content against the admitted
manifest (neither images nor label files). The receipt authorizes
admission semantics (membership, leakage, annotation chains); it does not
bind what actually gets trained. A caller holding a valid receipt can
produce canonical records that claim dataset A while training on
different bytes. This matches GAP-006 Attack D's "same claimed id with
different actual content — must fail" clause; it does not.

Honest counter-consideration (recorded for the next correction): the
P4-T2 design declares the dataset to BE the manifest, and data_path is
"execution location context". But the manifest's identity is content
hashes of bytes, so the claimed dataset identity is falsifiable at the
execution seam. Binding data content to the admitted manifest (or
declaring data_path explicitly unbound) is the correction decision.

### GAP-007 — annotation acceptance authority — **CLOSED, E4 / NOT_BYPASSABLE**

Fresh probes, all blocked:

- **ATTACK_A (predecessor origin):** unpersisted predecessor, lookalike
  persisted predecessor with same-looking payload, mutated predecessor —
  all rejected ("acceptance predecessor must already be persisted";
  content-identity load).
- **ATTACK_B (event binding):** canonical event A + different accepted
  annotation B, mutated accepted payload under canonical provenance,
  wrong reviewer identity, invented event id, wrong stage lineage — all
  rejected by `validate_acceptance_chain` (accepted-annotation identity,
  payload hash, reviewer, deterministic event id, stage/LabelSpace).
- **ATTACK_C (internal surfaces):** annotation module exposes no
  `save_acceptance_event` and no `_persist_*` helpers; the only write
  symbols are `accept_and_persist`, `save_annotation`, and the generic
  `write_once_json`. Public `save_annotation` refuses ACCEPTED records.
  The full original forge replay (MODEL_ASSISTED draft + forged accepted
  annotation + public save + admission) fails at every step.
- **ATTACK_D (legacy):** an ACCEPTED record without a verifiable chain
  classifies as LEGACY_ACCEPTANCE_PROVENANCE; admission rejects it.
  Legacy records remain readable.

The generic primitive `write_once_json` can fabricate any self-consistent
JSON for a hostile caller — this was probed and is documented (§6); it is
classified as direct filesystem mutation, out of scope per the frozen
threat model (the domain writer surface is what is closed).

### GAP-008 — TrainingRun authority — **STILL BYPASSABLE, S1**

The session-minting attacks are blocked:

- **ATTACK_A/B (caller-minted session / predictable evidence id):**
  `commit_execution_run` reloads the persisted content-addressed session
  evidence and requires byte-level payload equality — an in-memory
  session or an invented evidence id with no persisted record is
  rejected (`TRAINING_INVOCATION_MISMATCH`). A caller-minted session
  passes only when byte-identical to real persisted evidence, i.e. it
  can only re-commit the truth (same run id).
- **ATTACK_C (public writer):** `save_execution_session_evidence` is
  absent from the module (removed at `3f0e774`); no public domain writer
  persists session evidence.

But the terminal record's remaining fields are caller-declared, and the
following attacks MINTED canonical persisted TrainingRuns:

- **ATTACK_D1 (terminal-state falsification):** a REAL failed execution
  session (`terminal_error='RuntimeError: boom'`, persisted canonical
  evidence, congruent=True) → `commit_execution_run(state=COMPLETED,
  terminal_outcome='completed')` **minted and persisted a COMPLETED
  TrainingRun** whose own session evidence shows the error. The reverse
  (a successful session claimed FAILED) also mints. Attack D's
  "different terminal state — must fail" clause is violated: state and
  terminal_outcome are not derived from `session.terminal_error`.
- **ATTACK_D2 (base-model substitution):** real session + real config
  (config identity binds `base_model_artifact_id=sha256:realbase`) →
  commit with `base_model_artifact_id="sha256:FABRICATED_BASE_MODEL"`
  minted a run whose baseModelArtifactId contradicts its own
  training_config_id. "Different config — must fail" violated.
- **ATTACK_D3/D4/D5 (fabricated checkpoints / environment / metrics):**
  invented `produced_checkpoints` entries, a fabricated
  TrainingEnvironment (python 9.9 / cuda / "FABRICATED" os), and
  invented training metrics (`fitness: 1.0, mAP50: 0.99`) were all
  minted into canonical history over the real session. These fields are
  not derived from or cross-checked against the persisted execution
  evidence.

**Attack E (real FAILED history) is satisfied:** a real failed execution
commits a canonical FAILED run through the same path (control PASS), and
`save_training_run` still refuses all states.

### Regression replay (previously closed gaps)

```text
GAP-001: PASS — semantic [] preserved; Vision behavioral suite fresh in
         the 906-run.
GAP-002: E4 — fresh 9/9 geometry-enforcement tests replay the invalid
         boundsPx / NaN / beyond-frame attacks (only valid siblings
         survive, INVALID_GEOMETRY never OK_EMPTY); _run_pipeline remains
         the single serialization convergence.
GAP-003: PASS — ASSET_03 source-byte mismatch blocks before pipeline;
         ASSET_04 verified buffer is the only authoritative image read
         (fresh in evaluation 88/88).
GAP-005: PASS — write-once tests fresh (root 19/19). Re-proven live:
         the repo mini-artifacts' legacy-schema acceptance events caused
         a write-once collision refusal instead of an overwrite (§5).
GAP-009 / P4-34: E4 — factory-created real Host composition + behavioral
         proofs passed inside the fresh .NET run (906, 0 skipped).
GAP-010: PASS — immutable request/terminal result (RUN_04/RUN_06 fresh).
GAP-011: PASS — graduation doc retains the additive executable pipeline
         description.
GAP-012: PASS — Adapter doc + VisionServiceHost use
         platforms/perception/uniclaw_perception /
         uniclaw_perception.server:app; no legacy launch references.
```

No regression found.

---

## 2. Cross-domain mint laundering

```text
valid object + wrong origin → canonical consumer .... blocked (content
      addressing makes origin = content; domain loaders verify identity)
valid id + no persisted authority → canonical consumer .... blocked
      (receipt / session evidence probes)
valid persisted component records → forged aggregate authority .... OPEN
      (GAP-004 scope shrink; GAP-008 state/base-model/metrics)
legacy readable record → new canonical write .... blocked
      (legacy annotation inadmissible; no legacy TrainingRun writer)
noncanonical helper output → canonical commit .... blocked
      (build_provenance_bound_scorecard output cannot enter
      BaselineReport.create — no scorecard parameter;
      compute_task_metrics / execute_ultralytics_training have no
      persistence path)
caller-created execution/admission evidence → canonical history .... OPEN
      (GAP-008 D1..D5 over real session evidence)
```

Structural validity no longer mints authority anywhere except the three
surviving consumer-boundary seams above.

---

## 3. Fresh canonical mini-training — PASS (process proof)

Executed fresh in an isolated artifacts directory through the same
canonical API chain as `training/mini.py`, with REAL ultralytics
execution (1 epoch, CPU, 6 synthetic images, mAP50 0.0933 — process
proof only, not quality):

```text
draft → save_annotation → accept_and_persist → DatasetVersion →
save_dataset → admit_dataset_for_training →
save_training_admission_receipt → execute_training (real model.train,
congruent=True) → persisted execution-session evidence →
commit_execution_run → terminal TrainingRun (persisted) →
checkpoint (best.pt) → ModelArtifact → Candidate → lineage.
```

Exact identity connections verified (10/10):

```text
run.dataset_version_id == dataset id            OK
run.training_admission_receipt_id == receipt id OK
run.training_config_id == cfg id                OK
session.dataset_version_id == dataset id        OK
artifact.source_training_run_id == run id       OK
artifact.source_checkpoint_id == checkpoint id  OK
candidate.training_run_id == run id             OK
candidate.dataset_version_id == dataset id      OK
candidate.training_config_id == cfg id          OK
candidate.model_artifact_id == artifact id      OK
```

Corruption probes — downstream lineage does NOT materialize:

```text
CORRUPT1 admission-receipt identity (dataset swap) .... blocked
        TRAINING_ADMISSION_MISMATCH — no new run records
CORRUPT2 execution-session evidence identity (invented id) .... blocked
        no commit, corrupt-runs dir empty
CORRUPT3 TrainingConfig/invocation congruence (captured≠resolved) ....
        blocked — TrainingInvocationMismatchError
CORRUPT4 config swap (different TrainingConfig over real session) ....
        blocked — session config identity mismatch
```

**Observation (write-once working as designed):** running the repo's own
`python -m training.mini` against `training/artifacts` fails with a
write-once collision: existing acceptance events predate the
`acceptedAnnotationId` binding (legacy event schema), and the canonical
writer correctly refuses to replace them. The historical artifacts remain
readable, are not fresh-admissible, and were not touched. The fresh proof
above used isolated storage — new records only.

---

## 4. Fresh test execution

```text
Record minting (RM-MET 12 + RM-C3 37): 49/49 PASS  (within eval+training)
Evaluation:       88/88 PASS   (fresh)
Training:         74/74 PASS   (fresh)
Governance unit:  48/48 PASS   (fresh; two consecutive clean runs —
  the first run of this session had 1 transient failure at 47.9s whose
  name was lost to output truncation; it did not reproduce)
Model-Intelligence: 19/19 PASS (fresh)
Perception root:  19/19 PASS   (fresh)
Geometry enforcement: 9/9 PASS (fresh)
Architecture guards (VisionHost behavioral 16/16 + factory 11/11):
  inside the fresh .NET run, 0 skipped
Consistency C1-C10 (scripts/check-consistency.sh): ALL PASS (fresh)
DiffCheck: PASS — no production/test/doc file modified by this audit;
  working tree clean at 3f0e774
```

## 5. .NET full regression

```text
dotnet test UniClaw.Runtime.sln (fresh, this session):
  906/906 PASS — 0 failed, 0 skipped, 51 s
  Includes real Host composition and the PF01 ProcessRunner test under
  full-suite load.
```

## 6. PF01 timing test

```text
PF01_ProcessRunner_TimeoutKillsShortLivedChildWithoutShellInterpolation:
PASS — fresh full-suite run included it with 0 failures; targeted
ProcessRunner filter run: 2/2 PASS.
The previously reported under-load flake did not reproduce in this
session. No PF-01 file was modified during this audit.
```

## 7. Boundary judgment documented (generic primitive)

`write_once_json` (persistence.py) is the canonical storage primitive and
can fabricate any self-consistent JSON record — probed: a forged
session-evidence file written through it verifies at commit. This is
classified as DIRECT FILESYSTEM MUTATION by a hostile caller, out of
scope per the frozen threat model (ARTIFACT/INFRASTRUCTURE INTEGRITY
COMPROMISE; defending it would require signing/PKI/secrets, which the
threat model explicitly does not require). The enforcement boundary is
the DOMAIN writer surface, and that surface is closed: no public writer
mints accepted annotations, acceptance events, admission receipts, or
execution-session evidence outside the canonical paths. PublicCallerMint
Paths: 0.

---

## 8. Historical artifacts

```text
HistoricalArtifacts: WAIVED_BY_HUMAN_NOT_EXECUTED
(preserved verbatim; no historical rewrite; no promotion to PASS)
```

---

## 9. Final output block

```text
PERCEPTION_PHASE3_PHASE4_FINAL_RECORD_AUTHORITY_REAUDIT_RESULT

Status: REPAIR_INCOMPLETE

S0Count: 0
S1Count: 3
RemainingS1: GAP-004 (quality scope authority), GAP-006 (execution
              content binding), GAP-008 (terminal record fields)

AuthoritativeBypassableCount: 3
CriticalUnprovenCount: 0
AmbiguousAuthoritativePaths: 1
PublicCallerMintPaths: 0

GAP004: STILL_BYPASSABLE / S1
QualityScopeCherryPickAttack: SURVIVED_EXECUTABLE
  (10 requested, 7 supplied → 7/7 SUFFICIENT canonical baseline;
   + out-of-scope asset inclusion; + declared_tasks/classified/safety/
   count caller-claims; + GT-version selection ambiguity)

GAP006: PARTIALLY_CLOSED / S1 (one seam)
UnpersistedReceiptAttack: BLOCKED
  (receipt mint authority E4: unpersisted/forged/stale/substitution/
   policy all rejected; data_path content is not bound to the admitted
   dataset manifest — Attack D2 survives)

GAP007: E4 / NOT_BYPASSABLE
ForgedAcceptanceOriginAttack: BLOCKED
  (predecessor origin, event binding, internal surfaces, legacy — all
   rejected; full original forge replay fails at every step)

GAP008: STILL_BYPASSABLE / S1
CallerMintedExecutionSessionAttack: BLOCKED
  (in-memory session / invented evidence id / public writer — blocked)
  BUT terminal-state falsification, base-model substitution, and
  fabricated checkpoints/environment/metrics mint canonical runs over
  real session evidence

FreshCanonicalMiniTraining: PASS
  (fresh real ultralytics; 10/10 identity connections; 4/4 corruption
   probes blocked; repo artifacts untouched — legacy write-once
   collision refused replacement)

RegressionBattery: PASS
  GAP-001/003/005/010/011/012 PASS — no regression
GAP002: E4
GAP009_P4_34: E4

FreshTests: 257 Python tests fresh PASS (74+88+48+19+19+9) +
  49/49 record-minting falsifiers (within the above) +
  C1-C10 ALL PASS + DiffCheck clean
DotNetRegression: 906/906 PASS (fresh, 0 failed, 0 skipped)
PF01TimingTest: PASS (fresh full-suite run; targeted 2/2)

HistoricalArtifacts: WAIVED_BY_HUMAN_NOT_EXECUTED

RuntimeDelta: NONE
SemanticDelta: NONE
AuthorityDelta: NONE
ArchitectureReopenRequired: NO

SemanticClosureDeclarable: NO
Phase3Phase4SemanticEnforcement: OPEN
NextTask: ONLY_THE_EXACT_SURVIVING_CORRECTION
  (1) GAP-004: canonical scope authority at BaselineReport.create —
      load persisted EvaluationRunRequest / EvaluationRunResult /
      EvaluationSuite and derive population, classified, declared_tasks,
      asset/scored counts, safety section from canonical records;
      cross-check claimed GT version.
  (2) GAP-006: bind execution content to the admitted dataset manifest
      (verify data_path images/labels against membership asset ids) or
      explicitly declare and record the binding decision.
  (3) GAP-008: derive state/terminal_outcome from
      session.terminal_error, base_model_artifact_id from the config,
      and anchor checkpoints/environment/metrics to canonical evidence
      in training_run_from_execution.

PF01RealityProof: NOT_EXECUTABLE_NO_ONLINE_DEVICE
```

STOP.
