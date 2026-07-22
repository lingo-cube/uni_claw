# UniBrain — Unified AI Service Layer PRD

> **状态**: 方案文档 (待审批)
> **日期**: 2026-07-22
> **作者**: Fran + Claude
> **来源**: `docs/refactor/2026-07-21-unibrain-concept-design.md` 分析 + 架构审查
> **范围**: 接口设计 + 架构关系 + 迁移路径 (不含具体实现细节)

---

## 0. TL;DR

| 结论 | 说明 |
|------|------|
| **IUniBrain 替换 IVisionProvider** | 引擎注入 IUniBrain (含 3 子接口)，不再注入 IVisionProvider |
| **Hybrid facade + ISP** | 对外统一 IUniBrain facade，内部 IPageAnalyzer / ITraversalAdvisor / ITextUnderstanding 各自独立 |
| **配置驱动组合，非品牌 monolith** | 无 ClaudeUniBrain；UniBrainService 是纯组合容器，子接口实现独立可替换，组合由配置/DI 决定 |
| **滚动感知脱离 AI** | IScreenStateProvider 独立接口 (Traversal namespace)，不在 IUniBrain 上 |
| **UniBrain 零 StateMachine 引用** | ITraversalAdvisor 方法只接收 Domain + BCL 类型，call site 从 ITraversalContext 提取值传入 |

---

## 1. 动机

当前 `IVisionProvider` 只覆盖视觉分析（截图 → PageAnalysis），但真实遍历需要 AI 做三件事：

| 能力 | 当前 | 目标 |
|------|------|------|
| **页面感知+验证** | IVisionProvider.AnalyzeCurrentPageAsync | 截图 → PageAnalysis + 页面类型验证 |
| **遍历决策** | IAIStrategyAdvisor (仅骨架，零消费者) | 容器推断、动作规划、异常恢复、安全评估 |
| **文本理解** | ❌ 无 | 文本分类、意图识别、OCR 后处理 |

三个能力共享同一组基础设施（模型调用、token 预算、重试、缓存），不应各建一套独立接口。**UniBrain** 是对这三个能力的统一抽象，底层通过 IModelProvider 可插拔后端实现。

---

## 2. 设计决策

### 2.1 接口形状: Hybrid facade + ISP

**决策**: 对外统一 `IUniBrain` facade (单一注入点)，内部 3 子接口各自独立 (ISP)。

**理由**: 消费者注入一个东西，但各能力可独立测试/替换/路由到不同 provider。纯统一接口 (3 方法都在 IUniBrain 上) 牺牲 ISP；纯独立接口 (3 个分别注入) 增加注入复杂度。Hybrid 兼顾两者。

### 2.2 子接口按职责分组，非按调用模式

**决策**: 子接口按 **职责语义** 分组，非按 AI 调用模式 (vision/text)。

| 子接口 | 职责 (回答的问题) | 包含方法 |
|--------|-------------------|---------|
| **IPageAnalyzer** | "当前屏幕是什么？是期望页面吗？" | AnalyzeCurrentPageAsync, FindAppEntryAsync, VerifyPageTypeAsync |
| **ITraversalAdvisor** | "遍历引擎该怎么做？" | InferContainerTypeAsync, DecideNextActionAsync, HandleExceptionAsync, ScreenSafetyAsync |
| **ITextUnderstanding** | "这段文本的含义是什么？" | UnderstandTextAsync |

**理由**: 旧 Vision/Text/Decision 分组本质是按 AI 调用模式 (需要截图 / 纯文本 / 需上下文)，导致：
- IVisionBrain 混了 4 种职责 (感知 + 入口 + 滚动 + 验证)
- IDecisionBrain 混了 5 种职责
- VerifyPageTypeAsync 在 Decision, VerifyPageWithVisionAsync 在 Vision — 同一逻辑能力分裂

按职责分组：每个接口单一职责，内聚性高。

### 2.3 IUniBrain 替换 IVisionProvider

**决策**: IUniBrain 替换 IVisionProvider 作为引擎 AI seam。TraversalEngine/StepContext 注入 IUniBrain 而非 IVisionProvider。

**理由**: 统一 AI 服务入口，避免引擎同时注入 IVisionProvider + IAIStrategyAdvisor 两个 AI 接口。Mode A/B 成为 IPageAnalyzer 实现选择 (ClaudePageAnalyzer / RuleBasedPageAnalyzer)，facade 无感。

