# ESO-001 — EntityScopedObligation 物理入口（producer ②：contract → standing demand → identity 绑定）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 598c6bc2

## Intent（WHAT/WHY）
P23 producer ②（EntityScopedObligation）语义自 ADR-0014 起保留、物理入口
deferred（⑭/UWM-009 §33 deferred 20）。Q1 场景 2（standing demand 早于首次
观察、早于 identity）至今无物理路径。本 change 落 phase 1：contract 中
entity-scoped obligation 经 Kernel admission 组合缝自动登记 descriptor-scoped
standing ContinuityDemand；首次充分观察后经 ResolveContinuity 建立 LogicalItem
并绑定 demand——deferred ⑭ 的入口半边闭合。

## Scope
- `Run/`：obligation 载荷增可选 EntityScope（复用 World.TargetDescriptor——
  P23/GroundingView 协议词汇，单一 assembly 内复用不跨组件）；contract
  authoring → RunObligation 全链携带（读 RunModel.AdmitContract 构造路径适配）。
- `UniKernel`：AdmitContract 组合缝扩展——admission accepted 后，对每个带
  EntityScope 的 obligation 登记
  `ContinuityDemand("demand-obl-<obligationIdentity>", EntityScopedObligation,
  descriptor 字段, anchors: null)`（幂等；rejected/未接受 → 零登记）。
- 协议基线 P23 Status 一行注记（producer ② 入口已落；fulfillment 判定 =
  phase 2，blocked by CDS-001 state 载荷）。
- 测试：E1–E6（新文件）。

## Out of Scope（禁止）
- **Phase 2（fulfillment 判定）**：entity-scoped obligation 的满足判据需
  occurrence state 载荷 = CDS-001 未提交施工面——显式 blocked，CDS 落地后
  另立 ESO-002。
- WorldModel / UiEntityModel / Slice / Control（CDS-001 未提交改动所在）；
  CONTEXT.md（当前为 CDS 混合文件）；Assurance 判定逻辑；demand 撤销语义
  （obligation 与 run 同生命周期，无独立 revoke buyer）。

## Decisions（Leader 预固定）
- D1 复用 TargetDescriptor 作 EntityScope 载荷（词汇统一：P23 缝、GroundingView、
  obligation 三处同一 descriptor 语义；不造平行 record）。
- D2 入口 = Kernel 组合缝（AdmitContract accepted 后）——RunModel 不取得对
  WorldModel 的直连边（L0 边界不破）；登记幂等键 = obligation identity 派生
  （同 contract 重复 admit 复用同 demand）。
- D3 standing demand 合法性：无 anchor（descriptor-scoped）→ 允许早于任何
  revision/identity（ADR-0014 timing）；首个充分 evidence 到达后
  ResolveContinuity → ReferenceEstablished（走既有 UIW-003 机器，零新判别语义）。
- D4 确定性：DemandId 内容派生；零 wall-clock/random。

## Acceptance
E1 contract 带 entity-scoped obligation → AdmitContract accepted →
   world.ContinuityDemands 恰含该 demand（source=EntityScopedObligation、
   descriptor 字段正确、无 anchor）
E2 同 contract 重复 admit（幂等路径）→ demand 不重复
E3 admission rejected → 零 demand 登记
E4 无 EntityScope 的 obligation → 零登记（既有行为不变）
E5 standing demand → 首帧观察（corpus/double）→ ResolveContinuity →
   ReferenceEstablished → demand.LogicalItemId 绑定新 item（Q1 场景 2 全链）
E6 既有套件全绿 + 纯 checkout 独立成立（stash 并行面验证）

## Constraints
Zone：Run/ + UniKernel.cs + 新测试 + 协议一行；显式路径提交；stash 往返验证。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案）+ stash 往返纯 checkout 验证
  expected: E1–E6 满足
  actual: >
    197/197 GREEN（Agent 17 + Kernel 180；+5 = E1–E5，Leader 独立复跑确认；
    基线 192 = 183 + CDS-001 的 9 条 DesiredStateSatisfactionTests——CDS 于本
    change 施工期间落地 0b2cf4ef，我方零触碰其文件）。变更面 = RunObligation
    可选 EntityScope（复用 TargetDescriptor）+ UniKernel.AdmitchContract 组合缝
    （accepted → 逐 obligation 登记 demand-obl-<ObligationId>，幂等）+ E1–E5
    测试 + 协议 P23 Status 一句。E5 全链 = standing demand 早于观察 → 首帧 →
    ReferenceEstablished → demand.LogicalItemId 绑定（Q1 场景 2 闭合）。
  evidence: dotnet test 输出（2026-09-08，两次独立运行）；git diff 抽审
```

## Status log
2026-09-08 · understanding→resolved · CDS-001 closed-but-uncommitted 确认；
  phase 1/2 分区裁定（fulfillment blocked by CDS state 载荷）
2026-09-08 · resolved→persisted→planned · state.md 建立；切片 = 载荷 → 入口缝
  → E1–E6 → stash 验证
2026-09-08 · planned→implemented · 委派 fresh subagent：E1–E5 RED → EntityScope
  载荷 + 组合缝 + 协议一句 → GREEN（偏离 2 条合规：Accepted bool 判定字段、
  stash 验证归 Leader）
2026-09-08 · implemented→reviewed · REVIEW：红线遵守（World/Control/CONTEXT/
  Assurance 零触碰——施工期间 CDS-001 恰落地为 0b2cf4ef，无混合）；L0 边界
  保持（RunModel 无直连 WorldModel 边）
2026-09-08 · reviewed→verified→closed · Leader 独立复跑 197/197 →
  ENTITY_SCOPED_OBLIGATION_INGRESS_ESTABLISHED；ESO-002（fulfillment）解除
  阻塞（CDS state 载荷已在 HEAD）
