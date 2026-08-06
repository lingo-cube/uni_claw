## Why

E2E enumerate 运行诊断发现枚举质量问题，根因为**双 crop 坐标空间不一致**：

1. **C# `PageAnalyzer.ImageResizer`** crop 6.25% top/bottom + resize 720px → 模型返回坐标归一化到 crop 空间（720×1120），非全屏（1080×1920）
2. **Python `server._preprocess`** 在 C# 已 crop 的图上再次 crop 6.25% → `_remap_coords` 映射回 C# crop 空间（1120h），非全屏
3. **C# 消费端**（`AdbActionExecutor`、`InterceptionHandler.BuildYoloBboxes`）将 crop 空间坐标当全屏像素点击 / 传 ROI

此单一 bug 导致：step 32/117 坐标错位点击、V2 副标题降级 4/5 漏判（dy 在 crop 空间 < 0.035 但真实空间 > 阈值）、V1 同排去重漏判（dy 在 crop 空间超出阈值）、V5 搜索框 y 偏移失效、底部 6.1% 屏幕不可见。

此外还有四个独立问题：
- **D-G11** 阻止 11 个子页面滚动（`depth >= maxDepth → skip scroll`，maxDepth 是树下降约束不应限制同层滚动）
- **Verifier** `traceEndProof` 只认 legacy seen-set 路径 `scroll_no_new_elements_end_reached`，不认生产 ROI 路径 `scroll_roi_end_reached` → `endProven` 恒 false
- **Verifier** `Normalize()` 与 D-G13 `NormalizeItemText` 不一致 → 逗号变体 `"a,b" vs "a, b"` 无法匹配 → `child_control_execution_detected` 误报
- **OCR 文本变体** 跨帧不一致 → nodeId 不同 → `_generatedPairs` + `VisitedNodes` 双双失配 → 同一页面重复进入 2-3 次（已有 D-G12 目的地去重防御 + V1-V4 源头修复）

## What Changes

### P0 — 坐标空间反变换（根因修复）
- **PageAnalyzer.cs**: `ToCoordinate` 方法体内加坐标逆变换 `y_full = y·(1-cropTop-cropBottom) + cropTop`，变换参数与 `ImageResizer` 同源
- **InterceptionHandler.cs BuildYoloBboxes**: 删除内部变换改透传（消除双变换风险 + env 重复解析），改为简单去归一化
- **server.py**: `_CROP_TOP` / `_CROP_BOTTOM` 默认改为 0（保留 env 可覆盖），消除二次 crop 及底部 6.1% 覆盖损失
- **下游自动修复**: step 32 坐标错位、V2 副标题降级 4/5→5/5、V1 同排去重漏判、V5 搜索框排除、底部区域恢复可见

### P1 — D-G11 删除
- **InterceptionHandler.cs**: 删除 `depth >= maxDepth → skip scroll` 门，滚动预算靠 maxScrolls/maxSteps 约束

### P0 — Verifier ROI 信号接受
- **ScenarioCompletionVerifier.cs traceEndProof**: 接受 `scroll_roi_end_reached` OR `scroll_roi_content_guard`（保留 legacy `scroll_no_new_elements_end_reached` 供模拟环境）

### P1 — 归一化统一
- **ScenarioCompletionVerifier.cs Normalize()**: 加 `\s*,\s*` → ", " 处理，与 D-G13 `NormalizeItemText` 一致

### P2 — IsEndOfList 废弃声明
- **PageAnalysisRecords.cs**: `IsEndOfList` / `HasScroll` 加 `[Obsolete]`，方案声明（当前不实施 B）

## Capabilities

### New Capabilities
- `coordinate-inverse-transform`: PageAnalyzer 输出全屏归一化坐标，消除双 crop 误差
- `verifier-roi-end-signal`: Verifier 接受 ROI 路径的 end-of-list 信号
- `normalization-comma-unification`: Normalize() 处理逗号-空格变体

### Modified Capabilities
- `traversal-destination-dedup`: 保留 D-G12 目的地指纹去重（已有，不变）
- `vision-item-dedup`: V1-V4 Vision 质量修复（已有，坐标修复后自动生效）
- `traversal-scroll-depth`: 删除 D-G11 深度门（同级滚动不受 maxDepth 约束）

### Deprecated
- `page-analysis-end-of-list`: `PageAnalysis.IsEndOfList` / `HasScroll` 废弃，改为从 trace decision 读取

## Impact

- 引擎: `PageAnalyzer.cs`（+15~20 行坐标变换）、`InterceptionHandler.cs`（-25 行 BuildYoloBboxes 简化 + -4 行 D-G11 删除）
- Vision: `server.py`（3 行 crop 默认值改动）
- Host: `ScenarioCompletionVerifier.cs`（~3 行 traceEndProof + ~2 行 Normalize）、`PageAnalysisRecords.cs`（2 行 [Obsolete]）
- 测试: `FixVerificationTests.cs`（D-G11 删除测试）、`ScenarioCompletionVerifierTests.cs`（ROI 信号接受 + 归一化统一）、`LocalVisionProviderTests.cs`（副标题降级 5/5）、`coord_validation.py`（坐标一致性脚本）
- E2E: `scenario-enumerate` 回归，expected: success + visitedEntries ≥ 20 + 无 `child_control_execution_detected`
