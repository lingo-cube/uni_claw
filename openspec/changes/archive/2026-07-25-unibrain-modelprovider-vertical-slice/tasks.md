## 1. 基础标签层

- [x] 1.1 新增 `src/UniClaw.Core/UniBrain/ModelCapabilities.cs`：static class，5 个 `public const string`（`ParseInstruction="parse_instruction"` / `VerifyPageType="verify_page_type"` / `DecideNextAction="decide_next_action"` / `ScreenSafety="screen_safety"` / `AnalyzeVisual="analyze_visual"`）。**排除** `verify_page_with_vision`（C# `IPageAnalyzer` YAGNI）。对照 model-provider ADDED「ModelCapabilities defines 5 capability string constants」+ scenario。
- [x] 1.2 修改 `src/UniClaw.Core/UniBrain/ModelRequest.cs`：record 末尾加 `string? Capability = null`（可选，向后兼容）。验证现有 `ModelRequest` 用法不破。对照 model-provider MODIFIED R2 + 「Capability flows through router and decorator」scenario。

## 2. 传输级 mock（Core/Simulation，namespace `UniClaw.Core.Simulation`）

- [x] 2.1 新增 `MockModelEntry`（sealed record：`string Content`, `int InputTokens=0`, `int OutputTokens=0`, `double LatencyMs=0`, `bool Success=true`, `string? ErrorMessage=null`）。
- [x] 2.2 新增 `MockModelFixture`（sealed record，持 `ImmutableDictionary<string, MockModelEntry> Responses`；构造期 `DomainValidationException` 校验非 null；`MockModelEntry? Resolve(string capability)`；`static FromJson(string)` + 内部 DTO + DomainJsonOptions）。⚠️ **校验是纯新增**——`StateFixture` 现状不带 `DomainValidationException`（用 InvalidOperationException），勿照抄；仅复用 sealed-record + FromJson + DTO 结构。对照 model-provider ADDED mock requirement 的「Fixture loaded from JSON」scenario。
- [x] 2.3 新增 `MockModelProvider`（sealed class : `IModelProvider`；构造 `(MockModelFixture fixture, string providerId = "mock")`；`CompleteTextAsync` 按 `request.Capability ?? ""` 查 fixture，未命中 `DomainValidationException`，命中返回 `ModelResponse`（Content/tokens/latency/Success/ErrorMessage, ProviderId, Mode="text"）；Vision/Multimodal throw NIE）。对照 mock 的「Preset response」/「Missing preset fails fast」scenarios。
- [x] 2.4 建 fixture JSON（目录见 design OQ2，倾向 `tests/fixtures/parse_instruction.mock.json`），含 `parse_instruction` 预设响应（合法 JSON 内容，供 TextUnderstanding 解析）。

## 3. 观测 decorator（Core/UniBrain）

- [x] 3.1 新增 `ObservingModelProvider`（sealed class : `IModelProvider`；构造 `(IModelProvider inner, ITraceRecorder recorder)`；`ProviderId => inner.ProviderId`；`CompleteTextAsync` = Stopwatch 计时 → `inner.CompleteTextAsync` → `recorder.RecordAICallAsync` → 原样返回；Vision/Multimodal 对称，mode 分别 "vision"/"multimodal"；不吞 inner 异常、不改 response）。⚠️ **AICallRecord 构造细节**：实际 record 有 8 字段（`Capability/ProviderId/Success/LatencyMs/Context?/Tokens?/Timestamp/Metadata?`）—— `Context` 留默认 null（切片无 span 上下文）、`Timestamp` 默认、`Tokens = resp.InputTokens + resp.OutputTokens`、`Metadata` 至少含 `{model, mode, error?}`。对照 decorator 的 3 scenarios（成功/失败/ProviderId 委托）。

## 4. 路由层（Core/UniBrain）

- [x] 4.1 新增 `IModelRouter`（interface：`IModelProvider Resolve(string capability)`）。
- [x] 4.2 新增 `ModelRouter`（sealed : `IModelRouter`；构造 `(ImmutableDictionary<string,string> capabilityRouting, ImmutableDictionary<string,IModelProvider> providers, ITraceRecorder recorder, string defaultProviderId)`；构造期校验 routing 引用的 providerId 都在 providers（否则 `DomainValidationException`），并为每个裸 provider 套 `new ObservingModelProvider(inner, recorder)` 存内部表；`Resolve` = 查表 → `defaultProviderId` 回落 → 仍无则 `DomainValidationException`，只返回已套实例）。对照 IModelRouter 的 5 scenarios。

