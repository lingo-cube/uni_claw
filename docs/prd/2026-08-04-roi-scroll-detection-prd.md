# ROI 滚动检测 + UIA 依赖移除 PRD

> 日期: 2026-08-04
> 状态: proposed
> 范围: `src/UniClaw.Core/Traversal/` (C#) + `src/UniClaw.Core/Observation/` (C#) + `src/UniClaw.Device/` (C#) + `src/UniClaw.Host/` (C#) + `tools/local_vision/` (Python)

## 1. Motivation

当前滚动到底检测依赖 UIAutomator XML dump，存在三个问题：

1. **双通道冗余**：UIAutomator 是 Android 平台特有的 UI 层次 dump 机制，与视觉管线（YOLO + OCR）构成两套独立观测通道。UIA 快速路径（D5 指纹快路径）和视觉 seen-set diff 路径同时在跑，增加维护负担。

2. **UIAutomator 不可靠**：部分设备 / WebView 页面 / 车机系统不提供 UIAutomator dump；`AdbScreenStateProvider` 首次失败即永久禁用（D6），后续全部退化到纯视觉路径。

3. **平台绑定**：UIAutomator 是 Android-only 能力。项目长期方向是跨平台（iOS、车机、桌面），UIA 依赖是迁移障碍。

本 PRD 提出：
- **完全移除 UIAutomator 依赖**，包括 `AdbScreenStateProvider`、`UiAutomatorPageAnalysis`、`ObservationPipeline` 的 UIA 分支、`StepCaptureStore` 等。
- **用同一 Container 内 ROI 聚合比对替代 UIA 滚动检测**，使滚动到底判断完全基于视觉截图，无需平台特定 API。

## 2. Architecture

### 2.1 改动范围总览

```
删除:
  src/UniClaw.Device/AdbScreenStateProvider.cs       整个文件
  src/UniClaw.Core/Observation/UiAutomatorPageAnalysis.cs  整个文件
  src/UniClaw.Core/Traversal/IUiAutomatorAvailability.cs   整个接口
  src/UniClaw.Core/Traversal/IScreenStateCache.cs         整个接口
  src/UniClaw.Host/Runner/StepCaptureStore.cs              整个文件
  ObservationPipeline.cs     UIA 分支 (~50 行)
  ObservationConfig.cs       UIA_Enabled / UIA_MinItems / SkipUIAOnBackNavigation
  InterceptionHandler.cs     D5 指纹快路径 (~30 行)
  ScreenStateResult.cs       HierarchyXml / HierarchyFingerprint
  RunAssetHook.cs            before.xml / after.xml 写入
  HostCommands.cs            AdbScreenStateProvider DI + StepCaptureStore 连线
  IAdbSession.cs             DumpUiHierarchyAsync()

新增:
  src/UniClaw.Core/Traversal/RoiSelector.cs             ROI 候选评分与选择
  src/UniClaw.Core/Traversal/RoiSnapshotGenerator.cs     快照标准化流水线
  src/UniClaw.Core/Traversal/SnapshotComparer.cs          多指标复合比较
  src/UniClaw.Core/Traversal/StableFrameCapturer.cs       稳定帧采集循环
  src/UniClaw.Core/Traversal/RoiRect.cs                   ROI 矩形 record
  src/UniClaw.Core/Traversal/RoiSnapshot.cs               标准化快照 record
  src/UniClaw.Core/Traversal/SnapshotComparison.cs        比较结果 record

修改:
  ScrollSwipeConfig.cs       扩展 11 个新阈值
  InterceptionHandler.cs     TryHandleScrollAsync 重写为 ROI 比对流程
  VisionScreenStateProvider.cs  删除 _uia 冗余侧信道
  ObservationConfig.cs       移除 UIA 字段
  IObservableScreenStateProvider.cs  RefreshAsync 签名简化（去掉 previousHierarchyXml / afterScroll）
  ScreenStateResult.cs       移除 HierarchyXml / HierarchyFingerprint
  RunAssetHook.cs            删除 xml 写入
  HostCommands.cs            去掉 AdbScreenStateProvider / StepCaptureStore / ObservationPipeline UIA 参数
  IAdbSession.cs + 实现      删除 DumpUiHierarchyAsync()
```

**Python 侧零改动** — 现有 `bounds` 字段已包含 YOLO 检测框坐标，C# 直接使用。

### 2.2 职责边界

```
Python: 首次页面分析 → 返回元素列表
  candidates[].bounds   = YOLO 检测框（x1,y1,x2,y2 归一化）
  candidates[].evidence.yoloId = null → OCR 提升候选（无 YOLO 框）

C# RoiSelector: PageAnalysis + 原始截图 → RoiRect?
  ├─ Items 中含 YOLO 框 → 密度分 + 纹理分 − 惩罚
  └─ 全部 OCR 提升（yoloId==null）→ 纯纹理分（Laplacian 方差 + 非纯色占比）

C# StableFrameCapturer: IScreenCapture + RoiRect + RoiSnapshotGenerator
  → 循环采集稳定帧

C# SnapshotComparer: (RoiSnapshot, RoiSnapshot) → SnapshotComparison
  ← 纯函数，汉明距离 + 平均绝对差 + 变化像素比

C# InterceptionHandler.TryHandleScrollAsync:
  组装上述模块 → 输出 Scrolled / EndReached / Unknown
```

所有新增类均为 `internal`，仅在 `InterceptionHandler` 内部组装调用。无公共接口变更（`IPageAnalyzer`、`IScreenStateProvider`、`IScreenCapture` 均不动）。

### 2.3 对比：旧路径 vs 新路径

```
── 旧路径（UIA + seen-set diff 双通道）──
UIA XML dump (1-2s) → parse scrollable(1)s
  ├─ D5 快路径: swipe前后UIA指纹比对 → 跳过AI
  └─ 不可用 → seen-set diff (AI 视觉)

── 新路径（ROI 聚合比对，纯视觉）──
首次进入Container → RoiSelector选定ROI → 缓存坐标 + 初始基准S0
每次滚动:
  获取稳定S0 (连续2帧相似)
    → swipe
  → 获取稳定S1 (连续2对相似)
  → 三指标复合比较
    ├─ Different → Scrolled (更新基准S0=S1)
    └─ Same → 二次swipe(手势缩小50%) → S2 → 三组比较 → EndReached
```

## 3. 详细设计

### 3.1 Python 侧：零改动，复用现有 `bounds` + `evidence.yoloId`

**文件**: `tools/local_vision/fusion.py` — 不动。

#### 3.1.1 覆盖率验证

`fusion.py` 两条融合路径中，**每个 candidate 的 dict literal 均包含 `bounds` 键，覆盖率 100%**：

| 融合路径 | candidate 类型 | `bounds` 赋值 | 代码位置 |
|---------|---------------|-------------|---------|
| `fuse_evidence` | YOLO 融合 | `detection.box.normalized(image_width, image_height)` | `fusion.py:75` |
| `fuse_evidence` | OCR 提升（`promote_unmatched_ocr`） | `token.box.normalized(image_width, image_height)` | `fusion.py:104` |
| `fuse_evidence_from_crops` | YOLO 融合 | `detection.box.normalized(image_width, image_height)` | `fusion.py:186` |

#### 3.1.2 语义区分

`bounds` 的来源决定了其语义，C# 侧通过 `evidence.yoloId` 区分：

| 候选类型 | `bounds` 来源 | `evidence.yoloId` | 框语义 | C# 密度评分 |
|---------|-------------|-------------------|--------|------------|
| YOLO 融合 | `Detection.box`（YOLO 检测框） | `"det_N"`（非 null） | UI 元素整体边界 | 参与计算 ✅ |
| OCR 提升 | `OcrToken.box`（OCR 文本行框） | `null` | 文本行边界（通常比元素整体小） | 跳过（密度分=0）❌ |

OCR 提升候选的 `bounds` 是文本行框而非元素整体框——面积偏小，纳入密度计算会抬高非内容区域的得分。因此 C# ROI 评分时**仅统计 `yoloId != null` 的 Items**。

#### 3.1.3 示例

```json
// YOLO 融合候选 — yoloId 非 null，bounds 是 YOLO 检测框
{
  "type": "menuItem",
  "bounds": {"x1": 0.05, "y1": 0.10, "x2": 0.95, "y2": 0.14},
  "boundsPx": [54, 192, 1026, 269],
  "evidence": {"yoloId": "det_3", "ocrIds": ["ocr_12"], "allIds": ["det_3", "ocr_12"]}
}

// OCR 提升候选 — yoloId 为 null，bounds 是 OCR 文本行框
{
  "type": "text_block",
  "bounds": {"x1": 0.35, "y1": 0.60, "x2": 0.65, "y2": 0.64},
  "boundsPx": [378, 1152, 702, 1229],
  "evidence": {"yoloId": null, "ocrIds": ["ocr_25"], "allIds": ["ocr_25"]},
  "riskFlags": ["ocr_only"]
}
```

### 3.2 坐标系约定

- 全屏原始截图作为唯一基准坐标系，C# 不进行任何预裁剪（crop）。
- Python 返回的所有坐标均为 0-1 归一化值，对应原始全屏截图的宽高。
- C# 使用截图的 `Width`、`Height` 直接反归一化 → 像素坐标。
- ADB swipe 坐标同样基于物理分辨率，与截图坐标系保持一致。
- 若屏幕发生旋转或分辨率变化，ROI 立即失效并重新初始化。

**截图路径约束**（新增）：`RoiSnapshotGenerator` 和 `StableFrameCapturer` 使用的截图必须走**原始全屏路径**（`UNICLAW_RAW_SCREEN_BUFFER=1` 或等效未经 `ImageResizer` 处理的 `IScreenCapture.CaptureAsync`）。禁止使用经过 crop/resize/JPEG 编码的截图——金字塔缩放 + 高斯模糊会放大压缩伪影，导致像素比对指标失真。实现时在 `InterceptionHandler` 内捕获的截图来源必须与 `RoiSelector` 一致——如果 `IScreenCapture` 产出的是旧路径（ImageResizer 处理后），ROI 坐标反归一化将产生系统偏差。

### 3.3 C# ItemDto — 不动

**文件**: `src/UniClaw.LocalVisionProvider/LocalVisionProvider.cs` — 现有 `bounds` 已反序列化为 `BoundsDto`（`{X1, Y1, X2, Y2}`），`EvidenceDto` 已含 `YoloId`。无新增字段。

### 3.4 RoiRect

**文件**: `src/UniClaw.Core/Traversal/RoiRect.cs`（新文件）

```csharp
namespace UniClaw.Core.Traversal;

/// <summary>
/// 像素坐标 ROI 矩形，由 RoiSelector 选定，Container 生命周期内缓存。
/// </summary>
public readonly record struct RoiRect(
    int X1, int Y1, int X2, int Y2
);
```

### 3.5 ROI 选择

**文件**: `src/UniClaw.Core/Traversal/RoiSelector.cs`（新文件）

```csharp
namespace UniClaw.Core.Traversal;

internal static class RoiSelector
{
    /// <summary>
    /// 在候选范围内滑动窗口，按综合得分选择最佳 ROI 区域。
    /// </summary>
    /// <param name="analysis">页面分析结果</param>
    /// <param name="screenshot">全屏原始截图字节</param>
    /// <param name="screenWidth">截图宽</param>
    /// <param name="screenHeight">截图高</param>
    /// <returns>ROI 像素坐标；无法选择时返回 null。</returns>
    public static RoiRect? Select(
        PageAnalysis analysis,
        byte[] screenshot,
        int screenWidth,
        int screenHeight)
    { ... }
}
```

#### 3.5.1 候选范围

- 纵向：屏幕高度的 30%–85%，优先 40%–65% 中上区域。
- 横向：避开左右边缘各 5%。
- ROI 宽：屏幕宽度的 70%–90%。
- ROI 高：屏幕高度的 20%–30%。

#### 3.5.2 评分方法

滑动窗口在候选范围内移动（步长 = 窗口高度的 50%），每个位置计算综合得分：

**正向因子：**

| 因子 | 权重 | 实现 |
|------|------|------|
| YOLO 检测框覆盖密度 | 0.6 | Σ(`boundsPx` 与窗口交集面积) / 窗口面积；仅统计 `yoloId != null` 的 Items |
| 纹理复杂度 | 0.3 | 窗口灰度图的 Laplacian 方差，归一化到 [0, 1] |
| 非纯色占比 | 0.1 | 灰度标准差 > 噪声阈值（15）的像素占比 |

**负向因子：**

| 因子 | 判定方式 |
|------|---------|
| 动态/动画元素 | 排除 `type` 在黑名单中的候选（见下方映射表）；黑名单元素占窗口面积比超 20% → 惩罚 |
| 固定区域 | 窗口与状态栏（y < 5% 屏幕高）、底栏（y > 95%）重叠 → 位置权重减半；与悬浮按钮（`type` = `floating_button` / `fab`）重叠 → 排除 |

```
Score = DensityScore × 0.6 + TextureScore × 0.3 + NonSolidScore × 0.1 − DynamicPenalty
```

选择得分最高的窗口位置作为最终 ROI。若所有窗口得分过低（全空白页），返回 null → 上报 Unknown 并中断自动滚动。

动态元素和固定区域的识别完全基于 `PageAnalysis.Items` 的 `type` 字段 + 坐标位置，不需要 Python 提供额外标注。

**动态元素 type 映射表**（C# 侧静态维护）：

| 黑名单 type | 来源 | 说明 |
|------------|------|------|
| `loading` | YOLO 标签 | 加载指示器、spinner |
| `banner` | YOLO 标签 | 广告轮播、顶部横幅 |
| `carousel` | YOLO 标签 | 轮播图容器 |
| `progressbar` | YOLO 标签 | 进度条、滑块动画 |
| `video` | YOLO 标签 | 视频播放区域 |

YOLO 模型输出标签与实际名称可能不一致（如模型输出 `advertisement` 而非 `banner`）。黑名单需要在首次实机运行时用实际模型标签校准——`PageAnalysis.Items` 中出现的所有 `type` 值应被记录，人工标注哪些属于动态元素后固化。**初始实现可用上述 5 个标签作为假设，标注后修正。**

**注意**：`text_block` 可能是动态的（滚动新闻 ticker），但无法从单帧判断。不纳入黑名单——若导致 ROI 误选，由纹理分 + 非纯色分平衡。

#### 3.5.3 退化策略

全部 Items 的 `yoloId == null`（纯 OCR 场景）：密度分权重 = 0，纹理分权重 = 0.7，非纯色占比权重 = 0.3。权重调整而非简单放大纹理分——渐变背景（如车机设置页灰色渐变）虽然 Laplacian 方差高，但非纯色占比低，组合评分后会自动偏好有图案/文字的实心区域。

### 3.6 快照标准化

**文件**: `src/UniClaw.Core/Traversal/RoiSnapshotGenerator.cs`（新文件）

```csharp
namespace UniClaw.Core.Traversal;

internal static class RoiSnapshotGenerator
{
    public static RoiSnapshot Generate(
        byte[] screenshot,
        RoiRect roi,
        int snapshotWidth = 0,    // 0 = auto
        int snapshotHeight = 0,
        long frameSeq = 0)
    { ... }
}
```

流水线（两步独立缩放）：

```text
全屏截图
  → 裁剪ROI
  → 灰度化
  → 缩放至固定尺寸（256×128 / 128×256）→ 轻度高斯模糊 → GrayPixels（像素比对用，灰度值 0-255）
  → 内部再缩放至 9×8 → 逐行比较 → 64-bit dHash（汉明距离用）
```

两步缩放独立：快照本体 `256×128`（或 `128×256`）为像素比对保留足够空间分辨率；dHash 内部使用标准 `9×8` 网格，逻辑封装在 `RoiSnapshotGenerator` 内不对外暴露。

缩放尺寸默认：
- 宽屏（宽 > 高）：256×128
- 竖屏（高 > 宽）：128×256
- 通过 `ScrollSwipeConfig.RoiSnapshotWidth` / `RoiSnapshotHeight` 自定义；0 = auto。

### 3.7 RoiSnapshot

**文件**: `src/UniClaw.Core/Traversal/RoiSnapshot.cs`（新文件）

```csharp
namespace UniClaw.Core.Traversal;

public sealed record RoiSnapshot(
    ulong PerceptualHash,   // dHash 64-bit，汉明距离 O(1)
    byte[] GrayPixels,      // 标准化灰度矩阵，值域 0-255
    int Width,
    int Height,
    long FrameSeq           // 自增帧序号
);
```

不比较原始 JPEG/PNG 二进制数据。

### 3.8 稳定快照采集

**文件**: `src/UniClaw.Core/Traversal/StableFrameCapturer.cs`（新文件）

```csharp
namespace UniClaw.Core.Traversal;

internal sealed class StableFrameCapturer
{
    private readonly IScreenCapture _capture;
    private readonly ScrollSwipeConfig _config;
    private readonly RoiSnapshotGenerator _generator;

    /// <summary>
    /// 统一稳定帧采集。滚动前（requiredPairs=1）→ 连续2帧相似；
    /// 滚动后（requiredPairs=2）→ 连续2对相似（共3帧）。
    /// </summary>
    internal async Task<RoiSnapshot?> CaptureStableAsync(
        RoiRect roi,
        int requiredConsecutivePairs,  // S0: 1, S1/S2: 2
        CancellationToken ct)
    { ... }
}
```

内部通过 `requiredConsecutivePairs` 控制采样强度。对外暴露两个语义明确的方法（内部委托到统一实现）：

| 对外方法 | requiredPairs | 语义 |
|---------|--------------|------|
| `CaptureBeforeScrollAsync` | 1 | S0：连续2帧相似 → 返回后一帧 |
| `CaptureAfterScrollAsync` | 2 | S1/S2：连续2对相似 → 返回第三帧 |

#### 3.8.1 采集逻辑

**S0（滚动前，requiredPairs=1）**：

```text
A1 → 等待 targetInterval → A2
若 Same(A1, A2) → S0 = A2
否则继续采样，直至连续两帧相似或达到 StableSampleMaxRetries 次（返回 null → Unknown）
```

**S1（滚动后，requiredPairs=2）**：

```text
B1 → B2 → B3 ……
当同时满足 Same(Bn, Bn+1) AND Same(Bn+1, Bn+2) 时，S1 = Bn+2
→ S1 本身已保证是稳定帧，可直接用作下次滚动的基准
```

要求连续两对相似，避免惯性滚动/回弹的中间帧被误判为稳定。

#### 3.8.2 动态延迟

`StableFrameCapturer` 必须感知 ADB 截图耗时，采用"目标间隔 − 已耗时"作为剩余等待时间：

```csharp
while (retries < maxRetries)
{
    var t0 = DateTimeOffset.UtcNow;
    var frame = await _capture.CaptureAsync(ct);
    var elapsed = (DateTimeOffset.UtcNow - t0).TotalMilliseconds;
    var wait = Math.Max(0, _config.StableSampleIntervalMs - (int)elapsed);
    await Task.Delay(wait, ct);
    // 取下一帧 ...
}
```

这保证在不同设备上以最快速度获取稳定帧，避免因截图慢导致间隔过短。

#### 3.8.3 绝对超时

除 `StableSampleMaxRetries` 外，`StableSampleMaxTimeMs`（默认 3000ms）作为绝对时间上限。当懒加载图片、网络请求等导致页面持续渲染时，重试循环可能在超时前无法积累到足够的相似对。超过 `StableSampleMaxTimeMs` 直接返回 null → Unknown。

```csharp
var startedAt = DateTimeOffset.UtcNow;
while (retries < maxRetries)
{
    if ((DateTimeOffset.UtcNow - startedAt).TotalMilliseconds > _config.StableSampleMaxTimeMs)
        return null;  // 绝对超时
    // ... 采样逻辑 ...
}
```

### 3.9 快照相似度判定

**文件**: `src/UniClaw.Core/Traversal/SnapshotComparer.cs`（新文件）

```csharp
namespace UniClaw.Core.Traversal;

internal static class SnapshotComparer
{
    public static SnapshotComparison Compare(
        RoiSnapshot a, RoiSnapshot b, ScrollSwipeConfig config)
    { ... }
}
```

#### 3.9.1 复合判定规则

```text
HashDistance ≤ HashDistanceThreshold
AND MeanAbsoluteDifference ≤ MadThreshold
AND ChangedPixelRatio ≤ ChangedPixelRatio
→ IsSame = true
```

三个指标必须同时满足，任一超标即 `IsSame = false`。

#### 3.9.2 指标说明

| 指标 | 值域 | 算法 | 用途 |
|------|------|------|------|
| `HashDistance` | 0–64 | 两个 dHash 的汉明距离 | 快速判断整体结构相似 |
| `MeanAbsoluteDifference` | 0–255 | GrayPixels 逐像素绝对差值均值（值域 0-255，非归一化） | 确认整体像素变化 |
| `ChangedPixelRatio` | 0–1 | 绝对差值 > 噪声阈值（15）的像素比例 | 避免局部小动画误判 |

`MadThreshold` 的单位是灰度值（0-255），默认 12.75（≈5% 最大差异）。`ChangedPixelRatio` 是比例（0-1），默认 0.1（10% 像素变化）。

### 3.10 SnapshotComparison

**文件**: `src/UniClaw.Core/Traversal/SnapshotComparison.cs`（新文件）

```csharp
namespace UniClaw.Core.Traversal;

public sealed record SnapshotComparison(
    int HashDistance,
    double MeanAbsoluteDifference,  // 0-255
    double ChangedPixelRatio,       // 0-1
    bool IsSame
);
```

### 3.11 滚动结果判定

#### 3.11.1 单次滚动流程

```
S0 = CaptureBeforeScrollAsync(roi, ct)    ← 若为 null → Unknown
swipe(config.StartX, StartY, EndX, EndY, DurationMs)
S1 = CaptureAfterScrollAsync(roi, ct)     ← 若为 null → Unknown
diff = Compare(S0, S1, config)
```

#### 3.11.2 判定逻辑

**Different(S0, S1)**：

```text
→ Scrolled
基准更新: _currentBaseline = S1（S1 经 CaptureAfterScrollAsync 已保证稳定）
连续无变化计数器清零
childMgr.Invalidate(currentFrame.NodeId) → 下次 NodeSelect 重新生成子节点
```

**Same(S0, S1)**：

不立即断定到底。第二次滚动手势缩小为原来的 50%（距离 = `(StartY - EndY) * 0.5`），方向不变。理由：两次完全相同的 swipe 可能在列表弹性区域造成假稳定（如已到顶部继续上滑，回弹后 S0≈S1）。

```text
S1 → swipe2（距离缩至 50%）→ CaptureAfterScrollAsync → S2
```

比较三组：`(S0, S1)`, `(S1, S2)`, `(S0, S2)`。

- **全部 Same** → `EndReached`（二次缩距滚动无新内容，三个稳定状态一致 → 确认为底）
- **任意一组明显 Different** → `Scrolled`
- **证据冲突**（如 Hash 相似但 MAD 显著偏高）→ `Unknown`

#### 3.11.3 防死循环

- 单次稳定快照获取最大重试 `StableSampleMaxRetries`（默认 5），超时返回 `Unknown`。
- 连续 `Unknown` 达到 `MaxConsecutiveUnknown`（默认 3）→ 强制清除 ROI + 触发 Python 重新分析（通过 `childMgr.Invalidate` + 下一轮 `AnalyzeCurrentPageAsync` 自然触发）。
- ROI 重选后仍连续失败 → 上报异常并终止当前 Container 的自动滚动。

### 3.12 完整状态流

```text
Container 首次进入
→ RoiSelector.Select(ctx.CurrentPageAnalysis, screenshot, w, h)
→ 缓存 _roiRect + 采集初始基准快照 _currentBaseline
   （基准快照在首次滚动时作为 S0 使用；若采集失败 → 清除 ROI → Unknown）

每次滚动循环:
  S0 = StableFrameCapturer.CaptureBeforeScrollAsync(_roiRect, ct)
    → S0 == null? → Unknown → 递增 _consecutiveUnknown

  ctx.Action.SwipeAsync(_config.StartX/Y → EndX/Y, DurationMs)

  S1 = StableFrameCapturer.CaptureAfterScrollAsync(_roiRect, ct)
    → S1 == null? → Unknown → 递增 _consecutiveUnknown

  diff = SnapshotComparer.Compare(S0, S1, _config)

  !diff.IsSame
  → Scrolled → _currentBaseline = S1; _consecutiveUnchanged = 0
    _consecutiveUnknown = 0
    ctx.ChildMgr.Invalidate(frame.NodeId)
    return ScrolledResult()

  diff.IsSame
  → _consecutiveUnchanged++
  → 缩距swipe2（距离 = 原距离 × 0.5）
  → S2 = StableFrameCapturer.CaptureAfterScrollAsync(_roiRect, ct)
    → S2 == null? → Unknown

  d01=diff; d12=Compare(S1,S2); d02=Compare(S0,S2)

  d01.IsSame && d12.IsSame && d02.IsSame
  → EndReached → 释放所有快照; return EndReachedResult()

  !d12.IsSame || !d02.IsSame
  → Scrolled → _currentBaseline = S2; _consecutiveUnchanged = 0
    _consecutiveUnknown = 0
    ctx.ChildMgr.Invalidate(frame.NodeId)
    return ScrolledResult()

  证据冲突
  → Unknown → 递增 _consecutiveUnknown
    if _consecutiveUnknown >= MaxConsecutiveUnknown:
      → 清除 _roiRect + _currentBaseline → 下一轮重新 Select ROI
```

### 3.13 快照生命周期

- `S0`、`S1`、`S2` 均为临时对象，判定完成后立即释放 `GrayPixels`。
- 长期只保留：
  - ROI 像素坐标（`_roiRect: RoiRect?`）
  - 当前内容基准快照哈希（`_currentBaselineHash: ulong`）
  - 连续无变化计数器（`_consecutiveUnchanged: int`）
  - 连续 Unknown 计数器（`_consecutiveUnknown: int`）

不长期保存完整截图或像素矩阵。`_currentBaseline` 的 GrayPixels 在更新时释放旧数组。

### 3.14 ROI 失效与重选

以下情况清除 `_roiRect` + `_currentBaseline` + 计数器，下次滚动时重新走 `RoiSelector.Select()`：

- 页面 / Container 切换（`currentFrame.NodeId` 变化）→ 自然触发，Container 边界检测已在 StepOrchestrator 中
- 横竖屏或分辨率变化（截图尺寸变化）
- ROI 坐标越界（动态布局导致区域超出屏幕）
- 连续 `StableSampleMaxRetries` 次无法获取稳定帧
- 连续 `MaxConsecutiveUnknown` 次返回 `Unknown`

**弹窗**：弹窗出现通常是用户交互结果，伴随 Container 切换（PopupHandling → ResultVerify → Branch）。如果是不切换 Container 的被动弹窗（网络错误 toast），其生命周期短（< 3s），稳定帧采集超时会自然解决。不需要专门的弹窗检测。

普通滚动导致的内容变化只更新 `_currentBaseline`，不清除 `_roiRect`。

### 3.15 TryHandleScrollAsync 重写

**文件**: `src/UniClaw.Core/Traversal/InterceptionHandler.cs`

移除 `static` 修饰符（不再纯函数——需要访问实例字段 `_roiRect`、`_currentBaseline`、计数器）。新增字段：

```csharp
private RoiRect? _roiRect;
private RoiSnapshot? _currentBaselineSnapshot;   // 仅保留最近一次基准
private int _consecutiveUnchanged;
private int _consecutiveUnknown;
private StableFrameCapturer? _stableCapture;     // 首次使用时懒初始化
```

现有 `D5 快路径`（`IObservableScreenStateProvider` cast + pre/post swipe UIA dump）删除。

新流程伪代码见 3.12。`GetOrSelectRoi` 为私有方法：检查 `_roiRect` 是否存在且有效 → 若无则获取一个截图、调 `RoiSelector.Select` 并采集初始基准。首次进入 Container 时，优先复用页面分析过程中的截图（`ctx.Context.CurrentPageAnalysis` 关联的截图，后文称为 `_lastAnalysisScreenshot`），避免重复 ADB 往返。若缓存截图不可用（尺寸不匹配或被释放），则调 `IScreenCapture.CaptureAsync`。

### 3.16 ScrollSwipeConfig 扩展

**文件**: `src/UniClaw.Core/Traversal/ScrollSwipeConfig.cs`

```csharp
public sealed record class ScrollSwipeConfig(
    // —— 手势（现有，不动）——
    double StartX = 0.5,
    double StartY = 0.7,
    double EndX = 0.5,
    double EndY = 0.3,
    int DurationMs = 300,

    // —— 稳定快照采集 ——
    int StableSampleMaxRetries = 5,
    int StableSampleIntervalMs = 100,
    int StableSampleMaxTimeMs = 3000,     // 绝对超时（懒加载等慢场景），超过则 Unknown

    // —— 快照尺寸 ——
    int RoiSnapshotWidth = 0,   // 0 = auto-detect from aspect ratio
    int RoiSnapshotHeight = 0,

    // —— 相似度阈值 ——
    int HashDistanceThreshold = 10,      // dHash 汉明距离，值域 0-64
    double MadThreshold = 12.75,         // 平均绝对像素差，值域 0-255（≈5%）
    double PixelNoiseThreshold = 15.0,   // 变化像素判定噪声阈值，值域 0-255
    double ChangedPixelRatio = 0.1,      // 变化像素比例，值域 0-1

    // —— 防死循环 ——
    int MaxConsecutiveUnknown = 3,

    // —— 二次滚动缩距比 ——
    double SecondSwipeDistanceRatio = 0.5,  // 二次滚动手势距离 = 原距离 × 此值

    // —— 现有字段保留 ——
    int MaxEmptyScrollRetries = 1
);
```

所有阈值在实机测试后标定，不作为代码常量固化。

## 4. 删除清单

### 4.1 完整删除（文件级）

| 文件 | 原因 |
|------|------|
| `src/UniClaw.Device/AdbScreenStateProvider.cs` | 只做 UIAutomator XML dump |
| `src/UniClaw.Core/Observation/UiAutomatorPageAnalysis.cs` | XML → PageAnalysis 解析器 |
| `src/UniClaw.Core/Traversal/IUiAutomatorAvailability.cs` | UIA 可用性探针接口 |
| `src/UniClaw.Core/Traversal/IScreenStateCache.cs` | 只为复用 hierarchy dump |
| `src/UniClaw.Host/Runner/StepCaptureStore.cs` | hierarchy 复用热路径实现 |

### 4.2 部分删除（行级）

| 文件 | 删什么 |
|------|--------|
| `ObservationPipeline.cs` | UIA 分支（L1 gate + dump + parse + UIA decision）；**保留 back navigation reuse + AI 透传 + Remember 历史**。注意：`Remember` 用 `HierarchyFingerprint` 做去重键——该字段删除后改用 `PageSnapshotManager.Fingerprint(analysis)` 替代，行为等价 |
| `ObservationConfig.cs` | `UIA_Enabled` / `UIA_MinItems` / `SkipUIAOnBackNavigation`；保留其余字段 |
| `InterceptionHandler.cs` | D5 指纹快路径（`IObservableScreenStateProvider` cast + pre/post swipe UIA dump + 指纹比对）~30 行 |
| `ScreenStateResult.cs` | `HierarchyXml` / `HierarchyFingerprint` → 删除字段 |
| `RunAssetHook.cs` | `before.xml` / `after.xml` 写入逻辑（screenshot 写入保留） |
| `HostCommands.cs` | `AdbScreenStateProvider` 构造 DI、`StepCaptureStore` 构造、`ObservationPipeline` 的 UIA 参数传递 |
| `IObservableScreenStateProvider.cs` | `RefreshAsync` 的 `previousHierarchyXml` / `afterScroll` 参数 |
| `VisionScreenStateProvider.cs` | `_uia` 字段 + 整个 UIA try-catch 冗余侧信道；RefreshAsync 简化为直接返回 vision 状态 |
| `IAdbSession.cs` | `DumpUiHierarchyAsync()` 方法声明 |
| `ProcessAdbSession.cs` / `AdvancedSharpAdbSession.cs` | `DumpUiHierarchyAsync()` 实现 |

### 4.3 不动

| 接口/类 | 说明 |
|---------|------|
| `IScreenStateProvider` | `HasScroll` / `GetScrollProgress` / `IsEndOfList` / `GetScrollSwipeConfig` 不变 |
| `IScreenCapture` | `CaptureAsync` 不变，`StableFrameCapturer` 内部调用 |
| `IActionExecutor` | `SwipeAsync` 不变 |
| `IPageAnalyzer` | 接口不动 |
| `PageSnapshotManager` | Vision 指纹不变 |
| `PageAnalyzer` | 不动 |
| seen-set diff | `RecordSeenElementIds` 保留，与 ROI 比对互补 |
| R-12 门控 | `MaxEmptyScrollRetries` 保留，与 ROI 比对独立并行 |
| Python `fusion.py` / `server.py` / `schema.py` | **零改动** |

## 5. Decisions

| ID | 决策 | 理由 |
|----|------|------|
| D-1 | ROI 选择在 `InterceptionHandler` 内部消化，不暴露接口 | `IPageAnalyzer` 不改——ROI 是"怎么用分析结果"不是"分析"本身 |
| D-2 | **不复用** `bounds` 新增 `bbox` 字段——现有 `bounds` + `evidence.yoloId` 已足够 | `bounds` 来自 YOLO `Detection.box`（有 yoloId）或 OCR `token.box`（无 yoloId）；C# 侧可通过 `yoloId == null` 区分，无需 Python 任何改动 |
| D-3 | 稳定帧采集感知 ADB 截图延迟（动态等待） | 固定 sleep 在不同设备上不稳定；动态计算"目标间隔 − 已耗时"保证最快速度 + 足够间隔 |
| D-4 | 状态机先作为 `InterceptionHandler` 私有方法，后续按需抽类 | 改动最小；逻辑稳定后再决定是否独立 |
| D-5 | 全屏原始截图作为唯一坐标系 | 坐标系一致性问题；`UNICLAW_RAW_SCREEN_BUFFER=1` 推广后天然无 crop |
| D-6 | `HasScroll` / `IsEndOfList` 保持恒乐观（true/false），不做判断 | Vision 单帧无法证"到底"；ROI 比对是唯一到底权威 |
| D-7 | seen-set diff + R-12 门控保留，与 ROI 比对并行 | 双重保障：ROI 比对不依赖元素识别，seen-set diff 不依赖像素稳定——两者互补 |
| D-8 | dHash 作为感知哈希算法；快照尺寸与 dHash 尺寸独立 | 快照 256×128 为像素比对保留空间分辨率；dHash 内部缩放到 9×8 标准网格 |
| D-9 | 所有阈值通过 `ScrollSwipeConfig` 配置 | 不在代码中固化常数；实机标定后调整 |
| D-10 | `ObservationPipeline` 保留 back navigation reuse + AI 透传，只删 UIA 分支 | back reuse 是独立于 UIA 的有价值优化；删 UIA 不意味着删 pipeline |
| D-11 | 新增类全部 internal | 不暴露公共 API；`InterceptionHandler` 是唯一组装点 |
| D-12 | 二次滚动手势缩至 50% 距离，**同方向** | 避免弹性回弹造成"S0≈S1≈S2"假稳定；50% 足够区分。不改变方向——列表滚动始终同向（向下发现更多内容）。二次 swipe 的 `StartY`, `EndY` 计算：`separation = (config.EndY - config.StartY) × SecondSwipeDistanceRatio`；若 `separation ≈ 0`（手游/短屏设备），取最小滑动距离 `0.05` 屏幕高 |
| D-13 | 动态元素通过 `type` 黑名单识别，不需 Python 标注 | `loading`/`banner`/`carousel`/`progressbar` 等标签已知且稳定；在 C# 侧静态维护黑名单即可 |
| D-14 | `MadThreshold` 值域 0-255（灰度绝对值），非归一化 | 灰度矩阵值域 0-255，阈值直接用绝对像素差，避免浮点归一化开销 |
| D-15 | `_currentBaseline` 在首次 `GetOrSelectRoi` 时采集 | 首次滚动的 S0 必须是稳定帧——与后续滚动的基准采集逻辑一致，采集失败则 ROI 选择失败 |
| D-16 | ROI 快照路径强制使用原始全屏截图，不得经 `ImageResizer` 处理 | 缩放+压缩伪影 + 高斯模糊 = 像素比对指标失真。若 `UNICLAW_RAW_SCREEN_BUFFER` 未启用，`StableFrameCapturer` 内部截图需显式绕过 ImageResizer |
| D-17 | back reuse 去重键从 `HierarchyFingerprint` 迁移到 `PageSnapshotManager.Fingerprint` | UIA 指纹删除后，Vision 指纹是唯一页面标识；`ObservationPipeline.Remember` 行为不变 |
| D-18 | 动态元素黑名单初始值基于假设，实机运行后按实际 YOLO 标签校准 | 见 3.5.2 动态元素映射表 |

## 6. Acceptance Criteria

### 6.1 删除验证

| # | 标准 | 验证方式 |
|---|---|---|
| D1 | `AdbScreenStateProvider` 不再存在于代码库 | grep |
| D2 | `UiAutomatorPageAnalysis` 不再存在 | grep |
| D3 | `IUiAutomatorAvailability` 不再存在 | grep |
| D4 | `StepCaptureStore` / `IScreenStateCache` 不再存在 | grep |
| D5 | `ObservationPipeline.AnalyzeCurrentPageAsync` 无 UIA 分支 | 代码审查 |
| D6 | `ScreenStateResult` 无 `HierarchyXml` / `HierarchyFingerprint` | 代码审查 |
| D7 | `RunAssetHook` 不写 `.xml` 文件 | 集成测试 |
| D8 | 全项目编译通过（Release） | dotnet build |
| D9 | Python evidence 格式不变（`bounds` / `evidence.yoloId` 仍然存在且值正确） | Python unit test |

### 6.2 ROI 功能

| # | 标准 | 验证方式 |
|---|---|---|
| R1 | `RoiSelector.Select()` 有 YOLO Items → 返回非 null ROI | C# unit test（mock PageAnalysis + 真实截图） |
| R2 | `RoiSelector.Select()` 全部 yoloId==null → 退化到纯纹理评分，返回非 null（非空白页） | C# unit test |
| R3 | `RoiSelector.Select()` 全空白页 → 返回 null | C# unit test |
| R4 | `RoiSelector.Select()` 不选择大面积渐变背景区域（退化场景） | C# unit test（渐变背景 fixture） |
| R5 | `RoiSnapshotGenerator.Generate()` 输出 dHash 64-bit + GrayPixels 尺寸正确 | C# unit test |
| R6 | `SnapshotComparer.Compare(A, A)` → `IsSame=true`（同一帧） | C# unit test |
| R7 | `SnapshotComparer.Compare(A, B)` → `IsSame=false`（明显不同帧） | C# unit test |
| R8 | `StableFrameCapturer` 动态延迟：截图耗时 300ms，`StableSampleIntervalMs=100` → 无额外 sleep | C# unit test（mock IScreenCapture with delay） |

### 6.3 滚动判定

| # | 标准 | 验证方式 |
|---|---|---|
| S1 | `Different(S0, S1)` → `Scrolled` | 集成测试（模拟器 Settings 列表一次滚动） |
| S2 | `Same(S0,S1) + Same(S1,S2) + Same(S0,S2)` → `EndReached` | 集成测试（列表滚到底） |
| S3 | 证据冲突 → `Unknown`，不卡死 | 集成测试 |
| S4 | 连续 `Unknown` ≥ `MaxConsecutiveUnknown` → 清除 ROI + 重新分析 | 集成测试 |
| S5 | `HasScroll` / `IsEndOfList` 恒乐观 → 不可滚动页面仍尝试后到底 | 集成测试 |

### 6.4 回归

| # | 标准 | 验证方式 |
|---|---|---|
| G1 | scenario-locate 完整跑通（无 UIA） | 集成测试 |
| G2 | 现有滚动相关测试不变（模拟器 Settings） | dotnet test |
| G3 | Python `/v1/analyze` + `/v1/analyze_raw` endpoint 行为不变 | Python unit test |
| G4 | 现有 PageAnalyzer 测试全绿 | dotnet test |

## 7. 不改动的部分

- `ImageResizer` — 不动（旧路径继续使用，raw 路径绕开）
- Python `fusion.py` / `server.py` / `schema.py` — **零改动**
- `run_yolo_on_image` / `run_rapid_ocr_on_image` — 不动
- `fuse_evidence` / `fuse_evidence_from_crops` — 不动
- `PageAnalyzer` / `IPageAnalyzer` — 接口不动
- `IScreenStateProvider` / `IScreenCapture` — 接口不动
- `IActionExecutor` — 不动
- `PageSnapshotManager` — Vision 指纹不动
- seen-set diff + R-12 门控 — 保留
- 所有现有测试 — 旧路径行为不变（UIA 测试需删除或重写）
