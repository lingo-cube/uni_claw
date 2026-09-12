# FSV-001 四臂 A/B 评测摘要

- runId: fsv001-7b1439bb8758.json
- 起跑时间: 2026-09-12T20:13:06+0800（仅记录，不参与确定性）
- matcher: matcher-greedy-v1 (class 相等 + IoU ≥ 0.5)
- grounding hit margin: ±8px
- 帧数: 39；臂: baseline, integration, replacement, ocr-off

## 质量指标（arm × 指标 pooled；N/S = NOT_SCORABLE，— = 无值）

| arm | element P | element R | element F1 | elem R(center) | elem P(center) | elem F1(center) | typeAcc | text exact | text CER | cand F1 | grounding hit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| baseline | 0.091 | 0.161 | 0.116 | 0.262 | 0.149 | 0.190 | 0.670 | 0.532 | 0.202 | 0.092 | 0.370 |
| integration | 0.089 | 0.182 | 0.120 | 0.267 | 0.131 | 0.176 | 0.691 | 0.532 | 0.202 | 0.090 | 0.370 |
| replacement | 0.086 | 0.180 | 0.116 | 0.435 | 0.207 | 0.281 | 0.750 | 0.532 | 0.202 | 0.063 | 0.370 |
| ocr-off | 0.086 | 0.180 | 0.116 | 0.435 | 0.207 | 0.281 | 0.750 | 0.000 | — | 0.092 | 0.022 |

- element 主口径 = matcher-greedy-v1（class 相等 + IoU ≥ 0.5）；elem R/P/F1(center) = 次级行容忍口径（matchMode=center：class 相等 ∧ 预测中心 ∈ GT 框，无 margin——YOLO 文字 extent vs GT 整行 extent 的框语义差异，D6r 几何归因）。

## Latency（跨帧 pooled；wall + 分段 P50/P95；n≥10 才报 p50/p95）

| arm | wall n | wall P50 | wall P95 | yolo P50 | yolo P95 | ocr P50 | ocr P95 | fusion P50 | fusion P95 | screenparse P50 | screenparse P95 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| baseline | 195 | 555.9 | 823.1 | 395.0 | 558.3 | 0.0 | 0.0 | 67.7 | 339.5 | — | — |
| integration | 195 | 1027.5 | 1367.9 | 437.1 | 614.7 | 0.0 | 0.0 | 92.6 | 477.4 | 383.8 | 403.1 |
| replacement | 195 | 896.1 | 1215.2 | 670.5 | 898.1 | 0.0 | 0.0 | 78.4 | 463.2 | — | — |
| ocr-off | 195 | 458.3 | 617.0 | 456.0 | 615.3 | 0.0 | 0.0 | 1.7 | 3.0 | — | — |

## RSS（推理期采样 max）

| arm | max RSS (MB) | 样本数 | scope |
|---|---|---|---|
| baseline | 861.3 | 1492 | uvicorn server proc |
| integration | 1252.9 | 2807 | uvicorn server proc |
| replacement | 1429.3 | 2220 | uvicorn server proc |
| ocr-off | 908.7 | 1330 | self (in-process) |

## Grounding miss 原因分布

| arm | reason | 计数 |
|---|---|---|
| baseline | no-box | 6 |
| baseline | no-text-match | 18 |
| baseline | off-target | 5 |
| integration | no-box | 6 |
| integration | no-text-match | 18 |
| integration | off-target | 5 |
| replacement | contain-fail | 1 |
| replacement | no-text-match | 18 |
| replacement | off-target | 10 |
| ocr-off | no-text-match | 35 |
| ocr-off | off-target | 10 |

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

### integration — typeAcc=0.691（配对 110 pairs）
| pred \ gt | button | icon | list_item | slider | switch | text_block |
|---|---|---|---|---|---|---|
| button | 6 | 0 | 11 | 0 | 13 | 0 |
| icon | 0 | 12 | 2 | 0 | 0 | 1 |
| input | 0 | 0 | 2 | 1 | 0 | 1 |
| switch | 0 | 0 | 0 | 0 | 29 | 0 |
| text_block | 0 | 2 | 1 | 0 | 0 | 29 |

混淆对 top-10（predLabel→gtClass）:

| predLabel | gtClass | count |
|---|---|---|
| switch | switch | 29 |
| text_block | text_block | 29 |
| button | switch | 13 |
| icon | icon | 12 |
| button | list_item | 11 |
| button | button | 6 |
| icon | list_item | 2 |
| input | list_item | 2 |
| text_block | icon | 2 |
| icon | text_block | 1 |

