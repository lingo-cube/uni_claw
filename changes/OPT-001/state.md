# OPT-001 — 感知优化第一批：工程修复 + 同权重推理后端对照
lifecycle_state: persisted · disposition: none · depth: decision-heavy · base: 1fbc99aa

## Intent（WHAT/WHY）
感知链已闭环（RUN-002）但执行面粗糙：YOLO/OCR 串行、每请求强制全量 GC、
分段计时不完整、推理只在 torch-CPU。本 change 在**不改变识别能力**的前提
下做工程修复与后端对照——每一步都要能对基线说「什么都没变坏，只有 X
变好了」。三块地基已就绪：bench 双锚（PER-008）+ 错题集（CORPUS-002）+
历史质量基线（CORPUS-003，model_id 与当前部署一致）。

## Decisions（grill-lite 2026-09-12，Human 按推荐全采纳）
| # | 决策 |
|---|---|
| D1 | **后端首批 = torch-MPS**（同权重换 device，零导出成本，impl 注册表增量）；ONNX-RT / CoreML EP 作后续增量（导出后需数值等价验证） |
| D2 | **质量门自动化 = 移植评分器**：uni-agent evaluation 的 matcher + metrics + scorecard 核心平移（4 个有 gt 资产自动评分 → 新旧 scorecard 对照）|
| D3 | **实验纪律 = 每项独立、数据先行**：并行化须证明输出逐字节等价（bench 哈希锚）才合入；GC 先 `--gc-off` 对照拿数据再定去留；**计时补全先落地**（是前两者的前提）|
| D4 | 三关验收（裁判席）：① bench 重采性能（arm64 本机）② gt 资产质量对照（评分器 vs 历史基线）③ REGRESSION 角色资产输出哈希对拍 |

## Scope（切片序）
- S1 **计时补全**：server.py 分段计时覆盖 JSON 序列化 + GC（现 yolo_ms 含
  预处理、不含序列化/GC）；STAGES 计时归位 runner（Server-Timing 增段）。
- S2 **torch-MPS impl**：`detect` 注册 `torch-mps`（STAGE_IMPLS 增量 +
  yolo/inference device 参数化 + pipeline 变体接线）；MPS 不可用 fail-closed
  回 CPU 报告（不静默）。
- S3 **评分器移植**：matcher/metrics/scorecard 纯函数族 →
  `evaluation/`（资产域同侧）；对 4 gt 资产产出新 scorecard。
- S4 **GC 对照实验**：bench 增 gc 开关对照；数据决定策略（保留/降频/移除）。
- S5 **并行化**（最后）：detect/recognize ThreadPool 受控并行；合入门槛 =
  输出逐字节等价。
- 每片独立提交，各自对基线验证。

## Out of Scope（禁止）
- 换模型/权重（OPT-002 PP-OCRv5）；融合逻辑改动；契约面变化（路由/语义
  数组/失败分类）；C# 侧；热切换；ONNX/CoreML 导出（后续增量）。

## Acceptance
- A1（DETERMINISTIC）计时补全后 Server-Timing 覆盖全段（含 serialize/gc），
  默认行为输出零漂移（基线对照）。
- A2（DETERMINISTIC）torch-mps 变体：MPS 可用时输出与 torch-cpu **语义
  等价**（浮点后端差异如实记录容忍口径：坐标/标签集合一致，confidence
  容差）；MPS 不可用 → 启动失败或显式回退报告（不静默）。
- A3（DETERMINISTIC）评分器对 4 gt 资产产 scorecard，与历史基线同口径
  可对照（默认配置下质量分数不降）。
- A4 GC/并行各自实验数据 + 决策留痕（state/evidence）。
- A5 三关裁判席全过（D4）+ 全量回归零破坏。

## Verification
```yaml
verification:
  level: DETERMINISTIC（实验对照）+ 全量回归
  method: pending（分片各自：基线对照 / 哈希对拍 / scorecard 对照 / 实验数据）
  expected: A1–A5
  actual: pending
  evidence: pending
```

## Status log
2026-09-12 · enter→resolving→persisted · grill-lite 三问答（全按推荐）；
  三块地基确认就绪；切片序定稿（计时先行）；待开工 S1。
