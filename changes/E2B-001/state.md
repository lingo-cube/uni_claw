# E2B-001 — Evidence-to-Belief Vertical Slice

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 7ec44b7a

## Intent

**WHAT**: 在真实代码中实现 Target Architecture 的认识论核心闭环——Evidence
admission → Belief relevance 判定 → Reconciliation → WorldBelief revision →
Slice 派生——证明 Admission ≠ Truth、Evidence ≠ Belief、Reconciliation 是
唯一的 belief 更新路径。

**WHY**: 这是 Target Architecture（v0.1，ADR-0007 采纳）的最短可验证语义
链。后续一切能力（Control、Assurance、Effect）都建立在它之上。

## Scope

- Evidence Ledger：admission path（record integrity / provenance / admission
  record / canonical Evidence Record）
- World Model：Belief Relevance 判定 → Reconciliation → WorldBelief
  revision（含 Evidence Basis / Freshness / Uncertainty / Conflicts）
- Slice 派生（从 WorldBelief revision 的只读投影）
- 最小 Uni Kernel 边界（仅组合上述两个 L2，无其他）

## Out of Scope

- Control Loop、Assurance、Effect Boundary、Memory System
- Capability Plane 框架化（测试用 scripted Provider 即可）
- FSM、存储/部署、多 Goal/多 Run
- 一切 legacy 迁移（GREENFIELD，不依赖 Mapping/Migration Baseline）
- 产品代码落点分支的最终裁决（按 ADR-0007，进入实现时决定）

## Decisions

| # | 决策 | 来源 |
|---|---|---|
| D1 | Target v0.1 全量采纳（非临时）；L0-L3 CLOSED per §23 | Grill Q1 |
| D2 | GREENFIELD 先行；不依赖 Mapping/Migration | Grill Q2 |
| D3 | uni-harness = 目标分支谱系；产品代码落点为实现时独立决策 | Grill Q3/Q5 |
| D4 | Evidence-to-Belief 为首个切片 | Grill Q4 |
| D5 | ADR-0007 中文措辞已批准 | Grill Q6/Q8 |
| D6 | 测试环境 = 确定性 scripted Provider（非真机） | Grill 探索 |
| D7 | to-spec 输出映射到 Change State；to-tickets 映射到 WorkItem | issue-tracker.md |
| D8 | 产品代码落当前分支 `uni-harness`（即重构分支），不建新分支；参考旧分支 = uni-agent | 用户裁决（2026-09-07 实现入口，推翻 PLAN 初版 uni-product 方案） |
| D9 | C# / .NET 10 + xUnit；`UniClaw.*` 命名 / src-tests 布局 / global.json 沿用 uni-agent 谱系 | 用户裁决（2026-09-07） |

## Acceptance（8 条，grill 定稿原文）

1. **分离可观察**：Admission 判定与 Belief Relevance 判定各自独立留痕
   （admission record 与 relevance 判定是两个产出），任一 accepted 输入必经
   两者且次序不可合并
2. **relevant accepted Evidence** → Reconciliation 必须产生恰好一个新的
   immutable WorldBelief revision，其 evidence basis 引用该 record
3. **irrelevant accepted Evidence** → canonical record 保留；不要求产生任何
   WorldBelief revision
4. **fail-closed**：provenance 不完整的输入产出 rejected Admission Record，
   不产生 canonical Evidence Record，也不得进入 Relevance / Reconciliation
   或修改 Belief
5. **conflict**：两条冲突的 relevant accepted 观察产生显式表达 conflict 的
   新 revision，不静默覆盖
6. **派生失效**：revision 被取代后成为历史；Slice 有效性由 source revision
   与 freshness 派生判定——不预设或要求显式 invalidation event
7. **负向**：plan / expectation 不存在进入 Evidence Reconciliation 的路径
8. **幂等**：同一 canonical Evidence Record 的重复提交不得重复产生 revision

## Verification

