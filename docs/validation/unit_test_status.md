# Unit Test Status

**Project**: UniClaw (Core + Host)
**Version**: local-vision-host-wiring (apply)
**Change**: local-vision-host-wiring
**Task**: 4.1–5.6 — Host 装配层接线：Python 生命周期管理、条件装配、路径解析集中化
**Generated**: 2026-08-03
**Git Branch**: feature/refactor

---

## Executive Summary

local-vision-host-wiring 全量落地（22/22 任务完成）：Python 生命周期从 `CreateProviders` 迁移至 `RunScenarioAsync`（StartAsync → engine.RunAsync → DisposeAsync）；`CreateRunServices` 条件装配——本地模式 `VisionScreenStateProvider` + bare `PageAnalyzer`，非本地保持 `AdbScreenStateProvider` + `ObservationPipeline`；路径解析集中化（label-mapping.json + server.py 在 Host 层一次性解析）；`AnalysisWritingDecorator` 接入生产装配。

全量测试 **1240/1240 通过**（1086 Core + 154 Host），0 失败。Build 0 错误。

| Metric | Value |
|--------|-------|
| Total Tests | **1240** |
| Passed | **1240** |
| Failed | **0** |
| Error | **0** |
| Skipped | 14（emulator-gated + 既有 skip） |
| Architecture Guards | 56/56 |
| Build | 0 errors |
| 快照闸门 | 8/8 |
| Oracle 零改动 | ✅（TraceSpanTests/TraceSpanTree/HandlerTraceWriter/InMemoryRecorder/ArchitectureGuard/PageAnalyzer/Traversal/SafetyGate/Analyzer/Baseline 零 diff） |

## Detailed Analysis

### AC 验收矩阵（M3）

| AC | 判定 | 证据 |
|----|------|------|
| AC1 快照闸门 | ✅ | 8/8：S1–S3 逐字节不变；S4 重冻结（ai.call parent=engine.step）；**S5 随并行 local-vision R-12 重冻结（53→70 行，用户裁决，3 次确定性复现 + Set/Reset 移除对照实验证明与本 change 无关）**；S6 完整父链含重试；NonEngineEntry 孤儿保留 |
| AC2 Oracle 零改动 | ✅ | git status 下 oracle 文件无 M；PageAnalyzer 可选 ctor 参数保证测试编译零改动 |
| AC3 无新脚手架 | ✅ | `StartSpanAsync/EndSpanAsync` 命中仅限 ITraceRecorder 声明、TraversalEngine passthrough、InMemoryTraceRecorder 实现 |
| AC4 目录与枚举冻结 | ✅ | `SpanType` enum 11 值（guard 绿）；TraceFields 45 键完整性测试绿；SpanTypes 本 change 未增删（并行 change +4：ai.yolo/ocr/fusion/scroll，无 guard 断言计数） |
| AC5 基线计数 | ✅ | 最终 Core 1083/2、Host 143/7；归因：M0 +4、M2 +11、M1 +2；+19 Core 差额来自并行 local-vision 测试 |
| AC6 分级缺省兼容 | ✅ | 缺省 Detailed 字段集与全量一致（S1–S6 快照 + 单元断言）；Basic 仅核心字段、None 空属性、profile=null 不过滤 |
| AC7 父链双向覆盖 | ✅ | S6（引擎入口 → parent=engine.step）+ NonEngineEntry（非引擎入口 → 保留孤儿根）同时绿 |

### 关键决策（apply 期，用户裁决）

1. **生产父链通道（2.7）**：D1 原"TraceCoordinator 实现 provider"在生产走不通（引擎 `Initialize()` 自建 coordinator 跟踪 step id；Host 组合根另建 `new TraceCoordinator(recorder)` 且 traceId=null → Active=false，双实例不相通）。用户裁决 **AsyncLocal 通道**：`EngineStepSpanContext`（静态单例 + `AsyncLocal<string?>`），引擎 step scope 开/合 Set/Reset，Host 注入 Instance；TraceCoordinator provider 实现移除。
2. **S5 快照冲突**：并行 change 的 R-12 滚动重试合法改变引擎行为。用户裁决立即重冻结（当前 70 行行为），AC1 措辞同步。
3. **M2 level 接线 inert（记录）**：`TraversalPlan.EntryConfig` 生产与测试均 null → `SpanTraceLevel ?? Detailed` 恒 Detailed。真正驱动引擎 span 分级需 ITraceCoordinator 接口变更（宪章级），留独立决策。

### 架构约束验证

