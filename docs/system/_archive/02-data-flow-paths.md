# Domain 数据流路径

> **日期**: 2026-07-02
> **分支**: `feature/refactor`（P0 fix 后）

---

## 1. 两种 AI 分析模式

Python 的 AI 层有 **两种截图→PageAnalysis 的路径**：

### 模式 A — 直接模式（单步）

多模态模型一次调用，直接输出 `PageAnalysis`。**不走 FlattenedScreen**。

```
┌──────────────────────────────────────────────────────────────────────┐
│  AI 多模态模型                                                        │
│  输入: Android 截图 (PNG bytes)                                       │
│  Prompt: "分析截图，输出菜单结构+所有可交互项+类型+预期行为"            │
│  输出: PageAnalysis JSON                                              │
│    { level1_dir: "left", items: [{type:"toggle", expected_action:"toggle"}] } │
└────────────────────────────┬─────────────────────────────────────────┘
                             │
                             │  JSON 反序列化
                             │  Direction.FromValue("left") → Left
                             │  MenuItemType.FromValue("toggle") → Toggle
                             │  ExpectedAction.FromValue("toggle") → Toggle
                             │
                             ▼
┌──────────────────────────────────────────────────────────────────────┐
│  PageAnalysis (Domain.Content)                                        │
│    items: [MenuItem(name="WiFi", type=Toggle,                        │
│      expected_action=Toggle, coordinate=Coordinate(0.85, 0.35))]     │
│    level1_dir: Left, level1_menus: [...]                             │
└────────────────────────────┬─────────────────────────────────────────┘
                             │
                             │  ③ 操作构造
                             │  Operation(Action=Click, Target(Text, "WiFi"))
                             │
                             ▼
                      TraversalNode (Phase 2)
```

**Python 实现**: `VisionService.analyze_screenshot(image_data) → PageAnalysis`
- AI 模型直接输出 menu 结构 + items with type/expected_action
- **FlattenedScreen 不参与**——模型不输出单个元素的 bbox/type_hint/selection_state
- Domain.Vision 类型（TypeHint/BoundingBox/FlattenedElement）**不在此链路中**

**C# 现状**: `IAIStrategyAdvisor` 接收 `AI.PageAnalysis`（简化版），不接收 `FlattenedScreen`

### 模式 B — 两步模式（视觉+文本）

第一步：多模态模型输出视觉元素 → `FlattenedScreen`。
第二步：文本模型或规则引擎将 `FlattenedScreen` 转换为 `PageAnalysis`。

```
┌──────────────────────────────────────────────────────────────────────┐
│  Step 1: 多模态模型 → 视觉识别                                        │
│  输入: Android 截图                                                   │
│  Prompt: "识别屏幕上所有 UI 元素的类型/位置/状态"                      │
│  输出: FlattenedScreen JSON                                           │
│    { elements: [{type_hint:"button", bbox:{...}, selection_state:"selected"}] } │
└────────────────────────────┬─────────────────────────────────────────┘
                             │
                             │  ① 视觉识别
                             │  TypeHint.FromString("button") → Button
                             │  SelectionState.FromString("selected") → Selected
                             │  BoundingBox(0.1, 0.2, 0.3, 0.05)
                             │
                             ▼
┌──────────────────────────────────────────────────────────────────────┐
│  FlattenedScreen (Domain.Vision)                                      │
│    elements: [FlattenedElement(id=5, text="WiFi",                    │
│      type_hint=Button, bbox=..., region="left_panel")]               │
│    screen_hints: ScreenHints(regions=[Region(id="left_panel",...)])  │
└────────────────────────────┬─────────────────────────────────────────┘
                             │
                             │  ② 行为语义推断（文本模型 或 规则映射）
                             │  路径 A (规则): ElementTypeMapper.MapAndroidClass → "toggle"
                             │             ToMenuItemType("toggle") → MenuItemType.Toggle
                             │  路径 B (文本模型): FlattenedScreen JSON → 文本模型 → PageAnalysis JSON
                             │
                             ▼
┌──────────────────────────────────────────────────────────────────────┐
│  PageAnalysis (Domain.Content)                                        │
│    items: [MenuItem(name="WiFi", type=Toggle,                        │
│      expected_action=Toggle, coordinate=Coordinate(0.85, 0.35))]     │
└────────────────────────────┬─────────────────────────────────────────┘
                             │
                             │  ③ 操作构造
                             │  Operation(Action=Click, Target(Text, "WiFi"))
                             │
                             ▼
                      TraversalNode (Phase 2)
```