### replacement — typeAcc=0.750（配对 100 pairs）
| pred \ gt | button | checkbox | icon | list_item | slider | switch | text_block |
|---|---|---|---|---|---|---|---|
| button | 4 | 0 | 8 | 0 | 0 | 2 | 0 |
| icon | 0 | 0 | 3 | 0 | 0 | 0 | 0 |
| input | 1 | 0 | 0 | 5 | 1 | 0 | 1 |
| list_item | 0 | 0 | 0 | 8 | 0 | 2 | 0 |
| switch | 0 | 0 | 0 | 0 | 0 | 19 | 0 |
| text_block | 0 | 2 | 2 | 1 | 0 | 0 | 41 |

混淆对 top-10（predLabel→gtClass）:

| predLabel | gtClass | count |
|---|---|---|
| text_block | text_block | 41 |
| switch | switch | 19 |
| button | icon | 8 |
| list_item | list_item | 8 |
| input | list_item | 5 |
| button | button | 4 |
| icon | icon | 3 |
| button | switch | 2 |
| list_item | switch | 2 |
| text_block | checkbox | 2 |

### ocr-off — typeAcc=0.750（配对 100 pairs）
| pred \ gt | button | checkbox | icon | list_item | slider | switch | text_block |
|---|---|---|---|---|---|---|---|
| button | 4 | 0 | 8 | 0 | 0 | 2 | 0 |
| icon | 0 | 0 | 3 | 0 | 0 | 0 | 0 |
| input | 1 | 0 | 0 | 5 | 1 | 0 | 1 |
| list_item | 0 | 0 | 0 | 8 | 0 | 2 | 0 |
| switch | 0 | 0 | 0 | 0 | 0 | 19 | 0 |
| text_block | 0 | 2 | 2 | 1 | 0 | 0 | 41 |

混淆对 top-10（predLabel→gtClass）:

| predLabel | gtClass | count |
|---|---|---|
| text_block | text_block | 41 |
| switch | switch | 19 |
| button | icon | 8 |
| list_item | list_item | 8 |
| input | list_item | 5 |
| button | button | 4 |
| icon | icon | 3 |
| button | switch | 2 |
| list_item | switch | 2 |
| text_block | checkbox | 2 |

## per-stratum 分解（meta.stratum）

### baseline

| stratum | frames | elem P | elem R | elem F1 | text exact | ground hit | wall P50 |
|---|---|---|---|---|---|---|---|
| dense | 3 | 0.088 | 0.162 | 0.114 | 0.585 | 0.500 | 583.6 |
| dialog | 3 | 0.116 | 0.156 | 0.133 | 0.724 | 0.667 | 563.6 |
| icon-heavy | 4 | 0.133 | 0.192 | 0.157 | 0.175 | 0.250 | 709.2 |
| list | 7 | 0.032 | 0.082 | 0.046 | 0.534 | 0.091 | 515.0 |
| scrollable | 3 | 0.078 | 0.156 | 0.104 | 0.582 | 0.167 | 746.4 |
| settings | 10 | 0.117 | 0.198 | 0.147 | 0.633 | 0.500 | 525.6 |
| sidebar | 3 | 0.057 | 0.100 | 0.072 | 0.551 | 1.000 | 469.6 |
| text-heavy | 3 | 0.143 | 0.172 | 0.156 | 0.714 | 0.333 | 454.8 |

### integration

| stratum | frames | elem P | elem R | elem F1 | text exact | ground hit | wall P50 |
|---|---|---|---|---|---|---|---|
| dense | 3 | 0.072 | 0.162 | 0.100 | 0.585 | 0.500 | 1100.2 |
| dialog | 3 | 0.109 | 0.156 | 0.128 | 0.724 | 0.667 | 1098.4 |
| icon-heavy | 4 | 0.111 | 0.192 | 0.141 | 0.175 | 0.250 | 1301.4 |
| list | 7 | 0.035 | 0.098 | 0.052 | 0.534 | 0.091 | 937.3 |
| scrollable | 3 | 0.071 | 0.156 | 0.097 | 0.582 | 0.167 | 1232.6 |
| settings | 10 | 0.119 | 0.234 | 0.158 | 0.633 | 0.500 | 975.6 |
| sidebar | 3 | 0.058 | 0.133 | 0.081 | 0.551 | 1.000 | 951.3 |
| text-heavy | 3 | 0.195 | 0.276 | 0.229 | 0.714 | 0.333 | 913.8 |

### replacement

| stratum | frames | elem P | elem R | elem F1 | text exact | ground hit | wall P50 |
|---|---|---|---|---|---|---|---|
| dense | 3 | 0.062 | 0.135 | 0.085 | 0.585 | 0.500 | 915.4 |
| dialog | 3 | 0.176 | 0.188 | 0.182 | 0.724 | 0.667 | 959.8 |
| icon-heavy | 4 | 0.052 | 0.090 | 0.066 | 0.175 | 0.000 | 1055.6 |
| list | 7 | 0.011 | 0.033 | 0.017 | 0.534 | 0.182 | 790.2 |
| scrollable | 3 | 0.078 | 0.178 | 0.108 | 0.582 | 0.167 | 951.6 |
| settings | 10 | 0.111 | 0.243 | 0.152 | 0.633 | 0.500 | 869.0 |
| sidebar | 3 | 0.119 | 0.233 | 0.157 | 0.551 | 1.000 | 867.4 |
| text-heavy | 3 | 0.264 | 0.483 | 0.342 | 0.714 | 0.333 | 830.8 |

