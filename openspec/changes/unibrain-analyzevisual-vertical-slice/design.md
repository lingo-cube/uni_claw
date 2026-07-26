## Context

UniBrain 通用范式（`IModelRouter` 路由 + `ObservingModelProvider` decorator + `ModelRequest.Capability` 标签 + `IPromptLibrary`）已由两条垂直切片确立：

| 切片 | capability | 形态 | 传输方法 |
|---|---|---|---|
| 1 (modelprovider) | `parse_instruction` | 纯文本入 → 扁平 4 字段出 | `CompleteTextAsync` |
| 2 (traversaladvisor) | `decide_next_action` | 复合类型序列化进 prompt → 7 字段含 Params 字典出 | `CompleteTextAsync` |
| **3 (本切片)** | **`analyze_visual`** | **截图 byte[] 入 → PageAnalysis 12 字段含嵌套 record 出** | **`CompleteVisionAsync`（多模态）** |

两条切片都走纯文本传输。`IPageAnalyzer` 接口已存在（3 方法签名齐全），无任何实现类（仅 Simulation fixture mock）。`ModelCapabilities.AnalyzeVisual` 常量已预留；`IModelProvider.CompleteVisionAsync(req, byte[], ct)` 已定义；`ObservingModelProvider.CompleteVisionAsync` 已转发并记 `mode="vision"`。**范式对多模态零基础设施缺口**——`byte[] imageData` 是 `CompleteVisionAsync` 的方法参数（独立于 `ModelRequest`）。

本切片要在不新增任何基础设施签名的前提下，把范式套到多模态传输上，验证它覆盖截图入、丰富嵌套出，并落地两项 vision 策略设计决策：
- **§12-A 单一真相源**：剥掉 Python prompt 内联的 type→action 散文映射，AI 只做 type 分类，action + page/state change 由 code 派生。
- **§12-B 截图归属**：截图捕获组合进 provider 侧，`IPageAnalyzer` 签名零改动。

上游设计稿（权威细节）：`docs/refactor/2026-07-26-unibrain-analyzevisual-vertical-slice-design.md`。

约束（来自 CLAUDE.md / charter / vision 策略）：
- `TypeHint` 8 值 / `SelectionState` 3 值 🔴火山级锁定 —— 本 slice **不碰**
- `Direction` / `MenuItemType` / `ExpectedAction` enum 锁定 —— 本 slice 仅消费、**绝不新增**
- 所有 record `sealed record class` + `ImmutableArray/Dictionary`
- 所有校验 `DomainValidationException` fail-fast
- Domain.Vision ↔ Domain.Content 零直接 import —— `PageAnalysis` 属 Content，本 slice 仅消费其类型
- C# 查询先 MCP 后 Read
- `ElementTypeMapper` 是 type→action 映射的**唯一真相源**（两 mode 共用）

## Goals / Non-Goals

**Goals:**
- 新增 `PageAnalyzer` sealed class，`AnalyzeCurrentPageAsync` 真实 7 步链路（截图 → `CompleteVisionAsync` → DTO → `ElementTypeMapper` 派生 → `PageAnalysis`），provider-agnostic（仅 `IModelProvider` + `IPromptLibrary` + `IScreenCapture`）
- 验证范式推广到「多模态入 → 嵌套丰富出」形态（范式成立性最后一块试金石）
- 落地 §12-A：`ElementTypeMapper` 成为 type→action 单一真相源，prompt 不含散映射
- 落地 §12-B：`IScreenCapture` 截图捕获组合进 provider 侧，`IPageAnalyzer` 签名零改动
- 落地 D-8：子接口注入 `IModelProvider`（装配期 `router.Resolve` 产物）替代 `IModelRouter`
- 复用观测闭环：经装配期 router 的调用必然产生 `mode="vision"` 的 `AICallRecord`
- 单元 + 端到端测试全绿，无网络 / 真机依赖

