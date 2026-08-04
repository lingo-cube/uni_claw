# Local Vision — Host Assembly Wiring PRD

> 日期: 2026-08-03
> 状态: draft
> 前置: `docs/prd/2026-08-03-local-vision-provider-prd.md`（已实现）
> 范围: `src/UniClaw.Host/` + `src/UniClaw.Core/Traversal/` + `src/UniClaw.LocalVisionProvider/`

## 1. Motivation

Local Vision Provider 的 Core 实现已完成（Python 服务、C# IModelProvider、4 步映射管道），但 Host 装配层存在 4 个运行时 gap，导致 `--provider local` **完全不可用**：

| # | 问题 | 症状 |
|---|------|------|
| G1 | Python 进程从未启动 | `HttpClient` 为 null，一调 `CompleteVisionAsync` 就 NPE |
| G2 | ScreenState 写死 UIA 实现 | 本地模式无 UIAutomator，滚动判断不可靠 |
| G3 | 文本能力 provider 可能缺失 | `ModelRouter` 构造期 `DomainValidationException` |
| G4 | label-mapping.json 路径脆弱 | CWD 相对路径，非项目根启动找不到 |

目标是修复 Host 装配，使 `--provider local` 可端到端运行：
- Python 服务在 engine 前启动、结束后关闭
- 滚动状态从 PageAnalysis 读取而非 UIA
- 文本能力由 DeepSeek API 提供（已验证可用）
- 配置路径由 Host 统一解析、同时注入 C# 和 Python

## 2. Architecture

### 2.1 完整装配流程

```
RunScenarioAsync()
 │
 ├─ path = ResolveLabelMappingPath()                            ← G4
 ├─ env["UNICLAW_LABEL_MAPPING"] = path
 │
 ├─ pythonService = new PythonVisionService(
 │       serverScriptPath: ResolveServerScriptPath())           ← G4
 ├─ await pythonService.StartAsync()                            ← G1
 │
 ├─ accessor = new CurrentPageAnalysisAccessor()                ← G2
 │
 ├─ CreateRunServices(pythonService.HttpClient, path, accessor)
 │    │
 │    ├─ screenState = new VisionScreenStateProvider(
 │    │       getAnalysis: () => accessor.Current,
 │    │       uia: isLocal ? null : new AdbScreenStateProvider(runner))
 │    │                                                         ← G2
 │    ├─ providers = CreateProviders("local")
 │    │    ├─ ["local-vision"] = new LocalVisionProvider(
 │    │    │       pythonClient, labelMappingConfigPath: path)  ← G4
 │    │    └─ ["deepseek"] = new DeepSeekModelProvider(...)     ← G3
 │    │
 │    ├─ innerAnalyzer = isLocal
 │    │       ? brain.PageAnalyzer                              ← 直连
 │    │       : new ObservationPipeline(brain.PageAnalyzer, ...) ← UIA 富化
 │    ├─ cache = new InvalidatingPageAnalysisCache(innerAnalyzer)
 │    └─ pageAnalyzer = new AnalysisWritingDecorator(
 │            cache, accessor)                                  ← G2
 │
 ├─ try { engine.RunAsync() }
 └─ finally
      ├─ await services.DisposeAsync()
      └─ await pythonService.DisposeAsync()                     ← G1
```

### 2.2 新增类型

| 类型 | 位置 | 职责 |
|------|------|------|
| `CurrentPageAnalysisAccessor` | `src/UniClaw.Host/` | 共享状态持有者——`PageAnalysis? Current { get; set; }` |
| `AnalysisWritingDecorator` | `src/UniClaw.Host/` | `IPageAnalyzer` 装饰器——拦截 `AnalyzeCurrentPageAsync`，写完 accessor 再返回 |
| `HostRunServices` 扩展 | `src/UniClaw.Host/` | 新增 `PythonVisionService?` 属性，供 lifecycle 管理 |

### 2.3 依赖图

```
                         ┌─────────────────────┐
                         │ CurrentPageAnalysis  │
                         │     Accessor         │
                         └──────┬──────────────┘
                                │
              ┌─────────────────┼─────────────────┐
              │                 │                 │
     VisionScreenState    AnalysisWriting      Host 装配
     Provider (读)        Decorator (写)       (创建/注入)
```

### 2.4 能力路由

```
page_analysis      → CapabilityRouting["page_analysis"]
                   → "local-vision"
                   → LocalVisionProvider  (Python YOLO+OCR)

traversal_advisor  → DefaultProvider
                   → "deepseek"
                   → DeepSeekModelProvider (API, text-only)

text_understanding → DefaultProvider
                   → "deepseek"
                   → DeepSeekModelProvider (API, text-only)
```

## 3. 详细设计

### 3.1 G1 — Python 生命周期

**现状**：`CreateProviders` 里 `new PythonVisionService()` 后 `HttpClient` 是 `null!`。StartAsync 没人调，DisposeAsync 没人调。

**方案**：生命周期提升到 `RunScenarioAsync`。

