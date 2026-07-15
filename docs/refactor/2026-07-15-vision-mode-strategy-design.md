# Vision Provider Mode A/B 策略设计 — 接口即接缝

> 状态: **Approved**（2026-07-15 定稿 — 架构决策全锁，见 §9 待决表 ✅ + §12 架构决议）
> 日期: 2026-07-15
> 来源: Mode A/B explore session
> 主题: 不在 A/B 之间二选一 —— `IVisionProvider` 作为 mode 接缝，两种实现可插拔；先建 Mode A 通真机，Mode B 作为可替换后备
> 关联: `docs/refactor/20-b-refactoring-roadmap-design.md`、`docs/refactor/2026-07-15-python-csharp-gap-triage.md`、CLAUDE.md「两种 AI 分析模式」

---

## 0. TL;DR

| 结论 | 说明 |
|------|------|
| **CLAUDE.md 的 A/B 闸门已被化解** | 原文「Phase 2 需先决定走哪种模式再设计上层架构」——上层架构已建好，且天然 mode-agnostic（消费 `IVisionProvider`，输出 `PageAnalysis`）。A/B 降级为「先实现哪个 provider」，不再是架构前置 |
| **`IVisionProvider` 即 mode 接缝** | A 与 B 的最终输出都是 `PageAnalysis`；mode 是具体实现的私有细节，接口零改动即可承载两种实现（Strategy 模式） |
| **Python 生产路径 = Mode A（已核实）** | `ClaudeVisionService` 用内联 `PROMPT_STRUCTURE` 让 Claude 直接产出 `PageAnalysis` JSON。Mode B 的 `analyze_visual.md` prompt + `FlattenedScreen` 是设计但从未接线的死件 |
| **Domain.Vision / ElementTypeMapper 不再是「死代码」** | 重新定位为「Mode B provider 的实现域」。Mode A provider 不用它无妨——它们是同一接口后的可替换实现 |
| **先建 Mode A** | 最快通真机、跟随 Python 已验证路径。把「真机含噪截图下 Claude 直接出 PageAnalysis 有多可靠」这个最大未知变成已知。Mode B 仅在 A 被证不可靠时启动，且不碰任何上层代码 |
| **设备 I/O 已有两个 Core 接缝** | `IActionExecutor`（动作执行）+ `IVisionProvider`（屏幕分析）均已存在。ADB 只需实现它们 + 提供截图捕获 |

---

## ⚠️ 外部依赖阻塞问题 (External Dependency Blockers)

> **代码架构外的依赖 — 不解决无法推进 Phase 3。集中记录便于发现。**
> 稳定 ID `E-1`~`E-4`,文档内外引用时用 ID。`grep -rn "E-[1-4]" docs/` 可定位全部引用点。
> 本节位于 §0 TL;DR 之后首位,确保打开文档即见。

| ID | 阻塞项 | 影响范围 | 解锁条件 |
|----|--------|---------|---------|
| **E-1** | 真机/模拟器 + ADB 可达 | 整个 Phase 3-A(截图捕获 + 动作执行都依赖设备) | 一台可达的 Android 设备/模拟器 + `adb` CLI |
| **E-2** | Claude API key + provider 配置 | 真实 vision 调用(Mode A/B 都需要) | API key + `src/config` 配置层(gap triage: 配置层全缺) |
| **E-3** | 真实截图样本 + expected-PageAnalysis 标注 | Mode A 可靠性度量(golden-screenshot 测试台)→ A/B 决策依据 | 一批真机截图 + 人工标注「正确 PageAnalysis」 |
| **E-4** | 目标 app 布局/领域知识 — 菜单分组约定 | Mode A prompt 调优 + Mode B 规则引擎 —— 两者共享的语义地基 | 确认目标 app 布局是否规整(顶栏=level2 / 左列=level1 / main=items) |

**为何单列**:线索 2(Mode A 可靠性度量)、线索 4(菜单分组约定)靠继续读代码讨论不动 —— 需要真机截图样本(E-3)和领域知识(E-4)。E-1/E-2 是 Phase 3-A 的硬前置。四项任一缺失,对应决策只能停在「设计就绪,待外部输入」,不得凭主观跳过。

**对应 explore 线索**:E-3 ← 线索 2;E-4 ← 线索 4。(线索 1、3 为纯架构取舍,已在 §12 解决。)

---

## 1. 背景：A/B 闸门

CLAUDE.md 记录两种 AI 分析模式：

