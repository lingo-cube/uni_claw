## Why

UniBrain 抽象层（`IModelProvider` / `IUniBrain` + 3 子接口 / Prompt Engine）已齐备，但**实现层全空**：子接口只有 Simulation 的 fixture mock，不经 `ModelRequest`/`ModelResponse`；`DeepSeekModelProvider` / `AnthropicModelProvider` 是 throw `NotImplementedException` 的 stub（无构造器、无 HttpClient、无 SDK）；`MockModelProvider` 不存在；`AICallRecord` + `ITraceRecorder.RecordAICallAsync` 观测挂钩已就位但 `IModelProvider` 链路未接通；`UniBrainConfig.CapabilityRouting` 是无人消费的 stub 字段。

与 Python 最大分叉：Python 的 `UniBrain`（`src/ai/provider.py`）自持 providers dict + 做 capability routing；C# 的 `UniBrainService` 是纯组合容器，routing 意图无处落地。

本 change 通过**最小垂直切片**——跑通 `parse_instruction` 一条端到端链（`TextUnderstandingRequest → IPromptLibrary → ModelRequest → IModelRouter → IModelProvider → ModelResponse → TextUnderstandingResult`）——验证 `IModelProvider` 抽象是否站得住、接通观测闭环、暴露基础设施缺口，并把可复用模式（router + decorator + capability 标签 + 传输级 mock）铺给其余 4 个 capability 与 provider。

## What Changes

- **新增 capability 路由层**：`IModelRouter` + `sealed ModelRouter`（capability → 已套 decorator 的 provider；构造期接收裸 provider + recorder，内部套 `ObservingModelProvider`；`Resolve` 查表 → default 回落 → 未知 capability fail-fast）
- **新增观测 decorator**：`ObservingModelProvider`（实现 `IModelProvider`，唯一 `AICallRecord` 挂钩点；router 组装期为每个裸 provider 套用，**结构上不可绕过**）
- **翻转 model-provider spec 的观测责任**（spec 契约改变，接口签名不变）：现有 spec 锁定「`IModelProvider` 不带 recorder / 子接口实现负责观测」→ 改为「decorator 在 router 组装期统一套用，观测不可绕过」。落地设计稿 D-E11 待解矛盾
- **`ModelRequest` 加 `string? Capability = null` 字段**（可选，向后兼容）：流经 router / decorator / 传输三层的唯一语义标签，让 mock 按 capability 查 fixture、decorator 记 capability、传输层可忽略
- **新增 `ModelCapabilities` 5 常量**（对齐 Python 5 capability：`parse_instruction` / `verify_page_type` / `decide_next_action` / `screen_safety` / `analyze_visual`）
- **填实 `DeepSeekModelProvider`**：OpenAI-compatible HTTP（`POST {BaseUrl}/chat/completions`），text 模式优先；Vision / Multimodal 留 `NotImplementedException`。配套 `DeepSeekProviderConfig`（构造期 fail-fast：ApiKey/Model/BaseUrl 非空、并发>0、超时>0）
- **新增 `MockModelProvider` + `MockModelFixture`**：传输级声明式 mock（capability → 预设 `MockModelEntry` 响应），平行于 `StateFixture` 的 sealed-record + FromJson + 内部 DTO 结构（DomainValidationException 校验为新增设计，非复用 —— `StateFixture` 现状不带校验）
- **新增 `TextUnderstanding` 真实实现**（`Core/UniBrain/`，provider-agnostic）：消费 `IModelRouter` + `IPromptLibrary`，组装 prompt + 解析 JSON 响应为 `TextUnderstandingResult`。注：provider 项目里已有的 `DeepSeekTextUnderstanding` / `ClaudeTextUnderstanding` stub 属 provider-specific 旧架构，本 change 不动（见 Impact）
- **接通观测闭环**：所有经 router 的 AI 调用产生 `AICallRecord`

**非目标（延后）**：`IPageAnalyzer` / `ITraversalAdvisor` 真实实现（另 4 个 capability）；`AnthropicModelProvider` 填实；DI 容器 / `IHttpClientFactory` / Polly；yaml 配置加载；`UniBrainConfig.CapabilityRouting` 运行时消费（本期硬编码 routing）；重试 / 缓存 decorator；provider 项目旧 stub 清理。

## Capabilities

### New Capabilities

无。本 change 全部落在已存在的 capability 上。

### Modified Capabilities

- `model-provider`:
  - **MODIFIED**「`IModelProvider` pure transport / 不记 `AICallRecord` / 子接口负责观测」requirement → 引入 `ObservingModelProvider` decorator，观测挂 router 组装期、结构不可绕过
  - **MODIFIED** `ModelRequest` requirement → 加 `string? Capability = null` 字段
  - **ADDED** `IModelRouter` / `ModelRouter`（capability 路由 + 组装期套 decorator + default 回落 + 未知 capability `DomainValidationException`）
  - **ADDED** `ObservingModelProvider` decorator（Stopwatch 计时 + 委托 + 记 `AICallRecord`）
  - **ADDED** `DeepSeekModelProvider` 真实实现（OpenAI-compatible HTTP、传输错误 graceful）+ `DeepSeekProviderConfig`（构造期 fail-fast）
  - **ADDED** `MockModelProvider` + `MockModelFixture`（传输级声明式 mock）+ `MockModelEntry`
  - **ADDED** `ModelCapabilities` 5 常量
- `text-understanding`:
  - **ADDED** `TextUnderstanding` 真实实现 requirement（消费 `IModelRouter` + `IPromptLibrary`，prompt 组装 + JSON 响应解析；模板缺失 / 模型调用失败 fail-fast）

## Impact

- **代码**：
  - `src/UniClaw.Core/UniBrain/`：新增 `ModelCapabilities` / `IModelRouter` / `ModelRouter` / `ObservingModelProvider` / `TextUnderstanding`（5 类）+ 修改 `ModelRequest`（加字段）
  - `src/UniClaw.Core/Simulation/`：新增 `MockModelEntry` / `MockModelFixture` / `MockModelProvider`（3 类）
  - `src/UniClaw.DeepSeekProvider/`：填实 `DeepSeekModelProvider` + 新增 `DeepSeekProviderConfig`
- **spec 契约**：`model-provider` 2 MODIFIED + 5 ADDED requirement；`text-understanding` 1 ADDED requirement
- **架构方向**：UniBrain 层引用 Observability（`ITraceRecorder`）。依 D-17（Observability 为 cross-cutting utility，非传统顶层），与 StateMachine / Traversal 引用 Observability 同等待遇；现有 `UniBrainGuardTests` 不验 UniBrain→Observability 方向，不触发 guard
- **测试**：单元（`TextUnderstanding` + `ObservingModelProvider` + `ModelRouter`，用 `MockModelProvider`，无网络）；集成（`DeepSeekModelProvider` 真实 HTTP，opt-in，无 `DEEPSEEK_API_KEY` 跳过）
- **不动**：provider 项目旧 stub（`ClaudeTextUnderstanding` / `DeepSeekTextUnderstanding` 等 provider-specific 子接口实现，在 router 架构下废弃，清理留待后续 change）；DI / `IHttpClientFactory` / Polly；`UniBrainConfig.CapabilityRouting` 消费；其余 4 capability 的子接口实现
- **Python 偏离**（理由见 design）：`UniBrainService` 不持有 providers / routing（下沉 `ModelRouter`）；观测用 decorator 而非子接口内联；`ModelCapabilities` 细粒度 5 个 vs `UniBrainConfig.CapabilityRouting` 粗粒度 3 个（本期两套词汇表不交叉，整合留开放问题）
