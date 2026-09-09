# DSE-001 — Dispatch Seam 规格化（P14 DispatchRequest 收窄 + P15 三态 outcome）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 05fd3e6e

## Intent（WHAT/WHY）
两个已裁决缺口在同一条 `IEffectDriver` 缝上闭合（Human Gate 2026-09-09）：

1. **J-b（P0 authority）——known authority leak**：`IEffectDriver.Deliver(
   CanonicalBinding)` 把 CanonicalBinding（含 IntentId / BindingId /
   authorization correlation 三元组之二者）整传进 Capability Plane，违反
   P14「不携带任何 judgment / authorization 语义」「capability 对授权态零
   感知」。legacy 反证（Authority: NONE）：`uni-agent` 的
   `IEnvironment.ExecuteAsync(DeviceAction)` 载荷仅 TargetElementIndex +
   TargetBounds，correlation 根本不下穿——Target 当前 realization 比 legacy
   泄漏更多。收窄为显式 `DispatchRequest`。
2. **HD-1 第一阶段（P1 protocol completeness）**：`DispatchResult` 两态
   （Delivered/Failed）压扁 timeout / unavailable / indeterminate（协议
   Deferred ⑤），且「结果缺失 / 不可解读」不可表达（Deferred ⑬——
   IEffectDriver 必返回非 null，无 fail-closed 路径）。落三态 outcome ×
   reason 两层词汇。

## Scope
- `Effects/`：
  - `DispatchRequest` record：bounded physical target（含 SpatialFrame 标识，
    P-UW-16）+ typed effect family + typed parameters + revision anchor；
    **不携带 IntentId / BindingId / judgment / authorization 语义**。
  - `IEffectDriver.Deliver(DispatchRequest)` 签名切换。
  - `DispatchOutcome` 三态化：`DeliveryCompleted / DeliveryFailed /
    UnknownOutcome`；`DispatchResult` 增 reason/diagnostic 字段（provider
    diagnostic 保留但分类词汇不进共享协议）。
  - EB 内 lowering：CanonicalBinding（occurrence）→ DispatchRequest 的
    bounded physical target 派生（frame-bound spatial 消费面：BindingView
    无 spatial fact，需扩 view 或 CanonicalBinding 建 spatial anchor——
    PLAN 裁决）；driver 不得重新 grounding。
- `Control/ControlLoop.NoteDispatchOutcome`：`UnknownOutcome` 与 `Failed`
  同触发 re-observe recovery；`DeliveryCompleted` 不触发（现仅 Failed
  触发，`ControlLoop.cs:71-80`——迁移验收点）。
- 协议基线 P14/P15 更新 + Deferred ⑤⑬ 闭合标注。
- 测试迁移（driver doubles / PER-003 新测试形状适配）+ 新场景测试。

## Out of Scope（禁止）
- `RequestAccepted` / pending 状态机 / attempt correlation id（HD-3 DEFER，
  见 Decisions 重开条件）。
- `Cancelled` 作为 outcome（裁决：reason=CancelledByCaller；cancel-before-
  dispatch 是 Gate 拒绝、不产生 receipt，不进词表；细分辨析留 async 阶段）。
- capability registry / catalog / 运行时 selection（HD-5 DEFER）。
- 非 UI effect family 扩展（HD-6 DEFER by family）。
- Control satisfaction 决策（= CDS-001）。

## Decisions
- **outcome 三态**（对世界的认知状态，进共享协议）：`DeliveryCompleted` /
  `DeliveryFailed` / `UnknownOutcome`。**reason 两层**（成因分类，不进共享
  协议词汇）：timeout-killed / transport-error / device-offline /
  device-unauthorized / command-rejected / result-uninterpretable /
  cancelled-by-caller。
- legacy ADB 映射（DIRECT IDEA，`AdbDispatchTarget.cs:29-36` 实证）：exit≠0
  （确定性失败）→ DeliveryFailed；timeout + kill client 进程（设备侧效果
  可能已发生，`AdbProcessRunner.cs:77-83`）→ UnknownOutcome；exit 0（命令
  已送达执行）→ DeliveryCompleted（≠ world effect）。device-offline 等
  transport 细类入 reason。
- **invariant：UnknownOutcome → re-observe → NEVER blind redispatch**（ADB
  实证纪律：legacy Traversal 对 TimedOut 与 Dispatched 同路径处理、零重发）。
- `Cancelled` 语义：Effect certainty = Unknown + Reason = CancelledByCaller
  （cancel request ≠ effect did not occur——client kill 后设备侧可能仍执行）。
- DispatchRequest **不裸传 OccurrenceRef**（driver 需要 effect-executable
  physical target，可能是 ADB 坐标 / DOM / Accessibility / 远程执行器句柄）；
  occurrence → physical target 的 lowering 由 EB 控制；不照搬 legacy
  DeviceAction 类型（思路同源、类型独立）。
