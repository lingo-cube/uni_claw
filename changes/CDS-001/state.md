# CDS-001 — Control Desired-State Satisfaction（非幂等 effect 的 pre-dispatch 正确性）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 598c6bc2

## Intent（WHAT/WHY）
P0 correctness 缺口（Operate/Operation 架构审计 + ADB 源码二轮分析，Human Gate
裁决 HD-4）：物理非幂等动作必须在发出前由 Control 评估 desired-state
satisfaction。legacy 证据（Authority: NONE）：
`uni-agent:src/UniClaw.Runtime.Adapters/Operator/DeviceActionTranslator.cs:63-69`
——SetSwitch 物理执行 = tap 开关位置，"DesiredValue semantic is handled by the
Runtime (idempotent)"；**对已 Checked=true 的开关再 tap 会翻成 false**。当前
Target 链路无任何一层做目标状态满足检查（Assurance 判 freshness/conflict/
safety 不判满足度；EB/Gate 无 world state 输入）——SetSwitch(true) when
already true → tap → false 是真实 destructive correctness bug（场景 U2）。

裁决（Human Gate，2026-09-09）：

```text
Control consumes Slice（目标 occurrence 的 state claim）
→ Satisfied      → 不签发 EffectIntent（decision outcome，不是 effect）
→ Unsatisfied   → 正常签发 act-intent（全链不变）
→ Unknown / Conflicting → observe / resolve / safe-stop per policy
```

## Scope（入口复验后修订，2026-09-09，base 598c6bc2）
- 载荷路径**定案为 occurrence 增可选 state 字段**：`ProposedOccurrence` /
  `OccurrenceBelief` / `OccurrenceFact` 各增 `string? State = null`（末位
  可选，既有构造零迁移）；`WorldModel` occurrence 铸造（:243）与
  `DeriveSlice`（:770）直通携带。`CandidateOccurrenceFact`（GroundingView
  面）**不加**——grounding buyer 不需要（ADR-0011 最小载荷）。
- `Control/DescriptorTargetPolicy.cs`：`TargetSpec` 增 `string? DesiredState
  = null`；`Decide` 匹配 occurrence 后做 satisfaction 检查（满足→标
  visited 跳过该 spec；Unknown→跳过不标 visited；不满足→Act）。
- `docs/adr/0017`：I-3 ADR（pre-dispatch non-idempotent safety）。
- `CONTEXT.md`：Avoid 词条（Operate/IOperation/Executor，HD-2）+
  Desired-State Satisfaction 词条。
- 测试：新场景文件 + 既有全量零回归。

## 入口复验记录（2026-09-09，解阻后执行）
- 解阻条件确认：CTL-001 closed+committed（28ae4c36）；其间另发生 RVR-001
  （0a62a963：GroundingSeam owner-fact / 级联统一 / ForUiTarget 工厂）与
  CLE-001（598c6bc2：claim evolution，同 producer 异 scope → Revise）。
- **D-复验-1 载荷路径定案**：ScopedClaims occurrence 寻址方案被否定——
  claim subject 由 evidence 侧决定，occurrence id 是 revision-local 每轮
  重铸（P-UW-25），作 claim subject 会使 CLE-001 Revise 域（同 subject 才
  Revise）结构性失效。occurrence state 走 occurrence 族字段（revision-local
  presentation fact，随 occurrence 集合替换，不进 claim 演化域——语义正确：
  跨 revision state 演化属 claim/LogicalItem 域）。
- **D-复验-2 satisfaction 落点定案**：policy 层（TargetSpec.DesiredState +
  DescriptorTargetPolicy 检查），非 ControlLoop 通用执法——desired-state
  是 authoring 语义（Click 型 intent 无期望终态，不适用）；policy 是
  Control 权域内 authoring 意图的消费点（CTL-001 D9：policy 产 proposal、
  签发权仍在 ControlLoop）。CTL-001 已 closed，其文件扩展为合法增量。
- **D-复验-3 词汇边界精化**：occurrence state 的 epistemic 是
  Known(value)/Unknown(null) 二态；Conflicting 属 claim 域（CLE-001
  Revise/Conflict），occurrence-state 模型不可达——裁决「Conflicting →
  safe-stop」由 claim 域路径承载，非本切片载荷。
- 基线：183/183 GREEN（Kernel 166 + Agent 17，HEAD 598c6bc2）。

