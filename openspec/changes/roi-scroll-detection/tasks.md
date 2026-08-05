## 1. Data Records（零依赖，最先完成）

- [x] 1.1 创建 `src/UniClaw.Core/Traversal/RoiRect.cs` — `readonly record struct RoiRect(int X1, int Y1, int X2, int Y2)`
- [x] 1.2 创建 `src/UniClaw.Core/Traversal/RoiSnapshot.cs` — `sealed record RoiSnapshot(ulong PerceptualHash, byte[] GrayPixels, int Width, int Height, long FrameSeq)`
- [x] 1.3 创建 `src/UniClaw.Core/Traversal/SnapshotComparison.cs` — `sealed record SnapshotComparison(int HashDistance, double MeanAbsoluteDifference, double ChangedPixelRatio, bool IsSame)`

## 2. Core Utilities（纯函数，依赖 records）

- [x] 2.1 创建 `src/UniClaw.Core/Traversal/RoiSnapshotGenerator.cs` — 静态类。`Generate(byte[] screenshot, RoiRect roi, int snapshotWidth, int snapshotHeight, long frameSeq)`：裁剪 ROI → 灰度 → 缩放（256×128 / 128×256 auto）→ 高斯模糊 → dHash（内部 9×8）+ GrayPixels（0-255）。强制 raw buffer 输入。详见 spec `roi-scroll-detection` Requirement 4
- [x] 2.2 创建 `src/UniClaw.Core/Traversal/SnapshotComparer.cs` — 静态类。`Compare(RoiSnapshot a, RoiSnapshot b, ScrollSwipeConfig config)`：汉明距离 + MAD（0-255）+ 变化像素比（>PixelNoiseThreshold），三指标 AND 语义 → `SnapshotComparison`。详见 spec `roi-scroll-detection` Requirement 5
- [x] 2.3 创建 `src/UniClaw.Core/Traversal/RoiSelector.cs` — 静态类。`Select(PageAnalysis analysis, byte[] screenshot, int screenWidth, int screenHeight)`：滑动窗口评分（密度 0.6 + 纹理 0.3 + 非纯色 0.1 − 动态惩罚），有 bbox 退化策略，返回 `RoiRect?`。内嵌动态元素 type 黑名单。详见 spec `roi-scroll-detection` Requirements 1-3

## 3. StableFrameCapturer（依赖 IScreenCapture + SnapshotComparer）

- [x] 3.1 创建 `src/UniClaw.Core/Traversal/StableFrameCapturer.cs` — internal 实例类。构造函数 `(IScreenCapture, ScrollSwipeConfig, RoiSnapshotGenerator)`。`CaptureStableAsync(RoiRect, int requiredPairs, CancellationToken)`：动态延迟采集循环 + 绝对超时 3s。对外暴露 `CaptureBeforeScrollAsync`（requiredPairs=1）+ `CaptureAfterScrollAsync`（requiredPairs=2）。详见 spec `roi-scroll-detection` Requirement 6

## 4. ScrollSwipeConfig 扩展

- [x] 4.1 修改 `src/UniClaw.Core/Traversal/ScrollSwipeConfig.cs` — 新增 11 个字段：`StableSampleMaxRetries=5`, `StableSampleIntervalMs=100`, `StableSampleMaxTimeMs=3000`, `RoiSnapshotWidth=0`, `RoiSnapshotHeight=0`, `HashDistanceThreshold=10`, `MadThreshold=12.75`, `PixelNoiseThreshold=15.0`, `ChangedPixelRatio=0.1`, `MaxConsecutiveUnknown=3`, `SecondSwipeDistanceRatio=0.5`。现有 6 字段 `(StartX, StartY, EndX, EndY, DurationMs, MaxEmptyScrollRetries)` 不变。详见 spec `scroll-swipe-config`

## 5. InterceptionHandler 重写（核心）

