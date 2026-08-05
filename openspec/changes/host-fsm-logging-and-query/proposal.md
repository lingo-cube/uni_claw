## Why

Host 日志基础设施（`TraceCorrelatedFileProvider` + `TraceCorrelatedConsoleProvider`）已落地，
但埋点严重不足——默认 Info 级别下用户只能看到 run 启动/结束和异常栈，
无法回答"引擎做了什么动作？看到什么页面？为什么停？"等基本排查问题。
需要在关键路径上补充 Info 级别日志，并让 `host-test-runner` skill 和 `trace-analyzer` agent 能消费这些日志。

## What Changes

- **新增** 5 个日志埋点：FSM 状态转换、操作执行、页面分析摘要、引擎终止原因、安全门拒绝
- **新增** 2 个 ILogger 注入点：`SafeActionExecutor`、`InvalidatingPageAnalysisCache`（可选 ctor 参数 + NullLogger 默认）
- **修改** `HostCommands.CreateRunServices`：组合根注入新 loggers
- **修改** `host-test-runner` skill：Phase 3 实时日志 tail + Phase 4 日志查询子步骤
- **修改** `trace-analyzer` agent：Step 3 补证来源增加 run.log、Step 4 完整性表增加日志行
- **新增** trace-analyzer memory 条目：日志格式/路径/查询方法

## Capabilities

### New Capabilities
- `host-fsm-logging`: Host + FSM 运行时 Info 级别日志埋点，覆盖操作执行、页面分析摘要、FSM 正常状态转换、引擎终止原因、安全门拒绝判定

### Modified Capabilities
- `trace-analyzer-cli`: run.log 作为 Step 3（深入取证）第一优先级补证来源；Step 4（完整性自评表）新增 run.log 行

## Impact

- 受影响的代码文件：`SafetyGate.cs`、`InvalidatingPageAnalysisCache.cs`、`TraversalFSM.cs`、`TraversalEngine.cs`、`HostCommands.cs`（共 5 个文件）
- 受影响的 skill/agent：`.claude/skills/host-test-runner/SKILL.md`、`.claude/agents/trace-analyzer.md`、`.claude/agents/trace-analyzer-memory/knowledge.md`
- 非破坏性变更：新增 ILogger 参数均可选 + NullLogger 默认，已有调用方无需修改
- 性能影响：Info 级别新增约 10-20 行/run 的日志 I/O，可忽略
