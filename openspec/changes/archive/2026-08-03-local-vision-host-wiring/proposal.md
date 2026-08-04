# Proposal: Local Vision Host Assembly Wiring

## Why

Local Vision Provider 的 Core 实现（Python 服务、C# IModelProvider、4 步映射管道）已完成并通过 1083 个单元测试，但 Host 装配层存在 4 个运行时 gap 导致 `--provider local` 完全不可用：Python 进程从未启动（NPE）、ScreenState 写死 UIA、文本能力 provider 可能缺失、配置路径依赖 CWD。修复这些装配缺陷即可端到端运行本地视觉遍历。

## What Changes

- **Python 生命周期管理**：在 `RunScenarioAsync` 层管理 `PythonVisionService` 的 `StartAsync` / `DisposeAsync`，与 engine 生命周期对齐
- **VisionScreenStateProvider 升级**：新增 `IObservableScreenStateProvider` 实现 + 可选 UIA 冗余副路径。UIA 故障不影响 Vision 主流程
- **CurrentPageAnalysisAccessor**（新 Host 类型）：共享状态持有者，连接装饰器（写端）和 ScreenState provider（读端）
- **AnalysisWritingDecorator**（新 Host 类型）：薄 IPageAnalyzer 装饰器，每次分析完自动更新 accessor
- **条件装配**：本地模式跳过 `ObservationPipeline`（无 UIA 可富化），PageAnalyzer 直连 LocalVisionProvider。云端/模拟模式保持现有流程不变
- **路径解析集中化**：Host 启动时解析 label-mapping.json 和 server.py 的绝对路径，显式传入构造器，消除 CWD 依赖
- **文本 provider 强制检查**：本地模式下 `DEEPSEEK_API_KEY` 缺失时抛 `HostPreparationException`，不再静默崩溃
- **`PythonVisionService` 接收显式路径**：新增 `serverScriptPath` 构造器参数，不再内部拼路径

## Capabilities

### New Capabilities

- `host-lifecycle-management`: Python 进程生命周期与 engine 对齐（StartAsync → engine.RunAsync → DisposeAsync）
- `vision-screen-state-with-uia-fallback`: VisionScreenStateProvider 实现 IObservableScreenStateProvider，主路径 Vision + 冗余 UIA
- `analysis-page-accessor`: Host 层共享状态模式——CurrentPageAnalysisAccessor + AnalysisWritingDecorator 连接分析结果与消费方
- `conditional-page-analyzer-assembly`: 本地模式跳过 ObservationPipeline，PageAnalyzer 直连；云端保持现有 UIA→AI 富化链路
- `centralized-path-resolution`: Host 统一解析 label-mapping.json 和 server.py 路径，消除 CWD 依赖

### Modified Capabilities

- `screen-state-provider`: VisionScreenStateProvider 接口实现从 `IScreenStateProvider` 扩展为 `IObservableScreenStateProvider`，新增 UIA nullable 冗余
- `model-provider`: LocalVisionProvider 构造器移除 CWD fallback，labelMappingConfigPath 参数变为必需

## Impact

- **Host**: 新增 2 个类型（`CurrentPageAnalysisAccessor`、`AnalysisWritingDecorator`），`HostCompositionFactory` 重构装配逻辑
- **Core/Traversal**: `VisionScreenStateProvider` 新增 `IObservableScreenStateProvider` 实现
- **Device**: `PythonVisionService` 新增 `serverScriptPath` 参数
- **Provider**: `LocalVisionProvider` 移除 CWD fallback
- **无 breaking change**：`IScreenStateProvider` 4 方法锁不变，`PageAnalyzer` 零改动，云端/模拟模式装配路径不变
