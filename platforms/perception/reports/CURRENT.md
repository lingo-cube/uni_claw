# UniClaw Perception 当前状态

> MACHINE MANIFESTS ARE TRUTH. HUMAN REPORTS EXPLAIN TRUTH; THEY NEVER CREATE TRUTH.
> TRAINING METRICS HAVE ZERO RELEASE AUTHORITY.

DerivedFrom: deploy:101f5ddccd2db3d179de5ed00205f45887442a3e74f443fcdda9f0beb88a71b8|cand:c26b55fd765d70c1787852759cc0ea2c685a6e984676e92c7754bb22401d0837|trun:6f41b678173f93ea41a587f99cd9d12be5884638d12724bfb18ce6123b2b94aa|run:a90543cfbb748a05a988298a63e01be82848e55dfe409b20d358ee1130cb724c

## 当前生产部署

- modelName: `android_ui_detection_yolov8`
- ModelId: `3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782`
- ConfigId: `config:edb7ad546d2b7f9c5b2b41affca70c13953e9efbbb5e2347c7418583778ac48f`
- PipelineRevision: `prev:55602ff1e7e0f34bdc58edc05216a12f54c762e3015bf6450afb88d3b10613e5`
- DeploymentId: `deploy:101f5ddccd2db3d179de5ed00205f45887442a3e74f443fcdda9f0beb88a71b8`
- provenance stance: `LEGACY_PROVENANCE_PARTIAL`

## 最新 Candidate

- identity: `cand:c26b55fd765d70c1787852759cc0ea2c685a6e984676e92c7754bb22401d0837`
- status: `CANDIDATE_TEST_ONLY`
- source TrainingRun: `trun:6f41b678173f93ea41a587f99cd9d12be5884638d12724bfb18ce6123b2b94aa`
- source DatasetVersion: `dataset:c7abafd051d2fb04b6725c800340c03609e6c0ce7f900e30cf70f3dbbc140894`
- Evaluation state: 已存在 EvaluationRun `run:a90543cfbb748a05a988298a63e01be82848e55dfe409b20d358ee1130cb724c`（不等于 VALIDATED——VALIDATED 语义属于未来的比较/发布层）
- Candidate-vs-ACTIVE comparison state: NOT ESTABLISHED（EvaluationComparison 尚未实现）

## 最近一次训练

- TrainingRun: `trun:6f41b678173f93ea41a587f99cd9d12be5884638d12724bfb18ce6123b2b94aa`
- status: `COMPLETED` / `completed`
- dataset: `dataset:c7abafd051d2fb04b6725c800340c03609e6c0ce7f900e30cf70f3dbbc140894`
- TrainingConfig: `tcfg:3b8c746cd4a5b30a6a893f2d31b812db566cee780feb3fe4d091e98b51c9f8be`
- codeRevision: `d843557c87456841369cefc46473d40d42997544` (dirty=True)
- checkpoint `best`: `sha256:0f72dd1cb7eb798dfc6aeba85076fac9b60631cd84ee1a0a61fdbe2ae08ef9c8`（checkpoint 名只是训练角色，不是模型身份）

## Evaluation 状态

- 现有 EvaluationRun 数量: 3
- EvidenceSufficiency（首个基线）: PARTIAL
- 已知未评估切片: OneUI/switch-state/holdout（见缺口）

## Release 状态

AUTHORITATIVE RELEASE DECISION: NOT ESTABLISHED

（不存在权威发布决策。这不是『模型失败』，只是尚未进入发布治理。）

## 当前已知缺口

- Holdout: NOT_ESTABLISHED
- 数值发布阈值: NOT_FROZEN
- EvaluationProfile: NOT_IMPLEMENTED
- ReleasePolicy: DEFERRED（架构已购买，实现推迟）
- Candidate-vs-ACTIVE 比较: NOT_IMPLEMENTED
- 存在 1 个 FAILED TrainingRun（诚实保留，未产生 ModelArtifact）