**Non-Goals:**
- `AnthropicModelProvider.CompleteVisionAsync` 填实真实 SDK —— L2
- `AdbScreenCapture : IScreenCapture` 真机实现 —— L3（依赖外部 E-1 设备/ADB）
- 真机可靠性度量 / golden-screenshot 测试台 —— L3（依赖外部 E-3 样本）
- §12-A 剥散文后的 Claude 纯 type 分类可靠性验证 —— E-3 / L2 / L3
- 截图 ref 进 trace（§12-B proposal-time 细节）—— 独立 trace 集成切片
- host 生产 prompt 注册 / DI 组合根 —— L2 host 落地（本 slice 测试 stub wiring）
- `FindAppEntryAsync` / `VerifyPageTypeAsync` 真实实现 —— 独立切片（本 slice NIE pending）
- D-8 回改前两切片（`TextUnderstanding` / `TraversalAdvisor` 从 `IModelRouter` 统一到 `IModelProvider`）—— follow-up refactor change（OQ-6）

## Decisions

### D1: 切片边界 = 仅 `AnalyzeCurrentPageAsync` 真实，其余 2 方法 `NotImplementedException`

**选择**：`PageAnalyzer` 实现 `AnalyzeCurrentPageAsync` 完整 7 步；`FindAppEntryAsync` / `VerifyPageTypeAsync` 抛 `NotImplementedException("PageAnalyzer.<method> pending future slice.")`。

**理由**：一个 capability = 一条垂直切片。`analyze_visual` 是 CLAUDE.md「先建 Mode A 通真机」里程碑的核心 capability（截图 → 多模态 AI → PageAnalysis 即 Mode A 视觉链路本身），走 vision 路径。其余 2 方法属不同语义（app 入口查找 / 页面类型验证），混入会模糊切片边界。

**备选**：同时实现 `VerifyPageTypeAsync`（同吃 PageAnalysis）—— 拒：它属 `verify_page_type` capability，语义不同。

**为何 NIE 而非 `NotSupportedException`**：对齐项目既有 idiom（前两切片的 Vision / `AnthropicModelProvider` stub 均用 NIE）；语义是「尚未」而非「永不」。D-143 idiom 推广到 `IPageAnalyzer`。

### D2: `PageAnalyzer` 三依赖 + 第 0 步截图 + `CompleteVisionAsync` 调用步

**选择**：ctor 注入 `IModelProvider` + `IPromptLibrary` + `IScreenCapture`（截图来源是第三依赖）。7 步链路：① `_screenCapture.CaptureAsync` 截图（范式新增步）② `GetTemplate(AnalyzeVisual)` 缺失 fail-fast ③ `Resolve({})`（截图是 bytes 不入 prompt 变量，Variables 空）④ `ModelRequest(User, System, Schemas.AnalyzeVisual, MaxTokens, Capability: AnalyzeVisual)` ⑤ `_modelProvider.CompleteVisionAsync(req, bytes, ct)`（直接调，无路由步）⑥ `!resp.Success` → fail-fast ⑦ `Deserialize<PageAnalysisDto>` → `MapToPageAnalysis` 派生。

**理由**：骨架沿用前两切片，仅插第 0 步截图、调用步换 `CompleteVisionAsync`。范式零基础设施扩展——`byte[]` 本是方法参数，`ObservingModelProvider` 已覆盖。

### D3: §12-A 落地 — `ElementTypeMapper` 派生 action + page/state change

**选择**：prompt 删 type→action 散文映射，AI 只返 `type`。映射阶段：
- `itemType = ElementTypeMapper.ToMenuItemType(dto.Type)`
- `action = ElementTypeMapper.ToExpectedAction(dto.Type)`（同 type 串二次查询，`ElementTypeMapper.ExpectedActionMap` 即 Python 散文复制的那份映射）
- `pageChange` / `stateChange` 由 `action` 确定性派生：

