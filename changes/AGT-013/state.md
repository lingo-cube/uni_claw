# AGT-013 — Android Settings safety policy runtime contract

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: d1509162

## Intent（WHAT/WHY）

AGT-012 已建立 Android Settings 三项真实测试集，但 `safeActionSet` /
`forbiddenActionSet` 仍停留在静态 fixture，尚未进入首次 UniAgent 咨询前的
运行时上下文；Settings coverage runner 的 `ForbiddenEffects` 也仍为空。因此
任务三不能证明危险动作在 Runtime 层被拦截，任务二也没有统一的目标开关幂等
安全策略。

本 Change 建立最小可执行接缝：策略在首次咨询前由 Settings profile 加载并
校验，Prompt/Agent 只消费只读投影，Runtime 在 dispatch 前拥有最终拒绝权，
EffectBoundary 只接收通过 Runtime Guard 的动作，运行产物记录策略 digest 与
每个动作的判定。

## Scope / Out of Scope

### 范围

- `settings-coverage.yaml` 引用并要求安全策略 fixture；缺失或非法策略返回
  `PROFILE_CONTRACT_NOT_READY`，不发起咨询、不产生 effect。
- 增加策略模型与确定性校验：safe、targeted、forbidden action classes，禁止
  target patterns，unknown target fail-closed。
- 将策略摘要注入 Settings coverage 的 Agent context，并把 forbidden effect
  classes 进入 Execution Contract。
- 在 Settings coverage director 中对 Agent proposal 做最终目标/类别 guard：
  非目标 toggle、禁止模式、未知动作和不满足当前状态的重复 toggle 均在
  dispatch 前拒绝。
- 为任务一、任务二、任务三增加 deterministic contract/guard 验收，并记录
  policy digest、guard verdict 与 zero-forbidden-receipt 证据。

### 不在范围

- 不把 Prompt 设为安全 authority；不允许模型绕过 Runtime Guard。
- 不修改通用 Product/Kernel 协议的 authority 归属；只在 Settings coverage
  Host adapter 使用已有 contract/assurance 缝。
- 不把 deterministic double 结果当作真实模型能力；真实 GLM/DSH 和真机执行
  另列 ENVIRONMENT 验证。
- 不宣称 Android Settings 字面意义的全树穷举；仍以 manifest 的有界覆盖范围
  为准。

## Decisions

1. **策略来源**：profile 在首次咨询前加载已生成的 JSON policy fixture；策略
   缺失、版本不支持、集合为空或 digest 无法计算时 fail closed。
2. **authority**：profile 负责提供策略，Prompt Adapter 负责投影，UniAgent
   负责提出建议，Runtime Guard 负责最终拒绝，EffectBoundary 负责实际投递。
3. **动作语义**：`tap` + typed `DesiredState` 的目标控件被归类为 toggle；
   只有策略声明的目标 toggle 可以执行，目标状态已满足时返回 no-action。
4. **unknown**：未知 action 或无法唯一归类的 target 统一拒绝；拒绝不产生
   `DeliveryCompleted`。
5. **兼容边界**：已有纯 coverage ledger/AGT-005 单测使用未声明 policy 的
   临时配置保持可运行；真实 `.dsh/profiles/settings-coverage.yaml` 必须声明
   `required: true`，因此真实入口不会静默绕过策略。

## Acceptance

| ID | 用户可观察行为 | 预期与证据 |
|---|---|---|
| A1 | profile 在首次咨询前加载策略 | 合法 policy 有稳定 digest；缺失/非法/过期返回 `PROFILE_CONTRACT_NOT_READY`，咨询次数与 effect 数均为 0 |
| A2 | 策略进入 Agent 上下文 | context/objective 含 policy ref、digest 和只读动作集合摘要；每次 run 使用同一 digest |
| A3 | Runtime 最终执法 | 非目标 toggle、禁止 target、unknown action 在 dispatch 前拒绝；无 `DeliveryCompleted` |
| A4 | 目标开关幂等 | 当前状态已满足时 `NO_ACTION`；状态不满足时最多一个目标 toggle；重复 run 不反向切换 |
| A5 | 三项测试集可判定 | find-menu 零 effect 导航；toggle-reuse 有 post-action verification；safe-full-traversal 输出 coverage/route/effect/verification 且禁止项零 receipt |
| A6 | 证据可追溯 | run 产物记录 policy digest、proposal、guard verdict、receipt/拒绝原因；确定性测试与现有回归全绿 |

## Plan

1. 增加 Settings action policy 模型、JSON loader、digest 和 fail-closed profile
   读取；把真实 coverage profile 标为 `required: true`。
2. 在 coverage director 和 HostRunner 的 Agent seam 注入只读 policy projection，
   并在 dispatch 前复用同一个 guard 检查安全类别、禁止 target 和幂等 toggle。
3. 把 forbidden action classes 进入 Execution Contract，run facts/consult log
   记录 policy digest、guard verdict 和拒绝原因。
4. 增加缺失策略、禁止 target、unknown toggle、已满足状态和可执行目标的
   deterministic tests，随后跑 Host/DSH/Kernel/Simulation 回归。

## Verification

```yaml
level: DETERMINISTIC + SCENARIO
method: policy parser/fixture checks; missing-policy fail-closed tests; context projection tests; forbidden/unknown/idempotent guard tests; Settings coverage scenario tests; full affected test projects; git diff --check
expected: policy required profile cannot bypass the guard; safe actions pass; forbidden and duplicate toggles stop before dispatch; evidence carries one policy digest
actual: PASS：policy tests 7/7；Host.Tests 153/153；Kernel.Tests 794/794；Simulation.Tests 188/188；Agent.Dsh.Tests 132/132；Host.Dsh build 0 errors；manifest/schema/diff checks PASS
evidence: evidence/agt-013/verification-2026-10-06.md
```

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · 现有 Settings coverage 已有
  `AllowedEffects` 与 RuntimeAssurance effect-class 检查，但真实 profile 未注入
  policy，runner 的 `ForbiddenEffects` 为空；决策为 profile 提供、Runtime 拦截、
  EffectBoundary 投递。
- 2026-10-06 · PERSIST → PLAN → IMPLEMENT · 增加 required policy profile、稳定
  digest、Agent 只读 projection、SettingsActionGuard、Host/coverage facts 记录及
  缺失/禁止/unknown/幂等 deterministic tests。
- 2026-10-06 · IMPLEMENT → REVIEW → VERIFY → CLOSED · policy 7/7、Host 153/153、
  Kernel 794/794、Simulation 188/188、DSH 132/132、Host.Dsh build 通过；真实
  设备 ENVIRONMENT 验证尚未执行，保持为后续门。
