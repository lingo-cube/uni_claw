## 1. Code — 日志埋点

- [x] 1.1 `TraversalFSM.StepAsync` 加 FSM 正常转换日志（Info），在 `TransitionTo(nextState)` 后
- [x] 1.2 `TraversalEngine.RunAsync` 加引擎终止原因日志（Info），在每个 `Done(...)` 返回前
- [x] 1.3 `SafeActionExecutor` 加可选 `ILogger<SafeActionExecutor>` 参数 + 操作执行日志（Info）+ deny 日志（Warning）
- [x] 1.4 `InvalidatingPageAnalysisCache` 加可选 `ILogger<InvalidatingPageAnalysisCache>` 参数 + 缓存 miss 页面分析摘要日志（Info）
- [x] 1.5 `HostCommands.CreateRunServices` 组合根注入新 loggers

## 2. Consumer — trace-analyzer agent

- [x] 2.1 `trace-analyzer.md` Step 3（深入取证）增加 `run.log` 为第一优先级补证来源
- [x] 2.2 `trace-analyzer.md` Step 4（完整性自评表）新增 `run.log` 行
- [x] 2.3 `knowledge.md` 新增日志格式 / 路径 / 查询方法条目

## 3. Consumer — host-test-runner skill

- [x] 3.1 `SKILL.md` Phase 3 新增实时日志 tail 命令
- [x] 3.2 `SKILL.md` Phase 4 新增日志查询子步骤（完整性检查 + 按组件/级别/spanId 过滤 + FSM 转换轨迹）

## 4. Verification

- [x] 4.1 构建 + 跑一次 locate-one-item（mock 即可），确认 `run.log` 包含全部 6 类 Info 日志
- [x] 4.2 `grep "FSM.*→" run.log` 确认 FSM 转换轨迹可见
- [x] 4.3 `grep "action=" run.log` 确认操作日志可见
- [x] 4.4 `grep "page=" run.log` 确认页面分析摘要可见
- [x] 4.5 `grep "Engine terminated" run.log` 确认终止原因可见
- [x] 4.6 `UNICLAW_LOG_LEVEL=Warning` 测试：确认 Info 级别日志不可见，Warning/Error 仍可见
