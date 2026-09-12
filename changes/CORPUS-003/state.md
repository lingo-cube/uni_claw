# CORPUS-003 — 历史基准报告迁移（CORPUS-002 补遗）
lifecycle_state: closed · disposition: implemented · depth: minimal · base: e44e45d4

## Intent（WHAT/WHY）
uni-agent 的评测基准报告（质量基线/逐帧预测/run 记录）未随 CORPUS-002
迁移——它们是 OPT-001「质量不降级」验收的历史对照物。

## Scope
- `evaluation/reports/` 12 份（baselines×2 含 quality/safety scorecard +
  performance + coverage + holdout 状态；predictions×7 两次 run 对 4 资产
  逐帧报告含 per-stage 计时；runs×3 元记录）。
- `bench/benchmark_raw.py`（legacy HTTP raw-vs-JPEG 基准脚本，参考工具）。
- README 增补可比性判读。本 state（minimal）。

## Decisions
- D1 可比性：质量维度可比（model_id 3f39b0d6… 与当前部署一致、同
  rapidocr、同资产）；性能维度不可比（x86_64 vs arm64 + 旧身份体系
  `pipeline_revision: 1.0.0` 是 PER-008 前的 stale 串）——性能基准以本机
  bench/run_l2.py 重采为准。

## Verification
```yaml
verification:
  level: CONTRACT
  method: 12 份 JSON 全部可解析 + baseline model_id 与当前 /version 一致核对 + README 判读成文
  expected: 迁移完整、判读明确
  actual: 12/12 解析 OK；model_id 前缀 3f39b0d648328010 匹配；提交 1f5e135。
  evidence: 本 state + evaluation/README.md
```

## Status log
2026-09-12 · enter→closed · Human 确认迁移；执行一轮（bash 循环变量错误
  修正后 12 份落地）；可比性判读入 README。
