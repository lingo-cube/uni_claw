## Why

60+ 次真机集成测试暴露 Core 观测层有三重断裂：(1) UIA/AI 双路径各自独立决策，弹窗检测和回退逻辑散落两处；(2) FSM ErrorHandling 缺少闸门，同页 item 失败后循环 Backtrack→NodeSelect 无上限；(3) AI 空响应后 2 次重试烧掉 370s+，却最终失败。这些不是独立 bug，而是观测-分析-导航三层各自为政的结构性缺陷。`TraversalAdvisor` 已定义但未接入 FSM 决策链。Qwen 视觉模型切换进行中，需要一个稳定的 Core 管线来承载。

## What Changes

- **新增 `ObservationPipeline`**（Core 层）：截图→UIA→AI 三级级联，弹窗检测和内阈值判断统一收敛到 Pipeline 内；移除 Host 层 `useUiAutomatorAnalysis` 分散开关；UIA dump 失败时直接调 AI
- **FSM ErrorHandling 增强**：同页 item 失败计数闸门（超限自动 PressBack）；`TraversalAdvisor.DecideAsync` 接入 ErrorHandling 策略选择链
- **AI 空响应快速失败**：空响应不重试，直接抛 DomainValidationException（不降级到 UIA——弹窗场景下 UIA 数据不可靠）
- **UIA 动态开关**：`AdbScreenStateProvider` 首次 dump 失败自动标记 UIA 不可用，后续全部走 AI；back 导航后跳过 UIA dump 复用缓存
- **坐标/过滤补丁**：`TryParseBounds` 归一化；`MapItem` y-clamp 0.90、跳过 `android:id/summary`；`IsInteractive` 按 content-desc 过滤导航类 ImageButton
- **意图回退增强**：`ScenarioPlanCompiler` AI 提取失败时根据 `scenario.Mode` 正确回退机械映射
- **Qwen 视觉模型适配**：provider config 支持 temperature/top_p；prompt 重新校准提醒

## Capabilities

### New Capabilities
- `observation-pipeline`: 统一观测管线（UIA→AI 三级级联、阈值配置、弹窗检测、UIA 开关、AI 空响应不重试）
- `ai-retry-policy`: AI 调用重试策略（区分瞬态错误与结构性错误，空响应快速失败）
- `error-handling-back-gate`: ErrorHandling 回退闸门（同页 item 失败上限 + Advisor 决策）

### Modified Capabilities
- `page-analyzer`: UIA-first 阈值判断收敛到 ObservationPipeline；移除 `UiAutomatorAugmentingPageAnalyzer`
- `error-handler`: Advisor 接入策略选择链；空响应判定为结构性错误不重试
- `traversal-fsm`: PreconditionCheck 不再 stub；ErrorHandling 增加 PressBack 闸门；Advisor 调用点

## Impact

- `src/UniClaw.Core/Observation/ObservationPipeline.cs` — 新增（独立桥接命名空间，见 D-131；管线实现 IPageAnalyzer 同时消费 Traversal 类型，按 D-130 不能放 UniBrain/，按命名空间隔离约定独立成目录）
- `src/UniClaw.Core/UniBrain/PageAnalyzer.cs` — 移除 UIA-first 逻辑，仅做 AI 调用
- `src/UniClaw.Core/StateMachine/TraversalFSM.cs` — PreconditionCheck / ErrorHandling / Advisor
- `src/UniClaw.Host/Runner/ScenarioObservation.cs` — MapItem / TryParseBounds / IsInteractive 补丁
- `src/UniClaw.Host/Runner/InvalidatingPageAnalysisCache.cs` — 废弃 UiAutomatorAugmentingPageAnalyzer
- `src/UniClaw.Host/Runner/ScenarioRunnerBase.cs` — ValidateBoundary package prefix
- `src/UniClaw.Host/Runner/ScenarioPlanning.cs` — Intent 回退
- `src/UniClaw.ClaudeProvider/OpenAiCompatibleVisionProvider.cs` — temperature/top_p 配置
- `src/UniClaw.Core/UniBrain/ImageResizer.cs` — 截图压缩
- `src/UniClaw.Core/UniBrain/ModelRequest.cs` — 空响应标记（结构性错误）
