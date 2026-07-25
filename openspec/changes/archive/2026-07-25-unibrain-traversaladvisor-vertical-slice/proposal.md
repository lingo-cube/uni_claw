## Why

`unibrain-modelprovider-vertical-slice` 已跑通 `parse_instruction` 一条端到端链，验证了 `IModelProvider` 抽象栈（`IModelRouter` 路由 + `ObservingModelProvider` decorator + `ModelRequest.Capability` 标签 + 传输级 `MockModelProvider`）站得住、观测闭环接通、模式可复用。

但该模式目前只验证了**最简单的形态**——纯文本入 → 扁平结构出（`TextUnderstandingRequest` → `TextUnderstandingResult`）。`ITraversalAdvisor` 仍无任何实现类（接口存在，5 个子接口 stub 已在上一 change 清理）。本 change 通过**第二条垂直切片**——跑通 `decide_next_action` 一条端到端链——验证该模式能否推广到更复杂的形态：

- **结构化状态入**：`DecideNextActionAsync` 吃 `PageAnalysis`（Domain.Content 复合类型），必须序列化进 prompt，而非简单字符串拼接。
- **丰富决策出**：返回 `ContextDecisionResult`（7 字段，含 `Params` 字典与 `Reasoning`），比 `TextUnderstandingResult` 的解析约束更松、字段更多，暴露 JSON→record 的映射边界（尤其 `ImmutableDictionary<string,object>?` 的反序列化陷阱）。

跑通这条切片即可把「router + decorator + capability 标签 + 传输级 mock」模式确认为 UniBrain 全部 5 个 capability 的通用范式，并为剩余 3 个 `ITraversalAdvisor` 方法（`InferContainerType` / `HandleException` / `ScreenSafety`）铺路。

## What Changes

- **新增 `TraversalAdvisor` 真实实现**（`Core/UniBrain/`，provider-agnostic）：`sealed class : ITraversalAdvisor`，消费 `IModelRouter` + `IPromptLibrary`，仅依赖抽象、不引用任何具体 provider 类型。`DecideNextActionAsync` 7 步链路（取模板 → 解析变量 → 序列化 `PageAnalysis` 进 prompt → `ModelRequest` → router → `CompleteTextAsync` → 解析 JSON 为 `ContextDecisionResult`），镜像 `TextUnderstanding` 的结构与 fail-fast 策略。
- **切片边界（显式）**：本 slice 只实现 `DecideNextActionAsync` 真实链路；`ITraversalAdvisor` 其余 3 方法（`InferContainerTypeAsync` / `HandleExceptionAsync` / `ScreenSafetyAsync`）抛 `NotImplementedException`（带 "pending future slice" 文案），同上一 change 对 Vision / Anthropic stub 的诚实部分实现策略。
- **新增 `Schemas.DecideNextAction` 常量**：`ContextDecisionResult` 输出 JSON schema（`result` 枚举锁定 Success/Unsure/GiveUp；`confidence` 0-1；`params` 可选 object）。
- **`decide_next_action` prompt 模板**：变量 `{goal}` / `{page_analysis}` / `{current_node_id}` / `{depth}`；模板内容随 design 落定，注册发生在组合根 / 测试 wiring（与 `parse_instruction` 同侧）。
- **测试**：`TraversalAdvisorTests`（`MockModelProvider`，无网络）覆盖 happy path / 模板缺失 / 模型失败 / provider-agnostic 路由；`DecideNextActionEndToEndTests`（mock fixture）一条端到端；额外断言经 router 的调用产生 `AICallRecord`（观测闭环复用）。

## Capabilities

### New Capabilities

无。本 change 落在已存在的 capability 上。

### Modified Capabilities

- `traversal-advisor`:
  - **ADDED** `TraversalAdvisor` 真实实现 requirement（`sealed class : ITraversalAdvisor`，消费 `IModelRouter` + `IPromptLibrary`；`DecideNextActionAsync` 7 步 SHALL；模板缺失 / 模型失败 fail-fast；`PageAnalysis` 序列化进 prompt；JSON→`ContextDecisionResult`；其余 3 方法 `NotImplementedException`）。现有 7 个 requirement（接口签名 / `ContextDecisionResult` / `DecisionResult` / `ContainerInference` / `SafetyScreeningResult` / `PageTypeVerification` / `Suggestion`）不变。

## Impact

- **代码**：
  - `src/UniClaw.Core/UniBrain/`：新增 `TraversalAdvisor`（1 类）+ 修改 `Schemas`（加 `DecideNextAction` 常量）
  - `tests/UniClaw.Core.Tests/UniBrain/`：新增 `TraversalAdvisorTests` + `DecideNextActionEndToEndTests` + `Fixtures/decide_next_action.mock.json`
- **spec 契约**：`traversal-advisor` 1 ADDED requirement（不动现有 7 个；接口签名零变更）
- **架构方向**：零新方向。复用上一 slice 已确立的 router + decorator + capability 范式；不新增 enum（`DecisionResult` 3 值锁定，schema 仅引用）；不新增 layer 引用；`UniBrainGuardTests` 不受影响
- **测试**：单元（`TraversalAdvisor`，用 `MockModelProvider`，无网络）+ 端到端（mock fixture）；无 live HTTP（`DeepSeekModelProvider` 已由上一 change 覆盖，本 slice 不重复）
- **不动**：`ITraversalAdvisor` 其余 3 方法的真实实现（另起 slice）；生产组合根的 prompt 模板注册（本 slice 模板在测试侧 wiring，生产 registry 留待统一 wiring change）；`ContextDecisionResult.Confidence` 的 0-1 构造期校验（类型现状无校验，本 slice 不改类型契约，见 design 取舍）；`Params` 嵌套 object 支持（本 slice 仅原始值 string/number/bool，见 design）
- **Python 偏离**（理由见 design）：`PageAnalysis` 序列化进 prompt 用 `DomainJsonOptions.Default`（camelCase + enum-as-string），与 Python 把 page dict str() 进 prompt 等价但格式更结构化；`Params` 反序列化映射为 CLR 原始值（Python 直接吃 dict）
