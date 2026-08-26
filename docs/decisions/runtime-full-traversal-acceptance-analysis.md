# Runtime Full Traversal Acceptance Gate — Reality Analysis + Acceptance Design

DocumentType: `REALITY_ANALYSIS_AND_ACCEPTANCE_DESIGN`
Decision: `PROJECT_LEADER_RUNTIME_FULL_TRAVERSAL_ACCEPTANCE_GATE_ANALYSIS`
Date: 2026-08-26
Basis: 冻结基线（Phase 2 + 2.5 GRADUATED/ARCHIVED）上的源码级审计 + 既有毕业证据盘点
Status: **分析完成，停止于实现之前**（未改 Runtime、未建 OpenSpec、未入 Phase 3）

---

## 1. Decision

**结论先行：剩余 Gap 是 validation-only gap（验收缺口），不是 Runtime capability 缺口。**
提出最小 Full Traversal Acceptance 方案（validation-only，复用已毕业 harness 模式与已购买
contract），并把 Roadmap 位置定为 **Phase 2.6（独立 Acceptance Gate，非虚假架构阶段）**。
进入实现前需要一次 Human Gate（原因见 §15——不是买能力，是授权一次真机验收战役与
它的新 fixture 语义绑定）。

## 2. Human-readable Reality Analysis

Phase 2.5 的 8/8 证明了什么：抽象 Strategy → 真实 Android UI 的**纵向执行链**（一个
Run 内：观察→接地→授权→执行→验证→完成），在**一个真实 ScrollView 根 + 8 个已知形态
子页**上成立，含滚动联合、popup 阻挡处置、跨 Run 适应。

它**没有**证明什么：真实未知**递归树**的 exhaustion。8/8 的树是单层（root→children），
子页是 record-only leaf——**递归下降（parent push/pop、深度 2/3 的 verified return 再入、
兄弟 frontier 续行）在 Tier B strategy 路径上从未执行过**。这个差别是本质的：递归路径上
的 identity/revisit/return/depth-boundary 语义只在 Tier A 确定世界和 legacy 真机测试中
被证明过。

## 3. Expected Reality（买方问题的正面表述）

RuntimeAgent 在无 Emulator Run 内接管、无预编译路径、无 Memory 下，对真实未知 bounded
UI scope：自己发现 inventory；按已毕业 semantics 递归展开 container / 记录 leaf；滚动后
新节点进入同一 interpretation；已访问节点不被重复计算；verified return 后续行 frontier；
depth 边界形成 record-only/unknown-frontier；unresolved 不被吞；external boundary 不越界；
pending 耗尽或 bounded fail-closed；Ledger 与 Evidence 可对账；completion 仅由
GoalEvidence+FSM 建立。

## 4. Observed Reality（能力矩阵盘点，源码级）

| 能力 | 符号证据 | 已验证层 |
|---|---|---|
| 递归下降 + verified parent return | `Agent.OpenWorld.cs` parents Stack + `TryPerformVerifiedParentReturnAsync`(×4 调用点) | Tier A + **legacy 真机 depth-3**（SettingsTreeCapstone GRADUATED, maturity `SETTINGS_TREE_CAPSTONE_PROVEN`） |
| 滚动 exhaustion（viewport union/overlap） | `ExploreCurrentContainerViewportsAsync` + `SourceEquivalenceNormalizer` | Tier A + Tier B strategy 路径（8/8 根页滚动） |
| depth 0/1/N 语义 + fail-closed cutoff | `ExplorationExecutionSemantics`（RESAR admission 派生） | Tier A 确定世界（DepthBoundaryTests） |
| unresolved/unknown-frontier 记账 | RESAR identity-exact ledger（unresolved 进 discovered；frontier=visited 重叠注释） | Tier A（identity 断言） |
| external boundary | EBD boundary obligation/disposition | Tier A + 既有真机 EBD 毕业 |
| popup 阻挡处置 | popup 页 OCR 消歧 + fail-closed normalization | Tier B（popup 子页真实通过） |
| Strategy 路径直达递归深度 | `RunStrategyOpenWorldAsync(..., semantics)` → `RunOpenWorldAsync(maximumDepth...)` | **仅 depth≤2 确定世界**；真机 strategy 路径只有 depth-1（8/8 单层） |
| 真实 Settings 语义绑定 | `SettingsSemanticCapability`（生产库 UniClaw.Semantic.Settings；en-US/GB 容器/行/返回 5 meanings） | legacy 真机 depth-3（GRADUATED） |
| depth 上限 | `MaximumSupportedDepth=64`（冻结契约内） | admission 校验层 |

