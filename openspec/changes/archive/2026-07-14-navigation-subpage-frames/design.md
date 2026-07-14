## Context

`scroll-action-refactor` 归档后,为回答"遍历的深度/广度/元素覆盖是否充分",用 probe 实测覆盖度,发现 **DynamicMatch 多兄弟导航分支只走第一个**。

**实测证据**(probe):

| 场景 | listA | listB | 动作序列 |
|------|-------|-------|---------|
| 滚动版 | **16/16** | **0/16** | `tap:to_A → A_0..A_7 → swipe → A_8..A_15`(结束) |
| 非滚动控制组 | **3/3** | **0/3** | `tap:to_A2 → A2_0..A2_2`(结束) |

`to_B` 从未被 tap,但遍历上报 `all_visited=true`。非滚动控制组同样复现 → **与 scroll-action-refactor 无关**(非回归),是核心 DynamicMatch×导航 pre-existing 缺陷。完整调查见 `docs/refactor/2026-07-14-navigation-subpage-frames-design.md`。

**根因**(systematic debugging,三处代码锁定):
1. **`Generate` 从当前页生成子节点** — [TraversalEngine.cs:519](../../../src/UniClaw.Core/Traversal/TraversalEngine.cs#L519):子节点来自 `CurrentPageAnalysis`(当前屏幕页)。
2. **页指纹变化 → 作废并从新页重生成** — [TraversalEngine.cs:480-489](../../../src/UniClaw.Core/Traversal/TraversalEngine.cs#L480-L489)。
3. **导航后元素挂 root + 无 PressBack 触发点** — child NodeId = `dyn_button_leaf_{item}_root`(父=root);唯一 PressBack [StepOrchestrator.cs:132](../../../src/UniClaw.Core/Traversal/StepOrchestrator.cs#L132) 仅在非根 DynamicMatch 耗尽时触发。但 listA 元素是 root 扁平子节点 → root 在 depth=1 耗尽 → `frameCompleted=true`,**没有"从 listA pop 回 hub"的动作**让 PressBack 触发。

**深层根因**:C# 用 DynamicMatch 同时发现滚动元素与导航按钮,两者对"页面变化"语义相反 —— 滚动后应重生成(当前行为正确)、导航后应返回原页继续兄弟(当前错误)。引擎用同一套指纹作废+重生成处理两者,导航后从新页重生成,永久丢弃原页剩余兄弟。`all_visited` 只校验已生成子节点 → to_B 从未重生成 → 平凡为真。

## Goals / Non-Goals

**Goals:**
- DynamicMatch 父节点的**所有兄弟导航子节点**都被进入并完整遍历(含各自子页滚动到底)。
- 任意深度导航树逐层 PressBack 还原,全覆盖。
- `all_visited` 仅在所有兄弟分支遍历后才为真;`VisitedNodes` 跨帧去重。
- 对外接口零变更;复用既有 seen-set 滚动终止 + PressBack 回退。

**Non-Goals:**
- 不重构整个帧/计划模型(不走方案 D)。
- 不改 DynamicMatch 滚动内行为(scroll-action-refactor 已定)。
- 不解决真实服务 PressBack 启发式不确定性。
- 不改计划作者心智模型(不强制 StaticNodes 显式页面树)。

## Decisions

### 1. 选方案 B(导航子节点 → 子页帧),而非 A 或 D

| 方案 | 评价 | 结论 |
|------|------|------|
| **A: 框架记录原页 + PressBack 回退** | ❌ **触发点缺失**:导航后 listA 元素挂 root 扁平子节点,root 在 depth=1 耗尽 → 直接 `frameCompleted`,**没有"从 listA pop 回 hub"的动作**让 PressBack 触发。靠指纹猜"变化是导航还是滚动"也不稳。A 最多修一层,多层树仍丢。 | 否决 |
| **B: 导航子节点推子页帧** | ✅ **根因正确**:导航=帧递归,对齐真实 back-stack。子页有自己帧 → 耗尽(depth≥2)自然触发既有 PressBack+Pop。判定信号是行为观测(tap 后指纹变化),不靠元数据预标记。范围可控。 | **选定** |
| **D: 显式导航树(StaticNodes),DynamicMatch 只管滚动** | ⚠️ 概念最干净,但要改所有基线测试计划数据 + 引擎 StaticNode 导航支持,范围过大。 | 留作未来 |

**决定性论据**:A 在根因层面站不住 —— listA 元素被错误归到 root,root 耗尽时在 depth=1,没有 pop 触发 PressBack。B 通过让导航子节点拥有自己子页帧,使"耗尽 → PressBack+Pop"触发点自然存在。

### 2. 导航检测:行为观测(指纹变化),不靠元数据

**不再使用 `ExpectedAction` 元数据预标记。** 改为行为检测:在 StepOrchestrator 中,非滚动动作(tap/click)执行后,比较动作前后的页面指纹。指纹变化 → 就是导航 → 推子页帧;指纹未变 → 普通叶子。

**前提**:滑动由 `TryHandleScroll` 专属通道处理(显式 `Invalidate` + 重新截图),不会走到指纹变化检测分支。`GetNextUnvisitedChild` 中的指纹自动作废逻辑(第 480-489 行)需移除 —— 它对滚动冗余(TryHandleScroll 已显式作废),对导航错误(应推子帧而非作废父节点兄弟)。

**理由**:行为观测比元数据预判更可靠 —— 不依赖 AI 正确标记 `ExpectedAction`,只看实际效果(页面真变了吗)。且滑动不会误判为导航(滑动走专属通道,不经过此检测)。

### 3. 子页帧归属:子页元素归导航子节点,而非 root

导航子节点执行后推的子页 DynamicMatch 帧,其 `Generate` key 用**该导航子节点 NodeId**(如 `dyn_button_leaf_to_A_root`),不用 root。listA 元素 → 父 = to_A 帧。**理由**:子页耗尽时 pop 的是 to_A 帧(depth≥2)→ 触发既有 Step 9 PressBack+Pop → 页面还原回 hub → root 从 hub 重生成 → `to_B` 出现。这是让触发点存在的关键。

### 4. 复用既有 PressBack + Pop,不改 Step 9 主结构

子页耗尽走现有 Step 9 else-branch(depth>1 → PressBack+Pop → NodeSelect)。唯一新增:推子页帧的时机(导航子节点执行后)。root(depth=1)完成判定不变 —— 这次 root 真把所有兄弟导航走完才完成。

### 5. VisitedNodes 跨帧去重

`VisitedNodes` 按 NodeId 全局去重(已如此)。导航子节点(to_A)访问后入集;子页 pop、父页重生成时 to_A 在集中 → 标记已访问不重入。`all_visited` 校验父页重生成后全部子节点(含 to_B)都在集 → 不再平凡为真。

## Risks / Trade-offs

- **[子帧归属改动 Generate key 逻辑]** → 目前固定按父 NodeId;改为按当前帧 NodeId。`_dynamicChildren` 已按 NodeId 字典缓存,root 帧与子页帧天然隔离。Mitigation:单元测试验证归属。
- **[真实服务 PressBack 启发式]** → 设备返回键可能不精确还原页。Mitigation:mock 用导航历史栈精确还原;真实服务不确定性是既有架构限制,非本次引入;未来可加页面指纹校验。
- **[基线 numeric 变化]** → hierarchy 多分支现走更多元素,visitedPagesCount/totalSteps 增长。Mitigation:按 D-67 标定流程重标(信息性,非 CI 阻断)。
- **[hierarchy 测试兼容]** → 现多分支可能暴露之前掩盖问题。Mitigation:TDD 先加多分支断言(现 fail),实现后转绿;661 现有测试回归护栏。
- **[假导航]** → tap 后页未变(tab 切换、展开等)。Mitigation: 指纹未变 → 不推子帧,按普通叶子处理。行为检测天然处理此情况。

## Migration Plan

单分支提交,每步测试绿(详见 tasks.md):
1. TDD 失败基线:加多分支覆盖测试(现 fail)。
2. 移除 `GetNextUnvisitedChild` 指纹自动作废 + StepOrchestrator 行为检测(tap 后指纹变 → 推子帧)。
3. 子页帧归属验证(`Generate` key 已是当前帧 NodeId,子帧推入后自然正确)。
4. PressBack 还原:验证子页耗尽触发既有 PressBack+Pop,父页重生成 → 兄弟覆盖。
5. 去重 / all_visited 校验。
6. 回归 + 基线重标;`openspec validate`。

**回滚**:每步独立提交,可逐 commit 回退。

## Open Questions

- 导航子节点的"目标页"是否需在生成时记录(用于真实服务 PressBack 后校验还原正确页)?v1 不强制(mock 不需要);真实服务接入时再补页面指纹校验。
- 子页帧的 `ExitCondition` 是否继承 root 的 `AllChildrenVisited`?倾向是。
