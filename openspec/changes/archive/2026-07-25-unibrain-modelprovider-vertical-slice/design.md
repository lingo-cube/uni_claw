## Context

UniBrain 抽象层已齐备但实现层全空（详见 proposal Why 与上游设计稿 `docs/refactor/2026-07-25-unibrain-modelprovider-vertical-slice-design.md`）。本 design 覆盖**垂直切片**（`parse_instruction` 端到端链）的技术决策；详细类型清单、数据流图、测试梯度见设计稿 §5/§6/§10，本文件不重复，只记**决策理由、风险、与 spec 契约的映射、researcher 偏差的修正**。

**关键现状（researcher 已逐项验证）**：
- `IModelProvider.cs:7` 注释 + `model-provider` spec L11 双重锁定「IModelProvider 不带 recorder / 子接口负责观测」（D-E11）。本 change 翻转此契约（见 D2）。
- `DeepSeekModelProvider` / `AnthropicModelProvider`（注：位于 `src/UniClaw.ClaudeProvider/`，非 AnthropicProvider）是 stub；provider 项目里另有 `DeepSeekTextUnderstanding` / `ClaudeTextUnderstanding` stub（provider-specific 子接口实现，throw NIE）—— Python 风格旧架构残留，与 router 架构互斥（见 D8）。
- 只有 `MockPageAnalyzer` 消费 `StateFixture`；`MockTextUnderstanding` / `MockTraversalAdvisor` 是无 fixture 硬编码 mock。
- `StateFixture` 现状**不带 DomainValidationException 校验**（构造器空、FromJson 用 InvalidOperationException）—— `MockModelFixture` 的校验是**纯新增**，非复用（见 D3）。
- `AICallRecord` 实际 8 字段（设计稿 §8 表格漏列 `Context` envelope 与 `Timestamp`）；`InMemoryTraceRecorder` 构造器需 `ITraceStorage`（设计稿 §11 无参示例无法编译）—— tasks 阶段修正。
- `UniBrainGuardTests`（行 766-910）不验 UniBrain→Observability 方向（与 D-17 一致），引入 `ObservingModelProvider` 不触发 guard。
- `PromptTemplate.Resolve` 签名是 `IReadOnlyDictionary<string,string>`（设计稿 §5.1 写 `Dictionary` 是合法调用，签名描述不严谨）。

## Goals / Non-Goals

**Goals**
1. 跑通 `TextUnderstanding.UnderstandTextAsync` 端到端链。
2. 实现 `DeepSeekModelProvider`（OpenAI-compatible HTTP）+ `DeepSeekProviderConfig`。
3. 实现 `MockModelProvider` + `MockModelFixture`（传输级声明式 mock）。
4. 实现 `ObservingModelProvider`（decorator，唯一观测挂钩点）。
5. 实现 `IModelRouter` + `ModelRouter`（capability 路由 + 组装期套 decorator）。
6. 定义 `ModelCapabilities` 5 常量；`ModelRequest` 加 `Capability` 字段。
7. 接通观测闭环：所有经 router 的 AI 调用产生 `AICallRecord`。

**Non-Goals**
- 其余 4 capability（`IPageAnalyzer` / `ITraversalAdvisor`）真实实现；`AnthropicModelProvider` 填实；DI / `IHttpClientFactory` / Polly；yaml 配置；`UniBrainConfig.CapabilityRouting` 运行时消费；重试 / 缓存 decorator；**provider 项目旧 stub 清理**（D8）。

## Decisions

**D1. `UniBrainService` 不持有 providers / routing（与 Python 不同）**
下沉 routing 到 `ModelRouter`，保持 facade 纯组合。*Alternative*: 让 UniBrainService 自持 providers dict（Python 风格）—— 否决，因与已锁定的 `unibrain-facade` spec（UniBrainService 不做 routing、不持 IModelProvider）冲突，且 facade 变胖难测。

