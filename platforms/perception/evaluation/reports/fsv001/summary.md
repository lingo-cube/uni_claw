# FSV-001 四臂 A/B 评测摘要

- runId: fsv001-eed2e12a65a2.json
- 起跑时间: 2026-09-12T19:01:18+0800（仅记录，不参与确定性）
- matcher: matcher-greedy-v1 (class 相等 + IoU ≥ 0.5)
- grounding hit margin: ±8px
- 帧数: 39；臂: baseline, integration, replacement, ocr-off

## 质量指标（arm × 指标 pooled；N/S = NOT_SCORABLE，— = 无值）

| arm | element P | element R | element F1 | elem R(center) | elem P(center) | elem F1(center) | typeAcc | text exact | text CER | cand F1 | grounding hit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| baseline | 0.091 | 0.161 | 0.116 | 0.262 | 0.149 | 0.190 | 0.670 | 0.532 | 0.202 | 0.092 | 0.370 |
| integration | 0.086 | 0.173 | 0.114 | 0.262 | 0.130 | 0.174 | 0.679 | 0.532 | 0.202 | 0.090 | 0.370 |
| replacement | 0.063 | 0.137 | 0.087 | 0.428 | 0.198 | 0.270 | 0.760 | 0.532 | 0.202 | 0.038 | 0.435 |
| ocr-off | 0.063 | 0.137 | 0.087 | 0.428 | 0.198 | 0.270 | 0.760 | 0.000 | — | 0.034 | 0.065 |

- element 主口径 = matcher-greedy-v1（class 相等 + IoU ≥ 0.5）；elem R/P/F1(center) = 次级行容忍口径（matchMode=center：class 相等 ∧ 预测中心 ∈ GT 框，无 margin——YOLO 文字 extent vs GT 整行 extent 的框语义差异，D6r 几何归因）。

## Latency（跨帧 pooled；wall + 分段 P50/P95；n≥10 才报 p50/p95）

| arm | wall n | wall P50 | wall P95 | yolo P50 | yolo P95 | ocr P50 | ocr P95 | fusion P50 | fusion P95 | screenparse P50 | screenparse P95 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| baseline | 195 | 513.4 | 787.5 | 366.3 | 532.9 | 0.0 | 0.0 | 63.4 | 326.3 | — | — |
| integration | 195 | 1030.3 | 1402.6 | 432.1 | 619.0 | 0.0 | 0.0 | 87.3 | 466.7 | 427.8 | 452.0 |
| replacement | 195 | 827.9 | 1220.8 | 651.4 | 776.7 | 0.0 | 0.0 | 75.7 | 443.2 | — | — |
| ocr-off | 195 | 443.0 | 453.0 | 441.2 | 450.2 | 0.0 | 0.0 | 1.8 | 3.1 | — | — |

## RSS（推理期采样 max）

| arm | max RSS (MB) | 样本数 | scope |
|---|---|---|---|
| baseline | 860.2 | 1433 | uvicorn server proc |
| integration | 1233.4 | 2886 | uvicorn server proc |
| replacement | 1415.7 | 2350 | uvicorn server proc |
| ocr-off | 1051.3 | 1381 | self (in-process) |

## Grounding miss 原因分布

| arm | reason | 计数 |
|---|---|---|
| baseline | no-box | 6 |
| baseline | no-text-match | 18 |
| baseline | off-target | 5 |
| integration | no-box | 6 |
| integration | no-text-match | 18 |
| integration | off-target | 5 |
| replacement | no-text-match | 18 |
| replacement | off-target | 8 |
| ocr-off | no-text-match | 35 |
| ocr-off | off-target | 8 |

## 混淆（type accuracy 主口径：class-agnostic 贪心 IoU≥0.5 配对，pred→gt 计数）

### baseline — typeAcc=0.670（配对 100 pairs）
| pred \ gt | button | icon | list_item | slider | switch | text_block |
|---|---|---|---|---|---|---|
| button | 6 | 0 | 11 | 0 | 13 | 0 |
| icon | 0 | 12 | 2 | 0 | 0 | 1 |
| input | 0 | 0 | 2 | 1 | 0 | 1 |
| switch | 0 | 0 | 0 | 0 | 29 | 0 |
| text_block | 0 | 1 | 1 | 0 | 0 | 20 |

