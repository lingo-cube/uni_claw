# UniClaw Perception 训练工件指南（人读视图）

> 分类四档：KEEP — CANONICAL / KEEP / ARCHIVE — DIAGNOSTIC / DERIVED / DISPOSABLE / UNKNOWN — REVIEW BEFORE DELETE。
> 不熟悉的文件一律先归 UNKNOWN，绝不建议删除。

DerivedFrom: deploy:101f5ddccd2db3d179de5ed00205f45887442a3e74f443fcdda9f0beb88a71b8|cand:c26b55fd765d70c1787852759cc0ea2c685a6e984676e92c7754bb22401d0837|trun:6f41b678173f93ea41a587f99cd9d12be5884638d12724bfb18ce6123b2b94aa|run:a90543cfbb748a05a988298a63e01be82848e55dfe409b20d358ee1130cb724c

| Artifact | Category | 是什么 | Authority | 可重新生成 | 怎么读 |
|---|---|---|---|---|---|
| `best.pt` | KEEP / ARCHIVE — DIAGNOSTIC | 训练过程按其验证标准选出的 checkpoint（角色名）。 | 无任何模型/release 权威。ModelId 才是内容身份。 | 否（除非重新训练） | 看 TrainingRun 的 producedCheckpoints 与 ModelArtifact，而不是文件名。 |
| `last.pt` | KEEP / ARCHIVE — DIAGNOSTIC | 最后一个 epoch 的 checkpoint。 | 无权威。 | 否 | 同上。 |
| `args.yaml` | KEEP / ARCHIVE — DIAGNOSTIC | 训练框架的调用参数记录。 | 无权威；canonical 是 TrainingConfig manifest。 | 是（由 TrainingConfig 派生） | 与 tcfg 清单交叉核对。 |
| `results.csv` | KEEP / ARCHIVE — DIAGNOSTIC | 训练逐 epoch 指标原始数据。 | 无权威。 | 否 | 看收敛趋势，不用于上线判断。 |
| `results.png` | KEEP / ARCHIVE — DIAGNOSTIC | 指标曲线图。 | 无权威。 | 是（由 csv 重绘） | 看 train/val 分叉。 |
| `PR_curve.png` | KEEP / ARCHIVE — DIAGNOSTIC | PR 曲线。 | 无权威。 | 是 | 看类别差异。 |
| `F1_curve.png` | KEEP / ARCHIVE — DIAGNOSTIC | F1-阈值曲线。 | 无权威。 | 是 | 仅诊断。 |
| `P_curve.png` | KEEP / ARCHIVE — DIAGNOSTIC | Precision-阈值曲线。 | 无权威。 | 是 | 仅诊断。 |
| `R_curve.png` | KEEP / ARCHIVE — DIAGNOSTIC | Recall-阈值曲线。 | 无权威。 | 是 | 仅诊断。 |
| `confusion_matrix.png` | KEEP / ARCHIVE — DIAGNOSTIC | 混淆矩阵。 | 无权威。 | 是 | 看易混淆类别对（如 switch↔icon）。 |
| `confusion_matrix_normalized.png` | KEEP / ARCHIVE — DIAGNOSTIC | 归一化混淆矩阵。 | 无权威。 | 是 | 看错误去向。 |
| `labels.jpg` | KEEP / ARCHIVE — DIAGNOSTIC | 标注可视化。 | 无权威。 | 是 | 查标注质量。 |
| `labels_correlogram.jpg` | KEEP / ARCHIVE — DIAGNOSTIC | 标注框分布。 | 无权威。 | 是 | 查几何分布。 |
| `train_batch*.jpg / val_batch*_*.jpg` | KEEP / ARCHIVE — DIAGNOSTIC | 批次预览图。 | 无权威。 | 是 | 查输入对齐。 |
| `manifests/*.json` | KEEP — CANONICAL | DatasetVersion / TrainingRun / Candidate / lineage 等机器真理清单。 | 机器真理（只读）。 | 否（内容哈希不可再生成） | 直接读 JSON；本 Skill 生成的报告只是人读视图。 |
| `model-store/<modelId>.pt` | KEEP — CANONICAL | 内容寻址的 ModelArtifact 本体。 | canonical 模型字节（modelId = 字节 SHA-256）。 | 否 | 以 modelId 引用，永远不要以文件名引用。 |
| `mini-data/` | DERIVED / DISPOSABLE | 物化的训练目录视图（images/labels/data.yaml）。 | 无权威；canonical 是 DatasetVersion manifest。 | 是（从种子生成代码重生成） | 删除不影响 dataset 身份。 |
| `runs/ultralytics/…/weights/*.pt` | KEEP / ARCHIVE — DIAGNOSTIC | 训练框架输出的权重文件。 | 无权威；canonical 副本在 model-store。 | 仅通过重新训练 | 删除前确认 model-store 中存在相同内容的副本。 |