```csharp
// HostCompositionFactory 新增字段
private PythonVisionService? _localPythonService;

// RunScenarioAsync 中
var pythonService = new PythonVisionService();
await pythonService.StartAsync(ct);  // 启动进程 + 等 health warm:true (30s 超时)
_localPythonService = pythonService;

try
{
    // engine.RunAsync() ...
}
finally
{
    if (_localPythonService != null)
        await _localPythonService.DisposeAsync();
}
```

`CreateRunServices` 接收 `HttpClient? pythonClient` 参数——local 模式传入，其他模式传 null 跳过。

### 3.2 G2 — ScreenState

**现状**：`CreateRunServices` 写死 `new AdbScreenStateProvider(runner)`。本地模式无 UIAutomator，滚动判断应从已分析的 `PageAnalysis` 读取。

**约束**：`HostRunServices.ScreenState` 类型为 `IObservableScreenStateProvider`——`RunAssetHook` 需要 `RefreshAsync()` 做步骤截图。本地模式应保留此能力（截图来自 ADB，hierarchy 可选）。

**设计决策**：`VisionScreenStateProvider` 同时实现 `IObservableScreenStateProvider`。接口名 "Observable" 表达"可主动查询"而非"必须用 UIA"——Vision 只是数据源不同（PageAnalysis vs UIA dump），抽象一致。

```ascii
              ┌──────────────────────────────────────────┐
              │   VisionScreenStateProvider              │
              │   : IObservableScreenStateProvider       │
              │                                          │
              │   主路径 ──→ PageAnalysis (Vision)        │
              │         ├─ HasScroll()                   │
              │         ├─ IsEndOfList()                 │
              │         └─ RefreshAsync().HasScroll 等   │
              │                                          │
              │   冗余路径 ──→ IObservableScreenStateProvider? (UIA, nullable)
              │         └─ RefreshAsync().HierarchyXml   │
              │            UIA 挂了不影响主流程           │
              └──────────────────────────────────────────┘
```

**`CurrentPageAnalysisAccessor`**（Host 层共享状态持有者）：
```csharp
// src/UniClaw.Host/HostServices/
public sealed class CurrentPageAnalysisAccessor
{
    public PageAnalysis? Current { get; set; }
}
```

**`AnalysisWritingDecorator`**（Host 层 IPageAnalyzer 装饰器，分析完自动写 accessor）：
```csharp
public sealed class AnalysisWritingDecorator : IPageAnalyzer
{
    // AnalyzeCurrentPageAsync → await inner → accessor.Current = result → return
    // FindAppEntryAsync / VerifyPageTypeAsync → 直接 delegate
}
```

**装配**：
```csharp
accessor = new CurrentPageAnalysisAccessor();
screenState = new VisionScreenStateProvider(
    () => accessor.Current,
    uia: isLocalMode ? null : new AdbScreenStateProvider(runner));
//                                 ↑ 本地模式传 null，UIA 不可用时主路径仍正常工作

cache = new InvalidatingPageAnalysisCache(
    isLocalMode ? brain.PageAnalyzer                      // 本地: 直连
                : new ObservationPipeline(brain.PageAnalyzer, screenState, ...));
//                ↑ 云端: ObservationPipeline 做 UIA→AI 富化

pageAnalyzer = new AnalysisWritingDecorator(cache, accessor);
```

**不变量**：`IScreenStateProvider` 4 方法锁不变；`PageAnalyzer` 零改动；UIA 挂掉不影响遍历主流程。

### 3.3 G3 — 文本 provider

**方案**：`CreateProviders("local")` 强制检查 `DEEPSEEK_API_KEY`，不存在就抛 `HostPreparationException`。

```csharp
var deepseekApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
if (string.IsNullOrWhiteSpace(deepseekApiKey))
{
    throw new HostPreparationException(
        "DEEPSEEK_API_KEY is required for local-vision mode. "
        + "Local vision handles screenshots; text reasoning (decide_next_action, "
        + "parse_instruction) requires a separate text provider.");
}
```

已验证：`DeepSeekModelProvider` 20 个测试全过，`CompleteTextAsync` 可用。

### 3.4 G4 — 配置路径

**问题**：C# `LocalVisionProvider` 和 Python `server.py` 各有一处 CWD 相对路径：
- `LocalVisionProvider` 默认 `"tools/local_vision/label-mapping.json"`
- `PythonVisionService.StartProcessAsync` 里 `Path.GetFullPath("tools/local_vision/server.py")`

从非项目根目录启动时两边都找不到文件。

**方案**：Host 启动时解析一次，通过两条路径注入：

```csharp
// 两处路径均由 Host 解析
var labelMappingPath = Environment.GetEnvironmentVariable("UNICLAW_LABEL_MAPPING")
    ?? Path.GetFullPath("tools/local_vision/label-mapping.json");
var serverScriptPath = Path.GetFullPath("tools/local_vision/server.py");

Environment.SetEnvironmentVariable("UNICLAW_LABEL_MAPPING", labelMappingPath);
// Python server 通过 env var 读 label-mapping.json
// server.py 路径通过 PythonVisionService 构造器传入

var pythonService = new PythonVisionService(
    serverScriptPath: serverScriptPath);            // ← 新增参数
var provider = new LocalVisionProvider(
    httpClient,
    labelMappingConfigPath: labelMappingPath);      // ← 显式路径
```

