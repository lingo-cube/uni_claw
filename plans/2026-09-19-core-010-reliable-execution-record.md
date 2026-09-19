# Plan — CORE-010 可靠执行记录首个受控实现切片

> PlanType: `CONTROLLED_IMPLEMENTATION_PLAN`  
> Status: `PLAN / APPROVED; IMPLEMENTATION_AUTHORIZATION_REQUIRED`  
> Change State: `changes/CORE-010/state.md`  
> Contract: `docs/design/core-execution-record-contract-v0.1.md`  
> Simulation contract: `docs/design/core-execution-record-simulation-contract-v0.1.md`  
> Implementation authorization: `NONE`

## 1. 计划边界

这是实现规划，不是实现授权。第一切片只覆盖单执行路径、进程崩溃与重启范围；不承诺
磁盘损坏、跨节点接管、真实设备或外部服务恢复。

禁止：新增 Core 顶层对象、把 Trace 改成恢复权威、把 Receipt 当 Attempt、实现独立
数据库、修改真实发送行为。

## 2. 现有 seam 与复用判断

| 现有位置 | 可复用内容 | 当前缺口 |
|---|---|---|
| `EffectBoundary.Bind` / `BindingLog` | binding 判定、拒绝原因、固定 revision 关联 | 不是完整 Attempt；没有请求快照和发送前可靠提交确认 |
| `EffectBoundary.Dispatch` | 唯一 gate/driver 调用 seam、Binding/Receipt 关联 | driver 调用前没有可靠执行记录提交点；Receipt 仅在返回后产生 |
| `EffectBoundary.ReceiptLog` | 发送后回执和 UnknownOutcome 关联 | 无 Receipt 时没有可发现的 Attempt 记录 |
| `UniKernel` / `KernelRunDriver` | Runtime 编排与恢复 phase 入口 | phase 和 receipt 当前是运行内存状态，不证明可恢复执行源 |
| `RunTraceArtifact` / `AsyncFileTraceWriter` | 诊断引用、耗时、丢弃诊断 | 可关闭、可丢弃、非权威，不能作为唯一执行源 |

最高现有 seam 仍是 `EffectBoundary.Dispatch`；不要在 Trace 层另造一条发送路径。

### 2.1 仿真重启审计结论

`SimulationHost.Compose`（`tests/UniClaw.Simulation.Tests/SimulationHost.cs`）每次都会
重新组合 `WorldModel`、`EvidenceLedger`、`RunModel`、`EffectBoundary`、`UniKernel`、
`KernelRunDriver`、`DeterministicEffectDriver` 和 Trace。它可以作为 Host A/Host B 的
确定性重建入口，但当前没有恢复输入或状态注入参数；Host B 只能得到空的新组合，不能
自动还原 Host A 的运行进展。

因此，当前仿真已经能模拟“丢弃 Host A 并重建 Host B”，但还不能模拟“重启后恢复可靠
执行记录”。可靠执行源 fixture 应放在 `tests/UniClaw.Simulation.Tests/` 的测试组合层，
由 Host A/B 共享，独立于 `RunTraceScope`、`AsyncPerceptionTracer`、摘要和 Host 内存对象。
本轮不为此修改 `EffectBoundary` 构造函数，也不把 fixture 提前做成产品 Runtime 接口。

当前不能跨 Host 自动恢复的内存状态包括：`KernelRunDriver` phase、`EffectBoundary`
binding/receipt、`RunModel` history、`WorldModel`/`EvidenceLedger`、`UniKernel` activation
latch、stimulus feed、scripted agent、stage metrics 以及 `DeterministicEffectDriver`
计数。`RunId` 可用于关联，但不等于状态已持久化。

## 3. 垂直切片

```text
准备登记 → 可靠提交结果 → EffectBoundary gate → driver 调用
    ↓                 ↓              ↓             ↓
Attempt/执行源    success/fail/unknown  内容一致性   Receipt/Unknown
    ↓
恢复发现（可不带 Attempt ID）→ 查询/协调/迟到反馈关联
```

### 行为

1. 准备记录包含 Effect、Attempt、固定请求、Binding、依据、执行端和准入关联；登记不等于发送。
2. 提交成功才允许越过可能产生外部影响的 driver 边界。
3. 提交失败不得调用 driver；提交结果未知只能沿原关联查询/协调。
4. driver 已调用但无 Receipt 时，已有 Attempt 保持未决并可被恢复发现。
5. 迟到 Receipt 追加到原 Attempt；不覆盖原请求、Binding 或历史评价。
6. 同一逻辑干预的外部重试是新 Attempt；改变逻辑含义或补偿是新 Effect。

### 模型/契约

不先冻结类名。先定义一个最小执行源接口所需的行为契约：

- `Prepare`：登记完整、固定、可还原的准备信息；
- `CommitResult`：明确返回成功、失败或未知；
- `DiscoverPending`：在未知 Attempt ID 时枚举声明范围内的未决记录；
- `AppendSubmission` / `AppendReceipt` / `AppendCorrection`：追加过程和反馈，不覆盖历史；
- `LinkRetry` / `LinkCompensation`：分别表达同一 Effect 的重试和新 Effect 的补偿关联。

