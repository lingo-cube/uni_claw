---
name: host-test-runner
description: Host 集成测试全生命周期编排 —— 启动模拟器 + 视觉服务、执行测试（dotnet test / Host CLI）、实时监控（ConsoleTrace + TraceTool watch）、事后分析（diagnose/verify/timeline）、可视化（TUI/HTML/ASCII tree），全程可配置。与 trace-analyzer agent（深度归因）和 trace-analysis skill（离线排查）互补，本 skill 是测试执行 + 浅层仪表盘侧。
metadata:
  author: uni-claw-ai-team
  version: "1.0"
  tags: [host, integration-test, emulator, adb, trace, visualization, orchestration]
---

# Host Test Runner Skill

编排 Host 集成测试的完整生命周期：**环境准备 → 执行 → 实时监控 → 事后分析 → 可视化**。
输出 run 目录产物（`artifacts/runs/{scope}/{scenarioId}/{runId}/`）并可实时查看 ConsoleTrace 日志。

## When to Use

- 执行 Host 集成测试（单场景 / 批量）
- 启动模拟器 + 视觉服务做端到端验证
- 实时监控正在跑的 run（ConsoleTrace 流式输出 + TraceTool watch）
- 跑完后做快速诊断 + 验证（shallow dashboard），或委托 trace-analyzer agent 做深度归因
- 查看 run 产物（manifest / result / trace / analysis / 截图）
- 可视化 span 树 / 状态转换 / 时间线

## 架构概览

```
┌─────────────────────────────────────────────────────────┐
│  host-test-runner  SKILL（本文件）                        │
│  ┌──────────┐  ┌──────────┐  ┌────────────┐             │
│  │ Phase 1  │→ │ Phase 2  │→ │ Phase 3    │            │
│  │ 环境准备  │  │ 测试执行  │  │ 实时监控    │            │
│  └──────────┘  └──────────┘  └────────────┘            │
│                                    ↓                     │
│  ┌──────────┐  ┌──────────┐  ┌────────────┐             │
│  │ Phase 4  │← │ Phase 5  │← │ (agent)    │            │
│  │ 事后分析  │  │ 可视化    │  │ trace-     │            │
│  │ shallow  │  │          │  │ analyzer   │            │
│  └──────────┘  └──────────┘  └────────────┘            │
└─────────────────────────────────────────────────────────┘
```

- **本 skill 负责**：启动环境、执行测试、实时监控、浅层仪表盘（run list / result 摘要 / 控制台日志）
- **trace-analyzer agent 负责**：深度归因（root cause / failing step / confidence / evidence）
- **trace-analysis skill 负责**：离线手动排查（与 agent 同协议但交互式）

## 分层知识（执行前掌握）

| 层 | 内容 | 文档 |
|----|------|------|
| **C1 配置层** | integration.config.json schema、provider 段、scenario 段、env 覆盖优先级 | `tests/UniClaw.Host.Tests/Integration/IntegrationConfig.cs` + 实际 `integration.config.json` |
| **C2 输入层** | Scenario JSON 格式（scenarioId / mode / target / boundaries / safetyPolicy / successCriteria）、policy 文件 | `scenarios/android-settings/*.v1.json` |
| **C3 执行层** | Host CLI（doctor/analyze/run）、dotnet test + IntegrationFact scope 门控、HostCompositionFactory 装配链 | `src/UniClaw.Host/Commands/HostCommands.cs` |
| **C4 产物层** | run 目录布局（manifest/result/trace/issues/analysis/criteria + steps/D4/）、ConsoleTrace 实时输出 | `src/UniClaw.Host/Artifacts/RunAssets.cs` + `ConsoleTrace.cs` |
| **C5 分析层** | TraceTool CLI 8 子命令（list/diagnose/timeline/diff/report/interactive/verify/watch）、退出码契约 | `src/UniClaw.TraceTool/Commands/TraceCommands.cs` |