**D2. 观测用 decorator（`ObservingModelProvider`）而非子接口内联 —— 翻转 spec R1**
这是 D-E11 的解法。现有 `model-provider` spec 字面是「子接口实现负责观测」，但子接口内联会重复 4 处（每 capability 一份），且无法保证 `MockModelProvider` 也被观测。decorator 在「传输之上、子接口之下」，router 组装期套用 → **结构上不可绕过**。本决策**翻转 model-provider spec R1**，但 `IModelProvider` 接口签名不变（仅新增一个实现 IModelProvider 的 decorator 类）。*Alternative A*: 子接口内联（spec 字面）→ 否决，重复代码 + Mock 绕过。*Alternative B*: 传输 provider 基类带 recorder → 否决，`MockModelProvider` 也得继承，污染纯传输职责。

**D3. `MockModelFixture` 平行于 `StateFixture`，不扩展 `StateFixture`**
`StateFixture` 存页面元素数据（服务 `analyze_visual`），与传输级响应（`parse_instruction`）数据形状不同；扩展违反 SRP。校验是**新增**（`StateFixture` 现状不校验），`MockModelFixture` 用 `DomainValidationException` 对齐项目 fail-fast 风格。*Alternative*: `StateFixture` 扩成通用 fixture 容器 → 否决，形状不同 + SRP 违反。

**D4. `ModelRequest.Capability` 字段流经三层**
一字段统一三处需求（mock 按 capability 查 fixture / decorator 记 capability / 传输层可忽略）。可选字段，向后兼容。*Alternative*: router 用独立 capability 参数 → 否决，decorator 拿不到 capability（除非改 `IModelProvider` 签名，破坏面更大）。

**D5. 垂直切片不引入 DI / `IHttpClientFactory` / Polly**
YAGNI。`HttpClient` 由调用方注入 `DeepSeekModelProvider`。重试/缓存用 decorator 预留位（本期空）。*Alternative*: 直接上 DI 容器 → 否决，切片目标是验证抽象、非搭基础设施。

**D6. `ModelCapabilities` 定义全部 5 常量，切片只用 `ParseInstruction`**
消灭魔术字符串 + 跨语言对照 + 为其余 capability 铺路；成本 5 行。排除 Python 的 `verify_page_with_vision`（C# `IPageAnalyzer` YAGNI）。

**D7. `DeepSeekModelProvider` 仅 `CompleteTextAsync`，Vision/Multimodal 留 NIE**
DeepSeek text 优先；切片只跑 `parse_instruction`（text 模式）。

**D8（新）. provider 项目旧 stub（`DeepSeekTextUnderstanding` / `ClaudeTextUnderstanding` 等）本 change 不动**
它们是 Python 风格 provider-specific 子接口实现的残留，与 router 架构（子接口 provider-agnostic、靠 router 路由到 IModelProvider）互斥。清理涉 `ClaudeProvider` / `DeepSeekProvider` 所有子接口 stub（含 PageAnalyzer / TraversalAdvisor），是独立架构清理 change，不该塞进垂直切片。短期 `ITextUnderstanding` 有 4 实现，但仅新 `TextUnderstanding` 消费 `IModelRouter`，旧 stub 继续抛 NIE 不影响切片。*Alternative*: 本期一并删除 → 否决，scope 蔓延 + 删除破坏性需独立评审。

**D9（新）. capability 命名：本期两套词汇表并存**
`unibrain-facade` spec L49 锁 `UniBrainConfig.CapabilityRouting` 用粗粒度 3 个（`page_analysis` / `traversal_advisor` / `text_understanding`）；`ModelCapabilities` 用细粒度 5 个（Python 风格）。切片不消费 `UniBrainConfig.CapabilityRouting`（routing 硬编码传入 `ModelRouter`），两套词汇表本期不交叉。*Alternative*: 统一命名 → 否决，需改 `unibrain-facade` spec + 重设 routing 粒度，超切片 scope。留 Open Question 追踪整合。

## Risks / Trade-offs

