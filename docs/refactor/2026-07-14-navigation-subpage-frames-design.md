# 多分支导航覆盖重构设计:导航子节点 → 子页帧

> 日期: 2026-07-14
> 状态: Draft(OpenSpec change `navigation-subpage-frames` 已建,待实施)
> 分支: feature/refactor
> 相关代码: `src/UniClaw.Core/Traversal/TraversalEngine.cs`(`DynamicChildManager`)、`src/UniClaw.Core/Traversal/StepOrchestrator.cs`、`src/UniClaw.Core/Graph/Models/DynamicMatcher.cs`
> 相关文档: `docs/system/layers/traversal.md`、`docs/system/decisions/log.md`、`docs/refactor/2026-07-14-scroll-as-action-refactor-design.md`
> OpenSpec: `openspec/changes/navigation-subpage-frames/`(proposal + design + specs + tasks)

---

## 1. 背景与问题发现

`scroll-action-refactor` 归档后,为回答"遍历的深度/广度/元素覆盖是否充分",用临时 probe 实测了遍历覆盖度。结论:

- **单列表/页面内广度满分**(long-list 30/30、listA 16/16,seen-set 差分逐页推进)。
- **深度可达**(3 层 root→settings→list,深层页 20/20 + 滚动)。
- **Windowed+Jump 按设计跳元素**(overshoot 2.0 精确跳过 page 1 的 8 项)。
- **⚠️ 真实缺口:多兄弟导航分支只走第一个。**

**缺口复现**:hub 页含两个导航按钮 `to_A`(→listA)、`to_B`(→listB),各自可滚动。实测:

| 场景 | listA | listB | 动作序列 |
|------|-------|-------|---------|
| 滚动版 | **16/16** | **0/16** | `tap:to_A → A_0..A_7 → swipe → A_8..A_15`(结束) |
| 非滚动控制组 | **3/3** | **0/3** | `tap:to_A2 → A2_0..A2_2`(结束) |

`to_B` 从未被 tap,但遍历仍上报 `all_visited=true`。

**关键判定**:非滚动控制组同样复现 → **与 scroll-action-refactor 无关**,是核心 DynamicMatch×导航的 pre-existing 缺陷。

## 2. 根因分析(systematic debugging)

三处代码锁定根因:

