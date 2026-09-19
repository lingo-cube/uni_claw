# CORE-003 Grill With Doc 结果

日期：2026-09-19

## 检查范围

本轮对照了 vNext.1 Core 设计、Core 抽取对齐材料、场景枚举库 v0.1、CORE-001
锁定基线，以及仓库中的 Core、Kernel projection、Terminal Outcome、Simulation
和异步感知测试。目标是反驳 Core 对象集，不以旧类签名决定 Core。

## 结论

没有发现必须新增 Core 顶层对象的明确反例。当前对象集和执行层次保持：

```text
Clause
Segment ── Slice
Evidence / Claim / Event（Event 暂以组合语义表达）
Effect ── Attempt ── TargetBinding
```

Event 的发生、来源、时间、参与者、次数、同一性和因果判断，当前可由 Evidence +
Claim 表达。场景库中关于自主变化、时钟不一致、巧合因果、延迟结果和反馈环的案例，
要求保留依据和不确定性，但没有证明必须建立独立 Event 类型。

Expectation、Verification、OpenDuties、Outcome、异步操作生命周期和运行终止判断，
属于评价或 Runtime 配套契约，不上提为新的 Core 顶层对象。

## 支持当前对象集的材料

| 语义 | 材料/测试证据 | 结论 |
|---|---|---|
| Segment 持续引用、Slice 重叠/包含/重复、旧历史不覆盖 | vNext.1 §6.4–§6.10；场景 4–8、13–18、38–42；CoreObjectRelationTests、CoreProjectionSeamTests | 保持 |
| Evidence 与 Claim 分工，缺少观察不等于不存在 | vNext.1 §6.11–§6.14、§6.19–§6.22；异步感知和对抗证据测试 | 保持 |
| Event 组合表达发生和因果 | vNext.1 §6.15–§6.18；场景 3、60、97、111、延迟结果；Event 组合测试 | 保持组合，独立类型待反例 |
| Effect、Attempt、TargetBinding 分层 | vNext.1 §7.1–§7.10；场景 19–30、91–96；Core projection 与 TerminalOutcomeTests | 保持 |
| Unknown、Stale、Ambiguous、Unauthorized、Unverified 不升级 | vNext.1 §7.2、§7.9；场景 2、19、20、26、66、91、119、120；fail-closed 测试 | 保持 |
| Expectation/Outcome/运行结束不成为 Core 事实 | vNext.1 §7.2、§8–§10；TerminalOutcomeTests、Simulation tests | 保持配套边界 |

## 字段与使用契约缺口

以下是待裁决缺口，不是新增对象的依据：

1. `TargetBinding` 当前只有可选 `BasisSliceId` 和不透明定位值。vNext.1 §7.7、§8.1
   要求至少一项可解析的固定依据，依据可以是 Evidence、Claim、Slice 或固定资源
   版本。当前尚未建立通用 BasisRef/固定资源版本契约。依赖历史复核、文件版本、
   API 资源或标定参考系的用途，在该保证缺失时必须阻塞，不能把引用塞进 LocatorKey
   后放行。
2. `Effect/Attempt` 当前还不能完整表达当次请求快照、执行端、授权依据，以及发送
   进展、外部执行判断、协调状态三者并存。场景 19、20、21、24、26、27、62、66、
   91–96、122 支撑该缺口。依赖崩溃恢复、幂等重试、迟到结果或多维状态判断的用途，
   在契约缺失时必须阻塞。
3. Slice 的参考系、单位、覆盖和上下文仍需要可解析的领域契约；不能通过通用
   `freshness`、`confidence` 或 `trustScore` 字段掩盖缺失。
4. 当前 Kernel projection seam 仍要求 Slice 依据，不能投影固定非 Slice 依据；这是
   seam/实现对齐缺口，不改变 Core 对象数量。

## 处理决定

- 保持 CORE-003 的 Core 对象集，不新增独立 Event、Expectation、Verification 或
  OpenDuties 对象。
- Event 继续使用 Evidence + Claim 组合，直到出现无法保持多来源、时间、同一性、
  因果或历史演化的具体反例。
- 字段和使用契约缺口暂不扩张 Core；后续用实际模型和场景确定最小 BasisRef、请求
  快照和执行记录保证。
- 旧模型只作为对齐证据，不作为 Core 字段、继承树或对象数量的裁决者。
- 缺少某项保证时，只阻塞依赖该保证的用途，不把局部缺口升级为全项目阻塞。

## 状态

本轮 `grill-with-doc` 完成。没有新增 Core 顶层对象，没有修改旧模型，没有源码迁移。
下一步进入旧模型责任对齐准备，而不是立即补齐所有字段或建立新对象。