> C4/C5 与 trace-analyzer agent 的 L3/L4 共享同一套文档——本 skill 侧重执行面与浅层查看，agent 侧重深度归因。

---

## Phase 0 — 模拟器提前启动

模拟器启动耗时长（boot 30-120s），**应在 Phase 1 之前、甚至在 build 之前就启动**，与构建并行。

```bash
# 最早就绪：先启动模拟器，再做其他事
scripts/android-emulator.sh doctor
scripts/android-emulator.sh start &
```

模拟器在后台 boot 的同时，可以并行执行 build（Phase 1.3 配置验证可延后）。

## Phase 1 — 环境准备

### 1.1 模拟器（就绪确认）

> 若已在 Phase 0 提前启动，此处只需确认就绪。

```bash
# 检查 / 启动
scripts/android-emulator.sh doctor
scripts/android-emulator.sh start

# 关键 env（可选覆盖）
# UNICLAW_AVD_NAME        AVD 名（默认 uniclaw-lite-api35）
# UNICLAW_EMULATOR_HEADLESS=1  无窗口模式
# UNICLAW_EMULATOR_BOOT_TIMEOUT  启动超时秒数（默认 180）
# ANDROID_SDK_ROOT        SDK 根目录
```

doctor 检查项：adb 可用 → AVD 存在 → 设备在线 → boot 完成 → 截图能力 → UIAutomator 能力。

### 1.2 视觉服务（仅 local provider）

仅当 provider 为 `local` 时需要。Host 侧 `PythonVisionService` 自动管理生命周期（start/stop 在 RunScenarioAsync 内部），**但若使用 dotnet test 路径（不走 Host CLI），需要手动预热**：

```bash
# 检查 vision server 是否已在运行
curl -s http://localhost:8000/health 2>/dev/null || echo "not running"

# 手动启动（如果需要独立验证）
.venv-local-vision/bin/uvicorn tools.local_vision.server:app --app-dir /Users/fran/Documents/Code/spacex/uni-claw --uds /tmp/uniclaw-vision.sock &
```

测试侧 env（`ApplyProviderEnv` 注入，优先级：手设/CI > integration.config.json）：
- `UNICLAW_VISION_SOCK` — socket 路径（默认 `/tmp/uniclaw-vision.sock`）
- `UNICLAW_YOLO_MODEL` — YOLO 模型绝对路径
- `UNICLAW_LABEL_MAPPING` — label mapping 绝对路径
- `UNICLAW_OCR_BACKEND` — OCR 后端（rapidocr / paddleocr）

### 1.3 配置验证

```bash
# 验证 integration.config.json 可解析（在 testhost bin 目录下）
# 非破坏性：只加载 + 校验 schema 版本、provider 引用存在性
dotnet test tests/UniClaw.Host.Tests --filter "FullyQualifiedName~IntegrationConfigTests" -v:q
```

检查项：
- schema 版本 = `uniclaw.integrationConfig.v1`
- provider id 在已知集合（local/sensenova/claude/qwen/mock）内
- visionServer 只允许挂在 local 下
- 云端 provider 的 model 必填
- scenario 引用的 provider 存在

---

## Phase 2 — 测试执行

三种执行模式，按需选择：

### 2.1 dotnet test（xUnit 集成测试 — 推荐用于 CI / 自动化）

```bash
# 单场景（scope 门控）
UNICLAW_INTEGRATION_SCOPES=scenario-locate \
  dotnet test tests/UniClaw.Host.Tests \
    --filter "IntegrationScope=scenario-locate" \
    -v:minimal

# 多场景
UNICLAW_INTEGRATION_SCOPES=scenario-locate,scenario-enumerate \
  dotnet test tests/UniClaw.Host.Tests \
    --filter "Category=Integration" \
    -v:minimal

# 所有集成测试（包括 ADB 连通性）
UNICLAW_INTEGRATION_SCOPES=all \
  dotnet test tests/UniClaw.Host.Tests \
    --filter "Category=Integration" \
    -v:minimal

# 常用 env 覆盖
# UNICLAW_INTEGRATION_PROVIDER=local   覆盖配置文件 provider
# UNICLAW_INTEGRATION_MODEL=deepseek-v4-flash  覆盖 model
# UNICLAW_ADB_SERIAL=emulator-5554    指定设备串行
```

