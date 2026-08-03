# Local Vision Provider — 设计审阅文档

> 精简版审阅文档，配套 `2026-08-03-local-vision-provider-prd.md`（完整版）。
> 本文档只保留**接口协议**（逐字段列出）与**架构约束**，供评审核对。
> 2026-08-03 已按对抗审阅修订（R-1~R-10，见 PRD §12）；正文与修订后 PRD 一致。

---

## 1. 一句话概述

将现有 Python YOLO+PaddleOCR 管线（`tools/local_vision/`）包装为常驻 FastAPI 服务，C# 侧通过 `IModelProvider` 接缝注入 —— `PageAnalyzer` 零改动，云端 AI 与本地视觉可切换。

## 2. 架构分层（依赖单向：Host → Device → Core）

```
┌────────────────────────────────────────────────────┐
│ Core (UniClaw.Core)      ← 纯逻辑，零直接 I/O      │
│   IModelProvider 接缝（仅接口，无传输层实现）       │
│   VisionScreenStateProvider : IScreenStateProvider │
│     从 PageAnalysis 读滚动状态（薄包装）            │
├────────────────────────────────────────────────────┤
│ UniClaw.LocalVisionProvider  ← 独立项目            │
│   与 ClaudeProvider/DeepSeekProvider 同模式         │
│   HTTP → evidence → PageAnalysisDto 映射           │
├────────────────────────────────────────────────────┤
│ Device (UniClaw.Device)  ← I/O 适配层              │
│   PythonVisionService : IPythonVisionService       │
│     进程生命周期 + UDS/TCP HttpClient 工厂          │
├────────────────────────────────────────────────────┤
│ Python (tools/local_vision/)  ← 视觉推理引擎       │
│   server.py: POST /v1/analyze → evidence JSON      │
└────────────────────────────────────────────────────┘
```

## 3. 模块职责与依赖约束

| 模块 | 位置 | 职责 | 禁止依赖 |
|---|---|---|---|
| `LocalVisionProvider` | `UniClaw.LocalVisionProvider/`（独立项目） | HTTP → evidence → PageAnalysisDto JSON | `PythonVisionService`、`Process`、ADB |
| `VisionScreenStateProvider` | `Core/Traversal/` | `HasScroll()`/`IsEndOfList()` 从 `PageAnalysis` 读取（门禁保守化） | UniBrain 目录（Guard 约束） |
| `PythonVisionService` | `Device/` | 进程启动/自愈/停止 + HttpClient 工厂 | `IModelProvider`、`PageAnalysis`、YOLO/OCR |
| `server.py` | `tools/local_vision/` | 收图 → YOLO → ROI OCR → evidence JSON | C#、ADB |

> R-1：Provider 独立项目与既有模式一致（AnthropicModelProvider / DeepSeekModelProvider 均独立程序集，`UniBrainFactory` 只收 Host 装配好的 providers dict）。

---

## 4. HTTP 协议（C# ⇄ Python）

### 4.1 传输层

| 平台 | 模式 | 地址（env 可覆盖） |
|---|---|---|
| macOS / Linux | HTTP over UDS | `/tmp/uniclaw-vision.sock`（`UNICLAW_VISION_SOCK`） |
| Windows | TCP loopback | `127.0.0.1:8765`（`UNICLAW_VISION_PORT`） |

### 4.2 端点清单

| 方法 | 路径 | 输入 | 输出 |
|---|---|---|---|
| `POST` | `/v1/analyze` | raw JPEG bytes | evidence JSON + `Server-Timing` header |
| `GET` | `/health` | — | `{"status": "ok", "warm": true}` |

> R-9：`warm` 区分"进程存活"与"模型预热完成"；`StartAsync` 就绪门 = `warm:true`。自愈前先 health 探测（存活即复用，不重复拉起）；启动前 unlink 残留 socket。

### 4.3 请求（POST /v1/analyze）

```
POST /v1/analyze
Content-Type: image/jpeg
X-Uniclaw-Trace-Id: engine-abc123     (可选)
X-Uniclaw-Step-Id: abc123-000042      (可选)

<raw JPEG bytes>
```

| Header | 类型 | 必填 | 含义 |
|---|---|---|---|
| `Content-Type` | `image/jpeg` | ✅ | 图片 MIME |
| `X-Uniclaw-Trace-Id` | string | ❌ | 当前 traversal run 的 trace ID（v1 预留：C# 不发送，Python 透传 + echo 进 metadata） |
| `X-Uniclaw-Step-Id` | string | ❌ | 当前 engine.step 的 span ID（同上） |
| Body | raw bytes | ✅ | JPEG 图片，零磁盘（PIL BytesIO 直读） |

### 4.4 响应（200 OK）

```
200 OK
Content-Type: application/json
Server-Timing: yolo;dur=45.2, ocr;dur=68.7, fusion;dur=2.3, scroll;dur=0.4
```

