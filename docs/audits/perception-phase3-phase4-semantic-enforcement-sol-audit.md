# Sol Audit — Perception Phase 3 / Phase 4 Semantic Constraint and Enforcement

> Contract: `SOL_PHASE_3_PHASE_4_SEMANTIC_CONSTRAINT_AND_ENFORCEMENT_AUDIT`  
> Mode: adversarial, read-only falsification audit  
> Repository commit: `d843557c87456841369cefc46473d40d42997544`  
> Verdict: **REPAIR_REQUIRED**

## 1. Executive decision

The Phase 3/4 architecture boundary remains sound: Perception produces evidence, Vision Host owns Python lifecycle mechanism, Evaluation measures, Training records lineage, and none has acquired Agent, action, goal-completion, Recovery, promotion, activation, or release authority. Runtime/Python isolation and the Agent→Container→Traversal→Environment invariants remain intact.

The implementation is **not semantically clean**. Twelve rules are bypassable and one Host rule is unproven in a real production composition. Nine S1 root gaps affect coordinate truth, content identity, scoring provenance, append-only persistence, leakage rejection, annotation acceptance authority, training execution provenance, Host identity composition, and EvaluationRun terminal truth. They require a repair gate before ReleasePolicy or L3; none currently requires architecture reopening.

Primary artifacts:

- [Rule enforcement matrix](perception-phase3-phase4-rule-enforcement-matrix.md)
- [Gap register](perception-phase3-phase4-gap-register.md)

## 2. Scope and method

The audit reloaded source, decisions, current dirty working tree, artifacts, tests, project references, constructors, writers, composition roots, environment inputs, and release-related names. Graduation summaries were treated as claims to falsify, not proof.

For each of P3-01..15 and P4-01..40 the audit:

1. reconstructed frozen intent from repository decisions and implementation;
2. identified production enforcement sites and alternate paths;
3. searched constructor, direct-record, optional guard, legacy, test-only, direct-write, metric, composition, environment, mutable-file, catch-all, and truth-default bypass classes;
4. ran current behavioral/negative suites and cheap probes;
5. classified enforcement E0–E4 separately from rule status;
6. correlated symptoms into shared root gaps and self-challenged every S1.

No production fix was made. The worktree already contained Phase 3/4 implementation and decision changes: 9 tracked modified files plus 16 untracked paths before audit artifacts. Those changes were preserved.

## 3. Enforcement coverage

| Coverage measure | Count |
|---|---:|
| Phase 3 rules | 15 |
| Phase 4 rules | 40 |
| Critical rules total | 55 |
| E4 — fail closed and falsified | 9 |
| E3 — runtime enforced | 18 |
| E2 — construction guarded | 18 |
| E1 — structural | 10 |
| E0 — document only | 0 |
| Bypassable | 12 |
| Unproven | 1 |
| Contradicted | 0 |

These counts measure enforcement strength, not model quality or architecture quality. Tests passing do not elevate a rule to E3/E4 unless the actual production path rejects the attempted violation.

## 4. Sol semantic adjudication

### 4.1 Observation versus truth

The core boundary is preserved. Detection presence and optional switch evidence enter `Observation`; Container/Agent retain belief and completion semantics. Invalid C# bounds are discarded rather than converted into usable coordinates. No perception output directly sets Goal completion.

Two adjacent gaps remain:

- P3-05 cannot represent the operational difference between a real empty result and HTTP/schema failure (`GAP-001`). Safety is fail-closed, diagnostics are not.
- P3-11 lets Python serialize out-of-range normalized bounds successfully (`GAP-002`). The downstream C# guard prevents unsafe use but does not make the service's evidence truthful.

### 4.2 Perception versus Agent authority

No authority theft was found. Python/adapter code has no semantic target selection, business capability choice, authorization, recovery, or Goal completion API. The evidence failures above are producer-contract gaps, not transfers of Agent authority.

### 4.3 Host mechanism versus semantic/release authority

`VisionServiceHost` owns process, socket, readiness, restart budget, version negotiation, and expected/observed comparison only. It does not select a semantic target or deployment based on task meaning and has no score/promotion/activation path.

However, `ForCanonicalProduction` is a guarded factory without an actual production composition root in this repository. Direct `VisionHostConfig` construction omits expected identity and makes verification a no-op. Current tests prove the mechanism but not that real composition must use it (`GAP-009`). This is enforcement/composition pressure, not a reason to move authority into Host.

### 4.4 Phase 3 provider architecture drift

No `ProviderRegistry`, generic provider manager, plugin lifecycle, capability registry, or speculative provider framework was found. `IPerceptionSource` remains a bounded adapter-private port. P3-15 is structurally effective.

### 4.5 Stage versus LabelSpace semantics

The two concepts remain separate types and explicit incompatibility yields `NOT_SCORABLE`, never model failure. Canonical baseline and candidate evaluation pass explicit fused stage/space.

