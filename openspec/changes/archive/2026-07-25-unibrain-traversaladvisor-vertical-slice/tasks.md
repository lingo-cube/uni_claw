## 1. 输出 schema

- [x] 1.1 在 `src/UniClaw.Core/UniBrain/Schemas.cs` 加 `DecideNextAction` 常量：`ContextDecisionResult` 输出 JSON schema（`result` enum Success/Unsure/GiveUp；`action`/`target`/`reasoning` string；`params` object 可选；`confidence` number 0-1；`safety_verified` boolean；`required: ["result","confidence"]`）

## 2. TraversalAdvisor 真实实现

- [x] 2.1 新建 `src/UniClaw.Core/UniBrain/TraversalAdvisor.cs`：`sealed class : ITraversalAdvisor`，ctor `(IModelRouter router, IPromptLibrary promptLibrary)`，null 参数 → `DomainValidationException` fail-fast（镜像 `TextUnderstanding`）
- [x] 2.2 实现 `DecideNextActionAsync` 7 步：① `promptLibrary.GetTemplate(ModelCapabilities.DecideNextAction)` 缺失 fail-fast ② `JsonSerializer.Serialize(pageAnalysis, DomainJsonOptions.Default)` 注入变量 `{goal}/{page_analysis}/{current_node_id}/{depth}` ③ `ModelRequest(Prompt=resolved.User, SystemPrompt=resolved.System, Schema=Schemas.DecideNextAction, MaxTokens=1024, Capability=DecideNextAction)` ④ `router.Resolve(DecideNextAction)` ⑤ `CompleteTextAsync` ⑥ `resp.Success==false` → `DomainValidationException` 带 ErrorMessage ⑦ 解析 JSON 为 `ContextDecisionResult`
- [x] 2.3 反序列化映射：私有 `DecideNextActionDto`（`Result` string / `Action` / `Target` / `Params Dictionary<string,JsonElement>?` / `Reasoning` / `Confidence` / `SafetyVerified`）；`Result` 大小写不敏感 parse 为 `DecisionResult`，未识别 → `DomainValidationException`；`Params` 按 `ValueKind` 映射 CLR 原始值（String→string / Number→double / True·False→bool / 其余→`GetRawText()`）构建 `ImmutableDictionary<string,object>?`，null 保持 null
- [x] 2.4 `InferContainerTypeAsync` / `HandleExceptionAsync` / `ScreenSafetyAsync` 抛 `NotImplementedException("TraversalAdvisor slice covers decide_next_action only; <method> pending future slice.")`

## 3. prompt 模板与 mock fixture

- [x] 3.1 落定 `decide_next_action` prompt 模板内容（system: 遍历决策顾问角色 + 输出 schema 指示；user: goal + page_analysis JSON + current_node_id + depth），变量集 `{goal}/{page_analysis}/{current_node_id}/{depth}`；回填 design.md 记录终稿
- [x] 3.2 新建 `tests/UniClaw.Core.Tests/Fixtures/decide_next_action.mock.json`：`MockModelEntry` 响应（`capability=decide_next_action`，返回合法 `ContextDecisionResult` JSON）

## 4. 测试

- [x] 4.1 新建 `tests/UniClaw.Core.Tests/UniBrain/TraversalAdvisorTests.cs`：happy path（解析 7 字段，Params 含 `timeout` 为 double）/ 模板缺失 fail-fast / 模型 `Success=false` 传播 / 非法 `result` enum fail-fast / provider-agnostic（Mock 与另一 fake provider 同路径）/ 其余 3 方法抛 `NotImplementedException`
- [x] 4.2 观测闭环断言：经 router 的调用产生 `AICallRecord`（`InMemoryTraceRecorder` + `InMemoryTraceStorage.GetAICalls()` 非空，capability=`decide_next_action`）
- [x] 4.3 新建 `DecideNextActionEndToEndTests.cs`：用 `decide_next_action.mock.json` fixture 走 PromptLibrary + ModelRouter + MockModelProvider → TraversalAdvisor 一条端到端

## 5. 验证

- [x] 5.1 `dotnet build src/UniClaw.Core.sln`：0 错误、0 功能性警告（新文件零警告）
- [x] 5.2 `dotnet test src/UniClaw.Core.sln`：全绿 — 913 通过 / 0 失败 / 0 跳过（+10 本 slice：9 单元 + 1 端到端）
- [x] 5.3 charter guard 不受影响：无新 enum 值（`DecisionResult` 仍 3 锁定）、无新 layer 引用、`ArchitectureGuardTests` 随全量套件 0 失败
