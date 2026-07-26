## Why

`unibrain-modelprovider-vertical-slice` 与 `unibrain-traversaladvisor-vertical-slice` 已跑通两条端到端链（`parse_instruction` / `decide_next_action`），把「`IModelRouter` 路由 + `ObservingModelProvider` decorator + `ModelRequest.Capability` 标签 + `IPromptLibrary`」范式确立为 UniBrain 通用模式。但两条切片都走 **纯文本传输**（`CompleteTextAsync`），未触及多模态；`IPageAnalyzer` 仍无任何实现类（接口 3 方法已定义，仅 Simulation fixture mock）。

本 change 通过**第三条垂直切片**——跑通 `analyze_visual` 一条多模态端到端链——回答范式成立性的最后一块试金石：

- **多模态传输**：`AnalyzeCurrentPageAsync` 截图 `byte[]` 入，走 `IModelProvider.CompleteVisionAsync(req, bytes)`，是范式首次（也是 CLAUDE.md「先建 Mode A 通真机」里程碑的核心 capability）。
- **§12-A 单一真相源落地**：Python prompt 含很重的内联 `type→action` 散文映射（10 type → 4 action + 4 example）；本切片剥掉散文，AI 只做 `type` 分类，`expected_action` / `expects_page_change` / `expects_state_change` 由 code 侧 `ElementTypeMapper` 确定性派生——消除 prompt↔code 散映射漂移。
- **§12-B 截图归属**：截图捕获组合进 provider 侧，`IPageAnalyzer.AnalyzeCurrentPageAsync` 签名零改动。

同时本切片带一项**范式洁癖演进（D-8）**：子接口依赖从 `IModelRouter` 改为 `IModelProvider`（路由属装配决策，子接口只调模型、不碰路由抽象），`IModelRouter` 降为装配期工厂。前两条切片的统一留作 follow-up。

跑通这条切片即把范式确认覆盖到多模态传输，并把 Mode A 视觉链路的 **Core 侧** 铺通（真机/SDK/可靠性留 L2/L3）。

## What Changes

- **新增 `IScreenCapture` Core 设备 I/O 接缝**（`Core/Traversal/`，`IActionExecutor` 先例）：`Task<byte[]> CaptureAsync(CancellationToken)`。Core 持有抽象；真机实现（`AdbScreenCapture`）属 host（§12-B，L3 不实现）。
- **新增 `PageAnalyzer` 真实实现**（`Core/UniBrain/`，provider-agnostic）：`sealed class : IPageAnalyzer`，ctor 注入 **`IModelProvider`**（D-8：装配期 `router.Resolve(AnalyzeVisual)` 产物，已套 `ObservingModelProvider`）+ `IPromptLibrary` + `IScreenCapture`（三依赖）。`AnalyzeCurrentPageAsync` 走 7 步链路（截图 → 取模板 → `ModelRequest` → `CompleteVisionAsync` → DTO → `ElementTypeMapper` 派生 → `PageAnalysis`）。
- **切片边界（显式）**：本 slice 只实现 `AnalyzeCurrentPageAsync`；`FindAppEntryAsync` / `VerifyPageTypeAsync` 抛 `NotImplementedException`（带 "pending future slice" 文案），同前两切片对未覆盖方法的诚实部分实现策略（D-143 idiom 推广）。
- **§12-A 落地 — `ElementTypeMapper` 派生**：item 的 `expected_action` = `ElementTypeMapper.ToExpectedAction(type)`，`expects_page_change` / `expects_state_change` 由 `ExpectedAction` 确定性派生（§6.1 表：Navigate/Action→page change；Toggle→state change；None→both false）。prompt 删 type→action 散文，**保留 type 词表**（AI 分类需要）。
- **新增 `Schemas.AnalyzeVisual` 常量**：`PageAnalysis` 输出 JSON schema，镜像 DTO；`items` **只含 `name/type/coordinate/parent`，不含 action 3 字段**（§12-A）。
- **`analyze_visual` prompt 模板**（移植 Python `vision_service.py:19-112` 的 `PROMPT_STRUCTURE`，按 §12-A 剥散文）：`Variables` 空（截图走 `CompleteVisionAsync` 的 byte 参数，不入 prompt 变量）；测试侧 wiring 注册（与前两切片对称）。
- **测试**：`PageAnalyzerTests`（mock `IScreenCapture` + mock `IModelProvider.CompleteVisionAsync`，无网络）覆盖 §6.1 派生 4 分支 / ctor null / 模板缺失 / fail-fast / 截图透传 / NIE；`AnalyzeVisualEndToEndTests`（装配期 `router.Resolve(AnalyzeVisual)` 套观测 decorator + mock fixture）一条端到端 + 断言 `mode="vision"` 的 `AICallRecord`（多模态观测闭环）。