| 模式 | 链路 | Domain.Vision 参与? | ElementTypeMapper 参与? |
|------|------|---------------------|------------------------|
| A（直接） | 截图 → 多模态 AI → PageAnalysis | ❌ | ❌ |
| B（两步） | 截图 → AI → FlattenedScreen → 规则/文本模型 → PageAnalysis | ✅ 核心链路第一步 | ✅ 规则引擎路径 |

原文约束：「Phase 2 需先决定走哪种模式再设计上层架构。」本设计核实的结论是：**该约束事实上已被满足——上层架构对 mode 无感**（见 §4）。

---

## 2. 证据：Python 生产路径 = Mode A（已核实）

### 2.1 真实调用链

`main:src/ai/vision_service.py` 中 `ClaudeVisionService.analyze_screenshot()`：

```
screenshot bytes ──PROMPT_STRUCTURE──▶ Claude ──JSON──▶ _parse_page_analysis ──▶ PageAnalysis
```

生产 prompt 是**内联常量 `PROMPT_STRUCTURE`**（vision_service.py:21），要求 Claude 直接返回 PageAnalysis 形态的 JSON：`level1_dir`、`level1_menus[]`、`level2_menus[]`、`current_path[]`、`items[]`（含 button type 分类）、popup 信息。AI 一次完成感知 + 语义分组 + 类型判定。

### 2.2 Mode B 是「设计但从未接线」的死件

| 死件 | 证据 |
|------|------|
| `analyze_visual.md`（返回 `elements[]` + `layout`，FlattenedScreen 形态） | 从未被 import；`vision_service.py` 用 `PROMPT_STRUCTURE`，不是它 |
| `FlattenedScreen` / `FlattenedElement` 模型 | `git grep` 全仓：仅在其自身模型定义文件内构造，**零运行时消费者**（生产 + 模拟都不用） |
| 类型→动作映射 | ElementTypeMapper 的确定性映射被**作为散文复制进 `PROMPT_STRUCTURE` 文本**（"menu_item→navigate, switch→toggle…"）。运行时映射活在 prompt 文本里，不在代码里 |

### 2.3 C# 镜像了同一个状态

| 项 | C# 现状（已核实） |
|----|------------------|
| `FlattenedScreen` 引用 | 仅模型自身文件 + README；Simulation 两个 mock **都不用**，直接从 fixture 构造 `PageAnalysis` |
| `ElementTypeMapper` 消费者 | 仅 `ElementTypeMapperTests`；**零生产消费者** |
| `IVisionProvider.AnalyzeCurrentPageAsync()` | 返回 `PageAnalysis?` —— **输出契约已是 mode-agnostic** |

---

## 3. 悖论：投资 vs 运行时死

C# 项目把 Domain.Vision + ElementTypeMapper 当架构支柱：

- CLAUDE.md 头条 P0 fix「两级映射分离」—— TypeHint（视觉）vs 行为映射，ElementTypeMapper 为「核心桥」
- 8 个锁定的 Domain.Vision 类型 + ElementTypeMapper（与 Python row-for-row 对齐）

但在 Mode A 下，这些**全部运行时死**（如 §2.3 核实）。这正是 A/B 决策的张力来源。

---

## 4. 化解：`IVisionProvider` 即 mode 接缝（Strategy）

```
                         IVisionProvider  (Core — 不变)
                         AnalyzeCurrentPageAsync() → PageAnalysis?
                                      ▲
                ┌─────────────────────┴─────────────────────┐
       (impl A — 先做)                          (impl B — 可替换, 后做)
       ClaudeVisionProvider                     RuleBasedVisionProvider
       ┌────────────────────┐                   ┌────────────────────────────┐
       │ _screenCapture (截图)│                   │ _screenCapture (截图)       │  ┐ host 项目
       │ _claude (AI 调用)   │                   │ _claude (AI 调用)           │  │ (Core 之外)
       │ PROMPT_STRUCTURE    │                   │   ↓ FlattenedScreen (感知)  │  ┘
       │ → PageAnalysis      │                   │ PageAnalysisBuilder ────────┤  ┐ Core (纯函数)
       │ (AI 一次做完)        │                   │   用 ElementTypeMapper       │ │ Domain.Vision
       └────────────────────┘                   │   → PageAnalysis            │ ┘ 在此激活
                                               └────────────────────────────┘
       sim: StatefulMockVisionService / ScrollableMockVisionService（现状，也是 impl）
```

