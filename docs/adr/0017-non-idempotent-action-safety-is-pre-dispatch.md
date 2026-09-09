# 0017 — 非幂等物理动作的安全性必须在 dispatch 前由世界状态与控制语义保证

Operate/Operation 架构审计 + ADB 设备源码二轮分析（2026-09-09）暴露的缺口：
物理 toggle 类动作的 adapter 翻译是「tap 开关位置」（legacy
`uni-agent:src/UniClaw.Runtime.Adapters/Operator/DeviceActionTranslator.cs:63-69`
自证"DesiredValue semantic is handled by the Runtime (idempotent). The
Operator only needs to tap"）——**物理执行非幂等：对已 Checked=true 的开关
执行 SetSwitch(true) 会把它 tap 成 false**。审计时 Target 链路无任何一层做
目标状态满足检查（Assurance 判 freshness/conflict/safety 不判满足度；EB/Gate
无 world state 输入，也不应有）。

Human Gate（2026-09-09）三条主裁决依据之三定为本 ADR：

## Decision

```text
非幂等物理动作的安全性必须在 action 发出之前
由 Runtime 的世界状态和控制语义保证。

post-action verification 只能发现破坏，不能防止破坏
（不变量 33/34 是事后验证义务；本 ADR 补事前保证义务）。
```

执法落点（Owner = Control，Control Intent Authority 语义内聚——
whether/what to do）：

```text
Control consumes Slice（目标 occurrence 的 state）
→ Satisfied      → 不签发 EffectIntent（decision outcome，不是 effect）
→ Unsatisfied   → 正常签发 act-intent（全链不变）
→ Unknown        → observe / resolve / safe-stop per policy（fail-closed）
```

realization（CDS-001）：occurrence 族携带可选 `State`（revision-local
presentation fact，随 occurrence 替换——不进 claim 演化域，ADR-0016 Revise
属 claim subject 域；null = Unknown ≠ false）；`TargetSpec.DesiredState`
（authoring 侧期望终态，非 null 才检查——Click 型 intent 无期望终态，不
适用）；`DescriptorTargetPolicy.Decide` 内三分支。

## Considered Options

- **canonical NoOp Effect**（Control 发 NoOp → EB 不 dispatch）：被拒——
  「无需行动」是 Control decision outcome，伪装成 effect 会污染 effect
  ontology（与 Click/Toggle 平级的假动作）。
- **Assurance 负责满足检查**：被拒——把 decision authority 向判断层挪半步；
  例外（Control 结构性无法获得该 state）保留但无启用情形。
- **EB / Driver 内检查**：被拒——P14 零 belief 感知；legacy
  `SemanticActionLowerer.Lower` 在 lowering 层混合状态检查是反面教材
  （职责归属 REJECT，正确性义务由本 ADR 迁移到 Control）。
- **靠 post-action verification 修复**：被拒——switch-stuck 后验证只能
  发现 false，tap 已把 true 破坏掉。

## Consequences

- desired-state 型 intent 的 satisfaction 检查是 Control policy 层职责
  （policy 产 proposal、签发权在 Control Loop——CTL-001 D9 不变）。
- occurrence State 的 epistemic 是 Known(value)/Unknown(null) 二态；
  Conflicting 属 claim 域（CLE-001 Revise/Conflict），occurrence-state
  模型不可达——claim 域冲突路径由 claim 语义承载。
- 「Operate」不因此成为 canonical noun（Human Gate HD-2：废弃为讨论标签；
  其候选需求映射到 Effect Boundary Dispatch、P14/P15、AttemptReport、
  Control Recovery 四个既有面）。
- 载荷字段 buyer 依据：ADR-0011（satisfaction 决策 = State 字段的真实
  buyer；GroundingView 不携带——grounding buyer 不需要）。