- **[spec 契约翻转]** `model-provider` R1 观测责任从「子接口负责」改为「decorator 负责」 → *Mitigation*: 本 change 显式 `MODIFIED` 该 requirement；decorator 不可绕过，反比「子接口自觉观测」更强；archive 时同步四层文档（decisions/log.md）。
- **[provider stub 残留]** 旧 `DeepSeekTextUnderstanding` / `ClaudeTextUnderstanding` 造成实现位重复 → *Mitigation*: D8 记录，专项 change 清理；切片仅用新 `TextUnderstanding`。
- **[`InMemoryTraceRecorder` 需 `ITraceStorage`]** 设计稿 §11 无参组合示例无法编译 → *Mitigation*: tasks 修正示例，注入 `ITraceStorage`（或测试用 Null 实现）。
- **[`AICallRecord` 字段]** 设计稿 §8 漏列 `Context` / `Timestamp` → *Mitigation*: `ObservingModelProvider` 构造时 `Context` 留默认 null（切片无 span 上下文），`Timestamp` 由 recorder 填或默认；spec 已按 researcher 修正。
- **[capability 词汇表分裂]** 细粒度 5 vs 粗粒度 3 → *Mitigation*: D9 本期不交叉；Open Question 追踪。
- **[集成测试需网络/密钥]** `DeepSeekModelProvider` 集成测试 → *Mitigation*: opt-in，无 `DEEPSEEK_API_KEY` 跳过；`MockModelProvider` 覆盖无网络路径。

## Migration Plan

无生产部署（Domain 层 + provider 库）。落地顺序（tasks 详）：
1. `ModelCapabilities` + `ModelRequest.Capability`（破坏面最小，先立标签）
2. `MockModelFixture` / `MockModelProvider`（铺测试接缝）
3. `ObservingModelProvider`（观测挂钩）
4. `ModelRouter`（组装中心）
5. `TextUnderstanding` 实现 + 单测（业务语义）
6. `DeepSeekModelProvider` + `DeepSeekProviderConfig` + 集成测试（真实传输）

回滚：纯新增类，回滚 = 删类 + revert `ModelRequest` 字段。spec 契约翻转（R1）是唯一不可纯代码回滚项，需 revert `model-provider` spec 的 MODIFIED。

## Open Questions

1. （设计稿 §13.1）`TextUnderstanding` 遇 `ModelResponse.Success==false` 是抛 `DomainValidationException` 还是返回降级 `TextUnderstandingResult`（Confidence=0）？→ **暂定抛**（spec 采此），未来可演化为降级或重试 decorator。
2. （设计稿 §13.2）`MockModelFixture` JSON 目录约定（`tests/fixtures/` vs `src/UniClaw.Core/Simulation/Fixtures/`）？→ tasks 阶段定，倾向 `tests/fixtures/`。
3. （设计稿 §13.3）`ParseInstructionSchema` 是 `JsonElement` 还是 JSON 字符串常量？→ tasks 阶段定，倾向 const string（简单、可对照 Python）。
4. （设计稿 §13.4）集成测试 `DEEPSEEK_API_KEY` 注入方式（env / user-secrets / CI secret）？→ tasks 阶段定。
5. （新）provider 项目旧 stub（`DeepSeekTextUnderstanding` / `ClaudeTextUnderstanding` / PageAnalyzer / TraversalAdvisor）清理 change？→ 本 change 不做，记录待续。
6. （新）capability 词汇表整合（`ModelCapabilities` 细粒度 5 vs `UniBrainConfig.CapabilityRouting` 粗粒度 3）？→ 本 change 不交叉，记录待续。

## Apply 落地决策（2026-07-25，供 archive 同步 decisions/log.md）

落地阶段（顶层 Opus 统筹 + 1 researcher + 4 coder/refactorer 并行）的实现决策，补充上文 D1-D9：

