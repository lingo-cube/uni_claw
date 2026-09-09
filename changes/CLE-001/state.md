# CLE-001 — Claim Evolution realization（frame/producer 域 Revise + Reaffirm 落地）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 0a62a963

## Intent（WHAT/WHY）
UWM-009 §18 冻结了 Reaffirm/Revise/Supersede/Conflict/Withdraw 语义，但
realization 被 UIW-001 显式 defer（当时仅 owner-derived claims 走最小 Revise）。
PER-003 E2 实测暴露真实 buyer：scroll v1→v2 后 13 条行文本位移**永久入
Conflicts**（latest 不获胜，PER-002 冻结）——冲突单调累积、Slice 供 v1 旧值、
uncertainty 计数只增不减、目标 subject 的 HasConflictOnTarget 永久 fail-closed。
本 change 落地 frame/producer 域的 Revise/Reaffirm，消解呈现层再观察的冲突堆积。

## Scope
- `World/WorldBeliefRevision.cs`：WorldClaim 增可选
  `EstablishingProducer` / `EstablishingScope`（establishing record 的 provenance
  摘要，belief 侧溯源）+ 可选 `SupersededEvidenceIds`（Revise 痕迹链）。
- `World/WorldModel.cs`：Reconcile 的 ApplyClaim 演进裁决（规则见 D1）；
  owner-internal append-only `ClaimEvolutionLog`（同 AssociationLog/ContinuityLog
  先例，R-UW replayability 家族）。
- `docs/adr/0016`：Revise 域规则 ADR。
- 测试：CLE 场景 + 受影响既有断言迁移（见 D4）。

## Out of Scope（禁止）
- Supersede / Withdraw realization（各自无 buyer：整帧取代 / 负证据撤销——
  §18 语义在、realization 继续 defer）。
- Evidence Ledger 任何改动（supersession/降权 = 协议 deferred ⑧，不动）；
  Assurance / Control / Effects（并行会话施工面）；Conflicts 对不同 producer
  冲突的既有语义（保持）。
- UWM-009 文档改动（§18 语义已冻；realization 归本 change + ADR-0016）。

## Decisions（Leader 预固定）
- D1 演进域规则（ADR-0016）：同 subject 新 accepted evidence 到达时——
  ```text
  同值                                    → Reaffirm（belief 无变化，log 留痕）
  异值 ∧ 同 Producer ∧ 异 Scope          → Revise（新值生效；旧 EvidenceId 入
                                            SupersededEvidenceIds；establishing
                                            provenance 更新；不产生 Conflict）
  异值 ∧ 同 Producer ∧ 同 Scope          → Conflict（同帧内矛盾 = 真冲突，保持）
  异值 ∧ 异 Producer                     → Conflict（跨源矛盾，保持）
  ```
  语义依据：同 producer 的不同 scope 帧 = 同一观察流对呈现的再观察（presentation
  re-observation），revision 而非矛盾；跨 producer = 独立来源矛盾。producer/scope
  取自 establishing record 的 Provenance（belief 侧新增摘要字段承载）。
- D2 Revise 非「静默覆盖」：值替换 + 痕迹链 + ClaimEvolutionLog 三件套满足
  CONTEXT「Reconciliation 不静默覆盖」（双方/历代 evidence 均可溯源）。
- D3 Conflict 条目仅按 D1 产生；既有 Conflict 记录不受 Revise 影响（历史保留）。
- D4 迁移原则：受影响既有断言 = 明确依赖「同 producer 异帧异值 → Conflict」
  旧 realization 的测试（PER-002 S3 滚动行、PER-003 E2 的 13-conflict 断言等）——
  按「保护架构事实而非旧测试本身」迁移为 Revise 语义断言，逐处注明；其余
  断言零改动。

## Acceptance
A1 scroll v1→v2（真实语料）：行文本位移 → Revise（当前值=v2、Superseded 链含
   v1 evidence、零新 Conflict、ConflictingClaimCount 不增）
A2 Slice ScopedClaims 供 v2 值（stale 呈现消除）
A3 同帧内矛盾（同 producer 同 scope 异值）→ Conflict 保持
A4 异 producer 异值 → Conflict 保持
A5 Reaffirm：同值再观察 → belief 不变、log 留痕
A6 replay 确定性 + ClaimEvolutionLog 完整可溯源（subject → 历代 value/evidence）
A7 既有套件全绿（迁移项逐处注明）

## Constraints
Zone 限 World/ 两文件 + ADR + tests（显式路径提交，避开并行施工面）；确定性；
不动物理文档除 ADR-0016。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案）+ 迁移清单
  expected: A1–A7 满足；全解决方案全绿
  actual: >
    183/183 GREEN（Kernel 166 + Agent 17；净增 A1–A6 六条，Leader 独立复跑
    确认）。变更面 = World/ 两文件（WorldClaim 溯源字段 + ApplyClaim 四象限
    裁决 + ClaimEvolutionLog）+ ClaimEvolutionTests.cs（新）+ 两处 D4 迁移
    （S3 / E2——S7/Accepted5/ControlToEffect/Replayability 经核验不依赖旧规则、
    断言未动）+ ADR-0016。真实语料 A1：row_title 当前值=Item 02、Superseded
    链含 v1 evidence、零新 Conflict；A5 Reaffirm belief 零变化 + log 留痕；
    A6 三代演进全链溯源 + 双实例 replay。纯 checkout 一致性经 stash 并行面
    后复测确认（迁移后 E2 在本 commit 上独立成立）。RealAssetEntityModelTests
    与并行 in-flight 混合：变更行零 DSE 词汇，整文件随本 commit 入库。
  evidence: dotnet test 输出（2026-09-08，两次独立运行）；hunk 归属 grep
    （CLE 标记 5 / DSE 标记 0）
```

## Status log
2026-09-08 · understanding→resolved · buyer 证据（PER-003 E2 十三行冲突堆积）；
  §18 语义已冻、realization defer 台账确认；Revise 域规则固定（D1）
2026-09-08 · resolved→persisted→planned · state.md + ADR-0016 建立；切片 =
  WorldClaim 溯源字段 → 裁决规则 → log → CLE 测试 → 既有迁移 → 全量回归
2026-09-08 · planned→implemented · 委派 fresh subagent：A1–A6 RED → 四象限
  裁决 + log + 迁移 → GREEN
2026-09-08 · implemented→reviewed · REVIEW：红线遵守（World/ 两文件 + 5 测试
  文件内）；S7/Accepted5/ControlToEffect 族核验为真 Conflict 构造、断言未动；
  E2/S3 迁移确属旧规则依赖；偏离 1 条（A2 分区 scoped claim 采用 S8 先例约定，
  注明）
2026-09-08 · reviewed→verified→closed · Leader 独立复跑 183/183 + 纯 checkout
  stash 验证 → CLAIM_EVOLUTION_REVISE_DOMAIN_ESTABLISHED
