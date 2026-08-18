# Design: dsh-control-plane-event-stream

## Context

控制平面 V1（`dsh-plugin-uniclaw/client/lib/client.js`）已实现任务列表/工作台/详情
三栏骨架，但事件流是"借道 shadow digest"（从 `uniclaw-shadow-analyze` 的文本里
正则提取 kernel-fact 行）——不是真实的 RuntimeEvent 通道。DriverHost 侧
`run.events.after` wire 方法、`RuntimeEventProjector`、`RuntimeEventStore`
均已冻结/毕业；`demo-driverhost-server.mjs` 已按多任务事件页响应 `run.events.after`。
缺口只在 DSH 命令层 + 控制平面消费端。

约束（来自已冻结决策）：
- wire 方法 `run.events.after` 语义不变（8 方法只读表）
- 命令零模型（F17：控制平面模块不得引用 llm/vlm/model）
- Kernel/DriverHost 零变更
- 控制平面 client bundle 是手写 closure-factory（无构建链）

## Goals / Non-Goals

**Goals:**
- 新增 `uniclaw-events-after` 零模型命令，暴露冻结事件读取
- 控制平面事件流改用真实命令通道，渲染分类事件时间线
- 支持 cursor 续读（有界轮询基础）
- fixture 验证链路完整（无需新 wire）

**Non-Goals:**
- 不新增 wire 方法、不改协议
- 不做事件推送/订阅（保持拉取模型；推送留待未来）
- 不实现"自动刷新"之外的事件触发（autoTriggers 仍 deferred）
- 不改 DriverHost / Kernel / 已冻结方法

## Decisions

### D1: 命令层暴露，adapter 直通

`uniclaw-events-after <runId> [--cursor <n>]` 命令 handler 直接调 adapter 的
`getRuntimeEvents(runId, cursor)`（adapter 已实现，走 `run.events.after` wire）。
命令只格式化返回，零模型。

- **备选 A**：控制平面直接调 adapter（不经命令）——不可行：浏览器端只能走
  `ctx.remote.commands.execute` 命令通道，不能直连 DriverHost。
- **备选 B**：新建专用事件服务——过度；命令通道已通，直接复用。
- **结论**：命令是浏览器→Kernel 的唯一已验证通道，D1 成立。

### D2: 命令输出用可解析的稳定文本

`uniclaw-events-after` 输出格式（与既有命令一致的 label/value 风格，便于
控制平面解析和人工可读）：

```
runId: run-x
cursor: 5
event: evt-3 [TrapRaised] seq=3 kind=TrapRaised payload={"trapKind":"StateMismatch"} refs=capture:... 
event: evt-4 [ObservationRecorded] seq=4 kind=ObservationRecorded payload={...}
```

每行一个事件，`event:` 前缀 + 结构化字段。控制平面按行解析（复用 V1 的
`parseFieldLine`），渲染时间线。

- 为什么不是 JSON：命令结果文本是 DSH 会话的持久化格式（command/done 事件），
  纯文本 label/value 与既有 4 命令一致，且人工在聊天框也能读。

### D3: 控制平面 fetchEvents 改用真实通道

`client/lib/client.js` 的 `fetchEvents` 从"shadow digest 正则提取"改为：

```js
const res = await runCommand(remote, sessionId, `/uniclaw-events-after ${runId}`);
const events = parseEventLines(res?.text);   // 解析 event: 行 → {kind, seq, text}
```

轮询：控制台打开期间每 2s 调一次（带 cursor 续读，去重已见 sequence）。
有界频率，无事件时显式空态（spec 要求）。

### D4: 复用 V1 组件与分类色

事件时间线复用 V1 的 `EventRow`（状态点 + 文本）+ 分类色：
TrapRaised/RunFailed 红、RunCompleted 绿、ObservationRecorded/StepAdvanced 灰。
无需新组件。

### D5: fixture 对齐验证

`demo-driverhost-server.mjs` 已为多任务实现 `run.events.after` 事件页（含
TrapRaised/RunCompleted/RunFailed/StepAdvanced/ObservationRecorded），
命令测试用 wire fixture 断言方法集含 `run.events.after` 且无 mutating 方法。

## Risks / Trade-offs

- **轮询延迟**（2s）：事件到达控制平面的延迟 ≤2s，可接受；推送模型留未来。
- **cursor 语义**：`run.events.after` 的 cursor 是 sequence 后开区间；客户端
  需记录 lastSequence 去重。fixture 已按此语义实现。
- **命令文本格式演进**：控制平面解析依赖 `event:` 行格式；若未来改格式需
  同步 client bundle（文档化格式，见 D2）。
- **大事件页**：一次返回事件数受 DriverHost 分页控制（`nextCursor`/
  `hasMore`）；控制平面当前只消费首页，后续可分页拉全。