### 2.4 滚动感知脱离 AI — IScreenStateProvider 独立

**决策**: 滚动感知方法 (HasScroll / GetScrollProgress / IsEndOfList / GetScrollSwipeConfig) 从 IVisionProvider 分离到独立 `IScreenStateProvider` 接口 (Traversal namespace)，不在 IUniBrain 上。

**理由**:
- 滚动是 **设备/平台状态查询**，不是 AI 判断
- Simulation mock 返回编程值，不走 AI
- Mode A: AI 在 PageAnalysis 中返回 has_scroll/is_end_of_list (对齐 Python PROMPT_STRUCTURE)
- Mode B: 规则引擎推导，不走 AI
- 强制放 "大脑" 接口是职责泄漏

### 2.5 配置驱动组合，非品牌 monolith

**决策**: 无 `ClaudeUniBrain` 品牌绑定类。`UniBrainService` 是纯组合容器 (sealed class)，子接口实现独立可替换，组合由配置/DI 决定。

**理由**: 高内聚低耦合 — 每个子接口实现只关心自己的能力 (ClaudePageAnalyzer 只管页面分析，不管决策)。品牌绑定在具体实现内部，不在 facade 上。配置灵活组合: Claude(vision) + DeepSeek(decision) + local(text) 等。

### 2.6 UniBrain 零 StateMachine 引用

**决策**: ITraversalAdvisor 方法只接收 **Domain 类型 + BCL 类型**，不引用 `ITraversalContext` (StateMachine 接口)。

**理由**: 避免 UniBrain ↔ StateMachine 双向依赖。ITraversalContext 是 StateMachine 接口，如果 UniBrain 引用它，形成循环：StateMachine→UniBrain (注入) + UniBrain→StateMachine (参数)。call site (Handler/StepOrchestrator) 从 ITraversalContext 提取 string/int 值直接传入，类型安全且解耦。

### 2.7 VerifyPageWithVisionAsync 不在 Core 接口

**决策**: `VerifyPageWithVisionAsync` (Python: verify_page_with_vision, 接收截图 bytes) 不在 Core `IPageAnalyzer` 接口上。它是 Host 层便利方法。

**理由**: YAGNI — 引擎零消费者。Core 接口只放遍历链路必需方法。`VerifyPageTypeAsync(PageAnalysis, string)` 在 IPageAnalyzer 上，已覆盖元数据版本验证。视觉版本 (需截图) 由 Host 项目通过扩展方法或独立服务提供。

---

## 3. 接口定义

### 3.1 IUniBrain — facade

```csharp
namespace UniClaw.Core.UniBrain;

/// <summary>
/// UniBrain — 统一 AI 服务 facade。
/// 引擎和 Handler 注入此接口，通过子接口访问各能力。
/// </summary>
public interface IUniBrain
{
    IPageAnalyzer PageAnalyzer { get; }
    ITraversalAdvisor Advisor { get; }
    ITextUnderstanding Text { get; }
}
```

### 3.2 UniBrainService — 组合容器

```csharp
/// <summary>
/// UniBrainService — 纯组合容器。
/// 不做路由、不持有 IModelProvider、不持有配置。
/// 子接口实现通过构造器注入，组合由配置/DI 决定。
/// </summary>
public sealed class UniBrainService : IUniBrain
{
    public IPageAnalyzer PageAnalyzer { get; }
    public ITraversalAdvisor Advisor { get; }
    public ITextUnderstanding Text { get; }

    public UniBrainService(
        IPageAnalyzer pageAnalyzer,
        ITraversalAdvisor advisor,
        ITextUnderstanding text)
    {
        PageAnalyzer = pageAnalyzer;
        Advisor = advisor;
        Text = text;
    }
}
```

### 3.3 IPageAnalyzer — 页面感知+验证

```csharp
/// <summary>
/// IPageAnalyzer — 页面感知+验证能力。
/// 单一职责: "当前屏幕是什么？是期望页面吗？"
/// 替换: IVisionProvider (页面分析 + 入口查找部分)
/// </summary>
public interface IPageAnalyzer
{
    /// <summary>分析当前页面截图 → PageAnalysis</summary>
    Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default);

    /// <summary>在启动器中查找目标 app 的图标坐标</summary>
    Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default);

    /// <summary>验证当前页面是否匹配期望类型 (元数据版本, 非 vision)</summary>
    Task<PageTypeVerification> VerifyPageTypeAsync(
        PageAnalysis pageAnalysis,
        string expectedType,
        string? expectedPageName = null,
        CancellationToken ct = default);
}
```