**scope 清单**（定义在 `IntegrationTestScopes`）：
`adb-connectivity` | `adb-read` | `adb-action` | `adb-vision-action` | `adb-session` | `scenario-locate` | `scenario-enumerate`

### 2.2 Host CLI 直接执行（推荐用于调试 / 单次 run）

```bash
# doctor — 设备诊断
dotnet run --project src/UniClaw.Host -- doctor \
  --device emulator-5554 \
  --provider local \
  --output artifacts/runs/doctor-test

# analyze — 单帧分析（不跑引擎）
dotnet run --project src/UniClaw.Host -- analyze \
  --device emulator-5554 \
  --provider local \
  --output artifacts/runs/analyze-test

# run — 完整场景执行
dotnet run --project src/UniClaw.Host -- run \
  --device emulator-5554 \
  --scenario scenarios/android-settings/locate-one-item.v1.json \
  --provider local \
  --output artifacts/runs/manual-test \
  --mode direct \
  --purpose "manual-sanity-check" \
  --task-id "task-001"

# env 覆盖（与 CLI 参数等效）
# UNICLAW_PROVIDER=local
# UNICLAW_MODEL=deepseek-v4-flash
# UNICLAW_VISION_MODE=direct
# UNICLAW_OUTPUT=artifacts/runs/commands
# UNICLAW_RUN_PURPOSE=manual-sanity-check
# UNICLAW_TASK_ID=task-001
```

**退出码**：0 成功 · 2 参数错误 · 10 准备失败 · 20 运行时失败 · 130 取消。

### 2.3 批量场景（通过 integration.config.json scenarios 列表）

```bash
# 读取 integration.config.json 的 scenarios 段，逐个执行
# 每个 scenario 独立 run 目录，互不影响
# 通过 env UNICLAW_INTEGRATION_PROVIDER / UNICLAW_INTEGRATION_MODEL 统一覆盖
```

### 2.4 复用已有构建

```bash
# 先构建（一次性）
dotnet build src/UniClaw.TraceTool -c Debug

# 后续用 bin 直调，跳过编译
BIN=src/UniClaw.TraceTool/bin/Debug/net10.0/UniClaw.TraceTool
HOST_BIN=src/UniClaw.Host/bin/Debug/net10.0/UniClaw.Host

$HOST_BIN run --device emulator-5554 --scenario ... --provider local --output ...
$BIN trace diagnose --run artifacts/runs/...
```

---

## Phase 3 — 实时监控

### 3.1 执行期日志（ConsoleTrace）

Host 运行时的实时控制台输出（emoji 图标 + 时间戳 + 动作/状态）。在 dotnet test 路径下，xUnit 默认吞掉 Console 输出——需要 `-v:detailed` 或单独捕获 stderr。

```bash
# Host CLI 直接运行时 ConsoleTrace 输出示例：
# 🚀 [09:15:23] session start  traceId=20260804T...
# 🔍 [09:15:28] step=  1 page_analysis → success
# 👣 [09:15:29] step=  2 step_start
# ⚡ [09:15:29] step=  2 safety.scroll → allow
# 📜 [09:15:30] step=  2 scroll_down → success
# 🔬 [09:15:32] step=  3 verification_page_check → pass
# 🔄 [09:15:32] Traversing → Completed (target_found)
# 🏁 [09:15:33] session end
```

