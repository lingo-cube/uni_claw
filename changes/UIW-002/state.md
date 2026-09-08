# UIW-002 — UI Entity Model & Slice Semantics（Explore 冻结 → 权威文档修订）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 661e3932

## Intent（WHAT/WHY）
UIW-002 Decision-Heavy Explore（Stateful Grill，Q1–Q6 + G2 修正 + G1 接受，
2026-09-08）产出候选冻结记录 v1.1：UI entity 三层身份模型（ContainerIdentity /
ObservationOccurrence / LogicalItem）、continuity demand 缝（S3′→P23）、词汇归属、
消费面（Slice / GroundingView）。本 change 把该冻结**窄增补**进权威文档
（UWM-009 v0.3、协议基线、CONTEXT.md）并立 2 个 ADR；不写产品代码。

## Scope
- `docs/adr/0015`（LogicalItem 身份模型）+ `docs/adr/0014`（continuity demand 缝）。
- UWM-009 v0.3 窄增补：header/§2.1/§9（输入 (iv)）/新增 §35–§41/§30/§31（P-UW-24..35）/
  §33（deferred 20–27）/§34（v0.3 freeze statement）。
- 协议基线：Protocol Map + P23 新边 + P4/P9/P10/P11 v0.3 注记 + deferred 表增补。
- `CONTEXT.md`：新增 "UI Entity Model & Continuity" 词条族 + 既有词条小修
  （Slice / Consumer View / Candidate Binding / Canonical Binding）。

## Out of Scope（禁止）
- 产品代码与测试（实现切片 = belief 侧 / 缝与消费面，另立 change，B/C 拆分见 Plan）。
- EntityScopedObligation 的 P23 物理入口；Traversal View 载荷；Resource 实体化；
  perception-side projection；跨 Container LogicalItem continuity（各自 deferred）。
- 重开 UWM-009 v0.2 已冻结语义（Container Association / P22 / Slice 既有 invariant）；
  重开 L0–L3 基线。
- 触碰他人 in-flight 文件（RUN-001 / TRC-001 / src/.../Run|Trace）。

## Decisions（Explore 冻结记录 v1.1 摘要；全文见 grill 会话）
- 三层模型：ContainerIdentity（长期）→ LogicalItem（Container-scoped、demand-gated、
  evidence-established、bounded、仅 actionable target）→ ObservationOccurrence
  （revision-local）。provider node id / bbox / OCR / DOM / UIA / detection id = evidence only。
- 四轴不混：Lifecycle(Established/Ended) × ContinuityAdjudication(SameReferent/Ambiguous/
  Insufficient/Contradicted) × Presence(复用 UWM-009 §8 词汇) × Maintenance(Hot/Cold，
  owner-internal 非 truth)。
- Ended 仅两类正面 lifecycle evidence（referent 终止/排他替代；owning Container
  canonical lifecycle Ended 级联）；Ambiguous/Insufficient/New/Absent/Contradicted/
  demand 消失六者 ≠ Ended；状态变化 = claim，不终止 continuity。
- ReferentBasis = 可修订 evidence/belief basis，禁复合 identity key；
  presentation continuity 至多是证据；Identity never creates information。
- S3′ 单缝双模：ResolveCurrent（只读）/ ResolveContinuity（声明+判别）；
  demand 非 evidence、不产生 revision、不建立身份；producer 封闭
  （EffectTargetCommitment / EntityScopedObligation-语义保留）；Control 仅引用；
  timing fail-closed；多 buyer demand 生命周期；DemandHandle = opaque token。
- 词汇：新增 canonical noun 恰两个（ObservationOccurrence / LogicalItem）；
  CurrentCandidateSetResult 四值全名；CanonicalBinding 保持唯一 noun（UI target
  shape 为载荷形态）；Avoid：UIEntity/InteractiveEntity/Element/LocalModel/
  Region-as-entity/Resource(v0.1)/Dissolved/Demand-Minted。
