# UniBrain AnalyzeVisual 实现设计 — 垂直切片

> **状态**: 设计稿（待 writing-plans 拆任务 / OpenSpec propose）
> **日期**: 2026-07-26
> **作者**: Fran
> **上游**:
> - `docs/refactor/2026-07-21-unibrain-concept-design.md`（UniBrain 概念设计）
> - `docs/refactor/2026-07-25-unibrain-modelprovider-vertical-slice-design.md`（范式第 1 条切片）
> - `docs/refactor/2026-07-15-vision-mode-strategy-design.md`（§12-A 单一真相源、§12-B 截图归属）
> **范围**: 垂直切片 L1 — Core 范式 + 单测，跑通 `IPageAnalyzer.AnalyzeCurrentPageAsync`（`analyze_visual` capability）一条多模态端到端链路（mock 全链）

---

## 1. 动机与背景

UniBrain 范式（`IModelRouter` + `ObservingModelProvider` + `ModelRequest.Capability` + `IPromptLibrary`）已由两条垂直切片确立并验证可推广：

| 切片 | capability | 形态 | 范式贡献 |
|---|---|---|---|
| 1 (modelprovider) | `parse_instruction` | 纯文本入 → 扁平 4 字段出 | 确立范式（D-135~137） |
| 2 (traversaladvisor) | `decide_next_action` | 复合类型序列化进 prompt → 7 字段含 Params 字典出 | 推广范式（D-140） |
| **3 (本切片)** | **`analyze_visual`** | **截图 byte[] 入（多模态）→ PageAnalysis 12 字段含嵌套 record 出** | **范式首次走多模态传输 + §12-A 单一真相源落地** |

两条切片都走 `IModelProvider.CompleteTextAsync`（纯文本 prompt）。**本切片是范式首次走 `CompleteVisionAsync`（prompt + 截图）**，验证范式对多模态传输的覆盖——这是范式成立性的最后一块试金石。

`analyze_visual` 同时是 CLAUDE.md 首要里程碑「先建 Mode A 通真机」的核心 capability：截图 → 多模态 AI → `PageAnalysis` 就是 Mode A 视觉链路本身（对齐 Python `ClaudeVisionService.analyze_screenshot` 生产路径）。L1 切片用 mock 把这条链路的 **Core 侧** 铺通，把真机/SDK/可靠性验证（依赖外部 E-1~E-4）留给 L2/L3。

### 1.1 现状盘点

