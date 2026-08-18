# Proposal: dsh-control-plane-event-stream

## Why

`PROJECT_LEADER_NEXT_STEP_SELECTION_RESULT` (2026-08-16) 选定
`CONTROL_PLANE_REALTIME_OBSERVABILITY_GAP` 为下一个 buyer：Shadow 已毕业，但
Intelligence 尚无 Kernel consumer；当前最大缺口是 **RuntimeEvent 未进入 DSH
控制平面**。

现状（已核实的仓库事实）：
- DriverHost wire 协议已冻结 `run.events.after` / `run.events.drain` 两个事件
  读取方法（`dsh-uniclaw-control-plane-protocol-baseline`，归档毕业）。
- DriverHost 侧 `RuntimeEventProjector` + `RuntimeEventStore` 已实现并毕业
  （`dsh-kernel-read-only-observability`）。
- **但 DSH 插件（`dsh-plugin-uniclaw`）未暴露事件读取命令**——控制平面
  V1（`dsh-plugin-uniclaw/client`）的事件流是"借道 shadow digest"提取的，
  不是真实的 `run.events.after` 通道。控制平面因此看不到真实的 Runtime 事件
  时间线（TrapRaised / RunFailed / RunCompleted / ObservationRecorded ...）。

控制平面是"以 UI 自动化测试任务为核心的界面"，实时事件流是它的灵魂（数据
优先级第一项，见 `docs/decisions/outer-intelligence-integration-architecture.md`
§5.1）。没有真实事件流，控制平面只能展示静态快照，无法"实时监控"。

## What Changes

补上控制平面事件流的**命令层缺口**，形成完整链路：

```
DSH 控制平面 (client bundle)
  → uniclaw-events-after 命令（新，零模型）
  → dsh-plugin-uniclaw (adapter, 已有)
  → DriverHost run.events.after（wire 已冻结，已有）
  → RuntimeEventProjector + RuntimeEventStore（已毕业）
```

1. **新增 DSH 命令 `uniclaw-events-after <runId> [--cursor <n>]`**：
   - 通过既有 adapter 调用冻结的 `run.events.after` wire 方法
   - 返回分类事件页（eventId / kind / sequence / payload / evidenceRefs）
   - 零模型调用（与既有 4 个只读命令一致）
   - 支持游标续读（`--cursor`），为控制平面轮询/增量拉取打基础

2. **控制平面 V1 client bundle 接入真实事件流**：
   - `fetchEvents` 从"借道 shadow digest"改为调用 `uniclaw-events-after`
   - 事件流按 sequence 排序渲染时间线（复用 V1 的 EventRow 组件）
   - 增量：轮询 + cursor 续读（有界频率，如 2s）

3. **fixture 对齐**：`demo-driverhost-server.mjs` 已实现 `run.events.after`
   多任务事件页——验证链路用，无需新增。

## Capabilities

### New Capabilities
- `dsh-control-plane/event-stream`: 控制平面实时事件流能力——`uniclaw-events-after`
  命令 + 控制平面真实事件时间线渲染 + 有界游标续读。

### Modified Capabilities
无（本 change 不改变任何已冻结 wire 方法语义；`run.events.after` 协议行为不变，
仅暴露命令层入口）。

## Impact

- `dsh-plugin-uniclaw/src/commands.js`：新增第 5 个零模型只读命令（`uniclaw-events-after`）
- `dsh-plugin-uniclaw/client/lib/client.js`：`fetchEvents` 改用真实命令通道
- `dsh-plugin-uniclaw/test/commands.test.mjs`：新增命令测试
- `dsh-plugin-uniclaw/test/adapter.test.mjs`：wire 方法断言增加 `run.events.after`
- fixture 无需改（`run.events.after` 已实现）
- 不改：DriverHost、Kernel、wire 协议、已冻结方法语义