**Python 架构**: `vision/__init__.py` 注释明确 "PRD V5.2 two-step visual pipeline"
- Step 1: 多模态模型 → FlattenedScreen（视觉识别）
- Step 2: 文本模型或 ElementTypeMapper 规则 → PageAnalysis（行为推断）
- **FlattenedScreen 是中间产物**——它承载视觉信息，但不直接驱动遍历

**C# 现状**: `IAIStrategyAdvisor` 有一个方法接收 `FlattenedScreen` 参数：
```csharp
// AI.PageAnalysis (简化版) 包含 FlattenedScreen
public sealed record class PageAnalysis(
    FlattenedScreen FlattenedScreen,
    List<string> Path,
    PopupInfo? PopupInfo = null);
```
这意味着 C# AI 层的 PageAnalysis **同时持有 FlattenedScreen 和 Path**——是两步模式的简化版。

---

## 2. 两种模式对比

| 维度 | 模式 A (直接) | 模式 B (两步) |
|------|---------------|---------------|
| **AI 调用次数** | 1 次 | 2 次（或 1 次 + 规则引擎） |
| **FlattenedScreen 参与** | ❌ 不参与 | ✅ 是中间产物 |
| **Domain.Vision 类型参与** | ❌ 不参与 | ✅ TypeHint/BoundingBox/SelectionState |
| **ElementTypeMapper 参与** | ❌ 不参与（AI 直接输出 MenuItemType） | ✅ 规则引擎路径需要 |
| **准确性** | 依赖 AI 模型一次性正确输出 type/expected_action | 视觉识别+行为推断分开，更可控 |
| **延迟** | 低（1 次 API 调用） | 高（2 次 API 调用 或 1 次+本地规则） |
| **适用场景** | AI 模型足够准确时 | 需要视觉+行为分层控制时 |

---

## 3. Domain 类型在两种模式中的角色

### 模式 A 中 Domain 的角色

| Domain 类型 | 是否参与 | 作用 |
|-------------|---------|------|
| Direction / Coordinate | ✅ | PageAnalysis 反序列化 |
| MenuItemType / ExpectedAction | ✅ | MenuItem 反序列化（AI 直接输出 string→enum） |
| TypeHint / BoundingBox / FlattenedElement / FlattenedScreen | ❌ | **不参与** |
| Operation / Target | ✅ | Step ③ 操作构造 |
| ElementTypeMapper | ❌ | **不参与**（AI 已完成行为推断） |

### 模式 B 中 Domain 的角色

| Domain 类型 | 是否参与 | 作用 |
|-------------|---------|------|
| TypeHint / BoundingBox / FlattenedElement / FlattenedScreen | ✅ | Step ① 视觉识别 |
| SelectionState | ✅ | Step ① 视觉识别 |
| ElementTypeMapper | ✅ | Step ② 行为语义推断（规则引擎路径） |
| MenuItemType / ExpectedAction | ✅ | Step ② 行为语义推断输出 |
| Direction / Coordinate | ✅ | PageAnalysis 组装 |
| Operation / Target | ✅ | Step ③ 操作构造 |

**关键洞察**：Domain.Vision 类型只在模式 B 中有价值。如果只用模式 A（直接模式），7 个 Vision 类型全部不在核心链路中。

---

## 4. 当前 C# 的双版本冲突

C# 同时存在两个 `PageAnalysis`：

| 版本 | 位置 | 字段 | 依赖 |
|------|------|------|------|
| **Domain 版** | `Domain.Content.PageAnalysis` | 12 字段（完整版） | Direction, Coordinate, MenuInfo, MenuItem, PopupInfo |
| **AI 简化版** | `AI.PageAnalysis` | 3 字段（FlattenedScreen + Path + PopupInfo） | FlattenedScreen (Vision!) |

**冲突本质**：两个 PageAnalysis 代表两种模式的输出：
- Domain 版 → 模式 A 的输出（完整菜单结构，无 FlattenedScreen）
- AI 化版 → 模式 B 的中间产物（FlattenedScreen + 有限信息）

