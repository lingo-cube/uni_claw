## Context

当前滚动检测架构：`InterceptionHandler.TryHandleScrollAsync` 在 DynamicMatch 子节点耗尽时执行。旧路径依赖两个独立通道：① UIAutomator XML dump（`AdbScreenStateProvider` 解析 `scrollable` 节点 + SHA-256 指纹）做 D5 快路径短路；② AI 视觉 seen-set diff（swipe 后 PageAnalysis.Items 差分）做兜底。UIA 首次失败即永久禁用（D6），且是 Android-only 依赖。

同时 `ObservationPipeline` 有 UIA→AI 级联快速路径（L1 UIA dump → parse → ≥3 items → 直接返回，~1s vs AI ~60s），但仅对标准 Settings 页面 >90% 命中。

本设计完全移除 UIAutomator 依赖链，将 scroll 到底检测替换为视觉 ROI 聚合比对，观测管线退化为纯 AI 透传（保留 back navigation reuse）。

## Goals / Non-Goals

**Goals:**
- 完全删除 UIAutomator 代码路径（5 文件 + 10+ 处行级删除）
- ROI 聚合比对提供可靠的 Scrolled / EndReached / Unknown 三态判定
- 零 Python 改动（现有 `bounds` + `evidence.yoloId` 已满足 ROI 密度评分）
- 零公共接口新增（所有新类 internal，`IPageAnalyzer`/`IScreenStateProvider`/`IScreenCapture` 不动）
- 防御性设计：稳定帧超时、连续 Unknown → 重选 ROI、二次缩距滚动防回弹

**Non-Goals:**
- 不改变 `IScreenStateProvider` 的 4 方法签名（`HasScroll` 等保持恒乐观）
- 不改变 swipe 手势执行（`IActionExecutor.SwipeAsync` 不变）
- 不改变 Container 域模型（滚动不推帧）
- 不在此变更中实现 raw RGBA 全链路（已有独立 PRD）

## Decisions

| ID | 决策 | 替代方案 | 理由 |
|----|------|---------|------|
| D-1 | ROI 选择在 `InterceptionHandler` 内部消化，不暴露接口 | 放 `IPageAnalyzer` 新增方法 | `IPageAnalyzer` 语义是"分析页面"不是"选 ROI"；且需要原始截图像素——`IPageAnalyzer` 不应碰像素 |
| D-2 | Python 零改动，复用现有 `bounds` + `yoloId` | 新增 `bbox` 字段 | `bounds` 覆盖率 100%（`fusion.py:75/104/186` 三个路径均包含）；`yoloId==null` 区分 OCR 提升候选 |
| D-3 | 稳定帧采集感知 ADB 截图延迟（动态等待） | 固定 sleep | 不同设备 ADB 耗时 100-500ms 波动；"目标间隔 − 已耗时"保证最快速度 + 足够间隔 |
| D-4 | 二次滚动手势缩至 50% 距离，同方向 | 换方向 / 不改距离 | 两次完全相同的 swipe 可能在弹性区域造成假稳定；缩距足够区分而不引入方向变化复杂性 |
| D-5 | dHash 作为感知哈希；快照本体 256×128 与 dHash 9×8 独立缩放 | pHash / 统一尺寸 | dHash 汉明距离 O(1)、对 UI 缩放鲁棒；快照本体保留足够空间分辨率给 MAD/变化比计算 |
| D-6 | 三指标复合判定（哈希 + MAD + 变化比），AND 语义 | 单一阈值 / OR 语义 | 三个指标覆盖不同失败模式：哈希判别整体结构、MAD 判别整体亮度偏移、变化比判别局部动画 |
| D-7 | 动态元素通过 C# 静态 `type` 黑名单识别 | Python 标注 `is_dynamic` | 不在 Python 侧加字段；黑名单初始 5 个标签（`loading`/`banner`/`carousel`/`progressbar`/`video`），实机标注后校准 |
| D-8 | 所有阈值通过 `ScrollSwipeConfig` 配置 | 代码常量 | 实机标定后调整；不同设备/场景可能需要不同阈值 |
| D-9 | `ObservationPipeline` 保留 back reuse + AI 透传，只删 UIA 分支 | 整个删除 | back reuse 是独立于 UIA 的有价值优化（避免回退时重复 AI 调用） |
| D-10 | 新增类全部 internal，`InterceptionHandler` 是唯一组装点 | 暴露公共 API | 不扩大 Core 的公共 surface area；滚动检测是遍历引擎内部实现细节 |
| D-11 | `StableSampleMaxTimeMs`（默认 3000ms）作为绝对超时 | 仅靠重试次数 | 懒加载图片/网络请求等慢场景，页面持续渲染可能永远不稳定——绝对时间上限兜底 |

## Risks / Trade-offs

| 风险 | 缓解 |
|------|------|
| **ROI 误选**：YOLO 候选稀疏时密度分可能选中"恰好有 YOLO 框"但内容稀疏的区域 | 纹理分 + 非纯色分权重平衡；退化策略在全部 OCR 时自动切换为纯纹理 |
| **动态元素标签不一致**：YOLO 模型输出标签可能与黑名单不匹配（如 `advertisement` vs `banner`） | 黑名单初始基于假设，实机首次运行时记录所有 type 值并人工校准 |
| **弹性回弹假稳定**：iOS/车机过界回弹可能使 S0≈S1≈S2 | 二次缩距滚动 + 三次两两确认 + 稳定帧采集等待回弹结束——三层防御 |
| **懒加载导致永远不稳定**：占位图逐渐加载，连续两对相似条件永远不满足 | `StableSampleMaxTimeMs` 绝对超时 → Unknown → 连续 Unknown 达到上限 → 清除 ROI 重选 |
| **渐变背景误选**：车机设置页灰色渐变 Laplacian 方差高但无内容 | 退化策略中纹理分+非纯色分组合评分——渐变背景非纯色占比低，综合得分低于有图案区域 |
| **压缩伪影失真**：经 ImageResizer JPEG 编码的截图再经高斯模糊后像素比对指标偏差 | 3.2 截屏路径约束——ROI 快照强制使用原始全屏截图 |
| **无 UIA 后每次空滚多 1 次 AI 调用 (~3-5s)** | `MaxEmptyScrollRetries=1` → 至多 2 次空滚 ≈ +6-10s per Container；ROI 比对不依赖元素识别，无需额外 AI |
