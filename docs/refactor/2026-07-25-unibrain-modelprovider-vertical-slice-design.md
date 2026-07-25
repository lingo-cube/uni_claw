# UniBrain ModelProvider 实现设计 — 垂直切片

> **状态**: 设计稿（待 writing-plans 拆任务）
> **日期**: 2026-07-25
> **作者**: Fran
> **上游**: `docs/refactor/2026-07-21-unibrain-concept-design.md`（UniBrain 概念设计）
> **范围**: 垂直切片 — 跑通 `TextUnderstanding`（`parse_instruction`）一条端到端链路

---

## 1. 动机与背景

UniBrain 抽象层已齐备，但**整个实现层是空的**：

| 层 | 状态 |
|---|---|
| `IModelProvider` / `ModelRequest` / `ModelResponse` / `UniBrainConfig` | ✅ 已定义 |
| `IUniBrain` facade + 3 子接口（`IPageAnalyzer` / `ITraversalAdvisor` / `ITextUnderstanding`） | ✅ 签名齐全 |
| `IPromptLibrary` / `PromptTemplate` / `ResolvedPrompt`（Prompt Engine） | ✅ 已定义 |
| **子接口的真实实现**（消费 `IModelProvider`） | ❌ 全是 Simulation fixture mock，不经 `ModelRequest/Response` |
| `AnthropicModelProvider` / `DeepSeekModelProvider`（独立项目） | ⚠️ stub，全 `NotImplementedException`，无构造器、无 SDK、无 HttpClient |
| `MockModelProvider` | ❌ 不存在 |
| 观测挂钩 | ⚠️ `AICallRecord` + `ITraceRecorder.RecordAICallAsync` 已就位，但 IModelProvider 链路未接通 |
| DI 容器 / `IHttpClientFactory` / `HttpClient` | ❌ src 全量零命中，纯 Domain 层 |

**与 Python 的最大分叉**：Python 的 `UniBrain`（`src/ai/provider.py`）自己持有 `providers` dict + 做 capability routing；C# 的 `UniBrainService` 是**纯组合容器**，不做路由、不持有 `IModelProvider`，routing 意图只停留在 `UniBrainConfig.CapabilityRouting` 这个 stub 字段里，无人消费。

**待解矛盾（D-E11）**：`IModelProvider.cs:7` 注释说"不负责 AICallRecord — 子接口实现负责"，但概念设计文档说"ModelProvider 层负责观测"。观测挂哪一层决定基类设计。

本设计通过一条**最小垂直切片**（`parse_instruction`）验证 `IModelProvider` 抽象是否站得住、接通观测闭环、暴露基础设施缺口，并把可复用的模式铺给其余 4 个 capability 与其余 provider。

---

## 2. 设计原则（6 条要求 → 设计决策）

| 要求 | 设计落点 |
|---|---|
| 职责清晰 / 边界清晰 | 三层分离：`IModelProvider`（传输，只管 HTTP/数据）‖ `IModelRouter`（路由+组装）‖ 子接口实现（业务语义，组装 prompt / 解析响应）。`ModelRequest.Capability` 是流经三层的唯一语义标签。 |
| 高内聚低耦合 | DeepSeek 的 `System.Net.Http` 关在 provider 项目，Core 零引用（ArchitectureGuard 不破）。decorator 把观测（及未来重试/缓存）横切与传输解耦。传输级 mock 与子接口级 mock 各自独立，不互相依赖。 |
| 观测清晰 | `AICallRecord` 记 `Capability/ProviderId/Success/LatencyMs/Tokens`，prompt/response 摘要进 `Metadata`。`ObservingModelProvider` 是**唯一**挂钩点（D-E11 落地），且因 router 组装期统一套用而**结构上不可绕过**。 |
| 利于扩展变更 | 新 provider = 实现接口 + 注册；新 capability = 加常量 + 路由条目；新横切 = 加 decorator；prompt 变更 = `IPromptLibrary`。开放封闭，每条扩展都是加法不改旧代码。 |
| 测试梯度清晰 | 5 级测试各有明确 provider 接缝，互不混淆（见 §10）。 |
| 链路可端到端 | `IUniBrain` 整体可注入 TraversalEngine；垂直切片手工组装，后续可平滑替换为 DI 组合根。 |