### ocr-off

| stratum | frames | elem P | elem R | elem F1 | text exact | ground hit | wall P50 |
|---|---|---|---|---|---|---|---|
| dense | 3 | 0.062 | 0.135 | 0.085 | 0.000 | 0.000 | 454.1 |
| dialog | 3 | 0.176 | 0.188 | 0.182 | 0.000 | 0.000 | 472.2 |
| icon-heavy | 4 | 0.052 | 0.090 | 0.066 | 0.000 | 0.000 | 452.7 |
| list | 7 | 0.011 | 0.033 | 0.017 | 0.000 | 0.091 | 453.0 |
| scrollable | 3 | 0.078 | 0.178 | 0.108 | 0.000 | 0.000 | 455.9 |
| settings | 10 | 0.111 | 0.243 | 0.152 | 0.000 | 0.000 | 458.6 |
| sidebar | 3 | 0.119 | 0.233 | 0.157 | 0.000 | 0.000 | 494.6 |
| text-heavy | 3 | 0.264 | 0.483 | 0.342 | 0.000 | 0.000 | 546.8 |

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
| 058f62426c1f | equal | 26 | 26 |
| 16068bc9a851 | equal | 7 | 7 |
| 1e572c8f5092 | equal | 28 | 28 |
| 206556c78e8e | equal | 17 | 17 |
| 29f34bdb9dac | equal | 13 | 13 |
| 3682353fffbf | equal | 32 | 32 |
| 3a28cc28b0b5 | equal | 33 | 33 |
| 4909995071e6 | equal | 4 | 4 |
| 4a1f3e1d4421 | equal | 25 | 25 |
| 4e490bf99988 | equal | 35 | 35 |
| 518f41240dd0 | equal | 22 | 22 |
| 5df0424f787c | equal | 11 | 11 |
| 5ef64e4a384a | equal | 26 | 26 |
| 6ea31e4ba9be | equal | 48 | 48 |
| 731aa1393639 | equal | 21 | 21 |
| 7b3f715379b0 | equal | 14 | 14 |
| 7e41f85e06e4 | equal | 25 | 25 |
| 82565b48ca54 | equal | 24 | 24 |
| 8576385f9b04 | equal | 18 | 18 |
| 882f15432eca | equal | 26 | 26 |
| 8db380e98151 | equal | 13 | 13 |
| 9297d23b7c9f | equal | 16 | 16 |
| 9591de5f37de | equal | 52 | 52 |
| 9ee712f0f817 | equal | 26 | 26 |
| 9f5d4e04c7cf | equal | 22 | 22 |
| a5d983aa849b | equal | 39 | 39 |
| a632ca5c963b | equal | 28 | 28 |
| a86c6501da91 | equal | 25 | 25 |
| acea3e4e4839 | equal | 10 | 10 |
| afff0f4a8557 | equal | 28 | 28 |
| bcc3cf830444 | equal | 13 | 13 |
| be36cfe40647 | equal | 26 | 26 |
| bf0eca5f9851 | equal | 33 | 33 |
| c1667d8b209e | equal | 26 | 26 |
| c28ad3c4e752 | equal | 30 | 30 |
| c8b2e65f2451 | equal | 14 | 14 |
| d2a5c3eca70a | equal | 35 | 35 |
| d8b2a487cdfa | equal | 46 | 46 |
| f043962c67eb | equal | 29 | 29 |

## B2 已知限制（如实记录）

- B2（ocr-off）：进程内跑 _run_pipeline，无 HTTP 序列化/GC 段（serialize/gc 为 null）；该臂 latency = 管道 wall。
- click-state：state 感知超出当前 runtime 边界能力，resolver 只做目标框中心命中判定。
- typeAccuracy 主口径 = class-agnostic 贪心 IoU≥0.5 配对下的 label==gtClass 比例（WI-5b 修正 b——严格 class 相等配对下按构造恒 1.0，无信息量）；混淆对计数（predLabel→gtClass top-N）随表。
- 次级行容忍口径（elem P/R/F1(center)）：matchMode=center，class 相等 ∧ 预测中心 ∈ GT 框，无 margin——YOLO 文字 extent vs GT 整行 extent 的框语义差异（D6r 几何归因），与主口径并列。
- skippedForQuality：reviewStatus 以 rejected 开头（D6r 排除 帧）→ 质量指标跳过、latency/RSS 保留，报告显式列出。