图标映射（`src/UniClaw.Host/Observability/ConsoleTrace.cs`）：
| 图标 | 动作 |
|------|------|
| 🚀 | session start |
| 🏁 | session end |
| 🔍 | page_analysis |
| 👣 | step_start |
| ⚡ | safety.* |
| 📜 | scroll_* |
| 🔬 | verification_* |
| 🔄 | GlobalFSM state transition |
| 📄 | page transition |
| ⏳ | AI call（含延迟 ms + token 数）|
| ❌ | ERROR |

### 3.2 TraceTool watch（轮询验证）

run 正在执行时（status 未到终态），用 watch 轮询等待 `pending_verification` 后自动 verify：

```bash
$BIN trace watch --run-id <runId> --dir artifacts/runs --interval 5000
# 按叶子目录名 == runId 定位
# 终态（pending_verification）后自动 verify，以 verify 退出码退出
# 0=verified · 1=not_verified · 2=usage/dir error · 3=evidence_missing
```

### 3.3 实时日志流（run.log）

如果已知 runId，可以实时 tail 运行日志（格式 `[HH:mm:ss.fff] [t=<runId>] [s=<spanId>] [LEVEL] Category: message`）：

```bash
tail -f artifacts/runs/<scope>/<scenarioId>/<runId>/trace/<runId>/run.log
```

---

## Phase 4 — 事后分析（浅层仪表盘）+ 测试报告

### 4.0 测试报告（必输出 — 无论成功/失败/异常）

**每次测试执行后必须输出结构化测试报告**，即使执行过程出现异常（Host CLI 崩溃、序列化异常、模拟器断连等）。报告不依赖任何特定工具的输出——能从产物读就读，读不到就标注缺失。

```
═══════════════════════════════════════════════
  Host Test Report — <scenarioId>
═══════════════════════════════════════════════

📋 Run Info
   runId:    <runId 或 "N/A (no run directory)">
   scenario: <scenarioId>
   provider: <providerId>
   model:    <model 或 "N/A (local)">
   device:   <serial 或 "unknown">

📊 Result
   status:           <success | failure | runtime_failure | unknown>
   completionReason: <reason 或 "N/A">
   exitCode:         <0/2/10/20/130 或 "N/A">
   stepsConsumed:    <N>
   actionsAttempted: <N>
   actionsSucceeded: <N>
   scrollsConsumed:  <N>
   durationMs:       <N>

🔍 Verify (TraceTool)
   verdict:   <verified | not_verified | evidence_missing | N/A>
   cause:     <cause 或 "N/A">
   identity:  <final_identity 或 "<none>">

⚠️ 异常 / 证据缺口
   <列出所有缺失产物 + 异常信息>
   - result.json:       <✅ 存在 | ❌ 缺失>
   - trace.jsonl:       <✅ N spans | ❌ 缺失 | ⚠️ 空>
   - analysis.jsonl:    <✅ N rows | ❌ 缺失 | ⚠️ 最后一行无 identity>
   - manifest.json:     <✅ 存在 | ❌ 缺失>
   - run.log:           <✅ 存在 | ❌ 缺失>
   - criteria.json:     <✅ 存在 | ❌ 缺失>
   - scenario.snapshot.json: <✅ 存在 | ❌ 缺失>
   - 截图 (steps/):     <✅ 存在 | ❌ 缺失>
   - 异常:             <异常类名: 消息 | 无>

💡 Next Steps
   <基于证据完整性的下一步建议>
   - evidence 充足 + confidence=high → 读 agent 报告按建议修复
   - evidence 不足 → 补产物或重跑
   - 异常阻断 → 先修异常再重跑
═══════════════════════════════════════════════
```

**证据缺口枚举规则**：
- 产物文件不存在 → 标注 `❌ 缺失`
- 产物存在但内容空/关键字段缺失 → 标注 `⚠️ <具体描述>`
- CLI 工具不可用（bin 未构建 / 退出码异常）→ 标注 `⚠️ CLI 不可用: <原因>`
- 不要静默跳过——每个缺口必须显式标注