## 5. Reality Gap

**真实未知递归树的 strategy 路径验收从未发生。** 具体缺三件（全部是"没跑过"，不是"没有"）：
1. strategy wire 上 depth≥2 的真实递归 Run；
2. 真实 Settings（非 fixture app）经 `SettingsSemanticCapability` 生产绑定的 strategy Run；
3. 未知树上的 exhaustion 对账（Ledger vs 实际 Evidence vs Scenario Acceptance 三方）。

## 6. First Divergence Point

**"bounded fixture / Real Emulator 8/8" → "真实未知 recursive exhaustion" 的 FDP**：
`TierB RealityFixtureStrategyBinding` 只构造单层树语义（root 的 inventory=Child 行；
child=record-only leaf）——即 Phase 2.5 的 harness 语义绑定**从未声明过递归 intent**，
Runtime 的递归机器因此从未被 strategy 路径触达。归类：**validation-only gap**
（binding 表达力问题，不是 Runtime 缺陷——递归机器在同一冻结代码里被 legacy 真机
depth-3 毕业覆盖）。

次级 FDP（验收设计必须覆盖的真实现实难点，预判为 harness 语义绑定工作量而非 Runtime）：
真实 Settings 的 duplicate/similar 语义节点（同名行跨容器）、跨容器 identity 去重、
revisit 语义——这些在 legacy 真机毕业时由 `SettingsSemanticCapability` 处理过，但从未与
RESAR 的 identity-exact ledger 语义在 strategy 路径上组合。

## 7. Full Traversal Capability Matrix（§4 压缩为判定）

Runtime 具备（冻结代码、部分已毕业）：递归、return/re-entry、scroll exhaustion、depth
语义、unresolved 记账、boundary、popup 处置、GoalEvidence/FSM completion。
验收缺口（从未组合执行）：strategy 路径 × 真实 Settings × depth 2–3 × 生产语义绑定 ×
exhaustion 对账。
缺失的 Runtime capability：**无**。

## 8. Level 1–4 Acceptance Design

通用骨架（全部 validation-only，复用已毕业 harness 组件：EmulatorDriver→ResultCollector
→BoundaryVerifier→Gates→Scenario Acceptance 双层判定）：

- **L1 真实 Settings depth=1 exhaustive**：Emulator 产 ExploreScope directive（scope=
  `com.android.settings` / root=解析出的 Settings 主页语义名）；Runtime 自主滚出根页
  inventory。证明 1/2/3/4/9/11/12/13。
- **L2 depth=2 recursive**：根 container 展开 → 子 container 递归 → verified return →
  兄弟续行。证明 5/6（depth 边界 record-only）/1/2/4。预判主要Harness工作：真实 Settings
  子页的 container 判定语义（哪些子页是 container vs leaf）——由
  `SettingsSemanticCapability` meanings 供能，binding 只做选择，不发明。
- **L3 depth=3 recursive（压力级）**：嵌套 container + 滚动 + 重名节点 + popup/obstruction
  + unknown/unresolved + external boundary + viewport 变化 + revisit。证明 3/4/6/7/8/14。
  每一项都是已毕业能力的真实组合，验收关注**组合下的对账**而非单项。
- **L4 Full bounded traversal**：由冻结契约决定合法上限——`MaximumSupportedDepth=64` 是
  上限不是目标；真实 Settings 树深约 3–4，**L4=depth 取真实树深的全树 exhaustion**
  （预计 depth 4–5 声明即可覆盖物理全树，含 System 面板）。L4 通过即
  `RUNTIME_AGENT_CAN_AUTONOMOUSLY_EXHAUST_A_REAL_BOUNDED_UI_TREE` 可声明。
  不引入 Dynamic Depth/Planner/Memory（禁止清单原文遵守）。

分级串行；任一级出现 Runtime/Contract 边界问题即停（同 Phase 2.5 纪律）。

## 9. Evidence Requirements（每级）

