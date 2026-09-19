# Evidence — CORE-011 测试侧 ReliableExecutionSourceFixture 受控实现

> Change: `changes/CORE-011/state.md`
> 授权: 2026-09-19 用户单独授权（完整首切片：fixture + SimulationHost 注入
> + crash cut point 场景 + 证据；`src/` 不动）
> 契约: `docs/design/core-execution-record-simulation-contract-v0.1.md`（APPROVED）
> 计划: `plans/2026-09-19-core-010-reliable-execution-record.md`

## 1. 变更面（授权边界内核对）

| 文件 | 类型 | 内容 |
|---|---|---|
| `tests/UniClaw.Simulation.Tests/ReliableExecutionSourceFixture.cs` | 新增 | fixture（PrepareCommit/FindPending/GetAttempt/AppendSubmission/AppendReceipt/AppendLateFeedback/LinkRetry/LinkCompensation/RetryCommit）+ guarded dispatch 序列（crash cut point 注入） |
| `tests/UniClaw.Simulation.Tests/ReliableExecutionRecordTests.cs` | 新增 | S1–S12 验收场景（13 个测试方法） |
| `tests/UniClaw.Simulation.Tests/SimulationHost.cs` | 修改 | `Compose` 增加可选 `executionSource` 参数 + `ExecutionSource` 属性（契约 §3：同一 fixture 交给 Host A/B；null = 不影响既有场景） |
| `tests/UniClaw.Simulation.Tests/PerCycleZeroDisciplineTests.cs` | 修改 | 纪律扫描清单纳入两个新文件（零 per-cycle 内部调用自证） |

`src/` 零改动（产品 Runtime、EffectBoundary、Trace、存储、Core 模型未触碰；
`EffectBoundary` 构造函数未改；fixture 未成为产品接口）。

## 2. 仿真实现要点

- **Host A 基线**：`TwoStepMissingMiddleEvidence` bundle，Compose(+fixture) →
  Admit → Activate → DriveOnce：step1 经真实产品路径 dispatch（driver=1），
  不变量 43 屏障等待 post-action 证据——非 terminal、delivery 未关闭，
  world belief 存在。guarded attempt 因此可用真实
  `WorldModel.DeriveBindingView` / `EffectBoundary.Bind` /
  `RuntimeAssurance.Judge` / `EffectBoundary.Dispatch` 产品 seam。
- **guarded 序列**（CORE-010 计划 §3 Runtime seam 的测试侧仿真）：
  Bind → Judge → [cut1] → PrepareCommit（非 Success 不越过 driver 边界）
  → [cut2] → EffectBoundary.Dispatch（gate+driver+Receipt 真实产品 seam）
  → AppendSubmission → [cut3] → AppendReceipt → [cut4]。
- **crash 语义**：在命名 cut point 停机，测试丢弃 Host A 内存组合
  （`hostA = null!`），仅 fixture 存活；Host B = Compose(同 bundle, 同
  fixture) + Admit + Activate，**不 Drive**，不带 Attempt ID 恢复发现。
- **跨 Host 关联**：RunId 由契约内容确定性派生，Host A/B 同 bundle →
  同 RunId，`FindPending(runId)` 即契约 §3 的声明范围发现。
- **pending 判定**：已越过 commit-success 且无确认 Receipt
  （CommittedPending/CommitUnknown/Dispatched/UnknownOutcome）；
  从未提交（cut1）在 fixture 中不存在，CommitFailed 与确认 Completed
  不可发现——S4「无可发送」与 S7「不重复投递」的结构保证。

## 3. TDD 留痕（RED → GREEN）

1. **RED**：先写 S1–S12 测试；构建失败（CS0246：fixture 类型不存在）
   ——新 seam 的红灯证据。
2. **GREEN 迭代中的失败（保留为证据，不删）**：
   - S06 失败：crash AfterDriverBeforeReceipt 后状态为
     CommittedPending 而非 Dispatched——`AppendSubmission` 缺
     「driver 已调用」的状态迁移。修复：AppendSubmission → Dispatched。
   - S08 失败：断言尾部 Entries 计数写错（2 vs 实际 3：submission +
     receipt + late feedback）。修复测试断言。
   - 另修两处机械问题：字符串内插条件表达式需括号（CS8361）；
     `DispatchGuarded` 参数含 internal `SimulationHost` → 方法收为
     internal（CS0051）；`AttemptState` 由 record 改可变类（CS8852）。

## 4. 验证声明

```yaml
level: SCENARIO
method: >
  dotnet test UniClaw.Kernel.slnx（全量）+
  dotnet test UniClaw.Simulation.Tests --filter ReliableExecutionRecordTests（逐场景）
expected: >
  S1 提交成功→恰好一次 driver 调用（1 基线+1）且 Receipt 关联；
  S2 提交失败→driver 零调用、无可发送 Attempt；S3 提交未知→零调用、
  原关联可查询；S4 cut1 重启→fixture 空、无可发送；S5 cut2 重启→
  无 ID 发现未决（CommittedPending、完整准备集合还原、零投递）；
  S6 cut3 重启→Dispatched/Unknown 保持、零自动重发、显式无 Receipt
  登记后仍未决；S7 cut4 重启→Receipt 还原、脱离 pending（不重复投递）；
  S8 迟到反馈追加不覆盖准备集合、关联不明保留待关联；S9 本地提交重试
  保持原 Attempt 零外部投递；S10 外部重试新 Attempt 保留 RetryOf；
  S11 补偿新 Effect+新 Attempt 关联 Compensates；S12 Trace
  Disabled/FailingRecorder 两臂恢复结果一致。
actual: >
  UniClaw.Simulation.Tests 125/125（含新增 13 个验收方法）；
  全解决方案 544/544（Core 17 / Agent 13 / Simulation 125 / Kernel 389，
  DocsMetadataTests 4/4 在内）；git diff --check 通过。
evidence: 本文件 + 上述测试方法（可重复执行，零 wall-clock/零随机）
```

## 5. 边界重申（不主张）

- 仿真通过 ≠ 产品 Runtime 已集成可靠执行源：产品侧接入
  （EffectBoundary.Dispatch 内的执行源 seam）仍是后续单独授权的实现。
- 不主张真实 OS 崩溃、磁盘损坏、跨节点接管或真实外部服务恢复已验证。
- fixture 名称/接口不是已批准公共接口；NU1900 既有包源告警与本次无关。
