# FSV-001 WI-6 — v2（整屏输入域修正）对照 v1 Delta 分析

- v1 报告（只读基线）：`evaluation/reports/fsv001/fsv001-eed2e12a65a2.json`
- v2 报告：`evaluation/reports/fsv001-v2/fsv001-7b1439bb8758.json`
- 对比方法：同 harness（`bench/compare_arms.py`，matcher-greedy-v1，IoU≥0.5，
  hit margin ±8px，CPU，39 帧，runs=5，warmup=1）；v2 以 `analysis_delta.py`
  重新对拍（v1 数字与 `ANALYSIS.md` 逐项吻合，方法有效）。
- 结论预览：ScreenParser 系臂（A/B1/B2）原有系统劣势**主要源于输入域混杂**
  （proc 图喂给整屏 operating point 的模型），修正后 B1 元素口径升至
  baseline 平价（R 反超），但延迟/内存代价与 grounding 平价不变——
  **NO_CHANGE 作为操作结论维持，但其依据必须修订**（§9）。

## 1. 修正内容（代码/测试落点）

- 逆映射：`uniclaw_perception/screenparse/geom.py::map_original_to_proc`
  （原图像素空间 → proc 空间；公式 = preprocess crop+resize 的精确浮点逆：
  `proc_x = x·(proc_w/orig_w)`，`proc_y = (y−top_px)·(proc_h/crop_h)`）。
- 接线：`server.py::_run_pipeline` 两处（replacement `_detect()` +
  integration screenparse 段）改 feed **原始 image**，输出先逆映射再
  adapt/rescue/序列化；fusion/remap/enforce_geometry 零修改。
- 越界语义（D11）：映射后不完全落在 proc 画布内的检测**丢弃**（fail-closed，
  不 clamp），计数进 `screenParse.summary.droppedOffCanvas`。
- additive：`screenParse.summary.inputSpace="original"`（旧 proc 行为可从
  该字段缺席区分；v1 证据无此字段）。
- 单测：`tests/test_screenparse_geom.py`（17 例：已知点/边界/越界/非有限/
  计数/双向/自定义几何）、`tests/test_screenparse_provider.py`（summary 新
  契约）、`tests/test_screenparse_integration.py::TestVariantPathW16`
  （slow：整屏 ≥ 预处理检出对拍 + inputSpace + 确定性两跑）。
- **回归锚**：default 管道三资产逐字节回归测试绿（baseline 臂 v1→v2 全部
  质量指标 Δ=0，见 §3）+ pytest 全量 136 绿（原 115 + 新增 21）。

## 2. 输入域修正的现场验证（按资产）

settings-home（1080×1920）：raw full 33 vs raw proc 22（整屏多 50%），
逆映射后 kept 22 + dropped 11；integration 变体 summary
`{inputSpace: original, rawCount 22, mappedCount 22, structuralCount 0,
rescuedCount 2, droppedOffCanvas 11}`——与 D11「实测整屏每帧多检出 ~25%」
方向一致。case-b-off（B1）：raw 27 → kept 16 + dropped 11，mode=replacement。

v2 全帧合计：screenParse.summary.droppedOffCanvas = **664**
（integration 306 / replacement 358）；yolo[] 总量 baseline 813（= v1，
锚）→ integration 940（+10）/ replacement 966（−35，净少于 v1 1001——
整屏检出中越界丢弃部分不再进池）。

## 3. v2 四臂主表（pooled，36 已评分帧）

| 臂 | elem P | elem R | elem F1 | elem R(center) | elem P(center) | elem F1(center) | typeAcc | bboxIoU mean | text exact | cand F1 | grounding hit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| baseline | 0.0913 | 0.1608 | 0.1164 | 0.2624 | 0.1490 | 0.1901 | 0.6700 | 0.6969 | 0.5321 | 0.0923 | 0.3696 |
| integration | 0.0892 | 0.1820 | 0.1198 | 0.2671 | 0.1309 | 0.1757 | 0.6909 | 0.7122 | 0.5321 | 0.0897 | 0.3696 |
| replacement | 0.0857 | 0.1797 | 0.1160 | 0.4350 | 0.2074 | 0.2809 | 0.7500 | 0.7920 | 0.5321 | 0.0631 | 0.3696 |
| ocr-off | 0.0857 | 0.1797 | 0.1160 | 0.4350 | 0.2074 | 0.2809 | 0.7500 | 0.7920 | 0.0000 | 0.0918 | 0.0217 |

## 4. delta 表（v2 − v1）