## Out of Scope（禁止）
- Effect Boundary / IEffectDriver / DispatchRequest / outcome 词汇（= DSE-001）。
- canonical NoOp effect（裁决禁止：「无需行动」是 Control decision outcome，
  不是 effect，不得与 Click/Toggle 平级）。
- Assurance 侧满足判断（例外仅当 Control 结构性无法获得该 state——当前
  Slice 载荷扩展后 Control 可获得，例外不启用）。
- 非 UI effect family（HD-6 defer by family）。
- CTL-001 in-flight 文件（见 Constraints 让路声明）。

## Decisions
- Owner = Control（Control Intent Authority 语义内聚：whether/what to do）。
  「已满足所以不做」是决策，不是授权判断，不是 delivery。
- I-3 invariant（本 change 立 ADR）：**非幂等物理动作的安全性必须在 action
  发出之前由 Runtime 的世界状态和控制语义保证**——post-action verification
  只能发现破坏，不能防止破坏。现有不变量体系（33/34：Attempt≠Effect）只有
  事后验证义务，I-3 补事前保证义务。
- 词汇隔离纪律：Control 侧 desired-state `Unknown/Conflicting`（claim
  epistemic 轴，UWM-009 §8）≠ Assurance FreshnessJudgment `Unknown`（消费
  相对轴，ADR-0010）——两套 Unknown 不同轴，测试与文档不得混用。
- legacy `SemanticActionLowerer.Lower` 的 SAFETY NoOp 检查（已满足→不
  dispatch）：正确性义务成立、归属 REJECT（lowering 层混合状态检查），由
  本 change 迁移到 Control 侧。

## Assumptions
- 载荷扩展后 Control 能经 Slice 读到目标 occurrence 期望 state 维度值及其
  认知状态（Known/Unknown/Conflicting）。
- 物理非幂等性由 adapter 层翻译事实佐证（ADB SetSwitch=tap 是实例，非孤例；
  typed toggle 族通用假设）。

## Alternatives（被拒，Human Gate 2026-09-09）
- canonical NoOp Effect（Control 发 NoOp → EB 不 dispatch）：被拒——把决策
  结果伪装成 effect，污染 effect ontology。
- Assurance 负责满足检查：被拒——把 decision authority 向判断层挪半步；
  例外（Control 无法获得 state）保留但当前不启用。
- EB / Driver 内检查：被拒——P14 零 belief 感知；legacy Lowerer 的混合
  职责正是反面教材。
- 靠 post-action verification 修复：被拒——只能发现破坏不能防止（I-3）。

## Owner-Authority impact
- 无新 L2 Owner；Control Intent Authority 语义扩展（pre-dispatch
  satisfaction 成为决策输入维度）。
- World Model：Slice 载荷扩展（owner-derived，ADR-0011 字段 buyer 原则——
  buyer = 本 change 的 satisfaction 决策）。
- Assurance / EB / Capability Plane：零改动。

## ADR refs
- 本 change 新增 I-3 ADR；关联 ADR-0011（consumer view 字段证据先例）、
  ADR-0010（词汇隔离：freshness 轴对照）。

## Residual risks
- OccurrenceFact state 维度字段形状未定（buyer-driven，PLAN 裁决）。
- Control policy 输入形状变化触及 IControlPolicy 全部实现与测试 doubles。
- 与 CTL-001 时序：CTL-001 未 closed 前本 change 的 Control/ 侧不可落地。

## Acceptance
S1 Slice 载荷可表达目标 occurrence 的期望 state 维度值 + 认知状态
   （Known/Unknown/Conflicting 三态可区分）
S2 claim=已满足 → Control 不签发 act-intent：零 EffectReceipt、零 dispatch、
   零 binding 认定副作用
S3 claim=相反值/未满足 → 正常签发，全链（Bind→Judge→Gate→Deliver）行为不变
S4 claim=Unknown/Conflicting → 按 policy observe/resolve/safe-stop，
   fail-closed 不 dispatch
S5 端到端：SetSwitch(true) on already-true 目标 → 零物理动作（U2 闭合；
   I-3 满足）
S6 词汇隔离断言：satisfaction Unknown 不与 FreshnessJudgment Unknown 混用
S7 既有测试零回归（载荷形状迁移除外，架构断言保持——CBA-005 先例）