混淆对 top-10（predLabel→gtClass）:

| predLabel | gtClass | count |
|---|---|---|
| switch | switch | 29 |
| text_block | text_block | 20 |
| button | switch | 13 |
| icon | icon | 12 |
| button | list_item | 11 |
| button | button | 6 |
| icon | list_item | 2 |
| input | list_item | 2 |
| icon | text_block | 1 |
| input | slider | 1 |

### integration — typeAcc=0.679（配对 106 pairs）
| pred \ gt | button | icon | list_item | slider | switch | text_block |
|---|---|---|---|---|---|---|
| button | 6 | 0 | 11 | 0 | 13 | 0 |
| icon | 0 | 12 | 2 | 0 | 0 | 1 |
| input | 0 | 0 | 2 | 1 | 0 | 1 |
| switch | 0 | 0 | 0 | 0 | 29 | 0 |
| text_block | 0 | 2 | 1 | 0 | 0 | 25 |

混淆对 top-10（predLabel→gtClass）:

| predLabel | gtClass | count |
|---|---|---|
| switch | switch | 29 |
| text_block | text_block | 25 |
| button | switch | 13 |
| icon | icon | 12 |
| button | list_item | 11 |
| button | button | 6 |
| icon | list_item | 2 |
| input | list_item | 2 |
| text_block | icon | 2 |
| icon | text_block | 1 |

### replacement — typeAcc=0.760（配对 75 pairs）
| pred \ gt | button | icon | list_item | slider | switch | text_block |
|---|---|---|---|---|---|---|
| button | 2 | 9 | 0 | 0 | 0 | 0 |
| icon | 0 | 3 | 0 | 0 | 0 | 0 |
| input | 0 | 0 | 5 | 1 | 0 | 0 |
| list_item | 0 | 0 | 3 | 0 | 0 | 1 |
| switch | 0 | 0 | 0 | 0 | 7 | 0 |
| text_block | 0 | 1 | 1 | 0 | 0 | 42 |

混淆对 top-10（predLabel→gtClass）:

| predLabel | gtClass | count |
|---|---|---|
| text_block | text_block | 42 |
| button | icon | 9 |
| switch | switch | 7 |
| input | list_item | 5 |
| icon | icon | 3 |
| list_item | list_item | 3 |
| button | button | 2 |
| input | slider | 1 |
| list_item | text_block | 1 |
| text_block | icon | 1 |

### ocr-off — typeAcc=0.760（配对 75 pairs）
| pred \ gt | button | icon | list_item | slider | switch | text_block |
|---|---|---|---|---|---|---|
| button | 2 | 9 | 0 | 0 | 0 | 0 |
| icon | 0 | 3 | 0 | 0 | 0 | 0 |
| input | 0 | 0 | 5 | 1 | 0 | 0 |
| list_item | 0 | 0 | 3 | 0 | 0 | 1 |
| switch | 0 | 0 | 0 | 0 | 7 | 0 |
| text_block | 0 | 1 | 1 | 0 | 0 | 42 |

混淆对 top-10（predLabel→gtClass）:

| predLabel | gtClass | count |
|---|---|---|
| text_block | text_block | 42 |
| button | icon | 9 |
| switch | switch | 7 |
| input | list_item | 5 |
| icon | icon | 3 |
| list_item | list_item | 3 |
| button | button | 2 |
| input | slider | 1 |
| list_item | text_block | 1 |
| text_block | icon | 1 |

## per-stratum 分解（meta.stratum）

### baseline

| stratum | frames | elem P | elem R | elem F1 | text exact | ground hit | wall P50 |
|---|---|---|---|---|---|---|---|
| dense | 3 | 0.088 | 0.162 | 0.114 | 0.585 | 0.500 | 563.4 |
| dialog | 3 | 0.116 | 0.156 | 0.133 | 0.724 | 0.667 | 544.4 |
| icon-heavy | 4 | 0.133 | 0.192 | 0.157 | 0.175 | 0.250 | 698.6 |
| list | 7 | 0.032 | 0.082 | 0.046 | 0.534 | 0.091 | 452.6 |
| scrollable | 3 | 0.078 | 0.156 | 0.104 | 0.582 | 0.167 | 702.9 |
| settings | 10 | 0.117 | 0.198 | 0.147 | 0.633 | 0.500 | 485.3 |
| sidebar | 3 | 0.057 | 0.100 | 0.072 | 0.551 | 1.000 | 446.0 |
| text-heavy | 3 | 0.143 | 0.172 | 0.156 | 0.714 | 0.333 | 431.1 |