### 4.1 发现 run

### 4.1 发现 run

```bash
# 列出所有 run
$BIN trace list --dir artifacts/runs --format json

# 只看失败的
$BIN trace list --dir artifacts/runs --status failure --format json

# 按 task-id 过滤
$BIN trace list --dir artifacts/runs --task-id task-001 --format json

# 限制数量
$BIN trace list --dir artifacts/runs --status pending_verification --limit 10 --format json
```

### 4.2 快速诊断（shallow — 本 skill 执行）

```bash
$BIN trace diagnose --run <runDir> --format json
# 输出 JSON（schemaVersion "1"）：verdict/cause/failingStep/summary/confidence/evidence/suggestions/artifactPaths
```

**shallow 解读**（对照 C4 产物层）：
- `cause` 透传 `result.json.completionReason`——查看 manifest.json 的 provider/model/deviceSerial 了解运行环境
- `error_loop_stuck` = ErrorLoopAnalyzer 命中（≥5 连续全跳过 / 跳过>访问×4）→ 对照 C2 输入层的 boundaries 检查是否过紧
- `confidence` = low 且 `evidence` 空 → 证据不足，需委托 trace-analyzer agent 做深度归因（Phase 4.3）

### 4.3 深度归因（委托 trace-analyzer agent）

当 shallow 诊断不足以解释失败原因时，**启动 trace-analyzer 子代理**：

> **Agent 调用模式**：`Agent(subagent_type="trace-analyzer", prompt="诊断 run: <runDir>。")`

trace-analyzer 的职责（不在此 skill 内重复）：
- 按 L1→L4 分层掌握后下结论
- 判定与解释分离（判定是 C# 确定性规则，agent 只做归因解读）
- trace 完整性自评 + 证据不足时补读日志
- **资产缺失回报**：agent 必须在 `[资产缺失]` 段逐条列出缺失的 trace / 产物 / 日志 / 截图 / 配置，格式 `❌ <资产名>: <缺失原因>`。缺失项由本 skill 纳入 Phase 4.0 测试报告的"异常 / 证据缺口"
- 输出 format: `[分层掌握] [定位] [资产缺失] [完整性自评] [结论] [建议] [反思] [执行]`

**本 skill 不替代 trace-analyzer**——skill 负责浅层仪表盘 + 测试报告，agent 负责深度诊断 + 资产缺口检测。

### 4.4 验证（TraceTool verify）

```bash
# 单 run 验证（判定是确定性 C# 规则 LocateOneItemRule）
$BIN trace verify --run <runDir> --format json
# 退出码：0=verified · 1=not_verified · 2=usage/dir error · 3=evidence_missing

# 批量验证（CI / 收尾）
$BIN trace verify --dir artifacts/runs --status pending_verification --format json
```

### 4.5 性能时间线

```bash
$BIN trace timeline --run <runDir> --threshold 500 --format json
# 列出超过 500ms 的 span，含 span 类型 / 耗时 / 上下文
```

### 4.6 跨 run 回归对比

```bash
$BIN trace diff --run-a <runA> --run-b <runB> --format json
# 退出码：0=无差异 · 1=行为差异 · 2=用法错误 · 3=空 trace
```

### 4.7 日志查阅（run.log）

run.log 位于 `{runDir}/trace/{runId}/run.log`，格式 `[HH:mm:ss.fff] [t=<runId>] [s=<spanId>] [LEVEL] Category: message`。

**完整性检查**：
```bash
# 日志文件是否存在
test -f <runDir>/trace/<runId>/run.log
# 是否有 run start / end 记录
grep "Run.*started" <runDir>/trace/<runId>/run.log
grep "Run.*ended\|final state" <runDir>/trace/<runId>/run.log
```

