# 0014 — Continuity demand 是经单一双模缝（P23）进入 World Model 的 non-evidentiary 输入

ADR-0013 的 demand-gated 规则需要合法 ingress：UWM-009 §9 冻结的 association
合法输入（accepted evidence / previous revision / P22 prior）没有任何边能携带
"消费者要继续引用同一 referent"。UIW-002 grill Q4（2026-09-08）裁决 S3′：不建
独立边、不靠查询隐式推导，单一 continuity request seam 承载两种显式分离的副作用
契约。本 ADR 是 ADR-0012（P22）同族的输入边窄修正。

## Decision

```text
单缝双模（continuity request seam = P23）：
ResolveCurrent    = 只读 current-revision resolution；不铸 LogicalItem、
                    不登记 demand
ResolveContinuity = 声明 same-referent continuity demand；登记/延续 eligibility；
                    基于 accepted evidence 执行 continuity adjudication
禁止实现成含义模糊的 resolve(..., track=true/false)。

Demand 不变量（P22 族）：
≠ EvidenceRecord；不建立 identity；不强制 Matched/New；不修改事实 claims；
不直接证明 Presence/Lifecycle；demand 状态变化不产生 WorldBelief revision
（若激活对既有 accepted evidence 的 reconciliation 且 belief 变化，由该
reconciliation commit 产生 revision）。

Timing：demand 必须在源 occurrence 仍属 current revision 时登记；过期后
occurrence-anchored 登记 fail-closed；descriptor-scoped 回退 = 新 demand，
不继承过期 occurrence 的 ReferentBasis / continuity；standing demand 可早于
identity（obligation 先于首次观察），但不能代替 identity evidence。

生产者封闭（ContinuityDemandSourceKind，合法性由调用端口 + 运行时 authority
验证，不信载荷自述）：
EffectTargetCommitment   = EB bind/re-bind 路径（主 buyer 入口）
EntityScopedObligation   = contract 侧 entity-scoped obligation（语义保留，
                           物理入口 deferred——无 buyer 不建空协议边）
Control / traversal 仅引用既有 LogicalItem，无 mint 权；UniAgent 不直连。

多 buyer 生命周期：DemandId / DemandHandle（opaque correlation token，非
identity 非 truth）；按 producer 撤销；active demand > 0 → 可维持 Hot；
last revoke → 仅转 Cold。Revoke 结束 demand 本身：不删除 LogicalItem、
不改历史 belief、不产生 Ended。
```

## Considered Options

- **S1 独立 standing demand 边**：被拒——与 S3 语义相同却多一条边；缝的数量应最小。
- **S2 无边（查询隐式 mint）**：被拒——mint 时机晚于 occurrence 过期，continuity
  链断在消费者侧（E6 失守）；entity-scoped claims 失去稳定 subject（退回描述寻址）；
  读模式变成写侧状态的结构性 smell。
- **Control 持有 mint 权（traversal bookkeeping 触发）**：被拒——E5：Control may
  reference, never own；且遍历簿记会按列表规模铸造身份。
- **Demand 作为 EvidenceRecord 走 P2/P3**：被拒——同 ADR-0012 拒绝理由：会把
  消费者需求升级为 world evidence，破坏 Admission ≠ Truth 链条。
- **attempt-correlated = strong establishment evidence**：被拒（grill 中段提案，
  已撤回）——attempt correlation 只帮助 WorldModel 知道在追踪哪个 referent
  （attention/routing prior），continuity 的建立与维持永远需要 accepted
  observation evidence。

## Consequences

- UWM-009 §9 合法输入新增 (iv) ContinuityDemand（non-evidentiary）；协议基线
  新增 P23 边 + Protocol Map 行；GroundingView 作为 P11 族新成员是该缝的出面
  形态（Grounding Provider 成为合法只读 consumer）。
- ContinuityResolutionOutcome = ReferenceEstablished | ContinuityAdjudicationOutcome
  (SameReferent/Ambiguous/Insufficient/Contradicted) | NoCurrentCandidate；
  与 Container Association 是两个判别过程、两套词汇，trace 必须带类型限定。
- Contradicted 只证明 current candidate ≠ existing LogicalItem，不终止 item。
- 无当前实现；P23 / GroundingView / demand registry 实现随 UIW-002 vertical
  slices 另立 change 走 UniFlow。
