# C2E-002 — Control-to-Effect Belief Closed Loop Vertical Slice

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: d8f6e319

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
| D7 | Effect Evidence provenance = producer 命名空间约定（`effect.boundary` 前缀 + lineage 携 dispatch 引用）；E2B 类型零改动 | 立项 grill Q1 |
| D8 | Binding 三态判定：Canonical(revisionId,target) \| Rejected(stale-revision/ambiguous/unknown-target)；失效为派生判定（复用 Slice 模式，无 event） | 立项 grill Q2 |
| D9 | ControlPolicy 可注入缝：(ContractView, Slice, RunState) → ControlIntent(observe/act/recovery)；测试用确定性策略表 | 立项 grill Q3 |
| D10 | 最小 Recovery 边进本片：dispatch-failed → recovery-intent 重新入环（证明不变量 30/31） | 立项 grill Q5 |

## Acceptance（10 条，立项 grill 定稿原文）

1. **契约准入 fail-closed**：非法/不完整 Execution Contract 被拒，零 Run
   State 副作用
2. **权威分离**：Control intent / Assurance judgment / canonical binding /
   Effect receipt 四类产出各自独立留痕，任一 act-intent 必经四者且
   次序不可合并
3. **Candidate ≠ Canonical**：Grounding 产出的 candidate binding 未经
   Effect Boundary 认定不得 dispatch；canonical binding 必须绑定 current
   WorldBelief revision（不变量 24）
4. **Gate 只执法**：Assurance 拒绝的 intent，Effect Gate 拒绝 dispatch；
   Gate 不重判、不改 target、不扩权（不变量 26）
5. **Attempt ≠ Effect**：Effect Receipt 留痕但不产生 effect-claim 的
   WorldBelief revision；只有 post-action accepted observation 才产生，
   且其 producer 前缀表达 self-produced（`effect.boundary`，不变量 33）
6. **Run State 边界**：Progress State 随 cycle 推进；action-local
   assurance state 不进入 canonical Run State（§13 边界）
7. **负向**：Tactical Hypothesis / plan 类型不得出现在 Reconciliation、
   Assurance judgment、Binding 的任何输入签名（不变量 32）
8. **E2B 权威零穿透**：全流程中 Evidence Ledger / World Model 的状态
   变化仅经由既有 admission / relevance / reconciliation 路径，无平行
   写入
9. **Contract View immutable**：同 version 重复 admit 不产生新 View
10. **Recovery 重新入环**：dispatch-failed 后必经 re-observe→reconcile
    产生新 WorldBelief revision 后才可再 act；同 target 的直接重试
    （无新 revision）被 Assurance 拒绝（不变量 30/31，无 blind retry）

## Verification

```yaml
level: DETERMINISTIC   # 纯内存 fake world，scripted driver/observation provider/policy
method: >
  逐条验收各自一个测试用例（E2B 模式）：契约 fail-closed、四产出留痕
  与次序、candidate 拒绝路径（stale/ambiguous/unknown-target 三态）、
  gate 拒绝、attempt≠effect（receipt 留痕零 revision + post-action
  observation 产 revision）、run state 边界、plan 负向签名封闭、
  ledger/world 零平行写入、contract view 幂等、recovery 重新入环且
  无新 revision 的重试被拒
expected: >
  10 条验收全部 GREEN；fail-closed 路径零副作用；recovery 路径无
  blind retry
actual: >
  2026-09-07 dotnet test（UniClaw.Kernel.slnx，SDK 10.0.400，net10.0）：
  失败 0 / 通过 18 / 跳过 0——C2E 10 用例全 GREEN，E2B 8 用例零改动
  全保持。fail-closed 路径零 Run State 副作用；candidate 三态拒绝零
  dispatch；gate 拒绝路径零 receipt；attempt 回流 admitted 但零
  effect-claim revision；recovery 路径无 blind retry（no-blind-retry
  拒绝留痕，receipt 计数不增长）
evidence: evidence/2026-09-07-c2e-002-deterministic.md
```

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

- （已解）act-intent 与 Tactical Hypothesis 附着语义 → P1 零附着
  （hypothesis 仅 ControlLoop 内部 log；验收 7 反射锁死不外溢）
- （已解）post-action observation 时机契约 → P2（dispatch 产生 receipt
  后、下一 control cycle 前；producer 前缀 effect.boundary + lineage
  携 dispatch 引用）
- （已解）Effect Evidence provenance → D7；Binding 词汇 → D8；
  scripted policy → D9
- L2 公有方法的 authority 校验集中在 UniKernel 组合缝（P5）：
  EffectBoundary/RunModel 直连消费不在本片组合面内——若后续 change
  开放直连，需补 issuance/provenance 校验（Review F2）

## Status Log

| 日期 | from→to | 依据 |
|---|---|---|
| 2026-09-07 | →understand | grill-with-docs 方向会话（2 轮 6 问）定稿主轴与边界；acceptance 留待立项首轮 grill |
| 2026-09-07 | understand→resolved | 立项 grill（1 轮 5 问，全按推荐）定稿 acceptance 10 条 + D7-D10；Verification 声明就位 |
| 2026-09-07 | resolved→planned | PLAN 落盘 `plans/2026-09-07-c2e-002-control-to-effect-closed-loop.md`（P1-P6 兑现两项 residual risks；Route: Direct） |
| 2026-09-07 | planned→implemented | 四 L2（Run/Control/Assurance/Effects）+ UniKernel 六缝实现；10 用例首跑 2F/16P（测试预期与 E2B conflict 语义不一致，测试缺陷）修正后 18/18 GREEN |
| 2026-09-07 | implemented→reviewed | fresh SubAgent review：不变量 21-27/32-34 逐条 PASS、E2B 零改动；F1（major）§17 缺 dispatch 失效源 + F3/F4/F5/F6/F8；F2 记 residual risk |
| 2026-09-07 | reviewed→implemented | 修复 F1（binding 派生失效并入 dispatch 消费判定，仍无 event）+ F3/F4/F5/F6/F8；复跑 18/18 GREEN |
| 2026-09-07 | implemented→verified | 验收 10 条逐条对照 evidence 全 GREEN；四元组 method/expected/actual/evidence 完整 |
| 2026-09-07 | verified→closed | 范围完成 + acceptance 被证明；E2B-001 零改动保持 GREEN；CONTEXT.md 术语同步；无未授权改动（`.tmp-hf-intake/` 未纳入） |