### 3.4 ITraversalAdvisor — 遍历决策

```csharp
/// <summary>
/// ITraversalAdvisor — 遍历决策能力。
/// 单一职责: "遍历引擎该怎么做？"
/// 替换: IAIStrategyAdvisor
/// 关键: 方法参数只用 Domain+BCL 类型，不引用 ITraversalContext (解耦)
/// </summary>
public interface ITraversalAdvisor
{
    /// <summary>推断页面容器类型</summary>
    Task<ContainerInference> InferContainerTypeAsync(
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        CancellationToken ct = default);

    /// <summary>决策下一步操作</summary>
    Task<ContextDecisionResult> DecideNextActionAsync(
        string goal,
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        int? depth = null,
        CancellationToken ct = default);

    /// <summary>处理异常 — 恢复规划</summary>
    Task<ContextDecisionResult> HandleExceptionAsync(
        Exception exception,
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        CancellationToken ct = default);

    /// <summary>安全筛选</summary>
    Task<SafetyScreeningResult> ScreenSafetyAsync(
        PageAnalysis pageAnalysis,
        string instruction,
        string? pageType = null,
        CancellationToken ct = default);
}
```

### 3.5 ITextUnderstanding — 文本理解

```csharp
/// <summary>
/// ITextUnderstanding — 文本理解能力。
/// 单一职责: "这段文本/指令的含义是什么？"
/// 对齐 Python: parse_instruction capability
/// </summary>
public interface ITextUnderstanding
{
    Task<TextUnderstandingResult> UnderstandTextAsync(
        TextUnderstandingRequest request,
        CancellationToken ct = default);
}

public sealed record class TextUnderstandingRequest(
    string Text,
    string? Context = null);

public sealed record class TextUnderstandingResult(
    string Category,
    double Confidence,
    ImmutableArray<string> Entities,
    string? Summary = null);
```

### 3.6 IModelProvider — 抽象后端

```csharp
/// <summary>
/// IModelProvider — AI 模型调用抽象。
/// 对齐 Python AIProvider: complete_text / complete_vision / complete_multimodal
/// 负责: 调用重试、token 预算、超时、观测记录 (→ ITraceRecorder)
/// 消费者: 子接口实现内部注入，不穿过 IUniBrain
/// </summary>
public interface IModelProvider
{
    string ProviderId { get; }

    Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default);
    Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default);
    Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default);
}

public sealed record class ModelRequest(
    string Prompt,
    string? SystemPrompt = null,
    object? Schema = null,
    int MaxTokens = 4096);

// 对齐 Python AIResponse 字段
public sealed record class ModelResponse(
    string Content,
    string ProviderId,
    string Mode,
    int InputTokens,
    int OutputTokens,
    double LatencyMs,
    string Model = "",
    bool Success = true,
    string? ErrorMessage = null);
```

### 3.7 IScreenStateProvider — 滚动感知 (独立, 非 AI)

```csharp
namespace UniClaw.Core.Traversal;

/// <summary>
/// IScreenStateProvider — 滚动+设备状态查询。
/// 从 IVisionProvider 分离 — 滚动是设备状态, 不是 AI 判断。
/// Traversal namespace (与 ScrollSwipeConfig 同层)。
/// </summary>
public interface IScreenStateProvider
{
    bool HasScroll();
    double GetScrollProgress();
    bool IsEndOfList();
    ScrollSwipeConfig? GetScrollSwipeConfig();
}
```

### 3.8 UniBrainConfig — 组合配置

```csharp
namespace UniClaw.Core.UniBrain;

/// <summary>
/// UniBrainConfig — 配置驱动组合。
/// 定义: 哪个子接口实现 → 哪个 IModelProvider
/// 对齐 Python: ai_providers.yaml routing config
/// </summary>
public sealed record class UniBrainConfig(
    string DefaultProvider = "deepseek",
    ImmutableDictionary<string, string>? CapabilityRouting = null,
    bool EnableTrace = true);
```

---

## 4. 已迁移类型 — Python↔C# 字段对齐

### 4.1 ContextDecisionResult (对齐 Python ai_types.ContextDecisionResult)