**按组件/级别过滤**：
```bash
# FSM 状态转换轨迹
grep "TraversalFSM:" <runDir>/trace/<runId>/run.log
# 所有操作执行
grep "SafeActionExecutor:" <runDir>/trace/<runId>/run.log
# 安全门拒绝（deny 的 action）
grep "→ deny" <runDir>/trace/<runId>/run.log
# 页面分析摘要
grep "page=" <runDir>/trace/<runId>/run.log
# 引擎终止原因
grep "Engine terminated" <runDir>/trace/<runId>/run.log
# 所有 ERROR
grep "\[ERROR\]" <runDir>/trace/<runId>/run.log
# 所有 WARN + ERROR
grep "\[WARN\]\|\[ERROR\]" <runDir>/trace/<runId>/run.log
```

**spanId 交叉引用**（与 trace.jsonl 关联）：
```bash
# 从 trace.jsonl 找到异常 step 的 spanId → 在 run.log 中定位
grep "s=<spanId>" <runDir>/trace/<runId>/run.log
```

**时间区间**：
```bash
# 提取某段时间的日志
sed -n "/09:32:/,/09:33:/p" <runDir>/trace/<runId>/run.log
```

---

## Phase 5 — 可视化

### 5.1 交互式 TUI

```bash
$BIN trace interactive --run <runDir>
# Terminal.Gui TUI：逐记录浏览 span 树、查看 metadata
```

### 5.2 HTML 聚合报告

```bash
$BIN trace report --dir artifacts/runs --format html --output artifacts/report.html
# 输出多 run 聚合 HTML 报告（含状态分布、时间线对比等）
```

### 5.3 ASCII tree / Mermaid（手动可视化）

当需要快速查看 span 树结构时，读取 `trace.jsonl` 中 `record_type=="span"` 的记录，按 `spanType` / `parentSpanId` 构建层级树：

```
engine.run (00:05.2s)
├── engine.entry
│   ├── safety.launch
│   └── safety.wait
├── engine.step #1
│   ├── ai.call (2.3s)
│   │   └── ai.analyze (1.8s)
│   └── safety.scroll
├── engine.step #2
│   ├── ai.call (3.1s)
│   └── stateDecision.click (denied)
...
```

### 5.4 产物直接查看

```bash
# result.json — 快速看结局
cat <runDir>/result.json | python3 -m json.tool

# manifest.json — 看运行身份
cat <runDir>/manifest.json | python3 -m json.tool

# analysis.jsonl — 逐帧分析快照（matcher/OCR 排查关键）
tail -1 <runDir>/assets/<runId>/analysis.jsonl

# issues.jsonl — 运行期问题
cat <runDir>/issues.jsonl

# 截图（step 级产物）
# 目录：<runDir>/steps/0001/before.png, after.png
```

### 5.5 关键指标仪表盘

```bash
python3 scripts/log-analyzer.py metrics <runDir>/trace/<runId>/run.log
```

生成 run 指标摘要卡片，含终止原因、总步数、操作统计、错误/警告计数。

### 5.6 Step 执行摘要表

```bash
python3 scripts/log-analyzer.py table <runDir>/trace/<runId>/run.log
```

生成 12 步逐行表：每 step 的 FSM 状态转换、操作执行、页面分析摘要，一目了然。

### 5.7 FSM 状态 ASCII 时间线

```bash
python3 scripts/log-analyzer.py timeline <runDir>/trace/<runId>/run.log
```

ASCII 时间线展示 FSM 状态随时间流转，含每步间隔和终止点。

### 5.8 Mermaid 状态图

```bash
python3 scripts/log-analyzer.py mermaid <runDir>/trace/<runId>/run.log
```

输出 Mermaid stateDiagram，可直接嵌入 Markdown 渲染。

### 5.9 双 Run 对比表

```bash
python3 scripts/log-analyzer.py compare <runA>/trace/<idA>/run.log <runB>/trace/<idB>/run.log
```