| 项 | 状态 |
|---|---|
| `IPageAnalyzer` 接口（3 方法） | ✅ 签名齐全（[IPageAnalyzer.cs](../../src/UniClaw.Core/UniBrain/IPageAnalyzer.cs)） |
| `ModelCapabilities.AnalyzeVisual` 常量 | ✅ 已预留（[ModelCapabilities.cs:22](../../src/UniClaw.Core/UniBrain/ModelCapabilities.cs#L22)） |
| `IModelProvider.CompleteVisionAsync(req, byte[], ct)` | ✅ 多模态传输方法已定义（[IModelProvider.cs:19](../../src/UniClaw.Core/UniBrain/IModelProvider.cs#L19)） |
| `ObservingModelProvider.CompleteVisionAsync` 转发 + 记 `mode="vision"` 观测 | ✅ 已覆盖（[ObservingModelProvider.cs:42-51](../../src/UniClaw.Core/UniBrain/ObservingModelProvider.cs#L42-L51)） |
| `IModelRouter.Resolve(capability)` → 已套 decorator 的 `IModelProvider` | ✅ 已实现 |
| `PageAnalysis` / `MenuInfo` / `MenuItem` / `Coordinate` / 4 enum + Extensions | ✅ Domain.Content 齐备 |
| `ElementTypeMapper.ToMenuItemType` / `ToExpectedAction` | ✅ 派生 API 已存在 |
| **`PageAnalyzer : IPageAnalyzer` 实现** | ❌ 不存在（切片新建） |
| **`IScreenCapture` 截图捕获接缝** | ❌ 不存在（vision 策略 §5「唯一真正缺失的接缝」） |
| **`Schemas.AnalyzeVisual`** | ❌ 不存在（仅 ParseInstruction / DecideNextAction） |
| **`analyze_visual` 业务 prompt**（PROMPT_STRUCTURE 移植） | ❌ 不存在（Python `vision_service.py:19-112` 待移植） |
| `AnthropicModelProvider.CompleteVisionAsync` | ⚠️ stub（全 `NotImplementedException`） |

**关键结论（范式无缺口）**：`byte[] imageData` 是 `CompleteVisionAsync` 的**方法参数**（独立于 `ModelRequest` 之外），`ObservingModelProvider` 已正确转发。装配期 `router.Resolve(AnalyzeVisual)` 产出的 `IModelProvider` 调 `CompleteVisionAsync(req, bytes, ct)` 直接可走——**`ModelRequest` 不动、`IModelRouter` 不动、`ObservingModelProvider` 不动**。范式对多模态零扩展。

---

## 2. 设计原则

| 要求 | 设计落点 |
|---|---|
| 范式一致性 + 洁癖演进 | `PageAnalyzer` 沿用第 1/2 条切片骨架，但子接口依赖从 `IModelRouter` 改为 `IModelProvider`（D-8：路由属装配决策，子接口只调模型、不碰路由抽象）。插第 0 步截图、调用步换 `CompleteVisionAsync`。范式零基础设施扩展。 |
| 单一真相源（§12-A） | prompt 删 type→action 散文映射，AI 只做 type 分类；`expected_action` / `expects_page_change` / `expects_state_change` 由 `ElementTypeMapper` 在 code 侧确定性派生。零漂移。 |
| 截图归属（§12-B） | 截图捕获组合进 provider 侧（`PageAnalyzer` 注入 `IScreenCapture`），`IPageAnalyzer` 签名零改动，sim 不受影响。 |
| Core 纯净 | `IScreenCapture` 是 Core 抽象接口（同 `IActionExecutor` 先例），设备实现（`AdbScreenCapture`）留 host。Core 零设备依赖。 |
| 一 capability 一切片 | 本切片只实现 `AnalyzeCurrentPageAsync`；`FindAppEntryAsync` / `VerifyPageTypeAsync` 抛 NIE pending（D-143 idiom 推广）。 |
| fail-fast 校验 | ctor null 校验 + Coordinate 0-1 校验 + enum `FromValue` + DTO 必填字段校验，全用 `DomainValidationException`。 |

---

## 3. 目标与非目标

### 目标（L1）
1. 跑通 `PageAnalyzer.AnalyzeCurrentPageAsync` 一条多模态端到端链：`IScreenCapture.CaptureAsync → IPromptLibrary → ModelRequest → IModelProvider.CompleteVisionAsync(req, bytes) → ModelResponse → PageAnalysisDto → ElementTypeMapper 派生 → PageAnalysis`（`IModelProvider` 由装配期 `router.Resolve(AnalyzeVisual)` 注入）
2. 新建 `IScreenCapture` Core 接口（截图捕获抽象，`IActionExecutor` 先例）
3. 新建 `PageAnalyzer : IPageAnalyzer`（范式首次三依赖 ctor，`AnalyzeCurrentPageAsync` 真实实现）
4. 新增 `Schemas.AnalyzeVisual`（输出 JSON schema 常量，镜像 DTO）
5. 移植 Python `PROMPT_STRUCTURE` → `PromptTemplate`（`analyze_visual` capability），按 §12-A 剥 type→action 散文
6. 落地 §12-A 派生：`ElementTypeMapper.ToMenuItemType` / `ToExpectedAction` + `ExpectedAction → page/state change` 确定性派生
7. 单元测试 + 端到端测试（mock `IScreenCapture` + mock `IModelProvider.CompleteVisionAsync`）

### 非目标（延后项）
- `AnthropicModelProvider.CompleteVisionAsync` 填实真实 Anthropic SDK（→ L2）
- `AdbScreenCapture : IScreenCapture` 真机实现（→ L3，依赖 E-1）
- 真机可靠性度量 / golden-screenshot 测试台（→ L3，依赖 E-3）
- 截图 ref 进 trace（§12-B proposal-time 细节，→ 独立 trace 集成切片）
- host 生产 prompt 注册（template-engine design Non-Goal：业务 prompt 属 host；L1 只测试 stub 注册）
- `FindAppEntryAsync` / `VerifyPageTypeAsync` 真实实现（→ 独立切片，本切片 NIE pending）
- §12-A 剥散文的真机验证（Claude 纯 type 分类可靠性 → E-3 / L2 / L3）

---

## 4. 架构总览

```
装配期 (组合根/test):
   var provider = router.Resolve(AnalyzeVisual)   ← IModelRouter 降为装配期工厂, 仍套 ObservingModelProvider
   new PageAnalyzer(provider, promptLibrary, screenCapture)   ← 子接口只拿 IModelProvider, 见不到 router
                                          │
   IPageAnalyzer.AnalyzeCurrentPageAsync(ct)                     ← Core 接口（签名零改动）
        │
        ▼
PageAnalyzer : IPageAnalyzer   (Core, 新建)                   ← 范式第 3 条切片
   ctor(IModelProvider modelProvider, IPromptLibrary prompts,
        IScreenCapture screenCapture)                         ← 三依赖, 不含 IModelRouter (D-8)
        │
        ├── 0. var bytes = await _screenCapture.CaptureAsync(ct)         ← 范式新增步
        ├── 1. template = _prompts.GetTemplate(AnalyzeVisual)  → null → fail-fast
        ├── 2. template.Resolve({})                           ← 截图是 bytes, 不入 prompt 变量
        ├── 3. new ModelRequest(User, System, Schemas.AnalyzeVisual,
        │                       MaxTokens, Capability: AnalyzeVisual)
        ├── 4. resp = await _modelProvider.CompleteVisionAsync(req, bytes, ct)  ← 直接调, 无路由步
        ├── 5. !resp.Success → DomainValidationException
        └── 6. Deserialize<PageAnalysisDto>(resp.Content)
                → MapToPageAnalysis(dto)                      ← ElementTypeMapper 派生 + 构造
                → PageAnalysis
                                                              │
   IScreenCapture (Core, 新建) ◄──────────────────────────────┘ 截图来源
        ▲
   mock (test) / AdbScreenCapture (host, L3, 本切片不实现)
```

**与前两条切片的同构与差异**：

| 步 | TextUnderstanding / TraversalAdvisor（现范式） | PageAnalyzer（本切片，新范式 D-8） |
|---|---|---|
| ctor 依赖 | router + prompts（2 个） | **modelProvider** + prompts + screenCapture（3 个） |
| 路由步 | `_router.Resolve(cap)`（方法体内） | ❌ 删除（装配期 `router.Resolve` 完成，子接口见不到 router） |
| 0 | — | **截图**（范式新增步） |
| 调用步 | `CompleteTextAsync(req, ct)` | **`CompleteVisionAsync(req, bytes, ct)`** |
| 反序列化 | DTO → TResult（直通/Enum.TryParse） | **DTO → ElementTypeMapper 派生 → PageAnalysis** |

> **范式演进（D-8）**：本切片起子接口注入 `IModelProvider`（装配期 `router.Resolve` 产物）替代 `IModelRouter`——路由属装配决策，子接口只调模型、不碰路由抽象。`IModelRouter` 降为装配期工厂，观测组装的结构性保证保留（`router.Resolve` 仍套 `ObservingModelProvider`）。前两条切片（TextUnderstanding/TraversalAdvisor）仍用旧范式，**follow-up change 统一**。

---

## 5. 类型清单

### 5.1 `src/UniClaw.Core/Traversal/IScreenCapture.cs` — 新建

```csharp
namespace UniClaw.Core.Traversal;

/// <summary>
/// IScreenCapture — 屏幕截图捕获抽象（Core 设备 I/O 接缝）。
/// 与 IActionExecutor 同列（vision 策略 §5：截图捕获 + 动作执行为两个 Core 接缝）。
/// Core 只持有抽象；真机实现（AdbScreenCapture）属 host，不进 Core（§12-B）。
/// </summary>
public interface IScreenCapture
{
    /// <summary>捕获当前屏幕，返回 PNG/JPEG 字节流。</summary>
    Task<byte[]> CaptureAsync(CancellationToken ct = default);
}
```

- **位置**：`Traversal/`，与 `IActionExecutor`（[IGraphTraversalEngine.cs:59](../../src/UniClaw.Core/Traversal/IGraphTraversalEngine.cs#L59)）共置——两者都是 Core 设备 I/O 抽象，vision 策略 §5 并列。
- **签名**：最简 `Task<byte[]>`。§12-B 的「截图 ref 进 trace」是独立关注点，host 实现时再演化（可能扩含 ref 的返回类型）。L1 YAGNI。
- **实现**：L1 不提供；test 注入 mock，`AdbScreenCapture` 留 L3。

### 5.2 `src/UniClaw.Core/UniBrain/PageAnalyzer.cs` — 新建

```csharp
namespace UniClaw.Core.UniBrain;

/// <summary>
/// PageAnalyzer — 页面感知能力实现（IPageAnalyzer）。
/// 范式第 3 条切片：IModelProvider + IPromptLibrary + IScreenCapture（三依赖）。
/// D-8：子接口注入 IModelProvider 替代 IModelRouter，router 降为装配期工厂。
/// AnalyzeCurrentPageAsync 走 CompleteVisionAsync（多模态）；其余 2 方法 NIE pending。
/// </summary>
public sealed class PageAnalyzer : IPageAnalyzer
{
    private readonly IModelProvider _modelProvider;
    private readonly IPromptLibrary _promptLibrary;
    private readonly IScreenCapture _screenCapture;

    public PageAnalyzer(IModelProvider modelProvider, IPromptLibrary promptLibrary, IScreenCapture screenCapture)
    {
        // 三依赖 null → DomainValidationException (范式 fail-fast)
        _modelProvider = modelProvider ?? throw new DomainValidationException(nameof(modelProvider), "null");
        _promptLibrary = promptLibrary ?? throw new DomainValidationException(nameof(promptLibrary), "null");
        _screenCapture = screenCapture ?? throw new DomainValidationException(nameof(screenCapture), "null");
    }

    public async Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default) { /* §4 七步 (0-6) */ }

    public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
        => throw new NotImplementedException("PageAnalyzer.FindAppEntryAsync pending future slice.");
    public Task<PageTypeVerification> VerifyPageTypeAsync(PageAnalysis pageAnalysis, string expectedType,
        string? expectedPageName = null, CancellationToken ct = default)
        => throw new NotImplementedException("PageAnalyzer.VerifyPageTypeAsync pending future slice.");
}
```

### 5.3 `src/UniClaw.Core/UniBrain/Schemas.cs` — 扩展

新增 `AnalyzeVisual` 常量，镜像 `PageAnalysisDto`（§6），**items 只含 type，不含 action 字段**（§12-A）：

```csharp
public const string AnalyzeVisual = """
    {
      "type": "object",
      "properties": {
        "level1_dir":   { "type": "string", "enum": ["left","right","top","bottom"] },
        "level1_menus": { "type": "array", "items": { "type": "object",
            "properties": { "name": {"type":"string"}, "coordinate": {"type":"object","properties":{"x":{"type":"number"},"y":{"type":"number"}}}, "active": {"type":"boolean"} } } },
        "level2_dir":   { "type": "string", "enum": ["left","right","top","bottom"] },
        "level2_menus": { "type": "array", "items": { /* 同 level1_menus */ } },
        "current_path": { "type": "array", "items": { "type": "string" } },
        "items":        { "type": "array", "items": { "type": "object",
            "properties": { "name": {"type":"string"}, "type": {"type":"string"},
                            "coordinate": { /* x,y */ }, "parent": {"type":"string"} } } },
        "is_popup":      { "type": "boolean" },
        "popup_info":    { "type": ["object","null"], "properties": { "title":{"type":"string"}, "content":{"type":"string"}, "close_button": { /* x,y */ } } },
        "close_button":  { "type": ["object","null"], "properties": { "x":{"type":"number"}, "y":{"type":"number"} } },
        "back_button":   { "type": ["object","null"], "properties": { "x":{"type":"number"}, "y":{"type":"number"} } },
        "has_scroll":    { "type": "boolean" },
        "is_end_of_list":{ "type": "boolean" }
      }
    }
    """;
```

> 注：`items` 故意**省略** `expected_action` / `expects_page_change` / `expects_state_change`——§12-A 剥散文，这三字段由 code 派生，AI 不再产出。type 词表保留在 prompt 指令里（AI 分类需要），不在 schema enum 硬约束（宽容 AI 返回，code 侧 `ElementTypeMapper.IsValidType` 校验）。

### 5.4 `PageAnalysisDto`（`PageAnalyzer.cs` 内部私有）— 反序列化专用

镜像 prompt JSON，宽容承载（可空 + 用 `JsonElement` 处理 coordinate），映射阶段 fail-fast：

```csharp
private sealed class PageAnalysisDto {
    public string? Level1Dir;
    public List<MenuInfoDto>? Level1Menus;
    public string? Level2Dir;
    public List<MenuInfoDto>? Level2Menus;
    public List<string>? CurrentPath;
    public List<ItemDto>? Items;
    public bool IsPopup;
    public PopupInfoDto? PopupInfo;
    public CoordDto? CloseButton;
    public CoordDto? BackButton;
    public bool HasScroll;
    public bool IsEndOfList;
}
private sealed class MenuInfoDto { public string Name = ""; public CoordDto? Coordinate; public bool Active; }
private sealed class ItemDto { public string Name = ""; public string Type = ""; public CoordDto? Coordinate; public string? Parent; }
private sealed class CoordDto { public double X; public double Y; }
private sealed class PopupInfoDto { public string? Title; public string? Content; public CoordDto? CloseButton; }
```

---

## 6. 数据流：DTO → `ElementTypeMapper` 派生 → `PageAnalysis`（§12-A 落地）

本切片最复杂、也最有架构价值的一步。Python prompt 让 AI 返回每个 item 的 `type` + `expected_action` + `expects_page_change` + `expects_state_change`（含散映射）。§12-A 剥散文后，**AI 只返回 `type`，其余 3 字段由 code 确定性派生**：

```
prompt 返 JSON items[{name, type, coordinate{x,y}, parent}]    ← 剥掉 action 3 字段
        │
        ▼  JsonSerializer.Deserialize<PageAnalysisDto>(resp.Content, DomainJsonOptions.Default)
        │
        ▼  MapToPageAnalysis(dto) 逐字段映射:
   ┌─────────────────────────────────────────────────────────────┐
   │ MenuInfoDto → MenuInfo(Name, new Coordinate(x,y), Active)    │ ← Coordinate 0-1 fail-fast
   ├─────────────────────────────────────────────────────────────┤
   │ ItemDto → MenuItem 逐项:                                      │
   │   var itemType   = ElementTypeMapper.ToMenuItemType(dto.Type)│ ← 已存在
   │   var action     = ElementTypeMapper.ToExpectedAction(dto.Type)│ ← 已存在, 同 type 串二次查询
   │   var pageChange = action is Navigate or Action             │ ← ExpectedAction 派生
   │   var stateChange= action is Toggle                          │
   │   new MenuItem(name, coord, itemType, parent, null, action, pageChange, stateChange) │
   └─────────────────────────────────────────────────────────────┘
        │
        ▼  Direction.FromValue(level1_dir/level2_dir)  ← enum FromValue, 非法抛 DomainValidationException
        │
        ▼  new PageAnalysis(level1Dir, level1Menus, level2Dir, level2Menus, currentPath,
                           items, isPopup, popupInfo, closeButton, backButton, hasScroll, isEndOfList)
                                                              ← PageAnalysis 自有 fail-fast 校验
```

### 6.1 §12-A 派生规则（确定性，集中可测）

| `ElementTypeMapper.ToExpectedAction(type)` | `ExpectsPageChange` | `ExpectsStateChange` | 语义 |
|---|---|---|---|
| `Navigate` | ✅ true | false | 导航到新页面 |
| `Action` | ✅ true | false | 执行动作，导致页面变化 |
| `Toggle` | false | ✅ true | 切换开关，UI 状态变化 |
| `None` | false | false | 无预期变化 |

派生逻辑封在私有 helper（如 `DeriveChangeFlags(ExpectedAction)`），单测覆盖 4 分支。

> **零漂移保证**：`ElementTypeMapper.ExpectedActionMap` 正是 Python prompt 散文复制的那份 type→action 映射（vision 策略 §12-A 已核实）。剥散文不是改语义，是把散映射从 prompt 文本移到代码单一真相源。AI 仍只做 type 分类（它擅长的），action 派生确定性、与 Python 生产路径行为一致。

### 6.2 校验链（全 `DomainValidationException`）

- `Coordinate(x,y)`：x/y 任一超出 [0,1] → 构造期 fail-fast（[EnumsAndCoordinate.cs:21](../../src/UniClaw.Core/Domain/Models/Content/EnumsAndCoordinate.cs#L21)）
- `ElementTypeMapper.ToMenuItemType/ToExpectedAction`：非法 type 串 → fail-fast
- `Direction.FromValue`：非法方向 → fail-fast
- DTO 必填字段缺失（如 `Items` null、`ItemDto.Type` 空）→ 映射期 fail-fast
- `resp.Success == false` → 第 5 步 fail-fast（模型调用失败）

---

## 7. PROMPT_STRUCTURE 移植 + §12-A 剥散文

### 7.1 移植源头

Python `main` 分支 `src/ai/vision_service.py:19-112` 的 `PROMPT_STRUCTURE` 常量：让 Claude 分析 mobile app 截图，返回单一 JSON（schema 见 §5.3）。**含很重的内联 type→action 散文映射**（`BUTTON TYPE CLASSIFICATION` 段：10 type → 4 action + 4 example）。

### 7.2 剥什么、留什么

| 内容 | 处理 | 理由 |
|---|---|---|
| 任务描述（分析截图、识别菜单/items/popup） | ✅ 保留 | 核心指令 |
| 输出 JSON 格式描述（字段名、坐标归一化 0-1） | ✅ 保留 | 对齐 DTO |
| **type 词表**（menu_item/tab/back_button/switch/toggle/button/icon/link/text/readonly） | ✅ **保留** | AI 分类需要知道分哪 10 类 |
| **type→action 散文映射**（menu_item→navigate, switch→toggle…） | ❌ **删除** | code 侧 `ElementTypeMapper.ToExpectedAction` 派生，单一真相源 |
| **4 个 example**（互联/移动数据/开关/设置） | ❌ **删除** | 教 AI 推导 action，现已 code 派生，example 失去意义 |
| `expected_action` / `expects_page_change` / `expects_state_change` 输出字段要求 | ❌ **删除** | AI 不再产出，code 派生 |

### 7.3 `PromptTemplate` 形态

```csharp
new PromptTemplate(
    Capability:     ModelCapabilities.AnalyzeVisual,            // "analyze_visual"
    SystemPrompt:   <移植后的 PROMPT_STRUCTURE — 任务+格式+type 词表, 剥散文>,
    UserPrompt:     "分析当前应用截图，返回上述格式的 PageAnalysis JSON。",
    Variables:      ImmutableArray<string>.Empty)               // 截图是 bytes 不入 prompt, 无变量
```

- **Variables 空**：`AnalyzeCurrentPageAsync` 无业务参数，截图走 `CompleteVisionAsync` 的 byte 参数，prompt 是静态模板。`Resolve({})` 空字典通过（`PromptTemplate` 允许空 Variables，D-2 校验只约束已声明变量）。
- **注册位置（L1）**：测试 stub（`PageAnalyzerTests` / `AnalyzeVisualEndToEndTests` 构造期注册，与前两条切片对称）。生产注册留 host（template-engine design Non-Goal）。

---

## 8. 测试策略

L1 全程 **mock 全链**，不碰真实 Anthropic SDK / 真机 / API。

### 8.1 单元测试（`PageAnalyzerTests.cs`）

- **ctor 校验**：modelProvider/prompts/screenCapture 任一 null → `DomainValidationException`
- **模板缺失**：`IPromptLibrary.GetTemplate(AnalyzeVisual)` 返 null → fail-fast（不发模型调用）
- **DTO 映射正确性**：mock `CompleteVisionAsync` 返固定 JSON → 验证 `PageAnalysis` 各字段
  - level1/level2 menu 反序列化（name/coordinate/active）
  - **item 的 ElementTypeMapper 派生**：给定 type=switch → `Type=Switch, ExpectedAction=Toggle, ExpectsStateChange=true, ExpectsPageChange=false`（§6.1 四分支全覆盖）
  - current_path / popup / close_button / back_button / scroll 字段
- **fail-fast**：非法 type 串 / 非法 Direction / coordinate 越界 / 必填字段缺失 → `DomainValidationException`
- **截图透传**：mock `IScreenCapture.CaptureAsync` 返特定 bytes → 验证传到 `CompleteVisionAsync` 的 byte[] 与之一致
- **NIE**：`FindAppEntryAsync` / `VerifyPageTypeAsync` 抛 `NotImplementedException`（含 "pending future slice"）

### 8.2 端到端测试（`AnalyzeVisualEndToEndTests.cs`）

- 真实 `PageAnalyzer` + 装配期 `router.Resolve(AnalyzeVisual)`（套 `ObservingModelProvider`，产物为 mock `IModelProvider` 声明式返固定 JSON）+ mock `IScreenCapture` + 注册 `analyze_visual` prompt stub —— 一并验证 D-8「router 降为装配期工厂」的正确性
- 验证整链：`CaptureAsync → CompleteVisionAsync(req, bytes) → Content → PageAnalysis`
- 验证观测：`ITraceRecorder` 收到一条 `AICallRecord`（`mode="vision"`, `capability=analyze_visual`）—— 确认多模态观测闭环

### 8.3 不测（L1 范围外）

- 真实 Anthropic SDK 调用（→ L2）
- 真机截图（→ L3）
- Claude 纯 type 分类可靠性（→ E-3 / L2 / L3）
- 截图 ref 进 trace（→ 独立 trace 切片）

---

## 9. Decisions

### D-1: `IScreenCapture` 作为 Core 设备 I/O 抽象（`IActionExecutor` 先例）
截图捕获是 Core 接缝（vision 策略 §5），与 `IActionExecutor` 同列。Core 持有抽象、host 提供设备实现。`IPageAnalyzer.AnalyzeCurrentPageAsync` 签名零改动（§12-B 已锁原则）。

### D-2: `PageAnalyzer` 三依赖
ctor 注入 `IModelProvider` + `IPromptLibrary` + `IScreenCapture`（截图来源是第三依赖）。范式从「两依赖」推广到「N 依赖」——子接口按需注入它要消费的基础设施，骨架不变。

### D-3: 第 0 步截图 + 调用步 `CompleteVisionAsync`（范式多模态扩展点）
骨架前插「截图」步，调用步从 `CompleteTextAsync` 换 `CompleteVisionAsync`。**范式零基础设施扩展**——`byte[] imageData` 本就是 `IModelProvider` 方法参数，`ObservingModelProvider` 已覆盖。范式对多模态传输天然支持，本切片证实。

### D-4: §12-A 剥散文落地 — `ElementTypeMapper` 派生 action + page/state change
prompt 删 type→action 散文映射，AI 只返 type；`expected_action` = `ElementTypeMapper.ToExpectedAction(type)`，`expects_page_change/state_change` 由 `ExpectedAction` 确定性派生（§6.1 表）。`ElementTypeMapper` 成为 type→action 单一真相源，消除 prompt↔code 散映射漂移。注意：剥的是「action 派生散文」，**保留 type 词表**（AI 分类需要）。

### D-5: `PageAnalysisDto` + 映射模式（TraversalAdvisor DTO idiom 推广到 vision）
反序列化用内部私有 DTO 宽松承载 prompt JSON，映射阶段调 `ElementTypeMapper` + 构造 Domain record（fail-fast）。推广自第 2 条切片的 `ContextDecisionResult` DTO 映射 idiom（D-141），处理更复杂形态（嵌套 MenuInfo/MenuItem/Coordinate）。

### D-6: NIE 边界（D-143 idiom 推广到 `IPageAnalyzer`）
`PageAnalyzer` 仅实现 `AnalyzeCurrentPageAsync`；`FindAppEntryAsync` / `VerifyPageTypeAsync` 抛 `NotImplementedException("…pending future slice.")`。一 capability 一切片纪律，从 `ITraversalAdvisor` 推广到 `IPageAnalyzer`。

### D-7: `Schemas.AnalyzeVisual` items 剥 action 字段（与 §12-A 对齐）
schema 常量的 items 只列 `name/type/coordinate/parent`，**不含 action 3 字段**，与剥散文后的 prompt 输出契约一致。type 不在 schema enum 硬约束（宽容 AI 返回，code 侧校验）。

### D-8: 子接口注入 `IModelProvider` 替代 `IModelRouter`（范式洁癖演进）
前两条切片子接口 ctor 注入 `IModelRouter`（路由+观测组装抽象）。review 反馈：路由属装配决策，业务子接口只调模型，不该碰路由抽象。本切片起子接口注入 `IModelProvider`（装配期 `router.Resolve(capability)` 产物，已套 `ObservingModelProvider`）。`IModelRouter` 降为**装配期工厂**——不再作为子接口运行时依赖，但观测组装的结构性保证保留（`router.Resolve` 仍统一套 decorator）；子接口 provider-agnostic 性质更纯（连路由都不依赖）。**前两条切片（TextUnderstanding/TraversalAdvisor）仍用旧范式，开 follow-up refactor change 统一**，不在本切片 scope 内回改。

---

## 10. Open Questions / 延迟决策

| # | 问题 | 延迟到 |
|---|---|---|
| OQ-1 | `IScreenCapture` 返回类型是否需扩含截图 ref（path/hash，§12-B 进 trace） | host 实现 / trace 集成切片（L1 取最简 `byte[]`） |
| OQ-2 | §12-A 剥散文后 Claude 纯 type 分类可靠性 | E-3 golden-screenshot / L2 / L3 |
| OQ-3 | `prompt` 大页 token 优化（手写 flattener / 裁剪） | 真机 L2/L3（L1 全量序列化） |
| OQ-4 | host 生产 prompt 注册点 / DI 组合根 | L2 host 落地（L1 测试 stub） |
| OQ-5 | `ExpectedAction.Action` 同时设 `ExpectsPageChange=true` 是否与 Python 行为一致 | L2 真实样本对照（L1 按 §12-A 文字约定） |
| OQ-6 | D-8 范式演进：前两条切片（TextUnderstanding/TraversalAdvisor）从 `IModelRouter` 统一到 `IModelProvider` | follow-up refactor change（不在本切片 scope） |

---

## 11. 切片边界

### 做
- `IScreenCapture` Core 接口（`Traversal/IScreenCapture.cs`）
- `PageAnalyzer : IPageAnalyzer`（`UniBrain/PageAnalyzer.cs`，ctor 注入 `IModelProvider`）+ `AnalyzeCurrentPageAsync` 真实实现 + 2 方法 NIE
- `PageAnalysisDto` 内部映射 + `ElementTypeMapper` 派生（§6）
- `Schemas.AnalyzeVisual` 常量
- `analyze_visual` prompt stub（测试注册，§7.3）
- 单元测试 + 端到端测试（§8）

### 不碰
- `UniBrainService`（纯组合容器，零改动——test 直接 `new UniBrainService(new PageAnalyzer(…), advisor, text)`）
- `IModelRouter` / `ModelRouter` / `ObservingModelProvider` / `IModelProvider` / `ModelRequest` 任一**签名**（范式零扩展；装配期仍消费 `router.Resolve` 注入 `IModelProvider`，但 router 类型本身不改）
- `AnthropicModelProvider.CompleteVisionAsync`（保持 stub）
- `IPageAnalyzer` 接口签名（零改动）
- Domain.Content 全部类型（只读消费）
- 真机 / ADB / trace 集成 / host 生产注册

---

## 12. 验证对照（OpenSpec spec SHALL 雏形）

切片完成应满足（供 propose 阶段细化）：

- **SHALL** 新建 `IScreenCapture`（`Traversal/`，`Task<byte[]> CaptureAsync(CancellationToken)`）
- **SHALL** 新建 `PageAnalyzer : IPageAnalyzer`，ctor 注入 **modelProvider** + promptLibrary + screenCapture（null → `DomainValidationException`）；`IModelProvider` 由装配期 `router.Resolve(AnalyzeVisual)` 产物注入
- **SHALL** `AnalyzeCurrentPageAsync` 走截图 → `CompleteVisionAsync` → DTO → `PageAnalysis` 链路
- **SHALL** item 的 `expected_action` / `expects_page_change` / `expects_state_change` 由 `ElementTypeMapper` 从 type 派生（§6.1 表），prompt 不含 type→action 散文
- **SHALL** `FindAppEntryAsync` / `VerifyPageTypeAsync` 抛 `NotImplementedException`（pending future slice）
- **SHALL NOT** 修改 `IPageAnalyzer` / `IModelProvider` / `IModelRouter` / `ModelRequest` / `ObservingModelProvider` / `UniBrainService` 任一签名
- **SHALL NOT** 在 `PageAnalyzer` 方法体内调 `IModelRouter.Resolve`（路由装配期完成，D-8）
- **SHALL NOT** 落地真实 Anthropic SDK / 真机截图（L1 范围外）
- **SHALL** 单测覆盖 §6.1 派生 4 分支 + ctor null + 模板缺失 + fail-fast + 截图透传 + NIE
- **SHALL** 端到端测试验证 `mode="vision"` 的 `AICallRecord` 观测闭环
- **SHALL** `dotnet build src/UniClaw.Core.sln` 0 错误 / `dotnet test` 全绿（含 ArchitectureGuard）