- [x] 5.1 在 `src/UniClaw.Core/Traversal/InterceptionHandler.cs` 新增实例字段：`_roiRect`（RoiRect?）、`_currentBaselineSnapshot`（RoiSnapshot?）、`_consecutiveUnchanged`（int）、`_consecutiveUnknown`（int）、`_stableCapture`（懒初始化 StableFrameCapturer）
- [x] 5.2 新增私有方法 `GetOrSelectRoi(StepContext ctx)`：检查 `_roiRect` 有效 → 直接返回；否则获取截图（优先复用页面分析截图）→ 调 `RoiSelector.Select` → 采集初始基准 `S0` → 缓存 ROI + baseline
- [x] 5.3 新增私有方法 `ClearRoi()`：释放 `_roiRect`、`_currentBaselineSnapshot`、计数器
- [x] 5.4 删除 D5 UIA 指纹快路径（`IObservableScreenStateProvider` cast + preSwipe/postSwipe UIA dump + 指纹比对 ~30 行）
- [x] 5.5 重写 `TryHandleScrollAsync`：移除 `static` 修饰符。新流程见 PRD §3.12。—— `GetOrSelectRoi` → `CaptureBeforeScroll` S0 → `SwipeAsync` → `CaptureAfterScroll` S1 → `Compare` → Different → Scrolled；Same → 缩距 swipe2（距离×0.5）→ S2 → 三组比较 → EndReached/Scrolled/Unknown。防死循环：连续 Unknown ≥ `MaxConsecutiveUnknown` → `ClearRoi()`。详见 spec `scroll-aware-traversal` + `roi-scroll-detection` Requirements 7-10

## 6. 删除 UIA 依赖（文件级）

- [x] 6.1 删除 `src/UniClaw.Device/AdbScreenStateProvider.cs`
- [x] 6.2 删除 `src/UniClaw.Core/Observation/UiAutomatorPageAnalysis.cs`
- [x] 6.3 删除 `src/UniClaw.Core/Traversal/IUiAutomatorAvailability.cs`
- [x] 6.4 删除 `src/UniClaw.Core/Traversal/IScreenStateCache.cs`
- [x] 6.5 删除 `src/UniClaw.Host/Runner/StepCaptureStore.cs`

## 7. 删除 UIA 依赖（行级）

- [x] 7.1 `ObservationPipeline.cs`：删除 UIA 分支（L1 gate `UIA_Enabled`/`IsUiAutomatorAvailable` + `GetFreshScreenStateAsync` + `UiAutomatorPageAnalysis.Parse` + UIA decision + `RecordDecisionAsync("UIA")` + `HasPopupItems` 入口），保留 back navigation reuse（`ConsumeBackPending`/`GetBackReuseAnalysis`）+ AI 透传 + `Remember` 历史。`Remember` 去重键从 `HierarchyFingerprint` 迁移到 `PageSnapshotManager.Fingerprint(analysis)`。详见 spec `screen-state-provider`
- [x] 7.2 `ObservationConfig.cs`：删除 `UIA_Enabled`（默认 true）、`UIA_MinItems`（默认 3）、`SkipUIAOnBackNavigation`；保留其余字段
- [x] 7.3 `ScreenStateResult.cs`：删除 `HierarchyXml` 和 `HierarchyFingerprint` 字段。最终字段：`(bool Succeeded, string Status, bool HasScroll, bool IsEndOfList, ScreenFailure? Failure)`
- [x] 7.4 `IObservableScreenStateProvider.cs`：`RefreshAsync` 签名简化为 `Task<ScreenStateResult> RefreshAsync(CancellationToken cancellationToken = default)`，删除 `previousHierarchyXml` / `afterScroll` 参数
- [x] 7.5 `VisionScreenStateProvider.cs`：删除 `_uia` 字段 + UIA try-catch 冗余侧信道；`RefreshAsync` 直接返回 vision-only 状态（`HasScroll`=true, `IsEndOfList`=false, `HierarchyXml`=null, `HierarchyFingerprint`=null）。删除构造函数 `IObservableScreenStateProvider?` 参数。详见 spec `vision-screen-state-with-uia-fallback` + `screen-state-provider`
- [x] 7.6 `RunAssetHook.cs`：删除 `before.xml` / `after.xml` 写入（`_pipeline.Submit` AssetCategories.UiXml 调用 + `Encoding.UTF8.GetBytes(uiXml)`）；screenshot 写入保留
- [x] 7.7 `IAdbSession.cs`：删除 `DumpUiHierarchyAsync()` 方法声明
- [x] 7.8 `ProcessAdbSession.cs` + `AdvancedSharpAdbSession.cs`：删除 `DumpUiHierarchyAsync()` 实现

