# E2E enumerate 去重与 Vision 质量修复 — PRD

> 状态: 设计完成 → 待实施
> 日期: 2026-08-06
> 关联: fix-dfs-depth-runaway → openspec/changes/e2e-dedup-vision-quality/

---

## 1. 问题背景

fix-dfs-depth-runaway 的 P0-P3 修复消除了 E2E 死循环（20min→正常完成），scrolls 41→3。但枚举质量存在以下问题：

### 1.1 双 crop 坐标空间不一致（根本原因）

C# `PageAnalyzer.ImageResizer` crop 6.25% top/bottom + resize 720px → Python `server._preprocess` 二次 crop → `_remap_coords` 映射回 crop 空间（720×1120），非全屏（1080×1920）→ `AdbActionExecutor` 用 crop 坐标当全屏像素点击 → 误差 `err_px = 240·y − 120`。

此单一 bug 导致：step 32/117 坐标错位点击、V2 副标题降级 4/5 漏判、V1 同排去重漏判、V5 搜索框阈值失效、底部 6.1% 屏幕不可见。

### 1.2 OCR 文本变体导致重复点击

同一 UI item 的 OCR 文本跨帧不一致（空格/逗号变体："Bluetooth, pairing" vs "Bluetooth,pairing"）→ nodeId 不同 → `_generatedPairs` + `VisitedNodes` 双双失配 → 同一页面被重复进入 2-3 次。

### 1.3 子页面从不滚动

D-G11 `depth >= maxDepth → 跳过滚动` 阻止了 11 个子页面滚动，只记录首屏。maxDepth 是树下降约束，不应用来限制同层滚动。

### 1.4 验证层缺陷

`ScenarioCompletionVerifier` 的 `traceEndProof` 只认 legacy seen-set 路径的 `scroll_no_new_elements_end_reached`，不认生产 ROI 路径的 `scroll_roi_end_reached` → `endProven` 恒 false。归一化器与 D-G13 不一致 → `child_control_execution_detected` 误报。

### 1.5 运行数据

| 指标 | Baseline | +D-G10/G11 | +D-G11 fix | +D-G12 | +D-G13+V1-V4 |
|------|----------|------------|------------|--------|-------------|
| stepsConsumed | 59 | 120 | 45 | 120 | 120 |
| scrollsConsumed | 21 | 41 | 8 | 4 | 5 |
| discoveredEntries | 11 | 14 | 7 | 23 | 23 |
| visitedEntries | 2 | 11 | 1 | 20 | 18 |
| completionReason | settings_home_not_restored | settings_home_not_restored | settings_home_not_restored | settings_home_not_restored | child_control_execution_detected |

引擎枚举能力从"0 discovered"进步到"23-25 discovered"，修复方向正确。失败原因已从引擎层转移到 Host 验证层。

---

## 2. 方案设计

### 2.1 坐标空间反变换（P0）

**位置**：`UniBrain/PageAnalyzer.cs` — `AnalyzeCurrentPageAsync` 方法体内，与 `ImageResizer` 调用同源。

**逻辑**：对返程 `PageAnalysisDto` 的 item / menu / popup 坐标做反变换 `y_full = y·(1-cropTop-cropBottom) + cropTop`。变换参数来自 env `UNICLAW_IMAGE_CROP_TOP/MAX_WIDTH`（同源 `ImageResizer.DefaultCropTop/DefaultMaxWidth` 兜底）。

**同时清理**：`InterceptionHandler.BuildYoloBboxes` 删除内部变换改透传（消除双变换风险 + env 重复解析）。

**捆绑修**：`tools/local_vision/server.py` 的 `_CROP_TOP/_CROP_BOTTOM` 默认改为 0（保留 env 可覆盖），消除二次 crop 导致的底部 6.1% 屏幕覆盖损失。

**下游自动修复**（此一修改，无需额外改动）：
- step 32/117 坐标错位 → 自然正确
- V2 副标题降级 4/5 漏判 → dy 回到真实空间 < 0.035
- V1 同排去重漏判 → dy 回到阈值内
- V5 搜索框排除 → y 恢复全屏后自然命中
- 底部区域恢复可见

