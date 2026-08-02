## 1. 坐标/过滤补丁（先修，低风险高收益）

- [x] 1.1 `TryParseBounds` — 倒置 bounds 归一化（Math.Min/Max swap 替代 reject）
- [x] 1.2 `MapItem` — y 坐标 clamp 到 [0.08, 0.90]（避开状态栏和导航栏）
- [x] 1.3 `MapItem` — 跳过 `android:id/summary`（副标题误取为菜单项）
- [x] 1.4 `IsInteractive` — 按 `content-desc` 过滤 "Navigate up" 的 ImageButton（不滤 "More options"）
- [x] 1.5 `MapItem` — label 来自 input 类后代时返回 null（搜索栏包裹容器过滤）
- [x] 1.6 `ValidateBoundary` — package 前缀匹配（`StartsWith(appPackage + ".")`）

## 2. FSM 仿真回归测试（Harness 模式）

新增 `FsmSimulationHarness`（tests/），对齐 `RunnerTestHarness` 模式。Harness 提供：
- 可控 `StepContext` 注入（mock Brain / Action / ScreenState / Trace）
- 可编程 `IActionExecutor` 返回（模拟 safety deny、dispatch success/fail）
- 可编程 `IPageAnalyzer` 返回（模拟 AI 空响应、popup 检测）
- `InMemoryTraceStorage` + `ITraceRecorder`（断言 FSM 状态迁移路径）
- `DriveTo(state)` helper（将 FSM 推进到指定状态，减少 boilerplate）

以下 9 项仿真测试全部经由 Harness 编写，无 emulator、无 AI、<1ms 每项。放入 AC1 门禁。
Core 侧 7 项在 `FsmSimulationRegressionTests`（<1ms）；2.6/2.7 属于 Host runner 的
`VerifyScroll`/`VerifyBack` 逻辑，由 `EnumerateScenarioRunnerTests`（RunnerTestHarness 模式）覆盖。

- [x] 2.1 `FsmSimulationHarness` — 创建 Harness 类，对齐 RunnerTestHarness 模式
- [x] 2.2 ErrorHandling 循环闸门 — 5 个 item 失败（交错成功重置连续计数）→ FSM ErrorHandling → PressBack → FrameComplete（断言不进入无限 NodeSelect 循环）
- [x] 2.3 Backtrack 不重置连续错误计数 — 注入 `ErrorHandler` 连续 3 次返回 Backtrack → `ConsecutiveErrors` = 3 → PressBack gate 生效
- [x] 2.4 ResultVerify 弹窗检测单次重试 — 首次分析无变化 + 重试返回 `IsPopup=true` → FSM 进入 PopupHandling（断言不进入第 3 轮重试）
- [x] 2.5 ResultVerify 无变化后直接 Branch — 首次无变化 + 重试无变化 + 无 popup → FSM 返回 Branch（断言不循环）
- [x] 2.6 Enumerate scroll 耗尽 → success — `EnumerateScenarioRunnerTests`：`SingleSafeEntry_EndOfList_ReachesSuccess` / `DuplicateRowsAfterScroll_ClickedOnceByDedup` 覆盖 verified_end_of_list；`ScrollStuck_NoNewEntries_NoEnd_NeverReachesEnd` 覆盖 stuck 路径
- [x] 2.7 Enumerate back 验证 — `EnumerateScenarioRunnerTests`：returned_to_settings_home 成功路径 + `ReturnLandsOffSettings_FailsReturnVerification` 失败路径
- [x] 2.8 Execute 成功 action → ResultVerify — 原 CompletionPolicy 用例因 `TestTraversalNode` 无 Operation 无法构建，替换为真实 `TraversalNode` + Click 操作的 dispatch 测试
- [x] 2.9 PreconditionChecker 门禁 — 注入 checker 返回 false → FSM 从 PreconditionCheck 进入 ErrorHandling
- [x] 2.10 AI 空响应不重试 — 注入 `IPageAnalyzer` 抛出 DomainValidationException → `IsTransient` 返回 false → 不重试

