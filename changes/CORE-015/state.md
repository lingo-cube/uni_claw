# CORE-015 — Core 抽取 × UI World realization 责任对齐与拆分设计

lifecycle_state: in_progress · disposition: none · depth: decision-heavy · base: working-tree
triage_label: step1-alignment-done
parent_change: CORE-013（执行源线收口）· 谱系：CORE-002..008（Core 提取线）
goal_framing: >
  Core 提取为顶层抽象契约；UI World 相关模型作为第一个按该契约实现的
  realization（Human 2026-09-19 定格）。方向 = Core 契约约束 UI 必须保留
  的语义 + UI 自持 identity/观察算法；不是 Core→UI 反向投影。

## Intent

按四步路线推进 UI World 的 Core 契约化（不先搬类）：
Step 1 文档与现状对齐（本轮）→ Step 2 UI World realization 契约草案 →
Step 3 双向验证设计 → Step 4 物理拆分决策。每步独立 Gate。

## Scope

- Step 1（已完成）：只读对齐表
  `docs/design/core-extraction-uiworld-alignment-v0.1.md`——目标分类、
  Core 契约面现状、WorldModel 能力清单、projection 覆盖矩阵、缺口归属
  分类、文档漂移清单；同步修正 qspec v0.2 §7 与 docs/design/README.md
  索引（含 5 篇漏登）。零代码改动。

## Out of Scope（本 Change 全程禁止，除非另行授权）

- 修改 `src/`（任何搬类/拆分/新 assembly）；冻结程序集名、继承树、
  最终类签名；Core → UI 反向投影；把 WorldModel 原样搬成 UI Core。
- Step 2-4 各自在评审通过后才进入下一步；Step 4 拆分实现需单独授权。

## Decisions

1. 本轮核verified 关键事实：三个常被当作 Core 缺口的投影缺口
   （Attempt 四字段 / BasisReferences / Claim 谓词）在 Core 候选类型
   字段已全部备好（CORE-006）——不存在需立即新增的 Core 对象。
2. Attempt 未填字段的数据源已具备：CORE-013 执行源
   ExecutionRegistration + journal 状态（三轴映射语义留给 Step 2/3，
   不在本 Change 冻结）。
3. `ProjectTargetBinding` 的 Slice-basis 必填对 UI realization 语义
   正确；BasisReferences 缺口归属未来非 UI realization 的投影义务。

## Verification

```yaml
level: CONTRACT
method: 只读对齐（源码/文档逐项核对：CoreRecords.cs / CoreSemanticProjection.cs /
        WorldModel 族 / qspec v0.2 / docs README）
expected: 对齐表与漂移清单的每一条均经本轮直接核verified，无凭记忆条目
actual: >
  完成——3 个投影缺口、Core 字段已备事实、qspec 漂移、README 错标行与
  5 篇漏登全部实证核对并修正；零代码改动
evidence: docs/design/core-extraction-uiworld-alignment-v0.1.md
```

## Status log

2026-09-19 · created · Human 只读审计（本轮上游输入）+ 入口协议复验后
执行 Step 1：对齐表成文、两处漂移修正、README 索引补全。等待 Step 1
评审；Step 2（realization 契约草案）为下一个 Gate。
