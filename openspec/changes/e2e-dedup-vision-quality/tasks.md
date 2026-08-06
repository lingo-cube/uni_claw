## 0. 前置: D-G12 + V1-V4 (已完成，保持不变)

- [x] 0.1 D-G12 目的地指纹去重（`TraversalEngine.RunAsync`）— 已实现
- [x] 0.2 V1 同排 item 去重（`LocalVisionProvider`）— 已实现
- [x] 0.3 V2 副标题类型降级（`LocalVisionProvider`）— 已实现，坐标修复后 dy 阈值生效
- [x] 0.4 V3+V4 OCR 文本质量（`LocalVisionProvider`）— 已实现

## 1. P0: 坐标空间反变换（`PageAnalyzer.cs`）

- [x] 1.1 在 `AnalyzeOnceAsync` raw 路径中提取 `RawScreenBuffer` 原始宽高，传入 `MapToPageAnalysis` / `ToCoordinate`
- [x] 1.2 `ToCoordinate` 加坐标逆变换逻辑：`y_full = y_cropped * (1 - cropTop - cropBottom) + cropTop`
- [x] 1.3 变换参数与 `ImageResizer` 调用同源（env `UNICLAW_IMAGE_CROP_TOP` / `UNICLAW_IMAGE_MAX_WIDTH`，fallback `ImageResizer.DefaultCropTopRatio` / `DefaultMaxWidth`）
- [x] 1.4 fallback 路径（byte[] PNG）保持现有行为（无尺寸信息，跳过逆变换）
- [x] 1.5 `PageAnalysis` 中 `YoloBboxes` 数组的 bbox 中心点 y 坐标同步变换（单点 x 不涉及 crop 可不动）

## 2. P0: `BuildYoloBboxes` 透传（`InterceptionHandler.cs`）

- [x] 2.1 删除 `BuildYoloBboxes` 内部的 `sx` / `cropTopPx` 计算和 y 方向 `+ cropTopPx` 变换（lines 699-708, 710-721）
- [x] 2.2 改为简单去归一化：`x_px = x_norm * screenW`, `y_px = y_norm * screenH`
- [x] 2.3 删除 env var 解析 `UNICLAW_IMAGE_MAX_WIDTH` / `UNICLAW_IMAGE_CROP_TOP`（lines 699-708）

## 3. P0: Python vision server crop 归零（`server.py`）

- [x] 3.1 `_CROP_TOP` / `_CROP_BOTTOM` 默认值 0.0625 → 0.0（lines 63-64）
- [x] 3.2 保留 env var / label-mapping config 覆盖能力（`_load_spatial` 逻辑不变）

## 4. P0: Verifier 接受 ROI end 信号（`ScenarioCompletionVerifier.cs`）

- [x] 4.1 `traceEndProof` 改为接受 `scroll_roi_end_reached` OR `scroll_roi_content_guard`（保留 legacy `scroll_no_new_elements_end_reached`）
- [x] 4.2 新增 `ScenarioCompletionVerifierTests` 测试：ROI end reached → endProven=true、ROI content guard → endProven=true

## 5. P1: 删除 D-G11 深度门（`InterceptionHandler.cs`）

- [x] 5.1 删除 `TryHandleScrollAsync` 内 `depth >= maxDepth → return (false, ...)` 逻辑（lines 487-490）
- [x] 5.2 保留并更新注释说明 maxDepth 仅约束树下降（`NodeStack.Push`），不约束同层滚动

## 6. P1: 归一化统一（`ScenarioCompletionVerifier.cs`）

- [x] 6.1 `Normalize()` 方法加 `\s*,\s*` → `, ` 替换（与 D-G13 `NormalizeItemText` 一致）
- [x] 6.2 新增测试：`Normalize("Darktheme,fontsize") == Normalize("Darktheme, fontsize")`、`Normalize("a , b") == Normalize("a, b")`

## 7. P2: `IsEndOfList` 废弃声明（`PageAnalysisRecords.cs`）

- [x] 7.1 `PageAnalysis.IsEndOfList` / `HasScroll` 加 `[Obsolete("Use trace decision scroll_roi_end_reached instead. See openspec/changes/e2e-dedup-vision-quality/design.md D9.")]`
- [x] 7.2 `HostCommands.cs:956` 调用处加 `#pragma warning disable`（或改为读 trace decision，若 D7 已就绪）

## 8. 测试

- [ ] 8.1 `coord_validation.py` 脚本：截图 OCR 坐标 ↔ analysis 坐标差值验证（AC1: diff < 0.002）
- [x] 8.2 `FixVerificationTests.cs` 新增 D-G11 删除测试（AC6: depth=2 scrollable fixture）
- [x] 8.3 `LocalVisionProviderTests.cs` 新增 V2 副标题降级 5/5 测试（AC5: 坐标修复后 dy < 0.035）
- [x] 8.4 `ScenarioCompletionVerifierTests.cs` 新增 ROI 信号接受 + 归一化统一（AC3 + AC4）
- [x] 8.5 全量回归 `dotnet test tests/UniClaw.Core.Tests -v:q`（AC2: 1146/17/2，与 pre-existing baseline 一致，0 回归）

## 9. E2E 验证

- [ ] 9.1 `scenario-enumerate` E2E 跑通 success（AC7: `dotnet test IntegrationScope=scenario-enumerate`）
- [ ] 9.2 TraceTool diagnose: 0 out-of-scope click（AC8）
- [ ] 9.3 result.json: visitedEntries ≥ 20, 无 `child_control_execution_detected`（AC9 + AC12）
- [ ] 9.4 trace 无 QSearch 被点击、无副标题独立出现（AC10 + AC11: `"28%used"` 等不出现）
