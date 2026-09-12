# FSV-001 WI-4 — GT 完成（transient WorkItem 载荷）

- status: agent 侧 dispatched → subagent 0adfcd46-df46-4a68-b831-6b34dbd0f674；
  Leader 校正随后（calibration core ≥5 屏全框核对 + 其余抽样）。
- objective: gt_generate.py（a11y→候选 GT：可见叶子 + 容器行折叠 + canonical
  类映射 + state）+ overlay 可视化 + index + groundingTasks 提案 +
  GENERATION-REPORT（偏差清单）。
- acceptance: 39 帧全覆盖；schema 合 eval-spec；确定性重跑稳定；
  偏差如实；overlay 可供 Leader read_image 核对。
- result: 完成（2026-09-12）。agent 侧：39 候选 GT + overlays + index +
  GENERATION-REPORT（445 元素；63 groundingTasks；B1–B14 偏差清单）。
  Leader 校正（D6r 确定性路线）：R1×10/R2×14 规则修正；3 帧强 stale
  排除（5df0424f/9f5d4e04/d8b2a487）；dialog 豁免；36 帧参与质量评分。
  calibrate.py + CALIBRATION-REPORT.md + calibration-decisions.json 落盘。