这些名称只是规划词，不是已批准的公共接口。

### Runtime seam

未来实现优先在 `EffectBoundary.Dispatch` 的 gate 通过、driver 调用之前接入执行源：

1. 固定并发布本次请求、Binding、执行端、准入依据；
2. 等待可靠提交结果；
3. 仅在明确成功时调用现有 `IEffectDriver.Deliver`；
4. 将 driver 结果或缺失结果追加到同一执行关联；
5. 恢复由执行源发现未决记录，再进入既有查询/重新观察路径。

`KernelRunDriver` 的 phase 状态不得直接冒充持久执行源；`RunTraceArtifact` 只作为可选
诊断引用。

### 测试切片

首个实现 Change 应先加入确定性 fake execution source 和 driver spy，覆盖：

1. 提交成功 → 允许 driver；
2. 提交失败 → driver 调用次数为零；
3. 提交未知 → driver 调用次数为零，原 Attempt 可查询；
4. 准备未提交崩溃 → 不产生可发送 Attempt；
5. 准备已提交、发送前崩溃 → 可发现未决 Attempt；
6. driver 调用后无 Receipt → 原 Attempt Unknown，不创建伪造失败；
7. 迟到 Receipt → 关联原 Attempt，不覆盖原数据；
8. 本地提交重试保持原 Attempt，外部重试创建新 Attempt；
9. 补偿创建新 Effect 和新 Attempt；
10. Trace 禁用或队列丢弃不影响可靠执行源发现。

### 证据

实现 Change 需要分别记录：

- 执行源提交结果与 driver 调用计数；
- 无 Attempt ID 的 pending discovery 结果；
- 请求/Binding/执行端/准入依据的一致性断言；
- Receipt 丢失、迟到关联和 Unknown 保持；
- Trace disabled/dropped 与可靠执行源的独立性。

## 4. 实现前 Human Gate

进入 IMPLEMENT 前必须复审并明确：

1. 执行源 Owner 是否仍由 EffectBoundary/Runtime 承担；
2. 复用现有记录是否能提供可靠提交和 pending discovery；
3. 第一版持久性范围是否仅为单进程崩溃/重启；
4. `Prepare/CommitResult/DiscoverPending` 的最小接口是否足够且不成为第二事实权威。

本计划未获得上述实现授权前，不修改 `src/` 或 `tests/`。

## 5. 仿真崩溃/重启方案（仅计划）

可以用确定性 fault injection 模拟，不需要真实杀进程：

```text
Simulation Host A
  → 在固定 cut point 注入 Crash
  → 丢弃 Host A 的 Kernel/Runtime 内存组合
  → 保留契约声明的可靠执行源 fixture
  → 重新 Compose Host B
  → 在不依赖 Attempt ID 的情况下 DiscoverPending
```

建议最小 cut point：

1. `BeforePrepareCommit`：准备尚未可靠提交；恢复后不得出现可发送 Attempt；
2. `AfterPrepareCommitBeforeDriver`：准备已提交但尚未进入 driver；恢复后必须发现未决 Attempt；
3. `AfterDriverBeforeReceipt`：driver 已调用但 Receipt 未落；恢复后原 Attempt 保持 Unknown；
4. `AfterReceiptAppend`：Receipt 已关联；重启后不得重复创建同一外部 Attempt。

仿真必须使用独立于 Trace 的可靠执行源 fixture。`DisabledRunTrace`、Trace 队列丢弃或
`AsyncFileTraceWriter.FaultAfterRecords` 只能验证诊断采集故障，不能单独证明执行恢复。
同样，`KernelRunDriver` 在同一对象上继续 `Drive()` 只能证明运行内 phase resume，不等于
进程重启后的恢复。

仿真验收仍不代表真实 OS 崩溃、磁盘损坏或跨节点接管已经验证；它只证明契约在声明的
单进程崩溃/重启模型下可被确定性反驳或通过。

### 5.1 仿真 seam 的后续实现边界（仍未授权）

受控实现若获授权，优先在测试侧引入最小 `ReliableExecutionSourceFixture`，仅提供
`PrepareCommit`、`FindPending`、`GetAttempt`、`AppendSubmission`、`AppendReceipt` 和
`AppendLateFeedback` 等行为；这些是规划词，不是已批准接口。`SimulationHost` 负责把同一
fixture 交给 Host A 与 Host B，`DeterministicEffectDriver` 继续只负责确定性外部调用计数
和回执，不承担跨 Host 状态。

`AsyncPerceptionTracer` 的 `DisabledRunTrace` 变体只能作为“Trace 关闭仍可运行”的对照，
不能承担恢复存储；`AsyncFileTraceWriter.FaultAfterRecords` 只能验证诊断采集故障，不能
证明执行记录可靠性。