| `ExpectedAction` | `ExpectsPageChange` | `ExpectsStateChange` | 语义 |
|---|---|---|---|
| `Navigate` | true | false | 导航到新页面 |
| `Action` | true | false | 执行动作，导致页面变化 |
| `Toggle` | false | true | 切换开关，UI 状态变化 |
| `None` | false | false | 无预期变化 |

派生逻辑封在私有 helper（如 `DeriveChangeFlags(ExpectedAction)`）。

**理由**：`ElementTypeMapper.ExpectedActionMap` 覆盖 Python prompt 散文映射的全部 10 type，是 type→action 的 code 侧唯一真相源（vision 策略 §12-A 已核实）。剥散文不是改语义，是把散映射从 prompt 文本移到 code 单一真相源——AI 仍只做 type 分类（它擅长的），action 派生确定性、与 Python 生产路径行为对齐。

**零漂移核实结论**：9/10 type 与 Python 散文完全一致；唯一分歧 `link`——Python 散文标 `action`，C# `ExpectedActionMap` 标 `Navigate`。但二者派生出的 `expects_page_change/state_change` 相同（pageChange=true / stateChange=false），仅中间 enum 标签不同，**可观察行为一致**。剥散文后 AI 只返 `link`，code 侧按 C# 映射派生，结果与 Python 生产路径行为对齐（enum 标签差异属内部表示，不影响下游 page/state change 决策）。`link` 是否最终应归 `Action` 留 L2 真实样本对照（OQ-5 同域，不阻塞 L1）。

**备选**：保留 prompt 散文让 AI 返 action —— 拒：散映射在 prompt↔code 两处复制，Python prompt 已证实易漂移；§12-A 已锁单一真相源原则。

**注意**：剥的是「action 派生散文」，**保留 type 词表**（AI 分类需要知道分哪 10 类）；`ExpectedAction.Action` 同时设 `ExpectsPageChange=true` 的真机一致性留 OQ-5（L1 按 §12-A 文字约定）。C# `ElementTypeMapper` 另覆盖 `slider`/`input`/`item` 等 Python 10 type 词表外类型（C# 表更全），剥散文后 prompt 仍只列 Python 10 type；若 AI 返回 `slider` 等，`ElementTypeMapper` 照常映射（`ToMenuItemType`/`ToExpectedAction` 有合法回落），非非法值——但 `IsValidType` 当前只查 `MenuItemTypeMap`（含 slider/input），故仍判合法。

### D4: `PageAnalysisDto` + 映射模式（TraversalAdvisor DTO idiom 推广到 vision）

**选择**：反序列化用内部私有 DTO 宽松承载 prompt JSON（可空字段 + 宽容 coordinate），映射阶段调 `ElementTypeMapper` + 构造 Domain record（fail-fast）。DTO 镜像 §5.3 schema：`PageAnalysisDto` / `MenuInfoDto` / `ItemDto`（仅 name/type/coordinate/parent）/ `CoordDto` / `PopupInfoDto`。

**理由**：推广自第 2 条切片的 `DecideNextActionDto` 映射 idiom（D-141），处理更复杂形态（嵌套 MenuInfo/MenuItem/Coordinate + 12 字段）。DTO 宽容承载、映射期集中 fail-fast，分离「传输形态」与「Domain 不变式」。

### D5: `IScreenCapture` 作为 Core 设备 I/O 抽象（`IActionExecutor` 先例）

**选择**：新建 `IScreenCapture`（`Traversal/`，`Task<byte[]> CaptureAsync(CancellationToken)`），与 `IActionExecutor` 共置。Core 持有抽象；真机实现（`AdbScreenCapture`）属 host。`IPageAnalyzer.AnalyzeCurrentPageAsync` 签名零改动（§12-B）。

**理由**：截图捕获是 Core 接缝（vision 策略 §5「唯一真正缺失的接缝」），与 `IActionExecutor`（动作执行）并列。Core 纯净——零设备依赖。