1. **`DynamicChildManager.Generate` 从当前页生成子节点** — [TraversalEngine.cs:519](../../src/UniClaw.Core/Traversal/TraversalEngine.cs#L519):`var pageAnalysis = runtimeCtx?.CurrentPageAnalysis;` 子节点来自**当前屏幕页**。
2. **页指纹变化 → 作废并从新页重生成** — [TraversalEngine.cs:480-489](../../src/UniClaw.Core/Traversal/TraversalEngine.cs#L480-L489):`if (cachedEntry.Fingerprint != currentFingerprint) { Invalidate(node.NodeId); }` 然后从新页 Generate。
3. **导航后元素挂在 root 名下 + 无 PressBack 触发点** — child NodeId = `dyn_button_leaf_{item}_root`(父=root);唯一 PressBack [StepOrchestrator.cs:132](../../src/UniClaw.Core/Traversal/StepOrchestrator.cs#L132) 仅在**非根** DynamicMatch 耗尽时触发。但 listA 元素是 root 的扁平子节点 → root 在 depth=1 耗尽 → `frameCompleted=true`,**没有一个"从 listA pop 回 hub"的动作**让 PressBack 触发。

**机制全貌**:

```
hub 页, root(DynamicMatch), depth=1
  gen root.children from hub → [to_A(NAV), to_B(NAV)]
  visit to_A: tap → 导航 hub→listA (副作用,页面变)
    to_A 是叶子 → FrameComplete → pop to_A
  回到 root(depth=1), 但当前页=listA
  gen root.children: 指纹变(hub→listA) → 作废 → 从 listA 重生成 → [A_0..A_7]
    ┄┄┄ to_B(在 hub)永久丢失 ┄┄┄
  访问 A_0..A_7, 滚动, A_8..A_15
  listA 耗尽 + 到底 → root depth=1 → frameCompleted=true → 结束
  to_B 从未访问; all_visited=true (谎言)
```

**为什么 `all_visited` 撒谎**:它只校验**已生成**子节点。to_B 从未重生成(页面再不回 hub),所以"所有已生成子节点已访问"平凡为真。

**深层根因**:C# 用 DynamicMatch(按当前页生成子节点)同时发现**滚动元素**和**导航按钮**。两者对"页面变化"语义相反:
- **滚动**:页面元素变化(出新元素)→ 应**重新生成**子节点。✅ 当前行为正确。
- **导航**:tap 跳到新页 → 应**返回原页继续兄弟**,而非从新页重生成。❌ 当前行为错误。

引擎把两者混淆(都用指纹作废+重生成处理),导航后从新页重生成,永久丢弃原页剩余兄弟。

## 3. 方案对比

| 方案 | 评价 | 结论 |
|------|------|------|
| **A: 框架记录原页 + PressBack 回退** | ❌ **触发点缺失**:导航后 listA 元素挂在 root 扁平子节点下,root 在 depth=1 耗尽 → 直接 `frameCompleted`,**没有"从 listA pop 回 hub"的动作**让 PressBack 触发。靠指纹猜"变化是导航还是滚动"也不稳。A 最多修一层,多层树仍丢。 | 否决 |
| **B: 导航子节点推子页帧** | ✅ **根因正确**:导航=帧递归,对齐真实 back-stack。子页有自己帧 → 耗尽(depth≥2)自然触发既有 PressBack+Pop。判定信号是子节点元数据(`ExpectedAction.Navigate`),不靠指纹猜。范围可控。 | **选定** |
| **D: 显式导航树(StaticNodes),DynamicMatch 只管滚动** | ⚠️ 概念最干净(导航=显式计划结构),但要改**所有基线测试计划数据**+引擎 StaticNode 导航支持,对"修覆盖缺口"范围过大。 | 留作未来 |

**选 B** 的决定性论据:**A 在根因层面站不住** —— 因为 listA 元素被错误归到 root,root 耗尽时在 depth=1,没有 pop 动作触发 PressBack。B 通过让导航子节点拥有自己的子页帧,使"耗尽 → PressBack+Pop"的触发点自然存在。

## 4. 选定方案 B 的核心设计

### 4.1 导航子节点检测:用元数据,不靠指纹

生成子节点时(`DynamicMatcher.MatchAll` → `TemplateInstantiator`),若匹配项 `MenuItem.ExpectedAction == Navigate` 或 `ExpectsPageChange == true`,把生成的子节点标记为**导航子节点**(携带标记,无新 enum)。

**为什么不用指纹**:指纹变化无法区分滚动(应重生成)vs 导航(应入帧+返回还原)。元数据判定确定性,不混淆。

### 4.2 子页帧归属:子页元素归导航子节点,而非 root

导航子节点执行(tap 导航)后,推一个 DynamicMatch **子页帧**,其 `Generate` 的 key 用**该导航子节点的 NodeId**(如 `dyn_button_leaf_to_A_root`),不用 root。listA 元素 → 父 = to_A 帧。

**这是让触发点存在的关键**:子页耗尽时 pop 的是 to_A 帧(depth≥2)→ 触发既有 Step 9 PressBack+Pop → 页面还原回 hub → root 从 hub 重生成 → `to_B` 出现。

### 4.3 期望遍历流程

```
hub 页, root(DynamicMatch)
  gen from hub → [to_A(NAV), to_B(NAV)]
  visit to_A(NAV): tap → 导航 hub→listA
     └─ 推子帧 listA(DynamicMatch), depth=2          ← B 核心
          gen from listA(归 to_A 帧) → [A_0..A_7]
          滚动(swipe+seen-set 差分), A_8..A_15, 到底
          listA 耗尽(depth2) → PressBack + pop          ← 触发点自然存在
     ← 页面还原回 hub
  gen from hub → [to_A(✓visited), to_B(NAV)]
  visit to_B(NAV): tap → 导航 hub→listB
     └─ 推子帧 listB ... 到底 → PressBack + pop
  root: 所有兄弟导航已走 → all_visited=true (真)
```

### 4.4 复用既有机制,零新接口/enum

- 子页耗尽 → 复用 [StepOrchestrator.cs:132](../../src/UniClaw.Core/Traversal/StepOrchestrator.cs#L132) 的 PressBack+Pop(Step 9 else-branch),不新增终止逻辑。
- 子页内滚动 → 复用 scroll-action-refactor 的 `TryHandleScroll` seen-set 差分终止。
- Step 9 的 root(depth=1)完成判定不变 —— 这次 root 真把所有兄弟导航走完才完成。
- 无新 enum、无新接口方法、无 `TraversalResult` 字段变更。

### 4.5 VisitedNodes 跨帧去重

`VisitedNodes` 按 NodeId 全局去重(已如此)。导航子节点(to_A)访问后入集;子页 pop、父页重生成时,to_A 在集中 → 标记已访问,不重入。`all_visited` 校验父页重生成后的**全部**子节点(含 to_B)都在集中 → 不再平凡为真。

## 5. 与 scroll-action-refactor 的关系

- **无关**:scroll-action-refactor 改的是滚动循环终止(`TryHandleScroll` seen-set 差分),未触及指纹作废 / Generate-from-current-page 逻辑(本缺口的根因所在)。
- **复用**:本方案复用 scroll-action-refactor 的 seen-set 滚动终止 + PressBack 回退,不重复造轮子。
- **决策日志**:本重构对应新决策 **D-74**(DynamicMatch 多分支导航覆盖 —— 导航子节点推子页帧),append-only 追加,不 supersede 既有滚动决策。

## 6. 风险与权衡

| 风险 | 缓解 |
|------|------|
| 子帧归属改动 `Generate` key 逻辑(目前固定按父 NodeId) | `_dynamicChildren` 已按 NodeId 字典缓存,root 帧与子页帧天然隔离;单元测试验证归属 |
| 真实服务 PressBack 启发式(设备返回键可能不精确还原页) | mock 用导航历史栈精确还原;真实服务不确定性是既有架构限制,非本次引入;未来可加页面指纹校验 |
| 基线 numeric 变化(hierarchy 多分支现走更多元素) | 按 D-67 标定流程重标(信息性指标,非 CI 阻断) |
| 与 hierarchy 测试兼容(现多分支可能暴露掩盖问题) | TDD:先加多分支断言(现 fail),实现后转绿;661 现有测试作回归护栏 |
| 假导航(`ExpectedAction.Navigate` 但 tap 后页未变) | 执行后校验页面指纹是否真变;未变则按普通叶子处理,不推子帧 |

## 7. 迁移分期(每步测试绿)

1. **TDD 失败基线**:加多分支覆盖测试(hub→listA/listB 两边断言;深度链;非滚动控制组)—— 现 fail。
2. **检测导航子节点**:`DynamicMatcher`/`TemplateInstantiator` 传播 `ExpectedAction` 标记。
3. **推子页帧 + 子页元素归子帧**:Step 8/9 导航子节点执行后推帧;`Generate` key 改用当前帧 NodeId。
4. **PressBack 还原**:验证子页耗尽触发既有 PressBack+Pop,父页重生成 → 兄弟分支覆盖。
5. **去重 / all_visited 校验**:VisitedNodes 跨帧去重;all_visited 须所有兄弟分支走完。
6. **回归 + 基线重标**:全量绿;hierarchy 等 numeric 重标;`openspec validate`。

**回滚**:每步独立提交,可逐 commit 回退。

## 8. 范围外 / 延后

- 重构整个帧/计划模型(方案 D)—— 留作未来架构演进。
- DynamicMatch 的滚动内行为 —— scroll-action-refactor 已定。
- 真实服务 PressBack 启发式不确定性 —— 既有架构限制。
- 改计划作者心智模型(强制 StaticNodes 显式定义页面树)—— 不改。
- 向上/回顶滚动场景 —— 与 scroll-action-refactor §13 一致,另行设计。

## 9. 宪章影响 (Governance)

本重构**不违反任何 Tier-1 约束**:
- C-1~C-8 锁定 enum:无新增/移除 enum(导航子节点用元数据标记,非 enum)。
- C-3 Domain 隔离、C-4/C-7 FSM 独立性、C-5 依赖方向、C-6 visited 隔离:均不触碰。
- 无 C-11 级 schema 变更(`TraversalResult`/`NumericAnchor` 字段不变)。
- 仅追加决策 **D-74**(append-only),不 supersede 既有决策。
- 不触发 `constitution/locked-enums.md`;不改 `ArchitectureGuardTests` 约束(可能新增多分支覆盖测试,但非 CI 约束)。

## 10. 已解决的决策

| # | 决策 | 选择 |
|---|------|------|
| 方案 | 重构方向 | B(导航子节点推子页帧) —— A 触发点缺失,D 范围过大 |
| 检测 | 导航 vs 滚动判定 | 用子节点元数据(`ExpectedAction.Navigate`),不靠指纹猜 |
| 归属 | 子页元素父节点 | 归导航子节点帧(非 root),让 PressBack 触发点存在 |
| 还原 | 页面还原机制 | 复用既有 Step 9 PressBack+Pop,不新增终止逻辑 |
| 去重 | VisitedNodes | 按 NodeId 全局去重,导航子节点跨帧只算一次 |
| 范围 | StaticNodes 显式导航树(D) | 延后,本次不改计划模型 |