---

## 3. 目标与非目标

### 目标
1. 跑通 `TextUnderstanding.UnderstandTextAsync` 一条端到端链：`TextUnderstandingRequest → IPromptLibrary → ModelRequest → IModelRouter → IModelProvider → ModelResponse → TextUnderstandingResult`
2. 实现 `DeepSeekModelProvider`（真实 HTTP，OpenAI-compatible）+ `DeepSeekProviderConfig`
3. 实现 `MockModelProvider` + `MockModelFixture`（传输级声明式 mock）
4. 实现 `ObservingModelProvider`（decorator，唯一观测挂钩点）
5. 实现 `IModelRouter` + `ModelRouter`（capability 路由 + 组装期套 decorator）
6. 定义 `ModelCapabilities` 5 常量；给 `ModelRequest` 加 `Capability` 字段
7. 接通观测闭环：所有 AI 调用产生 `AICallRecord`

### 非目标（延后项）
- `IPageAnalyzer` / `ITraversalAdvisor` 的真实实现（另 4 个 capability）
- `AnthropicModelProvider` 填实（`Claude`/`MiMo`/`MCP` provider）
- DI 容器 / `IHttpClientFactory` / Polly 重试治理
- yaml 配置加载（`ai_providers.yaml` 等价物）
- `UniBrainConfig.CapabilityRouting` 的运行时配置加载（垂直切片硬编码 routing）
- 重试 / 缓存 / 限流 decorator（结构预留，本期不实现）

---

## 4. 架构总览

```
                  tests / 调用方（无 DI，手工组装）
   ┌──────────────────────────────────────────────────────────┐
   │  new ModelRouter(                                         │
   │      routing:    { ParseInstruction → "deepseek" },        │
   │      providers:  { "deepseek" → new DeepSeekModelProvider │ ← 裸 provider
   │                    (httpClient, dsCfg),                    │
   │                    "mock" → new MockModelProvider(          │
   │                               new MockModelFixture(json)) },│
   │      recorder:   traceRecorder,                           │ ← 组装期套 decorator
   │      default:    "deepseek")                              │
   │     │ 构造期：每个裸 provider 套 ObservingModelProvider     │
   │     ▼                                                      │
   │  router.Resolve("parse_instruction")                       │
   │     → ObservingModelProvider(DeepSeekModelProvider)        │ ← 已套，观测不可绕过
   └──────────────────────────────────────────────────────────┘
                  │
   Core/UniBrain  ▼   (纯净 — 零 System.Net.Http)
   TextUnderstanding 实现
     TextUnderstandingRequest
       → IPromptLibrary.GetTemplate(ParseInstruction).Resolve({text, context})
       → ResolvedPrompt → ModelRequest(Prompt=User, SystemPrompt=System, Capability=ParseInstruction)
       → router.Resolve(request.Capability).CompleteTextAsync(request)
       → (decorator 记 AICallRecord) → ModelResponse
       → 解析 Content (JSON) → TextUnderstandingResult
```

**三层职责**：

| 层 | 关注点 | 类型 |
|---|---|---|
| 传输 | HTTP / 数据序列化 / 协议错误映射 | `DeepSeekModelProvider`、`MockModelProvider` |
| 路由+组装 | capability → provider、组装期套横切 | `ModelRouter`、`IModelRouter` |
| 业务语义 | 组装 prompt、解析响应成 Domain 类型 | `TextUnderstanding` 实现 |

---

## 5. 类型清单

### 5.1 Core/UniBrain — 新增