- 消费面：Slice = Control consumer view（scope = RootContainerIdentity + buyer-required
  InScopeContainerRefs；多屏/多区域/横纵混合）；GroundingView = P11 族新成员
  （P23 出面）；UI effect 恒绑 CurrentOccurrenceRef，LogicalItem 永不直接 dispatch；
  UI 字符串寻址退役（不得作 fallback），非 UI 字符串通道暂留待 buyer audit。
- 执行顺序（G2 修正）：Control Intent → ResolveCurrent/ResolveContinuity →
  GroundingView → CandidateBinding → CanonicalBinding → Assurance(三元组) →
  Gate/Dispatch（与 ADR-0009/CBA-005 既锁次序一致）。

## Assumptions
- Q1 buyer 裁决成立：EffectTargetCommitment 显式 same-referent requirement 为主 buyer；
  普通 revision advance 后的 fresh re-ground 不是 LogicalItem buyer（默认路径原则）。
- Container canonical lifecycle Ended 判定当前不存在（级联触发源 deferred 26），
  v0.1 内该触发不可达，无害。
- 行业类比（Playwright Locator / RecyclerView stable IDs / OSWorld·UI-TARS）仅为
  rationale provenance（Authority: NONE），产品语义独立成立。

## Alternatives（被拒，理由见 ADR-0015/0014 与 grill 记录）
- P1 act-minted / P2 affordance-minted / P3 双 trigger 晋升（语义错误或抽象膨胀）。
- 统一 WorldEntityGraph（A）与 ContainerGraph/EntityGraph 并列（B）：元素身份通胀 /
  无 buyer 双图；C（Container anchor）以收窄形态吸收进三层模型。
- S1 独立 standing demand 边 / S2 查询隐式 mint / Control mint 权。
- 通用 Slice dump / claim-flat Slice（provider-key 反模式）/ Slice 顺带装 grounding /
  NoCandidate 折叠进 Insufficient / dispatch LogicalItem / 纯连续链身份 /
  描述性身份 / 静默消失 / Anchor×Role 复合键。

## Owner-Authority impact
- World Model 新增 canonical 语义（ObservationOccurrence / LogicalItem / continuity
  adjudication / demand eligibility registry）——**无新 L2 Owner**；demand registry 与
  Maintenance 为 owner-internal 非 truth 状态。
- Capability Plane 权威零变化；Grounding Provider 成为 GroundingView 合法只读
  consumer（P11 清单扩展）。
- EB Canonical Binding Authority 不变；Assurance 判定次序不变（Bind→Judge→Gate）；
  ADR-0011 consumer view 四原则延伸适用 GroundingView。

## ADR refs
0015（LogicalItem 身份模型）· 0014（continuity demand 缝）；关联 0010/0011/0012。

## Residual risks
- P23 / GroundingView / LogicalItem 均无当前实现（target 锁定）——实现切片须防
  词汇漂移（判别结果必须带类型限定）。
- UWM-009 §9 输入清单修订后，未来实现须在 WorldModel 边界执法 demand 非 evidence
  纪律（同 P22 执法先例）。
- Container lifecycle Ended 判定源缺失 = 级联规则暂不可触发（显式记录，不伪装存在）。

## Acceptance
1. UWM-009 header = FROZEN v0.3（provenance 增补）；§9 含输入 (iv)；§35–§41 存在；
   §31 含 P-UW-24..35；§33 含 deferred 20–27；§34 含 v0.3 freeze statement。
2. 协议基线 Protocol Map 含 P23 行；P23 完整边规格存在；P4/P9/P10/P11 各含 v0.3
   注记；deferred 表含 ⑭⑮。
3. CONTEXT.md 含 UI Entity Model & Continuity 词条族（7 词条 + Avoid），且无实现
   细节；Slice / Consumer View / Candidate Binding / Canonical Binding 词条已同步。
4. docs/adr/0015、0014 存在且符合 ADR 格式。
5. v0.2 已冻结正文保持原样（窄增补/注记式，无历史改写）。
6. 产品代码 / 测试零改动；RUN-001 / TRC-001 in-flight 文件零触碰。
7. 跨文档一致：无 "P1–P22 封闭" 类残留声明与新 P23 矛盾；无悬空 §引用。