- 同步 driver 下 DispatchRequest 不携带 correlation id（调用栈即关联）；
  async buyer 出现时与 AttemptId 一并设计（HD-3 联动）。
- **HD-3 DEFER 落档**（表述修正版）：当前架构无任何合法路径对同一
  authorized binding 自动重复 dispatch（设备预留 + 串行环 + dispatch-once
  gate），故无 attempt-correlation buyer；**无重复提交路径 ≠ 操作幂等**
  （tap×2 = 两次 effect）。重开条件四类：async driver / multiple in-flight
  attempts / cross-process recovery / external delivery acknowledgement
  （callback correlation）。
- **HD-5 DEFER 落档**：registry/catalog 无 buyer；显式组合 + attach 期四轴
  readiness 门控（mechanism / device channel / dispatch path / observation
  path，legacy `AdbDevicePreflight` DIRECT IDEA）保留为 architecture
  rationale。
- **HD-6 DEFER 落档**：effect family 共享 delivery boundary 的判据 =
  delivery semantics 同构（single bounded attempt / typed request /
  bounded timeout / typed outcome / no retry authority / effect verified by
  observation），不是「是否 UI 操作」。
- 与 PER-003（closed，05fd3e6e）关系：`ExportTransitionContext` 为纯增量、
  无语义冲突；本 change 的 DispatchOutcome 枚举扩展触及 PER-003 测试形状
  （迁移面，架构断言保持）。

## Assumptions
- frame-bound spatial 可经 GroundingView 族获得（候选 facts 携带）；EB
  消费面扩展属本 change 内设计。
- 同步单 driver 下三态词汇已够表达全部真实 outcome（ADB 实证）。

## Alternatives（被拒，Human Gate 2026-09-09）
- 四态 outcome（Cancelled 升格为 outcome）：被拒——cancel 是 termination
  cause / caller intent，不是第四种世界认知。
- 六词族一次锁死（含 RequestAccepted）：被拒——同步模型下死词汇，两阶段
  解锁。
- AttemptId 现在引入：被拒——无 buyer（HD-3，四类重开条件未满足）。
- 等多 driver buyer 再收窄 P14：被拒——P14 协议语义本身就是 buyer（EXP-008
  known-leak 闭合同族先例）；裁决定性为现已存在的 authority leak。
- DispatchRequest 裸传 OccurrenceRef：被拒——driver 需 physical target，
  重新 grounding 权禁入 driver。
- 照搬 legacy DeviceAction/AdbOperation 类型：被拒——思路同源（typed
  command + adapter 内翻译），类型按 Target buyer 重新证明。

## Owner-Authority impact
- EB：新增内 lowering（CanonicalBinding → DispatchRequest 派生）；Effect
  Delivery Authority / Canonical Binding Authority 不变。
- Capability Plane：输入面收窄；「对授权态零感知」从纪律要求变为结构事实。
- Control：NoteDispatchOutcome 行为扩展（UnknownOutcome 触发 re-observe）。
- 无新 L2 Owner、无新协议边（P14/P15 边内演化）。

## ADR refs
- 关联 ADR-0011（known-leak 闭合 / 字段 buyer 先例）；协议基线 P14/P15/
  Deferred ⑤⑬；本轮 Human Gate 三条主裁决依据（driver 只负责 delivery；
  receipt 只证 attempt 不证 effect；非幂等动作事前保证 = CDS-001 I-3）。

## Residual risks
- spatial 消费面设计未定（BindingView vs CanonicalBinding anchor，PLAN 裁决）。
- driver doubles 迁移面触及全部 IEffectDriver 实现（含 PER-003 测试）。
- reason 分类词汇若过早冻结会重蹈「两态压扁」覆辙——锁 outcome 不锁
  reason 全集（协议通则 4）。

## Acceptance
S1 IEffectDriver 签名 = Deliver(DispatchRequest)；结构断言：DispatchRequest
   无 IntentId/BindingId/judgment/authorization 字段
S2 DispatchRequest 携带 bounded physical target + SpatialFrame 标识 + typed
   effect/parameters + revision anchor；occurrence→physical 派生全部在 EB 内
   （driver 无 grounding 输入、无 WorldBelief 访问）
S3 三态 outcome + reason 落地；「结果不可解读」= UnknownOutcome(result-
   uninterpretable) fail-closed 不猜测成功（Deferred ⑬ 闭合）
S4 UnknownOutcome → Control re-observe recovery 触发、零补发（invariant
   断言）；DeliveryCompleted 不触发 recovery
S5 ADB 映射语义测试：确定性失败→DeliveryFailed+reason；timeout→
   UnknownOutcome+reason=timeout-killed；成功送达→DeliveryCompleted
S6 协议基线 P14/P15 更新、Deferred ⑤⑬ 闭合标注；P15 Reference
   Realization 同步
S7 既有测试迁移（含 PER-003 形状适配）架构断言保持；全量回归 GREEN