### integration

| stratum | frames | elem P | elem R | elem F1 | text exact | ground hit | wall P50 |
|---|---|---|---|---|---|---|---|
| dense | 3 | 0.072 | 0.162 | 0.100 | 0.585 | 0.500 | 1075.7 |
| dialog | 3 | 0.109 | 0.156 | 0.128 | 0.724 | 0.667 | 1137.5 |
| icon-heavy | 4 | 0.108 | 0.192 | 0.138 | 0.175 | 0.250 | 1315.1 |
| list | 7 | 0.035 | 0.098 | 0.052 | 0.534 | 0.091 | 978.5 |
| scrollable | 3 | 0.074 | 0.156 | 0.101 | 0.582 | 0.167 | 1268.0 |
| settings | 10 | 0.112 | 0.207 | 0.145 | 0.633 | 0.500 | 979.7 |
| sidebar | 3 | 0.046 | 0.100 | 0.063 | 0.551 | 1.000 | 976.5 |
| text-heavy | 3 | 0.160 | 0.276 | 0.203 | 0.714 | 0.333 | 954.0 |

### replacement

| stratum | frames | elem P | elem R | elem F1 | text exact | ground hit | wall P50 |
|---|---|---|---|---|---|---|---|
| dense | 3 | 0.023 | 0.054 | 0.032 | 0.585 | 0.500 | 962.9 |
| dialog | 3 | 0.167 | 0.125 | 0.143 | 0.724 | 0.667 | 974.7 |
| icon-heavy | 4 | 0.074 | 0.141 | 0.097 | 0.175 | 0.250 | 949.5 |
| list | 7 | 0.005 | 0.016 | 0.008 | 0.534 | 0.364 | 771.9 |
| scrollable | 3 | 0.071 | 0.178 | 0.101 | 0.582 | 0.167 | 1073.0 |
| settings | 10 | 0.064 | 0.135 | 0.087 | 0.633 | 0.500 | 788.1 |
| sidebar | 3 | 0.053 | 0.133 | 0.076 | 0.551 | 1.000 | 805.2 |
| text-heavy | 3 | 0.250 | 0.448 | 0.321 | 0.714 | 0.333 | 772.9 |

### ocr-off

| stratum | frames | elem P | elem R | elem F1 | text exact | ground hit | wall P50 |
|---|---|---|---|---|---|---|---|
| dense | 3 | 0.023 | 0.054 | 0.032 | 0.000 | 0.000 | 442.9 |
| dialog | 3 | 0.167 | 0.125 | 0.143 | 0.000 | 0.000 | 444.9 |
| icon-heavy | 4 | 0.074 | 0.141 | 0.097 | 0.000 | 0.000 | 444.1 |
| list | 7 | 0.005 | 0.016 | 0.008 | 0.000 | 0.273 | 443.6 |
| scrollable | 3 | 0.071 | 0.178 | 0.101 | 0.000 | 0.000 | 442.4 |
| settings | 10 | 0.064 | 0.135 | 0.087 | 0.000 | 0.000 | 440.6 |
| sidebar | 3 | 0.053 | 0.133 | 0.076 | 0.000 | 0.000 | 443.4 |
| text-heavy | 3 | 0.250 | 0.448 | 0.321 | 0.000 | 0.000 | 444.0 |

## NOT_SCORABLE 帧计数（每臂；缺 GT 面 → 对应指标 null）

| arm | elements | text | candidates | grounding | scoredFrames | skippedForQuality |
|---|---|---|---|---|---|---|
| baseline | 0 | 0 | 0 | 1 | 36 | 3 |
| integration | 0 | 0 | 0 | 1 | 36 | 3 |
| replacement | 0 | 0 | 0 | 1 | 36 | 3 |
| ocr-off | 0 | 0 | 0 | 1 | 36 | 3 |

## skippedForQuality（rejected 帧：质量跳过、latency/RSS 保留）

reviewStatus 以 `rejected` 开头（D6r 排除帧，见 CALIBRATION-REPORT.md）：element/candidate/text/grounding 指标不参与评分；latency/RSS 正常包含。

