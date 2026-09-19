# CORE-010 可靠执行记录仿真契约 v0.1

> Status: `DESIGN / APPROVED`  
> Change: `CORE-010`  
> Authority: `NONE`  
> Implementation authorization: `NONE`  
> Contract reference: `docs/design/core-execution-record-contract-v0.1.md`

## 1. 目的与范围

本契约只定义受控实现前的测试侧仿真方式和验收口径，不实现可靠执行源，不修改产品
Runtime、Trace、存储或 Core 模型。

覆盖范围限定为：

- 单执行路径；
- 单进程崩溃后重新组合 Host；
- 可靠执行源 fixture 在 Host A 与 Host B 之间保持；
- 通过确定性故障切点检查发送前登记、未决发现、未知结果和迟到反馈。

不覆盖磁盘损坏、跨节点接管、真实设备、真实外部服务或 OS 级崩溃证明。

## 2. Owner 与权威边界

| 内容 | Owner | 约束 |
|---|---|---|
| Attempt 产品语义 | Core 契约 | 不新增 Core 顶层记录 |
| 可靠执行记录行为 | Runtime / EffectBoundary | 负责发送前固定、提交、发送关联和反馈追加 |
| 仿真恢复依据 | 测试侧 `ReliableExecutionSourceFixture` | 只服务验收，不成为产品事实源 |
| 外部调用计数与确定性返回 | `DeterministicEffectDriver` | 不保存跨 Host 状态，不证明外部世界结果 |
| 诊断记录 | Trace | 可关闭、可丢弃，不能作为恢复唯一依据 |

fixture 与 Trace、`AsyncPerceptionTracer`、摘要、Host 内存对象相互独立。fixture 的存在
不授权修改产品构造函数，也不预设未来生产接口名称。

## 3. Host A / Host B 仿真协议

```text
Host A = SimulationHost.Compose(bundle, options)
  → AdmitContract / Activate
  → 驱动到指定 crash cut point
  → 丢弃 Host A 的 Kernel、Runtime 和 Trace 内存对象

保留：测试侧 ReliableExecutionSourceFixture

Host B = SimulationHost.Compose(bundle, options)
  → AdmitContract / Activate
  → 不预先提供 Attempt ID
  → DiscoverPending / GetAttempt
  → 查询、协调或等待迟到反馈
```

`RunId` 可以作为关联提示，但不能被当作已持久化运行状态。Host B 不得从 Host A 的
`KernelRunDriver` phase、`EffectBoundary` 内存 binding/receipt、`RunModel` history、
`WorldModel`、`EvidenceLedger`、stimulus feed 或 driver 计数中推断恢复结果。

## 4. 测试侧 Fixture 行为契约

首个受控实现可以在测试程序集提供下列行为；名称仅为契约术语，不是已批准公共接口：

- `PrepareCommit`：提交完整准备集合，返回 `success`、`failure` 或 `unknown`；
- `FindPending`：不依赖 Attempt ID 发现声明范围内的未决记录；
- `GetAttempt`：按稳定关联还原 Effect、Attempt、请求、Binding、固定依据、执行端和准入依据；
- `AppendSubmission`：追加实际提交过程，不覆盖准备记录；
- `AppendReceipt`：追加 Receipt 或明确的无 Receipt 未决状态；
- `AppendLateFeedback`：追加迟到反馈；关联不明确时保留待关联；
- `LinkRetry` / `LinkCompensation`：分别表达同一 Effect 的再次投递和新 Effect 的补偿。

Fixture 必须满足：

1. 记录追加是历史保留，不覆盖原请求、Binding、依据或既有反馈；
2. `PrepareCommit` 未明确返回 `success` 时，不能越过 driver 调用边界；
3. `FindPending` 不要求调用方已经知道 Attempt ID；
4. 同一 Attempt 的本地提交重试不增加外部投递次数；
5. 外部重试创建新 Attempt；补偿创建新 Effect 及其 Attempt；
6. fixture 的恢复结果不依赖 Trace 是否启用或 Trace writer 是否丢弃记录。

## 5. 四个 Crash Cut Point

| 切点 | 仿真位置 | Host B 必须观察到 |
|---|---|---|
| `BeforePrepareCommit` | 准备尚未可靠提交 | 没有可发送 Attempt；不得解释为成功、失败或已发送 |
| `AfterPrepareCommitBeforeDriver` | 准备已提交，尚未调用 driver | 无 Attempt ID 也能发现未决 Attempt；完整准备集合可还原；不得盲重发 |
| `AfterDriverBeforeReceipt` | driver 已调用，Receipt 尚未追加 | 原 Attempt 保持未知；不得解释为成功或失败；不得自动新投递 |
| `AfterReceiptAppend` | Receipt 已追加并关联 | 可还原原 Attempt/Receipt；不得重复投递；迟到反馈只追加 |

每个切点都必须同时观察：可靠执行源状态、driver 调用次数、Receipt 数量、Trace 臂状态
和 Host B 的恢复结果。Trace 关闭或诊断写入失败只能作为对照条件，不能替代可靠执行源
断言。

## 6. 验收计划（尚未执行）

| 编号 | 验收场景 | 期望 |
|---|---|---|
| S1 | 提交成功 | 允许一次 driver 调用 |
| S2 | 提交失败 | driver 调用次数为零 |
| S3 | 提交未知 | 不调用 driver；原关联可查询 |
| S4 | `BeforePrepareCommit` 重启 | 无可发送 Attempt |
| S5 | `AfterPrepareCommitBeforeDriver` 重启 | 无 ID 发现未决 Attempt，driver 次数为零 |
| S6 | `AfterDriverBeforeReceipt` 重启 | 原 Attempt Unknown，不自动重发 |
| S7 | `AfterReceiptAppend` 重启 | Receipt 可还原，不重复投递 |
| S8 | 迟到 Receipt | 追加到原 Attempt，不覆盖历史 |
| S9 | 本地提交重试 | 保持原 Attempt，不增加外部投递 |
| S10 | 外部投递重试 | 新 Attempt，保留重试关系 |
| S11 | 补偿 | 新 Effect、新 Attempt，关联被补偿 Effect |
| S12 | Trace disabled / dropped | 恢复结果与可靠执行源一致 |

这些是验收计划，不代表测试已执行或通过。

## 7. 实现前最后一道 Gate

本契约通过后，仍需单独授权实现。实现授权前不得：

- 修改 `src/`、产品 Runtime、`EffectBoundary` 或 Trace；
- 新增生产存储或 Core 类型；
- 把 fixture 名称冻结为公共接口；
- 把仿真通过写成真实崩溃、持久化或恢复能力已证明。
