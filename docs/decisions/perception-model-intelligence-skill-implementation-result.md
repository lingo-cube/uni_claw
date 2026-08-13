# Perception Model Intelligence Skill — Implementation Result

> Date: 2026-08-13
> Role: Project Leader (GPT-5.6 Sol) / Bounded Human-Readable Derived View Skill Verifier
> Result: `PERCEPTION_MODEL_INTELLIGENCE_SKILL_IMPLEMENTATION_RESULT`
> Status: **VALIDATED**

---

## Result

```text
SkillPath:            .claude/skills/perception-model-intelligence/SKILL.md
SkillAuthority:       DERIVED_VIEW_ONLY
CanonicalTruthSources:
  governance/artifacts/current-active-identity.json
  training/artifacts/manifests/ (runs/candidates/model-artifacts/lineage/…)
  training/artifacts/model-store/<modelId>.pt
  evaluation/reports/ (runs/baselines/predictions)
  governance/artifacts/ (config-manifests/model-manifests/deployments/…)

HumanReports:
  CURRENT:       platforms/perception/reports/CURRENT.md
  TRAINING_RUNS: platforms/perception/reports/TRAINING_RUNS.md
  ARTIFACT_GUIDE: platforms/perception/reports/ARTIFACT_GUIDE.md

ArtifactClasses:
  KEEP_CANONICAL / KEEP_ARCHIVE_DIAGNOSTIC /
  DERIVED_DISPOSABLE / UNKNOWN_REVIEW_BEFORE_DELETE

CurrentActiveSummary:
  modelName android_ui_detection_yolov8 / YOLOV8 / DEKI_YOLO_RAW_V1 /
  LEGACY_PROVENANCE_PARTIAL / deploy:101f5ddc…

LatestTrainingRunSummary:
  trun:6f41b678… COMPLETED (process-closure mini run, 6 synthetic images,
  1 epoch, CPU, 48.2s; codeRevision d843557c, dirty=true; 1 FAILED
  attempt preserved)

LatestCandidateSummary:
  cand:c26b55fd… CANDIDATE_TEST_ONLY / mini_synthetic_box (YOLO11) /
  modelId 0f72dd1c… / linked EvaluationRun run:a90543cf…

ReleaseDecisionStatus:
  NOT_ESTABLISHED（无权威发布决策——不是“模型失败”）

CandidateVsActiveComparisonStatus:
  NOT_ESTABLISHED（EvaluationComparison 尚未实现）

StaleDetection:      PASS（DerivedFrom: SourceSnapshot 行，基于 canonical
                     ID 而非修改时间；live 校验三份报告全部 fresh）
MI01_MI18:           19/19 PASS
CanonicalArtifactsMutated: NO（34 个 canonical 文件组合哈希前后一致：
                     3d7ed28133db1816acbf8f810e591b3a）
RuntimeDependency:   NONE
SemanticDelta:       NONE
AuthorityDelta:      NONE
ReleaseAuthorityIntroduced: NO
ArchitectureGuards:  PASS（生产/评估/训练/治理代码不导入 helper；
                     helper 仅依赖 stdlib）
DiffCheck:           PASS
```

## First human acceptance proof (repository truth)

1. **当前 ACTIVE 是谁？** `android_ui_detection_yolov8`，ModelId
   `3f39b0d6…782`，deploymentId `deploy:101f5ddc…`，YOLOV8 /
   DEKI_YOLO_RAW_V1，provenance `LEGACY_PROVENANCE_PARTIAL`。
2. **最新 TrainingRun 是什么？** `trun:6f41b678…`（COMPLETED，迷你闭环
   训练）；另有一次 FAILED 尝试被诚实保留。
3. **最新 Candidate 是谁？** `cand:c26b55fd…`，`CANDIDATE_TEST_ONLY`，
   `mini_synthetic_box`（YOLO11 派生）。
4. **best.pt 当前对应什么语义？** 训练程序按验证标准选出的 checkpoint
   角色名——不是生产最优、不是 ACTIVE；ModelId（字节 SHA-256）才是内容
   身份；canonical 副本在 `model-store/0f72dd1c….pt`。
5. **最近训练目录哪些文件是 canonical / diagnostic / derived？**
   canonical：`manifests/*.json`、`model-store/<modelId>.pt`；
   diagnostic：`weights/*.pt`、`results.csv/png`、曲线图、混淆矩阵、
   `labels.jpg`、批次预览、`args.yaml`；derived：`mini-data/` 物化训练视图。
6. **当前是否存在 authoritative release decision？** 否 ——
   `NOT ESTABLISHED`。
7. **Candidate 是否已经被证明优于 ACTIVE？** 否 —— 不存在 canonical
   EvaluationComparison；训练指标差异不推导生产优劣。
8. **当前最重要的 evidence gap 是什么？** Holdout NOT_ESTABLISHED +
   数值发布阈值 NOT_FROZEN + EvaluationProfile/ReleasePolicy 未实现 +
   训练图表/指标全部仅诊断价值。

All eight answers derive from machine truth via the read-only helper — the
Skill itself was not involved in producing any of the underlying facts.

## What was built

```text
.claude/skills/perception-model-intelligence/SKILL.md
  — core semantic law, authority boundary, 6 operations, classification
    semantics, best.pt rule, file-deletion safety, DisplayVersion rule,
    future /explain-candidate-comparison stub (inactive)

platforms/perception/tools/model_intelligence/
  __init__.py
  mi.py                 read-only helper: canonical readers, SourceSnapshot,
                        artifact classifier, chart/metric Chinese explanations,
                        renderers, stale detection; ONLY write = 3 reports
  tests/test_mi.py      19 falsifiers (MI-01..MI-18 + classification extras)

platforms/perception/reports/
  CURRENT.md / TRAINING_RUNS.md / ARTIFACT_GUIDE.md   （真实数据 bootstrap）

docs/guides/README.md   追加人读报告与 Skill 指针（不复制工件指南全文）
```

## Dependency direction (verified)

```text
canonical machine system  X→  human-readable reporting
human reporting may depend READ-ONLY on machine truth
```

Runtime / Host / perception inference / training / evaluation / release
logic import nothing from the helper; the helper imports only stdlib.

## Recommended next task

```text
PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_4_CANDIDATE_VS_ACTIVE_COMPARISON_AND_EVALUATION_PROFILE_GATE

NO_AUTOMATIC_RELEASE_POLICY
NO_AUTOMATIC_PROMOTION
NO_AUTOMATIC_DEPLOYMENT
```

STOP.