- baseline: 3 帧 5df0424f787c(rejected-stale-dump), 9f5d4e04c7cf(rejected-stale-dump), d8b2a487cdfa(rejected-stale-dump)
- integration: 3 帧 5df0424f787c(rejected-stale-dump), 9f5d4e04c7cf(rejected-stale-dump), d8b2a487cdfa(rejected-stale-dump)
- replacement: 3 帧 5df0424f787c(rejected-stale-dump), 9f5d4e04c7cf(rejected-stale-dump), d8b2a487cdfa(rejected-stale-dump)
- ocr-off: 3 帧 5df0424f787c(rejected-stale-dump), 9f5d4e04c7cf(rejected-stale-dump), d8b2a487cdfa(rejected-stale-dump)

## B2 vs B1 yolo 抽查（同图同 device 必须同 yolo；WI-3 保证）

| frame | status | b1 | b2 |
|---|---|---|---|
| 058f62426c1f | equal | 25 | 25 |
| 16068bc9a851 | equal | 8 | 8 |
| 1e572c8f5092 | equal | 28 | 28 |
| 206556c78e8e | equal | 20 | 20 |
| 29f34bdb9dac | equal | 13 | 13 |
| 3682353fffbf | equal | 36 | 36 |
| 3a28cc28b0b5 | equal | 33 | 33 |
| 4909995071e6 | equal | 2 | 2 |
| 4a1f3e1d4421 | equal | 28 | 28 |
| 4e490bf99988 | equal | 35 | 35 |
| 518f41240dd0 | equal | 21 | 21 |
| 5df0424f787c | equal | 13 | 13 |
| 5ef64e4a384a | equal | 31 | 31 |
| 6ea31e4ba9be | equal | 51 | 51 |
| 731aa1393639 | equal | 20 | 20 |
| 7b3f715379b0 | equal | 12 | 12 |
| 7e41f85e06e4 | equal | 28 | 28 |
| 82565b48ca54 | equal | 34 | 34 |
| 8576385f9b04 | equal | 21 | 21 |
| 882f15432eca | equal | 32 | 32 |
| 8db380e98151 | equal | 13 | 13 |
| 9297d23b7c9f | equal | 10 | 10 |
| 9591de5f37de | equal | 48 | 48 |
| 9ee712f0f817 | equal | 31 | 31 |
| 9f5d4e04c7cf | equal | 20 | 20 |
| a5d983aa849b | equal | 31 | 31 |
| a632ca5c963b | equal | 31 | 31 |
| a86c6501da91 | equal | 34 | 34 |
| acea3e4e4839 | equal | 11 | 11 |
| afff0f4a8557 | equal | 26 | 26 |
| bcc3cf830444 | equal | 16 | 16 |
| be36cfe40647 | equal | 25 | 25 |
| bf0eca5f9851 | equal | 33 | 33 |
| c1667d8b209e | equal | 28 | 28 |
| c28ad3c4e752 | equal | 27 | 27 |
| c8b2e65f2451 | equal | 15 | 15 |
| d2a5c3eca70a | equal | 35 | 35 |
| d8b2a487cdfa | equal | 52 | 52 |
| f043962c67eb | equal | 24 | 24 |

## B2 已知限制（如实记录）

- B2（ocr-off）：进程内跑 _run_pipeline，无 HTTP 序列化/GC 段（serialize/gc 为 null）；该臂 latency = 管道 wall。
- click-state：state 感知超出当前 runtime 边界能力，resolver 只做目标框中心命中判定。
- typeAccuracy 主口径 = class-agnostic 贪心 IoU≥0.5 配对下的 label==gtClass 比例（WI-5b 修正 b——严格 class 相等配对下按构造恒 1.0，无信息量）；混淆对计数（predLabel→gtClass top-N）随表。
- 次级行容忍口径（elem P/R/F1(center)）：matchMode=center，class 相等 ∧ 预测中心 ∈ GT 框，无 margin——YOLO 文字 extent vs GT 整行 extent 的框语义差异（D6r 几何归因），与主口径并列。
- skippedForQuality：reviewStatus 以 rejected 开头（D6r 排除 帧）→ 质量指标跳过、latency/RSS 保留，报告显式列出。
