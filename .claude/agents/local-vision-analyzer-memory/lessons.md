# Local Vision Analyzer — 历史诊断记录 (问题→根因→修复→验证)

## 2026-08-06: ScenarioCompletionVerifier endProven 缺口 — 生产 ROI 信号不被接受 (新发现)

**现状**: `endProven = !RequireEndOfList || traceEndProof || screenEndOfList` (ScenarioCompletionVerifier.cs:127-129)。
- `traceEndProof` 只认 `scroll_no_new_elements_end_reached` (legacy seen-set 路径, InterceptionHandler.cs:643)
- 生产恒走 ROI 路径 (InterceptionHandler.cs:496, `_capture != null`), end 信号是 `scroll_roi_end_reached` (:577) / `scroll_roi_content_guard` (:538,589) — **verifier 从不接受 → 生产环境 endProven 恒 false**
- `screenEndOfList` 生产恒 false (LocalVisionProvider isEndOfList 硬编码 false → VisionScreenStateProvider.IsEndOfList 恒 false; 仅模拟环境 MockScreenStateProvider=true)
- D-G13 被 child_control 失败遮蔽 (failure 优先级高于 end_of_list_unproven), 且 D-G13 trace 里 5 scrolls 全 `scroll_revealed_new_elements`, 无任何 end action — 缺口处于潜伏态
- 推论: 修复坐标 bug 后, 下一个自然失败模式将是 `end_of_list_unproven` (incomplete)

**结论**: 修 end 验证必须改 verifier (接受 `scroll_roi_end_reached`/`scroll_roi_content_guard`), 不是改 vision 端。

## 2026-08-06: D-G13 E2E run 双 crop 坐标空间 bug (root cause 级)

**Run**: artifacts/runs/integration/scenario-enumerate/enumerate-settings-safely/20260805T152640Z/enumerate-settings-safely/20260805T152708137Z-bc37815245f6462
(152 analysis.jsonl 帧, 19 clicks, result=failure child_control_execution_detected)

**现象**:
- step 32 点 'Notification history, conversations'(analysis y=0.8527) → 落到 Battery 页
- step 117 点 'Display, interaction, audio'(analysis y=0.1000) → 落到搜索框 → IME
- 5 个副标题幻影 click: step 17/32/65/98/117, V2 降级全部漏过
- 根页 Battery 行 (真实 y≈0.90) 在未滚动帧中完全不可见
- isEndOfList 152/152 false (非 bug, 见下)

**根因**: **双 crop 坐标空间 bug** (a6d6b37 引入, 2026-08-05)
- C# 侧 ImageResizer.ProcessRaw (PageAnalyzer.cs:112) 先 crop 6.25% top/bottom + resize 到 720px, 再发 JPEG 给 server
- Python server `_preprocess` (server.py:129) 对已 crop 图**再 crop 一次** 6.25%, `_remap_coords` 只映射回"收到的图"空间 (720x1120), 不是全屏 1080x1920
- 最终 item 坐标 = crop 空间: `y_crop = (y_real - 0.0625) / 0.875`
- 点击时 AdbActionExecutor.NormalizeAsync 直接 `y_crop * 1920` 当全屏像素 → 点击误差 `err_px = 240*y - 120`, 在 y=0.85 处偏下 ~85px → 落进下一行
- 同一误差: 服务器二次 crop 切掉真实屏幕下 6.1% (真实 y>0.879 不可见 → Battery 行整行消失)
- 坐标被 inflate 1/0.875=1.143× → V2 副标题 dy 0.0357-0.0384 > 0.035 全漏 (真实 dy 只有 0.031-0.034)
- C# 侧其实**知道**这个空间 (InterceptionHandler.cs:668-671 注释 + BuildYoloBboxes:691 对 YoloBboxes 做了反变换), 但 **item 坐标从未反变换**

**验证方法 (沉淀为脚本)**: OCR 真实截图 (RapidOCR, 全屏 1080x1920) 提取行真实 y (Settings 0.2138 / Network 0.4148 / Connected 0.5357 / Apps 0.6583 / Notifications 0.7760 / Battery 0.8982), 与 analysis.jsonl y 对比: analysis = (real-0.0625)/0.875 精确匹配到 4 位小数 (0.4027↔0.4148, 0.8143↔0.7760)。铁证。

**修复建议 (未实施)**:
- C# 主修: MapToPageAnalysisDto/PageAnalyzer 对 item + YoloBboxes 统一应用反变换 `y_full = y*0.875 + 0.0625` (参数与 BuildYoloBboxes 同源 env)
- 副修: V2 用独立 lastMenuItemAnchor (不被中间 text item 污染); V5 TopBarYThreshold 0.10→0.35 (搜索框真实 y=0.31)
- Python 备选: server 通过 header 接收原始全屏尺寸并映射回全屏 (需 API 变更)
- 影响面: analysis.jsonl fingerprint 变化 → trace-replay 基线 (FixVerificationTests/SettingsEnumerateRegression) 需再生成

## 2026-08-05: OCR 副标题误标为 menuItem (Storage "28%used" 案例)

副标题 ("28% used - 5.72GB free" 等) 被 deki-yolo 标为 menu_item → 引擎当独立导航项, 与主标题重复进入同一页面。
V2 降级 (SubtitleRowThreshold=0.035) 已实现, 但见 2026-08-06 案例: crop 空间 inflate 使 dy 超阈值漏判。

## 2026-08-05: OCR 文本空格变体导致重复点击 ("Bluetooth, pairing" vs "Bluetooth,pairing")

同一行 OCR 跨帧返回不同空格变体 → nodeId/identity 不稳定。V4 NormalizeTextForIdentity (LocalVisionProvider.cs:555) 折叠空白+标点→空格, 用于 V1 去重与跨帧 key; display Name 保持原文。
D-G13 引擎侧 NormalizeItemText (TraversalEngine.cs:1088) 归一逗号间距; 但 Host 验证器 Normalize (ScenarioCompletionVerifier.cs:239) 只折叠空白不归一逗号 → child_control 误报 (本次 run 失败原因)。

## 2026-08-05: YOLO 搜索框 type 波动 (text ↔ button)

搜索框 "QSearch settings" 被标 input/EditText → MenuItemType.Text 显示但 ExpectedAction.Action (可点击)。
V5 ExcludeTopBarSearch (LocalVisionProvider.cs:705) 要求 y<0.10 + 文本含 search — 本机搜索框真实 y=0.31, 永不命中 → 潜在幻影点击源 (本次 run 未点中, 潜伏风险)。

## 2026-08-05: endOfList 从未触发 (6/6 run 全部 false)

**不是 bug — 设计决定** (LocalVisionProvider.cs:491-517, D-7/D-199): 单帧视觉无法证明"到底", 硬编码 false, 真正 end 检测在引擎侧 ROI 快照差分 (InterceptionHandler.TryHandleScrollAsync: S0/S1/S2 → scroll_roi_end_reached)。本次 run ROI 路径工作正常 (5 scrolls 全成功)。hasScroll 恒 true 同理。

## 2026-08-05: 坐标错位模式 (副标题幻影 item 坐标偏到相邻行)

2026-08-06 已定位: 不是"幻影 item 坐标偏", 而是 crop 空间未反变换的点击误差随 y 增长。
