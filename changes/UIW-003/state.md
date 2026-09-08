# UIW-003 — UI Entity Model · Belief 侧实现（occurrence / LogicalItem / continuity / demand registry）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 9d7adc15

## Intent（WHAT/WHY）
把 UIW-002 冻结模型（UWM-009 v0.3 Part VI / ADR-0015 / ADR-0014 / P23）落进
WorldModel belief 侧：revision 内 ObservationOccurrences、LogicalItems、
continuity adjudication（四轴 + Ended 规则）、owner-internal demand registry。
纯增量（UIW-001 先例：无 strategy 的既有路径语义不变），零回归。

## Scope
- `src/UniClaw.Kernel/World/`：新类型（OccurrenceBelief / LogicalItemBelief /
  ProposedOccurrence / IUiObservervationStrategy / ContinuityDemand 族 /
  IContinuityStrategy / ContinuityResolutionOutcome 族 / ContinuityDecision log）
  + WorldModel 扩展（occurrence 派生入 revision、Register/Revoke demand、
  ResolveContinuity adjudication + mint 经 reconciliation commit）。
- `WorldBeliefRevision` 增可选 `Occurrences` / `LogicalItems`（默认 null，旧路径
  语义不变——Containers/Relations 同款先例）。
- 新测试文件（确定性 doubles），覆盖 S1–S15 场景（见 Acceptance）。

## Out of Scope（禁止）
- GroundingView / P23 出面 / ResolveCurrent / Slice 重构 / binding target shape
  （= UIW-004）。
- 触碰既有 view allowlist 锁定面、Control / EffectBoundary / Assurance 签名、
  Evidence Ledger、perception。
- EntityScopedObligation 物理入口；Container lifecycle Ended 判定源（级联规则
  实现但不可达即可）；跨 Container continuity。

## Decisions（Leader 预固定 realization 骨架，worker 可微调命名不改语义）
- 两个新 owner-internal seam（同 IAssociationStrategy 先例：确定性、无
  authority、proposal 须经 WorldModel gates）：
  `IUiObservervationStrategy.Derive(record, previous)` → occurrences（revision-local：
  每次派生自当条 evidence，替换而非继承）；
  `IContinuityStrategy.Propose(input)` → continuity proposal（支持/反对证据分携）。
- id 铸造：`occ-` / `li-` + evidence 内容派生前缀（同 MintContainerIdentity
  模式；算法 = realization，确定性 replay 稳定）。
- Demand registry = owner-internal 非 revision 化状态（List + 只读视图）；
  Register 幂等（同 DemandId 复用）；occurrence anchor 过期 → fail-closed 异常；
  descriptor-scoped 无 anchor 合法；Revoke 只删该 demand。
- ResolveContinuity：gates = ReferenceEstablished 需候选 occurrence + supporting
  ⊆ basis + 零 contradicting；SameReferent 需既有 item + 候选 + 同上；反证在场
  → Contradicted；无候选 → NoCurrentCandidate；歧义 → Ambiguous（不强制选择）；
  prior/demand-only 一律不 mint（P-UW-26/32）。mint/延伸 = 新 revision commit
  （basis 集合不变，仅 LogicalItems 变化）；无 belief 变化 → 不 commit（决策仍入
  ContinuityLog）。
- Ended：仅 strategy 提议 + supporting ⊆ basis 的 referent 终止，或容器缺失级联
  （v0.1 不可达，规则在即可）；Contradicted/NoCurrentCandidate/Ambiguous/
  Insufficient/demand 消失皆不 Ended。
- Maintenance：派生计算（active demand 引用 → Hot），不落 revision、不新增字段。

## Acceptance
S1 occurrence 派生 + revision-locality（不跨 revision 继承，id 每轮新铸）
S2 demand anchor 过期 → fail-closed；descriptor-scoped 可登记
S3 demand 登记不产生 revision（P-UW-32）
S4 ReferenceEstablished：demand + 充分 evidence → item 铸造 + revision commit + basis 不变
S5 demand 无证据 → Insufficient，无 item、无 revision 变化
S6 SameReferent：revision advance 后 scroll 帧 → continuity 延续 + basis 扩展
S7 recycled row 反证 → Contradicted，item 不 Ended、presence 不动
S8 无候选 → NoCurrentCandidate，零 belief/lifecycle 副作用
S9 双胞胎 → Ambiguous，不强制选择（Identity never creates information）
S10 referent 终止正证 → Ended(referent-terminated)；证据不足 → 保持 Established
S11 Revoke：demand 删除、item 留存、无 revision；Hot→Cold 派生正确
S12 状态变化（value/text 变）不终止 continuity（SameReferent 存活）
S13 replay 确定性：同输入同 id 同决策
S14 多 demand 同 item：撤一仍 Hot、撤尽转 Cold
S15 既有 125 测试零回归（无 strategy 构造路径行为逐字节不变）

## Constraints
新代码仅限 World/ + 新测试；不改 harness 层；不伪造证据；确定性 doubles。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案）+ 新测试文件清单
  expected: S1–S15 GREEN；既有 125 零回归；git diff 面仅 src/UniClaw.Kernel/World/ + tests/
  actual: >
    143/143 GREEN（Kernel 126 + Agent 17；新增 18 [Fact]：S1–S15 + S5b/S10b/S10c，
    既有 125 零回归——Leader 独立复跑确认）。变更面 = World/（WorldModel.cs、
    WorldBeliefRevision.cs 受控修改 + UiEntityModel.cs、Continuity.cs 新增）
    + tests/（UIWorldContinuityTests.cs、UIWorldContinuityDoubles.cs）。
    Leader diff 审阅：P-UW-32（demand 零 revision）、authority gates fail-closed
    降级、mint/延伸/Ended commit basis 不变、Contradicted 零副作用、无据终止
    静默不生效、container 级联规则在且注释不可达性——语义合规。
  evidence: dotnet test 输出（2026-09-08，两次独立运行）；git diff 审阅记录
```

## Status log
2026-09-08 · understanding→resolved · 权威已读（UWM-009 v0.3/ADR-0013/0014/
  P23/UIW-001/PER-002 实现）；realization 骨架由 Leader 固定（见 Decisions）
2026-09-08 · resolved→persisted→planned · state.md 建立；垂直切片 =
  types+seams → WorldModel 集成 → S1–S15 测试 → 全量回归
2026-09-08 · planned→implemented · 委派 fresh subagent（完整 spec）：RED 17 失败
  → UiEntityModel/Continuity + WorldModel 集成 → GREEN
2026-09-08 · implemented→reviewed · REVIEW：变更面零越界；无 strategy 旧路径
  逐字节不变（S15 + 125 零回归）；worker 偏离点 8 条均合规（pre-gate
  NoCurrentCandidate、Contradicted gate 补非空 supporting、Revoke no-op、
  registry 绑定结果 item、同 occurrence 重铸合并、id 派生模式、级联可达性
  选择、state 推进归 Leader——另修正 state.md 一处 ADR 编号笔误）
2026-09-08 · reviewed→verified→closed · Leader 独立复跑 143/143 GREEN + diff
  审阅语义合规 → UIENTITY_BELIEF_BASELINE_ESTABLISHED；UIW-004（缝与消费面）
  另立 change
