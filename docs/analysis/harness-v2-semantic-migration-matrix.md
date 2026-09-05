# Harness V2 — Old-Flow Semantic Migration Matrix

> DocumentType: `HARNESS_MIGRATION_ANALYSIS`
> Status: `COMPLETE`
> Authority: `NONE`
> Scope: 旧 Development Flow（uni-agent 谱系 V1：development-protocol.md
> Two-Lane/Hard Gates/H4 契约等）的**控制语义**向 V2 的迁移裁决。
> 原则：**Migrate control semantics, not historical governance structure.**
> 禁止边界: 本文件不迁移 Product Runtime / OpenSpec lifecycle / Product
> Architecture authority model（见 REMOVE 行）。

分类定义（directive §13）：

| 类别 | 语义 |
|---|---|
| MIGRATE_SEMANTIC | 控制语义迁入 UniFlow Gate / 结构 |
| REPLACE_WITH_SKILL | 由上游工程 Skill 承担（UniFlow 组合使用） |
| KEEP_PROJECT_SPECIFIC | 项目特有能力，以本地扩展保留（Phase 15） |
| REMOVE | 不进入 V2（历史重量 / 产品侧 / 已废弃） |
| ARCHIVE | 降级为 legacy record，可检索不参与流程 |

## 迁移矩阵

| 旧流程语义 | V1 来源 | V2 去向 | 类别 |
|---|---|---|---|
| Intent clarity（Reality Preflight：用户可见目标/当前状态/偏差） | workflow §4 | Gate 1 checks 1/5/6 | MIGRATE_SEMANTIC |
| Task type / risk 分级（L0-L4） | development-protocol §17.1 | Gate 1 "Task Type known" + Direct/Delegate 风险考量 | MIGRATE_SEMANTIC |
| Scope / write 边界（scope.write + 越界上报） | task-contract / workflow §6 | Gate 2 "Scope executable" + Gate 3 "Diff within authorized scope" | MIGRATE_SEMANTIC |
| Known / Unknown / Assumption 显式化 | Reality Preflight / agent-message-contract | Gate 1 checks 5-7 | MIGRATE_SEMANTIC |
| Owner / Authority clarity | development-protocol §5 invariants | Gate 1 #8 + Gate 2 #2 + uniflow §6（只检查不发明） | MIGRATE_SEMANTIC |
| Acceptance 先行 | WorkItem acceptance 字段 | Gate 1 "Acceptance expressible" + Gate 3 #1 | MIGRATE_SEMANTIC |
| Human Decision（七类 material boundary） | development-protocol §7 | Human Gate 单列表（uniflow §8） | MIGRATE_SEMANTIC |
| Evidence 证明完成（Worker 自述≠完成） | result-contract | Gate 3 / Gate 4 分离 + uniflow §11 | MIGRATE_SEMANTIC |
| Review / Verify / Complete 条件分离 | change-review + result-contract 状态机 | Gate 3 vs Gate 4 + Review≠Complete 规则 | MIGRATE_SEMANTIC |
| Fail-closed 行为（BLOCKED_* / 不可静默降级） | workflow §6 / model-routing policy | uniflow §12 Fail-Closed 总表 + 结果 token | MIGRATE_SEMANTIC |
| Two-Lane（Semantic Discovery / Fast Lane） | development-protocol | 压缩为 Pre-UniFlow Explore vs UniFlow 执行边界 | MIGRATE_SEMANTIC |
| 六类 Hard Gate（HG-SEMANTIC/ARCH/SAFETY/HUMAN/VALID/SCOPE） | development-protocol | 压缩进 Human Gate 触发条件 + Fail-Closed | MIGRATE_SEMANTIC |
| H4-1/2/3 契约（leader-decision / auto-continue / scenario-trigger） | .ai/*-contract.md | 四 Gate + Route 语义取代状态机 | MIGRATE_SEMANTIC |
| Change classification（Small/Medium/Large ceremony 表） | change-classification.md | Plan 持久化规则 + Direct/Delegate 判据（无强制 ceremony） | MIGRATE_SEMANTIC |
| Frozen decisions / Phase Boundary | development-protocol §6/§15 | WorkItem `frozen_decisions` + Gate 2 检查（项目经 ADR 声明） | MIGRATE_SEMANTIC |
| Tracer-bullet 垂直拆分 | （首批 directive 吸收） | uniflow §8 Delegate 拆分原则 | MIGRATE_SEMANTIC |
| 证据驱动 7 步 Worker 流 / Review 四象限 | development-protocol §17 | `diagnosing-bugs` / `code-review`（上游） | REPLACE_WITH_SKILL |
| 探索式质询 / 领域建模 / 结构设计 / TDD 循环 | .ai/skills 自有版 | `grilling`/`grill-with-docs` / `domain-modeling` / `codebase-design` / `tdd`（上游原文） | REPLACE_WITH_SKILL |
| E0-E4 证据分级 | evidence-driven-debugging | 本地调试扩展（组合进 diagnosing-bugs 使用） | KEEP_PROJECT_SPECIFIC |
| FDP / Owner / Root Cause 纪律 + 失败分类学 | evidence-driven / runtime-behavior-debugging | 同上（Trace/Evidence extension） | KEEP_PROJECT_SPECIFIC |
| Scenario Receipt / L1-L4 资产分级 | development-protocol §3/§17 | 产品线保留；harness 不迁移 | REMOVE |
| Product Runtime semantics（Container/Traversal/WorldBelief/GoalEvidence 权威） | 宪章 / Contract | 产品侧（docs/analysis 基线仅参考） | REMOVE |
| OpenSpec lifecycle（propose/apply/verify/archive） | development-protocol §4 | 有效语义提取入 ADR/Acceptance/Verification 后归档 | ARCHIVE |
| model-routing provider 绑定细节 | model-routing.yaml v5 | 本阶段不动（Stop Boundary → UNIFLOW_OPTIMIZATION） | KEEP_PROJECT_SPECIFIC |

## 验收对照（directive §15）

四 Gate + 结果 token（EXPLORE_RESOLVED / STAY_IN_EXPLORE /
STAY_IN_DIAGNOSIS / EXECUTION_READY / VERIFIED / VERIFICATION_FAILED /
COMPLETE）已在 `uniflow.md` 落地；Direct 无 WorkItem、Delegate 仅临时契约、
Human Decision fail-closed、产品语义与 OpenSpec lifecycle 均在
harness 之外——`FLOW_V2_READY` 的结构性条件成立（运行时 conformance 测试
属后续阶段）。