TraversalEngine / FSM / StepOrchestrator 全部消费 `IVisionProvider`，**对 A/B 完全无感**。决定从「必须先选 A 还是 B」降级为「先实现哪个 provider」。

### 4.1 Domain.Vision 的重新定位

> Domain.Vision / ElementTypeMapper **不是死代码，是 Mode B provider 的实现域**。Mode A provider 不用它无妨——它们是同一接口后的可替换实现。ElementTypeMapper 等着它的 `RuleBasedVisionProvider` 来激活。

比「为未来预留的设计资产」更干净：它是一个具体 provider 的依赖，只是那个 provider 还没写。

---

## 5. 设备 I/O 已有两个 Core 接缝

Phase 3 真机链路看似全缺（gap triage B 桶），但**动作执行和屏幕分析的 Core 接缝都已存在**：

| Core 接口 | 位置 | 方法 | 真机实现 |
|-----------|------|------|---------|
| `IActionExecutor` | [IGraphTraversalEngine.cs:49](../../src/UniClaw.Core/Traversal/IGraphTraversalEngine.cs) | `TapAsync` / `SwipeAsync` / `PressBackAsync` / `InputTextAsync` / `LongPressAsync` | `AdbActionExecutor : IActionExecutor` |
| `IVisionProvider` | [StepContext.cs:14](../../src/UniClaw.Core/StateMachine/StepContext.cs) | `AnalyzeCurrentPageAsync` / `FindAppEntryAsync` + 滚动感知 4 方法 | `ClaudeVisionProvider : IVisionProvider`（A）或 `RuleBasedVisionProvider`（B） |

**唯一真正缺失的接缝：截图捕获**。`AnalyzeCurrentPageAsync()` 当前无参——隐含「provider 自己看当前屏幕」。sim 里 mock 持有状态；真机上 provider 需要先拿到截图。

### 5.1 截图捕获归属决策

两种放法：

| 方案 | 形态 | 代价 |
|------|------|------|
| **A. 组合进 provider** ✅已定（§12-B） | `ClaudeVisionProvider` 内部依赖一个 `IScreenCapture` / `IAdbClient`，调用时先截图再分析。`IVisionProvider` 签名**零改动** | provider 持有设备依赖；但接口与所有上层代码不动 |
| B. 引擎截图后传 bytes | 引擎调 `IScreenCapture.Capture()`，把 bytes 传给 `IVisionProvider.AnalyzeAsync(byte[])` | 改接口签名 + 引擎职责膨胀（引擎要知道何时截图） |

**✅ 已定方案 A（§12-B / Q-1）**：与现有「provider 拥有屏幕访问权」语义一致，接口零改动，sim/真机差异完全封装在 provider 内。`IScreenCapture`（截图）与 `IActionExecutor`（动作）共享同一 ADB 后端连接，但作为独立关注点。

---

## 6. 设计接缝（均已在 §9 / §12 锁定）

以下 3 点在 explore 中逐一敲定，结论见各节 ✅ 指针（汇总于 §9 待决表）：

### 6.1 Mode B 的规则大脑放 Core 还是 host？

`FlattenedScreen → PageAnalysis` 转换是**纯函数**（无 I/O、无 AI、用 ElementTypeMapper + Domain.Vision）。

| 归属 | 优点 | 代价 |
|------|------|------|
| **host**（推荐先放） | Core 保持纯粹、不知具体 app | 规则随 host 走，不复用 |
| Core（Domain.Mappings / Domain.Service） | 纯、可单测、激活 ElementTypeMapper | Core 会编码 app-specific 布局假设（顶栏=level2 / 左列=level1…），Core 变得「知道目标 app 长什么样」 |

**✅ 已定（§12-C / Q-2）**：先放 host，等规则稳定、证明是通用领域知识后再考虑下沉 Core。避免 Core 过早编码 app 假设。

### 6.2 Mode B 是否暴露内部感知接缝？

Mode B 内部 = 感知（AI → FlattenedScreen）+ 规则（FlattenedScreen → PageAnalysis）。是否要把「AI 感知」独立成 `IFlattenedScreenSource` 接口？

- **最小版**：不独立。感知调用是 `RuleBasedVisionProvider` 的私有细节
- **独立版**：`IFlattenedScreenSource`（AI）+ `IPageAnalysisBuilder`（规则）分别可测/可换

**✅ defer（§9 Q-3）**：现在不定。写 Mode B 时，仅当规则引擎需独立单测、或感知源要换（不同 AI/不同 prompt）时再拆。

