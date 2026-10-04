# CAP-003 — Runtime integration capability simulation
lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）
先用确定性仿真验证 Capability Hub 能承载不同类型的 Runtime Integration 能力，
把“语言校对”和“性能测量”作为两条独立能力链跑通。这样可以先验证装配、解析、
关联和结果契约，再决定真实算法、外部观测器或 DSH Tool 投影的实现方式。

## Scope
- 在 Simulation 测试项目中注册并解析语言校对能力与性能测量能力。
- 语言校对仿真输出已有 `Finding` 契约。
- 性能测量仿真输出已有 `OperationMeasurementPair` 与 `MeasurementSample` 契约。
- 验证两类能力共存于 `RuntimeIntegration` 信任域，且不修改 Product authority。

## Out of Scope
- 真实语言模型、YOLO/OCR、摄像头或设备时钟接入。
- DSH Tool/Skill 注册和 Host adapter。
- 新增 Kernel 生产执行接口或改变 WorldModel、Evidence、Control、Assurance、Effect 语义。

## Decisions
1. 仿真能力只实现 `ICapability`；具体 `Inspect`/`Measure` 方法是测试夹具行为，不冻结生产执行协议。
2. 两个能力使用独立的 Runtime Integration descriptor 和协议名，Hub 只负责注册、解析和生命周期事实。
3. 语言结果沿用 `LanguageInspectorRequest.CreateFinding`；性能结果沿用显式关联的操作起止对和测量样本。
4. 仿真验证关联字段和状态语义，不把确定性夹具的耗时当作真实性能结论。

## Acceptance
1. Hub 能在 Runtime Integration 域同时注册并解析语言校对和性能测量两个实例。
2. 语言校对仿真能基于确定性文本输入产出带原始关联的 `Finding`，并表达通过、违规和输入不可用状态。
3. 性能测量仿真能产出同一关联下的 `OperationMeasurementPair` 与 `MeasurementSample`，包含阶段、状态、指标名、单位和数值。
4. Simulation 定向测试、Kernel/Host 回归和差异检查通过；不改动 DSH 工具面。

## Constraints
- 仿真代码位于 `tests/UniClaw.Simulation.Tests`，不得把测试夹具类型加入 Kernel public surface。
- 不用固定数值伪造外部设备或模型质量结论；测试只证明协议装配和数据关联。

## Verification
level: SCENARIO
method: Simulation capability scenario tests; Kernel and Host regression; `git diff --check`
expected: 两个能力可注册、解析、执行仿真并产出符合现有契约的结果；Kernel/Host 回归保持通过
actual: 4 simulated capability scenarios passed; Kernel 794 and Host 142 passed; full Simulation reported 185 passed and 3 existing certification/timing failures caused by the dirty working-tree source hash and an independent slow-writer threshold; `git diff --check` passed
evidence: current verification turn command output

## Status log
- 2026-10-04 · UNDERSTAND → RESOLVE · 用户决定先用仿真模拟语言校对和性能测量能力。
- 2026-10-04 · RESOLVE → PERSIST → PLAN · 确定只验证 Runtime Integration 装配与结果契约，真实算法和 DSH 投影延后。
- 2026-10-04 · PLAN → IMPLEMENT · 在 Simulation 测试项目中增加确定性语言校对和性能测量夹具，并通过同一 Hub 注册解析。
- 2026-10-04 · IMPLEMENT → VERIFY · 定向仿真 4/4、Kernel 794/794、Host 142/142 通过；全量 Simulation 的 3 个失败均落在既有认证哈希/时序测试，不涉及新增场景。
- 2026-10-04 · VERIFY → CLOSED · 定向 acceptance 和回归通过，差异检查通过；真实算法、设备观测和 DSH Tool 保持未实现。
