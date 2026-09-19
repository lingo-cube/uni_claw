# CORE-015 — Core 抽取 × UI World realization 责任对齐与拆分设计

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree
triage_label: done
parent_change: CORE-013（执行源线收口）· 谱系：CORE-002..008（Core 提取线）
goal_framing: >
  Core 提取为顶层抽象契约；UI World 相关模型作为第一个按该契约实现的
  realization（Human 2026-09-19 定格）。方向 = Core 契约约束 UI 必须保留
  的语义 + UI 自持 identity/观察算法；不是 Core→UI 反向投影。
  裁决二框架（Human 2026-09-19）：**Core 管核心世界模型抽象；UI 管
  相关领域实现细节补充。**

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
4. **调整自由度（Human 2026-09-19）**：UI 模型可以基于 Core 模型做
   调整——设计期向 Core 对齐的类型形状/命名/字段重构在契约范围内；
   契约约束是语义边界（Core 不反向生成 UI identity），不是 UI 现状
   冻结。已写入 realization 契约 §6 与对齐表 Step 4。

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
2026-09-19 · step1-approved · 用户裁决二通过：认可「三个投影缺口均非
Core 对象缺口」「主要缺口 = 缺独立 realization 契约」两个结论，并定格
框架「Core 管核心世界模型抽象；UI 管相关领域实现细节补充」。进入
Step 2：起草 `docs/design/uiworld-realization-contract-v0.1.md`。
2026-09-19 · framing-supplement · 用户补充裁决：UI 模型可以基于 Core
模型做调整（设计期向 Core 对齐的结构重构在范围内；语义边界不变）。
已落契约 §6 调整自由度块、对齐表 Step 4、本 state Decisions.4。
2026-09-19 · step2-approved-step3-done · 用户裁决「通过，继续」。Step 3
双向验证建立（不迁移、零产品代码）：7 场景 × 两方向矩阵落
`evidence/2026-09-19-core-015-step3-bidirectional-validation.md`；补齐
仅有的两个真实缺口（场景 7 Core 固定依据不变量、场景 4 连续性×投影
不泄漏），其余场景由既有测试族覆盖（矩阵逐条引用）。569/569 全绿、
567 基线零回归。等待 Step 4（物理拆分三选项 + 调整自由度）裁决 Gate。
2026-09-19 · step4-executed · closed · 用户裁决选项 3（模块级两步走：
拆 UI 专属类型 + 边界执法测试；程序集升格推迟到第二个 realization
buyer）。五文件纯搬移至 `World/UiRealization/`（零形状改动）+
UiRealizationBoundaryTests 执法（出向依赖白名单）。570/570 全绿。
CORE-015 四步路线完成关闭；遗留可选项：UI 类型向 Core 形状对齐
（调整自由度未用）、程序集升格（条件已记录）。
2026-09-19 · learning-hook · ADR-0024（模块边界 + 执法 + 升格推迟）+
CONTEXT 词条「UI World Realization」落档；A8 触发条件（新术语定型：
模块进产品代码后零覆盖）闭合。