- `ITraceRecorder` 9 声明方法不变（helper 全 additive）
- `ITraceCoordinator` 27 public 成员不变（显式接口实现 + internal 扩展）
- `IPageAnalyzer` 签名零改动；4 个 `AnalyzeCurrentPageAsync` 调用点零 diff（TraversalEngine/InterceptionHandler/TraversalFSM）
- 新增文件：`ITraceContextProvider`、`EngineStepSpanContext`、`TraceFields`、`SpanFieldProfile`（含 `TraceSpanFields`）；`InternalsVisibleTo("UniClaw.Host.Tests")`（fixture 访问 internal Set/Reset）

## Conclusions & Recommendations

- ✅ M0–M3 全部完成，AC1–AC7 全绿；span 树父子关系语义完整（`engine.run → engine.step → ai.call → ai.analyze`），非引擎入口观测不丢失
- ✅ 字段键名集中目录 + 按 TraceLevel 分级，为未来 `[TraceSpan]` source generator 提供 TSG002 字段校验输入
- ⏸️ **S5 重冻结的 R-12 归因**：local-vision-provider 验收时应知悉 S5 已在 trace-parent-linkage 内重冻结（70 行行为）；若 R-12 后续调整重试参数，S5 需再次重冻结
- ⏸️ **EntryConfig.TraceLevel 接线待决策**：当前 inert（恒 Detailed）；若需 Basic/None 生产生效，需宪章级决策（ITraceCoordinator 变更或 EngineStepSpanContext 通道扩展）
- Ready for `/opsx:archive`（trace-span-helpers 与 trace-parent-linkage 两个 change 均可归档）

---

*Report generated per validation-documentation skill standards. Data source: `.claude/skills/module-test/contracts/` (trace_unit.json, updated 2026-08-03).*

---

# Unit Test Status — trace-analyzer

**Project**: UniClaw (Core + Host + TraceTool)
**Version**: trace-analyzer (apply)
**Change**: trace-analyzer
**Task**: 1.1–8.4 — TraceTool CLI/TUI 离线 trace 分析工具 + Host 元数据增强
**Generated**: 2026-08-04
**Git Branch**: feature/refactor

---

## Executive Summary

trace-analyzer 全量落地（35/35 任务）：新增独立 `UniClaw.TraceTool` console 项目（6 子命令 + Terminal.Gui TUI）+ `UniClaw.TraceTool.Tests` 测试项目；Host 侧 RunManifest 元数据增强（Purpose/TaskId/SystemInfo/MachineInfo，全部 optional 向后兼容）。

| Metric | Value |
|--------|-------|
| 新增项目 | 2（`src/UniClaw.TraceTool/` + `tests/UniClaw.TraceTool.Tests/`） |
| 修改项目 | 1（`src/UniClaw.Host/` — 元数据字段 + 采集器） |
| Host 回归测试 | 164/164 通过，0 失败 |
| TraceTool 测试 | 32/32 通过，0 失败 |
| Build | 0 errors（全 solution） |
| 新增 NuGet 依赖 | System.CommandLine, Spectre.Console, Terminal.Gui（仅 TraceTool 项目） |
| schemaVersion | "1"（manifest 不变，JSON 契约独立） |

## Key Decisions (D-184–D-190)

| ID | 决策 |
|----|------|
| D-184 | TraceTool 独立项目，不并入 Host |
| D-185 | 复用 FileTraceStorage 回放，不新写 JSONL 解析器 |
| D-186 | 故障规则复用 Host 分析器产物（result.json），TraceTool 新增聚合规则 |
| D-187 | JSON 契约优先——全部命令 `--format json`，stdout 纯 JSON |
| D-188 | 退出码契约 0/1/2/3 |
| D-189 | 元数据进 manifest，不另写文件 |
| D-190 | TUI 层薄，逻辑全在 TraceRun 聚合 |

## Conclusions & Recommendations

