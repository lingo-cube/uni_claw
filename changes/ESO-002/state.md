# ESO-002 — Entity-Scoped Obligation Fulfillment（P23 producer ② 判定半边；deferred ⑭ 全闭）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: b2be8b4d

## Intent（WHAT/WHY）
ESO-001 落了 producer ② 入口（contract → standing demand → identity 绑定）；
判定半边仍缺：entity-scoped obligation（"该 referent 最终须达 RequiredState"）
的满足判定无路径。CDS-001 已交付 occurrence 族 `string? State`（Known/Unknown
二态、revision-local）。本 change 落 fulfillment：owner-derived tri-state fact
→ OutcomeAssuranceView 携带 → JudgeOutcome 实判 → terminal Completion 闭环。
deferred ⑭ 至此全闭。

## Scope
- `World/`：`EntityObligationFact`（Satisfied/Unsatisfied/Unknown）+
  `DeriveOutcomeAssuranceView` 增实体事实输入与 `EntityFacts` 输出（纯派生，
  零 commit、零 strategy 调用）。
- `Assurance/RuntimeAssurance.JudgeOutcome`：entity-scoped mandatory
  obligation 的 fulfillment = fact==Satisfied；Unknown 如实未满足（不伪装失败
  也不伪装满足）。
- `UniKernel.EvaluateTerminal`：obligation (id, EntityScope, RequiredState)
  传入 view 派生（组合适配）。
- view allowlist 测试更新（OutcomeAssuranceView +EntityFacts；显式解锁，机制
  不变，RVR-001 先例）。
- 协议 P23 Status 一句 + UWM-009 §33 deferred 20 一行注记（入口+判定均落地）。
- CONTEXT.md Obligation Fulfillment 词条补 entity-scoped 一句。
- 测试：F1–F6（新文件）。

## Out of Scope（禁止）
- SameReferent 严格性内嵌于 fact（v0 = descriptor 帧内唯一匹配 + Unknown
  fail-closed；continuity 严格判别仍走 ResolveContinuity，verification 时点
  不重复 adjudication / 不 commit）。
- Evidence Ledger / Supersede/Withdraw（协议 deferred ⑧）；Control/Effects/
  DescriptorTargetPolicy（CDS/DSE 已闭面，不重开）；obligation 撤销语义。

## Decisions（Leader 预固定）
- D1 fact 纯派生 tri-state：current occurrences 按 (Role ∧ descriptor[若给] ∧
  container[若给]) 匹配——恰一且 State==required → Satisfied；恰一且 State
  有值但≠required → Unsatisfied；零候选 / 多候选 / State==null → Unknown
  （fail-closed，Identity never creates information）。
- D2 判定集成：JudgeOutcome 对 entity-scoped mandatory obligation 的满足
  条件 = EntityFacts[obligationId]==Satisfied；fulfillment 结果沿既有
  obligation status 机制回填（非 entity 路径零改动）。
- D3 v0 verification 不调用 ResolveContinuity（view 必须纯派生；continuity
  判别语义已在 identity 建立时消费，此处 descriptor 唯一性 + Unknown 兜底）。
- D4 allowlist：EntityFacts 为新白名单成员。
- D5 确定性；纯 owner fact（不携带 consumer judgment——Satisfied 是 belief
  fact，fulfilled 判定权在 Assurance）。

## Acceptance
F1 fact tri-state 五情形（satisfied / unsatisfied / 零候选 / 多候选 / state null）
F2 OutcomeAssuranceView 携带 EntityFacts；allowlist 更新后全绿
F3 JudgeOutcome：Satisfied → fulfilled；Unknown → 未满足且不伪装失败
F4 E2E 正向：switch State off→on 帧 + ESO-001 demand + act → post-action
   on → EvaluateTerminal → Completion（obligation fulfilled 入 proof）
F5 E2E 负向：state 不变 off → 无 completion proof（保持 non-terminal 或
   Failure 按既有分类语义，如实断言）
F6 既有 197 零回归

## Constraints
Zone：World/（fact+view）+ Assurance + UniKernel + allowlist 测试 + 新测试 +
两处文档一句/一行；显式路径提交；确定性。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案）
  expected: F1–F6 满足
  actual: >
    本 change 独立验证 203/203 GREEN（197 基线 + F1–F5 六条；stash 往返：剥离
    并行 LAT-001 in-flight 后全绿——工作树中 2 个 RunTraceBullet 失败均由
    LAT 未提交改动所致、与本 change 无关，归 LAT 会话）。变更面 = 纯我方
    5 文件（RuntimeAssurance/ConsumerViews/WorldModel/RuntimeViewExposureTests/
    新测试）+ 3 处文档一句/一行 + UniKernel 单 hunk（EvaluateTerminal 实体
    传参——LAT-001 施工致该文件重度混合，index 手术式选择性暂存，其余 10
    hunks 留工作树归 LAT）。JudgeOutcome 集成 = EvaluateObligations entity
    分支（Satisfied→fulfilled、BackingEvidenceId=null；Unknown 如实未满足）；
    非_entity 路径逐字未动（null 防御为 no-op）。F4 E2E 全链：contract
    entity-scoped → demand → DesiredState policy 签发 → dispatch → post-action
    on 帧 → EvaluateTerminal → Completion（obligation satisfied 入 proof）；
    F5 负向 off 帧 → 无 proof、保持 non-terminal。
  evidence: dotnet test 输出（stash 前后各一次）；hunk 归属 python 核验输出
```

## Status log
2026-09-08 · understanding→resolved · CDS state 载荷实况确认（occurrence 族
  State 二态）；fact 纯派生 tri-state 裁决固定（D1–D5）
2026-09-08 · resolved→persisted→planned · state.md 建立；切片 = fact → view →
  judgment → kernel 适配 → F1–F6 → 全量回归
2026-09-08 · planned→implemented · 委派 fresh subagent：F1–F5 RED → fact+view+
  judgment+kernel → GREEN（偏离 3 条合规：终局断言等价替换、BackingEvidenceId
  null 防御、UniKernel 并行编辑重读重试）
2026-09-08 · implemented→reviewed · REVIEW：非 entity 路径零语义改动；view
  纯派生（零 commit/零 strategy）；allowlist 机制不变；UniKernel LAT 混合 →
  hunk 手术裁定
2026-09-08 · reviewed→verified→closed · Leader stash 往返验证 203/203 +
  hunk 归属核验 → ENTITY_SCOPED_FULFILLMENT_ESTABLISHED（deferred ⑭ 全闭；
  Q1 secondary buyer 终局）