The shared metric API is still bypassable: it accepts unbound candidate dictionaries, cannot compare Prediction asset identity to GT, and defaults omitted stage/space to fused values. Thus correct call-site behavior is not the same as non-bypassable evaluation semantics (`GAP-004`).

### 4.6 GroundTruth versus Annotation versus Prediction authority

The artifacts and packages are separate, and Prediction never automatically becomes GroundTruth. Annotation acceptance is weaker: `MODEL_ASSISTED + ACCEPTED` can be directly constructed or passed to the factory; DatasetVersion accepts its ID without validating an acceptance event. Therefore authority separation is structurally present but explicit review authority is bypassable (`GAP-007`).

### 4.7 EvidenceSufficiency semantics

EvidenceSufficiency remains distinct from quality scores and coverage. Tiny-corpus success stays PARTIAL; missing GT is `NOT_SCORABLE`; zero applicable assets are `UNASSESSED`; historical unresolved count expectations remain diagnostic-only. No single summary drives authority.

### 4.8 Training metric authority

Training loss/mAP/precision/recall are recorded but do not promote or activate anything. Candidate evaluation reuses the canonical Evaluation workflow. Training metric authority is therefore absent as frozen. The adjacent provenance problem is different: actual `model.train` arguments are not mechanically tied to the recorded TrainingConfig (`GAP-008`).

### 4.9 Release unit semantics

Canonical release identity is `PerceptionDeploymentIdentity`, not ModelArtifact, checkpoint name, ModelName, or TrainingRun. Candidate artifacts bind deployment identity and no ReleasePolicy/promotion/activation/rollback lifecycle exists. This is structural because no release authority is implemented; it is not falsely classified as an active E4 release denial.

### 4.10 DeploymentIdentity correctness

The identity formula correctly owns SchemaVersion, ModelId, ConfigId, and PipelineRevision; ServiceVersion is metadata. Startup snapshot semantics prevent ordinary post-load disk mutation from changing canonical loaded identity, and evaluation checks claimed model/config/pipeline identity before inference.

Remaining limitations:

- the guarded Host path is not production-composed (`GAP-009`);
- EvaluationRun is created and persisted as `COMPLETED` before identity-checked asset execution, and stays completed after infrastructure mismatches (`GAP-010`);
- the public metric API can detach scores from Prediction asset/stage provenance (`GAP-004`).

### 4.11 Optional guards

Several guards are optional enough to be semantically ineffective:

- `ExpectedIdentity` is nullable outside the canonical factory;
- metric stage/space arguments default rather than require provenance;
- leakage checking is an optional function whose findings do not block training;
- annotation factory accepts caller-supplied `ACCEPTED`;
- baseline has an explicit overwrite escape hatch, and other writers overwrite unconditionally.

They are recorded as bypasses, not counted as enforcement merely because the intended helper exists.

### 4.12 Backward compatibility and authority bypass

Legacy configHash remains compatibility-only and does not replace ConfigId. Historical count expectations remain diagnostic. Legacy Host configuration deliberately omits expected identity; because no real canonical composition root exists, that compatibility seam is a present composition bypass (`GAP-009`). No legacy path gained release or Agent authority.

### 4.13 Subtle frozen-intent change

Phase 3's simple pipeline order is still true but incomplete. Raw-label normalization, unmatched OCR promotion/reclassification, scroll-hint/metadata production, and evaluation stage-view capture are behaviorally meaningful transformations omitted from the frozen prose (`GAP-011`). They do not change authority or Runtime contracts. Stale legacy launch text remains in source/help (`GAP-012`).

## 5. Phase 3 alignment

| Dimension | Verdict | Evidence / gap |
|---|---|---|
| Semantic alignment | PARTIAL | Pipeline detail drift (`GAP-011`); coordinate contract bypass (`GAP-002`) |
| Authority alignment | PASS | Perception and Host have no semantic/action/release authority |
| Runtime boundary | PASS | Runtime has no Python, model, config, or Host dependency |
| Host boundary | PARTIAL | Sole lifecycle owner and bounded mechanism; production identity composition unproven (`GAP-009`) |
| Fail closed | PARTIAL | No fabricated candidates, but no-elements and infrastructure failure collapse (`GAP-001`); service may return invalid bounds (`GAP-002`) |
| Identity semantics | PASS | Full model byte hash; configHash remains partial and ConfigId distinct |
| Migration closure | PARTIAL | No active legacy implementation; stale documentation/help (`GAP-012`) |

## 6. Phase 4 alignment

| Dimension | Verdict | Evidence / gap |
|---|---|---|
| Evaluation semantics | PARTIAL | Correct canonical workflow, but asset/load, metric provenance and run-status gaps (`GAP-003`, `GAP-004`, `GAP-010`) |
| Stage/LabelSpace | PARTIAL | Explicit mismatch guarded; omissions/defaults bypassable (`GAP-004`) |
| Evidence semantics | PARTIAL | Sufficiency/coverage sound; append-only persistence and terminal truth weak (`GAP-005`, `GAP-010`) |
| Training semantics | PARTIAL | Metrics have no authority; config/execution congruence absent (`GAP-008`) |
| Dataset/annotation boundary | PARTIAL | Membership separate; leakage and acceptance enforcement bypassable (`GAP-006`, `GAP-007`) |
| Deployment identity | PARTIAL | Identity formula/snapshot sound; real Host composition unproven (`GAP-009`) |
| Release authority | PASS | No promotion/activation/rollback/automatic retraining/model-registry authority |

