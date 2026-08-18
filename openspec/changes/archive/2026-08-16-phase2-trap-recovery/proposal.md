# Proposal: Phase 2 — Trap & Recovery

| 属性 | 内容 |
|------|------|
| Change ID | `phase2-trap-recovery` |
| 状态 | Proposed |
| 类型 | **Vertical Slice**（Charter §60-E: Recovery WiFi Scenario） |
| 日期 | 2026-08-08 |
| 分支 | `uni-agent`（架构框架分支） |
| 根 Change | `phase1-deterministic-runtime`（Phase 1 地基） |

## 动机

Phase 1（Deterministic Runtime）已完成并独立验收通过（104/104 测试、6/6 Architecture Guards、5/5 Active Scenarios、validator PASS）。

Phase 1 的架构限制直接产生 Phase 2 压力：

1. **I-8 只实现了 escalate 半句**：`TraversalStepResult.Failed(Reason)` 是单字符串结构化失败——Agent 收到后只能 `Fail()` 终止 Run。Charter I-8 的 recovery 半句（"低层 escalate，高层 recover"）完全未实现。
2. **I-9 全环缺失**：Recovery 的 act→observe→verify→reconcile→resume 闭环无任何代码路径。Phase 1 只有一条"失败 = 终止"的简单路径。
3. **RecoveryAnchor 有数据无消费者**：3 字段（ApplicationIdentity / ExpectedSemanticEntry / VerificationCriteria）仅在 SC-P1-001 断言 2 中验证非空——RestoreRecipe / EntryStrategy 仍属 Charter §20 预留字段（裁决 8），无任何组件消费。
4. **无漂移检测**：Agent 只在 step 失败或 Plan 耗尽时终止——世界漂移（Launcher / 其他 App / 未知页面）在当前代码中无法被检测为独立事件。
5. **无重试能力**：Traversal.Select 无匹配候选 → 直接 `Failed`，无 re-observe/re-resolve 分支。

Charter §39 Phase 2（Trap & Recovery）和 §60-E（"Recovery WiFi Scenario"）明确授权本 change。

## 目标（本 change 范围）

1. **Trap 一等模型**（Charter §21-22）：Trap（Kind / Scope / Source / Expected / Observed / Recoverability / Evidence / LastAction）——不可变值类型，作为 escalate 的结构化载体，与 `TraversalStepResult.Failed` 并列。**字段精确集合标记为 Human Gate HG-2，待审批**。
2. **Agent-scope Recovery**（Charter §35）：Launcher drift 检测 → Agent-scope Trap → 消费 RecoveryAnchor（裁决 8 解除：RestoreRecipe / EntryStrategy 字段落地）→ 恢复动作 → Observe → Verify（I-9）→ Reconcile → Resume → 继续 Plan → Complete。
3. **Step-scope retry**（Charter §22 Step Scope）：Traversal 在派发前 re-observe / re-resolve（有界、确定性、不升级 Agent Scope）。派发后 timeout 重试仍属 Phase 3（Charter §37 Uncertain Action）。
4. **Recovery verification 门**（I-9 负向）：恢复动作返回成功 ≠ 恢复完成——必须经过 Observation + VerificationCriteria 验证。验证失败 → Run Failed（显式原因），不得 Resume。
5. **3 Scenario contracts**（SC-P2-001 / SC-P2-002 / SC-P2-003）——全部 Scenario-first，共享同一 Runtime slice（延续 Phase 1 裁决 7 模式）。

## 非目标（Deferred，本 change 不解决）

| 能力 | 推迟到 | 原因 |
|------|--------|------|
| Popup recovery（Container-scope） | Phase 3（§38） | Charter §39 明确 Phase 3；Container 无局部恢复动作能力 |
| Uncertain action（timeout ≠ failed） | Phase 3（§37） | Charter §39 明确 Phase 3；派发后 timeout 重试归 Robust Execution |
| Scroll identity（FingerprintChanged ≠ ContainerChanged） | Phase 3（§36） | Fingerprint 字段与机制仍 DEFER（裁决 2） |
| Dynamic Grounding / local history | Phase 3（§39） | 无 Scenario 消费 |
| 真实设备 / Vision Adapter | Phase 4（§39） | Fake Environment 确定性模拟优先（§33） |
| FSM 引入 | Phase 3+（§17） | Recovery 协议先用普通方法表达（I-7：FSM 条件未满足） |
| Semantic Identity 算法 | Phase 5 | 语义页面解析仍用注入显式规则（design.md §8） |
| LLM / VLM / Memory | Phase 5 | §57-12：不依赖 LLM 完成确定性测试 |
| DI 容器 | 不引入 | 构造器注入 + 测试侧组合根 |

## 验收

- 3 Scenario 全部可确定性 Fake 运行（SC-P2-001 / SC-P2-002 / SC-P2-003）
- RecoveryAnchor 完整消费（RestoreRecipe / EntryStrategy 有消费者 + 有断言）
- I-9 闭环完整（act→observe→verify→reconcile→resume 全路径）
- Guard 5 修订（Trap 类型仅限 Model + Recovery 组件）+ 新 Guard 7（Recovery 依赖方向）
- Phase 1 全量回归（104 测试 + 确定性重放 + 6 Guards 不减弱）
- 失败路径覆盖：（a）恢复成功 → 继续 → Complete；（b）恢复验证失败 → Failed 而非盲续；（c）Step retry 不升级
- Human Gate HG-1..5 在 Phase 2A 启动前全部获批

## Human Gate 决策点（Phase 2A 启动前必须获批）

| ID | 决策 | 状态 |
|----|------|------|
| HG-1 | Guard 5 修订协议（no-Trap → Trap 仅限 Model + Recovery 组件） | **待审批** |
| HG-2 | Trap 字段精确集合（Kind / Scope / Expected / Observed 形状） | **待审批** |
| HG-3 | WorldBelief 是否需要 DriftStatus 字段（复用现有面 vs 新字段） | **待审批** |
| HG-4 | Recovery 状态 owner（Agent 直接持有 vs 独立 Recovery 组件） | **待审批** |
| HG-5 | Recovery 机制 scope（RecoveryRequest/Planner/Runtime 全量 vs 最小 RecoveryResult） | **待审批** |

## 不修改（含明确修订）

- Architecture Invariants（I-1..I-14 全部不变）
- Phase 1 Scenario semantics（SC-P1-001..004 行为断言 1-4 不回退——裁决 1）
- **SC-P1-004 断言 5（架构断言）明确修订**：Guard 5 从「Model 层不出现 Trap 类型」缩窄为「Trap 类型仅限 Model + Recovery 组件」（Guard 5 修订方案见 design.md §7 HG-1），并新增 Guard 7（Recovery 依赖方向）。此修订由 Phase 2 的 Trap 一等模型引入直接触发——行为面（SC-P1-004 断言 1-4）完全不受影响。裁决 1（Golden Contract 不回退）适用的是行为契约语义，架构 guard 可随 Phase 演进修订——Charter §41 明确该纪律。
- Phase 1 production code（在 Phase 2 实施前不修改 `src/UniClaw.Runtime/`）