## 3. 观测管线收敛（Core 层架构）

- [x] 3.1 新增 `ObservationPipeline` — UIA→AI 三级级联（UIA fail→AI directly, UIA ok→parse→≥N items+no popup→return, else→AI, AI empty→throw）
- [x] 3.2 `ObservationConfig` — `{ UIA_MinItems, EnablePopupDetection, SkipUIAOnBackNavigation, UIA_Enabled }`
- [x] 3.3 废弃 `UiAutomatorAugmentingPageAnalyzer` — 逻辑迁移到 Pipeline
- [x] 3.4 `AdbScenarioObservationSource` — 移除 `useUiAutomatorAnalysis` 开关，统一调用 Pipeline
- [x] 3.5 UIA 动态开关 — `AdbScreenStateProvider` 首次 dump 失败标记 `UIA_Available=false`

## 4. AI 重试策略

- [x] 4.1 `ModelResponse` — 新增 `IsEmpty` 标记空响应
- [x] 4.2 `PageAnalyzer.AnalyzeOnceAsync` — 空响应直接抛异常（`IsTransient=false`）
- [x] 4.3 `PageAnalyzer.AnalyzeCurrentPageAsync` — 空响应不重试，1 次直接 fail（4.2 覆盖）
- [x] 4.4 `OpenAiCompatibleVisionProvider.SendAsync` — 空 content retry 从 2 次降为 1 次

## 5. FSM 导航增强

- [x] 5.1 `HandleErrorHandlingAsync` — 同页 item 失败计数闸门（`BackOnPageItemLimit` 超限→PressBack）
- [x] 5.2 `HandleErrorHandlingAsync` — `ConsecutiveErrors` 在所有 ErrorStrategy 下递增（不只是 Retry）
- [x] 5.3 `HandleErrorHandlingAsync` — `TraversalAdvisor.DecideAsync` 接入策略选择链
- [x] 5.4 `HandlePreconditionCheckAsync` — `IPreconditionChecker` 可选门禁
- [x] 5.5 `EnumerateScenarioRunner.VerifyScroll` — override 为 scroll 耗尽 → success

## 6. 意图 + 模型适配

- [x] 6.1 `ScenarioPlanCompiler.ResolveIntentSlots` — AI 失败 catch `DomainValidationException` → 回退机械映射
- [x] 6.2 机械映射 — 根据 `scenario.Mode` 正确区分 `target_only` vs `full` scope（已正确实现）
- [x] 6.3 `OpenAiCompatibleProviderConfig` — temperature/top_p 配置化（支持 Qwen 校准）

## 7. 验证

- [x] 7.1 仿真测试全绿 — `dotnet test --filter "FullyQualifiedName!~EmulatorScenarioIntegrationTests&FullyQualifiedName!~RealVisionIntegrationTests"`（2026-08-02 验证：Core 1013 pass / 0 fail / 1 skip，Host 131 pass / 0 fail / 8 emulator skips；含 `FsmSimulationHarness` + 7 项 FSM 流程回归 + 14 项 ObservationPipelineTests + 2 项 AC5 设备边界测试 + ArchitectureGuardTests 宪章守卫）
- [ ] 7.2 Locate 集成 — `scenario-locate` success, ≤120s, ≤1 AI call（待办，需 emulator）
- [ ] 7.3 Enumerate 集成 — `scenario-enumerate` success, ≥5 entries, ≥1 scroll（待办，需 emulator）

## Design Docs

| Module | Design Doc |
|--------|------------|
| src/UniClaw.Core/Traversal/ | docs/system/layers/traversal.md |
| src/UniClaw.Core/StateMachine/ | docs/system/layers/state-machine.md |
| src/UniClaw.Host/Runner/ | docs/system/layers/host.md |