## 7. Phase 3 → Phase 4 drift

- **No authority drift:** Host identity comparison did not become deployment selection; stage views did not enter Runtime truth; Evaluation/Training did not acquire release authority.
- **No Runtime dependency drift:** Runtime remains isolated from Python/evaluation/training/governance.
- **Identity evolution preserved:** Phase 3 configHash remains partial; Phase 4 ConfigId is a distinct canonical axis.
- **Semantic description drift:** The pipeline description does not fully name internal normalization/promotion/capture behavior (`GAP-011`).
- **Composition pressure:** A canonical identity-verified Host factory exists, but real production use is not established (`GAP-009`).

## 8. Delta audit

| Claimed delta | Audit result | Explanation |
|---|---|---|
| RuntimeDelta = NONE | TRUE_NONE | Phase 4 code does not change Agent/Container/Traversal/IEnvironment semantic core |
| SemanticDelta = NONE | TRUE_NONE_WITH_DOCUMENTATION_DRIFT | No undeclared domain/authority purchase; pipeline description needs reconciliation |
| AuthorityDelta = NONE | TRUE_NONE | No action, goal, recovery, release, promotion, or activation authority added |

The S1 gaps are enforcement defects within already-owned boundaries. Repair does not presently require new ownership, reversed dependencies, or architecture invariants.

## 9. Test evidence quality and execution

| Suite | Current result | Audit interpretation |
|---|---:|---|
| Full .NET regression | 862/862 PASS | Broad behavioral regression; not proof of Python governance invariants |
| Focused Vision Host | 38/38 PASS | Behavioral lifecycle/fixture proof |
| Vision + Golden + Architecture | 60/60 PASS | Combined regression/structural evidence |
| Identity round-trip/mismatch | 5/5 PASS | Guard mechanism proof; test composition only |
| Golden replay | 4/4 PASS | Runtime replay evidence, not fresh vision accuracy |
| Python perception | 15/15 PASS | Normal pipeline/equivalence behavior |
| Evaluation | 69/69 PASS | Canonical workflow and negative stage/coverage tests |
| Training | 33/33 PASS | Intended lineage behavior and checker-level falsifiers |
| Governance EXI/identity | 37/37 PASS | Identity calculations and mismatch guards |
| Consistency C1–C10 | PASS | Documentation/architecture consistency |
| `git diff --check` | PASS | Patch hygiene only |
| Governance runtime-snapshot/restart | NOT_EXECUTABLE to terminal result | Process stopped/hung after socket `ResourceWarning`; no fresh count available |

Strongest proofs include real negative identity mismatch, schema failure, stage/label mismatch, missing GT, zero coverage, fresh inference, and Runtime architecture guards. Weaker/self-referential areas are immutable writers, asset manifest load identity, metric asset binding, leakage admission, accepted-annotation construction, training invocation congruence, and real Host composition.

Real `/version` loaded-identity round-trip is **not claimed as freshly complete in this audit**. Prior graduation artifacts contain RSI claims, but the current runtime-snapshot/restart execution did not return a terminal result; current successful 5/5 tests establish the verification seam, not a production application composition root.

## 10. False-positive control

Every S1 was challenged against alternate guards, construction restrictions, type invariants, startup checks, tests, and absence of release authority:

- Downstream invalid-bounds rejection reduced coordinate severity from S0 to S1 but did not repair Python's successful output.
- Canonical callers bind metrics correctly, but the shared API permits direct mismatched use.
- Frozen dataclasses/content IDs prevent accidental mutation, but writers and files remain overwriteable.
- Leakage checker and acceptance helper exist, but admission/execution does not make them mandatory.
- Host mismatch logic is sound, but no production composition restricts callers to it.
- Evaluation infrastructure errors prevent scoring, but the persisted run still claims COMPLETED.
- No candidate can become ACTIVE and no release authority exists; therefore none of these gaps is S0.

Result: retained S1 findings are material; no architecture-reopen evidence was found.

## 11. Final disposition

```text
Status: REPAIR_REQUIRED
ArchitectureReopenRequired: NO
RepairRequired: YES

RepairOrder:
  1. GAP-002, GAP-003, GAP-004, GAP-010
  2. GAP-005
  3. GAP-006, GAP-007, GAP-008
  4. GAP-001, GAP-009
  5. GAP-011, GAP-012

RecommendedNextTask:
  PROJECT_LEADER_PERCEPTION_PHASE3_PHASE4_SEMANTIC_AUDIT_REPAIR_GATE
```
