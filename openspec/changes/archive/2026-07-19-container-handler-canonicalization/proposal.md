## Why

`ContainerHandler`(D-16 设计的 3 子组件管线:`CompletionDetector` 5 优先级链 / `FallbackDecider` / `ContainerActionExecutor`,纯函数、单测覆盖)**未接进引擎** —— src 生产代码零调用,仅单测跑。生产容器完成实际由 `InterceptionHandler` 跑(`FrameCompleted` 散落 9 处,与 navigation/scroll 检测揉在一起,ad-hoc 判定)。这是**容器完成逻辑的两份重复**——一份好的(dormant)、一份活的(ad-hoc)。

**Change A**(`plancompiler-default-alignment`,已落地)定义了意图层(`CompletionPolicy` 语义、`IntentSlots.Depth/Entry`、`PlanCompiler` 派生),dormant 安全。**本 change(Change B)** 让引擎**消费**意图层:wire ContainerHandler、接 `IntentSlots.Depth`、保真 `TraversalResult.Reason`、删 `ExitCondition`、`CompletionPolicyType.None → Exhaustive` 改名。属「完成本该完成的设计」——**engine-side,行为变更**(改 live frame 完成路径,非 dormant 安全)。

## What Changes

- **wire ContainerHandler 进引擎**(dormant→live):`StepOrchestrator` 步骤 8-10 注入 `ContainerHandler`;`InterceptionHandler` 剥离容器完成判定、**委托** ContainerHandler,只留事件检测(navigation/scroll/child 计数/指纹)。ContainerHandler 成为**容器完成的唯一权威**。
- **`ContainerActionResult → FrameCompleted` 翻译**:`Back`/`AutoEscape`/`Skip` → `FrameCompleted=true`(帧将 pop);`Abort` → 不设 `FrameCompleted`(走引擎错误/终止路径)。
- **Depth 接通(priority「紧者胜」)**:`effective_depth = min(config.MaxDepth, plan.IntentSlots.Depth ?? ∞)` 流入 `CompletionContext.MaxDepth`;`CompletionDetector` Priority 2 据此判容器完成。
- **`TraversalResult.Reason` 四档保真**:达成(AllVisited/TargetFound)/ 约束剪枝(MaxSteps/Timeout)/ 异常(AntiLoop/Error)/ 外部(Cancelled)。**关键不变量:异常永不伪装 AllVisited。** 字段结构不改(D-86 从 plan 读 IntentSlots.Depth 推导 out-of-scope)。
- **删 `ExitCondition` / `ExitConditionType`**(record + enum + `TraversalNode.ExitCondition` 字段 + `CompletionContext.ExitConditionFallback` 字段)—— 全冗余,ContainerHandler 接通后无 live consumer。**BREAKING**(TraversalNode 构造器少一参,12 测试文件受影响)。
- **nav 子帧 AutoEscape 改由 context 探测**(NodeType/Meta 标记,非 `ExitCondition.Fallback` 字段)。
- **`CompletionPolicyType.None → Exhaustive` 改名** + 引擎 L286 同步判定(Change A 延后项,语义已澄清)。**BREAKING**(enum 值改名)。
- **保留**:`FallbackAction` enum(Back/AutoEscape/Skip/Abort)—— FallbackDecider 用;`Cancelled` reason 已存在(归类为第 4 档「外部」,不加新字段)。

## Capabilities

### New Capabilities
<!-- 无新 capability —— 全部是对现有 engine/graph capabilities 的对齐修正与 wiring -->

### Modified Capabilities
- `graph-foundation`:删 `ExitCondition` record + `ExitConditionType` enum + `TraversalNode.ExitCondition` 字段(含 `ExitCondition.MaxDepth` 构造期校验条款移除);`CompletionPolicyType.None → Exhaustive` 改名(Change A 已澄清语义,本 change 落地改名)
- `container-handler`:`CompletionContext` 移除 `ExitConditionFallback` 字段;`CompletionDetector` Priority 4(AllVisited)不再读 `ExitConditionFallback` —— exit-action 全由 `FallbackDecider` 内部决策(AllVisited→Back 默认,nav 子帧→AutoEscape via context);ContainerHandler 在 src 生产路径被调用(dormant→live)
- `traversal-engine`:`IntentSlots.Depth` 经 priority `min(config.MaxDepth, intent.Depth)` 流入 `CompletionContext.MaxDepth`;`CompletionReason → TraversalResult.Reason` 四档映射保真(异常不伪装 AllVisited);`CompletionPolicyType.None → Exhaustive` 判定同步(L286)
- `step-orchestrator`:步骤 8-10 注入并委托 `ContainerHandler`;`InterceptionHandler` 不再直接设 `FrameCompleted`(委托 ContainerHandler,据 `ContainerActionResult` 翻译);`OnFrameComplete` 为天然主委托点

## Impact

| 文件 | 改动 |
|---|---|
| `Traversal/InterceptionHandler.cs` | 剥离完成判定 → 委托 ContainerHandler;移除 ExitCondition set(L212-214 nav 子帧);只留事件检测 |
| `StateMachine/ContainerHandler.cs` | wire 进引擎(dormant→live);`CompletionContext` 删 `ExitConditionFallback` 字段;`CompletionDetector` Priority 4 改 FallbackDecider 决策 |
| `Traversal/StepOrchestrator.cs` | 步骤 8-10 接线 ContainerHandler(构造或 DI) |
| `Traversal/TraversalEngine.cs` | L79 Depth 接通(priority `min`);L286 `None→Exhaustive`;reason 映射传播 |
| `Graph/Models/TraversalNode.cs` | 删 `ExitCondition` record(L239) + `ExitConditionType` enum(L203) + `TraversalNode.ExitCondition` 字段(L333) |
| `Graph/Models/TraversalPlan.cs` | `CompletionPolicyType.None → Exhaustive` 改名 |
| baseline | **可能 triage**(见 design §11):20 baseline 直接受影响,ContainerHandler 5 优先级链与 ad-hoc 判定可能不等价 |
| tests | 引擎集成测试 + guard「异常不伪装 AllVisited」+ 12 文件 ExitCondition 迁移 + ContainerHandler 已有单测保留 |

- 详细设计见 `docs/refactor/2026-07-19-container-handler-canonicalization-design.md`(refactor 文档,本 change 的设计源)
- 与 Change A(`plancompiler-default-alignment`)复合才让 model B 端到端生效;两者可独立落地,语义闭环需皆成
