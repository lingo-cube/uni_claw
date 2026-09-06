# C2E-002 — Control-to-Effect Belief Closed Loop Vertical Slice

lifecycle_state: understand · disposition: none · depth: decision-heavy · base: d8f6e319

## Intent

**WHAT**: 在 E2B-001 的 Evidence→Belief 地基上，把剩余四个 L2 一次串成
最短可验证语义闭环——Control intent 选择 → Assurance action-local
判定 → Effect Boundary canonical binding → dispatch → Effect Receipt →
Effect Evidence 回流 Evidence Ledger → 新 WorldBelief revision。证明
Candidate Binding ≠ Canonical Binding ≠ Authorized Dispatch（不变量 24/25）、
Action Attempt ≠ Effect（33）、系统的世界观因自己的动作而改变。

**WHY**: E2B 闭环只回答「依据什么形成判断」；C2E 回答「判断如何变成
受约束的动作、动作如何回到判断」。Target v0.1 的运行时骨架由此闭合，
后续一切（Outcome、Memory、真实 Provider）都挂在这条环上。

## Scope

- Run Model（最小面）：Execution Contract View admission、Objective State、
  Progress State；Proof Obligation State 仅占位（记录存在，不实现 discharge）
- Control Loop（最小）：Control State、Tactical Hypothesis、基于 Slice +
  Contract View + Run State 的 intent 选择（确定性 scripted policy）
- Assurance（action-local only）：Action Admissibility / Preconditions /
  Freshness / Safety Guard 判定
- Effect Boundary：Candidate→Canonical Binding、Effect Gate、Dispatch
  （scripted driver）、Effect Receipt
- Effect Evidence 回流：Receipt/raw artifact → Evidence Ledger admission →
  World Model Reconciliation
- Uni Kernel 组合面扩展：两 L2 组合缝 → 六 L2 组合缝（仍不成为兜底 Owner）

## Out of Scope

- Terminal 语义：Outcome Proof、Proof Obligation discharge、Effect
  Verification、Runtime Outcome emission（→ OUT-003）
- UniAgent（L1）：Execution Contract 由测试脚本构造，不建壳
- Memory System、Capability Plane 框架化、FSM、存储/部署、多 Goal/多 Run
- 真机/外部环境（SCENARIO/ENVIRONMENT 级验证）
- legacy（uni-agent）迁移或依赖

## Decisions

| # | 决策 | 来源 |
|---|---|---|
| D1 | 下一 change 主轴 = C2E 四 L2 闭环（否掉单 L2 增量 / E2B L4 加固先行 / 场景压力测试） | 方向 grill Q1 |
| D2 | 终止边界 = 信念闭环（Effect Evidence → 新 revision）；terminal/Outcome → OUT-003 | 方向 grill Q3 |
| D3 | Run Model 最小面；Proof Obligation State 占位不 discharge | 方向 grill Q4 |
| D4 | UniAgent 不建 L1 壳；scripted contract（同 E2B D6 模式） | 方向 grill Q5 |
| D5 | 验证 level = DETERMINISTIC（纯内存 scripted driver/dispatcher） | 方向 grill Q6 |
| D6 | 产品域词表收编根 CONTEXT.md 单 context（已执行 d8f6e319） | 方向 grill Q2 |

## Acceptance

（待立项首轮 grill 定稿——参照 E2B-001 的 8 条格式。已知必答遗留：
Effect Evidence 的 provenance 是否要求 self-produced 标记（不变量 34 的
admission 侧表达）；Candidate→Canonical Binding 的最小拒绝路径；scripted
policy 的确定性定义。）

## Constraints

- Target v0.1 §13-§16（Run Model / Control Loop / Assurance / Effect
  Boundary）为本切片直接权威；不变量 21-27、32-34 不可违反
- E2B-001 已定型语义（Admission / Relevance / Reconciliation / Revision /
  Slice）不得改动所有权或生命周期
- 测试验证行为，不验证实现细节

## Assumptions

- scripted driver 可产生确定性 Dispatch Receipt（含成功与失败两态）
- intent 选择可用确定性策略表达（无需 AI）
- E2B 的 claim/conflict 词汇可直接承载 Effect Evidence 回流

## Alternatives Considered

| 备选 | 被拒原因 |
|---|---|
| 单 L2 增量（Run Model 先行） | Run State 无消费方，缺行为压力，易产只有类型的层（Q1-B） |
| E2B L4 加固先行 | 三项残留会在闭环实现压力下自然逼出，单独做是脱稿设计（Q1-C） |
| 场景压力测试先行 | 无 GREENFIELD 运行时可跑、真机成本高（Q1-D） |
| 运行闭环（含 Outcome emission） | Outcome Proof 强耦合 Proof Obligation 完整语义，使 Run Model 膨胀（Q3-ii） |

## Owner / Authority Impact

- 新建四个 L2 Owner：Run Model（Canonical Run State Recording）、Control
  Loop（Control Intent）、Assurance（Runtime Assurance Judgment, action-local）、
  Effect Boundary（Canonical Binding + Effect Delivery）
- Uni Kernel 组合面扩展，仍不拥有任何 canonical state（不变量 3）
- 不触碰 E2B-001 两个 Owner 的既有权威

## Residual Risks

- Effect Evidence provenance 语义未定（立项 grill 必答）
- Binding 词汇（candidate/canonical/rejection）最小集未设计
- scripted policy 与未来真实 Control Loop 策略的边界需在验收中显式声明

## Status Log

| 日期 | from→to | 依据 |
|---|---|---|
| 2026-09-07 | →understand | grill-with-docs 方向会话（2 轮 6 问）定稿主轴与边界；acceptance 留待立项首轮 grill |
