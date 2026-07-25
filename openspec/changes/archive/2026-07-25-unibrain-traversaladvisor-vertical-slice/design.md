## Context

上一 change（`unibrain-modelprovider-vertical-slice`）跑通了 `parse_instruction` 端到端，确立了 UniBrain 的通用范式：

```
Request → IPromptLibrary.GetTemplate(capability)
        → PromptTemplate.Resolve(variables)
        → ModelRequest(Prompt, System, Schema, Capability)
        → IModelRouter.Resolve(capability)   [已套 ObservingModelProvider]
        → IModelProvider.CompleteTextAsync
        → ModelResponse.Content (JSON)
        → 反序列化为 Result record
```

`TextUnderstanding` 是该范式的首个实例，但只覆盖了最简形态（纯文本入、扁平 4 字段出）。`ITraversalAdvisor` 接口已存在（上一 change 清理了其子接口 stub，现仅剩裸接口），无任何实现类。

本切片要在**不新增任何基础设施**的前提下，把同一范式套到 `ITraversalAdvisor.DecideNextActionAsync` 上，验证它能否处理：
- 结构化状态入（`PageAnalysis` 复合类型 → 序列化进 prompt）
- 丰富决策出（`ContextDecisionResult` 7 字段，含 `Params` 字典）

约束（来自 CLAUDE.md / charter）：
- `DecisionResult` enum 3 值锁定（Success/Unsure/GiveUp）— 🔴火山级，**本 slice 仅引用、绝不新增**
- 所有 record `sealed record class` + `ImmutableArray/Dictionary`
- 所有校验 `DomainValidationException` fail-fast
- Domain.Vision ↔ Domain.Content 零直接 import（`PageAnalysis` 属 Content，本 slice 只 JSON 序列化它、不跨层引用类型）
- C# 查询先 MCP 后 Read

## Goals / Non-Goals

**Goals:**
- 新增 `TraversalAdvisor` sealed class，`DecideNextActionAsync` 真实 7 步链路，provider-agnostic（仅 `IModelRouter` + `IPromptLibrary`）
- 验证范式推广到「结构化入 → 丰富出」形态
- 解决 `ContextDecisionResult.Params`（`ImmutableDictionary<string,object>?`）的 S.T.J 反序列化陷阱
- 复用观测闭环：经 router 的调用必然产生 `AICallRecord`
- 单元 + 端到端测试全绿，无网络依赖

**Non-Goals:**
- `ITraversalAdvisor` 其余 3 方法（`InferContainerTypeAsync` / `HandleExceptionAsync` / `ScreenSafetyAsync`）的真实实现 — 另起 slice
- 生产组合根的 prompt 模板注册（本 slice 模板在测试侧 wiring）
- `ContextDecisionResult.Confidence` 构造期 0-1 校验 — 改类型契约，另起 change
- `Params` 嵌套 object/array 支持 — 本 slice 仅原始值
- `PageAnalysis` 的 prompt 内摘要/裁剪（token 优化）— 真机大页问题留待未来
- live HTTP 测试 — `DeepSeekModelProvider` 已由上一 change 覆盖

## Decisions

### D1: 切片边界 = 仅 `DecideNextActionAsync` 真实，其余 3 方法 `NotImplementedException`

**选择**：`TraversalAdvisor` 实现 `DecideNextActionAsync` 完整 7 步；`InferContainerTypeAsync` / `HandleExceptionAsync` / `ScreenSafetyAsync` 抛 `NotImplementedException("TraversalAdvisor slice covers decide_next_action only; <method> pending future slice.")`。

**理由**：一个 capability = 一条垂直切片，最大化复用已验证的 `TextUnderstanding` 结构。`DecideNextAction` 是遍历决策的规范方法（调用最频；走 text 路径，DeepSeek 可用，无需 Vision）。

**备选**：
- 同时实现 `HandleExceptionAsync`（同返 `ContextDecisionResult`，prompt 形状相近）— **拒**：它是异常恢复规划，语义属不同 capability，混入会模糊切片边界。
- 4 方法全实现 — **拒**：违反切片纪律，规模失控。

**为何 `NotImplementedException` 而非 `NotSupportedException`**：对齐项目既有 idiom（上一 change 的 Vision / `AnthropicModelProvider` stub 均用 NIE）；语义是「尚未」而非「永不」。

### D2: `PageAnalysis` → prompt 用 `DomainJsonOptions.Default` 序列化

**选择**：`var pageJson = JsonSerializer.Serialize(pageAnalysis, DomainJsonOptions.Default);`，注入模板变量 `{page_analysis}`。

**理由**：camelCase + enum-as-string 与模型可见的 schema 同构；一行完成，无需手写 flattener；信息完整（元素坐标、类型、文本全保留）。

**备选**：手写文本摘要（如只列元素描述）— **拒**：切片求简，完整 JSON 信息量更大且模型可处理；token 预算真机优化留待未来。

### D3: `Params` 反序列化 — DTO 用 `Dictionary<string, JsonElement>?` + ValueKind 映射

