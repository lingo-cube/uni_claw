# FSV-001 附加分析（WI-5b；基于 frames/*.json 明细）

- 聚合报告：`fsv001-eed2e12a65a2.json`
- 帧数：39（质量评分帧 = 39 − 3 rejected）

## 1. A（integration）vs baseline yolo[] 差异 —— rescue 增益

方法：同帧同输入；A 臂 yolo[] = baseline yolo[] ⊕ fs_ 救援元素（id 前缀 `fs_`，D2 确定性规则：IoU<rescueIouMax 且 conf≥阈值追加）。rescue 元素对 GT 的命中 = class-agnostic IoU≥0.5 配对。

| frame | stratum | baseline yolo | A yolo | rescue 增 | rescue 类别 | rescue→GT 命中 / 配对 |
|---|---|---|---|---|---|---|
| 058f62426c1f | settings | 21 | 23 | 2 | switch×1、text_block×1 | 0 / 2 |
| 16068bc9a851 | list | 5 | 7 | 2 | text_block×2 | 1 / 2 |
| 1e572c8f5092 | dense | 20 | 25 | 5 | text_block×5 | 0 / 5 |
| 206556c78e8e | sidebar | 15 | 18 | 3 | text_block×2、switch×1 | 0 / 3 |
| 29f34bdb9dac | settings | 7 | 7 | 0 | — | 0 / 0 |
| 3682353fffbf | list | 26 | 27 | 1 | text_block×1 | 0 / 1 |
| 3a28cc28b0b5 | settings | 22 | 24 | 2 | text_block×2 | 0 / 2 |
| 4909995071e6 | dialog | 16 | 16 | 0 | — | 0 / 0 |
| 4a1f3e1d4421 | dense | 21 | 27 | 6 | text_block×5、switch×1 | 0 / 6 |
| 4e490bf99988 | list | 32 | 36 | 4 | text_block×4 | 0 / 4 |
| 518f41240dd0 | text-heavy | 15 | 23 | 8 | text_block×8 | 1 / 8 |
| 5ef64e4a384a | scrollable | 25 | 27 | 2 | text_block×2 | 0 / 2 |
| 6ea31e4ba9be | icon-heavy | 30 | 46 | 16 | text_block×12、tab×2、button×2 | 1 / 16 |
| 731aa1393639 | text-heavy | 14 | 21 | 7 | text_block×7 | 2 / 7 |
| 7b3f715379b0 | dialog | 9 | 11 | 2 | text_block×1、list_item×1 | 0 / 2 |
| 7e41f85e06e4 | list | 25 | 27 | 2 | text_block×2 | 0 / 2 |
| 82565b48ca54 | sidebar | 19 | 23 | 4 | text_block×3、switch×1 | 0 / 4 |
| 8576385f9b04 | sidebar | 19 | 24 | 5 | text_block×4、switch×1 | 0 / 5 |
| 882f15432eca | icon-heavy | 27 | 30 | 3 | button×3 | 0 / 3 |
| 8db380e98151 | settings | 7 | 7 | 0 | — | 0 / 0 |
| 9297d23b7c9f | dialog | 18 | 19 | 1 | text_block×1 | 0 / 1 |
| 9591de5f37de | scrollable | 32 | 32 | 0 | — | 0 / 0 |
| 9ee712f0f817 | icon-heavy | 24 | 27 | 3 | button×3 | 0 / 3 |
| a5d983aa849b | list | 29 | 33 | 4 | text_block×4 | 0 / 4 |
| a632ca5c963b | dense | 27 | 31 | 4 | text_block×4 | 0 / 4 |
| a86c6501da91 | scrollable | 33 | 35 | 2 | text_block×2 | 0 / 2 |
| acea3e4e4839 | text-heavy | 6 | 6 | 0 | — | 0 / 0 |
| afff0f4a8557 | settings | 20 | 23 | 3 | text_block×3 | 0 / 3 |
| bcc3cf830444 | settings | 12 | 15 | 3 | text_block×3 | 0 / 3 |
| be36cfe40647 | settings | 21 | 23 | 2 | switch×1、text_block×1 | 0 / 2 |
| bf0eca5f9851 | settings | 22 | 24 | 2 | text_block×2 | 0 / 2 |
| c1667d8b209e | list | 27 | 29 | 2 | text_block×2 | 0 / 2 |
| c28ad3c4e752 | settings | 27 | 30 | 3 | text_block×2、switch×1 | 1 / 3 |
| c8b2e65f2451 | list | 11 | 12 | 1 | text_block×1 | 0 / 1 |
| d2a5c3eca70a | icon-heavy | 32 | 36 | 4 | text_block×4 | 0 / 4 |
| f043962c67eb | settings | 29 | 30 | 1 | text_block×1 | 0 / 1 |

合计：rescue 元素 109，GT 命中 6（配对基数 109，命中率 5.5%）。命中 = 该元素与 GT elements 存在 IoU≥0.5 配对——Test A 增量元素确有对应 GT 实体（漏检救援有效）的直接证据。

rescue 类别分布（全帧合计）：

- text_block: 91
- button: 8
- switch: 7
- tab: 2
- list_item: 1

## 2. B1（replacement）vs baseline 元素级差异

