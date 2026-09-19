# CORE-011 — 测试侧 ReliableExecutionSourceFixture 受控实现（首切片）

lifecycle_state: closed · disposition: none · depth: standard · base: working-tree
triage_label: done
parent_change: CORE-010
authorization: granted-by-human (2026-09-19, 单独授权完整首切片)

## Intent

在已授权范围内实现 CORE-010 仿真契约（`docs/design/
core-execution-record-simulation-contract-v0.1.md`，APPROVED）的测试侧
`ReliableExecutionSourceFixture`：Host A/B 共享的可靠执行源仿真 +
确定性 crash cut point 注入 + S1–S12 验收场景，证明可靠执行记录契约
在单进程崩溃/重启模型下可被确定性验证。

## Scope（授权边界）

- 新增 `tests/UniClaw.Simulation.Tests/ReliableExecutionSourceFixture.cs`
  （fixture + guarded dispatch 序列，契约 §4 行为术语的测试侧实现）。
- 新增 `tests/UniClaw.Simulation.Tests/ReliableExecutionRecordTests.cs`
  （S1–S12 验收场景）。
- `SimulationHost.Compose` 增加可选 `executionSource` 注入参数并把同一
  fixture 交给 Host A / Host B（契约 §3；仅测试组合层）。
- `PerCycleZeroDisciplineTests` 的扫描清单纳入新测试文件（纪律自证）。

## Out of Scope（禁止，未授权）

- 不修改 `src/` 任何产品代码：Runtime、`EffectBoundary` 构造、Trace、
  存储、Core 模型。
- 不把 fixture 名称/接口冻结为公共产品接口；不实现真实持久化。
- 不触发真实外部操作；不主张真实 OS 崩溃、磁盘损坏或跨节点恢复已证明。
- 仿真通过 ≠ 产品 Runtime 已集成可靠执行源（产品集成是后续单独授权）。

## Decisions

1. guarded dispatch 序列在测试侧复刻 UniKernel.Act 的目标序
   （Bind→Judge→[PrepareCommit→gate/driver→Append]），把 fixture 插在
   gate 通过、driver 调用之前——正是 CORE-010 计划 §3 Runtime seam 的
   仿真位置；真实投递仍经 `EffectBoundary.Dispatch` 产品 seam。
2. crash = 确定性故障注入：序列停在命名 cut point，测试丢弃 Host A
   内存组合、保留 fixture、重组合 Host B（不杀进程）。
3. `RunId`（契约内容确定性派生）作为 FindPending 的声明范围键；
   Host A/B 同 bundle → 同 RunId，符合契约 §3“RunId 仅作关联提示”。
4. fixture 只存不可变数据（CanonicalBinding/AssuranceJudgment/
   EffectReceipt 值记录 + 追加条目），不持有 host 引用——跨 Host 存活
   不依赖 Host A 内存对象。
5. pending 判定 = 已越过 commit-success 但无确认 Receipt
   （CommittedPending / CommitUnknown / Dispatched / UnknownOutcome）；
   Registered-only（未提交）与 CommitFailed、已确认 Completed 不可发现
   为未决——对应 S2/S4/S7 的“无可发送/不重复投递”口径。

## Acceptance（= 仿真契约 §6 S1–S12）

S1 提交成功→恰好一次 driver 调用；S2 提交失败→零调用；S3 提交未知→
零调用且原关联可查询；S4–S7 四个 crash cut point 重启语义；S8 迟到
Receipt 追加不覆盖；S9 本地提交重试保持原 Attempt；S10 外部重试新
Attempt；S11 补偿新 Effect+新 Attempt；S12 Trace 关闭/写入失败不影响
恢复结果。全部经 xUnit 断言 + evidence 留痕。

## Verification

```yaml
level: SCENARIO
method: dotnet test UniClaw.Kernel.slnx（新场景 + 全量回归）
expected: S1–S12 全绿；既有测试零回归；git diff --check 通过
actual: >
  已通过：Simulation.Tests 125/125（新增 13 个验收方法）；
  全解决方案 544/544（Core 17 / Agent 13 / Simulation 125 / Kernel 389）；
  git diff --check 通过；src/ 零改动
evidence: evidence/2026-09-19-core-011-reliable-execution-source-fixture.md
```

## Status log

2026-09-19 · created · 用户单独授权 CORE-010 测试侧 fixture 完整首切片
（fixture + Host 注入 + 4 cut point 场景 + 证据）；实现落本 Change，
CORE-010 保持规划 Change 不变。
2026-09-19 · implemented · RED（fixture 类型缺失构建失败）→ GREEN；
迭代中两处真实失败留痕（AppendSubmission 缺 Dispatched 迁移、S08 计数
断言）见 evidence §3。S1–S12 全绿，全量 544/544 零回归。
2026-09-19 · closed · 验收 = 仿真契约 §6 S1–S12 全部经确定性测试证明；
范围未越界（src/ 零改动、EffectBoundary 构造未动、fixture 未成为产品
接口）；无未授权改动；evidence 已留痕。仿真通过不主张产品 Runtime 已
集成可靠执行源——产品侧接入是下一个独立 Gate。
