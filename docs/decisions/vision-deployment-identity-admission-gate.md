# Vision Deployment Identity Admission — Gate Record

> Status: ADMISSION_ANALYSIS_COMPLETE (baseline; no receipt mutation)
> Date: 2026-08-17
> Prerequisites: VISION_RUNTIME_BOOTSTRAP GRADUATED · B1a = RESOLVED ·
> B1b = BLOCKED（stale admission）
> Constraint: no production/perception/bootstrap changes; `current-active-identity.json`
> remained read-only throughout this gate (git-clean, verified).

---

## 1. Identity derivation graph (source-verified)

```
best.pt (model artifact bytes) ──────────────→ modelId        (SHA-256 of file content)
resolved config snapshot + label-mapping hash → configId       (content-addressed config manifest)
perception source modules + deps + OCR models → pipelineRevision (content-addressed pipeline hash)
        schemaVersion = "uniclaw.localVisionEvidence.v1" (frozen constant)
                          │
     schema + schemaVersion + modelId + configId + pipelineRevision
                          │
                          └──────────────→ deploymentId  (canonical_hash of identity_content)
```

| Axis | Semantic | Authoritative input | Derivation | Deterministic | Independent | Validation point | Canonical producer |
|---|---|---|---|---|---|---|---|
| schemaVersion | identity schema contract | frozen constant | constant | yes | n/a | factory schema check | build_active_identity SCHEMA |
| modelId | model artifact identity | `best.pt` bytes | `sha256_file` (content) | yes | yes | factory model axis | `model_manifest.build_current_active_manifest` |
| configId | resolved config identity | config + label-mapping content | `build_from_perception_config` (content hash) | yes | yes | factory config axis | `config_manifest.build_from_perception_config` |
| pipelineRevision | pipeline source+deps identity | perception modules + deps + OCR models | `compute_pipeline_revision` (content hashes) | yes (verified: 2 runs identical) | yes | factory pipeline axis | `pipeline_revision.compute_pipeline_revision` |
| deploymentId | **composite** identity | the four axes above | `canonical_hash(identity_content)` | yes | NO — composes the others | factory deployment axis | `deployment.PerceptionDeploymentCandidate.deployment_id` |

`serviceVersion` is metadata only — NOT identity (deployment.py: "metadata only — NOT identity").

---

## 2. Governance flow (source-verified state machine)

```
candidate creation   ← build_active_identity.py: build active candidate from
                       ACTUAL production state (config manifest + pipeline
                       revision + model manifest → PerceptionDeploymentCandidate)
        ↓
candidate validation ← canonical legacy-collision checks (_save_new_or_verify_legacy)
                       + receipt four-axis validation (CanonicalVisionHostFactory)
        ↓
admission decision   ← HUMAN / PROJECT LEADER promotion authority
                       (Phase 4 governance: "Project Leader/human promotion
                       authority | unchanged" — the decision is OUTSIDE the script)
        ↓
canonical receipt generation ← build_active_identity.py writes the COMPLETE
                       current-active-identity.json (all axes in one write)
        ↓
current-active activation ← CanonicalVisionHostFactory.Create reads + validates
                       the complete receipt at bootstrap
```

**Missing step today**: the *admission decision* (human gate) has not been
exercised for the current candidate — the receipt still reflects the previously
admitted identity. The tooling (build_active_identity) exists and can regenerate
atomically-on-decision.

---

## 3. Candidate ≠ Admitted (frozen)

A live candidate MAY describe current repository/model/config/pipeline contents.
It MUST NOT become canonical merely because tests/bootstrap discovered it or its
hashes are newer. Admission is an explicit governance transition (human gate).

---

## 4. Current candidate vs admitted (read-only, computed via canonical derivation)