- ✅ 元数据字段全部 optional + default null，旧 run / 旧 reader 零破坏
- ✅ TraceTool 只读消费者：不写 session.json，不修改 run 目录
- ✅ JSON 契约含 schemaVersion，agent 可检测版本演进
- ✅ 诊断规则引擎已完成离线补检接入：`ErrorLoopAnalyzer(null).EvaluateAsync(ITraceQuery)`（null recorder = 纯检测），stuck_in_error_loop / skip_rate_too_high 命中 → cause `error_loop_stuck` + error_loop evidence + failingStep 定位（判定委托 Host，阈值引用公开常量）；IssueFingerprints 透传 evidence
- ⏸️ Terminal.Gui TUI 无法自动化测试，测试面集中在 TraceRun / DiagnoseEngine / RunDiffer 可单测层
- ✅ 端到端冒烟（真实 run 20260803-175325）：`list` 递归发现嵌套 run 目录 → `timeline`（12 steps + AI 延迟）→ `diagnose --format json`（schemaVersion "1"、run.system 含 ADB getprop 的 Android 15/SDK 35、artifactPaths 三件套定位正确）；退出码契约实测 2（目录不存在）/ 3（空 trace）/ 非 TTY 自动转 JSON
- ✅ Ready for `/opsx:archive`（tasks 1.1–8.4 全部完成）

---

*Report generated per validation-documentation skill standards.*

---

# Unit Test Status — trace-issue-evidence

**Project**: UniClaw (Core + Host + TraceTool)
**Version**: trace-issue-evidence (apply)
**Change**: trace-issue-evidence
**Task**: 1.1–4.2 — diagnose 从 issues.jsonl 补全 issue_fingerprints evidence
**Generated**: 2026-08-04
**Git Branch**: feature/refactor

---

## Executive Summary

trace-issue-evidence 全量落地（4/4 任务）：verification 类失败的 evidence 链断裂修复——`result.json.issueFingerprints` 恒空时，diagnose 从 `issues.jsonl` 补全指纹 + D-192 detail，confidence 从恒 low 恢复为 evidence 驱动（medium）。

| Metric | Value |
|--------|-------|
| 修改项目 | 1（`src/UniClaw.TraceTool/` — TraceRun +Issues / TraceRunLoader 读 issues.jsonl / DiagnoseEngine fallback） |
| Host / Core 改动 | **0**（纯 TraceTool 侧，源头回填留作独立 change） |
| TraceTool 测试 | 38/38 通过（32 既有 + 6 新增），0 失败 |
| 全 solution | 0 错误；Core 1087 / TraceTool 38 / Host 184 通过，0 失败 |
| JSON 契约 | schemaVersion "1" 不变；diagnose evidence 数组新增条目（向后兼容） |

## Key Decisions (D-1–D-4)

| ID | 决策 |
|----|------|
| D-1 | issues.jsonl 由 TraceRunLoader 聚合进 TraceRun（保持"TraceRun 是唯一入口"，子命令不直接读文件） |
| D-2 | 直接复用 Host `RunIssue` record（TraceTool 已引用 Host.Artifacts，与 RunManifest/RunResult 同源模式；镜像 record 有字段漂移风险） |
| D-3 | fallback 条件 = result 指纹为空 + issues 存在；result 非空不重复（幂等，未来源头修正落地后自动停用 fallback） |
| D-4 | evidence 文本 = `issues.jsonl: {fingerprint} — {summary}`；真实原因（D-192 detail 内嵌于 summary）可消费 |

## 冒烟验证（4.2）

真实 run（integration/scenario-locate/20260803-175325/locate-one-item/20260803T175346199Z-0bc10e2043d14af，verification 失败）：

```json
"confidence": "medium",
"evidence": [{
  "type": "issue_fingerprints",
  "description": "issues.jsonl: 271c8e6c949909e032f0 — target_page_identity_not_verified: Post-action page identity '<empty>' did not match the scenario success identities."
}]
```

- confidence low → **medium**（`evidence.Count > 0 ? "medium" : "low"`）
- evidence 含指纹 + 真实失败原因，Agent/脚本无需翻 issues.jsonl 即可消费
- cause / failingStep / artifactPaths 透传不变

## Conclusions & Recommendations

- ✅ verification 类失败 evidence 链恢复；对存量 run 立即生效（119 个历史 run 带 issues.jsonl）
- ✅ 附带健壮性修正：`result?.IssueFingerprints is { Length: > 0 }` 在 result.json 缺失该字段时 NRE（ImmutableArray default）→ `IsDefaultOrEmpty` 检查，旧 result.json 安全落入 fallback
- ✅ spec 措辞已对齐实现：RunIssue 无 `Detail` 字段，D-192 失败详情内嵌于 `Summary`（如 `target_page_identity_not_verified: <detail>`）
- ⏸️ 源头修正（Host issueSink 回填 outcome.IssueFingerprints）未做——design 明确留作独立 change；未来落地后 D-3 幂等规则自动停用 fallback
- ✅ Ready for `/opsx:archive`

---

*Report generated per validation-documentation skill standards.*