```csharp
// Python: result, action, target, params, reasoning, confidence, safety_verified
// 旧 C#: Result, Action, Target(object?), Confidence
// 新 C#: 全字段对齐
public sealed record class ContextDecisionResult(
    DecisionResult Result,
    string? Action = null,
    string? Target = null,                          // ← string? (非 object?)
    ImmutableDictionary<string, object>? Params = null,  // ← 新增, 对齐 Python params
    string? Reasoning = null,                       // ← 新增, 对齐 Python reasoning
    double Confidence = 0.0,
    bool SafetyVerified = true);                    // ← 新增, 对齐 Python safety_verified
```

### 4.2 MismatchDetails (对齐 Python)

```csharp
// Python: missing_items, unexpected_items, type_conflict
// 旧 C#: ExpectedType, ActualElements, MissingElements
// 新 C#: 对齐 Python 字段名
public sealed record class MismatchDetails(
    ImmutableArray<string> MissingItems,         // ← 对齐 Python missing_items
    ImmutableArray<string> UnexpectedItems,      // ← 对齐 Python unexpected_items
    string? TypeConflict = null);                // ← 对齐 Python type_conflict
```

### 4.3 Suggestion (对齐 Python)

```csharp
// Python: action, target, reason
// 旧 C#: Action, Target(object?), Reason
// 新 C#: Target → string?
public sealed record class Suggestion(
    string Action,
    string? Target = null,   // ← string? (非 object?)
    string? Reason = null);
```

### 4.4 其他类型 — 已对齐，无需修改

| 类型 | 对齐状态 |
|------|---------|
| ContainerInference | ✅ 已对齐 (container_type, confidence, matched_template) |
| PageTypeVerification | ✅ 已对齐 (is_match, confidence, actual_type, reasoning) |
| SafetyScreeningResult | ✅ 已对齐 (evaluations, page_level_guidance) |
| SafetyEvaluation | ✅ 已对齐 (name, safety_tag, confidence, reason) |
| PageLevelGuidance | ✅ 已对齐 (overall_safe_to_proceed, recommended_max_parallel) |
| DecisionResult | ✅ 已对齐 (Success, Unsure, GiveUp) |
| SafetyTag | ✅ 已对齐 (Safe, Caution, Skip, Unknown) |

---

## 5. 架构关系

### 5.1 依赖方向图

```
Domain (底层, 零向上 — C-4)
  ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑
UniBrain (新增层)
  依赖: Domain.Content (PageAnalysis) + Domain.Common (Target, Operation)
  不依赖: StateMachine ← 关键！(call site 解耦)
  ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑ ↑
Graph
  依赖: Domain only (C-5 单向)
  ↑
StateMachine
  依赖: Graph · Domain · Observability (D-17) · Traversal (D-14 acknowledged)
         UniBrain (IUniBrain 注入 — 新增向上引用, acknowledged)
  ↑
Traversal
  依赖: StateMachine · Domain · Graph · Observability · UniBrain (IUniBrain + IScreenStateProvider)
  ↑
Observability (cross-cutting — D-17)
  依赖: Domain.Common only
  ↑
Simulation (test infra)
  依赖: StateMachine · Domain · UniBrain · Graph · Traversal
  Guard: EngineLayers_DoNotReferenceSimulation (D-73)

Host Projects (Core 外):
  UniClaw.ClaudeProvider/    ← Claude 子接口实现 + AnthropicModelProvider
  UniClaw.DeepSeekProvider/  ← DeepSeek 子接口实现 + DeepSeekModelProvider
  UniClaw.Device/            ← ADB (IScreenCapture + IScreenStateProvider + IActionExecutor)
  互不引用, 都只依赖 Core 接口, 在 app root 装配
```

### 5.2 层级归属表

| 类型 | 命名空间 | 依赖方向 |
|------|---------|---------|
| IUniBrain, UniBrainService | `UniClaw.Core.UniBrain` | → Domain |
| IPageAnalyzer | `UniClaw.Core.UniBrain` | → Domain.Content |
| ITraversalAdvisor | `UniClaw.Core.UniBrain` | → Domain.Content, Domain.Common (零 StateMachine!) |
| ITextUnderstanding | `UniClaw.Core.UniBrain` | → 无或 Domain |
| IModelProvider | `UniClaw.Core.UniBrain` | → 无 (纯抽象) |
| UniBrainConfig | `UniClaw.Core.UniBrain` | → 无 |
| ContextDecisionResult, ContainerInference 等 | `UniClaw.Core.UniBrain` | → Domain.Common 或无 |
| IScreenStateProvider | `UniClaw.Core.Traversal` | → Domain, Traversal (ScrollSwipeConfig) |
| TextUnderstandingRequest/Result | `UniClaw.Core.UniBrain` | → 无 |
| ModelRequest/Response | `UniClaw.Core.UniBrain` | → 无 |

