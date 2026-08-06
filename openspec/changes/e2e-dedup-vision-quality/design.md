## Context

E2E enumerate-settings-safely 多轮诊断（20260805T123529899Z 及后续 run）发现枚举质量问题，根因为**双 crop 坐标空间不一致**：

- C# `PageAnalyzer.ImageResizer` crop 6.25% top/bottom + resize 720px → 模型返回坐标归一化到 crop 空间（720×1120），非全屏（1080×1920）
- Python `server._preprocess` 在已 crop 图上再次 crop 6.25% → `_remap_coords` 映射回 C# crop 空间
- C# 消费端（`AdbActionExecutor`、`BuildYoloBboxes`）用 crop 空间坐标当全屏像素 → 点击位置 `err_px = 240·y − 120`

此外，D-G11 深度门阻止子页面滚动、Verifier 不认 ROI 路径的 end-of-list 信号、Verifier Normalize() 与 D-G13 不一致导致逗号变体误匹配。

约束：不改 StepOrchestrator（编排层），不改 TraversalFSM handler 签名（FSM 层），不改 `Coordinate` 构造器 0-1 归一化不变式。

## Goals / Non-Goals

**Goals:**
- P0: 坐标空间反变换 — `PageAnalyzer` 输出全屏归一化坐标，`server.py` 消除二次 crop，`BuildYoloBboxes` 透传
- P1: 删除 D-G11 `depth >= maxDepth → skip scroll` 门
- P0: Verifier `traceEndProof` 接受 ROI 路径 end 信号
- P1: Verifier `Normalize()` 与 D-G13 统一（逗号变体归一化）
- P2: `PageAnalysis.IsEndOfList` / `HasScroll` 加 `[Obsolete]` 声明

**Non-Goals:**
- 不实施 IsEndOfList 方案 B（`PageAnalysisState` 包装类型，后续单独 change）
- 不修 Accessibility `ResolveTextTarget` 失败（独立 case）
- 不删 `excludePatterns` 死配置（已同意废弃，独立 change）
- 不修改 Generate 去重逻辑（`_generatedPairs` 不变）
- 不修改 FSM 转移矩阵

## Decisions

### D1: 去重在 verification_passed 时记录，不在 Generate 时预判

**选择**: 事后（验证时）记录目的地指纹 → 后续 sibling 进入时已有记录可查。

**拒绝替代方案**:
- 方案 A（Generate 时坐标去重）: 需要 `MatchableItem` 携带坐标，改造 Generate 链路。坐标启发式不可靠（不同设备行高不同），职责越界（引擎不该管 vision 排版）。
- 方案 B（verification_passed 时 Pop + PressBack）: 对抗审阅发现对叶子节点 Peek(1) 偏一、裸 Pop 引发 D-74 子帧击穿、`continue` 跳过收尾。采纳审阅建议：只记录+检测，不操作栈。

**代价**: 重复子节点仍被执行 1 次（多 2-3 步）。Vision 侧 V1-V4 修复后源头消除。

### D2: 父子关系用 stepFrame 判断，不用 Peek(n)

**选择**: RunAsync line 331 的 `stepFrame` 在执行前捕获。叶子节点在 line 342-357 被 pop，`stepFrame != Peek()` → 父=Peek()。容器节点未被 pop，`stepFrame == Peek()` → 父=Peek(1)。

**拒绝替代方案**: Peek(1) 在叶子场景偏一到祖父节点（审阅漏洞 1）。

### D3: 只走 verification_passed 路径

**选择**: 检查 `fromState==ResultVerify` + decision trace 为 `verification_passed` / `verification_passed_retry`。

**拒绝替代方案**: `fromState==ResultVerify` 单条件会让 `verification_page_unchanged` 路径误记录父页面自己的指纹（审阅漏洞 7），且 stale-click 熔断可能已 pop 过一帧。

### D4: 坐标逆变换在 ToCoordinate 单一咽喉点

