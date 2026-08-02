# Two-Stage Vision Pipeline — 设计文档

## 背景

单阶段 `PageAnalyzer`（VLM 直接产出 PageAnalysis）在 Sensenova 6.7-flash-lite 上有 **61% 截断率**。模型被要求同时完成视觉感知 + 类型分类 + 菜单层级 + 业务命名，prompt 太复杂，输出 token 经常打到上限导致 JSON 截断。

二阶段方案——原始元素提取（VLM）→ 语义推理（纯文本）——在 6 个不同 Android Settings 页面的 Python 基准测试中实现了 **0% 截断**，延迟几乎不变（~25s）。

## 架构对比

```
【单阶段 — 当前】                【二阶段 — 新】
                               
 截图                             截图
  │                                │
  ▼                                ▼
 PageAnalyzer                  TwoStagePageAnalyzer
  │                           ┌───┴──────────────────┐
  │                          S1: VLM 视觉提取         │
  │                           prompt: "只提取元素"     │
  │                           output: UIObservation   │
  │                                │                  │
  │                          Preprocess: 排序/聚类     │
  │                                │                  │
  │                          S2: 文本模型 语义推理     │
  │                           input: 元素JSON(无图片)  │
  │                           output: PageAnalysis    │
  │                           └──────────────────────┘
  ▼                                ▼
 PageAnalysis                   PageAnalysis
 (61%截断)                       (0%截断)
```

## 核心设计

```
TwoStagePageAnalyzer : IPageAnalyzer
  │
  ├─ Stage 1: IModelProvider.CompleteVisionAsync()
  │     prompt: "Extract INTERACTIVE UI elements only"
  │     output: UIObservation { elements: [{text, type, center:[x,y]}] }
  │     temperature=0, top_p=0.1, max_tokens=12000
  │
  ├─ Preprocess: sort top→bottom, left→right, detect list structure
  │
  └─ Stage 2: IModelProvider.CompleteTextAsync()  ← 无图片
        prompt: "Given these elements, infer page structure..."
        output: PageAnalysis (same as current)
        temperature=0.15, top_p=0.3, max_tokens=6000
```

### 关键思想

> **第一阶段不要追求"理解"，只做视觉感知；第二阶段不要看图片，只做结构推理和业务归一化。**

每个阶段做自己擅长的事。第一阶段输出叫 `UIObservation`（模型看到的事实），第二阶段输出才叫 `PageAnalysis`（对事实的解释）。

## Benchmark 数据

Python 原型在 6 个 Android Settings 子页面 × 3 次重复 = 18 次调用：

| 指标 | 单阶段 | 二阶段 |
|------|--------|--------|
| **截断率** | **61%** | **0%** |
| 可用 run 数 | 7/18 | **18/18** |
| 平均延迟 | 25,426ms | 25,709ms (+1.1%) |
| 最差延迟 | 48,910ms | 40,590ms (−17%) |
| 平均 Token | 4,643 | 5,555 (+20%) |

| 页面 | 单阶段 | 二阶段 |
|------|--------|--------|
| Settings | 22.1s (1/3) | 20.3s (3/3) |
| Network & internet | 17.3s (1/3) | 32.0s (3/3) |
| Connected devices | 32.7s (2/3) | 24.8s (3/3) |
| Apps | 9.3s (1/3) | 28.4s (3/3) |
| Notifications | ❌ 全截断 | 25.4s (3/3) |
| Battery | 31.9s (2/3) | 23.4s (3/3) |

## 文件清单

### 新增文件

| 文件 | 说明 |
|------|------|
| `src/UniClaw.Core/UniBrain/UIObservation.cs` | Stage 1 输出 DTO |
| `src/UniClaw.Core/UniBrain/PageAnalysisJson.cs` | 共享的 PageAnalysisDto → PageAnalysis 映射（从 PageAnalyzer 提取，避免重复） |
| `src/UniClaw.Core/UniBrain/TwoStagePageAnalyzer.cs` | 二阶段实现 |
| `tests/UniClaw.Core.Tests/UniBrain/TwoStagePageAnalyzerTests.cs` | 单元测试 |

### 修改文件

| 文件 | 改动 |
|------|------|
| `src/UniClaw.Core/UniBrain/ModelCapabilities.cs` | +2 capability 常量 |
| `src/UniClaw.Core/UniBrain/PromptTemplateRegistry.cs` | +2 prompt 模板 |
| `src/UniClaw.Core/UniBrain/Schemas.cs` | +1 JSON schema (Stage 1) |
| `src/UniClaw.Core/UniBrain/UniBrainConfig.cs` | +EnableTwoStage flag |
| `src/UniClaw.Core/UniBrain/UniBrainFactory.cs` | 二阶段分支 |
| `src/UniClaw.Host/Commands/HostCommands.cs` | 注册新模板 |

