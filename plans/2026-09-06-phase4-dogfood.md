# Plan — Phase 4 V2 Dogfood（2026-09-06）

> PlanType: EXECUTION_PLAN · Status: ADOPTED
> 依据：Next Phase Directive Phase 4（真实后续任务用 V2 执行 + friction 观察；
> 四 Gate 语义冻结；Stop Boundary 内——不做 UNIFLOW_OPTIMIZATION 项）。
> 持久化理由：跨多轮上下文，需可恢复。

## 任务清单（真实、有 buyer）

| # | 任务 | 类型 | 路由 | 状态 |
|---|---|---|---|---|
| T1 | CONTEXT.md 增补已定型术语（NO_REAL_BUYER / evidence packet） | Feature/doc | Direct | 待执行 |
| T2 | Stop-Boundary 合规审计：核查 373ee7c..HEAD 无优化阶段项泄漏（model routing 内部/成本/上下文编译/缓存/压缩/调度） | Audit | Delegate（独立复核价值真实） | 待派发 |
| T3 | 维护 V2_DOGFOOD_EVIDENCE friction 簿（贯穿） | 观察 | Direct | 已开始 |

## 路由判据记录（防「为 Gate 而 Gate」）

- T1：知识热、单文件 → Direct。
- T2：独立第三只眼复核本会话自身的提交——Leader 自证有偏差，真实委派价值。
- 覆盖对照：Feature（T1）+ Audit（T2）；Bug 类 dogfood 若期间自然出现则记录，
  不制造。

## 已知摩擦（初始，详见 friction 簿）

- worktree `.git` 陷阱（17 guard 失败，影响 worktree 内委派）。
- Phase 1 Path D 的委派价值偏弱（为覆盖而委派的 ceremony 风险——Phase 4
  起路由只按真实判据）。
- grilling / grill-with-docs 尚无真实 buyer 触发（未强迫使用）。