- **AD1. DeepSeek 集成测试归属 → Core.Tests 加 `UniClaw.DeepSeekProvider` ProjectReference（option a）**。测试文件 `tests/UniClaw.Core.Tests/UniBrain/DeepSeekModelProviderTests.cs`；fixture `tests/UniClaw.Core.Tests/Fixtures/parse_instruction.mock.json`（已被现有 `Fixtures\**\*.json` globbing 覆盖）。*否决 (b) 新建 `UniClaw.DeepSeekProvider.Tests`*：单 provider 拆项目开销过大，留作 provider 数量膨胀后的 scale-out 路径。test→prod 引用不受 charter `DependencyDirectionGuard` 约束（guard 只验生产层方向）。
- **AD2. `MockModelFixture.FromJson` 用 `DomainJsonOptions.Default`**（非 `StateFixture` 的本地 `PropertyNameCaseInsensitive`），对齐 charter §6 序列化单点真源。落地的 `parse_instruction.mock.json` 用 camelCase 验证通过。
- **AD3. `ParseInstructionSchema` 定义为 `src/UniClaw.Core/UniBrain/Schemas.cs` 的 `public static class Schemas { public const string ParseInstruction = ... }`**（raw-string JSON schema）。传 `ModelRequest.Schema`；DeepSeek 传输层把 `Schema != null` 映射为 OpenAI `response_format:{type:"json_object"}`。
- **AD4. `AnthropicModelProvider` 及 provider 项目所有子接口 stub（`DeepSeekTextUnderstanding`/`ClaudeTextUnderstanding`/`DeepSeekTraversalAdvisor`/`ClaudePageAnalyzer`/`ClaudeTraversalAdvisor`）本 change 一律不动**（D8 / Non-Goal）。它们是 Python 风格 provider-specific 残留，与新 router 架构互斥，清理留独立 change。
- **AD5. `MockModelEntry`/`MockModelFixture`/`MockModelProvider` 为生产代码**（`src/UniClaw.Core/Simulation/`，namespace `UniClaw.Core.Simulation`，非测试内部），供 simulation 上层与测试共用，对齐既有 `MockTextUnderstanding`/`MockPageAnalyzer` 约定。
- **AD6（偏差）. live 集成测试默认 model 改 `deepseek-v4-flash`**（沙箱网关拒 `deepseek-chat` 返 400），由 `DEEPSEEK_MODEL`/`DEEPSEEK_BASE_URL` env 覆盖。spec 只要求 `model = config.Model`，未锁具体模型名 → 不违 spec；公共 `api.deepseek.com` 用户设 `DEEPSEEK_MODEL=deepseek-chat` 即可。
- **AD7. 单测随组件分散 authored**（非 task 5.3 单独一人）：Mock→B、ObservingModelProvider→C、ModelRouter→D、TextUnderstanding+E2E→E、DeepSeek→F，各测自家组件。task 5.3 由 B/C/D/E 共同完成。

## 验证结果（2026-07-25）

- `dotnet build src/UniClaw.Core.sln`：**0 警告 0 错误**（6 项目全绿，含 DeepSeekProvider）。
- `dotnet test src/UniClaw.Core.sln`：**903 通过 / 0 失败 / 0 跳过**（pre-change 基线 ~878 + 本 change 新增 38：Mock 4 + ObservingModelProvider 4 + ModelRouter 5 + TextUnderstanding 4 + E2E 1 + DeepSeek 20）。
- Guard：`--filter Guard` **55 全绿**（EnumValueGuard 12 + DependencyDirectionGuard 4 + UniBrainGuard 等），UniBrain→Observability 方向依 D-17 不拦，`ObservingModelProvider` 引用 `ITraceRecorder` 不触发 guard。
- 端到端（task 7.3）：`ParseInstructionEndToEndTests` 用真实 `PromptLibrary → TextUnderstanding → ModelRouter(MockModelProvider) → ObservingModelProvider` 全组件链，断言 `TextUnderstandingResult` 正确 + `Assert.Single(storage.GetAICalls())` 且 `Capability=="parse_instruction"`/`ProviderId=="mock"`/`Success==true`。