### 6.3 具体 provider 的项目归属（见 §7）

---

## 7. 项目归属：新建 host 项目

`ClaudeVisionProvider` / `RuleBasedVisionProvider` / `AdbActionExecutor` / `IScreenCapture` 实现都依赖 Claude SDK / ADB 二进制 —— **不该进 `UniClaw.Core`**（CLAUDE.md 把 Core 定位成纯 Domain + engine）。

```
src/
  UniClaw.Core/                 ← 接口 + 纯模型/规则
                                   IVisionProvider, IActionExecutor, IScreenCapture  (Core 接口)
                                   Domain.Vision, ElementTypeMapper (类型→动作唯一真相源)
  UniClaw.Device/        (新建)  ← ADB 后端
                                   • AdbClient
                                   • AdbActionExecutor : IActionExecutor
                                   • AdbScreenCapture  : IScreenCapture
  UniClaw.ClaudeVision/  (新建)  ← Claude 后端
                                   • ClaudeVisionProvider : IVisionProvider (Mode A)
                                   • RuleBasedVisionProvider (Mode B, 条件性) + 规则大脑 (host, §12-C)
                                   • 组合 IScreenCapture (注入) + IClaudeClient
                                   • PROMPT_STRUCTURE 移植 (AI 只返回 type, §12-A)

app root (composition):  AdbScreenCapture ──注入──▶ ClaudeVisionProvider ──▶ 引擎
                         两 host 项目互不引用, 都只依赖 Core 接口
```

与 gap triage A-10（analysis/dashboard 不进 Core）同一分层原则：Core 不依赖外部 SDK / 设备二进制。

> ✅ **Q-4 已定（2026-07-15）：分离**。`UniClaw.Device`（ADB）+ `UniClaw.ClaudeVision`（Claude）两个 host 项目，按外部依赖/关注点切分；都只依赖 Core 接口、在 app root 装配、互不直接引用。`IScreenCapture` 作为 Core 接口（同 `IActionExecutor`），使两个 host 不互依。

---

## 8. 构建顺序

```
Phase 3-A: Mode A 通真机（跟随 Python 已验证路径）
  1. 新建 host 项目 (UniClaw.Device + UniClaw.ClaudeVision, 或先合一)
  2. IAdbClient / IScreenCapture: 截图捕获 + 动作执行 (实现 IActionExecutor)
  3. ClaudeVisionProvider : IVisionProvider
       └─ 移植 PROMPT_STRUCTURE → Claude → PageAnalysis
  4. 真机回归基线: 含噪截图下 Claude 直接出 PageAnalysis 的可靠性数据
       └─ 这是最关键产出: 把最大未知变成已知

Phase 3-B: 仅当 A 可靠性不足时启动（接口是接缝, 不碰上层）
  5. RuleBasedVisionProvider : IVisionProvider
       ├─ Claude → FlattenedScreen (感知)
       └─ PageAnalysisBuilder: FlattenedScreen → PageAnalysis (规则, 先 spike 难度)
            └─ 激活 Domain.Vision + ElementTypeMapper
  6. 规则大脑稳定后, 评估是否下沉 Core
```

**关键**：先建 Mode A，让「真机含噪截图下 Claude 行不行」这个最大未知落地。Mode B 仅在 A 被证不可靠时启动——而且因为接口是接缝，启动 B 不改任何上层代码、不改任何 sim 测试。

---

## 9. 待决设计取舍（需用户拍板）

| # | 问题 | 状态 / 推荐 |
|---|------|------|
| Q-1 | 截图捕获归属：组合进 provider vs 引擎传 bytes | ✅ **已解决**（§12-B）：组合进 host provider；Core 接口零改动，sim 不受影响；截图以 ref 入 trace |
| Q-2 | Mode B 规则大脑：Core vs host | ✅ **已解决（§12-C）**：先 host。Core 定位为「映射基础设施」(CLAUDE.md)，app-specific 布局启发式不进纯 Core；ElementTypeMapper(general) 已在 Core 且 Mode A 即激活，转换器的额外部分(spatial grouping + app 解释)属 host。稳定后可抽通用机制下沉 |
| Q-3 | Mode B 内部是否独立 `IFlattenedScreenSource` | ✅ **defer**：写 Mode B 时按需拆，现在不占决策带宽 |
| Q-4 | host 项目粒度：ADB+Claude 合一 vs 分离 | ✅ **已解决（§7）**：分离 —— `UniClaw.Device`(ADB) + `UniClaw.ClaudeVision`(Claude)，两 host 互不引用、都依赖 Core 接口；`IScreenCapture` 作为 Core 接口(同 `IActionExecutor`) |
| Q-5 | Mode A 是否需要 `IAIStrategyAdvisor` 的 noop | ✅ **非问题**：引擎/运行时**不消费** `IAIStrategyAdvisor`（已核实 Traversal/StateMachine 零引用）。advisor 属独立 Phase 3+ AI 栈，不进 Phase 3-A 关键路径 |
| Q-6 | 类型→动作映射的单一真相源 | ✅ **已解决**（§12-A）：ElementTypeMapper 为唯一源；prompt 删「Expected Actions」散文，AI 只返回 type，code 派生 action |