### 5.3 Call site 解耦模式

Handler/StepOrchestrator 从 ITraversalContext 提取值，直接传入 ITraversalAdvisor 方法：

```csharp
// TraversalFSM.HandleBranchAsync — call site
var result = await ctx.Brain.Advisor.DecideNextActionAsync(
    goal: currentGoal,
    pageAnalysis: currentPage,
    currentNodeId: ctx.Context.CurrentFrame?.NodeId,
    depth: ctx.Context.StepCount);
```

**效果**: UniBrain namespace 只看到 string/int/PageAnalysis — 不知道 ITraversalContext 存在。双向依赖彻底消除。

### 5.4 子接口实现内部组合

每个子接口实现是独立 sealed class，自己注入需要的 IModelProvider：

```csharp
// ClaudePageAnalyzer — 只关心页面分析
public sealed class ClaudePageAnalyzer : IPageAnalyzer
{
    private readonly IModelProvider _modelProvider;    // AnthropicModelProvider (注入)
    private readonly IScreenCapture _screenCapture;    // AdbScreenCapture (注入)
    private readonly ITraceRecorder? _traceRecorder;

    public async Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct)
    {
        var bytes = await _screenCapture.CaptureAsync(ct);
        var request = new ModelRequest(Prompt: PROMPT_STRUCTURE, Schema: PageAnalysisSchema);
        var response = await _modelProvider.CompleteVisionAsync(request, bytes, ct);
        _traceRecorder?.RecordAICallAsync(new AICallRecord(
            Capability: "page_analysis", ProviderId: _modelProvider.ProviderId, ...));
        return JsonSerializer.Deserialize<PageAnalysis>(response.Content, DomainJsonOptions);
    }
}
```

**IModelProvider 不穿过 UniBrainService** — 每个子接口实现自己注入。UniBrainService 只看到子接口，不知道底层用了什么 model provider。

### 5.5 配置驱动的组合

Composition root (app startup) 根据 UniBrainConfig 决定哪些实现组合：

```csharp
public static IUniBrain CreateUniBrain(UniBrainConfig config, IScreenCapture screenCapture, ITraceRecorder? traceRecorder)
{
    // 1. 创建 IModelProvider 实例
    var providers = CreateProviders(config);

    // 2. 创建子接口实现 (配置决定品牌)
    var pageAnalyzer = CreatePageAnalyzer(config.PageAnalyzerProvider, providers, screenCapture, traceRecorder);
    var advisor = CreateTraversalAdvisor(config.TraversalAdvisorProvider, providers, traceRecorder);
    var text = CreateTextUnderstanding(config.TextUnderstandingProvider, providers, traceRecorder);

    // 3. 组合
    return new UniBrainService(pageAnalyzer, advisor, text);
}
```

灵活组合示例:

| 场景 | PageAnalyzer | TraversalAdvisor | TextUnderstanding |
|------|-------------|-----------------|-------------------|
| 生产 (Claude+DeepSeek) | ClaudePageAnalyzer | DeepSeekTraversalAdvisor | DeepSeekTextUnderstanding |
| 全 Claude | ClaudePageAnalyzer | ClaudeTraversalAdvisor | ClaudeTextUnderstanding |
| Simulation (mock) | MockPageAnalyzer | MockTraversalAdvisor | MockTextUnderstanding |
| Mode B (规则) | RuleBasedPageAnalyzer | RuleBasedTraversalAdvisor | MockTextUnderstanding |

---

## 6. 与现有组件的替换映射