| Header | 格式 | 说明 |
|---|---|---|
| `Content-Type` | `application/json` | evidence JSON（schema 见 §5） |
| `Server-Timing` | W3C：`yolo;dur=X, ocr;dur=Y, fusion;dur=Z, scroll;dur=W` | 各阶段延迟 ms；**不进 JSON body** |

> R-3：非 2xx 响应 → C# 侧 graceful 返回 `Success=false`（与 AnthropicModelProvider 一致，不抛异常）。

---

## 5. Evidence JSON 协议（`uniclaw.localVisionEvidence.v1`）

### 5.1 顶层字段

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `image` | object | ✅ | `width`/`height`（原图像素） |
| `yolo` | array | ✅ | 原始 Detection[]（字段见 5.2） |
| `ocr` | array | ✅ | 原始 OcrToken[]（字段见 5.2；`scope` 预留，R-5 延后） |
| `candidates` | array | ✅ | 融合后的交互元素（字段见 5.3） |
| `summary` | object | ✅ | `yoloCount`/`ocrCount`/`candidateCount`/`unmatchedOcrCount`（unmatched 仅统计 roi scope） |
| `metadata` | object | ✅ | 版本信息（见 5.6） |
| `scrollHints` | object | ✅ | 滚动原始可观测值（字段见 5.5） |

### 5.2 yolo / ocr 元素字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | string | `det_N` / `ocr_N` |
| `label` / `text` | string | YOLO 归一化 label / OCR 文本 |
| `confidence` | float | 模型置信度 |
| `bounds` | object | 归一化 `x1/y1/x2/y2`（0~1） |
| `boundsPx` | [int×4] | 像素坐标 `[x1,y1,x2,y2]`（**统一基于原图**） |
| `center` | object | 归一化 `x`/`y` |
| `centerPx` | [int×2] | 像素中心 `[cx,cy]` |

> R-5（延后 v1.1）：滚动条带 OCR 暂不做。`token.scope` 字段预留（additive，默认 `"roi"`）；OCR-only token 永不提升为 candidate（`promote_unmatched_ocr` 恒 False，v1 期间此约束唯一承载点）。

### 5.3 candidate 元素字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | string | `candidate_N` |
| `type` | string | YOLO 归一化 label（**映射到 AI type 在 C# 侧做**） |
| `text` | string | 关联 OCR 文本（去重保序拼接；无则空串） |
| `confidence` | float | 工程评分（YOLO 0.72 + OCR 0.28；无 OCR 时 YOLO×0.85）。**仅排序/过滤/调试，非概率** |
| `confidenceDetail` | object | `{yolo, ocr}` 分项（R-7） |
| `bounds` / `boundsPx` | object / [int×4] | 同 5.2 |
| `center` / `centerPx` | object / [int×2] | 同 5.2 |
| `evidence.yoloId` | string \| null | 关联的 YOLO 框 ID（OCR 提升时 null） |
| `evidence.ocrIds` | [string] | 关联 OCR token ID（仅 roi scope） |
| `evidence.allIds` | [string] | 全部证据 ID（yolo + ocr） |
| `evidence.typeInferred` | string | 仅 chevron heuristic 重分类时出现：`row_alignment` |
| `riskFlags` | [string] | 见 5.4 |

### 5.4 riskFlags 枚举

| 值 | 含义 |
|---|---|
| `low_yolo_confidence` | YOLO 置信度 < 0.55 |
| `no_text_evidence` | 无关联 OCR 文本（icon/back/toolbar/popup 除外） |
| `low_ocr_confidence` | 关联 OCR 最低置信度 < 0.6 |
| `ocr_only` | 无 YOLO 框、由 OCR 提升（`type: text_block`） |

### 5.5 scrollHints 字段（Python 只出原始值，判断在 C#）

| 字段 | 类型 | 含义 |
|---|---|---|
| `totalCandidates` | int | YOLO 检测交互元素总数 |
| `candidatesNearBottom` | int | 中心 Y > `spatial.edgeThreshold`（默认 0.92）的候选数 |
| `scrollbarDetected` | bool | 是否检测到 scrollbar 控件 |

### 5.6 metadata 版本（R-6）

```json
{
  "schema": "uniclaw.localVisionEvidence.v1",
  "width": 1080, "height": 1920,
  "pipeline": { "name": "local-vision", "version": "1.0.0" },
  "models": { "yolo": "yolo11n.pt", "ocr": "ppocr" },
  "configHash": "sha256-of-label-mapping.json"
}
```

分层：**schema = 协议（冻结）；pipeline/models/configHash = 追踪（可演进）**（R-10）。

---

## 6. C# 映射管道 → PageAnalysisDto 输出协议

### 6.1 4 步映射

