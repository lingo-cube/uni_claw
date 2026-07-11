## Why

TraversalRuntimeContext 是 God Object — 30 mutable fields 全堆在一个 sealed class 里，没有正式的子系统归属。D-I (Context decomposition, P2) 的前置依赖是明确的命名边界：没有 canonical naming，拆分无方向。路线图 20-b §5 明确锁定了「先闭环再重构」序列，P0 handler 实装已完成 (603 tests stable)，现在是 P1 的第一步。

## What Changes

- **正式定义 5 subsystem** 的 canonical name + 字段归属表 + 无歧义标注
- **消除 8 个歧义字段** 的归属争议（如 `_visitedLevel1Menus`: DFS visited set 还是 cache dedup?）
- **产出 canonical 归属表** 作为 D-I (P2 Context 拆分) 的唯一输入 — 拆分方案必须基于此表，不是推测
- **更新 ArchitectureGuardTests** 加入 subsystem boundary guard (验证字段不跨 subsystem 引用)
- **更新 docs/system/layers/state-machine.md** 反映 subsystem 归属

## Capabilities

### New Capabilities

- `subsystem-boundaries`: 5 subsystem canonical definition + field attribution + ambiguity resolution + boundary guard tests

### Modified Capabilities

- `enum-value-guards`: Guard tests 新增 subsystem boundary verification (验证 TraversalRuntimeContext 字段不跨 subsystem 引用)

## Impact

- **代码**: `src/UniClaw.Core/StateMachine/TraversalRuntimeContext.cs` (注释标注归属，不改结构) + `tests/UniClaw.Core.Tests/Architecture/ArchitectureGuardTests.cs` (新增 guard)
- **依赖**: D-I (P2) 强依赖此产出 — 拆分方案基于 canonical 归属表
- **API**: 无 public API 变更 — 归属是注释/文档级别，不改字段签名
- **Guard tests**: 新增 subsystem boundary guard — 验证字段归属与 canonical 表一致