**备选**：`AnalyzeCurrentPageAsync(byte[] screenshot)` 把截图作参数 —— 拒：违反 §12-B 截图归属原则，改接口签名，污染所有调用方/sim。

**OQ-1**：返回类型是否需扩含截图 ref（path/hash 进 trace）—— host 实现 / trace 集成切片再演化（L1 取最简 `byte[]`，YAGNI）。

### D6: `Schemas.AnalyzeVisual` items 剥 action 字段（与 §12-A 对齐）

**选择**：schema 常量的 items 只列 `name/type/coordinate/parent`，**不含** `expected_action` / `expects_page_change` / `expects_state_change`，与剥散文后的 prompt 输出契约一致。type 不在 schema enum 硬约束（宽容 AI 返回，code 侧 `ElementTypeMapper` 校验）。

**理由**：schema 是对 AI 输出的契约声明；既然 action 3 字段由 code 派生，schema 不该要求 AI 产出它们，否则契约自相矛盾。

### D7: 测试策略镜像前两切片（mock 全链）

**选择**：`MockModelProvider`（声明式返固定 JSON，无网络）+ mock `IScreenCapture`（返特定 bytes）+ fixture 端到端 + `InMemoryTraceRecorder` 断言 `mode="vision"` 观测记录。无 live HTTP / 真机。

### D8: 子接口注入 `IModelProvider` 替代 `IModelRouter`（范式洁癖演进）

**选择**：`PageAnalyzer` ctor 注入 `IModelProvider`（装配期 `router.Resolve(AnalyzeVisual)` 产物，已套 `ObservingModelProvider`），**不注入 `IModelRouter`**。方法体内无 `router.Resolve` 步（装配期完成）。

**理由**：review 反馈——路由属装配决策，业务子接口只调模型，不该碰路由抽象。子接口 provider-agnostic 性质更纯（连路由都不依赖）。`IModelRouter` 降为**装配期工厂**——不再作为子接口运行时依赖，但观测组装的结构性保证保留（`router.Resolve` 仍统一套 decorator）。

**备选**：沿用 `IModelRouter`（前两切片范式）—— 拒：暴露不必要的路由抽象给业务子接口，违背最小依赖。

**Scope 约束**：前两切片（`TextUnderstanding` / `TraversalAdvisor`）仍用旧范式，**本 slice 不回改**（避免混入无关重构），开 follow-up refactor change 统一（OQ-6）。

## Risks / Trade-offs

- **[§12-A 剥散文后 Claude 纯 type 分类可靠性未知]** → L1 mock 全链不验证模型能力；可靠性留 E-3 golden-screenshot / L2 / L3（OQ-2）。剥散文是架构正确性（单一真相源），与模型能力正交。
- **[`ExpectedAction.Action` 设 `ExpectsPageChange=true` 可能与 Python 行为不一致]** → L1 按 §12-A 文字约定；L2 真实样本对照（OQ-5）。
- **[大页截图 token 撑爆模型上下文]** → L1 用 fixture 级响应；真机大页裁剪/摘要留 L2/L3（OQ-3，Non-Goal）。
- **[模型返回非法 type 串 / 非法 Direction / coordinate 越界]** → `DomainValidationException` fail-fast（正确行为，暴露模型漂移）。
- **[D-8 引入范式不一致（本切片 IModelProvider / 前两切片 IModelRouter）]** → 显式记录为 OQ-6 follow-up change；本 slice 不混入回改以保切片纯净。
- **[其余 2 方法 NIE 在运行期被调用]** → 文案明确标注 pending；当前无 handler 接 `IPageAnalyzer` 真实实现，无实际触发路径。

## Migration Plan

无迁移。纯新增类（`PageAnalyzer`）+ 新增接口（`IScreenCapture`）+ `Schemas` 加常量 + 2 个 spec requirement ADDED；不改既有接口签名、不改既有 record 字段、不改既有 requirement。回滚 = 删除新增类/接口/测试。

## Open Questions