方法：同帧下 B1 独有检出 = B1 yolo[] 中与 baseline 全部 yolo 无 IoU≥0.5 配对的元素；baseline 独有同理。各自对 GT 命中率（class-agnostic IoU≥0.5）对比——检出的『另一臂看不到』的元素是否真实。

| 集合 | 元素总数 | GT 命中 | 命中率 |
|---|---|---|---|
| B1 独有检出 | 443 | 39 | 8.8% |
| baseline 独有检出 | 295 | 63 | 21.4% |

B1 独有检出出现的帧（36）：058f62426c1f, 16068bc9a851, 1e572c8f5092, 206556c78e8e, 29f34bdb9dac, 3682353fffbf, 3a28cc28b0b5, 4909995071e6, 4a1f3e1d4421, 4e490bf99988, 518f41240dd0, 5ef64e4a384a, 6ea31e4ba9be, 731aa1393639, 7b3f715379b0, 7e41f85e06e4, 82565b48ca54, 8576385f9b04, 882f15432eca, 8db380e98151, 9297d23b7c9f, 9591de5f37de, 9ee712f0f817, a5d983aa849b, a632ca5c963b, a86c6501da91, acea3e4e4839, afff0f4a8557, bcc3cf830444, be36cfe40647, bf0eca5f9851, c1667d8b209e, c28ad3c4e752, c8b2e65f2451, d2a5c3eca70a, f043962c67eb

B1 独有检出的类别分布（screenparse 55→canonical 映射产物）：

- text_block: 317
- button: 41
- list_item: 40
- switch: 23
- icon: 12
- input: 4
- slider: 4
- tab: 2

baseline 独有检出的类别分布：

- text_block: 183
- icon: 47
- button: 27
- switch: 25
- input: 8
- toolbar: 3
- popup: 2

## 3. 每分层四臂 element recall 对比（主口径 IoU≥0.5）

| stratum | frames | baseline R | A R | B1 R | B2 R | strongest |
|---|---|---|---|---|---|---|
| dense | 3 | 0.162 | 0.162 | 0.054 | 0.054 | baseline |
| dialog | 3 | 0.156 | 0.156 | 0.125 | 0.125 | baseline |
| icon-heavy | 4 | 0.192 | 0.192 | 0.141 | 0.141 | baseline |
| list | 7 | 0.082 | 0.098 | 0.016 | 0.016 | integration |
| scrollable | 3 | 0.156 | 0.156 | 0.178 | 0.178 | replacement |
| settings | 10 | 0.198 | 0.207 | 0.135 | 0.135 | integration |
| sidebar | 3 | 0.100 | 0.100 | 0.133 | 0.133 | replacement |
| text-heavy | 3 | 0.172 | 0.276 | 0.448 | 0.448 | replacement |

臂间明显分化（≥15pp）的分层：

- text-heavy: 臂间 recall 差距 28%（best=replacement）

（ScreenParser 强场景 = B1/B2 recall ≥ baseline；弱场景反之。rejected 帧不入 stratum。）

## 4. Grounding miss 原因按臂分解

| arm | hitRate | miss 总 | no-text-match | contain-fail | no-box | off-target |
|---|---|---|---|---|---|---|
| baseline | 0.370 | 29 | 18 | 0 | 6 | 5 |
| integration | 0.370 | 29 | 18 | 0 | 6 | 5 |
| replacement | 0.435 | 26 | 18 | 0 | 0 | 8 |
| ocr-off | 0.065 | 43 | 35 | 0 | 0 | 8 |

no-text-match 占比 = OCR 未命中 query 文本的 miss——B2（OCR 关闭）该值即文字能力归零的直接出口（candidates 亦无文字，grounding 无文本锚）。

## 5. 延迟分解（跨帧 pooled；含 rejected 帧，latency 保留）

| arm | wall P50 | wall P95 | yolo P50 | ocr P50 | screenparse P50 | screenparse P95 | fusion P50 |
|---|---|---|---|---|---|---|---|
| baseline | 513.4 | 787.5 | 366.3 | 0.0 | — | — | 63.4 |
| integration | 1030.3 | 1402.6 | 432.1 | 0.0 | 427.8 | 452.0 | 87.3 |
| replacement | 827.9 | 1220.8 | 651.4 | 0.0 | — | — | 75.7 |
| ocr-off | 443.0 | 453.0 | 441.2 | 0.0 | — | — | 1.8 |

screenparse 段仅 integration（A）臂存在（D2 独立分段打点；replacement 臂 screenparse 推理计入 yolo;dur 段——与 baseline 同段语义可对拍）；B2 进程内 runner 无 HTTP 序列化/GC 段。

## 已知限制（如实记录）

- rescue/B1-only 的『GT 命中』用 class-agnostic IoU≥0.5 配对（几何命中优先），类型判对与否另见聚合报告 typeAccuracy/混淆。
- rejected 帧（5df0424f787c / 9f5d4e04c7cf / d8b2a487cdfa）不入质量评分及本分析的元素/grounding 表；latency/RSS 保留。
- B1 yolo[] id 为 det_{n}（replacement 语义），baseline 亦为 det_{n}——差异表以几何（IoU）配对，不依赖 id 命名。