```csharp
// capability 字符串常量（对齐 Python 5 capability，跨语言可对照）
public static class ModelCapabilities
{
    public const string ParseInstruction = "parse_instruction"; // ITextUnderstanding  ← 垂直切片主轴
    public const string VerifyPageType   = "verify_page_type";  // IPageAnalyzer.VerifyPageTypeAsync
    public const string DecideNextAction = "decide_next_action";// ITraversalAdvisor.Decide/HandleException
    public const string ScreenSafety     = "screen_safety";     // ITraversalAdvisor.ScreenSafetyAsync
    public const string AnalyzeVisual    = "analyze_visual";    // IPageAnalyzer.Analyze/FindAppEntry
    // 注：排除 Python 的 verify_page_with_vision — C# 已 YAGNI（IPageAnalyzer.cs:10）
}

// 极简路由：capability → 已套 decorator 的 IModelProvider
public interface IModelRouter
{
    IModelProvider Resolve(string capability);
}

// sealed 实现：组装中心。构造期接收裸 provider + recorder，内部套 ObservingModelProvider。
public sealed class ModelRouter : IModelRouter
{
    public ModelRouter(
        ImmutableDictionary<string, string> capabilityRouting,   // capability → providerId
        ImmutableDictionary<string, IModelProvider> providers,   // providerId → 裸 provider
        ITraceRecorder recorder,
        string defaultProviderId);
    // 构造期：为每个 providers[value] 套 new ObservingModelProvider(inner, recorder)，存内部表
    // Resolve: 查 capabilityRouting，未命中回落 defaultProviderId，再未命中抛 DomainValidationException
}

// decorator：唯一观测挂钩点（D-E11 落地）
public sealed class ObservingModelProvider : IModelProvider
{
    public ObservingModelProvider(IModelProvider inner, ITraceRecorder recorder);
    public string ProviderId => inner.ProviderId;
    // CompleteTextAsync:
    //   ① Stopwatch 计时
    //   ② var resp = inner.CompleteTextAsync(request, ct)
    //   ③ recorder.RecordAICallAsync(new AICallRecord(
    //        Capability: request.Capability ?? "",
    //        ProviderId: inner.ProviderId,
    //        Success: resp.Success,
    //        LatencyMs: sw.Elapsed.TotalMilliseconds,
    //        Tokens: resp.InputTokens + resp.OutputTokens,
    //        Metadata: { ["model"] = resp.Model,
    //                     ["mode"] = "text",
    //                     ["error"] = resp.ErrorMessage? }))
    //   ④ return resp
    // Vision/Multimodal 同理，mode 分别为 "vision"/"multimodal"
}

// TextUnderstanding 真实实现（业务语义层）
public sealed class TextUnderstanding : ITextUnderstanding
{
    public TextUnderstanding(IModelRouter router, IPromptLibrary promptLibrary);
    // UnderstandTextAsync:
    //   ① var tpl = _promptLibrary.GetTemplate(ModelCapabilities.ParseInstruction)
    //      tpl == null → 抛 DomainValidationException("prompt template missing: parse_instruction")
    //   ② var resolved = tpl.Resolve(new Dictionary<string,string>{ ["text"]=request.Text, ["context"]=request.Context??"" })
    //   ③ var mr = new ModelRequest(Prompt: resolved.User, SystemPrompt: resolved.System,
    //                               Schema: ParseInstructionSchema, MaxTokens: 1024,
    //                               Capability: ModelCapabilities.ParseInstruction)
    //   ④ var provider = _router.Resolve(mr.Capability!)
    //   ⑤ var resp = await provider.CompleteTextAsync(mr, ct)
    //      resp.Success == false → 抛/回落（见 §9）
    //   ⑥ 解析 resp.Content (JSON) → TextUnderstandingResult(Category, Confidence, Entities, Summary)
}
```

### 5.2 Core/UniBrain — 修改

```csharp
// ModelRequest 增加 Capability 字段（唯一破坏性改动，加可选字段，向后兼容）
public sealed record class ModelRequest(
    string Prompt,
    string? SystemPrompt = null,
    object? Schema = null,
    int MaxTokens = 4096,
    string? Capability = null);   // ← 新增：语义标签，流经三层
```