**Phase 2 决策**：删除 AI 简化版，统一用 Domain 版。但需要决定——统一走模式 A 还是模式 B？
- 如果走 **模式 A**：FlattenedScreen 仅用于安全检测/调试日志，不驱动遍历逻辑
- 如果走 **模式 B**：FlattenedScreen 是核心链路的中间产物，ElementTypeMapper 是关键桥

---

## 5. 映射链路详图：ToggleButton (模式 B 规则引擎路径)

```
Android 控件类名: "ToggleButton"
    │
    │  MapAndroidClass("ToggleButton")
    │  精确匹配 → "toggle"
    │
    ▼
中间字符串: "toggle"
    │
    ├─→ ToMenuItemType("toggle") → MenuItemType.Toggle    ← 行为: 独立 Toggle 类型
    ├─→ ToExpectedAction("toggle") → ExpectedAction.Toggle ← 行为: 状态切换操作
    ├─→ ToTypeHint("toggle") → TypeHint.Switch             ← 视觉: 看起来像开关（可选）
    │
    ▼
行为语义层输出: MenuItemType.Toggle + ExpectedAction.Toggle
视觉外观层输出: TypeHint.Switch (可选，非核心链路)
```

**对比 Switch 的链路**（展示两级映射的必要性）：

```
"Switch" → MapAndroidClass → "switch"
    ├─→ ToMenuItemType("switch") → MenuItemType.Switch      ← Switch ≠ Toggle
    ├─→ ToExpectedAction("switch") → ExpectedAction.Toggle   ← 同 Toggle (都改变状态)
    ├─→ ToTypeHint("switch") → TypeHint.Switch               ← 同 Toggle 视觉外观
```

**关键差异**：`"toggle"` 和 `"switch"` 在视觉外观层相同（Switch），在行为语义层不同（Toggle vs Switch）。这是两级映射分离的核心价值——单级映射（直接返回 TypeHint）无法区分这两种行为。

---

## 6. 校验链路

数据在每个转换点的校验行为：

```
AI JSON string ──→ TypeHint.FromString("scrollable")
                      │ AliasMap.TryGetValue("scrollable") → 未命中
                      │ 回落 → TypeHint.Text
                      │ IsValid("scrollable") → false ← 上层感知异常输入

AI JSON string ──→ SelectionState.FromString("activated")
                      │ DisabledAliases.Contains("activated") → false
                      │ SelectedAliases.Contains("activated") → false
                      │ 回落 → SelectionState.Normal
                      │ IsValid("activated") → false ← 上层感知异常输入

Android 类名 null ──→ MapAndroidClass(null)
                      │ throw DomainValidationException("className", null)
                      ← fail-fast，不构造非法对象

Confidence 1.5 ──→ new FlattenedElement(Confidence=1.5)
                      │ throw DomainValidationException("Confidence", 1.5)
                      ← fail-fast

Coordinate(-0.1, 0.5) ──→ new Coordinate(X=-0.1, Y=0.5)
                      │ throw DomainValidationException("X", -0.1)
                      ← fail-fast
```

**校验链路原则**：
- **输入边界校验**（FromString, MapAndroidClass）→ 回落或 IsValid 通知上层，不抛异常
- **构造期校验**（Coordinate, BoundingBox, Operation）→ DomainValidationException fail-fast
- **上层消费校验** → 用 IsValid(string) 前置验证，决定是否信任 AI 输出

---

## 7. Phase 2 预期新增链路

| 链路 | 起点 | 终点 | 模式 | 说明 |
|------|------|------|------|------|
| FlattenedScreen → DynamicRule 匹配 | Vision | Graph | B | MatchCondition.Type 使用中间字符串 |
| PageAnalysis → TraversalNode 生成 | Content | Graph | A/B | 页面结构驱动节点创建 |
| FlattenedScreen → 安全检测 | Vision | AI | B | ScreenSafetyScreening 接收 FlattenedScreen |
| Operation → Trace 记录 | Common | Observability | A/B | 操作执行被记录为 trace span |
| Template dict → TraversalNode 实例化 | Graph | Graph | A/B | 占位符解析后 dict→record 转换 |