并排对比两个 run 的关键指标：终止原因、步数、操作数、页面分析数、错误/警告。用于回归对比。**注意**：需要 run.log 完整（含 run ended 记录），否则 duration/status 显示 `-`。

---

## Phase 6 — 清理

### 6.1 模拟器

```bash
scripts/android-emulator.sh stop
```

### 6.2 视觉服务

```bash
# 如果手动启动了 Python vision server
pkill -f "tools.local_vision.server" 2>/dev/null || true
```

### 6.3 旧 run 清理

`integration.config.json` 的 `emulator.keepRuns` 控制每个 scenario 保留最近 N 个成功 run + 全部失败 run。手动清理：

```bash
# 查看 run 数量
find artifacts/runs/integration -name "manifest.json" | wc -l

# 删除指定 scope 的所有 run
rm -rf artifacts/runs/integration/scenario-locate/
```

---

## 常用工作流

### 工作流 A：快速手动验证（改完代码立即看效果）

```
1. scripts/android-emulator.sh start          # 启动模拟器
2. dotnet build src/UniClaw.Host -c Debug     # 构建 Host
3. [Phase 2.2] Host CLI run                   # 单次执行
4. 观察 ConsoleTrace 实时输出                 # Phase 3.1
5. [Phase 4.2] diagnose 快速看结果            # 成功/失败 + cause
6. (失败时) [Phase 4.3] 委托 trace-analyzer   # 深度归因
```

### 工作流 B：CI / 自动化批量验证

```
1. scripts/android-emulator.sh start
2. UNICLAW_INTEGRATION_SCOPES=all dotnet test  # Phase 2.1
3. [Phase 4.4] $BIN trace verify --dir artifacts/runs --status pending_verification
4. [Phase 4.6] $BIN trace diff --run-a <baseline> --run-b <latest>  # 回归对比
5. [Phase 5.2] $BIN trace report --dir artifacts/runs --format html
```

### 工作流 C：调试失败 run（离线）

```
1. [Phase 4.1] $BIN trace list --status failure  # 找到失败 run
2. [Phase 4.2] diagnose 快速看 cause + confidence
3. confidence=low → [Phase 4.3] Agent(trace-analyzer, "诊断 run: <dir>")
4. [Phase 4.5] timeline 看耗时瓶颈
5. [Phase 5.1] interactive 逐记录浏览 span 树
```

---

## 日志查阅 vs Trace Analyzer Agent（设计决策）

当前 Host 的"日志"分三类，查阅策略不同：

| 日志类型 | 格式 | 查阅方式 | 负责方 |
|----------|------|----------|--------|
| **ConsoleTrace 实时输出** | 文本行（emoji + 时间戳） | 执行期直接看 stderr；事后可从 run 目录的 trace.jsonl 还原 | **本 skill**（Phase 3.1） |
| **结构化 trace（trace.jsonl）** | JSONL（span/execution/transition/error/ai_call/session） | TraceTool CLI（diagnose/timeline/diff/interactive） | **trace-analyzer agent**（深度）+ 本 skill（shallow 摘要） |
| **产物 JSON（manifest/result/issues/analysis/criteria）** | JSON | 直接 Read；analysis.jsonl 是 matcher/OCR 排查关键证据 | 两者均可——skill 做摘要查看，agent 做归因取证 |
| **ADB 设备日志（logcat/dumpsys）** | 文本 | `adb shell logcat -d`（只读，不 `-c`） | **trace-analyzer agent**（证据不足时补证，Phase 4.3） |
| **FSM 运行日志（如后续添加 ILogger/Serilog）** | 待定 | 待定 | **建议**：shallow 查阅 → 本 skill；pattern 匹配 / 根因推断 → trace-analyzer agent |

### 推荐后续方向