```
candidates[] + scrollHints → PageAnalysisDto JSON
Step 1  YOLO label → AI type   查 label-mapping.json，未知 → "text" + warning
Step 2  Y 轴聚类 → menus       center.y < level1MaxY(0.08) → level1_menus
                               横向(X 方差>Y 方差) → level1_dir: "left"/"right"(按 X 均值)
                               纵向 → "top"/"bottom"      (R-2 契约修正)
                               items 补 type/parent(box 包含关系推断, 无则 null)
                               menus active 默认 false    (YOLO 无法推断选中态)
                               level2_dir/level2_menus → null/空数组
Step 3  scroll 门禁(保守化)    has_scroll = 数量溢出 OR scrollbarDetected
                               is_end_of_list 单帧不轻易为 true
                               空识别/不确定 → has_scroll:true, is_end_of_list:false
                               "到底"由引擎 seen-set 差分确认(InterceptionHandler 已有)
Step 4  popup 检测             type=="popup" → is_popup=true，取最近 close 候选
```

> R-4 关键：单帧门禁只回答"是否值得尝试一次 swipe"；最终"到底"结论来自 `TryHandleScrollAsync` 的滚动后差分（`GetElementIds` 用 `item.Name`，OCR 文本天然可作指纹）。

### 6.2 ModelResponse 输出

| 字段 | 值 |
|---|---|
| `Content` | PageAnalysisDto JSON（见 6.3） |
| `ProviderId` | `"local-vision"` |
| `Mode` | `"vision"` |
| `InputTokens` / `OutputTokens` | `0` |
| `LatencyMs` | Stopwatch 实测（含 HTTP 往返） |
| `Success` | HTTP 2xx → true；否则 **false（graceful，不抛）** |

### 6.3 PageAnalysisDto 序列化契约（对照真实代码）

