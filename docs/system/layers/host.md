# Host Layer

> **Tier 3 · Layers**: 可执行组合根、场景运行与设备测试资产。Host 可以引用
> Core、Device 和 provider；其他生产项目不反向引用 Host。

## Commands

- `doctor --device <serial>`：只读 readiness，禁止隐式启动 AVD。
- `analyze --device <serial>`：一次只读 `PageAnalysis`，动作计数为 0。
- `run --scenario <file> --device <serial>`：执行单个版本化场景。

## Engine-Driven Architecture (D-159)

场景执行通过 Core 的 `TraversalEngine`/`TraversalFSM` 驱动设备遍历，不再保留
自包含的 runner loop。Host 负责组装引擎 + hooks + analyzer，两种执行模式
(plan mode / intent mode) 共享同一引擎骨架。

**组合入口** (`HostCommands.RunScenarioAsync` / `HostRunServices.CreateTraversalEngine`):
- 注入 `SafeActionExecutor` 装饰的 `IActionExecutor`、`IObservableScreenStateProvider`、`ITraceRecorder`
- `IEntryPolicyExecutor.ExecuteAsync` 在 `engine.RunAsync()` 之前执行
- `ScenarioPlanCompiler` 产出 `DynamicMatch` intent plan；`ScenarioPlanLoader` 从 JSON 加载 `Static` plan

## Owned Components

- V1 scenario/policy sealed records、严格 JSON 校验、规范化与 SHA-256 hash。
- `ScenarioPlanCompiler`：scenario → `DynamicMatch` `TraversalPlan`。
- `ScenarioPlanLoader`：plan JSON → `ChildrenStrategy.Static` + `StaticNodes`。
- `TraversalPlan` provisioned as data (not engine code) per D-166.
- **Hooks** (`ITraversalHook`):
  - `RunAssetHook`：`OnBeforeStep`/`OnAfterStep` 写入 per-step artifacts + 截图
  - `SafetyContextHook`：`OnBeforeStep` 推送 per-step `SafetyCandidate` 到 `SafetyExecutionContext`
  - `BoundaryHook`：package/page-prefix 边界检查，违规记录入 trace
  - `VerifyHook`：plan mode `OnAfterStep` 匹配 `expected_change`，不改变引擎状态
- **Post-run analysis**:
  - `VerificationAnalyzer`：读取 `ITraceService` + `SafetyDecisionJournal` → `ScenarioRunOutcome`
  - 分析严格在 `RunAsync()` 之后执行，无实时耦合
- deterministic safety evaluator/decorators：固定 deny precedence、默认拒绝。
- isolated run assets、stable issue fingerprints、redaction 与 authoritative
  `result.json`。

## Safety and Failure Rules

所有真实 launch/click/back/scroll/input/long-press 都在紧邻 Device executor
之前经过同一 gate。Denied action 不调用 inner executor。未知 package/page、
不可信坐标、危险 semantic/text、越界或预算耗尽均拒绝。

`success` 只在场景 success criteria 有证据时产生。目标未验证、列表末端未
证明、设备/provider/报告失败或取消必须分别报告，不得提升为成功。

## Current Delivery Boundary

已交付 engine-driven locate 及 enumerate 离线闭环（mock 测试验证通过）。
Emulator 集成场景 (`scenario-locate`, `scenario-enumerate`) 的实机证据收集
已标记为 deprecated；真实 provider、repeat/10-run 稳定性门禁仍在对应
OpenSpec change 中保持未完成；文档不声明这些能力已可用。