### 5.3 Core/Simulation — 新增（传输级 mock，与子接口级 mock 同命名空间）

```csharp
namespace UniClaw.Core.Simulation;

// 平行于 StateFixture，复用其 sealed-record + FromJson + DTO + 校验 模式（StateFixture.cs:48-76）
public sealed record class MockModelEntry(
    string Content,                 // AI 会返回的原始文本/JSON（让真实 TextUnderstanding 实现去解析）
    int InputTokens = 0,
    int OutputTokens = 0,
    double LatencyMs = 0,
    bool Success = true,
    string? ErrorMessage = null);

public sealed record class MockModelFixture
{
    public ImmutableDictionary<string, MockModelEntry> Responses { get; }  // capability → 预设响应
    public MockModelFixture(ImmutableDictionary<string, MockModelEntry> Responses);  // DomainValidationException 校验
    public MockModelEntry? Resolve(string capability);
    public static MockModelFixture FromJson(string json);   // DTO 反序列化，对齐 StateFixture.FromJson 风格
    internal sealed class MockModelFixtureDto { ... }
}

// 传输级 mock：消费 MockModelFixture，平行于 MockPageAnalyzer 消费 StateFixture
public sealed class MockModelProvider : IModelProvider
{
    public MockModelProvider(MockModelFixture fixture, string providerId = "mock");
    public string ProviderId { get; }
    // CompleteTextAsync:
    //   var entry = _fixture.Resolve(request.Capability ?? "")
    //     ?? throw new DomainValidationException("mock: no preset for capability", request.Capability)
    //   return Task.FromResult(new ModelResponse(entry.Content, ProviderId, "text",
    //       entry.InputTokens, entry.OutputTokens, entry.LatencyMs, Success: entry.Success, ...))
    // Vision/Multimodal：垂直切片只服务 text 模式，throw NotImplementedException（与 DeepSeekModelProvider 对称）
}
```

> **Mock 位置决策**：所有 mock 集中在 `Core/Simulation/`（与 `MockPageAnalyzer` / `MockTextUnderstanding` / `MockTraversalAdvisor` 一致）。`MockModelProvider` 实现 `IModelProvider`（UniBrain 命名空间接口），但实现体放 Simulation —— 与现有 mock 内聚，且 `ObservingModelProvider`（生产 decorator，非 mock）放 `Core/UniBrain/` 区分。

### 5.4 UniClaw.DeepSeekProvider — 填实 / 新增

```csharp
// 新增：对齐 Python AIProviderConfig（base.py AIProviderConfig / config.py AIProviderConfig）
public sealed record class DeepSeekProviderConfig(
    string ApiKey,
    string Model,
    string BaseUrl,
    int MaxConcurrentRequests = 4,
    double RequestTimeoutSeconds = 30.0);
// 构造期 fail-fast：ApiKey/Model/BaseUrl 非空，并发>0，超时>0 → DomainValidationException

// 填实现有 stub（DeepSeekModelProvider.cs，当前无构造器、三方法 throw）
public sealed class DeepSeekModelProvider : IModelProvider
{
    public DeepSeekModelProvider(HttpClient http, DeepSeekProviderConfig config);
    public string ProviderId => "deepseek";
    // CompleteTextAsync: 组装 OpenAI-compatible 请求体 → POST {BaseUrl}/chat/completions
    //   请求体: { model, messages:[{system?},{user}], max_tokens, response_format:{type:"json_object"} (当 Schema≠null) }
    //   Authorization: Bearer {ApiKey}
    //   响应: choices[0].message.content → ModelResponse.Content
    //         usage.prompt_tokens/completion_tokens → InputTokens/OutputTokens
    //   错误（HTTP 非 2xx / 超时 / JSON 解析失败）→ ModelResponse(Success:false, ErrorMessage)（graceful，见 §9）
    // Vision/Multimodal：垂直切片不实现，throw NotImplementedException（DeepSeek text-only 优先）
}
```

---

