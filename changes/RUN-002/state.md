# RUN-002 — Live 闭环 Tracer Bullet（真机模拟器 × live 眼 + ADB 手 × 全链 trace）
lifecycle_state: closed · disposition: implemented · depth: standard · base: 4c2b6a4a

## Intent（WHAT/WHY）
眼（PER-005 live 感知）与手（ADB-001/002 真机效果）均已 live，但从未组合成
一次真正的自主运行闭环。本 change 在注册模拟器上打穿最小闭环：contract →
control（消费 live 观察）→ grounding → ADB tap → 再观察证实世界翻转 →
terminal 评估，全链真实 IRunTrace 收集（span 因果 + artifact 引用）。
另设 trace 缺口台账：过程中发现的「应补 trace / trace 实现不合理」逐条
记录（Human 指令），不顺手大改。

## Scope
- `tests/UniClaw.Kernel.Tests/Perception/LiveClosedLoopBulletTests.cs`
  （ENVIRONMENT，DSH_TEST_PERCEPTION_LIVE=1 门控 + fail-closed）：
  - 组合：EvidenceLedger + WorldModel（test-side live occurrence 策略：
    帧内 join 后派生 detect occurrences + 归一化 locator——PER-003 D2
    「真实策略 test 侧」先例）+ ControlLoop(DescriptorTargetPolicy) +
    RuntimeAssurance + EffectBoundary(AdbLiveEffectDriver) + 真实 trace
    （RunTraceFactory.BeginRun）。
  - 循环：观察（截屏→服务→derived artifact→proposals→join→Process）→
    DeriveSlice → SelectIntent → ActViaCurrentGrounding(TargetDescriptor)
    → receipt（真实 dispatch）→ 再观察 → revision 推进 + 景观变化断言
    （世界翻转经再观察证实，ADB-002 同款语义）→ EvaluateTerminal 如实记录。
  - trace 断言：span 全 Complete、感知 span 引用 derived artifact id、
    因果链覆盖 observe→act→re-observe。
- 本 state + evidence（含 trace 缺口台账）。

## Out of Scope（禁止）
- 产品代码修改（发现缺口只记录；确需修复另立 change）；switch state 读取
  （PER-005 §7.2 不开启清单）；性能断言；terminal 语义裁决。

## Acceptance
- A1（ENVIRONMENT）闭环执行：≥1 个 Act intent 经 grounding 真实 dispatch
  （receipt 非 null）。
- A2（ENVIRONMENT）世界翻转经再观察证实：tap 后 revision 推进且 occurrence
  景观 digest 变化。
- A3（DETERMINISTIC·trace）Finalize artifact：无 Incomplete span；感知
  span 引用本帧 derived artifact id；span 序列覆盖闭环两端。
- A4 EvaluateTerminal 结果如实记录（不伪造 terminal）。
- A5 全量回归零破坏。

## Verification
```yaml
verification:
  level: SCENARIO（ENVIRONMENT 主链 + trace 断言）
  method: >-
    ENVIRONMENT 主链（模拟器 × live 服务 × ADB 真驱动）+ trace 断言
    （span 全 Complete / 感知 span 引用 derived artifact）+ 全量回归。
  expected: A1–A5 满足；trace 缺口台账成文
  actual: >-
    1/1 GREEN（7s）：tap 真实 dispatch（DeliveryCompleted @ switch）、
    rev-2→rev-3 景观 509→446 再观察证实、terminal 如实（evidence-
    insufficient）、trace 9+ spans 因果可核验、全量 352/352 + 17/17。
    台账 6 条成文（观察组合缝缺失 / RunId 延迟绑定 / ownerless 静默 /
    span 无时长 defer⑪ / Quarantined 语义 / Process 诊断面正面）。
  evidence: evidence/2026-09-12-run-002-live-closed-loop.md
```

## Status log
2026-09-12 · enter→understanding→persisted · 组装面勘定（RunTraceFactory/
ActViaCurrentGrounding/AdbLiveEffectDriver/SpatialLocator 归一化）；test-side
join 策略设计（per-record 派生无法跨 proposal join bounds——帧级 master
proposal 先例 PER-003）；开实施。

2026-09-12 · persisted→implementing→closed · 五轮迭代修（join 长度设防/
  RevisionId 截断/seed lineage/组合缝 trace 注入/声明序），闭环全绿；
  台账 6 条留痕；模拟器与服务现场清理。