> 事实来源：`PageAnalyzer.PageAnalysisDto`（[PageAnalyzer.cs:448](src/UniClaw.Core/UniBrain/PageAnalyzer.cs#L448)）+ `Schemas.cs` + 黄金样本 `HostCommands.SettingsAnalysisJson`。

| 字段 | 类型 | JSON 键 | 说明 |
|---|---|---|---|
| items | array | `items` | `name`/`type`/`coordinate`/`parent`（parent 可空） |
| level1_menus | array | `level1_menus` | `name`/`coordinate`/`active` |
| level1_dir | string | `level1_dir` | **枚举 `left/right/top/bottom`** |
| level2_menus | array | `level2_menus` | 本版本输出空数组 |
| level2_dir | string | `level2_dir` | 本版本输出 null |
| current_path | **array** | `current_path` | `string[]`（如 `["Settings"]`） |
| is_popup | bool | `is_popup` | |
| popup_info | object \| null | `popup_info` | `{title, content, close_button}` |
| close_button | object \| null | `close_button` | |
| back_button | object \| null | `back_button` | |
| has_scroll | bool | `has_scroll` | |
| is_end_of_list | bool | `is_end_of_list` | |

> 多词键必须 `[JsonPropertyName]` 锚定（`DomainJsonOptions.CamelCase` 只对单词属性生效）。
> R-2 契约修正以**黄金样本契约测试**兜底（V23），不靠文档承诺。

---

## 7. label-mapping.json 配置协议（单点真源，C# + Python 共享）

```json
{
  "schema": "uniclaw.labelMapping.v1",
  "mappings": {
    "button": "menu_item", "list_item": "menu_item", "tab": "menu_item",
    "icon": "menu_item", "toolbar": "menu_item", "back": "menu_item",
    "switch": "toggle", "checkbox": "toggle",
    "input": "input", "slider": "slider", "text_block": "text"
  },
  "nonItemLabels": ["popup"],
  "spatial": { "level1MaxY": 0.08, "edgeThreshold": 0.92 }
}
```

| 字段 | 类型 | 说明 |
|---|---|---|
| `schema` | string | `uniclaw.labelMapping.v1` |
| `mappings` | object | YOLO label → AI type；值必须通过 `ElementTypeMapper.IsValidType()` 校验（构造期 fail-fast） |
| `nonItemLabels` | [string] | `popup` 只设置 `is_popup` 标志，不进 items 数组 |
| `spatial.level1MaxY` | float | 顶部 tab 栏 Y 阈值（Step 2 聚类用） |
| `spatial.edgeThreshold` | float | 边缘贴附阈值（Python 算 candidatesNearBottom 用） |
| 未知 label | — | 默认 `"text"` + warning 日志 |

路径解析顺序：构造器参数 → `UNICLAW_LABEL_MAPPING` 环境变量 → `tools/local_vision/label-mapping.json`。

---

## 8. Python 内部管线（内存直传，零磁盘）

```
PIL Image (内存)
  ├─ run_yolo_on_image()      → Detection[]     模型模块级缓存；imgsz=640, conf=0.35
  ├─ run_ocr_on_crops()       → token 列表      按 YOLO 框裁剪；ThreadPool 默认 2 workers
  │                                            每线程独立 PaddleOCR 实例 (threading.local)
  └─ fuse_evidence_from_crops() → candidates   zip 直关联，无需空间匹配
                                              (promote_unmatched_ocr 恒 False)
```

- `OMP_NUM_THREADS` env 可配（默认 4，`UNICLAW_OMP_THREADS`），必须在任何库 import 之前设置（R-8）
- `gc.collect()` per request（PaddleOCR 已知内存泄漏；P95 抖动进 P4 压测验证）
- lifespan 预热：YOLO + OCR（首次 load 5-10s 不阻塞首请求）
- uvicorn 单 worker 为默认值（模型 ~600MB/进程），文档标注可调非硬约束（R-8）

---

## 9. 关键决策（含审阅修订）

| 决策 | 要点 |
|---|---|
| D-1 | 映射逻辑在 C#，Python 只返回 evidence（可 xUnit 覆盖） |
| D-10 | 滚动判断在 C#；Python 只返回 scrollHints 原始值 |
| D-11 | ROI 裁剪 + 每线程独立 OCR 实例（C++ 推理 GIL 释放，真并行） |
| D-13 | timing 走 Server-Timing header，不进 JSON body |
| D-18 | OMP 线程数在模块顶部、import 之前设置（env 可配） |
| D-20 | VisionScreenStateProvider 放 Traversal/（Guard 约束） |
| D-9 | 不实现 IObservableScreenStateProvider → 走 seen-set 差分路径 |
| R-1 | Provider 独立项目（对齐 ClaudeProvider/DeepSeekProvider 模式） |
| R-2 | 契约修正（level1_dir 枚举 / current_path 数组 / items 含 type+parent / level2 字段） |
| R-4 | 滚动门禁保守化，到底由引擎 seen-set 差分确认 |
| R-5 | 两类 OCR 加 scope —— **延后 v1.1**；v1 仅 roi，`promote_unmatched_ocr` 恒 False |
| R-6 | metadata 版本扩展（pipeline/models/configHash） |
| R-9 | 生命周期加固（health 探测 / unlink 残留 / env 覆盖 / warm 就绪门） |
| R-11 | trace header 协议预留（v1 C# 不发送，Python 透传/echo）；删除虚构实例字段 |
| R-12 | 滚动确认：连续 N 次无新增才到底 + 空帧重试不耗预算；N 配置化（`ScrollSwipeConfig.MaxEmptyScrollRetries`，默认 1） |
| R-13 | OCR 预热：模块级长生命周期 ThreadPoolExecutor + 预热注入 dummy 任务（每 worker 建实例，请求期复用） |
| R-14 | ROI padding 配置化：`spatial.roiPadding`（x/y 比例 + 上下限），替换硬编码 4px |
| R-15 | items `parent` v1 恒 null（契约可选，引擎无消费者） |
| R-16 | 无 level1_menus → `level1_dir: null`（回落 Direction.Left）；删 top/bottom 发明规则 |
| R-17 | YOLO confidence 配置化（`detection.confidence`，替换硬编码 0.35），不新增过滤层 |

## 10. 错误处理

| 故障 | 处理 |
|---|---|
| 进程启动失败 | StartAsync → InvalidOperationException |
| 健康检查超时 (30s) 或 warm=false | StartAsync → TimeoutException |
| 进程异常退出 | 先 health 探测 → 存活复用；否则自动拉起（退避 0/0.5/1/3/10s，上限 5 次） |
| HTTP 非 2xx | Provider 返回 `Success=false`（graceful）→ 调用方重试（MaxAnalyzeAttempts=2） |
| 配置非法 | 构造期 fail-fast |
| OCR 线程崩溃 | 单线程隔离，该 crop 返回空 token 列表 |

## 11. 不变量（合入即违约）

1. `PageAnalyzer` **零改动**；`InterceptionHandler` 仅允许最小改动（滚动空帧重试 + N 次确认，N 配置化）
2. Core 无 `Process`、无传输层 provider、无 PythonVisionService using
3. **空结果 ≠ 已到底**；不确定偏"尝试滚动"（R-4）
4. Python 不做跨帧判断（滚动时序由引擎 seen-set 差分承担）
5. C# 不修改原始 evidence（原样进 trace，配合 trace-span-helpers）
6. 坐标统一基于原图（boundsPx 原图 + bounds 归一化，裁剪/offset 链内闭环）
7. OCR-only token 永不提升为 candidate（`promote_unmatched_ocr` 恒 False）
8. 输出兼容现有 `PageAnalyzer` —— 以黄金样本契约测试（V23）兜底
