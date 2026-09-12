# FSV-001 评测规格 — GT schema + 四臂指标 + grounding 任务族（WI-4/WI-5 契约）

> Leader 定稿；WI-4（GT 产出）与 WI-5（scorer/报告）共同遵守。与
> bench/score.py 的 matcher-greedy-v1 语义对齐（class 兼容 + IoU≥0.5）。

## 1. GT 文件（每帧一份）

`platforms/perception/evaluation/validation/fastscreen-v1/gt/<sha12>.json`

```json
{
  "schemaVersion": "uniclaw.fastscreenValset.gt.v1",
  "frameId": "<sha12>",
  "reviewStatus": "leader-verified | sampled | candidate",
  "provenance": {
    "method": "human-visible | a11y-assisted+human-verify",
    "annotator": "leader | wi4-agent+leader",
    "knownBiases": ["..."]
  },
  "elements": [
    {
      "gtId": "gt_1",
      "gtClass": "<canonical perception label>",
      "bounds": {"x1":0.1,"y1":0.2,"x2":0.9,"y2":0.25},
      "text": "<可见文本或 null>",
      "interactive": true,
      "state": "on|off|null"
    }
  ],
  "expectedTexts": ["<屏上可见的完整文本行，供 OCR 召回评估>"],
  "groundingTasks": [
    {"taskId":"g1","kind":"click-text","query":"Wi‑Fi","expectGtId":"gt_7"},
    {"taskId":"g2","kind":"click-nth-item","query":{"list":"gt list 或 stratum hint","nth":3},"expectGtId":"gt_9"},
    {"taskId":"g3","kind":"click-selected","query":{"state":"on"},"expectGtId":"gt_12"}
  ]
}
```

- bounds 归一化（相对原始全屏帧；与响应 remap 后 boundsPx/bounds 同空间——
  scorer 用 boundsPx 对 bounds×(W,H)）。
- gtClass 词汇 = canonical perception labels（与 adapter 目标一致）；
  不确定类型宁可标 text_block/image 也不猜 interactive 类。
- groundingTasks：每帧 0–3 条；序列帧优先（有 before/after 语义）；
  查询文本必须逐字符来自可见 UI。

## 2. 四臂

| 臂 | 服务配置 | 说明 |
|---|---|---|
| baseline | default | 现行为（回归锚兼对照） |
| A | X-Pipeline-Variant: fastscreen-integration | YOLO+OCR 原样 + FastScreen step |
| B1 | X-Pipeline-Variant: fastscreen-replacement | detect=screenparser，OCR 留 |
| B2 | B1 + bench 消融 ocr_tokens=[] | 诚实记录文字损失 |

## 3. 指标（每臂 × 全帧汇总 + per-stratum 分解）

- Element Recall/Precision/F1：predictions=yolo[]（canonical label +
  boundsPx），GT=elements[]，matcher=matcher-greedy-v1（class 相等 +
  IoU≥0.5 贪心一对一）。
- BBox 质量：matched pairs 的 IoU 分布（mean/median）+ center 偏差
  （px，normalized center × 分辨率）。
- Type Accuracy：matched pairs 中 label==gtClass 比例（matcher 已按
  class 兼容配对，此指标对"兼容但不同"敏感时单独列出）。
- Text Exact Accuracy / CER：OCR 侧——ocr[] tokens 与 expectedTexts/
  elements[].text 的贪心匹配（文本相等 = exact；编辑距离 = CER）。
  B2 预期 0/未定义——如实记录。
- Candidate-level（A 专属增益面）：candidates[] 同 matcher 对 GT 的
  P/R/F1 + text association 正确率（candidate.text == 所配 GT text）。
- Actionable Grounding Accuracy：对每个 groundingTask：用该臂响应按
  LiveFrameOccurrenceStrategy 同构 join（yolo[]/ocr[] → normalized
  locator）解析目标 → 判定解析出的 locator center 是否落在 expectGtId
  的 bounds 内（≤ 8px 边距）。解析失败 = miss。四臂同任务集。
- Latency：Server-Timing 分段（yolo/ocr/screenparse/fusion）+ 请求 wall
  clock；每臂每帧 ≥5 次重复，P50/P95。
- Resource：推理期进程 RSS（采样 max），四臂同法。

## 4. 产出

- bench/compare_arms.py（可重放：起服务→四臂跑帧→scorecards）
- reports：platforms/perception/evaluation/reports/fsv001/*.json（内容寻址）
- 汇总报告：evidence/2026-09-12-fsv-001-{integration,replacement}-ab.md
  （四臂表 + per-stratum + 失败案例截图引用 + latency/RSS + 结论素材）