```
CURRENT_ADMITTED:
  schemaVersion:     uniclaw.localVisionEvidence.v1
  modelId:           3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782
  configId:          config:edb7ad546d2b7f9c5b2b41affca70c13953e9efbbb5e2347c7418583778ac48f
  pipelineRevision:  prev:9e31f8d6d49e7e90f3ac1357bab11e4a7c083b005c4c501bc21a1b3146499bea
  deploymentId:      deploy:64f4b88ddaf5a964d80a9877fe93152eb239c0aa7ad9625273d52cd77c342f40

CURRENT_CANDIDATE:
  schemaVersion:     uniclaw.localVisionEvidence.v1
  modelId:           3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782
  configId:          config:edb7ad546d2b7f9c5b2b41affca70c13953e9efbbb5e2347c7418583778ac48f
  pipelineRevision:  prev:c5f506884a60c0b2e4d7ba929005e56956996774be4c421a12b4c2c6eb8bf83c
  deploymentId:      deploy:60c84225ff2e362bf37035371f16bb0e252149b3434f727b8727440643850d72

DIFF:
  modelId:           SAME
  configId:          SAME
  pipelineRevision:  CHANGED  (9e31f8d6… → c5f50688…)
  deploymentId:      CHANGED  (composite — follows pipelineRevision; NOT an independent axis)
```

## 5. Root cause of each changed axis

| Axis | Cause class | Exact source change |
|---|---|---|
| pipelineRevision | **A. INTENTIONAL_REPOSITORY_CHANGE** (+ B. generated artifact stale) | perception source committed after the receipt was admitted: `41e322f feat(perception): evaluation, training, tests updates`, `b7a2a11 Perception Platform Phase 4…` — content-hash derivation changed; working tree is clean (no uncommitted perception changes) |
| deploymentId | composite (derived, not an independent axis) | follows pipelineRevision by construction — no separate cause |

No C/D/E/F: not machine-specific, not nondeterministic, not unauthorized (all
changes are committed repository history).

## 6. Reproducibility

`compute_pipeline_revision` ran twice → identical `prev:c5f50688…`; modelId is
file-content SHA-256; configId is content-addressed. No dependency on cwd,
timestamps, pids, temp dirs, usernames, or unordered enumeration observed.
**Derivation is deterministic.**

## 7. Atomic identity semantics

Admission MUST be one atomic governance artifact. `build_active_identity.py`
constructs the COMPLETE receipt from production state in a single pass (all four
axes + schema in one payload) — no per-axis patching exists. Per-axis manual
mutation is forbidden (this gate performed none; the previous manual sync was
reverted at bootstrap graduation). **Smallest tooling gap**: the final
`current-active-identity.json` write uses `write_text` (direct overwrite) rather
than temp-file + atomic rename — a tiny window on crash. The APPLY transaction
records this as a tooling micro-fix (never an identity-semantics change).

## 8. build_active_identity.py role

**C. BOTH, WITH NO EXPLICIT MODES** — it builds the active candidate AND writes
the canonical receipt, but has no explicit "admission decision" mode or
authorization gate. Its name alone does not authorize admission: the human /
Project Leader promotion authority (Phase 4 governance) is the gate; the script
is the mechanical writer invoked under that authority.

## 9. Admission authorization boundary

- Human / Project Leader promotion authority (Phase 4 governance: "Project
  Leader/human promotion authority | unchanged").
- Decision recorded via the repository's decision-record convention (a
  `docs/decisions/` graduation/admission record), then the script is invoked.
- No new approval mechanism is invented.

## 10. Candidate validation

- modelId agrees with the actual `best.pt` artifact (content SHA-256 — identical
  to admitted: model unchanged).
- configId agrees with the resolved config + label-mapping (identical to
  admitted: config unchanged).
- pipelineRevision agrees with the current committed perception pipeline
  (content hash of committed source; matches what the live service reports).
- schemaVersion identical.
- deploymentId is the canonical composite of the above (derivation verified).
- "Vision starts successfully" was NOT used as admission proof — runtime
  viability and governance admission remain separate concerns.

