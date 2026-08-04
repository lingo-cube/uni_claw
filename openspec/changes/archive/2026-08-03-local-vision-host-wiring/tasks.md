# Tasks: Local Vision Host Assembly Wiring

## 1. VisionScreenStateProvider 升级

- [x] 1.1 修改 `VisionScreenStateProvider` 构造函数，新增 `IObservableScreenStateProvider? uia` 参数（默认 null）
- [x] 1.2 实现 `IObservableScreenStateProvider` 接口的 `RefreshAsync` 方法——主路径 Vision，UIA 可选冗余
- [x] 1.3 UIA 调用 try/catch，失败不影响主流程
- [x] 1.4 更新 `VisionScreenStateProviderTests`——验证 `IObservableScreenStateProvider` 实现 + UIA mock 场景

## 2. CurrentPageAnalysisAccessor + AnalysisWritingDecorator

- [x] 2.1 创建 `src/UniClaw.Host/HostServices/CurrentPageAnalysisAccessor.cs`——`PageAnalysis? Current` 属性
- [x] 2.2 创建 `src/UniClaw.Host/HostServices/AnalysisWritingDecorator.cs`——`IPageAnalyzer` 装饰器，`AnalyzeCurrentPageAsync` 写完 accessor 再返回
- [x] 2.3 单元测试：装饰器正确 delegate 三个方法，`AnalyzeCurrentPageAsync` 后 accessor.Current 更新

## 3. PythonVisionService + LocalVisionProvider 路径修复

- [x] 3.1 修改 `PythonVisionService`——新增 `serverScriptPath` 构造器参数，移除内部 `Path.GetFullPath("tools/local_vision/server.py")`
- [x] 3.2 修改 `LocalVisionProvider`——移除 CWD fallback，构造器 `labelMappingConfigPath` 参数变为必需（null 或空抛异常）

## 4. Host 装配层

- [x] 4.1 在 `HostCompositionFactory` 新增 `_localPythonService` 字段
- [x] 4.2 `CreateProviders("local")` 强制检查 `DEEPSEEK_API_KEY`，缺失抛 `HostPreparationException`
- [x] 4.3 在 `RunScenarioAsync` 中解析 label-mapping.json 和 server.py 路径，设 `UNICLAW_LABEL_MAPPING` 环境变量
- [x] 4.4 在 `RunScenarioAsync` 中创建 `PythonVisionService` + 调用 `StartAsync()`（try 之前）
- [x] 4.5 在 `RunScenarioAsync` 的 finally 中调用 `pythonService.DisposeAsync()`
- [x] 4.6 修改 `CreateRunServices`——接收 `HttpClient? pythonClient`、`string labelMappingPath`、`CurrentPageAnalysisAccessor accessor` 参数
- [x] 4.7 `CreateRunServices` 条件装配：本地模式下 `VisionScreenStateProvider` + PageAnalyzer 直连；非本地模式保持现有 AdbScreenStateProvider + ObservationPipeline

## 5. 验证

- [x] 5.1 `dotnet build src/UniClaw.Core.sln` 无错误
- [x] 5.2 `dotnet test tests/UniClaw.Core.Tests` 全绿（含 `ArchitectureGuardTests`、`VisionScreenStateProviderTests`）
- [x] 5.3 `dotnet test tests/UniClaw.Host.Tests` 全绿
- [x] 5.4 验证 `PageAnalyzer` zero diff
- [x] 5.5 验证 `IScreenStateProvider` 4 方法锁不变
- [x] 5.6 验证非本地模式（mock/claude/qwen）装配路径不受影响
