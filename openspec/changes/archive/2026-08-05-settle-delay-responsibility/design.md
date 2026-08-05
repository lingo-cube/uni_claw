## Context

引擎循环 `TraversalEngine.RunAsync` 在每步开头无条件 `Task.Delay(DelayPerStepMs)`（生产值 300ms）。此延时本意是给设备 UI 动画 settle，但：
1. 对非操作步（决策、验证重试、缓存命中）也生效
2. 放在循环头，ResultVerify 操作后首帧截图无 settle 窗口，靠重试（~4.5s）兜底
3. 引擎不应持有设备层时序概念

参考 PRD: [docs/prd/2026-08-05-settle-delay-responsibility-prd.md](../../docs/prd/2026-08-05-settle-delay-responsibility-prd.md)。

## Goals / Non-Goals

**Goals:**
- settle 仅对成功 ADB 操作生效，位于操作后、ResultVerify after 截图前
- 非操作步零开销
- 可配置（`UNICLAW_SETTLE_DELAY_MS`），可关闭（`0`）

**Non-Goals:**
- per-operation-type settle（tap/swipe/back 不同值）— 过早优化
- `TraversalEngineConfig.DelayPerStepMs` 删除 — 保留供测试/模拟
- `AdbActionExecutor` 改动 — settle 在 Host 视觉层

## Decisions

### D1: settle 放在 `PageInvalidatingActionExecutor` 而非 `AdbActionExecutor`

| 方案 | 评估 |
|------|------|
| A（选用）`PageInvalidatingActionExecutor` | 视觉管线专属；与 `_invalidate()` 语义相邻；设备后端无关 |
| B `AdbActionExecutor` | 低层设备组件持有上层视觉概念；每个设备后端需重复；非 Host 场景被污染 |

`PageInvalidatingActionExecutor` 已在操作成功后执行 `_invalidate()` 和 `onSuccess` 回调——在此加 `Task.Delay` 边界清晰。

### D2: 保留引擎 `DelayPerStepMs` 属性和循环守卫

不删除 — 测试和模拟场景通过自己构造 config 独立设置延时值。生产 `TraversalEngineConfig` 传 `0` 即可。

### D3: 配置优先 env var，不进 `integration.config.json`

`UNICLAW_SETTLE_DELAY_MS` 注册为 L4 层（与 `UNICLAW_OMP_THREADS` 同类），变动频率低、per-device 差异化，不进配置文件增加 schema 复杂度。

## Risks / Trade-offs

- **swipe 叠加**: 手势 durationMs（300ms）+ settle（300ms）= 600ms。手势时长是触屏物理参数，settle 是 post-animation 等待，两阶段独立，不冲突。
- **值未调优**: 300ms 沿用历史值，set `UNICLAW_SETTLE_DELAY_MS=0` 可立即回退。