`LocalVisionProvider` 移除 CWD fallback；`PythonVisionService` 新增 `serverScriptPath` 构造器参数，不再内部拼路径。

## 4. 改动清单

| 文件 | 变更 | 关联 |
|------|------|------|
| `src/UniClaw.Host/HostServices/CurrentPageAnalysisAccessor.cs` | **新** | G2 |
| `src/UniClaw.Host/HostServices/AnalysisWritingDecorator.cs` | **新** | G2 |
| `src/UniClaw.Host/Commands/HostCommands.cs` | 生命周期 + 条件装配 + 路径解析 | G1-G4 |
| `src/UniClaw.Core/Traversal/VisionScreenStateProvider.cs` | +`IObservableScreenStateProvider` + UIA nullable 冗余 | G2 |
| `src/UniClaw.Device/PythonVisionService.cs` | +`serverScriptPath` 构造器参数 | G4 |
| `src/UniClaw.LocalVisionProvider/LocalVisionProvider.cs` | 移除 CWD fallback，path 参数变为必需 | G4 |
| `tests/UniClaw.Core.Tests/LocalVision/VisionScreenStateProviderTests.cs` | 更新：验证 IObservableScreenStateProvider 实现 | G2 |
| `tests/UniClaw.Host.Tests/` | 新测试（Python 生命周期、条件装配） | G1, G2 |

## 5. Acceptance Criteria

### 5.1 硬性验收

| # | 标准 |
|---|------|
| V1 | `--provider local` 启动 → Python 进程启动，health check warm:true 后 engine 开始执行 |
| V2 | engine 正常结束或异常退出 → Python 进程被 Kill |
| V3 | 本地模式下 `ctx.ScreenState` 为 `VisionScreenStateProvider` 实例 |
| V4 | `VisionScreenStateProvider.HasScroll()` 返回最近一次 PageAnalyzer 分析的 `HasScroll` 值 |
| V5 | `DEEPSEEK_API_KEY` 缺失 → `HostPreparationException`，消息包含 "DEEPSEEK_API_KEY" |
| V6 | `DEEPSEEK_API_KEY` 存在 → `decide_next_action` / `parse_instruction` 正常调用 |
| V7 | `UNICLAW_LABEL_MAPPING` 指向不存在的路径 → Python 启动时 server.py 抛出清晰错误 |
| V8 | 从非项目根目录启动 `--provider local` → 仍然能找到 label-mapping.json |
| V9 | `ArchitectureGuardTests` 全绿 |
| V10 | `PageAnalyzer` zero diff |

### 5.2 集成验收（emulator-gated）

| # | 标准 |
|---|------|
| I1 | `--provider local --scenario scenarios/settings.json` 端到端运行 → scenario 正常完成 |
| I2 | 真机 ADB 截图 → Python YOLO+OCR 检测到 UI 元素 → PageAnalyzer 生成 PageAnalysis → engine 遍历 |

## 6. Decisions

| ID | 决策 | 理由 |
|----|------|------|
| D-1 | Python 生命周期在 `RunScenarioAsync` 层管理 | 与 engine 生命周期对齐；CreateProviders 只负责构造，不负责启动 |
| D-2 | `CurrentPageAnalysisAccessor` 放在 Host 层 | Core 不需要感知共享状态模式；纯 Host 装配胶水代码 |
| D-3 | `AnalysisWritingDecorator` 包装完整 `IPageAnalyzer` | 3 个方法全 delegate，只有 `AnalyzeCurrentPageAsync` 加一行写 accessor |
| D-4 | label-mapping.json + server.py 路径均由 Host 解析、显式传入 | 消除 CWD 依赖；Python 通过 env var 同步；构造器不接受 CWD fallback |
| D-5 | 文本 provider 缺失时 fail-fast（构造前） | 清晰错误 > 运行时崩溃 |
| D-6 | `VisionScreenStateProvider` 实现 `IObservableScreenStateProvider` | "Observable" 表达"可主动查询"，非"必须用 UIA"；`HostRunServices.ScreenState` 类型不降级 |
| D-7 | UIA 作为 Vision 的冗余副路径，不影响主流程 | RunAssetHook 仍可截图；hierarchy 有就有、没有拉倒；UIA 故障不阻塞遍历 |
| D-8 | 本地模式跳过 `ObservationPipeline`，`PageAnalyzer` 直连 | 本地无 UIA 富化数据；避免空转；ObservationPipeline 仅用于云端/模拟模式 |

## 7. 不变量

1. `PageAnalyzer` 零改动
2. `IScreenStateProvider` 接口 4 方法锁不变
3. Core 无 `Process`、无 `PythonVisionService` using
4. 能力路由语义不变——vision → local-vision，text → deepseek
