# Local Vision Provider — Python YOLO+OCR 视觉服务集成

> 日期: 2026-08-03
> 状态: approved
> 范围: `tools/local_vision/server.py` (Python) + `src/UniClaw.Device/` (C#) + `src/UniClaw.Core/UniBrain/` (Core) + `src/UniClaw.Core/Traversal/` (VisionScreenStateProvider)

## 1. Motivation

`tools/local_vision/` 已有完整的 YOLO+PaddleOCR 管线（`analyze.py` + `fusion.py`），输出 `uniclaw.localVisionEvidence.v1` 证据 JSON，但**零 C# 集成**——当前只能通过 CLI 一次一图调用。

目标：将 Python 管线包装为常驻 FastAPI 服务，C# 侧通过 `IModelProvider` 接缝注入，`PageAnalyzer` 零改动即可在云端 AI 和本地视觉之间切换。

## 2. Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    C# UniClaw.Core                        │
│                                                           │
│  PageAnalyzer (不动)                                      │
│    ├─ IScreenCapture.CaptureAsync() → byte[]              │
│    ├─ IPromptLibrary.GetTemplate(AnalyzeVisual)           │
│    ├─ IModelProvider.CompleteVisionAsync(req, bytes) ─────┼───┐
│    └─ JsonSerializer.Deserialize<PageAnalysisDto>(json)   │   │
│                                                           │   │
│  LocalVisionProvider : IModelProvider  ◀──────────────────┼───┤
│    ├─ HttpClient POST /v1/analyze → evidence JSON         │   │
│    ├─ LabelMappingConfig : YOLO label → AI type           │   │
│    ├─ 空间推理: Y 轴聚类 → menus                          │   │
│    ├─ 滚动判断: candidates 位置 → has_scroll/is_end_of_list │   │
│    └─ → PageAnalysisDto JSON                              │   │
│                                                           │   │
│  VisionScreenStateProvider : IScreenStateProvider           │   │
│    └─ 薄包装: 从 PageAnalysis 读取 HasScroll/IsEndOfList   │   │
│                                                           │   │
│  PythonVisionService : IPythonVisionService                │   │
│    ├─ Process 生命周期 (start/auto-restart/stop)          │   │
│    ├─ UDS/TCP 双模式 HttpClient 工厂                      │   │
│    └─ 健康检查 + 退避重试                                  │   │
└──────────────────────────────────────────────────────────┘   │
                                                                │
┌───────────────────────────────────────────────────────────┐   │
│            Python FastAPI (tools/local_vision/)            │   │
│                                                           │   │
│  POST /v1/analyze   ← raw JPEG bytes                     │◀──┘
│    ├─ PIL.Image.open(BytesIO(...)) — 零磁盘 I/O           │
│    ├─ run_yolo()     → Detections[]                       │
│    ├─ run_paddle_ocr() → OcrTokens[]                      │
│    ├─ fuse_evidence() → candidates[]                      │
│    ├─ build_scroll_hints() → scrollHints{}                │
│    └─ → evidence JSON (uniclaw.localVisionEvidence.v1)    │
│                                                           │
│  GET /health → {"status": "ok"}                           │
│                                                           │
│  OMP_NUM_THREADS=4  gc.collect() per request              │
└───────────────────────────────────────────────────────────┘
```

## 3. Python FastAPI 服务

### 3.1 新增文件: `tools/local_vision/server.py`

复用现有 `backends.py`（`run_yolo`、`run_paddle_ocr`）、`fusion.py`（`fuse_evidence`）、`schema.py`。新增 `server.py`：

```python
# server.py — FastAPI wrapper around existing analyze pipeline
import gc
import os
from io import BytesIO
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from PIL import Image

from .backends import run_paddle_ocr, run_yolo
from .fusion import fuse_evidence

os.environ["OMP_NUM_THREADS"] = "4"


@asynccontextmanager
async def lifespan(app: FastAPI):
    # 启动时预热模型（避免首次调用加载超时）
    warmup_yolo()
    warmup_ocr()
    yield


app = FastAPI(lifespan=lifespan)


@app.post("/v1/analyze")
async def analyze(request: Request):
    image_bytes = await request.body()
    image = Image.open(BytesIO(image_bytes))
    width, height = image.size

    detections = run_yolo(image, model_path="yolo11n.pt", image_size=640,
                          confidence=0.35, device="cpu")
    ocr_tokens = run_paddle_ocr(image, language="ch")
    evidence = fuse_evidence(detections, ocr_tokens,
                             image_width=width, image_height=height)
    evidence["metadata"] = _metadata(width, height)
    evidence["scrollHints"] = _scroll_hints(
        evidence["candidates"], width, height)

    gc.collect()  # PaddleOCR 长周期压测防内存泄漏
    return evidence


@app.get("/health")
async def health():
    return {"status": "ok"}
```

### 3.2 关键决策

| 决策 | 理由 |
|------|------|
| 图片走 HTTP body，不存盘 | PIL 直接从 `BytesIO` 读，零磁盘 I/O，对齐 ADB 内存截图管道 |
| `gc.collect()` per request | PaddleOCR 长周期压测已知内存泄漏，手动回收 |
| 模型预热在 lifespan | Ultralytics 首次 load 可能 5-10s，预热避免首次调用超时 |
| `OMP_NUM_THREADS=4` | 防止 AI 推理抢占 C# 控制算力 |
| evidence schema 不变 | 复用 `uniclaw.localVisionEvidence.v1`，`fusion.py` 零改动；新增 `scrollHints` 字段（原始数据，不做判断） |
| `scrollHints` 只含原始值 | Python 不做滚动判断——`totalCandidates`、`candidatesNearBottom`、`scrollbarDetected` 三个原始值，判断逻辑在 C# 侧 |

### 3.3 启动命令

```bash
# Unix (macOS/Linux) — UDS
uvicorn server:app --uds /tmp/uniclaw-vision.sock

# Windows — TCP
uvicorn server:app --host 127.0.0.1 --port 8765
```

### 3.4 新增依赖 (`requirements.txt`)

```
fastapi
uvicorn[standard]
# 已有: ultralytics, paddleocr, pillow
```

### 3.5 Evidence scrollHints 字段

Python 只返回原始可观测值，**不做滚动判断**。判断逻辑在 C# `LocalVisionProvider` 中（见 §5.2 Step 4）。

```json
{
  "candidates": [...],
  "scrollHints": {
    "totalCandidates": 12,
    "candidatesNearBottom": 3,
    "scrollbarDetected": true
  }
}
```

| 字段 | 类型 | 含义 |
|------|------|------|
| `totalCandidates` | int | YOLO 检测到的交互元素总数 |
| `candidatesNearBottom` | int | 中心点 Y > 0.85 的候选数 |
| `scrollbarDetected` | bool | YOLO 是否检测到 scrollbar 控件 |

C# 侧判断逻辑：
- `has_scroll`: `totalCandidates > estimatedVisibleCapacity` 或 `scrollbarDetected`
- `is_end_of_list`: `candidatesNearBottom == 0`（没有候选贴在屏幕边缘外）

## 4. Label Mapping 配置

### 4.1 配置文件: `tools/local_vision/label-mapping.json`

```json
{
  "schema": "uniclaw.labelMapping.v1",
  "mappings": {
    "button":    "menu_item",
    "list_item": "menu_item",
    "tab":       "menu_item",
    "icon":      "menu_item",
    "toolbar":   "menu_item",
    "back":      "menu_item",
    "switch":    "toggle",
    "checkbox":  "toggle",
    "input":     "input",
    "slider":    "slider",
    "text_block": "info"
  },
  "nonItemLabels": ["popup"],
  "spatial": {
    "level1MaxY": 0.08,
    "edgeThreshold": 0.92
  }
}
```

### 4.2 加载与校验

```csharp
// LocalVisionProvider 构造期
var path = configPath
    ?? Environment.GetEnvironmentVariable("UNICLAW_LABEL_MAPPING")
    ?? "tools/local_vision/label-mapping.json";

var json = File.ReadAllText(path);
_config = JsonSerializer.Deserialize<LabelMappingConfig>(json, DomainJsonOptions.Default)
    ?? throw new DomainValidationException("labelMapping", null, "config null or invalid.");

// fail-fast: 每个 mapping value 必须通过 ElementTypeMapper 校验
foreach (var (_, aiType) in _config.Mappings)
{
    if (!ElementTypeMapper.IsValidType(aiType))
        throw new DomainValidationException("labelMapping", aiType,
            $"'{aiType}' is not a recognized AI type.");
}
```

**关键决策：**

- **构造期 fail-fast**。全量校验 mapping 值 → 配置错误不等到运行时才发现。
- **`spatial` 参数可调**。不同车机屏幕比例可能需要不同的 `level1MaxY`（顶部 tab 栏 Y 阈值）和 `edgeThreshold`（边缘贴附阈值）。
- **`nonItemLabels`**。`popup` 只设置 `is_popup` 标志，不进入 items 数组。
- **缺失 label fallback**。YOLO label 不在 mapping 表中 → 默认 `info`，记录 warning 日志。
- **路径可覆盖**。`UNICLAW_LABEL_MAPPING` 环境变量或构造器参数。

## 5. LocalVisionProvider : IModelProvider

### 5.1 接口实现

放 `src/UniClaw.Core/UniBrain/LocalVisionProvider.cs`。

```
实现 IModelProvider:
  CompleteVisionAsync     → HTTP → Python → evidence → PageAnalysisDto JSON → ModelResponse
  CompleteTextAsync       → NotImplementedException
  CompleteMultimodalAsync → NotImplementedException
```

### 5.2 映射管道（4 步）

```
candidates[] + scrollHints      ──────►  PageAnalysisDto JSON
─────────────────────────                ────────────────────
type: "switch"   ── Step 1: YOLO → type
text: "Wi-Fi"
center: (0.5,0.15)                      items: [
                                          { name:"Wi-Fi", type:"toggle",
                                            coordinate:{x:0.5,y:0.15} },
                                        ]

type: "tab"      ── Step 2: Y 轴聚类 → menus
center: (0.15,0.05)                     level1_menus: [{...}]
type: "tab"                             level1_dir: "horizontal"
center: (0.35,0.05)

scrollHints{}   ── Step 3: scroll 判断
  totalCandidates: 12                   has_scroll: true
  candidatesNearBottom: 3               is_end_of_list: false
  scrollbarDetected: true

type:"popup"     ── Step 4: popup 检测    is_popup: true
```

**Step 1 — YOLO label → AI type 映射**：查 `LabelMappingConfig.Mappings`。默认表见 §4.1。

**Step 2 — Y 轴聚类 → menu 结构**：
- `center.y < level1MaxY` 的候选 → `level1_menus`
- X 方差 > Y 方差 → `level1_dir: "horizontal"`
- 其余 → `items`

**Step 3 — scroll 检测（从 evidence scrollHints）**：
- `totalCandidates > estimatedVisibleCapacity` 或 `scrollbarDetected` → `has_scroll: true`
- `candidatesNearBottom == 0` → `is_end_of_list: true`（没有候选贴在屏幕边缘外）
- `estimatedVisibleCapacity` 从 `image_height / avgItemHeight` 估算

**Step 4 — popup 检测**：
- 存在 type 为 `popup` 的检测框 → `is_popup: true`，提取最近的 close 候选作为 `close_button`

### 5.3 ModelResponse 构造

```csharp
public async Task<ModelResponse> CompleteVisionAsync(
    ModelRequest request, byte[] imageData, CancellationToken ct = default)
{
    var content = new ByteArrayContent(imageData);
    content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

    var httpResp = await _httpClient.PostAsync("/v1/analyze", content, ct);
    httpResp.EnsureSuccessStatusCode();

    var evidenceJson = await httpResp.Content.ReadAsStringAsync(ct);
    var evidence = JsonSerializer.Deserialize<LocalVisionEvidence>(
        evidenceJson, DomainJsonOptions.Default);

    var dto = MapToPageAnalysisDto(evidence);

    return new ModelResponse(
        Content: JsonSerializer.Serialize(dto, DomainJsonOptions.Default),
        ProviderId: "local-vision",
        Mode: "vision",
        InputTokens: 0,
        OutputTokens: 0,
        LatencyMs: 0,  // TODO: 记录 HTTP 往返延迟
        Success: true);
}
```

### 5.4 与 PageAnalyzer 的兼容性

`PageAnalyzer` 不动——它不关心 provider 是云端还是本地：

1. `_screenCapture.CaptureAsync()` → `byte[]`
2. `_promptLibrary.GetTemplate(AnalyzeVisual)` → prompt（本地 provider 忽略）
3. `_modelProvider.CompleteVisionAsync(req, bytes)` → `ModelResponse.Content` (JSON)
4. `JsonSerializer.Deserialize<PageAnalysisDto>(json)` → 经 `ElementTypeMapper` 派生 → `PageAnalysis`

关键：Step 1 的 YOLO label → AI type 映射在 provider 内部完成，输出的是 AI 兼容的 type 字符串（`menu_item`、`toggle`、`input`...），所以 `ElementTypeMapper.IsValidType()` 和 `ToMenuItemType()` 照常工作。

## 5.5 VisionScreenStateProvider : IScreenStateProvider

放 `src/UniClaw.Core/UniBrain/VisionScreenStateProvider.cs`。

local-vision 没有 UIAutomator，无法用 `AdbScreenStateProvider`。但 `InterceptionHandler.TryHandleScrollAsync` 需要 `IScreenStateProvider.HasScroll()` / `IsEndOfList()` 做快速门禁。

`VisionScreenStateProvider` 是一个 **薄包装**——从已分析的 `PageAnalysis` 读取滚动字段，不依赖 UIA：

```csharp
public sealed class VisionScreenStateProvider : IScreenStateProvider
{
    private readonly Func<PageAnalysis?> _getCurrentAnalysis;

    /// <param name="getCurrentAnalysis">
    /// 委托获取当前 PageAnalysis。local-vision 场景下由
    /// TraversalRuntimeContext.CurrentPageAnalysis 提供。
    /// </param>
    public VisionScreenStateProvider(Func<PageAnalysis?> getCurrentAnalysis)
    {
        _getCurrentAnalysis = getCurrentAnalysis
            ?? throw new ArgumentNullException(nameof(getCurrentAnalysis));
    }

    public bool HasScroll() =>
        _getCurrentAnalysis()?.HasScroll ?? false;

    public bool IsEndOfList() =>
        _getCurrentAnalysis()?.IsEndOfList ?? true;

    // local-vision 不支持 UIA fingerprint 快速路径
    // → InterceptionHandler 自动走 AI seen-set 差分路径
    public double GetScrollProgress() => 0.0;
    public ScrollSwipeConfig? GetScrollSwipeConfig() => null;
}
```

**与 `InterceptionHandler` 的兼容性**：

`InterceptionHandler.TryHandleScrollAsync` 有三条路径（不动）：

```
Line 438: ctx.ScreenState.HasScroll() / IsEndOfList()
          → VisionScreenStateProvider 从 PageAnalysis 回答 ✓

Line 451: if (ctx.ScreenState is IObservableScreenStateProvider)
          → VisionScreenStateProvider 不实现此接口
          → 跳过 UIA fingerprint 快速路径 ✓

Line 476: else → AI seen-set 差分
          → 全量重新分析，回退到安全路径 ✓
```

`VisionScreenStateProvider` 不实现 `IObservableScreenStateProvider`，所以 `InterceptionHandler` 自动跳过 UIA fingerprint 快速路径，走 seen-set 差分 + 全量重分析的安全路径。

**装配点**（HostCommands）：

```csharp
// local-vision 场景
var screenState = new VisionScreenStateProvider(
    () => ctx.CurrentPageAnalysis);
```

## 6. PythonVisionService 进程管理

### 6.1 接口

放 `src/UniClaw.Device/IPythonVisionService.cs`。

```csharp
public interface IPythonVisionService : IAsyncDisposable
{
    HttpClient HttpClient { get; }
    Task StartAsync(CancellationToken ct = default);
    bool IsRunning { get; }
}
```

### 6.2 实现

放 `src/UniClaw.Device/PythonVisionService.cs`。

**UDS/TCP 双模式 HttpClient 工厂**：

| 平台 | 模式 | 地址 |
|------|------|------|
| macOS / Linux | HTTP over UDS | `/tmp/uniclaw-vision.sock` |
| Windows | TCP loopback | `127.0.0.1:8765` |

UDS 实现：`SocketsHttpHandler.ConnectCallback` → `new UnixDomainSocketEndPoint(socketPath)`。

**生命周期状态机**：

```
Stopped
  │ StartAsync()
  ▼
Starting ── health check OK ──► Running
  │                                │
  │ health check 超时              │ process.Exited
  │ (retry < maxRestarts)          │ (自动拉起, 退避)
  │                                │
  └──► (重试)                      ▼
                             Starting (backoff)
                                   │
                                   │ restartCount > maxRestarts
                                   ▼
                                 Stopped (放弃)
                                   
DisposeAsync():
  Running/Starting
    │ Kill(entireProcessTree: true)
    ▼
  Stopped
```

**自动拉起退避序列**：

| 尝试 | 延迟 | 说明 |
|------|------|------|
| 1 | 0ms | 即时重连（可能只是进程瞬时退出） |
| 2 | 500ms | 短退避 |
| 3 | 1s | |
| 4 | 3s | |
| 5+ | 10s | 上限，超过 `maxRestarts`（默认 5）放弃 |

**StartAsync 阻塞健康检查**：

```csharp
public async Task StartAsync(CancellationToken ct = default)
{
    _process = new Process { ... };
    _process.Exited += OnProcessExited;
    _process.Start();

    // 阻塞等待 GET /health 返回 200（超时 30s）
    using var healthCts = new CancellationTokenSource(_healthTimeout);
    while (!await HealthCheckAsync(healthCts.Token))
    {
        if (_process.HasExited)
            throw new InvalidOperationException(
                $"Python vision process exited with code {_process.ExitCode}");
        await Task.Delay(200, healthCts.Token);
    }
}
```

**DisposeAsync 干净关闭**：

```csharp
public async ValueTask DisposeAsync()
{
    proc.Exited -= OnProcessExited;  // 避免关闭时触发重连
    proc.Kill(entireProcessTree: true);
    await Task.Run(() => proc.WaitForExit(5000));
    proc.Dispose();
    HttpClient.Dispose();
}
```

### 6.3 装配点

```csharp
// 应用启动时 (HostCommands 或 Program.cs)
var visionService = new PythonVisionService();
await visionService.StartAsync();

var localVision = new LocalVisionProvider(
    visionService.HttpClient);

// 应用关闭时
await visionService.DisposeAsync();
```

## 7. Error Handling

| 故障 | 处理 |
|------|------|
| Python 进程启动失败 | `StartAsync` → `InvalidOperationException` |
| 健康检查超时（30s） | `StartAsync` → `TimeoutException` |
| 进程异常退出 | `OnProcessExited` → 自动拉起（退避 + 上限） |
| HTTP 请求失败 | `LocalVisionProvider` → `HttpRequestException` → `PageAnalyzer` 重试（已有 `MaxAnalyzeAttempts=2`） |
| 配置映射表 value 非法 | 构造期 `DomainValidationException` fail-fast |
| YOLO label 不在映射表 | 默认 `info` + warning 日志 |

## 8. Testing Strategy

### 8.1 Python 侧

- `tools/local_vision/tests/` 已有测试数据
- 新增：`GET /health` 返回 200；`POST /v1/analyze` 对已知截图返回 expected evidence

### 8.2 C# 侧

| 测试 | 类型 | 说明 |
|------|------|------|
| `LabelMappingConfig_Deserialization` | 单元 | JSON 反序列化 + fail-fast 校验 |
| `MapToPageAnalysisDto_Basic` | 单元 | 给定 mock evidence → 输出合法 PageAnalysisDto |
| `MapToPageAnalysisDto_YoloLabelFallback` | 单元 | 未知 label → `info` |
| `MapToPageAnalysisDto_Level1MenuClustering` | 单元 | Y<0.08 候选 → level1_menus |
| `MapToPageAnalysisDto_ScrollDetection` | 单元 | scrollHints 原始值 → has_scroll/is_end_of_list 判断 |
| `VisionScreenStateProvider_ReadsFromAnalysis` | 单元 | HasScroll/IsEndOfList 从 PageAnalysis 正确读取 |
| `VisionScreenStateProvider_NotIObservable` | 单元 | 不实现 IObservableScreenStateProvider，确认 InterceptionHandler 走 seen-set 差分路径 |
| `PythonVisionService_Integration` | 集成 | emulator-gated：完整流程（不纳入此设计 scope） |

## 9. Decisions

| ID | 决策 | 理由 |
|----|------|------|
| D-1 | 映射逻辑放 C# 侧，Python 只返回 evidence | `ElementTypeMapper` 是 C# 单点真源；页面推理逻辑可 xUnit 覆盖 |
| D-2 | 映射配制成 JSON 文件 | 不同车机 YOLO label 可能不同，无需改代码 |
| D-3 | UDS (Unix) / TCP (Windows) 双模式 | macOS 开发 + Windows 部署，走操作系统原生最优路径 |
| D-4 | `gc.collect()` per request | PaddleOCR 已知内存泄漏，手动回收 |
| D-5 | 自动拉起有上限 | 无限重连 = 死循环，比快速失败更危险 |
| D-6 | 构造期 fail-fast 校验 label mapping | 配置错误不等到运行时 |
| D-7 | `CompleteTextAsync` / `CompleteMultimodalAsync` → NotImplementedException | 本地视觉不做文本推理 |
| D-8 | 滚动检测复用 `IScreenStateProvider`，不新增接口 | `ScrollableMockVisionService` 已有 `IPageAnalyzer + IScreenStateProvider` 同体模式；`ArchitectureGuardTests` 已锁定 4 方法 |
| D-9 | `VisionScreenStateProvider` 不实现 `IObservableScreenStateProvider` | 自动走 `InterceptionHandler` 的 seen-set 差分安全路径，不需要 UIA fingerprint |
| D-10 | Python 只返回 `scrollHints` 原始值，判断逻辑在 C# | Python 不做业务判断；C# 可 xUnit 测试滚动逻辑 |