---

## 10. CLAUDE.md 待修订项

本设计核心结论与 CLAUDE.md 现有措辞冲突，建议修订（待用户确认后执行）：

| 位置 | 现状 | 建议改为 |
|------|------|---------|
| 「两种 AI 分析模式」表下 | 「Phase 2 需先决定走哪种模式再设计上层架构」 | 「上层架构已 mode-agnostic（消费 `IVisionProvider`，输出 `PageAnalysis`）；接口即接缝，两种 mode 为可插拔实现。先建 Mode A 通真机，Mode B 为可替换后备」 |
| ElementTypeMapper 定位 | 「核心桥」 | 补充：运行时仅在 Mode B provider 激活；Mode A 下为测试资产 |
| Domain.Vision 完成状态 | （未提运行时消费者） | 补充：当前零运行时消费者（sim mock 直接构造 PageAnalysis）；为 Mode B provider 实现域 |

---

## 11. 不在本设计范围

- ADB 协议细节、Claude SDK 接入、API key 配置、SafetyFilter —— 属 Phase 3 实施细节，立项时再定
- `IAIStrategyAdvisor` 全栈（providers/UniBrain/prompts/cache/task parser）—— gap triage B 桶，独立推进
- Mode B 规则引擎的具体规则 —— 待 spike
- EntryPolicyExecutor 真实执行（deeplink/cold launch）—— 依赖 ADB 动作执行，随 Phase 3-A 一起

---

## 12. 架构决议（explore 线索 1、3 已解决）

> 两条纯架构取舍，在 explore 中讨论到结论。对应 §9 的 Q-1（→ §12-B）与 Q-6（→ §12-A）。
> 外部依赖阻塞（线索 2、4）不在本节，见文首「⚠️ 外部依赖阻塞问题」E-3、E-4。

### §12-A 类型→动作映射：单一真相源 = ElementTypeMapper（线索 1，Q-6）

**问题**：Mode A 下 Python `PROMPT_STRUCTURE` 把 ElementTypeMapper 的 type→action 映射**作为散文复制**（"menu_item→navigate, switch→toggle…"），改一处另一处漂移，无机制约束。