1. **FSM/Host 日志补充后** → Host 运行日志文件（如 `{runDir}/host.log`）写入 run 目录
2. **本 skill 增加日志查阅** → Phase 4 增加 `host-log` 子步骤：tail / grep 运行日志，提取关键事件（FSM 状态转换、异常堆栈、vision server 错误）
3. **trace-analyzer agent 扩展日志取证** → 其 L2 状态机知识可直接用于 FSM log pattern 匹配；Step 3 "深入取证" 增加 Host 运行日志来源
4. **不重复造轮子** → trace-analyzer 已有分层知识体系 + 记忆系统 + 自评反思，日志分析应复用此基础设施而非新建独立 agent

**结论**：**skill 做浅层日志查阅（dashboard），agent 做深层日志分析（diagnosis）**——与当前 trace.jsonl 的分工一致。FSM 日志补充后，skill 增加 `host-log` 快速查看命令，agent 的 Step 3 补证来源增加 Host 运行日志。

---

## 配置中心

所有配置项的优先级：**显式参数 > env var > integration.config.json > 默认值**。

### 模拟器

| 配置 | env var | 默认值 |
|------|---------|--------|
| AVD 名 | `UNICLAW_AVD_NAME` | `uniclaw-lite-api35` |
| 串行 | `UNICLAW_EMULATOR_SERIAL` / `UNICLAW_ADB_SERIAL` | auto（单在线设备） |
| 无窗口 | `UNICLAW_EMULATOR_HEADLESS=1` | 0（有窗口） |
| 启动超时 | `UNICLAW_EMULATOR_BOOT_TIMEOUT` | 180s |

### Provider

| 配置 | env var | integration.config.json 字段 |
|------|---------|------------------------------|
| Provider 选择 | `UNICLAW_INTEGRATION_PROVIDER` / `UNICLAW_PROVIDER` | `scenarios[].provider` |
| Model | `UNICLAW_INTEGRATION_MODEL` / `UNICLAW_MODEL` | `providers.<id>.model` |
| Vision socket | `UNICLAW_VISION_SOCK` | `providers.local.visionServer.socket` |
| YOLO model | `UNICLAW_YOLO_MODEL` | `providers.local.visionServer.yoloModel` |
| OCR backend | `UNICLAW_OCR_BACKEND` | `providers.local.visionServer.ocrBackend` |
| Label mapping | `UNICLAW_LABEL_MAPPING` | `providers.local.visionServer.labelMapping` |

### 执行

| 配置 | env var | 说明 |
|------|---------|------|
| 集成 scope | `UNICLAW_INTEGRATION_SCOPES` | 逗号分隔，或 `all` |
| 输出根 | `UNICLAW_OUTPUT` / `emulator.outputRoot` | run 目录根 |
| Run purpose | `UNICLAW_RUN_PURPOSE` | 写入 manifest.json |
| Task ID | `UNICLAW_TASK_ID` | 关联外部任务系统 |

### 凭据（不进 integration.config.json，只走 env / secrets）

| Provider | env var | 备选来源 |
|----------|---------|----------|
| Claude | `ANTHROPIC_API_KEY` | — |
| DeepSeek | `DEEPSEEK_API_KEY` | — |
| Sensenova | `SENSENOVA_API_KEY` | `~/.litellm/secrets.json` |
| Qwen | `QWEN_API_KEY` | `~/.litellm/secrets.json` |

---

## 禁止事项

1. **不修改源码 / 测试** —— 纯编排层，只调用已有工具
2. **不手写 JSONL 解析** —— trace 读取一律走 TraceTool CLI
3. **不跳过 Phase 1 环境检查** —— 模拟器未就绪时先 doctor 诊断，不直接启动测试
4. **ADB 命令只读** —— 不 `adb shell pm uninstall`、不 `adb reboot`、不清理设备日志（`logcat -c`）
5. **判定不交模型** —— run 成败判定是 TraceTool `VerifyEngine` 确定性 C# 规则，skill 只传递结果不做主观判断
6. **临时文件清理** —— 裸 trace 分析产生的 `/tmp/trace-analysis-*` 用完即删