| 臂 | elemP | elemR | elemF1 | secR | secF1 | typeAcc | bboxIoU | txtExact | candF1 | ground | wall P50 | yolo P50 | sp P50 | RSS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| baseline | 0.000 | 0.000 | 0.000 | 0.000 | 0.000 | 0.000 | 0.000 | 0.000 | 0.000 | 0.000 | +42.5ms | +28.7ms | — | +1.1MB |
| integration | +0.004 | +0.009 | +0.006 | +0.005 | +0.002 | +0.012 | +0.006 | 0.000 | −0.001 | 0.000 | −2.8ms | +5.0ms | −44.0ms | +19.5MB |
| replacement | +0.022 | +0.043 | +0.029 | +0.007 | +0.011 | −0.010 | −0.037 | 0.000 | +0.025 | −0.065 | +68.2ms | +19.1ms | — | +13.6MB |
| ocr-off | +0.022 | +0.043 | +0.029 | +0.007 | +0.011 | −0.010 | −0.037 | 0.000 | +0.058 | −0.043 | +15.4ms | +14.9ms | — | −142.6MB |

- baseline 全部质量指标 Δ=0 → **默认管道回归锚在 v2 运行中逐项成立**；
  latency/RSS 两项波动为机器噪声（同法观测，v1/v2 各自 run-to-run）。
- text 全臂不变（0.5321）——输入域修正只动 detect 路径，OCR 面零变化（B2
  恒定 0）。

## 5. 逐臂解读

- **integration（A）**：口径整体小幅改善——elem F1 0.114→0.120（+5pp 级别
  的 5.3% 相对提升），typeAcc 0.679→0.691，rescue 命中率 5.5%→8.5%（§7）。
  增益真实但量级小；grounding 不变；代价 = 整屏推理 screenparse 段
  P50 384ms + 全程 wall 1028ms（≈2× baseline）。**INTEGRATE 仍不成立**。
- **replacement（B1）**：v1 的最大缺陷（element R 0.137 大幅落于 baseline
  0.161）**基本消失**——R 0.180 反超 baseline，F1 0.116 与 baseline 打平，
  typeAcc 0.750 仍高于 baseline 0.670，bboxIoU 0.792 依旧大幅优于 baseline
  0.697，center 口径（行容忍）R 0.435 仍是 baseline 0.262 的 1.66×。
  代价：yolo P50 671ms（+70% vs baseline 395ms）、wall 896ms（+61%）、RSS
  +568MB。grounding 从 v1 反常的 0.435 回落至 0.370（= baseline，§8）。
- **ocr-off（B2）**：与 B1 同源 yolo[]（全部 spot-check "equal"），元素口径
  同 B1；文字能力归零的出口照旧（text 0.0、grounding 0.022、candidates
  textless）。B2 的生命周期不变：无文字场景才可能考虑。
- 变量隔离：B1/B2 同图同 yolo 抽查 39/39 equal ✓；D8 保持。

## 6. per-stratum 变化（element recall v1 → v2）

| stratum | baseline | integration | replacement |
|---|---|---|---|
| dense | 0.162 → 0.162 | 0.162 → 0.162 | **0.054 → 0.135** |
| dialog | 0.156 → 0.156 | 0.156 → 0.156 | **0.125 → 0.188** |
| icon-heavy | 0.192 → 0.192 | 0.192 → 0.192 | **0.141 → 0.090 ▼** |
| list | 0.082 → 0.082 | 0.098 → 0.098 | **0.016 → 0.033** |
| scrollable | 0.156 → 0.156 | 0.156 → 0.156 | 0.178 → 0.178 |
| settings | 0.198 → 0.198 | 0.207 → 0.234 | **0.135 → 0.243** |
| sidebar | 0.100 → 0.100 | 0.100 → 0.133 | **0.133 → 0.233** |
| text-heavy | 0.172 → 0.172 | 0.276 → 0.276 | **0.448 → 0.483** |

- 修正对 B1 的分层效应：dense/settings/sidebar/dialog/list 召回 2–3× 提升，
  text-heavy 继续领先（0.483）。
- **唯一回退 = icon-heavy**（B1 R 0.141→0.090，P 0.074→0.052；grounding
  0.25→0.0）：整屏输入下 ScreenParser 在该层检出总数减少（4 帧 yolo[] 51→48、
  32→26、31→26、35→35）且类等配 IoU<0.5 的匹配变少——整屏 letterbox 几何
  与 proc 不同 + 越界丢弃部分替换了原 proc 检出。该层 B1 仍显著落后
  baseline（0.090 vs 0.192），是 replacement 的已知弱层。

## 7. rescue（Test A）解剖：v1 vs v2

