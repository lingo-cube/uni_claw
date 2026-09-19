# CORE-013 — 产品侧可靠执行源接入（受控实现）

lifecycle_state: closed · disposition: none · depth: standard · base: working-tree
triage_label: done
parent_change: CORE-012
prior_slices: CORE-011（仿真证明）· CORE-012（四问裁决 + 实现计划）

## Intent

按 CORE-012 计划（`plans/2026-09-19-core-012-reliable-execution-source-integration.md`，
READY）实施产品侧可靠执行源：`EffectBoundary` 构造注入 + gate 通过、driver
之前的提交点；新增 `Effects/ExecutionSource/`（接口 + 只读投影 + 文件
append-only journal）；Kernel/Simulation 测试 + journal 验收。

## Authorization（2026-09-19 用户授权，范围 = 计划 §5）

- 修改 `src/UniClaw.Kernel/Effects/EffectBoundary.cs`（构造注入 + Dispatch
  接入 + AttemptId 铸造 + 追加失败语义）。
- 新增 `src/UniClaw.Kernel/Effects/ExecutionSource/`（BCL-only）。
- 新增/修改对应 Kernel/Simulation 测试与 `SimulationHost` 注入；文件
  journal 验收（含 Q3 提交边界专项）。
- 禁止不变：Trace 非权威、不新增 Core 顶层对象、不引入数据库、跨节点、
  真实外部操作；`UniKernel`/`KernelRunDriver` 不成为第二 delivery truth
  owner（预期零改动，仅核对 reason 透传）。

## Decisions

1. AttemptId 由执行源铸造（journal 全局序号续号），非 boundary 本地计数
   ——进程重启后同 binding 再 dispatch 不产生 id 冲突；boundary 只持
   单次 Dispatch 内的局部变量。
2. journal v1 只产生 Success（已 Flush）/Failure（IO 异常）；Unknown 留给
   其他传输实现，接口三值面与 S9 同 Attempt 重试语义经测试 double 证明。
3. commit Failure 不落帧（无可发送记录，binding 观察面已留痕）；与 CORE-011
   fixture 的 CommitFailed 可查询面差异入档。
4. driver 异常路径保持既有传播语义（不捕获）——attempt 停留
   CommittedPending = 未决未知，正是 S6 安全方向。
5. 追加失败（driver 已调用后）不抛过已投递边界：记录停留 pending，
   receipt 照常返回；仅提交阶段 fail-closed 返回。

## Acceptance（= 计划 §6）

- Slice 1 journal 契约：flush-后-success、未 Dispose 终止可重读、torn
  frame 截尾 = 未提交、无 ID 发现、append-only、迟到反馈/重试/补偿持久。
- Slice 2 boundary：S1–S3 产品版 + null source 零行为变化 + 追加失败
  不吞 delivery。
- Slice 3：commit 阻断 reason 经 Act/KernelRunDriver 既有路径透传。
- Slice 4：真实 journal 跨「进程死亡」（丢弃 writer 不 Dispose）恢复：
  S4/S6/S7/S8/S10/S11/S12 产品版；S5/S9 的窗口在 journal/double 层证明
 （产品 Dispatch 内无该注入点——映射入 evidence）。

## Verification

```yaml
level: SCENARIO
method: dotnet test UniClaw.Kernel.slnx 全量 + 分 slice 过滤运行
expected: 新测试全绿；既有 544 基线零回归；git diff --check 通过
actual: >
  567/567 全绿（Core 13 + Agent 17 + Simulation 132 + Kernel 405）；
  新增 23 测试（journal 8 + boundary 7 + 透传 1 + 重启 7）；544 基线零
  回归；git diff --check 通过；src/ 改动面 = EffectBoundary.cs（+56 行）
  + 新增 ExecutionSource/，UniKernel/KernelRunDriver 零改动
evidence: evidence/2026-09-19-core-013-product-execution-source.md
```

## Status log

2026-09-19 · created · 用户授权按 CORE-012 计划实施（落 CORE-013）；
实现开始，授权范围即本文件 Authorization 节。
2026-09-19 · implemented · 五切片完成（journal 契约 → boundary 接入 →
reason 透传 → 真实 journal 重启场景 → 证据）；RED→GREEN 留痕见
evidence §4；S1–S12 全部有产品级证明（S5/S9 落在 journal/double 层，
映射表见 evidence §3）。
2026-09-19 · closed · 验收 = 计划 §6 全项：Q3 提交边界五条逐条可证伪
（evidence §2）；授权边界零越界（REVIEW 核对：src/ 仅授权两处、
Trace 零代码依赖、Core 零新对象、UniKernel/KernelRunDriver 零改动）。
自动重试/补偿编排保持 deferred（接口面已备，Control/Agent 是未来 buyer）。
