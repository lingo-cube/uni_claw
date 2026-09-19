# Evidence — Core 最小候选基线验证

- Date: 2026-09-18
- Scope: 场景语义最小候选基线的一次有界验证
- Level: DETERMINISTIC + STATIC
- Status: SUPPORTED_WITH_GAPS
- Scope boundary: 只验证语义覆盖和现有测试行为；不进行源码迁移，不冻结最终字段、继承树或独立 Event 类型

## 1. 验证对象

当前候选基线（六种核心语义记录；不等于六个独立类型）：

```text
Specification
└── Clause

World
├── Segment
│   └── Slice（按局部观察、覆盖和历史绑定需要保留）
├── Evidence
├── Claim
└── Event（发生语义保留，独立表示待验证）

Effect
└── Attempt[]
    └── TargetBinding[]
```

待单独验证的表示或上层语义：Event 的独立表示、Revision、Projection、Expectation、Verification、OpenDuties、Outcome 和 Runtime 状态。

## 2. 验证方法

### 2.1 场景删除测试

将场景库按语义压力归并，不按场景数量建立模型。对每个候选概念尝试删除，检查是否仍能保持：

- 目标或对象是谁；
- 依据、时间范围和有效条件；
- 未知、冲突和否定的区别；
- 逻辑操作、实际投递和现实结果的区别；
- 历史版本、迟到信息和更正关系；
- 权限、责任和结束边界。

### 2.2 表达保持

比较精简前后的必要判断，而不是比较类型数量：

```text
q(original history) = q(compact representation)
```

其中 `q` 至少覆盖对象、依据、时间、未知/冲突、投递、现实结果和未闭合责任。

### 2.3 演化保持

加入新 Evidence、更正、撤销或迟到反馈，比较以下两条路径是否保持同一必要判断：

```text
先更新原模型，再转换
先转换为候选基线，再更新
```

## 3. 静态场景结果

| 语义 | 主要压力 | 结果 |
|---|---|---|
| Clause | 目标、权限、安全约束、无合法动作、对抗指令 | 候选必要；不能由 Evidence/Claim 自动产生权限 |
| Segment | 改名、移动、复制、替换、重渲染、跨观察连续性 | 候选必要；持续引用不等于现实身份确认 |
| Slice | 滚动、多视口、局部覆盖、元素集合、历史绑定依据 | Segment 下必要的条件性局部观察结构；不证明必须独立建库 |
| Evidence | 来源、时间、上下文、冲突、篡改、不可观测、迟到 | 候选必要；不能与 Claim 合并 |
| Claim | 属性、状态、身份、归属、因果、完成判断和不确定性 | 候选必要；不能把判断直接当现实 |
| Effect | 逻辑操作与现实结果分离 | 候选必要 |
| Attempt | 多次投递、部分执行、超时、Unknown、补偿、迟到回执 | Effect 内不可折叠职责 |
| TargetBinding | 当次目标、定位方式、固定依据、有效条件 | Attempt 内不可折叠职责；不强制 Slice/全局 World 版本/freshness 组合 |
| Event | 发生、次数、时间、参与者、同一性、延迟和更正 | 发生语义必须保留；独立 Event 类型尚未证明必要 |

## 4. 仓库动态验证

### 4.1 Kernel 语义测试

Command:

```text
dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --filter 'FullyQualifiedName~EvidenceToBeliefTests|FullyQualifiedName~ClaimEvolutionTests|FullyQualifiedName~UIWorldAssociationTests|FullyQualifiedName~UIWorldContinuityTests|FullyQualifiedName~UIWorldGroundingSeamTests|FullyQualifiedName~ControlToEffectTests|FullyQualifiedName~DispatchSeamSpecificationTests|FullyQualifiedName~TerminalOutcomeTests' --logger 'console;verbosity=minimal'
```

Expected:

- Evidence admission、relevance、冲突和 Claim 演化保持分离；
- Segment/Slice 连续性、歧义、stale 和 no-candidate fail-closed；
- Effect、Attempt、TargetBinding 分开；
- Completed、Failed、Unknown 和 recovery 分开；
- receipt、局部 Effect、迟到 Evidence 不直接推出或改写终局。