**陷阱**：`ContextDecisionResult.Params` 是 `ImmutableDictionary<string, object>?`。System.Text.Json 把 `object` 反序列化成 `JsonElement`，而 `JsonElement` 绑定底层 UTF-8 buffer，`JsonDocument` 释放后即失效——直接装箱进 `object` 会埋下 use-after-free 式的隐患。

**选择**：
- DTO 字段 `public Dictionary<string, JsonElement>? Params { get; init; }`
- 映射器按 `ValueKind` 转 CLR 原始值：`String → string`、`Number → double`、`True/False → bool`、其余 → `GetRawText()` 字符串
- 映射后构建 `ImmutableDictionary<string, object>?`（null 时保持 null）

**理由**：遍历决策的 params 是扁平原始值（如 `{"timeout": 5000}`）；ValueKind 映射轻量且规避 buffer 生命周期问题。

**备选**：用 `System.Text.Json.Nodes.JsonObject`（detached、安全）— 可行，但引入 `JsonNode` 依赖 + mutable→immutable 转换，对扁平场景偏重。

### D4: `DecisionResult` 映射是 parse，不是约束变更

**选择**：DTO.Result 为 `string`；映射器大小写不敏感 parse 为 `DecisionResult` enum；未识别字符串 → `DomainValidationException`（模型返回非法 enum）。

**理由**：仅消费 3 个锁定值（Success/Unsure/GiveUp），**不新增 enum 值**（火山约束守住）；非法值 fail-fast 暴露模型漂移。

### D5: `Confidence` 直通，不 clamp 不校验

**选择**：advisor 把模型返回的 `confidence` 原样塞进 `ContextDecisionResult`，不做 0-1 校验。

**理由**：`ContextDecisionResult.Confidence` 构造器现状无 0-1 校验（与 `TextUnderstandingResult` 不同）；advisor 作为 parser 应尊重既有类型契约。schema 的 0-1 是对模型的提示，非运行时保证。若要硬化，应改类型构造器（另起 change），而非在 advisor 里重复校验。

**备选**：advisor 校验 0-1 → `DomainValidationException` — **拒**：会复制类型未强制的校验，且在切片验证期可能掩盖模型真实输出。

### D6: 纯复用，不新增基础设施

不新增 router / decorator / capability 基础设施。`TraversalAdvisor` 结构与 `TextUnderstanding` 同构（`sealed class` + `IModelRouter` + `IPromptLibrary` ctor + 7 步 `DecideNextActionAsync`）。这正是切片目的：证明范式可推广。

### D7: 测试策略镜像 `TextUnderstanding`

`MockModelProvider`（无网络）+ fixture JSON 端到端 + `InMemoryTraceRecorder` 断言观测记录。无 live HTTP。

## Risks / Trade-offs

- **[Params 嵌套 object/array 不支持]** → schema 不宣传嵌套；Non-Goals 已声明；未来 slice 扩展 ValueKind 映射器。
- **[`PageAnalysis` JSON 过大撑爆模型上下文]** → 切片用 fixture 级 PageAnalysis；真机大页裁剪留待未来（Non-Goals）。
- **[模型返回非法 result enum]** → `DomainValidationException` fail-fast（正确行为，暴露漂移）。
- **[Confidence 越界静默通过]** → 接受（D5）；类型契约拥有校验权。
- **[其余 3 方法 NIE 在运行期被调用]** → 文案明确标注 pending；调用方（handler）目前不接 `ITraversalAdvisor` 真实实现，无实际触发路径。

## Migration Plan

无迁移。纯新增类 + 1 个 spec requirement ADDED；不改既有接口签名、不改既有 record 字段、不改既有 requirement。回滚 = 删除新增类与测试。

## Prompt Template (3.1 终稿)

`decide_next_action` 模板（capability = `ModelCapabilities.DecideNextAction`，变量 `{goal}` / `{page_analysis}` / `{current_node_id}` / `{depth}`）：

**SystemPrompt**：
> You are a mobile UI traversal decision advisor. Given a goal, the current page state (JSON), the current node id, and traversal depth, decide the single next action that best advances the goal. Respond ONLY with a JSON object: result (one of Success/Unsure/GiveUp), action (verb such as tap/scroll/input/back/wait), target (element id or null), params (flat object of primitive values, optional), reasoning (one sentence), confidence (0-1), safety_verified (boolean).

**UserPrompt**：
```
Goal: {goal}

Current page analysis (JSON):
{page_analysis}

Current node id: {current_node_id}
Traversal depth: {depth}

Decide the next action.
```

本 slice 在测试侧 wiring 此模板（`PromptLibrary` 构造期注册）；生产组合根统一注册留待 Open Question 1 的 wiring change。

## Open Questions

1. **生产 prompt 模板 registry 落点**：`decide_next_action` 模板最终在哪统一注册？本 slice 在测试侧 wiring；生产组合根 wiring 留待统一 change（与 `parse_instruction` 一并落地）。
2. **`ContextDecisionResult.Confidence` 是否应构造期校验 0-1**：本 slice 不改类型契约；若确认要校验，另起一个改类型的 change（影响所有 `ContextDecisionResult` 构造点）。
