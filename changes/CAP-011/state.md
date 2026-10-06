# CAP-011 — 感知维度 R5 可替换性执法：L1 双协议面上的 L2 替换测试
lifecycle_state: closed · disposition: none · depth: standard · base: 63804f7a

## Intent（WHAT/WHY）

CAP-010 D2 预告的跟进项，前提已由 CAP-009（CLOSED）创造：uni.perception 有了
实现 `ISemanticPerception + IUiElementPerception` 双协议的 L2 可执行实现
（`UniPerceptionCapability`）。所有者澄清（2026-10-07）：**感知维度的替换
测试，替换发生在 L2 实现类，消费闭包必须钉死在两个 L1 协议面
（ISemanticPerception + IUiElementPerception）上**——不混入其他维度。

补上 skill 协议质量门 R5 在感知侧缺位的确定性执法：同一消费闭包消费两个
L2 实现，协议契约互换成立；L2 替换只动组合根注册，消费方零改动。

## Scope / Out of Scope

In：`tests/UniClaw.Kernel.Tests/Capability/` 新增感知 R5 测试；不改产品源码。
Out：协议负载词汇（SemanticObservationProposal/PerceptionAssessment，CAP-009
明确留给下一个感知买方）；slow.visual 实例化；Kernel 公开面（白名单不受影响）。

## Decisions

- D1（所有者澄清落地）：消费闭包签名只出现 `ISemanticPerception` +
  `IUiElementPerception`（语义买方 + UI 元素买方两个视角消费同一实例），
  健康观测经 `ICapabilityHealthCheckable` mixin（CAP-009 冻结的聚合语义，
  当前唯一可观察行为面）。闭包内不出现任何 L2 具体类型。
- D2：两个 L2 实现——产品实现 `UniPerceptionCapability`（生产形状：fast
  资产面 + 模型端两源）与确定性 replay 形状 L2 替换件（测试程序集内的
  test double；先例：ModelManagementTests.NonModelManagementCapability）。
  替换件内部结构刻意不同（字典驱动、Max 聚合，非 Any 链），证明替换不
  要求复制实现形状，只要求遵守同一协议契约。
- D3：可互换契约 = 身份一致性（同一实例双面）+ L0 自述双协议声明 +
  健康聚合律（worst-of、Healthy+Unknown 混合→Degraded、探针异常→诚实
  Degraded）。聚合律是协议契约的一部分，与 L2 是谁无关——对产品实现
  独立成立，对替换件互换成立，二者都不是自我循环。
- D4：registry 腿：两个 L2 各自在 fresh Product Registry 注册并 Resolve
  取回，闭包经取回实例运行；替换件的注册同时首次行使
  ValidateImplementation 对非生产 L2 的协议-接口一致性执法。

## Acceptance

| # | 判据 |
|---|---|
| A1 | 同一消费闭包（只写 L1 双协议面）在产品 L2 上契约成立 |
| A2 | 同一闭包在 replay 形状 L2 替换件上契约互换成立 |
| A3 | L2 替换只经组合根（fresh registry 注册 + Resolve 取回），闭包代码零改动；替换件注册经协议-接口一致性执法 |
| A4 | capability 全套 + DocsMetadata + 白名单绿；全量无回归 |

## Constraints

- 不修改 Kernel/Host 产品源码与公开面。
- 遵循 CAP-009 的语义冻结：不预造协议负载词汇。

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "dotnet test tests/UniClaw.Kernel.Tests --filter 'FullyQualifiedName~Capability|FullyQualifiedName~DocsMetadata|FullyQualifiedName~KernelRuntimeSurfaceWhitelist'"
  expected: "全部通过（含新增感知 R5 测试）"
  actual: "专项 105/105 绿（PerceptionProtocolReplaceabilityTests 3 例）；全量 solution 1389/1389 绿；白名单不受影响（未改产品源码与公开面）"
  evidence: "evidence/cap-011/"
```

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 所有者澄清替换维度=L2 背后
  的 L1 双协议面；CAP-010 D2 预告项开立为 CAP-011。
- 2026-10-07 · PERSIST → IMPLEMENT → VERIFY → CLOSED · 感知 R5 测试 3 例落地
  （消费闭包只写 L1 双协议面；产品 L2 + replay 形状替换件互换；替换只动
  组合根）；专项 105/105、全量 1389/1389 绿；evidence 与 INDEX 已再生。