Actual:

```text
失败: 0，通过: 94，跳过: 0
```

Result: 通过。

### 4.2 Async Perception 场景测试

Command:

```text
dotnet test tests/UniClaw.Simulation.Tests/UniClaw.Simulation.Tests.csproj --no-restore --filter 'FullyQualifiedName~AsyncPerceptionScenarioTests' --logger 'console;verbosity=minimal'
```

Expected:

- Fast/Slow 修正、迟到、乱序、重复和 stale Evidence 不污染 World；
- Binding 随 Revision 失效；
- 取消后的迟到结果不复活；
- 终端状态不因迟到输入翻转。

Actual:

```text
失败: 0，通过: 15，跳过: 0
```

Result: 通过。

### 4.3 环境警告

两次运行均出现 `NU1900`：NuGet 漏洞缓存路径无写权限。编译和测试均完成，该警告记录为环境问题，不作为语义失败。

## 5. 关键证据引用

- [EvidenceToBeliefTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Kernel.Tests/EvidenceToBeliefTests.cs:60)：Evidence 接纳、relevance、无关证据和冲突 Revision。
- [ClaimEvolutionTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Kernel.Tests/ClaimEvolutionTests.cs:65)：reaffirm、revise、conflict 和历史演化链。
- [UIWorldAssociationTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Kernel.Tests/UIWorldAssociationTests.cs:51)：滚动连续性、相似外观、歧义和证据不足。
- [UIWorldContinuityTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Kernel.Tests/UIWorldContinuityTests.cs:27)：Revision-local occurrence、持续引用和历史边界。
- [UIWorldGroundingSeamTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Kernel.Tests/UIWorldGroundingSeamTests.cs:100)：唯一/无候选/多候选、stale 和重新绑定。
- [ControlToEffectTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Kernel.Tests/ControlToEffectTests.cs:116)：Effect、Attempt、Binding、Gate、Receipt 和 recovery 分离。
- [DispatchSeamSpecificationTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Kernel.Tests/DispatchSeamSpecificationTests.cs:100)：Completed/Failed/Unknown 和禁止 blind retry。
- [TerminalOutcomeTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Kernel.Tests/TerminalOutcomeTests.cs:167)：receipt 不等于 completion，迟到信息不改写终局。
- [Slice.cs](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Kernel/World/Slice.cs:20)：当前实现把 Slice 表达为某一 Revision 的不可变 scoped projection。
- [EffectDispatch.cs](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Kernel/Effects/EffectDispatch.cs:43)：投递结果与世界效果分离，Unknown 只能进入重新观察路径。

## 6. 结论

本次验证支持以下结论：

1. 当前材料支持 `Clause、Segment、Evidence、Claim、Effect` 作为最小候选核心记录。
2. `Attempt` 和 `TargetBinding` 是 Effect 内不可折叠的执行职责。
3. `Slice` 作为 Segment 下局部观察语义得到现有场景和测试支持；页面滚动遍历需要历史 `Slice[]` 或等价的可重建序列。
4. Slice 必须固定保存或可靠重建，不能仅因其是 Projection 就默认可以随时按最新规则重算替代历史依据。
5. Event 的发生语义不可删除，但当前没有证明独立 Event 记录不可由 Evidence + Claim + Revision/Runtime 关系无损表达。
6. 本结果是“当前材料支持的最小候选基线”，不是严格最小性证明，也不是源码迁移完成。

## 7. 未决项

1. Claim + Evidence 是否能无损表达事件次数、事件同一性、多因多果和延迟归属。
2. 历史 Slice 在存储或重建失败时如何满足历史 TargetBinding。
3. 页面多视口覆盖、元素出现时间和点击时间线的端到端场景尚未有专门测试。
4. 旧项目具体模型如何映射为 Core 实现、领域表示、投影、Adapter 或被删除，尚未裁决。

## 8. 变更范围

本轮只新增本验证记录；未修改产品源码、测试源码、指南、依赖或运行配置。
