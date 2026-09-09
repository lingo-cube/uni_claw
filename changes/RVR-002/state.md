# RVR-002 — 第二轮双轴评审台账（修复待区可用 / 移交并行系列 / 裁决记录）
lifecycle_state: implemented · disposition: none · depth: standard · base: 1cdc4a74

## Intent（WHAT/WHY）
对 28ae4c36..1cdc4a74（7 提交：RVR-001/CLE-001/DSE-001/CDS-001/ESO-001/
ESO-002/DSE-002）的第二轮双轴评审产出（Standards 0 hard + 4 judgement；
Spec 3 缺失/半实现 + 3 可疑）。修复点均落在并行会话 in-flight 施工区
（LAT-001/DSE-003：WorldModel/UniKernel/Effects 全为未提交 M）——本台账
持久化发现与处置，待队列排空后执行修复项。

## 发现与处置
### 修复项（我方执行，待区可用）
- F1 **descriptor 匹配谓词统一**（Standards ①，漂移已发生）：提取
  WorldModel owner-side 匹配器供 `ResolveCurrent` 与
  `DeriveEntityObligationFactKind` 共用；container 条件语义显式参数化
  （in-scope membership vs 相等匹配），漂移消除 + 回归测试。
- F2 **DispatchOutcome「未确认完成」谓词共享**（Standards ②）：
  `RuntimeAssurance.NoteOutcome` 与 `ControlLoop.NoteDispatchOutcome` 的
  `DeliveryFailed or UnknownOutcome` 双态判定提取共享（judgement，低危）。

### 移交项（并行系列自查）
- H1 **CDS-001 S6 缺失**（Spec a1，最重要）：satisfaction Unknown 与
  FreshnessJudgment Unknown 的词汇隔离无测试无实现——归 CDS 系列。
- H2 CDS-001 S1 Acceptance 改写（二态降格已有书面裁决 D-复验-3，条目未同步）。
- H3 CDS-001 S4 显式三路 policy（Unknown → observe/resolve/safe-stop；现为
  隐式 Observe）。
- H4 set-switch≡tap 的语义差异测试（真机 buyer 落地时）。

### 裁决（不改码）
- R1 ESO-002 EntityFacts=null 与 Unknown 不可辨 = 可接受 v0（调用面唯一且
  集成测试覆盖；脆弱点记录，future buyer 再加诊断维度）。
- R2 DSE-001 S2 跨提交时序窗口（终态满足）= 接受，无追补。
- R3 (State, Locator) 尾随可选对模式 = 既有协议镜像模式，非新债。

### 回归核对结论（一轮修复）
RVR-001 F1/F2/F3 全部完好，后续 6 提交零破坏；P-UW-33 链 / State-claim
边界 / ESO-002 fail-closed 三项跨系核对全过。

## Acceptance
A1 F1 落地：三处匹配归一、语义参数化、漂移回归测试绿
A2 F2 落地（若评审维持）或记录放弃理由
A3 移交项 H1–H4 已在并行系列侧登记（本台账即为凭据）
A4 全量测试绿 + 显式路径提交

## Constraints
执行前置条件：并行 in-flight（LAT-001/DSE-003）落地、施工区文件恢复 clean；
显式路径提交；不动并行系列语义。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案）
  expected: A1–A4 满足
  actual: 待填（待区可用后执行）
  evidence: 待填
```

## Status log
2026-09-08 · understanding→resolved · 二轮双轴报告聚合（两轴分离呈现）；
  修复/移交/裁决三分类；执行前置 = 并行队列排空
2026-09-08 · resolved→persisted · 台账建立，等待排空哨触发执行
2026-09-08 · persisted→implemented（分区执行）· F2 落地（IsUnconfirmedOutcome
  谓词单点，两消费点等价替换，既有双态测试零改动全绿）；H1 落地（真 S6 词汇
  隔离两场景：Control 轴 evaluator 零调用 + JudgmentLog 零留痕 / Assurance 轴
  Freshness.Sufficient 不提升 entity Unknown、entity Unknown 不触发 freshness
  拒绝）；232/232 GREEN（Leader 独立复跑）。**F1 显式 pending**：其区域
  （WorldModel）正被 DSE-003 in-flight 编辑，index 手术存在被整文件提交覆盖
  的 clobber 风险——待 DSE-003 落地后执行，执行面 = 三处匹配归一 + 漂移
  回归测试。移交项 H2–H4 归并行系列（台账即凭据）。