**选择**: 在 `PageAnalyzer.ToCoordinate`（line 384 `new Coordinate(dto.X, dto.Y)`）处施加逆变换 `y_full = y·(1-cropTop-cropBottom) + cropTop`。所有坐标（items/menus/popups/close/back）流经此处，单点全覆盖。

变换参数来源：优先 env `UNICLAW_IMAGE_CROP_TOP` / `UNICLAW_IMAGE_MAX_WIDTH`，fallback `ImageResizer.DefaultCropTopRatio` / `DefaultMaxWidth`。与 `ImageResizer` 调用（line 112/133）和 `BuildYoloBboxes`（line 699-708）保持同源。

`RawScreenBuffer` 携带原始宽高（`RawScreenBuffer.cs:9` record struct），`AnalyzeOnceAsync` 在 raw 路径（line 104）即可拿到。逆变换输出仍为 0-1 归一化坐标（除以原图宽高），保持 `Coordinate` 不变式。

**拒绝替代方案**:
- 在 `MapToPageAnalysis` 之后整体变换: 需要对 `PageAnalysis` 全量坐标做后处理，粒度过粗，改动面大
- 修改 Python `_remap_coords` 让它感知 C# crop: 需要跨进程传 C# crop 参数，耦合过紧

**代价**: `AnalyzeOnceAsync` 需改为实例方法或传原图尺寸参数。`ToCoordinate` 从 static 改为接收尺寸上下文参数。fallback 路径（byte[] PNG）无原图尺寸，需额外处理（解码 SKBitmap 取宽高，或接受 fallback 路径不做逆变换）。

### D5: BuildYoloBboxes 删除内部变换改透传

**选择**: `PageAnalyzer` 在输出 `PageAnalysis` 前对 `YoloBboxes` 数组也施加逆变换（按 bbox 中心点做 y 方向映射）。`BuildYoloBboxes` 改为简单去归一化 `x_px = x_norm * screenW, y_px = y_norm * screenH`。

**拒绝替代方案**: 保留 `BuildYoloBboxes` 内部变换。会导致：
1. env 参数在 `PageAnalyzer` + `InterceptionHandler` 两处重复解析，未来改参数需同步两处
2. 若未来 PageAnalyzer 变换逻辑变更（如支持非对称 crop），BuildYoloBboxes 需同步更新 → 容易遗漏

**代价**: `PageAnalyzer` 需额外处理 YoloBboxes 数组（+5 行）。

### D6: 删除 D-G11 深度门

**选择**: 删除 `InterceptionHandler.cs:487-490` 的 `depth >= maxDepth → return (false, ...)` 逻辑。maxDepth 是树下降约束（`NodeStack.Push` 拒绝 depth+1），不应限制同层滚动。P3 已处理遍历安全面，D-G7 已处理子帧 push 面，预算靠 maxScrolls/maxSteps 约束。

**拒绝替代方案**: 改为 `maxScrolls 约束` 间接控制。但 maxScrolls 是全局计数，无法感知"当前帧深度是否已达上限"。且 D-G11 的语义本就不对（滚动 = 同层翻页 ≠ 深度下降），删除更直观。

**代价**: depth=maxDepth 的帧可以滚动。若页面极深（≥6 层），可能消耗 maxScrolls 配额。但实际场景 Settings 深度通常 ≤4，风险可控。

### D7: traceEndProof 接受 ROI 路径 end 信号

**选择**: `ScenarioCompletionVerifier.cs:124` 的 `traceEndProof` 改为接受 `scroll_roi_end_reached` OR `scroll_roi_content_guard`（保留 legacy `scroll_no_new_elements_end_reached` 供模拟环境）。

**拒绝替代方案**: 只靠 `screenEndOfList`（`PageAnalysis.IsEndOfList`）兜底。但 IsEndOfList 经 `ScreenState` 中转，缺失分析时默认 true（偏乐观，`VisionScreenStateProvider.cs:31`），且语义上 "trace 有 end decision" 比 "page analysis 标记 end" 更权威（trace 是运行时决策记录）。

### D8: Normalize() 加逗号变体归一化