```yaml
level: DETERMINISTIC   # 纯内存 fake world，无 IO/设备依赖
method: >
  逐条验收各自一个测试用例：正常路径（complete provenance → accept →
  reconcile → new revision）、无关路径（accept 但零 revision）、fail-closed
  （reject + 零 revision + 零 belief 变化）、冲突路径（双 accepted →
  conflict-bearing revision）、取代路径（新 revision → 旧降级 + Slice 失效）、
  负向路径（plan 输入无法触达 Reconciliation）、幂等路径（同 record 重复
  → 恰好一个 revision）
expected: >
  8 条验收全部 GREEN；无额外 revision 产生；fail-closed 路径零副作用
actual: >
  2026-09-07 dotnet test（UniClaw.Kernel.sln，SDK 10.0.400，net10.0）：
  失败 0 / 通过 8 / 跳过 0。验收 1..8 各自一个用例全部通过；TDD RED
  阶段（桩 + NotImplementedException）失败 7 / 通过 1（通过项为验收 7
  契约测试，类型表面自桩即存在）。构建零 error 零 CS 警告。REVIEW
  （意图对齐 / 范围 / 不变量 8-20 逐条 / 意外改动）通过；一处封装
  加固（Ledger 集合只读视图）在 GREEN 内完成，无行为变化。
evidence: evidence/2026-09-07-e2b-001-deterministic.md
```

## Constraints

- 不变量 8-20（Target §20.2）不可违反
- 开放实现选择（Target §22）不得改变 Owner / Authority / Lifecycle / Boundary
- 测试验证行为，不验证实现细节

## Assumptions

- scripted Provider 可以模拟完整的 provenance（producer/time/scope/lineage）
- WorldBelief revision 可用纯内存不可变对象表达（无需持久化框架）
- 冲突表达可以用结构化字段（不需要 AI 判断）

## Alternatives Considered

| 备选 | 被拒原因 |
|---|---|
| Effect 闭环作为首切片 | 更长（5 Owner），且依赖 belief 地基；grill Q4 裁决 |
| MIGRATION 模式（从旧 RuntimeAgent 拆出） | 149 基线债 + G0-G7 重量；grill Q2 裁决 GREENFIELD 先行 |
| Slice invalidation event 机制 | Grill 定稿明确「不预设或要求显式 event」；派生失效 |

## Owner / Authority Impact

- 新建 Evidence Ledger（sole Evidence Admission Authority）
- 新建 World Model（sole Belief/Reconciliation Authority）
- Uni Kernel 仅组合，不成为兜底 Owner（Target §3.5/§7）
- 不触碰既有 Harness 层任何 Owner

## ADR Refs

- ADR-0007：Development Flow 基线 + UniFlow 重定位
- 产品架构基线：`docs/architecture/product-architecture-baseline-l0-l3.md`
  （Target v0.1 §3.6/§3.7/§11/§12/§17-§21 为本切片直接权威；原
  docs/analysis/ 路径由 ARCH-DOC-013 relocation 收口）

## Residual Risks

- WorldBelief revision 的最小数据结构需 L4 设计（§22 开放）
- 冲突表达词汇需 L4 设计
- scripted Provider 的 provenance 完整性标准需与 Admission 检查对齐

## Status Log

| 日期 | from→to | 依据 |
|---|---|---|
| 2026-09-07 | →resolved | grill-with-docs 四轮问答定稿；to-spec 合成 |
| 2026-09-07 | resolved→planned | PLAN 落 plans/2026-09-07-e2b-001-evidence-to-belief-slice.md；实现入口决策 D8/D9（用户裁决） |
| 2026-09-07 | planned→implemented→closed | TDD RED(7 失败)→GREEN(8/8)；REVIEW 通过；DETERMINISTIC 验证 8/8（证据见 Verification）；无阻塞 Human Decision |

> 收口备注：D8 用户裁决后，AGENTS.md §1「本分支不承载产品代码」与之冲突，
> 以用户直接指令为准；AGENTS.md 文档同步待后续 Harness change 处理（用户
> 明示「其他不变」，本 change 不改动）。