## Capabilities

### New Capabilities

无。本 change 落在已存在的 capability 上。

### Modified Capabilities

- `page-analyzer`:
  - **ADDED** `PageAnalyzer` 真实实现 requirement（`sealed class : IPageAnalyzer`，ctor 注入 `IModelProvider` + `IPromptLibrary` + `IScreenCapture`；`AnalyzeCurrentPageAsync` 7 步 SHALL 走截图 → `CompleteVisionAsync` → DTO → `ElementTypeMapper` 派生 → `PageAnalysis`；item 的 action 3 字段由 code 派生、prompt 不含 type→action 散文；模板缺失 / 模型失败 / 非法 type / 非法 Direction / coordinate 越界 fail-fast；`FindAppEntryAsync` / `VerifyPageTypeAsync` 抛 `NotImplementedException`）。现有 requirement（接口 3 方法签名 / `AppEntryPoint`）不变。
  - **ADDED** `IScreenCapture` 截图捕获接缝 requirement（Core 设备 I/O 抽象，`Task<byte[]> CaptureAsync(CancellationToken)`；Core 持有抽象，真机实现属 host）。本期唯一消费者为 `PageAnalyzer`；若未来出现第二消费者可提升为独立 capability。

## Impact

- **代码**：
  - `src/UniClaw.Core/Traversal/`：新增 `IScreenCapture`（1 接口）
  - `src/UniClaw.Core/UniBrain/`：新增 `PageAnalyzer`（1 类）+ 修改 `Schemas`（加 `AnalyzeVisual` 常量）
  - `tests/UniClaw.Core.Tests/UniBrain/`：新增 `PageAnalyzerTests` + `AnalyzeVisualEndToEndTests`（+ 截图/响应 fixture）
- **spec 契约**：`page-analyzer` 2 ADDED requirement（不动现有 2 个；接口签名零变更）
- **架构方向**：零新方向。复用前两切片已确立的 router + decorator + capability 范式；范式对多模态传输零基础设施扩展（`byte[] imageData` 本是 `CompleteVisionAsync` 方法参数，`ObservingModelProvider` 已覆盖）。D-8 子接口注入 `IModelProvider` 是范式洁癖演进，不改任一既有接口签名。不新增 enum（仅消费 `Direction` / `MenuItemType` / `ExpectedAction` 锁定值）；不新增 layer 引用；`ArchitectureGuardTests` 不受影响
- **测试**：单元（`PageAnalyzer`，mock `IScreenCapture` + mock `IModelProvider.CompleteVisionAsync`，无网络）+ 端到端（装配期 `router.Resolve` 套观测 decorator + mock fixture）；无 live HTTP / 真机（`AnthropicModelProvider.CompleteVisionAsync` 保持 stub）
- **不动**：`UniBrainService`（纯组合容器，零改动）；`IModelRouter` / `ModelRouter` / `ObservingModelProvider` / `IModelProvider` / `ModelRequest` 任一**签名**（装配期仍消费 `router.Resolve` 注入 `IModelProvider`，但 router 类型本身不改）；`IPageAnalyzer` 接口签名；Domain.Content 全部类型（只读消费）；`AnthropicModelProvider.CompleteVisionAsync`（保持 stub）；真机 / ADB / trace 集成 / host 生产注册
- **Python 偏离**（理由见 design）：§12-A 剥散文——AI 只返 `type`，action + page/state change 由 `ElementTypeMapper` code 派生（Python 让 AI 在 prompt 内推导并返回 action 3 字段）；`IScreenCapture` 截图捕获组合进 provider 侧而非 `IPageAnalyzer` 签名（Python `analyze_screenshot` 吃截图参数）
- **Follow-up（不在本 slice scope）**：D-8 范式演进回改前两切片（`TextUnderstanding` / `TraversalAdvisor` 从 `IModelRouter` 统一到 `IModelProvider`）——另起 refactor change（OQ-6）
