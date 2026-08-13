---
name: perception-model-intelligence
description: "解释 UniClaw 感知/ML 平台的机器真理给人看：当前生产模型、最新 Candidate、TrainingRun、训练工件分类、图表解读、生成人读报告。只读推导层，零发布/激活权威。"
---

# perception-model-intelligence

> ## MACHINE MANIFESTS ARE TRUTH.
> ## HUMAN REPORTS EXPLAIN TRUTH; THEY NEVER CREATE TRUTH.
>
> ## TRAINING METRICS HAVE ZERO RELEASE AUTHORITY.

本 Skill 是**解释与推导报告层**。它只读 canonical 机器真理，产出人读视图。
它**没有**模型 / 发布 / 激活权威。

## 铁律：永远不要从这些推导权威状态

禁止从以下任何一项推断 `ACTIVE` / `PROMOTED` / `VALIDATED` /
`RELEASE_READY` / `PRODUCTION_READY`：

```
best.pt  last.pt  training mAP  precision  recall  loss
results.csv  results.png  PR_curve.png  F1_curve.png  P_curve.png
R_curve.png  confusion_matrix.png  文件名  目录名  "latest"
时间戳  训练完成本身
```

**发布就绪状态只能是 `UNKNOWN / NOT_ESTABLISHED`，除非存在权威
ReleaseDecision。** 没有 ReleaseDecision 时只允许说：

> AUTHORITATIVE RELEASE DECISION: NOT ESTABLISHED

禁止把它翻译成「模型失败了」。

## Canonical 真理来源（只读）

在仓库中按需发现，不要假设文件名。当前 canonical 位置：

| 真理 | 位置 |
|---|---|
| 当前 ACTIVE 身份 | `platforms/perception/governance/artifacts/current-active-identity.json` |
| TrainingRun / Candidate / DatasetVersion / TrainingConfig / lineage | `platforms/perception/training/artifacts/manifests/` |
| ModelArtifact 字节 | `platforms/perception/training/artifacts/model-store/<modelId>.pt` |
| EvaluationRun / Baseline / Prediction | `platforms/perception/evaluation/reports/` |
| ConfigManifest / ModelManifest / Deployment | `platforms/perception/governance/artifacts/` |
| 训练框架诊断产物（图表/csv） | `platforms/perception/training/artifacts/runs/ultralytics/…/` |

## 权威边界

**只允许：** 读 canonical 真理；解释诊断产物；生成三份人读报告；
把人类导航到 canonical 文件；解释缺失的证据；检测报告过期。

**绝不允许：** 创建/修改 ModelId、ConfigId、DeploymentId；
修改 DatasetVersion / Annotation / GroundTruth / TrainingRun；
改写历史 EvaluationRun；创建 Candidate 状态；决定 promotion /
ACTIVE / rollback / ReleasePolicy；把 LEGACY_PROVENANCE_PARTIAL
重新解释成完整；把 PARTIAL 升级为 COMPLETE；补造 lineage / GT；
从训练指标推断发布资格；自动删除文件。

## 六大操作

### 1. 感知状态总览（/perception-status）

读取 canonical 真理，输出中文摘要：

- 当前生产部署：modelName / ModelId（短显示 + 完整引用）/ ConfigId /
  PipelineRevision / DeploymentId / provenance stance
- 最新 Candidate：identity / status / 来源 TrainingRun / DatasetVersion /
  Evaluation 状态 / 与 ACTIVE 的比较状态
- 最近一次 TrainingRun 摘要
- Evaluation 状态与 EvidenceSufficiency
- Release 状态：无权威 ReleaseDecision 时只写
  `AUTHORITATIVE RELEASE DECISION: NOT ESTABLISHED`
- 已知缺口（只列 canonical 证据支撑的缺口）

### 2. 解释一次训练（/explain-training-run <run>）

读 TrainingRun / TrainingConfig / DatasetVersion / Checkpoint /
ModelArtifact / Candidate / 关联 EvaluationRun，解释：

- 训练目的、Dataset、train/validation 成员、参数、状态（FAILED 如实说）
- 生成了哪些 checkpoint、**best 的真正含义**（训练程序按验证标准选出的
  角色名，不是生产最优）
- 是否产生 ModelArtifact / Candidate / Evaluation
- 哪些只是训练诊断、哪里有 lineage / provenance 缺口

全部用 canonical ID 作为证据。

### 3. 解释训练工件目录（/explain-training-artifacts <dir>）

对每个文件归入**且只归入**一类：