## DTO 设计

### UIObservation（Stage 1 输出）

```csharp
public sealed record class UIObservation
{
    public ImmutableArray<UIElement> Elements { get; init; }
}

public sealed record class UIElement
{
    public string Text { get; init; } = "";
    public string Type { get; init; } = "";  // clickable|icon|input|switch|toggle|unknown
    public double CenterX { get; init; }
    public double CenterY { get; init; }
}
```

### 与 PageAnalysis 的区别

| | UIObservation | PageAnalysis |
|---|---|---|
| 含义 | 模型看到的**事实** | 对事实的**解释** |
| 来源 | VLM 视觉提取 | 文本模型推理 |
| 字段 | text, type, center | name, type, coordinate, parent, expectedAction... |
| 有无层级 | 无（扁平列表） | 有（level1_menus, current_path） |
| 坐标 | 原始提取，不改 | 语义归一化后可能微调 |

## 新增 Capability

```csharp
public const string ExtractUIElements = "extract_ui_elements";     // Stage 1
public const string AnalyzeUIStructure = "analyze_ui_structure";   // Stage 2
```

## Stage 1 参数

```
temperature:  0
top_p:        0.1
max_tokens:   12000
```

## Stage 2 参数

```
temperature:  0.15
top_p:        0.3
max_tokens:   6000
```

## 切换方式

```bash
UNICLAW_TWO_STAGE=1  # 启用二阶段
# 默认 = 单阶段（向后兼容）
```

或通过 `UniBrainConfig.EnableTwoStage = true`。

## 关键设计决策

1. **IPageAnalyzer 接口不变**——`TwoStagePageAnalyzer` 产出相同的 `PageAnalysis`，调用方零改动
2. **Stage 2 复用现有 JSON Schema**——输出格式与单阶段一致，直接使用 `Schemas.AnalyzeVisual`
3. **坐标原样保留**——Stage 1 提取的 [cx,cy] 直接传给 Stage 2，不做映射
4. **两个 Stage 目前用同一个模型**——`extract_ui_elements` 和 `analyze_ui_structure` 都路由到 sensenova。未来 S2 可以换成更便宜的纯文本模型
5. **默认单阶段**——向后兼容，`UNICLAW_TWO_STAGE=1` 开启二阶段

## 验证

1. `dotnet build src/UniClaw.Core` — 0 errors
2. `dotnet test tests/UniClaw.Core.Tests --filter "TwoStagePageAnalyzer"` — 全部通过
3. `dotnet test tests/UniClaw.Core.Tests tests/UniClaw.Host.Tests` — 无回归
4. `UNICLAW_TWO_STAGE=1 UNICLAW_INTEGRATION_SCOPES=vision-smoke dotnet test` — 集成测试通过
5. `python3 scripts/vision_two_stage_benchmark.py` — 0% 截断

## 实现顺序

1. `ModelCapabilities` + `Schemas` + `PromptTemplateRegistry`（纯新增）
2. `UIObservation.cs`
3. `PageAnalysisJson.cs` + `PageAnalyzer` 重构 → 现有 `PageAnalyzerTests` 保持绿色
4. `TwoStagePageAnalyzer.cs` + 单元测试
5. `UniBrainConfig` + `UniBrainFactory` + 工厂测试
6. Host 接线

## 工厂路由覆盖（可选）

默认两个 Stage 用同一个 provider。可通过 `CapabilityRouting` 覆盖：

```csharp
new UniBrainConfig(
    CapabilityRouting: new Dictionary<string, string>
    {
        ["page_analysis_stage1"] = "sensenova",   // 专用 VLM
        ["page_analysis_stage2"] = "deepseek",    // 便宜文本模型
    }.ToImmutableDictionary())
```

## 错误处理

每阶段独立重试 2 次。Stage 1 失败重新截屏（UI 可能已变化），Stage 2 失败重新发送同一份 elements JSON（不重截屏）。

Stage 1 可重试的错误：
- 模型调用失败
- 响应不是有效 JSON
- 坐标超出 [0,1]

Stage 2 可重试的错误：
- 模型调用失败
- 响应不是有效 JSON
- 坐标超出 [0,1]

不可重试（fail-fast）：模板缺失、items 字段缺失、元素 type 非法。

## 注意

- **`page_context` 在生产环境默认 `{}`**——Stage 2 完全从 elements 推断 page 类型和 current path
- **Stage 2 不传图片**——使用 `CompleteTextAsync()`，不是 `CompleteVisionAsync()`
- **坐标用数组格式** `[x,y]`——不是对象格式 `{x,y}`，这是 Python benchmark 验证过的稳定格式
- **默认不裁剪**——`cropTopRatio=0, cropBottomRatio=0`（benchmark 验证配置）