**核实**：ElementTypeMapper 已暴露所需公开方法 ——
[`ToMenuItemType(string)`](../../src/UniClaw.Core/Domain/Mappings/ElementTypeMapper.cs#L136)、
[`ToExpectedAction(string)`](../../src/UniClaw.Core/Domain/Mappings/ElementTypeMapper.cs#L144)，
且 `TypeToExpectedActionMap`（`["menu_item"]=Navigate`、`["switch"]=Toggle`…）正是 prompt 散文复制的那份。

**决议**：

```
Mode A 重构后的 prompt (移植 PROMPT_STRUCTURE 时改):
  AI 只返回每个元素的 type (中间字符串词表: menu_item/switch/button/...)
        │  (删掉 prompt 里整段「Expected Actions」散文 + expects_page_change/state_change 指示)
        ▼
  ElementTypeMapper.ToMenuItemType(type)   ← 已存在
  ElementTypeMapper.ToExpectedAction(type) ← 已存在
        ▼
  MenuItemType + ExpectedAction
  (+ expects_page_change/state_change 由 ExpectedAction 确定性派生:
     Navigate/Action → page_change; Toggle → state_change)
```

**效果**：
- 映射单一源 = ElementTypeMapper，零漂移可能
- ElementTypeMapper **在 Mode A 下即成为运行时消费者** —— §3 的「死代码悖论」进一步消解（不再是「等 Mode B 才激活」）
- AI 少做一件事（不再推导动作语义，只做视觉分类 —— 它擅长的），prompt 更简单
- Mode B 最实质的论据（映射在代码里）提前在 Mode A 兑现一半

**待验证（→ E-3）**：Claude 对「纯 type 分类」的可靠性需 golden-screenshot 测。但这本就是 Phase 3-A 要测的，非额外未知。

---

### §12-B 截图作为 trace artifact：捕获留 host provider，Core 纯净（线索 3，Q-1）

**问题**：trace 系统纯文本（ExecutionRecord/StateTransition/ErrorRecord/PageTransition/AICallRecord，零 image/byte 字段，已核实）。真机调试时截图是最有价值的 artifact，但「截图在哪层捕获」(Q-1) 与「要不要进 trace」纠缠。

**两条原则**：

1. **截图是 trace 一等公民，但以引用存**（文件路径 / hash），图片单独存文件 —— **不嵌 base64 进 trace 记录**（否则 C-7 JSONL 爆炸 + trace 记录膨胀）。`AICallRecord` 关联一个 `ScreenshotRef`。

2. **捕获在 host provider 内**（`ClaudeVisionProvider` 组合 `IScreenCapture`），**Core 的 `IVisionProvider` 签名不动**。理由：sim 的 mock 没有截图，强制 bytes 穿过 Core 接口会污染 sim。

```
Core (纯, 不变)                       Host (新项目)
┌────────────────────────┐            ┌────────────────────────────────────┐
│ IVisionProvider         │◀───────────│ ClaudeVisionProvider               │
│   AnalyzeCurrentPage()  │            │  ├ IScreenCapture (截图 → 存文件)   │
│ ITraceRecorder          │◀──────┐    │  ├ IClaudeClient  (AI 调用)         │
│   RecordAICallAsync()   │       │    │  └ RecordAICallAsync(screenshotRef) │
└────────────────────────┘       │    └────────────────────────────────────┘
                                  └──→ 截图 ref 回流 trace, 不经 Core 接口
sim: StatefulMockVision (不动) ───────── mock 不实现 IScreenCapture, 不受影响
```

**Q-1 定案**：组合进 host provider。这修正了 explore 中一度倾向的「引擎持有 bytes」—— **sim 纯净性优先**；trace 便利性由 host provider 自己记录 screenshot ref 实现，不需要引擎持有 bytes。

**proposal-time 细节（不在此锁）**：截图 ref 如何进 trace —— 扩 `AICallRecord` 加可选 `ScreenshotRef` 字段，还是平行加 `ScreenshotRecord`（ correlated by span id）。原则已定：**ref 非 bytes、host 记录、Core 至多加一个可选字符串字段、Observability 不持有 image 数据**。

---

### §12-C Mode B 规则大脑：先 host（线索 / Q-2）

**问题**：`FlattenedScreen → PageAnalysis` 转换器（Mode B 的语义大脑）放 Core 还是 host？

**决议：先 host**（`UniClaw.ClaudeVision` 的 Mode B provider 内）。决定性理由 —— Core 的定位。

转换器内部分两层：

```
转换器 = 通用机制 + app-specific 解释
  ├ 通用: type→action          (ElementTypeMapper, 已在 Core, Mode A 即激活 ✓)
  ├ 通用: 空间分组 (bbox 聚类)、弹窗检测 (modal 覆盖启发式)
  └ app-specific: 哪个聚类是 level1 / level2 / items   ← 硬骨头, 属「解释」非「基础设施」
```

CLAUDE.md 把 Core 定位为「映射**基础设施**」—— ElementTypeMapper 对齐的是通用 `ANDROID_CLASS_MAP`，不是某个 app 的布局知识。最后那层「顶栏=level2、左列=level1」是对**目标 app 的解释**，放进 Core 会让 Core「知道具体 app 长什么样」，违反 infrastructure-vs-app 边界。

且 ElementTypeMapper（真正通用的部分）已在 Core 且 Mode A 即激活 —— 转换器进 host 不会让它白费，host 转换器照样调用它。

**演化路径**：转换器先 host；若其空间分组/弹窗检测机制证明通用（跨 app 或跨 A/B 校验复用），再抽机制下沉 Core、host 供 app 布局配置（Core mechanism + host config）。YAGNI —— Mode B 本身条件性，不过早做这层拆分。

**Domain.Vision 命运**：Mode B provider（host）消费 `FlattenedScreen`/`FlattenedElement`/`TypeHint`，Domain.Vision 在 Mode B 激活（仍是「Mode B provider 实现域」，§4.1）。
