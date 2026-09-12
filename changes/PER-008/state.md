# PER-008 — 感知管道显式化 + 可配置化（自 PER-007 剥离，待 grill）
lifecycle_state: persisted · disposition: none · depth: standard · base: pending（PER-007 落地后定）

## Intent（WHAT/WHY）
PER-007 迁移后的感知服务，其执行管道仍是 server.py `_run_pipeline` 的硬编码
序列。本 change 把管道升为显式 stage 结构 + 配置驱动——这是 OPT 系列
（后端对照 / 模型替换 / 融合调整 / 工程修复）的地基：换配置而非改服务。
**前置：PER-007 落地（迁移 + 解缠），本 change 站在已迁移的树上做。**

## Scope（草案，grill 后定稿）
- `_run_pipeline` 拆具名 stage：preprocess → detect → recognize → fuse →
  remap → validate → assemble，每段 (inputs, params) → outputs。
- `config/pipeline.json`（默认 = 今日行为）+ impl 注册表（detect 今天唯一
  接线 torch-yolo；onnx-ort / coreml 是 OPT-001 增量注册位）。
- 未知 stage/impl/param → 启动期 fail-closed；`UNICLAW_PIPELINE_CONFIG`
  env 覆盖；fusion operator 序列从「根规则默认解析」升为显式配置。
- per-stage 计时统一归 runner（Server-Timing 分段）。

## Out of Scope（禁止）
- 契约变化（路由 / 响应 schema / 失败分类冻结）；fusion engine 与 operators
  内部逻辑；推理 impl 的新后端接线（OPT-001）。

## Grill 待决问题（开 grill 时用）
1. 配置格式与层级：pipeline.json 独立文件 vs 并入现有 config 加载链；env
   覆盖的粒度（文件级 / 字段级）。
2. knob 集边界：哪些进配置（backend / 模型路径 / operator 序列 / 参数 /
   开关），哪些不进（无 buyer 不加）。
3. impl 注册表形状：注册在代码常量 vs 配置声明；版本化策略
   （pipelineConfigVersion）。
4. stage 粒度：remap/validate 是否值得独立 stage，还是 assemble 的一部分。
5. 默认配置等价性验收口径（对照基线的哪一层：字节 / 语义 / 关键数组）。

## Status log
2026-09-12 · enter→persisted · 自 PER-007 剥离（Human 裁决：配置化单独
  grill，先把已 grill 的迁移收尾）；待 PER-007 CLOSED 后开 grill 定稿。