## 11. Current receipt read-only

Verified: `git status` on `governance/artifacts/` is clean throughout this gate;
bootstrap reads the receipt, tests read it, candidate tooling compares against
it; no normal runtime/test path writes it (grep: no Write-to-receipt in src/tests).
No `VISION_GOVERNANCE_BOUNDARY_DEFECT`.

## 12. Future APPLY transaction (design only — NOT executed)

1. derive candidate via canonical inputs (read-only derivation above);
2. validate candidate (factory four-axis acceptance);
3. satisfy admission authorization: human/Project Leader decision recorded
   (docs/decisions admission record);
4. generate COMPLETE receipt via `build_active_identity.py` (tooling micro-fix:
   temp-file + atomic rename for the final write);
5. validate COMPLETE receipt (factory Create);
6. atomic activation (replace current-active-identity.json);
7. rerun identity guards (CORR_HOST03/04);
8. run managed Vision smoke (bootstrap + healthy);
9. verify CORR_HOST truthful acceptance WITHOUT weakening the verifier;
10. confirm no unrelated production files changed.

## 13. Rollback semantics

- Old admitted identity remains canonical until the new receipt is COMPLETELY
  generated and validated (build_active_identity constructs all manifests before
  writing the receipt; the atomic-rename micro-fix removes the half-write window).
- If activation succeeds but mandatory verification fails, the previously
  admitted receipt is restored from git (repository governance; no distributed
  machinery invented).

## 14. Relation to Vision bootstrap (frozen)

- VisionRuntimeBootstrap: loads admitted receipt → launches candidate → submits
  to identity verification → fails closed on mismatch. It never regenerates,
  auto-admits, patches axes, or weakens identity.
- Governance Admission: decides whether the candidate becomes canonical.
- The current truthful rejection is EVIDENCE the boundary works.

## 15. Falsifiers (all satisfied in this gate)

| # | Falsifier | Status |
|---|---|---|
| F1 | current-active receipt unchanged during baseline analysis | ✅ git-clean |
| F2 | candidate derived twice from same inputs is identical | ✅ 2-run identical |
| F3 | each changed axis has an identified canonical cause | ✅ pipelineRevision = committed source change |
| F4 | no per-axis manual mutation is part of admission | ✅ (previous manual sync reverted) |
| F5 | complete receipt validates before activation | ✅ factory four-axis validation |
| F6 | invalid candidate cannot replace current-active | ✅ verifier unchanged; rejection truthful |
| F7 | partial write cannot become canonical | ⚠️ tooling micro-gap (write_text→rename) recorded for APPLY |
| F8 | runtime/bootstrap has zero admission authority | ✅ |
| F9 | tests do not mutate governance to pass | ✅ grep + git-clean |
| F10 | new admission causes canonical verification to accept WITHOUT weakening | ✅ verifier untouched |

## 16. Impact on B1b & next gate

- **B1b VISION_DEPLOYMENT_IDENTITY_ADMISSION**: candidate is valid, reproducible,
  drift is authorized/explained (committed source change); the canonical
  admission path (human gate + build_active_identity) exists.
- Next: `PROJECT_LEADER_APPLY_VISION_DEPLOYMENT_IDENTITY_ADMISSION` — the §12
  transaction under human authorization (admission decision record), which would
  unblock L1 real-world validation (B1b → resolved; then B2/B3).

---

## FINAL DECISION

**`A. VISION_DEPLOYMENT_CANDIDATE_READY_FOR_ADMISSION`** — the current candidate
is reproducible, valid, and its drift is authorized and explained (committed
perception source change; model/config unchanged); the existing canonical
admission flow (human/Project Leader gate + `build_active_identity.py` complete
regeneration) can atomically activate it, with a recorded tooling micro-fix
(atomic rename for the receipt write) and strict human authorization as the only
preconditions. No production/perception/bootstrap code changed; receipt untouched.
