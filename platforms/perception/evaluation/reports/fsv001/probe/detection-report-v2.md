# 检测准确性分析 v2（ScreenParser vs baseline）

样本帧：39（39 帧均含 3 臂响应）

| 度量 | baseline | A(+rescue) | B1 |
|---|---|---|---|
| Σ yolo 检测框 | 813 | 930 | 1001 |
| rescue 增量（A） | — | 117 | — |
| 标签无关 IoU≥0.5：B1 覆盖 baseline | — | — | 444/813 (0.546) |
| 标签无关 IoU≥0.5：baseline 覆盖 B1 | 444 | — | — |
| 同 label 中心包含：B1 覆盖 baseline（宽松） | — | — | 570/813 (0.701) |
| 同 label 中心包含：baseline 覆盖 B1（宽松） | 570 | — | — |
| 宽松匹配标签一致率 | — | — | 570/570 (1.000) |

rescue 类分布: {"switch": 7, "text_block": 98, "button": 9, "tab": 2, "list_item": 1}
rescue 置信度 mean=0.527 min=0.353 max=0.922

| frame | |b| |A| |rescue| |B1| |
| 058f62426c1f | 21 | 23 | 2 | 25 |
| 16068bc9a851 | 5 | 7 | 2 | 8 |
| 1e572c8f5092 | 20 | 25 | 5 | 28 |
| 206556c78e8e | 15 | 18 | 3 | 20 |
| 29f34bdb9dac | 7 | 7 | 0 | 13 |
| 3682353fffbf | 26 | 27 | 1 | 36 |
| 3a28cc28b0b5 | 22 | 24 | 2 | 33 |
| 4909995071e6 | 16 | 16 | 0 | 2 |
| 4a1f3e1d4421 | 21 | 27 | 6 | 28 |
| 4e490bf99988 | 32 | 36 | 4 | 35 |
| 518f41240dd0 | 15 | 23 | 8 | 21 |
| 5df0424f787c | 19 | 20 | 1 | 13 |
| 5ef64e4a384a | 25 | 27 | 2 | 31 |
| 6ea31e4ba9be | 30 | 46 | 16 | 51 |
| 731aa1393639 | 14 | 21 | 7 | 20 |
| 7b3f715379b0 | 9 | 11 | 2 | 12 |
| 7e41f85e06e4 | 25 | 27 | 2 | 28 |
| 82565b48ca54 | 19 | 23 | 4 | 34 |
| 8576385f9b04 | 19 | 24 | 5 | 21 |
| 882f15432eca | 27 | 30 | 3 | 32 |
| 8db380e98151 | 7 | 7 | 0 | 13 |
| 9297d23b7c9f | 18 | 19 | 1 | 10 |
| 9591de5f37de | 32 | 32 | 0 | 48 |
| 9ee712f0f817 | 24 | 27 | 3 | 31 |
| 9f5d4e04c7cf | 14 | 21 | 7 | 20 |
| a5d983aa849b | 29 | 33 | 4 | 31 |
| a632ca5c963b | 27 | 31 | 4 | 31 |
| a86c6501da91 | 33 | 35 | 2 | 34 |
| acea3e4e4839 | 6 | 6 | 0 | 11 |
| afff0f4a8557 | 20 | 23 | 3 | 26 |
| bcc3cf830444 | 12 | 15 | 3 | 16 |
| be36cfe40647 | 21 | 23 | 2 | 25 |
| bf0eca5f9851 | 22 | 24 | 2 | 33 |
| c1667d8b209e | 27 | 29 | 2 | 28 |
| c28ad3c4e752 | 27 | 30 | 3 | 27 |
| c8b2e65f2451 | 11 | 12 | 1 | 15 |
| d2a5c3eca70a | 32 | 36 | 4 | 35 |
| d8b2a487cdfa | 35 | 35 | 0 | 52 |
| f043962c67eb | 29 | 30 | 1 | 24 |