## 6. 数据流（垂直切片详图）

```
TextUnderstandingRequest(Text="打开设置", Context="主页")
  │
  ▼ ① 取模板
IPromptLibrary.GetTemplate("parse_instruction") → PromptTemplate(SystemPrompt, UserPrompt, Variables=[text,context])
  │
  ▼ ② 填变量
PromptTemplate.Resolve({text:"打开设置", context:"主页"}) → ResolvedPrompt(System, User)
  │
  ▼ ③ 组装请求
ModelRequest(Prompt=User, SystemPrompt=System, Schema=ParseInstructionSchema, MaxTokens=1024, Capability="parse_instruction")
  │
  ▼ ④ 路由
ModelRouter.Resolve("parse_instruction") → ObservingModelProvider(DeepSeekModelProvider)
  │
  ▼ ⑤ 调用（decorator 计时 + 委托）
DeepSeekModelProvider.CompleteTextAsync → POST /chat/completions → JSON 响应
  │
  ▼ ⑥ 包装响应
ModelResponse(Content="{category,confidence,entities,summary}", ProviderId="deepseek", Mode="text", InputTokens, OutputTokens, LatencyMs, Success=true)
  │
  ▼ ⑦ decorator 记录
ITraceRecorder.RecordAICallAsync(AICallRecord(Capability="parse_instruction", ProviderId="deepseek", Success=true, LatencyMs, Tokens, Metadata))
  │
  ▼ ⑧ 解析
TextUnderstandingResult(Category, Confidence, Entities, Summary)
```

**DeepSeek API 映射**（OpenAI-compatible）：

| ModelRequest / ModelResponse | DeepSeek /chat/completions |
|---|---|
| `SystemPrompt` | `messages[0] = {role:"system", content:SystemPrompt}`（省略当 null） |
| `Prompt` | `messages[N] = {role:"user", content:Prompt}` |
| `Schema != null` | `response_format = {type:"json_object"}`（DeepSeek JSON mode） |
| `MaxTokens` | `max_tokens` |
| — | `model = config.Model`，`Authorization: Bearer config.ApiKey` |
| `Content` | `choices[0].message.content` |
| `InputTokens` / `OutputTokens` | `usage.prompt_tokens` / `usage.completion_tokens` |
| `Mode` | `"text"`（CompleteTextAsync 固定） |

`ParseInstructionSchema`（约束 AI 输出 `TextUnderstandingResult` 形态）：`{type:"object", properties:{category:{type:"string"}, confidence:{type:"number"}, entities:{type:"array",items:{type:"string"}}, summary:{type:"string"}}, required:["category","confidence","entities"]}`。

---

## 7. Capability 路由

垂直切片 routing 表（硬编码于测试/调用方组装点）：

| capability | providerId |
|---|---|
| `parse_instruction` | `deepseek` |

其余 4 个 capability（`verify_page_type` / `decide_next_action` / `screen_safety` / `analyze_visual`）的常量已定义，routing 条目在各自子接口实现落地时补。`defaultProviderId = "deepseek"`。

> `UniBrainConfig.CapabilityRouting` stub 字段（`UniBrainConfig.cs:13`）本期**不被消费**——routing 由 `ModelRouter` 构造器参数直接传入。运行时配置加载（yaml/Options → CapabilityRouting → ModelRouter）属非目标。

---

## 8. 观测设计

**唯一挂钩点**：`ObservingModelProvider`（decorator）。`AICallRecord` 字段映射：

| AICallRecord 字段 | 来源 |
|---|---|
| `Capability` | `request.Capability` |
| `ProviderId` | `inner.ProviderId` |
| `Success` | `resp.Success` |
| `LatencyMs` | decorator `Stopwatch` |
| `Tokens` | `resp.InputTokens + resp.OutputTokens` |
| `Metadata` | `{model, mode, error?, prompt摘要?, response摘要?}` |

**不可绕过保证**：`ModelRouter` 构造期为每个裸 provider 套 `ObservingModelProvider`，`Resolve` 只返回已套实例。所有经 router 的 AI 调用必然产生 `AICallRecord`。不依赖开发者约定。