| 旧组件 | 旧位置 | → 新组件 | 新位置 | 变化 |
|--------|--------|----------|--------|------|
| `IVisionProvider` (5 方法) | StateMachine/StepContext.cs | `IPageAnalyzer` (3 方法) | UniBrain/ | 页面分析+入口，类型重命名 |
| IVisionProvider 4 滚动方法 | StateMachine/StepContext.cs | `IScreenStateProvider` | Traversal/ | 分离到独立接口 |
| `IAIStrategyAdvisor` | AI/IAIStrategyAdvisor.cs | `ITraversalAdvisor` | UniBrain/ | 4 方法, 参数改为 Domain+BCL |
| IAIStrategyAdvisor 相关类型 | AI/ | 同名类型 | UniBrain/ | 迁入, 字段对齐 Python |
| `StatefulMockVisionService` | Simulation/ | `MockPageAnalyzer` | Simulation/ | 拆: 页面分析部分 |
| `ScrollableMockVisionService` | Simulation/Scroll/ | `MockPageAnalyzer` + `MockScreenStateProvider` | Simulation/ | 拆: 页面分析 + 滚动状态 |
| `StepContext.Vision` | StateMachine/StepContext.cs | `StepContext.Brain` + `StepContext.ScreenState` | StateMachine/ | 两个注入点 |
| `AI/` 目录整体 | — | 删除, 迁入 `UniBrain/` | — | 旧 namespace 清空 |

### StepContext 改动

```csharp
// 旧
IVisionProvider Vision,

// 新
IUniBrain Brain,                     // ← 替换 Vision
IScreenStateProvider ScreenState,    // ← 滚动独立
```

**消费代码迁移**:

| 旧调用 | 新调用 |
|--------|--------|
| `ctx.Vision.AnalyzeCurrentPageAsync()` | `ctx.Brain.PageAnalyzer.AnalyzeCurrentPageAsync()` |
| `ctx.Vision.HasScroll()` | `ctx.ScreenState.HasScroll()` |
| `ctx.Vision.GetScrollProgress()` | `ctx.ScreenState.GetScrollProgress()` |
| `ctx.Vision.IsEndOfList()` | `ctx.ScreenState.IsEndOfList()` |
| `ctx.Vision.FindAppEntryAsync(app)` | `ctx.Brain.PageAnalyzer.FindAppEntryAsync(app)` |

---

## 7. Observability 集成

IModelProvider 实现内部调 `ITraceRecorder.RecordAICallAsync`，对齐现有 TraceCoordinator 模式。

AICallRecord.Capability 值域 (对齐 UniBrain capability 名):

| AICallRecord.Capability | 来源 |
|------------------------|------|
| "page_analysis" | IPageAnalyzer.AnalyzeCurrentPageAsync |
| "find_app_entry" | IPageAnalyzer.FindAppEntryAsync |
| "page_type_verify" | IPageAnalyzer.VerifyPageTypeAsync |
| "container_inference" | ITraversalAdvisor.InferContainerTypeAsync |
| "next_action" | ITraversalAdvisor.DecideNextActionAsync |
| "exception_recovery" | ITraversalAdvisor.HandleExceptionAsync |
| "safety_screening" | ITraversalAdvisor.ScreenSafetyAsync |
| "text_understanding" | ITextUnderstanding.UnderstandTextAsync |

SpanType 值数锁定 = 11 (D-E8)。新增 capability 不新增 SpanType 值 — 用 AICallRecord.Capability 字符串区分，SpanType 只区分大类 (PageAnalysis / AICall / StateDecision)。

---

## 8. Host 项目归属

```
src/UniClaw.Core/UniBrain/       ← Core: 接口 + UniBrainService + 类型定义
src/UniClaw.ClaudeProvider/      ← Host: Claude 实现 (Anthropic SDK)
                                    ClaudePageAnalyzer, ClaudeTraversalAdvisor
                                    ClaudeTextUnderstanding, AnthropicModelProvider
                                    PROMPT_STRUCTURE 移植
src/UniClaw.DeepSeekProvider/    ← Host: DeepSeek 实现 (DeepSeek SDK)
                                    DeepSeekTraversalAdvisor, DeepSeekTextUnderstanding
                                    DeepSeekModelProvider
src/UniClaw.Device/              ← Host: ADB 后端
                                    AdbScreenCapture, AdbScreenStateProvider
                                    AdbActionExecutor
src/UniClaw.LocalProvider/       ← Host: 本地模型 (Ollama/vLLM) — Phase 3+
```

每个 Host 项目只实现自己品牌的子接口 + IModelProvider。互不引用。Core 之外的组合在 app root 完成。

---

## 9. Mock 策略 (Simulation 层)

Simulation 层 mock 不需要单独的 MockUniBrain 类 — 直接用 UniBrainService + mock 子接口:

```csharp
// 基线测试组合 mock — 对齐现有 StatefulMockVisionService 行为
var mockBrain = new UniBrainService(
    new MockPageAnalyzer(fixture),      // ← 对齐 StatefulMockVisionService
    new MockTraversalAdvisor(),         // ← 返回 GiveUp 或固定 ContextDecisionResult
    new MockTextUnderstanding());       // ← 返回固定 TextUnderstandingResult
```

| Mock 类型 | 实现 |
|-----------|------|
| MockPageAnalyzer | StateFixture → PageAnalysis (对齐现有 StatefulMockVisionService 构造逻辑) |
| MockTraversalAdvisor | 返回 ContextDecisionResult(GiveUp) 或固定决策 |
| MockTextUnderstanding | 返回固定 TextUnderstandingResult |
| MockScreenStateProvider | 返回编程值 (对齐 ScrollableMockVisionService 滚动方法) |
| MockModelProvider | 返回固定 JSON (Host 实现单元测试用) |

**现有基线测试迁移**: StatefulMockVisionService → MockPageAnalyzer。行为不变 (fixture → PageAnalysis 构造逻辑不变)，只是类型名从 IVisionProvider → IPageAnalyzer。MockScreenStateProvider 接管滚动方法。

---

## 10. Constitution 约束影响

| 约束 | 影响 | 是否违反？ |
|------|------|----------|
| **C-4 Domain 零向上** | UniBrain 引用 Domain.Content — OK, UniBrain 在 Domain 上方 | ❌ 不违反 |
| **C-5 Graph→StateMachine 单向** | 不涉及 | ❌ 不违反 |
| **C-9 sealed record class** | 新类型用 sealed record class; UniBrainService 用 sealed class (服务容器, 同 TraversalRuntimeContext 例外) | ❌ 不违反 |
| **C-10 DomainValidationException** | 新类型校验用 DomainValidationException (TextUnderstandingResult.Confidence 0-1 等) | ❌ 不违反 |
| **D-17 Observability cross-cutting** | IModelProvider 内部调 ITraceRecorder (Host 层, 非 Core→Core 循环) | ❌ 不违反 |
| **D-73 Engine 不引用 Simulation** | Engine 只引用 Core.UniBrain interfaces, mock 在 Simulation | ❌ 不违反 |
| **新增向上引用** | StateMachine→UniBrain, Traversal→UniBrain | ⚠️ acknowledged, 同 D-14/D-17 |
| **UniBrain→StateMachine** | 零引用 (DecisionContext 删除, 方法用 Domain+BCL) | ✅ 无双向依赖 |

### 需新增 ArchitectureGuard 测试

| Guard | 验证内容 |
|-------|---------|
| `UniBrain_DoesNotReferenceStateMachine` | UniBrain namespace 不引用 StateMachine namespace |
| `UniBrain_DoesNotReferenceTraversal` | UniBrain namespace 不引用 Traversal namespace |
| `IUniBrain_Has3SubInterfaces` | IUniBrain 有 PageAnalyzer + Advisor + Text 属性 |
| `IScreenStateProvider_Has4Methods` | 4 方法锁定 (HasScroll, GetScrollProgress, IsEndOfList, GetScrollSwipeConfig) |
| `StateMachine_ReferencesUniBrainForIUniBrain` | acknowledged 向上引用 |
| `Traversal_ReferencesUniBrainForIUniBrain` | acknowledged 向上引用 |

---

## 11. Python UniBrain↔C# UniBrain 对齐

