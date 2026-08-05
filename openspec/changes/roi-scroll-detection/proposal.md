## Why

当前滚动到底检测依赖 UIAutomator XML dump（Android 平台特有 API），与视觉管线构成两套独立观测通道。UIAutomator 在部分设备/WebView/车机系统不可用，首次失败即永久退化到纯视觉路径，且是跨平台迁移障碍。本变更完全移除 UIAutomator 依赖，用同一 Container 内 ROI 聚合比对替代滚动检测——比对 swipe 前后的标准化快照（dHash + 像素差 + 变化比），一次变化确认滚动，两次独立滚动无变化且三个稳定状态两两相同才确认到底。

## What Changes

- **删除** `AdbScreenStateProvider`、`UiAutomatorPageAnalysis`、`IUiAutomatorAvailability`、`IScreenStateCache`、`StepCaptureStore` 共 5 个文件，以及 `ObservationPipeline` UIA 分支、`InterceptionHandler` D5 指纹快路径、`ScreenStateResult.HierarchyXml/HierarchyFingerprint`、`RunAssetHook` xml 写入、`IAdbSession.DumpUiHierarchyAsync()`
- **新增** 7 个 internal 类型：`RoiSelector`（滑动窗口评分选择最佳 ROI）、`RoiSnapshotGenerator`（裁剪→灰度→缩放→模糊→dHash 标准化流水线）、`SnapshotComparer`（三指标复合比较纯函数）、`StableFrameCapturer`（动态延迟感知的稳定帧采集循环）、`RoiRect`/`RoiSnapshot`/`SnapshotComparison`（数据 record）
- **修改** `TryHandleScrollAsync` 重写为 ROI 比对流程（S0→swipe→S1→比较→same→缩距swipe2→S2→三组比较→Scrolled/EndReached/Unknown）；`ScrollSwipeConfig` 扩展 12 个新阈值；`VisionScreenStateProvider` 删除 UIA 冗余侧信道；`ObservationPipeline` 保留 back reuse + AI 透传
- **Python 零改动** — 现有 `bounds` + `evidence.yoloId` 已覆盖 ROI 密度评分所需全部信息
- **BREAKING**: `IObservableScreenStateProvider.RefreshAsync` 签名变更（删除 `previousHierarchyXml`/`afterScroll` 参数）；`ScreenStateResult` 删除 `HierarchyXml`/`HierarchyFingerprint`

## Capabilities

### New Capabilities
- `roi-scroll-detection`: ROI 选择（滑动窗口密度+纹理+非纯色综合评分）、标准化快照生成（dHash + 灰度矩阵）、三指标复合比较（汉明距离 + 平均绝对差 + 变化像素比）、稳定帧采集（动态延迟感知 + 绝对超时）、二次缩距滚动确认到底（手势缩 50% + 三组两两相同）

### Modified Capabilities
- `screen-state-provider`: `ScreenStateResult` 删除 `HierarchyXml`/`HierarchyFingerprint` 字段；`IObservableScreenStateProvider.RefreshAsync` 删除 `previousHierarchyXml`/`afterScroll` 参数
- `scroll-swipe-config`: `ScrollSwipeConfig` 新增 12 个字段（stable sample config + snapshot size + similarity thresholds + anti-deadloop + second swipe ratio）
- `vision-screen-state-with-uia-fallback`: 删除 UIA fallback 能力，`VisionScreenStateProvider` 不再依赖 `IObservableScreenStateProvider` 侧信道，`RefreshAsync` 简化为直接返回 vision 状态
- `scroll-aware-traversal`: `TryHandleScrollAsync` 行为从"UIA 指纹快路径 + seen-set diff"迁移到"ROI 聚合比对"

## Impact

- **Core.Traversal**: `InterceptionHandler`, `ScrollSwipeConfig`, `VisionScreenStateProvider`, `IObservableScreenStateProvider`, `IScreenStateProvider`, `ScreenStateResult`, `DefaultScreenStateProvider` (+7 新文件)
- **Core.Observation**: `ObservationPipeline`, `ObservationConfig`
- **Core.StateMachine**: `StepContext`（`ScreenState` 字段类型不变但 `IObservableScreenStateProvider` 签名变）
- **Device**: `AdbScreenStateProvider`（删除）, `IAdbSession` + `ProcessAdbSession` + `AdvancedSharpAdbSession`（删除 `DumpUiHierarchyAsync`）
- **Host**: `HostCommands`, `RunAssetHook`, `ScenarioObservation`
- **Tests**: UIA 相关测试 fixture 需更新或删除
- **Python**: 无改动
