# Evidence — CORE-013 产品侧可靠执行源接入

> Change: `changes/CORE-013/state.md`
> Authorization: 2026-09-19 用户授权（范围 = CORE-012 计划 §5）
> Plan: `plans/2026-09-19-core-012-reliable-execution-source-integration.md`
> 上游证明: CORE-011 仿真（S1–S12）· CORE-012 四问裁决

## 1. 变更面（授权范围内）

| 文件 | 类型 | 内容 |
|---|---|---|
| `src/UniClaw.Kernel/Effects/EffectBoundary.cs` | 修改（授权点名） | 构造注入 `IReliableExecutionSource?`（可选，null = 既有行为零变化）；Dispatch 内 gate 通过、driver 之前提交；非 Success → `execution-commit-failed/unknown` gate reason（循 `delivery-closed` 先例）+ 零 driver 调用；driver 后追加失败不吞 delivery（catch IO/ObjectDisposed，记录停留未决） |
| `src/UniClaw.Kernel/Effects/ExecutionSource/ReliableExecutionSource.cs` | 新增 | 接口 + 契约类型（三值 CommitOutcome、Registration correlation 原语、只读投影 View、Entry/Status 枚举） |
| `src/UniClaw.Kernel/Effects/ExecutionSource/FileExecutionJournal.cs` | 新增 | 文件 append-only journal：长度帧 + UTF8 JSON；同步写 + Flush（提交边界判定点）；torn 尾帧截尾 = 从未提交；重放重建状态；BCL-only 零新依赖 |
| `tests/UniClaw.Kernel.Tests/ReliableExecutionJournalTests.cs` | 新增 | Slice 1：journal 契约 8 测试 |
| `tests/UniClaw.Kernel.Tests/EffectBoundaryExecutionSourceTests.cs` | 新增 | Slice 2：boundary 接入 7 测试（scripted double） |
| `tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverTests.cs` | 修改 | Slice 3：+1 透传测试 + FailingCommitSource double |
| `tests/UniClaw.Simulation.Tests/ReliableExecutionJournalRestartTests.cs` | 新增 | Slice 4：真实 journal 跨进程死亡恢复 7 测试 |
| `tests/UniClaw.Simulation.Tests/SimulationHost.cs` | 修改 | Compose 增加可选 `reliableExecutionSource`（产品源注入；与 CORE-011 fixture 参数分层） |
| `tests/UniClaw.Simulation.Tests/PerCycleZeroDisciplineTests.cs` | 修改 | 纪律清单纳入 Slice 4 文件 |

**零改动核对**：`UniKernel.cs`、`KernelRunDriver.cs`（reason 经既有
GateRejected 表达式透传——Slice 3 测试证明）、Trace 全家、Core 模型、
`BindingLog`/`ReceiptLog` 语义、csproj/slnx。

## 2. Q3 提交边界证明（逐条对应计划 §2）

| 计划条款 | 证明测试 |
|---|---|
| flush-后才-success；未 Dispose 终止后同机可重读 | `CommitSuccess_IsFlushedAndRecoverable_AfterTerminateWithoutDispose`（journal 实例丢弃 + GC + FileShare 重开） |
| torn 尾帧 = 从未提交；截尾不损坏 | `TornTrailingFrame_IsNeverCommitted_AndReplayContinues` |
| 提交路径同步无异步 | `FileExecutionJournal.TryWriteFrame`（同步 Write+Flush；无队列/后台写）；`CommitAfterDispose_ReportsFailure_NothingDurable` |
| 内存/异步实现不得报 success | 接口契约注释 + scripted double 显式三值（`EffectBoundaryExecutionSourceTests`） |
| fixture 通过 ≠ journal 恢复验证 | Slice 4 全部走真实文件 journal |

## 3. 场景映射（仿真契约 §6 → 产品证明位置）

| 场景 | 产品证明 | 位置 |
|---|---|---|
| S1 成功→恰好一次 driver | `CommitSuccess_AllowsExactlyOneDriverCall_AndAppends` | Kernel.Tests |
| S2 失败→零调用 | `CommitFailure_BlocksDriver_WithExecutionCommitReason` + Drive 透传 | Kernel.Tests |
| S3 未知→零调用可查询 | `CommitUnknown_BlocksDriver_WithExecutionCommitReason` | Kernel.Tests |
| S4 无可发送 | `S04_NoDispatchBeforeTerminate_JournalEmpty_NothingDiscoverable` | Simulation.Tests |
| S5 已提交未发送（无产品注入点） | journal 纯 prepare + torn 尾帧测试 | Kernel.Tests |
| S6 driver 后无 Receipt | `S06_AppendFaultAfterDriver_*`（追加故障包装器——正好端到端走产品「追加失败不吞 delivery」路径） | Simulation.Tests |
| S7 Receipt 还原不重复投递 | `S07_CompletedDispatch_ReceiptRestorable_*`（产品 receipt id 与 journal 还原一致） | Simulation.Tests |
| S8 迟到反馈/待关联 | `S08_HostBCoordination_*`（跨二次重启持久） | Simulation.Tests |
| S9 本地提交重试 | `LocalCommitRetry_KeepsAttemptIdentity_ZeroExternalDelivery`（契约面 double；journal v1 无 Unknown，见决策 2） | Kernel.Tests |
| S10/S11 重试/补偿关联 | `S10_S11_HostBCoordination_*`（跨二次重启持久） | Simulation.Tests |
| S12 Trace 两臂 | `S12_TraceDisabled/RecorderFailing_*` | Simulation.Tests |

## 4. TDD 留痕（RED → GREEN，失败保留为证据）

1. Slice 1 RED：journal 类型缺失构建失败；GREEN 迭代中三处真实失败：
   feedback 帧字段位次写反（FeedbackId/Note 错位）、dispose 后
   ObjectDisposedException 未计入失败判定、torn 测试 helper 多追加一次
   submission（测试侧修正）。
2. Slice 2 RED：boundary 构造/接入缺失；GREEN 迭代一处：枚举直映
   "failure" 与计划词汇 "failed" 不符 → 显式映射。
3. Slice 4：一处 CS0120（静态 helper 引实例路径）→ 去 static。

## 5. 验证声明

```yaml
level: SCENARIO
method: dotnet test UniClaw.Kernel.slnx 全量（+ 分 slice 过滤运行）
expected: 新测试全绿（8+7+1+7=23）；既有 544 基线零回归；git diff --check 通过
actual: >
  Core 13 + Agent 17 + Simulation 132（+7）+ Kernel 405（+16）= 567/567 全绿；
  544 基线零回归（405-16=389、132-7=125 与基线一致）；git diff --check 通过
evidence: 本文件 + 上述测试（确定性：临时目录 + 内容派生 id，零 wall-clock 断言）
```

## 6. 边界重申（不主张）

- 单机、单进程崩溃/重启范围；不主张 OS 崩溃/断电（fsync）、磁盘损坏
  恢复、跨节点已验证。
- 自动重试/补偿编排没有实现（接口面已备，buyer deferred——Control/Agent
  流是未来消费方）。
- `IReliableExecutionSource` 及成员名是实现轮命名，不是冻结公共 API。