| Python | C# | 对齐程度 |
|--------|-----|---------|
| 5 capability routing (YAML) | UniBrainConfig.CapabilityRouting | ✅ 结构对齐 |
| `_execute_capability(capability, mode, ...)` | 内部: 子接口实现→IModelProvider | ✅ 语义对齐, C# 类型安全 |
| `AIStrategyAdvisor` ABC → UniBrain inherits | `ITraversalAdvisor` interface → UniBrainService composes | ✅ 对齐 (组合 vs 继承) |
| `AIProvider` base class | `IModelProvider` interface | ✅ 对齐 |
| `AIResponse` dataclass | `ModelResponse` sealed record class | ✅ 字段对齐 |
| `AIProviderConfig` dataclass | UniBrainConfig + per-provider config | ✅ 对齐 |
| `TraceIntegration(SpanContext)` | ITraceRecorder.RecordAICallAsync | ✅ 对齐 (C# 已有 Observability) |
| `analyze_screenshot(image_data)` | `AnalyzeCurrentPageAsync()` | ⚠️ 截图参数差异 (§12-B: Host 内部组合 IScreenCapture) |
| 同步方法 | async 方法 | ✅ C# 全链路 async (D-76) |
| `PromptManager` | 未定 (YAGNI — 子接口实现内联 prompt, Phase 3+) | ⚠️ defer |

---

## 12. 目录结构

```
src/UniClaw.Core/UniBrain/
  IUniBrain.cs
  UniBrainService.cs
  IPageAnalyzer.cs
  ITraversalAdvisor.cs
  ITextUnderstanding.cs
  IModelProvider.cs
  UniBrainConfig.cs
  TextUnderstandingRequest.cs + TextUnderstandingResult.cs
  ModelRequest.cs + ModelResponse.cs
  ContextDecisionResult.cs + DecisionResult.cs
  ContainerInference.cs
  PageTypeVerification.cs + MismatchDetails.cs + Suggestion.cs
  SafetyScreeningResult.cs + SafetyTag.cs + SafetyEvaluation.cs + PageLevelGuidance.cs
  AppEntryPoint.cs   ← 从 StateMachine/StepContext.cs 迁入 (IPageAnalyzer 返回类型)

src/UniClaw.Core/Traversal/
  IScreenStateProvider.cs   ← 新增 (滚动感知独立接口)

src/UniClaw.Core/AI/        ← 删除整个目录, 内容迁入 UniBrain/
```

---

## 13. 非目标 (此 PRD 不涉及)

- 具体模型选型 (GPT-4o vs Claude vs DeepSeek vs 其他)
- Token 计费和预算策略
- Prompt 工程模板 (子接口实现内联 prompt, PromptManager YAGNI)
- 多模态模型的具体 schema 设计
- 平台适配层 (Android ADB 截图 → IUniBrain) — UniClaw.Device 项目职责
- IModelProvider.SupportedModes (防误配 guard — YAGNI, defer Phase 3)
- VerifyPageWithVisionAsync 实现 (Host 层便利方法, 不在 Core 接口)
- 本地模型 (Ollama/vLLM) 支持 — Phase 3+

---

## 14. 审查缺陷与优化 (已修复/确认)

| # | 类型 | 内容 | 状态 |
|---|------|------|------|
| 1 | 🔴缺陷 | ITraversalAdvisor 引用 ITraversalContext → 双向依赖 | ✅ 修复: 方法改为 Domain+BCL 参数 |
| 2 | 🔴缺陷 | IScreenStateProvider 在错误 namespace | ✅ 修复: 移到 Traversal namespace |
| 3 | 🔴缺陷 | ContextDecisionResult 字段未对齐 Python | ✅ 修复: +Params +Reasoning +SafetyVerified, Target→string? |
| 4 | 🟡优化 | 删除 DecisionContext | ✅ 确认: 方法用具体参数, 不用 catch-all record |
| 5 | 🟡优化 | VerifyPageWithVisionAsync 从 Core 接口移除 | ✅ 确认: Host 层便利方法, YAGNI |
| 6 | 🟡优化 | IModelProvider.SupportedModes | ✅ defer: YAGNI, 误配时抛 NotSupportedException |
| 7 | 🟡优化 | 文件组织保持扁平 | ✅ 确认: 先不分子目录 |
| 8 | 🟡优化 | UniBrainService 用 sealed class (非 record) | ✅ 确认: 服务容器, 非数据 |
| 9 | 🟡优化 | 新增 6 个 ArchitectureGuard tests | ✅ 必须 |
| 10 | 🟡优化 | MockPageAnalyzer.VerifyPageTypeAsync 简单实现 | ✅ 确认: mock 不需精确 |

---

## 15. 下一步

1. 用户审批此 PRD
2. 写入 implementation plan (writing-plans skill)
3. Phase 3-A 实施顺序:
   - (1) 新建 UniBrain/ 目录 + 接口定义 + 类型迁入
   - (2) IScreenStateProvider 分离
   - (3) StepContext 改: IVisionProvider → IUniBrain + IScreenStateProvider
   - (4) 新增 ArchitectureGuard tests
   - (5) Mock 组合迁移 (Simulation 层)
   - (6) 引擎消费代码迁移 (Traversal + StateMachine call sites)
   - (7) 删除旧 AI/ 目录 + IVisionProvider
   - (8) Host 项目骨架 (UniClaw.ClaudeProvider, UniClaw.DeepSeekProvider) — 需外部依赖 E-1/E-2 解锁
