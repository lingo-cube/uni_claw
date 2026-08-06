# Local Vision Analyzer — 管线知识 (L1-L3, 源码锚定)

## L1 管线架构

- **C#**: src/UniClaw.LocalVisionProvider/LocalVisionProvider.cs — 4 步映射管道 (MapToPageAnalysisDto:385)
  - Step 1 YOLO label→AI type (label-mapping.json, 未知→"text"+warning:431-437)
  - Step 2 Y 聚类 level1 (Level1MaxY=0.08) / items
  - Step 3 scroll 门禁 (硬编码 hasScroll=true, isEndOfList=false:503-517)
  - Step 4 popup 检测 (nonItemLabels + ANR 文本语义兜底:733-758)
  - 后处理顺序: V2 降级(475) → V1 去重(476) → V5 搜索框排除(477)
- **Python**: tools/local_vision/server.py — /v1/analyze (JPEG) + /v1/analyze_raw (RGBA)
  - `_preprocess` (129): crop top/bottom 6.25% + resize 720px
  - `_remap_coords` (160): 映射回**收到的图**空间 (非全屏!)
  - YOLO deki-yolo (android_ui_detection_yolov8) + RapidOCR 全图 1 次 DBNet (305)
- **数据流**: screencap → C# ImageResizer.ProcessRaw (crop+resize+JPEG) → POST /v1/analyze → evidence → 4 步映射 → PageAnalysisDto → PageAnalyzer.MapItem → MenuItem (ExpectedAction 派生)
- **关键常量**: SameRowThreshold=0.03 (V1), SubtitleRowThreshold=0.035 (V2), TopBarYThreshold=0.10 (V5), Level1MaxY=0.08

## L2 Item 质量维度

- **类型正确性**: label-mapping.json (tools/local_vision/label-mapping.json) 无 "text" 键 (text_block→text); "input"/"EditText"→input→MenuItemType.Text+ExpectedAction.Action (搜索框陷阱)
- **坐标空间 (重要!)**: item 坐标是 **C# 发送图空间** (crop 6.25% top/bottom 后), 不是全屏空间。
  - 全屏转换: `y_full = y_crop * 0.875 + 0.0625`, `x_full = x_crop`
  - 反变换只在 InterceptionHandler.BuildYoloBboxes (691) 对 YoloBboxes 做了, item 坐标没有 → 点击误差 `err_px = 240*y - 120`
  - 服务器二次 crop 使真实屏幕底部 6.1% 不可见
  - 阈值 (0.03/0.035/0.10) 都在 crop 空间校准, 分析时先换算真实空间再判断
- **文本质量**: OCR 常见粘连/错字 ('Notiticationnistory,conversations', 'Charqed', '8.0GBtotal'), 空格变体 ("Bluetooth, pairing" vs "Bluetooth,pairing") — V4 NormalizeTextForIdentity (555) 只用于 identity, display 保持原文
- **去重**: V1 同排 (|dy|<0.03 + norm 文本包含) 合并; V2 副标题降级 (dy∈[0,0.035) menu_item→text, 只降非空文本)
- **滚动**: 视觉端 isEndOfList/hasScroll 恒 true/false — 别用来判断到底; 引擎 ROI 快照差分才是 end 检测
  - 引擎 end 信号 (trace action): 生产 ROI 路径 `scroll_roi_end_reached`/`scroll_roi_content_guard` (InterceptionHandler.cs:577/538/589); legacy seen-set 路径 `scroll_no_new_elements_end_reached` (:643)
  - ⚠️ ScenarioCompletionVerifier.cs:124 只认 legacy 名 → 生产 endProven 恒 false, 需 verifier 补 ROI 信号名
- **expectedAction**: PageAnalyzer → ElementTypeMapper.ToExpectedAction (text→None, menu_item→Navigate, input→Action)

## L3 诊断方法论

1. **analysis.jsonl 取证**: item_quality_check.py (endOfList/type 分布/搜索框/副标题 dy/同排重复/空文本)
2. **截图验证**: coord_validation.py (click→pre/post frame 关联) + RapidOCR 真实截图提取行 y, 与 analysis y 做空间换算验证 (crop↔real)
3. **点击验证**: safety.click 记录 (trace.jsonl) → targetValue; page_transition 记录验证实际落点; 用 "post frame 页面 ≠ 目标行页面" 判定真错位 vs 幻影
4. **回归对比**: 跨 run 同页 item dy 分布 (副标题 offset 约真实 0.031-0.034)
5. **注意**: click 无坐标 trace — 点击坐标 = analysis item (x,y), 落点需用截图 OCR 反推