| 指标 | v1 | v2 |
|---|---|---|
| rescue 元素总数 | 109 | **118** |
| rescue→GT 命中 | 6（5.5%） | **10（8.5%）** |
| 类别分布 | text_block 91 / button 8 / switch 7 / tab 2 / list_item 1 | text_block 97 / button 11 / switch 7 / tab 2 / slider 1 |

- 整屏输入把 rescue 池扩大（+9 元素）且命中率翻半（5.5%→8.5%）——漏检救援
  的证据质量改善，但绝对命中率仍低（118 个 rescue 仅 10 个挂上 GT 实体），
  Test A 增量增益依旧有限；D11「rescue 阈值维持 0.35」（解剖证据反对降低）
  维持。

## 8. grounding（B1 回落的三翻转）与附加发现

- B1 grounding v1 0.435（异常优于 baseline）→ v2 0.370（= baseline）。逐任务
  对拍：**恰好 3 个任务 HIT→MISS、0 个反翻转**：
  - `6ea31e4ba9be/g2`（icon-heavy）：contain-fail
  - `7e41f85e06e4/g1`（list）、`c1667d8b209e/g1`（list）：off-target
  机制：OCR 输入未变，差异全在 B1 yolo[] 几何（整屏检出框更大/位移），
  resolver 的「最小包含框」选到不同框 → 中心偏离 GT / 不包含 OCR 中心。
  v1 中 B1「grounding 优于 baseline」属输入域假象（proc 小图检出框更贴
  文本）；修正后 B1 grounding 不再优于 YOLO，为平价。
- D11 附加校正：v1「39 帧容器类检出=0」是 proc 输入的假象——v2 整屏输入
  在 screenParse[].structural 出现 14 条（Calendar 6 / Progress bar 6 /
  Rating Indicator 2），仍为零星数量，Optional Evidence 通道基本为空（只进
  screenParse[]，不入检测池），D11「结构性 Optional Evidence 按设计为空」的
  披露口径微调为「近空（14/78 臂帧）」。

## 9. 判定修订建议（核心问题：NO_CHANGE 是否维持）

**操作结论维持 NO_CHANGE，但理由必须修订——v1 的 NO_CHANGE 依据被本次修正
证伪了一半。**

- v1 依据（现已被证伪的部分）：「ScreenParser 系臂系统性不利——B1 element
  recall 大幅落后 baseline」。修正输入域后该劣势消失：B1 element R 反超、
  F1 打平、typeAcc/IoU 口径仍优。**以 'replacement 质量输给 baseline' 为
  理由的 NO_CHANGE 不再成立**。
- 维持 NO_CHANGE 的真实依据（修正后仍成立）：
  1. **代价/收益比**：B1 质量仅平价-略优，却付 yolo +70% / wall +61% /
     RSS +0.57GB（CPU 主表）；A 臂 +500ms 只换来 F1 +0.006。生产默认 CPU
     上不划算。
  2. **grounding 无优势**：B1 grounding 修正后回到 baseline 平价（resolver
     对整屏大框更敏感，3 任务翻转）。
  3. **icon-heavy 弱层**：B1 在该层仍大幅落后（R 0.090 vs 0.192 且 grounding
     归零）。
- 修订后的建议**形态**：NO_CHANGE（不迁移、不正式集成）依旧，但把
  PARTIAL_REPLACE 从「已被证据否决」改为「**延迟预算条件下的候选项**」——
  若未来有非 CPU 推理形态（MPS/加速器）或延迟敏感度放宽，B1 的
  typeAcc 0.75 / bboxIoU 0.79 / center 口径 R 0.435 使其成为值得再评的
  替代方案；本轮不落地（Human Gate 裁决留给 Leader/任务书 §18）。
- A 臂维持「不集成」（INTEGRATE 不成立）：rescue 命中率 8.5% 的增量质量
  不抵 +2× 延迟。

## 已知限制（如实记录）

- v2 与 v1 的 latency/RSS 对比含机器噪声（baseline 自身 wall P50 漂移
  +42ms——同法两轮）；B1/A 的延迟增量结论以**同轮内臂间对比**为准
  （B1/baseline 同轮 +61%）。
- rescue/B1-独有「GT 命中」用 class-agnostic IoU≥0.5（几何优先），类型判对
  与否另见 typeAcc/混淆。
- rejected 帧（5df0424f787c/9f5d4e04c7cf/d8b2a487cdfa）不入质量评分；
  latency/RSS 保留（与 v1 同法）。
- 复现：`bench/compare_arms.py --valset evaluation/validation/fastscreen-v1
  --out-dir evaluation/reports/fsv001-v2`；本分析脚本
  `evaluation/analysis_delta.py`。