## Constraints
- ~~CTL-001 让路~~（已解除：CTL-001 closed 28ae4c36；其文件按 CBA-005 先例
  作为合法扩展面）。
- 新代码仅限 World occurrence 族 + Control/DescriptorTargetPolicy + 新测试
  + docs/adr + CONTEXT.md；不改 Effects/；确定性 doubles；不硬编码场景字符串。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案，两次独立运行）
  expected: S1–S7 GREEN；既有 183 全量零回归；diff 面仅 World occurrence 族 +
    DescriptorTargetPolicy + docs/adr/0017 + CONTEXT.md + 新测试
  actual: >
    192/192 GREEN（Kernel 175 + Agent 17；两次独立运行）。新增 9 用例
    （S1/S1b/S2/S3/S4/S5/S6×3）全绿；既有 166 零回归（S7）。变更面 =
    World/（UiEntityModel：ProposedOccurrence/OccurrenceBelief 各 +State
    可选；Slice：OccurrenceFact +State；WorldModel：铸造/DeriveSlice 两处
    直通）+ Control/DescriptorTargetPolicy.cs（TargetSpec +DesiredState；
    Decide 三分支：Satisfied→visited 跳过 / Unknown→跳过不 visited /
    Unsatisfied→Act）+ docs/adr/0017（I-3）+ CONTEXT.md（Desired-State
    Satisfaction 词条 + Operate Avoid）+ 新测试。S5 多轮 cycle 已满足目标
    恒零物理动作（I-3 / U2 闭合）；CandidateOccurrenceFact 零改动
    （grounding buyer 不需要）。
  evidence: dotnet test 输出（2026-09-09，两次独立运行）；git diff 审阅
```

## Status log
2026-09-09 · understanding→resolved · Operate/Operation 只读审计（legacy
  uni-agent 全链路 + ADB 二轮）+ Stateful Grill + Human Gate 裁决：HD-4
  ACCEPT + P0 correctness（Owner=Control，不建 NoOp effect）、HD-2 ACCEPT
  （Operate 废弃）、I-3 invariant、三条执行前提记录（Slice 载荷缺口已源码验证）
2026-09-09 · resolved→persisted · to-spec 建立 state.md
2026-09-09 · parked（lifecycle 保持 persisted）· BLOCKED_BY = CTL-001 close
  （Human 指令，2026-09-09）：Control/ 实现面等待 CTL-001 closed；届时先基于
  新 Control/Slice 实况重新做入口复验（Entry/Resume Protocol），再 PLAN——
  不沿用本文件假设的实现形状。World/Slice 载荷与 I-3 ADR / CONTEXT.md 词条
  面与 CTL-001 out-of-scope 声明一致，解阻后如仍成立可先行。
2026-09-09 · parked→understanding→resolved→planned · 解阻（CTL-001 closed，
  另有 RVR-001/CLE-001 两轮并行变更）→ 入口复验执行（见「入口复验记录」：
  载荷路径定案 occurrence 字段 / satisfaction 落点定案 policy 层 / 词汇边界
  精化二态 epistemic；基线 183/183）→ Scope 修订 + PLAN。PLAN：
  plans/2026-09-09-cds-001-desired-state-satisfaction.md
2026-09-09 · planned→implemented · Direct 实施（会话上下文全热）。产品面与
  PLAN Before/After 完全一致（World 三类型 +State / WorldModel 两处直通 /
  TargetSpec +DesiredState / Decide 三分支）
2026-09-09 · implemented→reviewed · REVIEW：产品面零偏离。测试编排两处
  实现细节：①初版测试用虚构 owner id 被 Slice scope 过滤（空景观）——重写
  对齐 corpus 模式（ProbeContainerId 预知 + UIWorldDoubles.Observation 同源
  Prime，ControlReferencePolicyTests 先例）；②S3 经 ActViaCurrentGrounding
  取 GroundedActResult.Act.Receipt（CTL-001 形状）。语义断言逐条对照
  S1–S7 通过
2026-09-09 · reviewed→verified→closed · 两次独立 192/192 GREEN + diff 审阅
  合规 → DESIRED_STATE_SATISFACTION_PRE_DISPATCH_GUARANTEE_ESTABLISHED；
  ADR-0017 + CONTEXT.md 词条收口。Operate/Operation 审计三 change 链
  （DSE-001→CDS-001 + HD-3/5/6 defer 落档）至此全部闭环
