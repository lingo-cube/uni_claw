## Why

`scroll-action-refactor` 归档后的覆盖度实测(probe)发现:DynamicMatch 遍历当父节点有**多个导航子节点**(各自跳转不同子页)时,只走第一个分支,兄弟分支永不进入,却仍上报 `all_visited=true`。

**实测证据**:hub 页含 `to_A`(→listA)、`to_B`(→listB)两个导航按钮,各自可滚动。实测 `listA` **16/16** 全访问、`listB` **0/16**;动作序列 `tap:to_A → A_0..A_7 → swipe → A_8..A_15` 后直接结束,`to_B` 从未被 tap。非滚动控制组(listA2/listB2 各 3 静态项)同样 **3/3 vs 0/3** → **与滚动无关,是核心 DynamicMatch×导航的 pre-existing 缺陷**(非 scroll-action-refactor 回归)。

**根因**(详见 design / refactor 文档 §2):C# 用 DynamicMatch(按当前页生成子节点)同时发现滚动元素与导航按钮,而两者对"页面变化"语义相反 —— 滚动后应重生成子节点、导航后应返回原页继续兄弟。引擎把两者混淆,导航后从新页重生成(`Generate` 从 `CurrentPageAnalysis` 取 + 指纹变化作废重生成),永久丢弃原页剩余兄弟;且导航后元素挂在 root 扁平子节点下,root 在 depth=1 耗尽直接 `frameCompleted`,**没有"返回原页"的 pop 让 PressBack 触发**。`all_visited` 只校验已生成子节点,to_B 从未重生成 → 平凡为真。这限制了多分支树(如 hierarchy)的真实覆盖度,应单独修。

## What Changes

- **导航子节点成为一等帧概念**:`DynamicMatch` 生成的子节点中,凡匹配元素 `ExpectedAction == Navigate` / `ExpectsPageChange == true` 的,执行(tap 触发导航)后**推一个新的 DynamicMatch 子页帧**,其子节点从导航后的新页生成,**父归属为该导航子节点**(而非根)。
- **页面还原由帧 pop 驱动**:子页帧整页遍历完(含自身滚动到底)→ 在 depth≥2 耗尽 → 复用既有 `StepOrchestrator` Step 9 的 PressBack + Pop(此次会真正触发),页面还原,父帧重新生成 → 剩余兄弟导航子节点出现并被访问。任意深度导航树逐层 PressBack 还原。
- **判定用行为观测,不靠元数据**:导航检测 = 非滚动动作(tap)执行后比较前后页面指纹。指纹变 → 导航(推子帧);指纹不变 → 普通叶子。滑动由 `TryHandleScroll` 专属通道处理,不会误判。
- **覆盖度语义修正**:`all_visited` 仅在所有兄弟导航分支都遍历后才为真;`VisitedNodes` 跨帧去重,每个导航子节点只算一次。
- **BREAKING — 无**:对外接口(`IGraphTraversalEngine`/`IVisionProvider`/`IActionExecutor`)不变;`TraversalResult` 字段不变;无新 enum/接口方法。仅遍历行为更完整,基线 numeric 指标会相应增长(需重标)。

## Capabilities

### New Capabilities
_(无)_

### Modified Capabilities
- `scroll-aware-traversal`: 新增 DynamicMatch 导航分支覆盖要求 —— 遍历 SHALL 访问 DynamicMatch 父节点的所有兄弟导航子节点(每个导航子节点遍历完其目标子页后返回原页继续),取代当前"只走第一个分支、兄弟丢失却 all_visited"的行为。

## Impact

- **代码**:`src/UniClaw.Core/Traversal/TraversalEngine.cs`(`DynamicChildManager.Generate` 子节点归属 + 导航子节点检测;子页帧推入)、`src/UniClaw.Core/Traversal/StepOrchestrator.cs`(导航子帧耗尽 → 既有 PressBack+Pop 触发点)、`src/UniClaw.Core/Graph/Models/`(`DynamicMatcher`/`TemplateInstantiator` 传播 `ExpectedAction` 标记导航子节点)。无新 enum/接口方法。
- **测试**:新增多分支覆盖测试(hub→listA/listB 断言两边元素都访问;深度链;非滚动控制组);现有 661 测试作回归护栏。基线 JSON(hierarchy 等)按更完整覆盖重标 numericAnchor。
- **依赖**:无新增。复用 scroll-action-refactor 的 seen-set 滚动终止 + PressBack 回退。
- **风险**:子帧归属改动 `Generate` key 逻辑(目前固定按父 NodeId);真实服务 PressBack 启发式(mock 用导航历史栈精确还原)—— 此限制与既有架构一致,非本次引入。
- **详细设计**:见 `design.md` 与 `docs/refactor/2026-07-14-navigation-subpage-frames-design.md`(根因调查 + 覆盖度实测证据 + A/B/D 方案对比 + B 完整叙事)。