## 8. Host 集成连线

- [x] 8.1 `HostCommands.cs`：删除 `AdbScreenStateProvider` 构造 + DI 注册；删除 `StepCaptureStore` 构造 + DI 注册；删除 `ObservationPipeline` 的 UIA 参数传递（`ObservationConfig` 中 UIA 字段移除后的级联调整）；删除 `IObservableScreenStateProvider` cast 相关调用（如滚动快路径已删，Host 不再需要）
- [x] 8.2 `ScenarioObservation.cs`（`AdbScenarioObservationSource`）：删除 `_screenState.RefreshAsync` 调用中的 `previousHierarchyXml`/`afterScroll` 参数；删除 `HierarchyXml`/`HierarchyFingerprint` 对 `ScenarioObservation` 的赋值（对应 record 字段移除）
- [x] 8.3 `AdbEntryActionDriver.cs`：删除 `HierarchyContainsAsync` 方法（依赖 `DumpUiHierarchyAsync`），替换 entry condition check 为其他可用机制

## 9. 清理测试

- [x] 9.1 删除 `AdbScreenStateProvider` 相关测试（grep 全仓找到所有引用）
- [x] 9.2 删除 `UiAutomatorPageAnalysis` 相关测试（如 `PageAnalysisShapeContractTests` 中 UIA 路径 C4 契约测试）
- [x] 9.3 删除 `StepCaptureStore` 相关测试
- [x] 9.4 更新所有使用 `ScreenStateResult` 构造的测试 fixture——移除 `HierarchyXml`/`HierarchyFingerprint` 参数
- [x] 9.5 更新所有实现 `IObservableScreenStateProvider` 的测试 double——`RefreshAsync` 签名变更（删 2 参数）
- [x] 9.6 Python test：验证 evidence 格式不变（`bounds` / `evidence.yoloId` 仍存在且值正确）

## 10. 构建与回归

- [x] 10.1 `dotnet build` 全项目 Release 通过
- [x] 10.2 `dotnet test` 现有测试全绿（排除已删除的 UIA 测试）
- [ ] 10.3 集成测试：scenario-locate 完整跑通（无 UIA）
- [ ] 10.4 集成测试：不可滚动页面 → 1-2 次空滚后正常到底
- [ ] 10.5 集成测试：可滚动页面 → 滚动到底正常检测

## 11. 完美实现：YOLO bbox 密度透传（2026-08-05 追加）

> 初始实现中 RoiSelector 密度评分退化为纹理评分（MenuItem 无 boundsPx/yoloId，
> GetOrSelectRoiAsync 传空 bbox 列表）。本 section 接通 local-vision 检测数据，
> 密度权重 0.6 完全激活。AI provider（deepseek 等）无检测数据 → 保持退化，为已知缺口。

- [x] 11.1 `LocalVisionProvider.cs`：`MapToPageAnalysisDto` 收集非 popup 候选的 `boundsPx`
      扁平化为 `yolo_bboxes`（与 items 坐标同空间 = C# 发送图空间）
- [x] 11.2 `PageAnalysisRecords.cs`：`PageAnalysis` 新增 `ImmutableArray<int> YoloBboxes`
      侧通道字段（默认 empty，构造参数可选，不破坏现有构造点）
- [x] 11.3 `PageAnalyzer.cs`：私有 `PageAnalysisDto` 反序列化 `yolo_bboxes` →
      构造 `PageAnalysis` 时透传（AI 响应无此字段 → null → Empty）
- [x] 11.4 `InterceptionHandler.cs`：`BuildYoloBboxes` 反变换（sx = 全屏宽/发送宽,
      y_full = y×sx + cropTopPx，参数与 ImageResizer 调用同源 env/默认值）→
      `GetOrSelectRoiAsync` 传真实 bbox 给 `RoiSelector.Select`
- [x] 11.5 测试：`RoiSelectorTests`（密度激活/退化/位置驱动 3 例）、
      `InterceptionHandlerRoiTests`（反变换 4 例）、`LocalVisionProviderTests` V27/V27b（透传 2 例）
- [x] 11.6 全量回归：1325 passed / 0 failed（Core 1105 + Host 174 + TraceTool 46）