### settings-home：类计数 vs GT {"text_block": 17, "icon": 13, "list_item": 3}
| arm | text_block | icon | list_item | 总 |
|---|---|---|---|---|
| baseline | 18 | 7 | 0 | 26 |
| integration | 20 | 7 | 0 | 28 |
| replacement | 18 | 1 | 0 | 22 |
| ocr-off | 18 | 1 | 0 | 22 |

### synthetic-1：4 GT 元素（列表 bounds 旧格式）
| arm | yolo | tp | fp | fn | P | R | F1 |
|---|---|---|---|---|---|---|---|
| baseline | 2 | 0 | 2 | 4 | 0.000 | 0.000 | — |
| integration | 2 | 0 | 2 | 4 | 0.000 | 0.000 | — |
| replacement | 0 | 0 | 0 | 4 | — | 0.000 | — |
| ocr-off | 0 | 0 | 0 | 4 | — | 0.000 | — |

### synthetic-2：2 GT 元素（列表 bounds 旧格式）
| arm | yolo | tp | fp | fn | P | R | F1 |
|---|---|---|---|---|---|---|---|
| baseline | 0 | 0 | 0 | 2 | — | 0.000 | — |
| integration | 0 | 0 | 0 | 2 | — | 0.000 | — |
| replacement | 0 | 0 | 0 | 2 | — | 0.000 | — |
| ocr-off | 0 | 0 | 0 | 2 | — | 0.000 | — |

## 补充：calibrated GT（36 帧；WI-4+leader 校正版；3 帧 rejected-stale-dump 已跳过）元素级/质量
数据源：/tmp/fsv001-detect/fsv001-87cc08fde9df.json（39 帧全跑，runs=1；质量=首响应）

| 指标 | baseline | A(+rescue) | B1 | B2 |
|---|---|---|---|---|
| element P | 0.0913 | 0.0855 | 0.0633 | 0.0633 |
| element R | 0.1608 | 0.1726 | 0.1371 | 0.1371 |
| element F1 | 0.1164 | 0.1143 | 0.0866 | 0.0866 |
| bboxIoU (matched) | 0.6969 | 0.7061 | 0.8292 | 0.8292 |
| typeAcc | 0.6700 | 0.6792 | 0.7600 | 0.7600 |
| text exact | 0.5321 | 0.5321 | 0.5321 | 0.0000 |
| cand F1 | 0.0923 | 0.0904 | 0.0385 | 0.0336 |
| grounding hitRate | 0.3696 | 0.3696 | 0.4348 | 0.0652 |

latency pooled (n=39): baseline wall P50/P95=559/884ms；A=1037/1430；B1=831/1218；B2=447/464
B2 vs B1 yolo 抽查：39/39 equal（WI-3 保证全集复验通过）

## 结论（ScreenParser 能否提升检测准确性 —— 36 帧 calibrated GT + 跨检测器 + legacy counts 三面）
1. A（integration/rescue）：117 救援框（98 text_block，conf 均值 0.527）仅 +5 tp；
   element R +0.012 / P −0.006 / F1 −0.002 ≈ 无效提升。
2. B1（replacement）：整体下降——F1 0.087 vs 0.116；对 baseline 已检出召回保持
   仅 55–70%（IoU≥0.5 / 中心包含两口径）；settings-home icon 检测 7→1（OOD 最差类）。
3. ScreenParser 正向面：可匹配框质量更高（IoU 0.83 vs 0.70；typeAcc 0.76 vs 0.67；
   grounding 0.435 vs 0.370）——"框对了的更准"，但漏检/错检更多，净效果负。
4. 结论：当前权重+55→canonical 映射+救援配置 **不提升检测准确性**。要达成
   "提升"需改配置/方式：rescue 限定 interactive 类（button/switch/input）并提高
   conf 阈值；或对 ScreenParser fine-tune 消 OOD（icon 等）；不要 replacement。