Tier B 式全量：device/serial/版本/时间戳、Goal、Directive、StrategyId/RunId、admission、
Emulator call log（恰 N 次 start）、Runtime events、EvidenceRefs、ExplorationLedger
（Tier A 读模型不可用于真机——按 wire tier 真相性：ledger 级 unavailable 如实记录，对账
用 events+snapshot+Scenario Acceptance 三方）、terminal、终态 screenshot、
Scenario Acceptance（外部独立读数）。额外：**每级记录 visited-set 对账**（Runtime 报告
的已访问语义页集合 vs 独立 OCR/uiautomator 侧读数——uiautomator 仅测试侧对账，绝不入
Runtime perception）。

## 10. Pass / Fail Criteria（可证伪）

十四条验收断言逐条对应 §8 各级（映射已标注）；最终 claim
`RUNTIME_AGENT_CAN_AUTONOMOUSLY_EXHAUST_A_REAL_BOUNDED_UI_TREE` 成立当且仅当 L4 全过 +
回归绿 + 双不等式保持。任一断言失败→ Evidence-Driven（FDP→Owner→分类），禁止猜修；
若 FDP 落到 Runtime 生产 owner → STOP（§12 逻辑）。

## 11. Existing vs Missing Capability

Existing：全部遍历机器（§4 表）。Missing：**零 Runtime capability；零 contract 变更**。
缺的是 acceptance 执行 + 一套真实 Settings 的 Tier-C 级验证 harness 组合（含
`SettingsSemanticCapability` 经 `IStrategySemanticCapabilityBinding` 的 strategy 侧
适配——注意：该适配是 validation harness 侧 binding（同 Phase 2.5 模式），Runtime 无感）。

## 12. Whether Runtime Modification Is Required

**判定：NO（基于现有证据）。** 递归机器在同一冻结代码上被 legacy 真机 depth-3 毕业。
保留的停止条件：若 L2–L4 中 FDP 落入 Runtime 生产 owner（例如 strategy semantics 与
legacy 深度路径的真实分歧——预判不存在，因 RESAR 已把 semantics 注入同一
`RunOpenWorldAsync`），立即 `STOPPED_AT_<reason>` → Human Gate，不修代码。

## 13. Roadmap Position Recommendation

**插入为 Phase 2.6：Runtime Full Traversal Acceptance（独立 Acceptance Gate）。**
理由：它不是新的架构能力阶段（不购能力、不改架构），但也不从属于 Phase 2.5（对象从
"emulator 驱动链"换为"未知树 exhaustion"）；命名沿用 Phase 2.x 语义（有买方问题的
验证收口）。序列：Phase 2 → 2.5 → **2.6 Acceptance** → Phase 3 Memory → Phase 4。
本分析即其 Phase 0（reality analysis）；Roadmap 的 `PHASE3_MEMORY_HUMAN_GATE` 移至
2.6 之后（仅顺序，Phase 3 内容不变）。

## 14. OpenSpec Requirement

需要**一个新的 validation-tooling change**（同 Phase 2.5 形态：additive、
NONE_RUNTIME、harness-only），包含：四 Level 场景、acceptance semantics、双层判定、
真机设备 gate、claim/non-claims 冻结。**本轮不创建**（指令禁止）；设计要点已在本文件，
proposal 起草是 Exact Next Step 的一部分。不修改任何已归档/主 spec。

## 15. Human Gate Required

**需要（两项授权合一）**：
1. 创建 Phase 2.6 validation change（Large：新验收面 + 真机战役）；
2. 真实设备访问授权（L1–L4 全部需要物理设备或明确以 Real Emulator 替代的裁定——注意
   真实 Settings 在模拟器与真机上的树形态差异是验收内容之一，建议真机优先）。

## 16. AuthorityDelta

`NONE`（纯分析；未改任何代码/契约/文档语义——本文件为新增分析记录）。

## 17. ArchitectureDelta

`NONE`。

## 18. Exact Next Step

待 Human 批准 §15 后：
1. 起草 `runtime-full-traversal-acceptance` OpenSpec（proposal/design/spec/tasks，
   validation-only 边界显式冻结）；
2. 复用 Phase 2.5 harness 骨架实现 Tier-C 级 runner + `SettingsStrategyBinding`
   （生产 SettingsSemanticCapability 的 strategy 侧适配，harness-local）；
3. L1 起步，逐级证据驱动推进；
4. 每级独立验收 + 回归；L4 全过后出 acceptance report，Phase 2.6 毕业评审回 Human。

**本轮到此为止——分析停止于实现之前。**