## Constraints
外科化修订（append/注记优先，不重排既有 §编号——外部引用依赖 §12.1/§13/§21 等）；
CONTEXT.md 仅 glossary；新条目日期 = 2026-09-08（系统时钟）。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: >
    grep 核对：P-UW-24/35 于 uworld-protocol-baseline-l4.md；P23 于协议基线
    map + 逐边；§35..§41 存在且唯一；CONTEXT.md 新词条存在；git diff --name-only
    仅限 docs/ + CONTEXT.md + changes/UIW-002/；grep -n "P1–P22" docs/ 查封闭式
    残留矛盾；§编号唯一性核对。
  expected: 验收 1–7 全满足；diff 文件面 = 5（uworld-l4 / 协议基线 / CONTEXT.md /
    adr/0015 / adr/0014）+ changes/UIW-002/state.md。
  actual: >
    git 变更面 = M(CONTEXT.md / 协议基线 / uworld-l4) + ??(changes/UIW-002/、
    adr/0015、adr/0014)，产品代码 / 测试零改动（并行会话 RUN-001/TRC-001 已
    自行提交，零触碰）。P-UW-24..35 命中 19；§35–§41 各存在一次（§号无重复）；
    P23 于协议基线 5 处（map/逐边/P9/P10/P11/deferred ⑭）+ UWM-009 11 处；
    CONTEXT.md 七词条各唯一（Consumer View 去重后 1）；封闭式 "P1–P22" 残留
    = UWM-009 §2.1 标题 1 处 → 已修为 P1–P23，复查清零；文档尾部结构 =
    §34 v0.2 冻结块（原样）→ v0.3 冻结声明 → Part VI §35+。验收 1–7 全满足。
  evidence: grep/git 输出（2026-09-08 执行，见会话记录）；UWM-009 2022 行
```

## Status log
2026-09-08 · understanding→resolved · grill 会话已完成权威研读（基线/UWM-009/
  协议基线/CONTEXT/UIW-001/PER-002/实现），Q1–Q6+G2+G1 全部人工裁决，无阻塞未知
2026-09-08 · resolved→persisted · state.md 建立（DECISION-HEAVY）；PLAN：垂直切片
  = ADR×2 → UWM-009 v0.3 → 协议基线 → CONTEXT.md → 一致性验证
2026-09-08 · persisted→implemented · ADR-0015/0014；UWM-009 v0.3（header/§2.1/
  §9(iv)/§30/§31 P-UW-24..35/§33 20–27/§34 v0.3 声明/Part VI §35–§41）；协议基线
  （map+P23 逐边+P4/P9/P10/P11 v0.3 注记+deferred ⑭⑮）；CONTEXT.md 词条族 + 4 词条
  同步（Consumer View 插入重复 1 处，发现后即去重）
2026-09-08 · implemented→reviewed · REVIEW：无越界（产品代码/测试/in-flight 文件零
  触碰；v0.2 正文原样，全部窄增补/注记式；CONTEXT.md 无实现细节）；中途协议基线被
  并行会话（RUN-001，P18 RunId 注记）修改——重读后基于最新实况增补，零重叠零覆盖
2026-09-08 · reviewed→verified→closed · grep/git 一致性验证全过（验收 1–7）；
  §2.1 标题 P1–P22→P1–P23 修正；本 change 收口。实现切片（belief 侧 / 缝与消费面）
  另立 change 走 UniFlow
2026-09-08 · closed→resolving（并行落库冲突：TRC-001 已占用 ADR-0013；UIW-002
  identity ADR 的编号不再唯一，Acceptance 4 与跨文档引用失效）
2026-09-08 · resolving→implemented→reviewed→verified→closed · 保留已提交
  Trace ADR-0013 与 Continuity Demand ADR-0014；UI Element Identity ADR 外科
  重编号为 ADR-0015，并同步 state / UWM-009 / 协议基线 / ADR-0014 引用；
  产品代码与冻结语义零改动，唯一编号与无悬空引用复验通过
