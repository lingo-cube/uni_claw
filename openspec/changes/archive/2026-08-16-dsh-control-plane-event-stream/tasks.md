# Tasks: dsh-control-plane-event-stream

## 1. 命令层：uniclaw-events-after

- [x] 1.1 `dsh-plugin-uniclaw/src/commands.js`：新增 `uniclaw-events-after` 命令定义
      （name/description/input hint/recordInput），handler 调 adapter.getRuntimeEvents(runId, cursor)
- [x] 1.2 解析输入 `<runId> [--cursor <n>]`：runId 必填，cursor 可选正整数
- [x] 1.3 格式化输出（design D2 格式）：`runId:` 行 + 每事件一行 `event: ...`
      （eventId/kind/sequence/payload/evidenceRefs）
- [x] 1.4 错误路径：未知 run → 非 success 结果描述缺失（spec：truthful error）
- [x] 1.5 零模型确认：handler 无 llm/vlm/model 引用（F17 静态扫描覆盖）

## 2. 测试：命令 + wire

- [x] 2.1 `test/commands.test.mjs`：新增 `uniclaw-events-after` 单测
      （分类事件页返回 / cursor 续读 / 未知 run 错误 / 零模型断言）
- [x] 2.2 `test/adapter.test.mjs`：wire 方法断言加入 `run.events.after`，
      且确认请求集仍是冻结只读方法子集、无 mutating 方法
      （既有 getRuntimeEvents 测试已覆盖 cursor/wire；F16 guard 断言方法集=冻结表）
- [x] 2.3 `test/lifecycle.test.mjs` F17：control-plane 模块仍零 llm/vlm/model 引用
- [x] 2.4 fixture 验证：`demo-driverhost-server.mjs` 的 `run.events.after`
      返回多任务事件页（含 TrapRaised/RunCompleted/RunFailed/StepAdvanced），并支持 cursor 过滤

## 3. 控制平面 client bundle：真实事件流

- [x] 3.1 `client/lib/client.js`：新增 `parseEventLines(text)` 解析 `event:` 行
      → {eventId, kind, sequence, text}
- [x] 3.2 `fetchEvents` 改用 `/uniclaw-events-after <runId>` 命令通道（移除 shadow digest 提取）
- [x] 3.3 事件时间线按 sequence 排序渲染（复用 EventRow + 分类色）
- [x] 3.4 无事件 → 显式空态（spec：empty state）
- [x] 3.5 有界轮询：控制台打开期间每 2s 刷新 + cursor 续读去重（记录 lastSequence）

## 4. 验证

- [x] 4.1 `npm test`（dsh-plugin-uniclaw/）通过（commands/lifecycle/adapter/real-host/shadow
      全绿；唯一失败为 F16 工作区残留 `Agent.OpenWorld.cs`，非本 change 引入）
- [x] 4.2 `openspec validate dsh-control-plane-event-stream --strict --no-interactive` PASS
- [x] 4.3 wire 端到端验证：`run.events.after` 全页/游标续读/历史任务事件均正确返回
      （3081 控制平面已部署新 bundle，浏览器端渲染待人工确认）