**依赖方向**：UniBrain 层引用 `ITraceRecorder`（Observability）。依 D-17（Observability 为 cross-cutting utility，非传统顶层），与 StateMachine/Traversal 引用 Observability 同等待遇，不视为向上违规。当前 `ArchitectureGuardTests` 不验此方向（与 D-17 现状一致）。

---

## 9. 错误处理策略

两类边界，严格区分（对齐项目 fail-fast vs graceful 双策略）：

| 场景 | 策略 | 表现 |
|---|---|---|
| **构造/配置校验** | fail-fast | `DomainValidationException`：`DeepSeekProviderConfig` 字段非法、`ModelRouter` routing 引用未知 provider、capability 无法路由、prompt 模板缺失、`TextUnderstandingRequest.Text` 空（已有）、`TextUnderstandingResult.Confidence` 越界（已有） |
| **传输/运行时错误** | graceful | `ModelResponse(Success:false, ErrorMessage)`，**不抛**：HTTP 非 2xx、超时、JSON 解析失败、DeepSeek 返回错误体 |

**`ModelResponse.Success == false` 的上层处理**：`TextUnderstanding` 实现检测到失败响应时，抛 `DomainValidationException("model call failed", resp.ErrorMessage)`（让上层引擎决策重试/降级）—— 本期简化策略，未来可演化为返回带 `Confidence=0` 的降级 `TextUnderstandingResult` 或触发重试 decorator。

---

## 10. 测试梯度

每级有明确 provider 接缝，互不混淆：

| 测试级 | provider / mock 接缝 | 被测真实逻辑 | 网络 | fixture |
|---|---|---|---|---|
| **单元** | `MockModelProvider`（内联 `MockModelEntry`） | TextUnderstanding 的 prompt 组装 + 响应解析；decorator 记录；router 路由 | 无 | 内联 |
| **仿真** | 现有 `MockTextUnderstanding`（**子接口级**业务 mock）+ `StateFixture` | TraversalEngine 在模拟 app 上的行为，AI 全黑盒 | 无 | `StateFixture` JSON |
| **高阶仿真** | **真实** `TextUnderstanding` + `MockModelProvider` + `MockModelFixture`（**传输级**声明式剧本） | 真实 prompt 组装 + 真实响应解析，AI 响应可重现/可对照 Python | 无 | `MockModelFixture` JSON + `StateFixture` JSON |
| **集成** | 真实 `DeepSeekModelProvider` + 真实 `HttpClient`（或 mock HTTP server） | HTTP 协议映射、错误 graceful、真实 token 计费 | 真（opt-in，无 `DEEPSEEK_API_KEY` 跳过） | — |
| **链路** | `IUniBrain` 整体注入 TraversalEngine（provider 真实或 mock 按需） | 端到端：引擎 → 子接口 → router → provider → 观测 | 依配置 | 依配置 |

**仿真 vs 高阶仿真的本质区别**：前者子接口是硬编码 mock（快、AI 黑盒），后者子接口是**真实实现** + 传输级 fixture mock（真实 prompt/解析逻辑被测到，AI 响应声明式可重现）。两级都用 `StateFixture` 驱动页面流转，只换 AI 接缝、不换剧本载体——这正是 `MockModelFixture` 平行于 `StateFixture` 的价值。

垂直切片交付的测试：**单元**（TextUnderstanding + decorator + router，用 MockModelProvider）+ **集成**（DeepSeekModelProvider，opt-in）。仿真/高阶仿真/链路在子接口实现齐备后补。

---

## 11. 组合示例（手工，无 DI）

