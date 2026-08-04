## Context

运行链路已打通：`RunScenarioAsync` 产出完整 run 目录（manifest.json / result.json / trace/trace.jsonl / steps/D4/*）。观测层（UniClaw.Core.Observability）已具备完整回读能力——`FileTraceStorage` 的 JSONL 有 `record_type` 判别器 + 12 个读方法，`InMemoryTraceService` 提供 ITraceQuery 的全部 span 树查询。诊断分析器（CompletionMonitor / ErrorLoopAnalyzer / VerificationAnalyzer）已存在于 Host。

缺失的是**消费层**：从 trace 快速定位问题（人用表格/TUI，agent 用 JSON）没有工具化入口；run 实例缺少预期目的/任务/系统版本/机器信息等上下文，诊断结论无法携带运行环境。

约束：

- Core 的 `ITraceRecorder` / `ITraceService` / `SpanType` 为宪章锁定的接口，本 change **零改动**
- manifest schemaVersion 冻结为 "1"，新增字段必须 optional 向后兼容
- 运行目录布局（`{outputRoot}/{scenarioId}/{runId}/`）不可变——TraceTool 是只读消费者

## Goals / Non-Goals

**Goals:**

- 提供 6 个 CLI 子命令（list / timeline / diagnose / diff / report / interactive），人机两用
- `--format json` 机器可读契约 + 退出码语义，agent 三步定位：list → diagnose --json → 深入 artifactPaths
- `TraceRun` 作为唯一聚合入口：trace 回放 + result/manifest 交叉引用
- manifest 元数据增强：Purpose / TaskId / RunSystemInfo / RunMachineInfo（全部 optional）

**Non-Goals:**

- 不修改 Core 观测接口与 span 语义
- 不实现基线对比（集成测试标准化 plan 独立存在）
- 不内嵌图像预览（截图用系统 open 打开）
- 不做在线/实时分析——只读离线产物

## Decisions

### D1: 新独立项目 `src/UniClaw.TraceTool/`，不并入 Host

- **决策**：TraceTool 作为独立 console 项目，引用 UniClaw.Core。
- **理由**：Host 是运行期组件，TraceTool 是纯离线分析器；分离避免 Host 依赖 Spectre.Console / Terminal.Gui / System.CommandLine，防止 Host 程序集膨胀；Host 无 TUI 场景。
- **替代**：并入 Host 项目（被拒：污染运行期程序集）、独立 repo（被拒：过度隔离，需共享 Core 程序集，同 solution 即可）。

### D2: 回放复用现有设施，不新写解析器

- **决策**：`TraceRunLoader` 用 `FileTraceStorage` 读 JSONL → replay 进 `InMemoryTraceStorage` → 包 `InMemoryTraceService` 提供 ITraceQuery。所有 span 查询走 ITraceQuery，子命令不直接碰文件。
- **理由**：FileTraceStorage 已实现 record_type 判别 + 坏行跳过 + dedup 语义；InMemoryTraceService 已是测试验证过的查询实现。新增代码仅 ~50 行。
- **替代**：手写 JSONL 解析（被拒：重复 record_type 判别逻辑，双语义源）。

### D3: 故障规则复用 Host 分析器，新规则放 TraceTool

- **决策**：`diagnose` 复用 `CompletionMonitor` / `ErrorLoopAnalyzer` / `VerificationAnalyzer`（FailingStep / FailureCause / IssueFingerprints 已在 run 产物中），TraceTool 只新增聚合型规则（ai_call_failures、时间线空洞）。TUI 与 diagnose 共享同一规则引擎，避免双结论源。
- **理由**：已有规则经 1086+ 单测验证；产物已带 failureCause，不重复推断。
- **替代**：TraceTool 重写全套规则（被拒：双维护、结论可能分叉）。

### D4: JSON 契约优先——所有命令 `--format json`，stdout 纯 JSON

- **决策**：`--format json` 输出稳定 schema（含 schemaVersion），日志/警告走 stderr；非 TTY 自动去装饰；evidence 上限默认 5 条。
- **理由**：agent 消费优先（用户明确需求），人读格式是默认但非唯一；schemaVersion 让未来演进可检测。
- **替代**：仅表格 + jq 提取（被拒：schema 不稳定，agent 解析成本高）。

### D5: 退出码契约

- **决策**：0 = 成功；1 = diff 检测到差异（回归信号）；2 = 用法错误 / run 不存在；3 = 空 trace。
- **理由**：脚本 `if ! uni-claw trace diff ...; then` 可直接做回归判定。
- **替代**：统一 0/非0（被拒：丢失 diff 语义）。

### D6: 元数据进 manifest，不另写文件

- **决策**：Purpose / TaskId / SystemInfo / MachineInfo 扩展 `RunManifestInput` + `RunManifest`（全部 optional），Host 在 `RunScenarioAsync` 采集注入；`RunSystemInfo` 用 ADB getprop（模拟器模式）/ null（local 模式），`RunMachineInfo` 用 RuntimeInformation + MachineName。
- **理由**：manifest 已是 run 身份文档、已过 AssetRedactor 脱敏管线、TraceTool 本来就读它；避免 emulator-info.json 双源。
- **替代**：独立 emulator-info.json（被拒：双源）、写进 result.json（被拒：result 是状态文档，语义不符）。

### D7: TUI 层薄，逻辑全在 TraceRun 聚合

- **决策**：Terminal.Gui 仅做展示与键位；数据查询、结论推断全部在 `TraceRun` / 规则引擎，TUI 与 CLI 共享。
- **理由**：TUI 无法自动化测试，薄层使测试面集中在可单测的聚合层。
- **替代**：TUI 内嵌逻辑（被拒：不可测）。

## Risks / Trade-offs

- [Spectre.Console / Terminal.Gui / System.CommandLine 为新增外部依赖] → 三个库均为成熟稳定库，仅 TraceTool 项目引用，Host/Core 依赖图不变
- [manifest 新增字段被既有消费方（集成测试断言、IterationAggregator）读取时出现 null] → 全部 optional + 无参构造函数默认 null；消费方只读不写，null 安全
- [旧 run 目录无新字段，diagnose 上下文缺失] → TraceRun 显示 "unknown"，不阻断分析
- [Terminal.Gui 依赖包体积与跨平台差异] → 仅 interactive 命令路径加载，CLI 其他命令不初始化 TUI；`TERM=dumb` 拒绝启动
- [JSON 契约演进破坏既有 agent 脚本] → schemaVersion 字段前置，未来变更 bump 版本

## Migration Plan

1. 新增 TraceTool 项目 + 测试项目，接入 solution
2. Host 侧元数据（RunSystemInfo / RunMachineInfo 记录 + RunManifestInput 扩展 + 采集注入）——向后兼容，旧 run 不受影响
3. TraceTool 子命令按 list → timeline → diagnose → diff → report → interactive 顺序实现
4. 用既有 snapshot fixture 验证规则引擎输出

回滚：仅删除新项目与 Host 新增可选参数（无破坏面）。

## Open Questions

- （无 — 设计已全部收敛，brainstorming 5 节 + gap 分析已确认）