## 5. 业务语义层（Core/UniBrain）

- [x] 5.1 定义 `ParseInstructionSchema`（约束 `{category, confidence, entities, summary}`；倾向 `const string` JSON schema，见 design OQ3）。
- [x] 5.2 新增 `TextUnderstanding`（sealed class : `ITextUnderstanding`；构造 `(IModelRouter router, IPromptLibrary promptLibrary)`；`UnderstandTextAsync` 7 步：取模板（`GetTemplate(ParseInstruction)`，null → `DomainValidationException`）→ `tpl.Resolve(IReadOnlyDictionary<string,string>{["text"]=Text, ["context"]=Context??""})` → 组 `ModelRequest(Prompt, SystemPrompt, Schema=ParseInstructionSchema, MaxTokens=1024, Capability=ParseInstruction)` → `router.Resolve` → `CompleteTextAsync` → `Success==false` → `DomainValidationException` 携 ErrorMessage → 解析 JSON 成 `TextUnderstandingResult`）。⚠️ **provider-agnostic**，不引具体 provider 类型；**不动** provider 项目的 `DeepSeekTextUnderstanding`/`ClaudeTextUnderstanding` stub（D8）。对照 text-understanding ADDED 的 4 scenarios。
- [x] 5.3 单元测试（无网络，全用 `MockModelProvider`）：TextUnderstanding（happy path / 模板缺失 fail-fast / 模型失败 fail-fast / provider-agnostic）；`ObservingModelProvider`（成功记 AICallRecord / 失败记 error / ProviderId 委托）；`ModelRouter`（按 capability 路由 / default 回落 / 未知+无 default fail-fast / 构造期未知 provider fail-fast / 返回必被观测）。逐条对照 spec scenario。

## 6. 真实传输（UniClaw.DeepSeekProvider）

- [x] 6.1 新增 `DeepSeekProviderConfig`（sealed record：`ApiKey`/`Model`/`BaseUrl`/`MaxConcurrentRequests=4`/`RequestTimeoutSeconds=30.0`；构造期 `DomainValidationException` 校验非空 + >0）。对照 DeepSeekProviderConfig 的 2 scenarios。
- [x] 6.2 填实 `DeepSeekModelProvider`（构造 `(HttpClient, DeepSeekProviderConfig)`；`CompleteTextAsync` POST `{BaseUrl}/chat/completions`，header `Authorization: Bearer {ApiKey}`，body 含 `model/messages/max_tokens`、`Schema!=null` 时加 `response_format:{type:"json_object"}`；映射 `choices[0].message.content`→Content、`usage.prompt_tokens/completion_tokens`→Input/OutputTokens、Mode="text"；HTTP 非 2xx/超时/JSON 解析失败 → `ModelResponse(Success:false, ErrorMessage)` **不抛**；Vision/Multimodal throw NIE）。对照 DeepSeekModelProvider 的 4 scenarios。
- [x] 6.3 集成测试（opt-in，无 `DEEPSEEK_API_KEY` 跳过；注入方式见 design OQ4）：成功映射 / Schema 触发 json_object / HTTP 错误 graceful / Vision+Multimodal NIE。

## 7. 收尾校验

- [x] 7.1 `dotnet build src/UniClaw.Core.sln`：0 错误、0 功能性警告。
- [x] 7.2 `dotnet test src/UniClaw.Core.sln`：全绿（原有 840+ 测试 + 新增单测）。确认 `ArchitectureGuardTests` 不破 —— UniBrain→Observability 方向依 D-17 现有 guard 不验（不拦 `ObservingModelProvider` 引用 `ITraceRecorder`），guard 测试本身仍绿。
- [x] 7.3 垂直切片端到端验证：手工组合跑通 `parse_instruction` 链路（design §11 组合示例，**修正**：`InMemoryTraceRecorder` 构造需注入 `ITraceStorage`），确认 `AICallRecord` 产生。
- [x] 7.4 标记供 archive 提取的决策：D-E11 spec 契约翻转（model-provider R1）、D8 provider stub 待清理、D9 capability 词汇表待整合 —— 写入 design 已完成，archive 阶段同步 decisions/log.md + 四层文档。
