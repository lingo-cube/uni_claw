# LOOP-001 — 闭环组合确定性孪生（RUN-002 的默认套件锁）
lifecycle_state: closed · disposition: implemented · depth: minimal · base: 0bc36a07

## Intent（WHAT/WHY）
RUN-002 的闭环组合逻辑（seed→观察→join→Process→DeriveSlice→SelectIntent→
ActViaCurrentGrounding→dispatch→再观察）只活在 ENVIRONMENT 门控内——kernel
重构可静默破坏闭环。本 change 用 doubles 复刻同一组合为默认套件确定性测试
（帧源 = corpus golden JSON，frame2 移除 switch 模拟世界翻转；效果驱动 =
Ok double；trace/metrics 真实注入，含台账#1 教训）。

## Scope
- `tests/UniClaw.Kernel.Tests/Perception/LoopTwinTests.cs`（1 用例，
  DETERMINISTIC）。
- 本 state（minimal：纯测试锁，无产品改动，无独立 evidence——state 内验证）。

## Acceptance / Verification
```yaml
verification:
  level: DETERMINISTIC
  method: 孪生用例（golden 帧含 switch → Act/dispatch/Receipt 完成 →
    frame2 移除 switch → revision 推进 + 景观变化 + 第二意图 Observe（消失
    后 fail-closed 不猜）+ trace span 全 Complete 含 artifact 引用 +
    metrics ≥2 帧）+ 全量回归
  expected: 上述全过；默认套件零环境依赖
  actual: >-
    1/1 GREEN（38ms）；全量 Kernel 353/353（+1 孪生）+ Agent 17/17。
  evidence: 本 state
```

## Status log
2026-09-12 · enter→closed · RUN-002 台账后的第一补强；复用子弹组合（join
  策略/seed 模式/trace 注入三教训全固化）；世界翻转用 label 移除模拟（比
  bounds 漂移更确定的断言面：switch@ 消失 + Observe 意图）。