**改动量**：`PageAnalyzer.cs` +15~20 行，`InterceptionHandler.cs` -25 行，`server.py` 3 行。

### 2.2 D-G11 删除（P1）

**位置**：`Traversal/InterceptionHandler.cs:487-490`。

**逻辑**：删除 `depth >= maxDepth → return (false, ...)` gate。maxDepth 是树下降约束（`NodeStack.Push` 拒绝 depth+1），不应限制同层滚动。P3 已处理遍历安全面，D-G7 已处理子帧 push 面。预算靠 maxScrolls/maxSteps 约束。

**改动量**：-4 行 + 改注释。

### 2.3 Verifier ROI 信号接受（P0）

**位置**：`Host/Verification/ScenarioCompletionVerifier.cs:124`。

**逻辑**：`traceEndProof` 改为接受 `scroll_roi_end_reached` OR `scroll_roi_content_guard`（保留 legacy `scroll_no_new_elements_end_reached` 供模拟环境）。

**改动量**：~3 行。

### 2.4 归一化统一（P1）

**位置**：`Host/Verification/ScenarioCompletionVerifier.cs:239`。

**逻辑**：`Normalize()` 加 `\s*,\s*`→", " 处理，与 D-G13 `NormalizeItemText` 一致。

**改动量**：~2 行。

### 2.5 IsEndOfList 废弃声明（P2，方案 C → 后续 B）

**当前（C）**：`PageAnalysis.IsEndOfList` / `HasScroll` 加 `[Obsolete]`。verifier 改为从 trace decision 读 `scroll_roi_end_reached`。

**后续（B）**：引入 `PageAnalysisState` 包装类型，将滚动状态（endOfListReached / scrollCount）与页面分析绑定。`TryHandleScrollAsync` 检测到 end 时回写，下游直接读。消费 `CurrentPageAnalysis` 的代码需同步适配。

---

## 3. 验收标准

### 3.1 单元 / 仿真

| # | 标准 | 验证方式 |
|---|------|---------|
| AC1 | 坐标反变换后 analysis 坐标 = 全屏空间 | `coord_validation.py` 截图 OCR ↔ analysis 差值 < 0.002 |
| AC2 | 全量回归 ≥ 1161/0/2 | `dotnet test tests/UniClaw.Core.Tests -v:q` |
| AC3 | verifier 接受 ROI end 信号 | ScenarioCompletionVerifierTests 新增 |
| AC4 | 归一化统一：逗号变体匹配 | `Normalize("Darktheme,fontsize,brightness") == Normalize("Darktheme, fontsize, brightness")` |
| AC5 | V2 副标题降级 5/5（坐标修复后） | LocalVisionProviderTests 新增 |
| AC6 | D-G11 删除后 depth==maxDepth 可滚动 | FixVerificationTests 新增：depth=2 可滚动 fixture |

### 3.2 E2E

| # | 标准 | 验证方式 |
|---|------|---------|
| AC7 | `scenario-enumerate` 跑通 success | dotnet test IntegrationScope=scenario-enumerate |
| AC8 | 无坐标错位点击 | TraceTool diagnose: 0 out-of-scope click |
| AC9 | 无 child_control_execution_detected | result.json |
| AC10 | 搜索框不被点击 | trace verification_passed 无 QSearch |
| AC11 | 副标题不独立出现 | trace 无 "28%used" 等 |
| AC12 | visitedEntries ≥ 20 | result.json |

---

## 4. 不在此范围

- Accessibility `ResolveTextTarget` 失败（独立 case）
- `excludePatterns` 死配置（已同意废弃）
- V4 归一化接入 engine fingerprint（需改 `PageSnapshotManager`）

---

## 5. 资产索引

| 资产 | 路径 |
|------|------|
| E2E run 最新 | `artifacts/runs/integration/scenario-enumerate/.../20260805T152640Z/` |
| E2E run D-G12 | `artifacts/runs/integration/scenario-enumerate/.../20260805T144707Z/` |
| OpenSpec change | `openspec/changes/e2e-dedup-vision-quality/` |
| e2e-diagnose skill | `.claude/skills/e2e-diagnose/SKILL.md` |
| local-vision-analyzer | `.claude/agents/local-vision-analyzer.md` |