| 类别 | 语义 |
|---|---|
| `KEEP — CANONICAL` | 机器真理 / 不可变 lineage / 身份工件 |
| `KEEP / ARCHIVE — DIAGNOSTIC` | 调试有用、无语义或发布权威 |
| `DERIVED / DISPOSABLE` | 可从 canonical 源重新生成 |
| `UNKNOWN — REVIEW BEFORE DELETE` | 无法证明归属——**永不建议删除** |

对重要文件给出：是什么 / 什么时候看 / 有无 Authority / 是否可重新生成 /
删除风险。

> 注意：一个 `.pt` 不因存在就 canonical——看它是内容寻址的
> ModelArtifact 还是普通框架输出。

### 4. 解释训练图表（/explain-training-chart <file>）

支持 results.png、PR/F1/P/R_curve.png、confusion_matrix*.png、
labels.jpg、labels_correlogram.jpg、train_batch*.jpg、val_batch*_*.jpg。

两层解释：
- **ML 含义**：这张图是什么、该看什么、什么模式可疑
- **UniClaw 后果**：对感知可能意味着什么（如 switch↔icon 混淆对
  SwitchStateReader 的压力），并明确：
  > 训练图表 ≠ Runtime 失败证明；训练图表 ≠ 发布证据。
  且指出还需要什么 Evaluation / Scenario 证据。

指标用大白话：Precision =「模型判断为某类时有多少是真的」；
Recall =「真实元素里找回了多少」——并立刻声明「只用于训练诊断，
不直接决定是否上线」。

### 5. 刷新人读报告（/update-perception-human-report）

只重新生成这三份（不新增报告文件）：

```
platforms/perception/reports/CURRENT.md        ← 当前状态
platforms/perception/reports/TRAINING_RUNS.md  ← 训练历史
platforms/perception/reports/ARTIFACT_GUIDE.md ← 工件指南
```

调用只读 helper：

```bash
PYTHONPATH=platforms/perception python3 -m tools.model_intelligence.mi
```

每份报告头部记录 `DerivedFrom: <SourceSnapshot>`。SourceSnapshot 由
canonical ID 组成（currentDeploymentId | latestCandidateId |
latestCompletedRunId | candidateEvalRunId），**不是**修改时间。当前
canonical ID 与报告 DerivedFrom 不一致 → 报告过期，重新生成即可。

报告是**可变推导视图**（非语义历史），允许覆盖；canonical 源文件
必须保持只读。

### 6. 对比训练运行（/compare-perception-runs A B）

只对比 Dataset / TrainingConfig / 训练诊断 / checkpoint 结果 /
provenance / 评估可得性。必须显著打印：

> **TRAINING-RUN COMPARISON IS NOT RELEASE COMPARISON.**

允许：「Run B 的验证 mAP 高于 Run A。」禁止：「Run B 更好，应该替换
ACTIVE。」不存在 canonical EvaluationComparison 时绝不推断生产优劣。

## 缺失真理的措辞

缺失就说缺失：`UNKNOWN` / `NOT_ESTABLISHED` / `PARTIAL` /
`LEGACY_PROVENANCE_PARTIAL` / `NOT_IMPLEMENTED`。绝不推断。

## 未来能力（预备，不实现）

`/explain-candidate-comparison`：仅当 canonical `EvaluationComparison`
存在时激活——解释 ACTIVE vs Candidate 的分片改进/回退/未评估/
不可比/覆盖率/充分性/Profile 适用性。比较权威不在本 Skill 内。

## DisplayVersion 规则

报告可用 `Candidate — 2026-08-13 — TrainingRun 3` 这类展示名。
DisplayVersion 是 UI 专用，**绝不参与** modelId/configId/
pipelineRevision/deploymentId/发布决策/工件身份。绝不编造
「Model V1/V2/V3」作为 canonical 身份。

## 删除安全

建议删除前必须：确认文件是否被任何 canonical manifest/lineage 引用；
确认字节是否是 canonical ModelArtifact；确认能否从 canonical 源
重新生成；不确定 → `UNKNOWN — REVIEW BEFORE DELETE`。
本 Skill 从不自动删除文件。

## 测试

Falsifier 测试在 `platforms/perception/tools/model_intelligence/tests/`
（MI-01..MI-18）。修改本 Skill 或 helper 后必须全绿：

```bash
PYTHONPATH=platforms/perception python3 -m pytest platforms/perception/tools/model_intelligence/tests/ -q
```

## 依赖方向（架构守卫）

```
canonical 机器系统（Runtime / Host / inference / training / evaluation / release）
   X→
人读报告（本 Skill + helper）
```

Runtime / Host / 感知推理 / 训练 / 评估 / 发布逻辑**不得**导入或引用
本 Skill 的 helper。人读报告只允许只读依赖机器真理。