```csharp
// —— 垂直切片典型组合（单元/高阶仿真，无网络）——
var fixture = MockModelFixture.FromJson(File.ReadAllText("fixtures/parse_instruction.mock.json"));
var mock    = new MockModelProvider(fixture);
var router  = new ModelRouter(
    capabilityRouting: ImmutableDictionary.CreateRange(new[]{ KeyValuePair.Create(ModelCapabilities.ParseInstruction, "mock") }),
    providers:         ImmutableDictionary.CreateRange(new[]{ KeyValuePair.Create("mock", (IModelProvider)mock) }),
    recorder:          new InMemoryTraceRecorder(),
    defaultProviderId: "mock");
var text = new TextUnderstanding(router, promptLibrary);

// —— 集成组合（真实 DeepSeek）——
var http = new HttpClient { BaseAddress = new Uri(dsCfg.BaseUrl) };
var deepseek = new DeepSeekModelProvider(http, dsCfg);
var router2 = new ModelRouter(
    capabilityRouting: { [ModelCapabilities.ParseInstruction] = "deepseek" },
    providers:         { ["deepseek"] = deepseek },
    recorder:          traceRecorder,
    defaultProviderId: "deepseek");
```

---

## 12. 决策记录（对照 Python 的偏离）

| # | 决策 | 理由 |
|---|---|---|
| 1 | C# 不让 `UniBrainService` 持有 providers / 做 routing（与 Python `UniBrain` 不同） | 保持 facade 纯组合；routing 下沉到 `ModelRouter`，职责单一 |
| 2 | 观测用 decorator 而非子接口内联（D-E11 字面是"子接口负责"） | 避免子接口重复观测代码；decorator 在"传输之上、子接口之下"，结构上更内聚；router 组装期套用保证不可绕过 |
| 3 | `MockModelFixture` 平行于 `StateFixture`，不扩展 `StateFixture` 成通用容器 | `StateFixture` 存页面元素数据（服务 `analyze_visual`），与传输级响应（`parse_instruction`）数据形状不同；扩展会违反单一职责 |
| 4 | `ModelRequest.Capability` 字段流经三层 | 让 mock 能按 capability 查 fixture、decorator 能记 capability、传输层可忽略——一字段统一三处需求 |
| 5 | 垂直切片不引入 DI / `IHttpClientFactory` / Polly | YAGNI；`HttpClient` 由调用方注入 `DeepSeekModelProvider`，重试用 decorator 预留位（本期空） |
| 6 | `ModelCapabilities` 定义全部 5 常量，垂直切片只用 `ParseInstruction` | 消灭魔术字符串 + 跨语言对照 + 为其余 capability 铺路；成本仅 5 行 |
| 7 | `DeepSeekModelProvider` 仅实现 `CompleteTextAsync`，Vision/Multimodal 留 `NotImplementedException` | DeepSeek text 优先；垂直切片只跑 `parse_instruction`（text 模式） |

---

## 13. 开放问题（转 writing-plans 前可澄清，不阻塞）

1. `TextUnderstanding` 遇 `ModelResponse.Success==false` 是抛 `DomainValidationException` 还是返回降级 `TextUnderstandingResult`？（§9 暂定抛）
2. `MockModelFixture` JSON 文件目录约定（`tests/fixtures/` 还是 `src/UniClaw.Core/Simulation/Fixtures/`）？
3. `ParseInstructionSchema` 是定义成强类型 `object`（JsonElement）还是 JSON 字符串常量？
4. 集成测试的 `DEEPSEEK_API_KEY` 注入方式（env / user-secrets / CI secret）？

---

## 14. 后续路径

本设计稿是 design doc，不在 OpenSpec change 流程内。落地建议：
- 基于 §5 类型清单 + §10 测试梯度，用 `/opsx:propose` 创建 OpenSpec change（或 writing-plans 拆实施计划）
- 实施顺序建议：`ModelCapabilities` + `ModelRequest.Capability` → `MockModelFixture`/`MockModelProvider` → `ObservingModelProvider` → `ModelRouter` → `TextUnderstanding` 实现 + 单测 → `DeepSeekModelProvider` + `DeepSeekProviderConfig` + 集成测试
- 验证抽象站住后，模式复制到其余 4 个 capability 与其余 provider（Anthropic/MiMo）