## Constraints
- 迁移面最小化：只动形状适配点；不重写测试语义（CBA-005 先例）。
- reason 词汇不声明 exhaustive closed set（协议通则 4）。
- 不触碰 CTL-001 in-flight 文件（ControlLoop.NoteDispatchOutcome 为既有
  文件受控修改，如 CTL-001 落地触及同文件则协调时序）。
- 确定性 doubles；不改 harness 层。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案，两次独立运行 + CTL-001 适配后一次无移开全量）
  expected: 新增 S1–S4（结构断言 / 三态×reason / Unknown recovery 闭环 /
    Completed 对照）GREEN；全解决方案全绿；diff 面仅 Effects/ + Assurance/
    RuntimeAssurance.cs + Control/ControlLoop.cs + tests/ + 协议基线 + CONTEXT.md
  actual: >
    174/174 GREEN（Kernel 157 + Agent 17；两次独立运行 + CTL-001 提交后无移开
    全量复跑确认）。变更面 = Effects/（EffectDispatch 重写：DispatchRequest 四
    字段 + 三态枚举 + Reason；EffectBoundary：ToDispatchRequest lowering +
    receipt 沿载）+ Assurance/RuntimeAssurance.cs（NoteOutcome 三态化——Plan
    侦察漏列的第 4 触点，正向偏离 D1）+ Control/ControlLoop.cs
    （NoteDispatchOutcome 扩展 UnknownOutcome）+ tests/（9 文件 doubles/枚举
    迁移 + CTL-001 两文件适配[D3] + DispatchSeamSpecificationTests 新增 8
    用例）+ 协议基线（P14/P15/Deferred ⑤⑬）+ CONTEXT.md 词条。S3/S3b 行为级
    证明「Unknown → 强制 Recovery intent + binding-already-dispatched 执法 +
    新 revision 后才允许再 dispatch」——运行语义真闭环，非枚举改名。
  evidence: dotnet test 输出（2026-09-09，三次运行）；git diff 审阅记录
```

## Status log
2026-09-09 · understanding→resolved · Operate/Operation 只读审计 + ADB 二轮
  分析 + Human Gate 裁决：J-b ACCEPT + P0（known authority leak）、HD-1
  ACCEPT WITH REVISION（三态 outcome × reason；Cancelled=reason；
  RequestAccepted defer；Unknown→re-observe→never blind redispatch）、
  HD-3/5/6 DEFER 落档（表述修正 + 四类重开条件）
2026-09-09 · resolved→persisted · to-spec 建立 state.md
2026-09-09 · persisted→planned · PLAN 落 plans/2026-09-09-dse-001-dispatch-seam-specification.md
  （ADOPTED）。PLAN 前侦察三事实：①全部 9 处 driver doubles 忽略 binding 参数
  （零消费纯泄漏，收窄 = 纯减法）；②OccurrenceBelief/BindingView/GroundingView/
  CanonicalBinding 全链无 spatial 字段——DispatchRequest 第一版 target =
  occurrence 引用，不造无源 spatial 字段，physical-target 完整化随真实 driver
  buyer 另立 change；③EB.Dispatch 对外签名不变，UniKernel.cs 零改动，与
  CTL-001 文件面零交集。切片：seam 切换 → Unknown recovery 闭环 → 协议/词条
  收口；验证点 5（Unknown→Recovery intent + binding-already-dispatched 执法）
  为最重验收。Residual risks 的 spatial 项据此精确化（无源不造，非设计未定）
2026-09-09 · planned→implemented · Direct 实施（subagent 基础设施两次失败，
  按 UniFlow B2 切换；上下文全热）。产品四触点 + 测试迁移 + 新增
  DispatchSeamSpecificationTests（8 用例）
2026-09-09 · implemented→reviewed · REVIEW 偏离点 4 条：D1 正向——Plan 侦察
  漏列 RuntimeAssurance.NoteOutcome（no-blind-retry 执法面），实施中发现并
  同步三态化，S3b 全闭环场景证明「Unknown → re-observe（新 revision）→ 才
  允许再 dispatch」；D2 微——EffectReceipt.Reason 置末位+默认 null（C# 可选
  参数约束，语义等价，既有构造零迁移）；D3 环境——实施中 CTL-001 由 in-flight
  变 closed+committed（28ae4c36），其两文件按 CBA-005 先例纳入机械适配面
  （3 枚举 + 2 签名，架构断言保持，T4 recovery 场景新枚举下 GREEN）；D4 验证
  方法——CTL-001 in-flight 期间全量验证采用临时移开→跑→立即复原（内容零修改
  mtime 一致确认），提交后追加无移开全量
2026-09-09 · reviewed→verified→closed · 三次全量 GREEN（174/174）+ diff 审阅
  合规 → P14_P15_DISPATCH_SEAM_SPECIFIED；协议基线 P14/P15/Deferred ⑤⑬ +
  CONTEXT.md 词条同步收口。CTL-001 已 closed → CDS-001 解阻条件出现（待其
  会话按 parked 声明先做入口复验）
