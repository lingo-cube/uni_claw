## Why

当前 `IVisionProvider` 只覆盖视觉分析（截图 → PageAnalysis），但真实遍历需要 AI 做三件事：页面感知+验证、遍历决策、文本理解。三个能力共享同一组基础设施（模型调用、token 预算、重试、缓存），不应各建独立接口。此外，IVisionProvider 混了滚动方法（设备状态查询，非 AI 判断），职责泄漏。IAIStrategyAdvisor 仅有骨架零消费者，且 ITraversalAdvisor 参数引用了 ITraversalContext 导致 UniBrain↔StateMachine 双向依赖。UniBrain 是对这三个 AI 能力的统一抽象，底层通过 IModelProvider 可插拔后端实现。

## What Changes

- **BREAKING**: `IVisionProvider` (5+4 方法) → 拆分到 `IPageAnalyzer` (3 方法) + `IScreenStateProvider` (4 方法, 独立接口)
- **BREAKING**: `IAIStrategyAdvisor` → `ITraversalAdvisor` (4 方法, 参数改为 Domain+BCL 类型, 消除 ITraversalContext 引用)
- **BREAKING**: `StepContext.Vision` → `StepContext.Brain` (IUniBrain) + `StepContext.ScreenState` (IScreenStateProvider)
- **新增**: `IUniBrain` facade (3 子接口属性: PageAnalyzer, Advisor, Text)
- **新增**: `UniBrainService` sealed class (纯组合容器, 配置/DI 驱动)
- **新增**: `ITextUnderstanding` 接口 (文本理解能力, 对齐 Python parse_instruction)
- **新增**: `IModelProvider` 接口 (AI 模型调用抽象, 对齐 Python AIProvider)
- **新增**: UniBrain namespace 类型: ContextDecisionResult (字段对齐 Python), MismatchDetails, Suggestion, ContainerInference, PageTypeVerification, SafetyScreeningResult, SafetyEvaluation, PageLevelGuidance, DecisionResult, SafetyTag, AppEntryPoint, TextUnderstandingRequest/Result, ModelRequest/Response, UniBrainConfig
- **删除**: `AI/` 整个目录 (迁入 UniBrain/)
- **新增**: 6 个 ArchitectureGuard tests (UniBrain 零向上引用 + IScreenStateProvider 方法锁定)
- **迁移**: Simulation mock: StatefulMockVisionService → MockPageAnalyzer + MockScreenStateProvider

## Capabilities

### New Capabilities
- `unibrain-facade`: IUniBrain facade + UniBrainService 组合容器 + UniBrainConfig 配置驱动组合
- `page-analyzer`: IPageAnalyzer 接口 (AnalyzeCurrentPageAsync, FindAppEntryAsync, VerifyPageTypeAsync) + AppEntryPoint 类型
- `traversal-advisor`: ITraversalAdvisor 接口 (4 方法, Domain+BCL 参数) + ContainerInference/ContextDecisionResult/SafetyScreeningResult 等决策类型
- `text-understanding`: ITextUnderstanding 接口 + TextUnderstandingRequest/Result 类型
- `model-provider`: IModelProvider 接口 + ModelRequest/ModelResponse 类型
- `screen-state-provider`: IScreenStateProvider 接口 (4 方法, Traversal namespace, 独立于 AI)

### Modified Capabilities
- `enum-value-guards`: 新增 UniBrain guard tests (IUniBrain 3 子接口, IScreenStateProvider 4 方法, UniBrain 零 StateMachine/Traversal 引用)
- `simulation-baseline`: StatefulMockVisionService → MockPageAnalyzer + MockScreenStateProvider 组合迁移
- `scroll-swipe-config`: IScreenStateProvider 与 ScrollSwipeConfig 同层 (Traversal namespace)
- `span-type`: AICallRecord.Capability 值域新增 8 个 UniBrain capability 名称 (SpanType 值数不变)
- `traversal-engine`: StepContext 注入改为 IUniBrain + IScreenStateProvider, 消费代码迁移

## Impact

- **代码**: AI/ 目录删除; 新建 UniBrain/ 目录 (15+ 文件); Traversal/ 新增 IScreenStateProvider; StateMachine/StepContext.cs 属性名改; Simulation/ mock 类拆分
- **API**: 所有 IVisionProvider 消费代码迁移 (ctx.Vision → ctx.Brain.PageAnalyzer + ctx.ScreenState); IAIStrategyAdvisor 消费代码迁移
- **依赖**: 新增向上引用 StateMachine→UniBrain, Traversal→UniBrain (acknowledged, 同 D-14/D-17); UniBrain 零 StateMachine/Traversal 引用 (guard enforced)
- **架构**: 新增 UniBrain 层 (Domain 上方, StateMachine/Traversal 下方); 层级依赖图更新; subsystem-boundaries.md 更新
