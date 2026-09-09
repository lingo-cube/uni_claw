# CTL-001 — Control Loop 真实化最小 vertical（参考 policy + traversal 簿记 + 接地编排缝）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 05fd3e6e

## Intent（WHAT/WHY）
Control 是唯一没有真实 realization 的 L2（IControlPolicy 只有测试 doubles）；
重构后的 Slice occurrence 景观尚无真实 reader（ADR-0011 合规缺口）；intent →
ResolveCurrent → candidate → Act 的接地编排靠测试手工拼。本 change：产品侧
参考确定性 policy（首个真实 Slice reader）+ Control 侧最小 traversal 簿记 +
Kernel 最小接地编排缝，端到端真实资产场景验证。

## Scope
- `Control/`：`TargetSpec(Role, SemanticDescriptor?, EffectClass)` +
  `DescriptorTargetPolicy : IControlPolicy`（产品参考 realization，确定性）。
- `UniKernel`：`ActViaCurrentGrounding(intent, descriptor)` 组合缝 +
  `GroundedActResult(CurrentGroundingView, ActResult?)`（非唯一候选 → Act=null
  诚实返回，不强制选择）。
- 测试：T1–T7 场景（真实 corpus 优先：nav03 三按钮多目标 / golden-run 开关）。

## Out of Scope（禁止）
- EffectTargetCommitment demand 自动声明（显式 defer：P-UW-27 默认 fresh
  re-ground 已覆盖常见 retry；等 twin-retry 真实 buyer）。
- ControlLoop / ControlIntent / ControlDecision 签名改动；Assurance / EB /
  World 语义改动；objective→target-spec 的 authoring 语义（测试直接构造 spec）。
- Traversal View 协议载荷（deferred 21 不变）；协议 deferred ① 的最终裁决
  （本 change 只加组合缝，不改驱动权语义）。

## Decisions（Leader 预固定）
- D1 policy 行为：按 spec 顺序在 Slice.Occurrences 找 (Role 相等 ∧
  descriptor 相等[若给] ∧ container ∈ InScope) 且未 visited 的首个 occurrence
  → Act（TargetSubject = descriptor 序列化 "role[:desc]"，EffectClass 取 spec）；
  景观空或无可行动目标 → Observe。dispatch 失败恢复路径 = ControlLoop 既有
  强制 Recovery（零改动）。
- D2 traversal 簿记 = policy 内部 visited set，键 = (Role, SemanticDescriptor)
  （descriptor-keyed：occurrence id 是 revision-local 不能作键；这是 Control 侧
  hypothesis 簿记，非 world truth，referent 变化后误标可接受并注释）。
- D3 接地编排缝：UniKernel.ActViaCurrentGrounding = ResolveCurrent(descriptor)
  → UniqueCandidate 才构造 UiTarget candidate（SourceRevisionId 对齐）→ Act；
  非 Unique（No/Multiple/ScopeUnavailable）→ ActResult null + view 原样上抛，
  判定权在调用侧（不强制选择，P-UW-35）。
- D4 policy 状态仅由调用序列驱动（同序列同输出，replay 稳定）；无
  wall-clock/random。

## Acceptance
T1 golden-run 真实帧：policy 选中真实 switch occurrence → ActViaCurrentGrounding
   → Unique → canonical(TargetOccurrenceId) → dispatch → receipt
T2 nav03-parent 真实三按钮：多目标 spec 依序 A→B→C 三轮 act（每轮 revision
   advance + fresh grounding），visited 增长，不重复点击，完毕转 Observe
T3 景观空（无 occurrence 的 revision/Slice）→ Observe intent
T4 dispatch 失败 → 既有 Recovery 强制路径触发；新 revision 后 policy 续行
T5 双胞胎（同 role+descriptor 双 occurrence，构造或真资产）→
   ActViaCurrentGrounding 返回 MultipleCandidates、Act=null、零强制
T6 visited 跨 revision 存活（新 occ ids 不重置簿记）
T7 replay 确定性 + 既有 159 零回归

## Constraints
产品 diff = Control/ 新文件 + UniKernel 组合缝；ControlLoop 零改动；确定性；
真实资产优先、缺口如实标注。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案）+ 场景对照
  expected: T1–T7 GREEN；全解决方案全绿
  actual: >
    166/166 GREEN（Kernel 149 + Agent 17；新增 T1–T7，既有 159 零回归——Leader
    独立复跑确认）。产品面 = Control/DescriptorTargetPolicy.cs（TargetSpec +
    参考确定性 policy，Slice occurrence 景观第一个真实 reader）+ UniKernel
    GroundedActResult/ActViaCurrentGrounding 组合缝（非 Unique → Act=null 零强制）；
    ControlLoop/ControlIntent/ControlPolicy 零改动。真实语料真值探得：golden-run
    16 occ（icon×6/text_block×9/switch×1）、nav03 Role=Button descriptor=CHILD
    A/B/C。T2 三轮 act 依序 A→B→C + visited 跨 revision 存活（occ id 互异）；
    T4 失败→强制 Recovery→新 revision 续行；T5 双胞胎 MultipleCandidates 零
    dispatch；T7 双实例 replay 全等。偏离 3 条均合规（ownerless occurrence 与
    DeriveSlice 过滤的测试侧 owner 指派、幂等帧的 revision 推进方式、T4 断言
    用 occ id 沿载约定）。demand 自动声明维持 defer（D0）。
  evidence: dotnet test 输出（2026-09-08，两次独立运行）；git diff 抽审记录
```

## Status log
2026-09-08 · understanding→resolved · Control/Slice/GroundingSeam 实况确认；
  demand 自动声明显式 defer（D0 记录）
2026-09-08 · resolved→persisted→planned · state.md 建立；切片 = TargetSpec/
  policy → 接地缝 → T1–T7 → 全量回归
2026-09-08 · planned→implemented · 委派 fresh subagent：RED → DescriptorTargetPolicy
  + ActViaCurrentGrounding + T1–T7 → GREEN
2026-09-08 · implemented→reviewed · REVIEW：红线遵守（ControlLoop 家族零改动）；
  非 Unique 零强制（P-UW-35）；visited descriptor-keyed 合规 D2；偏离 3 条
  均为测试侧必要适配且如实标注
2026-09-08 · reviewed→verified→closed · Leader 独立复跑 166/166 GREEN + policy/
  缝 diff 抽审合规 → CONTROL_REFERENCE_REALIZATION_ESTABLISHED
