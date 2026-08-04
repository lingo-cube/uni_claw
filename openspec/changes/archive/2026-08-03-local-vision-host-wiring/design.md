# Design: Local Vision Host Assembly Wiring

## Context

`--provider local` 的 Core 实现已完成（Python 服务、C# IModelProvider、4 步映射管道），1083 个单元测试通过。但 Host 装配层 4 个 gap 阻止实际运行。此设计聚焦 Host 层修复，不改变 Core 架构。

## Goals / Non-Goals

**Goals:**
- 修复 `--provider local` 的端到端可运行性
- Python 进程生命周期与 engine 对齐
- 本地模式 ScreenState 从 PageAnalysis 读取
- 本地模式跳过 ObservationPipeline（无 UIA 数据可富化）
- 统一路径解析，消除 CWD 依赖
- 文本能力强制 fail-fast

**Non-Goals:**
- 修改 Core 层接口或 PageAnalyzer
- 修改 IScreenStateProvider 的 4 方法锁
- 云端/模拟模式的装配路径（保持现有行为）

## Decisions

| ID | 决策 | 理由 |
|----|------|------|
| D-1 | Python 生命周期在 `RunScenarioAsync` 层 | 与 engine 对齐；CreateProviders 只构造不启动 |
| D-2 | `CurrentPageAnalysisAccessor` 放 Host 层 | Core 无需感知；纯装配胶水 |
| D-3 | `AnalysisWritingDecorator` 包装完整 `IPageAnalyzer` | 3 方法全 delegate，仅 `AnalyzeCurrentPageAsync` 拦截 |
| D-4 | 路径 Host 解析、显式传入构造器 | 消除 CWD 依赖；Python 通过 env var 同步 |
| D-5 | 文本 provider 缺失 fail-fast | 清晰错误 > 运行时崩溃 |
| D-6 | `VisionScreenStateProvider` 实现 `IObservableScreenStateProvider` | 接口表达 "可主动查询"，非 "必须用 UIA"；`HostRunServices.ScreenState` 不降级 |
| D-7 | UIA 作为 Vision 冗余，不影响主流程 | RunAssetHook 仍可截图；UIA 故障不阻塞遍历 |
| D-8 | 本地模式跳过 `ObservationPipeline` | 无 UIA 可富化；避免空转 |

## Assembled Flow

```
RunScenarioAsync()
 ├─ path = ResolveLabelMappingPath()
 ├─ env["UNICLAW_LABEL_MAPPING"] = path
 ├─ pythonService = new PythonVisionService(serverScriptPath: ResolveServerScriptPath())
 ├─ await pythonService.StartAsync()
 ├─ accessor = new CurrentPageAnalysisAccessor()
 ├─ CreateRunServices(pythonService.HttpClient, path, accessor)
 │    ├─ screenState = new VisionScreenStateProvider(
 │    │       getAnalysis: () => accessor.Current,
 │    │       uia: isLocal ? null : new AdbScreenStateProvider(runner))
 │    ├─ providers["local-vision"] = new LocalVisionProvider(..., labelMappingConfigPath: path)
 │    ├─ providers["deepseek"] = new DeepSeekModelProvider(...)
 │    ├─ innerAnalyzer = isLocal ? brain.PageAnalyzer : new ObservationPipeline(...)
 │    ├─ cache = new InvalidatingPageAnalysisCache(innerAnalyzer)
 │    └─ pageAnalyzer = new AnalysisWritingDecorator(cache, accessor)
 ├─ try { engine.RunAsync() }
 └─ finally { await services.DisposeAsync(); await pythonService.DisposeAsync() }
```

## New Types

| 类型 | 位置 | 职责 |
|------|------|------|
| `CurrentPageAnalysisAccessor` | Host | `PageAnalysis? Current { get; set; }` |
| `AnalysisWritingDecorator` | Host | IPageAnalyzer 装饰器，分析完写 accessor |

## Risks / Trade-offs

- **[Desktop mode] AdbScreenStateProvider fails when no device connected**: Mitigated — local mode passes `uia: null`, VisionScreenStateProvider main path works standalone
- **[Startup time] Python health check 30s timeout**: First YOLO load ~22s (model download + warmup). Mitigation — model pre-downloaded, timeout configurable
- **[Coordination] Accessor writes happen after analysis completes**: InterceptionHandler calls AnalyzeCurrentPageAsync THEN checks ScrollState — one-frame lag is correct by design