**选择**: `ScenarioCompletionVerifier.cs:239` 的 `Normalize()` 在现有 whitespace-fold + lowercase 基础上加 `\s*,\s*` → ", " 处理。与 D-G13 `NormalizeItemText` 语义一致。

具体实现：在 split-by-whitespace 前，将 `,` 替换为 ` , ` → split 时逗号成为独立 token → rejoin 后 `"a , b"` → `"a , b"`... 

Wait, 需要更精确。D-G13 `NormalizeItemText` 的行为：将 `\s*,\s*` 替换为 `, `。所以 `"Dark theme , font size"` → `"Dark theme, font size"`。Normalize() 做同样的替换：`value.Trim().ToLowerInvariant()` 之后，`Regex.Replace(..., @"\s*,\s*", ", ")` 然后再 split + rejoin。

**拒绝替代方案**: 在 D-G13 侧去掉逗号归一化，让两边都不处理。但 D-G13 已落地且有效，改 D-G13 风险大于改 Normalize()。

### D9: IsEndOfList 废弃声明（方案 C → 后续 B）

**当前（C）**: `PageAnalysis.IsEndOfList` / `HasScroll` 加 `[Obsolete("Use trace decision scroll_roi_end_reached instead")]`。Verifier 改为从 trace decision 读（D7 已覆盖）。

**后续（B）**: 引入 `PageAnalysisState` 包装类型，将滚动状态（endOfListReached / scrollCount）与页面分析绑定。`TryHandleScrollAsync` 检测到 end 时回写，下游直接读。消费 `CurrentPageAnalysis` 的代码需同步适配。**后续单独 change，不在本范围。**

**当前代价**: `VisionScreenStateProvider.IsEndOfList()` 在 `HostCommands.cs:956` 唯一生产调用，`[Obsolete]` 后产生编译警告（warning，非 error）。`HostCommands` 可暂时 `#pragma warning disable` 或在 D7 verifier fix 后改为读 trace decision。

### D10: server.py crop 默认值改为 0

**选择**: `tools/local_vision/server.py:63-64` 的 `_CROP_TOP` / `_CROP_BOTTOM` 默认值从 `0.0625` 改为 `0.0`。保留 env 变量覆盖能力（`UNICLAW_IMAGE_CROP_TOP` / `UNICLAW_IMAGE_CROP_BOTTOM`）。

C# 已在发送前完成 crop（`ImageResizer`），Python 不应二次 crop。此改动同时恢复底部 6.1% 屏幕覆盖（`_CROP_BOTTOM=0.0625` 导致底部 UI 元素在 YOLO/OCR 阶段被裁剪丢弃）。

**拒绝替代方案**: 在 C# 侧不做 crop，让 Python 做唯一 crop。但 `ImageResizer` 同时服务 C# 模型调用（AI vision），不能移除。

## Risks / Trade-offs

- [指纹碰撞] 不同页面 hash 值相同 → 误判重复 → 合法子树被跳过（概率极低，32-bit int 空间，且 hash 输入为 sorted (type,name) 多重集）
- [空页漏检] fp==0 时不记录也不检查 → D-G12 对空页不生效（可接受，空页无内容可遍历）
- [跨 run 污染] 字典在 RunAsync 入口初始化 → 不跨 run 残留
- [坐标逆变换-fallback] fallback 路径（byte[] PNG）无原图尺寸，逆变换可能跳过 → 接受风险，生产走 raw 路径
- [YoloBboxes 变换一致性] 若 PageAnalyzer YoloBboxes 变换与 BuildYoloBboxes 原逻辑有微小偏差 → ROI 选区可能偏移 1-2px → 可接受，YOLO bbox 本身有 ±5px 误差
- [D-G11 删除后滚动溢出] depth=maxDepth 帧滚动超出预算 → maxScrolls 全局约束兜底
- [[Obsolete] 编译警告] 下游消费 `IsEndOfList`/`HasScroll` 的代码产生 warning → 用 `#pragma` 暂时抑制，待方案 B 实施后移除
