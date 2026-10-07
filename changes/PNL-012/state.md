# PNL-012 — run-diagnosis：planned → implemented

## Intent

run-diagnosis 工具从 planned 翻转为 implemented：Runtime Host 侧新增
model-procedure 执行执法与诊断流程（消费 run-report 产出、经 skill procedure
驱动模型、read-only posture），workbench 观察栏的「诊断当前任务」入口从
disabled 占位变为真实可用。

## Scope

- `src/UniClaw.Host.Dsh/RuntimeToolHost.cs`：outDir 计算重构为共享私有方法
  `ResolveOutputDir`（deterministic-script 与 model-procedure 两分支复用）；
  新增 `InvokeProcedureAsync`（前置执法 → 报告定位 → prompt 组装 → Transport
  调用）与 `DiagnosisTransport` 传输缝（与 `DshSlowConsult.Transport` 同形裁剪）、
  `DiagnosisResult(Model, Text, ReportRef)`、`BuildDiagnosisPrompt`。
- `src/UniClaw.Host.Dsh/RuntimeHttpServer.cs`：`InvokeToolAsync` 按 invocation
  分流；model-procedure 走 `InvokeProcedureToolAsync`（模型经产品缝
  `DshModelManagement.ResolveDshModel(FromBindings(...), AgentDecision)` 解析，
  失败 → 503 routing-unavailable；Transport 生产实现经 `DshOpenedHttpPeer`
  slow 端点；响应形状 model/text/reportRef；错误 envelope 保持统一）。
- `tool-registry.yaml`：run-diagnosis status planned → implemented。
- Web（`web/uniclaw-workspace/`）：controller `diagnoseRun()`（token 竞态防护、
  结果只进 `state.diagnosis`）；view-model `diagnosisGate` 投影（tools 已加载 +
  存在 implemented 的 model-procedure 工具 + toolsRunDir 非空）；renderer 启用
  diagnose-task 按钮（不可用保持 disabled + title 原因）；工具面板新增诊断结果
  区块（文本前强制显示「诊断输出：非权威观察，不构成 Runtime truth」）；
  entry.js 分发。

## Out-of-Scope

- 不修改 `.agents/skills/uniclaw-debug-evidence/SKILL.md`（只读 procedure 载体）、
  `tools/gen-run-report.py` 输出语义（evidence/pnl-006 goldens 不动）、ADR-0039、
  model-routing.yaml 共享层。
- 不把诊断文本写进 run 目录或任何 store（read-only posture：HTTP 响应返回即止）。
- 不新增 model profile choice（见 Decisions D1）。

## Decisions

- D1 模型解析复用 `LogicalProfileId.AgentDecision`：诊断与执行决策是同档语义
  能力，最小诚实接线；不新增 choice、不改 modelSelection 配置。解析不到 →
  显式 `ROUTING_UNAVAILABLE`（503），禁止静默降级。
- D2 有界等待：诊断 prompt 含 SKILL.md 全文 + report.json，显著大于 slow 感知
  prompt，timeout 取 120s（`DshSlowConsult` 15s 的放宽版），超时 → 504
  diagnosis-timeout。
- D3 报告缺失不自动生成：显式 `diagnosis-report-missing` 错误并提示先调
  run-report（报告所有权归 run-report，诊断永不越权补写）。
- D4 非权威声明双保险：prompt 指示模型首行输出声明；展示层（renderer 诊断
  区块）无条件前置同一声明，模型不遵守也不破坏产品约束。

## Acceptance（四元组）

1. 前置执法（planned 拒绝 / 非 workbench 拒 / skillRef 缺失 / procedure 文件
   缺失 / 非 model-procedure invocation 拒绝）
   - method: `dotnet test tests/UniClaw.Host.Dsh.Tests/`（合成 registry 用例）
   - expected: 各非法前置抛 `tool-not-invokable`
   - actual: 11 个 procedure 用例全过（含翻转前 planned 用例）
   - evidence: `RuntimeToolHostProcedureTests`
2. 报告缺失 → 明确错误
   - method: 同上；expected: `diagnosis-report-missing` 含 run-report 提示；
     actual: 通过；evidence: `Invoke_ReportMissing_*`
3. happy path（fake Transport 注入 + 合成 run 目录 + 手写 report.json）
   - method: 同上；expected: prompt 含 SKILL.md 关键内容与报告内容、返回
     text/model/reportRef；actual: 通过；evidence: `Invoke_HappyPath_*`
4. Transport 失败/超时 → 诚实错误
   - method: 同上；expected: `diagnosis-transport-failed` / `diagnosis-timeout`；
     actual: 通过；evidence: `Invoke_TransportFailure_*`、`Invoke_TransportTimeout_*`
5. registry 翻转与词汇执法
   - method: `python3 tools/validate-tool-registry.py`
   - expected: PASSED；actual: PASSED (2 tools)
   - evidence: 命令输出
6. Web 状态机与渲染
   - method: `cd web/uniclaw-workspace && npm test`
   - expected: 门控四边界（未加载/无工具/planned/无 runDir）、按钮 disabled
     切换、诊断区块声明前置、controller happy/no-op/error/竞态
   - actual: 89/89 通过（新增 6 用例）
   - evidence: `tests/workspace-view-model.test.js`、`tests/workspace-renderer.test.js`、
     `tests/workspace-controller.test.js`
7. 构建与共享层
   - method: `python3 tools/validate-workspace-schemas.py` / `npm run build:browser`
   - actual: PASSED 13 schemas / bundle 生成成功
   - evidence: 命令输出

## Constraints

- read-only posture：诊断链路不产生任何文件写入（Transport 只返回文本）。
- 不硬编码模型名；模型名仅在响应投影（provider/name）中出现。

## verification

- level: DETERMINISTIC（单测）+ CONTRACT（校验器/构建）
- `dotnet test tests/UniClaw.Host.Dsh.Tests/` → 30/30 通过
- `cd web/uniclaw-workspace && npm test` → 89/89 通过
- `python3 tools/validate-tool-registry.py` → PASSED (2 tools)
- `python3 tools/validate-workspace-schemas.py` → PASSED 13 schemas
- `npm run build:browser` → 生成成功

## Status log

- 2026-10-XX IMPLEMENT→VERIFY→CLOSED：实现 + 验证全绿；deviations 见下。
  - deviation 1：工单指定在 `.dsh/profiles/uniagent-prod.yaml` 的 modelSelection
    加 choice，但并行流 PRF-002（ADR-0041）已将该文件删除并把模型绑定迁移到
    `.dsh/product/uniagent-prod-bindings.yaml` + `FromBindings`；为不与并行流
    冲突，D1 选择复用 AgentDecision profile，不新增 choice。
  - deviation 2：`RuntimeHttpServer.cs` 同文件内含并行流 PRF-002 未提交 hunks；
    本提交按 hunk 粒度只暂存 PNL-012 的 2 个 hunk，故本提交单独 checkout 时
    依赖 PRF-002 先落地（引用其 `_bindings`/`_agentProfile` 字段）。测试与
    验证均在含 PRF-002 的工作树上运行通过。