1. **D-8 范式统一 follow-up**：前两切片（`TextUnderstanding` / `TraversalAdvisor`）从 `IModelRouter` 统一到 `IModelProvider` —— 另起 refactor change（不在本 slice scope）。
2. **生产 prompt 模板 registry 落点**：`analyze_visual` 模板最终在哪统一注册？本 slice 测试侧 wiring；生产组合根 wiring 留统一 change（与 `parse_instruction` / `decide_next_action` 一并，OQ-4）。
3. **`IScreenCapture` 返回类型是否扩含截图 ref**：host 实现 / trace 集成切片再定（L1 最简 `byte[]`，OQ-1）。
4. **§12-A 剥散文后 Claude 纯 type 分类可靠性**：E-3 golden-screenshot / L2 / L3（OQ-2）。
5. **`ExpectedAction.Action` 的 `ExpectsPageChange=true` 真机一致性** + **`link` 归 `Navigate` vs `Action` 的真机一致性**：L2 真实样本对照（OQ-5）。L1 按 §12-A 文字约定 + C# `ExpectedActionMap` 现状派生，可观察行为与 Python 对齐（`link` 两映射均产 pageChange=true/stateChange=false）。

## Prompt Template (4.1 终稿)

`analyze_visual` 模板（capability = `ModelCapabilities.AnalyzeVisual`，`Variables = ImmutableArray<string>.Empty`——截图走 `CompleteVisionAsync` byte 参数，不入 prompt 变量）。移植自 Python `vision_service.py:19-112` 的 `PROMPT_STRUCTURE`，按 §12-A 剥散文：删 `BUTTON TYPE CLASSIFICATION` 段的 type→action 映射 + `expected_action`/`expects_page_change`/`expects_state_change` 输出字段要求 + 4 example；**保留** 任务描述 + 输出 JSON 格式 + **type 词表**（10 type）。

**SystemPrompt**：
> You are analyzing a mobile app screen for UI traversal. Analyze this screenshot and provide: (1) menu structure (level 1 and level 2 menus with positions and active state), (2) current path (which menus are active/highlighted), (3) all clickable items in the content area each classified by `type`, (4) any popups/dialogs/special UI elements.
>
> Item `type` vocabulary (exactly one per item): `menu_item` (list items navigating to sub-pages), `tab` (top-level view switch), `back_button` (back/return), `switch` (on/off toggle with sliding animation), `toggle` (state-toggle buttons e.g. favorite), `button` (generic action), `link` (navigation links/hypertext), `icon` (icon-only buttons), `text` (non-interactive text), `readonly` (display-only).
>
> Return ONLY JSON with this exact structure (coordinates normalized 0-1): `{ "level1_dir": "left|right|top|bottom", "level1_menus": [{"name","coordinate":{"x","y"},"active"}], "level2_dir": "left|right|top|bottom", "level2_menus": [/* same shape as level1_menus */], "current_path": ["..."], "items": [{"name","type","coordinate":{"x","y"},"parent"}], "is_popup": false, "popup_info": {"title","content","close_button":{"x","y"}} or null, "close_button": {"x","y"} or null, "back_button": {"x","y"} or null, "has_scroll": false, "is_end_of_list": false }`.
>
> Important: coordinates normalized 0-1; mark parent-child via `parent`; `current_path` indicates active menus; name icons like "[icon] description"; include all interactive elements; level1_dir/level2_dir MUST be a single value from left/right/top/bottom (NEVER pipe-separated; choose ONE).

**UserPrompt**：
> Analyze the current app screenshot and return the PageAnalysis JSON above.

> 注：prompt **不**要求 AI 产出 `expected_action`/`expects_page_change`/`expects_state_change`——这三字段由 code 侧 `ElementTypeMapper` 派生（D3 / §12-A）。type 词表保留供 AI 分类。本 slice 在测试侧 wiring 此模板；生产组合根统一注册留 OQ-2 的 wiring change（与 `parse_instruction` / `decide_next_action` 一并）。
