# FSV-001 失败点探针：arm 级感知信号对比（无 GT；信号级非评分级）

帧：8 ｜ 臂：baseline, integration, replacement, ocr-off｜ runs=5/warmup=1｜ 出处：uni-agent real-world-failure-distribution（K 25% / F 8.3% / G 8.3% / scroll continuity）

## K 类（post-action/post-scroll 页面与 toggle 行解析）

| frame | arm | yolo switch/toggle | text 关联 switch 行 | Stay awake 锚 | Stay awake token |
|---|---|---|---|---|---|
| devopts-top | baseline | 2 | 0 | det_11 | ✓ |
| devopts-top | integration | 2 | 0 | det_11 | ✓ |
| devopts-top | replacement | 2 | 0 | det_26 | ✓ |
| devopts-top | ocr-off | 2 | 0 | 无包含框 | ✗ |
| devopts-toggle-off | baseline | 2 | 0 | det_11 | ✓ |
| devopts-toggle-off | integration | 3 | 0 | det_11 | ✓ |
| devopts-toggle-off | replacement | 2 | 0 | det_26 | ✓ |
| devopts-toggle-off | ocr-off | 2 | 0 | 无包含框 | ✗ |
| devopts-mid | baseline | 2 | 0 | 无包含框 | ✗ |
| devopts-mid | integration | 2 | 0 | 无包含框 | ✗ |
| devopts-mid | replacement | 2 | 0 | 无包含框 | ✗ |
| devopts-mid | ocr-off | 2 | 0 | 无包含框 | ✗ |
| apps-top | baseline | 0 | 0 | 无包含框 | ✗ |
| apps-top | integration | 0 | 0 | 无包含框 | ✗ |
| apps-top | replacement | 0 | 0 | 无包含框 | ✗ |
| apps-top | ocr-off | 0 | 0 | 无包含框 | ✗ |
| apps-mid | baseline | 0 | 0 | 无包含框 | ✗ |
| apps-mid | integration | 0 | 0 | 无包含框 | ✗ |
| apps-mid | replacement | 0 | 0 | 无包含框 | ✗ |
| apps-mid | ocr-off | 0 | 0 | 无包含框 | ✗ |

## F/G 类（SettingsRoot 导航候选 / 行文本可读性）

| frame | arm | ocr tokens | 带文本候选行数 | 前 6 行 (y1, text) |
|---|---|---|---|---|
| settings-home-top | baseline | 14 | 7 | 642:Q Search settings; 846:Network & internet; 1078:Connected devices; 1312:Apps; 1540:Notifications; 1772:Battery |
| settings-home-top | integration | 14 | 7 | 642:Q Search settings; 846:Network & internet; 1078:Connected devices; 1298:Apps; 1540:Notifications; 1772:Battery |
| settings-home-top | replacement | 14 | 11 | 418:Settings; 636:Q Search settings; 834:Network & internet; 904:Network & internet; 1065:Connected devices; 1298:Apps |
| settings-home-top | ocr-off | 0 | 0 |  |
| network-internet-page | baseline | 17 | 6 | 664:Internet; 870:SIMs; 1074:Airplane mode; 1228:Hotspot & tetherin; 1437:Data Saver; 1642:VPN |
| network-internet-page | integration | 17 | 6 | 664:Internet; 870:SIMs; 1074:Airplane mode; 1228:Hotspot & tetherin; 1437:Data Saver; 1642:VPN |
| network-internet-page | replacement | 17 | 13 | 432:Network & internet; 652:Internet; 652:Internet; 858:SIMs; 927:T-Mobile; 1064:Airplane mode |
| network-internet-page | ocr-off | 0 | 0 |  |
| bluetooth-page | baseline | 8 | 6 | 448:Connected devices; 664:Pair new device; 834:Saved devices; 952:See all; 1107:Connection prefere; 1425:Visible as "Androi |
| bluetooth-page | integration | 8 | 6 | 448:Connected devices; 664:Pair new device; 834:Saved devices; 952:See all; 1107:Connection prefere; 1425:Visible as "Androi |
| bluetooth-page | replacement | 8 | 6 | 430:Connected devices; 652:Pair new device; 828:Saved devices; 944:See all; 1096:Connection prefere; 1416:Visible as "Androi |
| bluetooth-page | ocr-off | 0 | 0 |  |

## Scroll continuity（相邻滚动帧共享行——EBD 归一化 / A7 变体的根）

| 帧对 | arm | 帧A行 | 帧B行 | 共享行 | 共享率 |
|---|---|---|---|---|---|
| devopts-top→devopts-mid | baseline | 9 | 10 | 1 | 0.10 |
| devopts-top→devopts-mid | integration | 9 | 10 | 1 | 0.10 |
| devopts-top→devopts-mid | replacement | 9 | 11 | 2 | 0.18 |
| devopts-top→devopts-mid | ocr-off | 0 | 0 | 0 | 0.00 |
| apps-top→apps-mid | baseline | 10 | 11 | 2 | 0.18 |
| apps-top→apps-mid | integration | 10 | 11 | 2 | 0.18 |
| apps-top→apps-mid | replacement | 9 | 10 | 2 | 0.20 |
| apps-top→apps-mid | ocr-off | 0 | 0 | 0 | 0.00 |

## Latency / RSS（跨帧 pooled）

| arm | wall n | wall P50 | wall P95 | RSS max (MB) |
|---|---|---|---|---|
| baseline | 40 | 585.3 | 795.3 | 910.5 |
| integration | 40 | 1060.4 | 1259.7 | 1280.2 |
| replacement | 40 | 818.5 | 1040.6 | 1353.6 |
| ocr-off | 40 | 444.2 | 455.8 | 1039.4 |

## 验证注记（2026-09-12，对上述信号的复核）

1. **devopts-mid 的 Stay-awake 锚 ✗ = 行滚出视野，非 OCR 漏读**：该帧 OCR 19 tokens
   （"Running services"、"Picture color mode"、"WebView implementation"、"
   Automatic system updates"…），"Stay awake" 不在屏内——滚动后目标行离开 viewport。
   四臂同况：fast perception 无跨帧记忆，滚动中无法重定位目标行。ScreenParser 同缺。
2. **replacement 重复行实锤（F 类非唯一候选的机制面）**：
   - settings-home-top："Network & internet" 同时被 screenparser 检测为
     `text_block det_2 (190,904,624,956)` 与 `menu_item det_21 (190,834,1012,904)`
     ——同一行两个不同 canonical 类 → fusion 各留一条 → 候选重复。
   - network-page："Internet" 同时为 `list_item det_14 (190,652,1020,720)` 与
     `menu_item det_27 (189,652,1023,720)`——近乎同一框映射成两类的双检测。
   - baseline/integration 无此现象（行序干净）。
3. **switch 行 text association 短板是四臂共有，非臂间差异**：devopts-top 各臂
   2 个 switch 候选但 text 全空（candidates 层无文字关联）；运行时靠
   ocr+yolo 空间包含 join 仍可解析（Stay-awake 锚 ✓）。ScreenParser 不改善此面。
4. B2（无 OCR）在全部文字相关信号上结构性为 0——纯 replace 不成立，
   "detector replaced, OCR retained" 是唯一合法姿势（与 WI-3 结论一致）。
