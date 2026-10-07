# DSE-004 — Effect Driver R5 可替换性执法：同一投递闭包 × 两个 IEffectDriver
lifecycle_state: closed · disposition: none · depth: standard · base: e71e1f2b

## Intent（WHAT/WHY）

Effects 维度抽样检查（CAP-010/011 之后的 R5 系列第三站）：`IEffectDriver`
缝（ADR/DSE-001 入参收窄；不变量 27「只 delivery 并如实报告」）有多个
realization（AdbLive/Adb/EgoBrowser/Simulation 的 Deterministic double），
但**无 R5 形状执法**——没有测试把同一消费闭包
（EffectBoundary gate→commit→Deliver→receipt→journal 链）喂给两个可互换
driver 并断言契约互换成立。AGENTS.md 1.5.3「缝的实现可替换（仿真 ⇄
真件），替换不改核心」在 effect 面缺确定性证明。

## Scope / Out of Scope

In：`tests/UniClaw.Kernel.Tests/Effects/EffectDriverReplaceabilityTests.cs`
（新）。Out：不改产品代码；不新增 driver；不动既有单 realization 测试
（GateMatrix/EgoBrowser/Adb/ExecutionSource 各自保留）。

## Decisions

- D1：两个 in-test driver double（结构刻意不同：无状态队列型 vs 有状态
  首败后成型），先例 EffectBoundaryExecutionSourceTests.CountingDriver；
  真件 driver 的自身契约由既有各 realization 测试 + Simulation
  SeamOverrideTests 覆盖，本测试证明**缝的可互换律**（同 Capability/
  Perception R5 先例：产品侧不可确定性实例化时用 double 对证明闭包不变）。
- D2：可互换律（闭包断言，与 driver 是谁无关）：① gate 判定不受 driver
  身份影响；② receipt 忠实映射 driver 三态 outcome（不伪造成功）；
  ③ 每次 Dispatch 恰一次 Deliver（**boundary 永不代 driver retry**，
  不变量 27）；④ receipt 逐次留痕、id 唯一；⑤ ExecutorId 如实记录
  driver 类型名（journal 溯源）。

## Acceptance

| # | 判据 |
|---|---|
| A1 | 同一闭包在无状态 OK driver 上契约成立（Completed 忠实映射） |
| A2 | 同一闭包在首败 driver 上契约成立（DeliveryFailed 诚实 receipt、无自动重试、二次投递成功轨迹完整） |
| A3 | driver 互换只换组合根构造参数，闭包代码零改动（law 断言对两 driver 同构成立） |
| A4 | Effects 全套 + Kernel 全量绿 |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "dotnet test Kernel.Tests --filter EffectDriverReplaceability + 全量"
  expected: "新测试绿；全量无回归"
  actual: "新测试 3/3 绿；Kernel 全量 849/849 绿（期间执法测试首次实战拦截并行会话新文件 RuntimeToolConfig，登记后复绿）"
  evidence: "evidence/dse-004/"
```

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 抽样确认 R5 缺口；
  DSE-004 开立。
- 2026-10-07 · PERSIST → IMPLEMENT → REVIEW → VERIFY → CLOSED · 首版
  闭包含反射与无效断言，Review 重写为 ICountingDriver 干净版；3/3 +
  全量 849/849 绿；执法测试首次实战拦截（RuntimeToolConfig 登记）；
  INDEX 已再生。
