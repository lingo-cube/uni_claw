# Semantic Architecture Gap Review (Revised)

> Generated: 2026-08-10
> Revised: 2026-08-10 — Architecture Review Revision
> Purpose: 回答"当前系统距离真实 AI Agent UI 自动化，还缺哪些关键语义能力？"
> Scope: 只生成文档，不修改代码、不创建 OpenSpec、不实现能力
> Inputs: CP-06/CP-12/CP-14/U2/U3 结果 · Reality Assets (A3/A4/B1) · Page Definition Challenge · Container Runtime Layer · Agent open-world traversal · Architecture Review Feedback

---

## 1. Current Capability Baseline

| Capability | Status | Evidence | Limitation |
|---|---|---|---|
| **IntentSemanticEnvelope** | IMPLEMENTED | `Planning/IntentSemanticEnvelope.cs` — ClosedWorldConcrete + OpenWorldTypeLevel 两模式 | 编译发生在调用侧；Runtime 不解包 intent，不解码 NL |
| **TypeLevelTraversalSpecification** | IMPLEMENTED | Scope + TargetCategories + MaximumDepth + Safety + Completion + Entry | 不含 dispatch 行为（DispatchPolicy 是后来加的） |
| **TypeLevelDispatchPolicy** | IMPLEMENTED | `TypeLevelDispatchPolicy` — caller-authorized Category→Handling 映射 | 调用侧必须预知所有 category；Agent 不制造 handling rules |
| **OpenWorld Traversal** | IMPLEMENTED | `Agent.RunOpenWorldAsync` — BranchInventory → dispatch → child container → parent return | 3 个 multi-page proof 未通过（fixture 问题）；parent-return 假设 1:1 screen↔page↔text |
| **Action-Scoped Grounding** | IMPLEMENTED | `TargetGroundingCriterion` 支持 Tap + SetSwitch | 调用侧提供 per-action criterion；criterion 不感知多步 Goal |
| **GoalEvidence Completion** | IMPLEMENTED | I-10 — 只有 GoalEvidence 触发 Completed；CP-06 证明 plan-length-independent | Goal 是调用侧注入的 predicate，不含世界知识 |
| **Reality-Seeded Testing** | IMPLEMENTED | L2-R 仿真：EP-04 真实元素数据 + A4 深度结构 + B1 真实设备标签 | 缺失：OFF→ON state-change pair（SYNTHETIC）、Popup、Recovery、Drift、真实 Scroll |

**总体评估：Runtime 具备完整的 closed-world concrete plan 执行能力和基础的 open-world type-directed dispatch。但所有"语义"（page 是什么、element 是什么、如何 grounding、何时完成）都由调用侧注入——Runtime 本身是一个 administrative shell。**

---

## 2. Core Semantic Model (Corrected)

### 2.1 原模型（已废弃）

```text
Element Semantics
        ↓
Page Semantics          ← 线性依赖：先定义 Element，再定义 Page
        ↓
Belief
        ↓
Continuity
```

**问题：** 这个模型假设 Element 和 Page 是上下级关系——先有元素语义，再有页面语义。实际上二者是同一 Observation 的不同语义投影，不存在线性依赖。

### 2.2 修正后模型

```text
Raw Observation
        |
        |
        +----------------+
        |                |
        v                v
 Element Evidence    Page Evidence
        |                |
        +-------+--------+
                |
                v
        Semantic Belief
                |
                v
     Action / World Understanding
```

**核心观点：**

1. **Element 和 Page 不是上下级关系。** 二者都是 Observation 的不同语义投影——Element Evidence 回答"这个元素是什么"，Page Evidence 回答"这个屏幕是什么页面"。
2. **二者并行汇聚到 Semantic Belief。** Belief 不是 Page 的下属，而是 Element Evidence + Page Evidence 的融合产物。
3. **必须删除"先定义 Element，再定义 Page"的线性依赖。** 这是原模型最大的错误假设。

**明确区分：**

| 物理层（Runtime 已有） | 语义层（Runtime 缺失） |
|---|---|
| `ObservedElement`（Text + SwitchState + Index） | `SemanticElement`（有身份、能力、状态的交互对象） |
| `Observation`（元素列表 snapshot） | `Page`（有语义身份、上下文、连续性的屏幕状态） |
| `WorldBelief.Confidence`（0/1 二值） | `Belief`（多源证据融合后的分级认知） |

---

## 3. SEMANTIC_EVIDENCE_MODEL_GAP (P0)

### 3.1 定义

当前 Runtime 缺少从 **Raw Observation** 到 **Semantic Evidence** 的中间层。

Runtime 拥有 administrative shell（生命周期、新鲜度、所有权、升级），但 100% 的语义判断委托给调用侧注入的 lambda。没有独立证据通道，没有证据融合，没有证据→信念的转换。

**原两个独立 Gap 合并升级：**
- ~~ELEMENT_SEMANTIC_GAP~~（原 P0）
- ~~PAGE_SEMANTIC_GAP~~（原 P0）
- → **SEMANTIC_EVIDENCE_MODEL_GAP**（P0）

合并理由：Element 和 Page 不是线性依赖关系，二者都是 Observation 的并行语义投影。把它们拆成两个独立 Gap 隐含了"先做 Element 再做 Page"的错误顺序。正确的做法是建立统一的 Semantic Evidence Layer，Element Evidence 和 Page Evidence 是其两个并行投影。

### 3.2 Element Evidence

`ObservedElement` (Model) 当前只有三个字段：
- `Text: string` — OCR 输出的文字
- `SwitchState: bool?` — 开关状态
- `Index: int` — 在元素列表中的位置

**没有：坐标、边界框、元素类型（YOLO label）、层级关系、交互能力、置信度。**

Element Evidence 需要包含：

| Evidence 维度 | 定义 | 当前缺失的后果 |
|---|---|---|
| **existence** | 这个元素真实存在吗？（不是 OCR 幽灵、不是 subtitle phantom） | "Bluetooth, pairing" 字幕被分类为 NavigableContainer — 它只是文字，不可交互 |
| **identity** | 这个元素的语义身份是什么？（"Wi‑Fi" 入口 vs "AndroidWifi" 文本 vs "Wi-Fi doesn't turn back on automatically" 说明） | 三个都含 "Wi‑Fi" 文字，哪个是 Wi‑Fi 入口？ |
| **category** | 元素的语义类别（menu entry / toggle / label / container / subtitle） | subtitle phantom 被分类为 NavigableContainer |
| **capability** | 这个元素可点击？可切换？只读？ | 当前依赖 YOLO type label（不可靠） |
| **state** | 一个 toggle 控制什么？空文字的 toggle 什么意思？ | 空文字 toggle 无法理解 |
| **grounding** | 如何从语义目标定位到这个具体元素？ | 5/16 元素在 Settings root 有空文字 → 被 CategoryClassifier 排除 |

**Element Evidence 现实挑战：**

| 现实场景 | 当前行为 |
|---|---|
| **Empty text** | 5/16 元素在 Settings root 页面上有空文字 → 被 CategoryClassifier 排除（返回 null） |
| **Subtitle phantom** | "Bluetooth, pairing" 是字幕，不是菜单项 → 被分类为 NavigableContainer |
| **Wi‑Fi vs AndroidWifi** | 都含 "Wi‑Fi" 文字，都是 NavigableContainer → 多候选 → 无消歧 |
| **Duplicate candidates** | "Network&internet" ×2、"Internet" ×2、"QSearch settings" ×3 — substring 匹配不够 |

### 3.3 Page Evidence

`Container.TryVerifyLocalContinuity` (Container.cs:178-200) 用 4 个条件定义"同一页面"：

```text
1. observation.SequenceNumber > _observation.SequenceNumber   (新鲜度)
2. observation.ForegroundApplication == expected               (前台匹配)
3. IsStillMine(observation)                                    (调用侧注入)
4. reconciledSemanticPage == _semanticPageName                 (字符串匹配)
```

`Reconcile.FromObservation` (World/Reconcile.cs:20-37) 调用 `resolveSemanticPage(observation)` → `WorldBelief`：
- 解析成功 → `Confidence = 1.0`
- 解析失败（返回 null）→ `Confidence = 0.0`

Page Evidence 需要包含：

| Evidence 维度 | 定义 | 当前缺失的后果 |
|---|---|---|
| **semantic identity** | 这个屏幕的语义页面身份（不是文字匹配） | 文字相同但页面不同 → alias collapse |
| **context** | 当前页面在导航树中的位置（parent/sibling/child） | parent-return 绑死文字精确匹配 |
| **continuity** | 滚动/动画/动态内容后仍是同一页面 | 文本变化 → 身份断裂 |
| **transition evidence** | 从 A 到 B 的过渡证据（不是 A 的文字 == B 的文字） | 返回父页面要求 `element.Text == parent.SemanticPageName` |

**Page Evidence 关键问题：**

| 问题 | 详情 |
|---|---|
| **Resolver 与 Verifier 同源** | `resolveSemanticPage` 和 `IsStillMine` 在测试中是同一个函数。没有独立证据通道。 |
| **无独立证据** | `WorldBelief` 的 `Confidence` 和 Container 的 `IsStillMine` 来自同一个 oracle。TryVerifyViewportContinuity 用同一个函数同时"定义"和"验证"身份。 |
| **Parent-Return 绑死文字匹配** | 返回父页面要求 `element.Text == parent.SemanticPageName`（文字精确匹配），且要求唯一的候选元素。内部标识符不会出现在 UI 中。 |

**Page Evidence 现实挑战：**

| 现实场景 | 当前模型行为 |
|---|---|
| **InternetPage/WifiPage alias collapse** | 两个不同屏幕因为都含 `SwitchState is not null` → 解析为同一个 "WifiSub" → Container 不知道自己错了 |
| **Duplicate labels** | "Network&internet" 出现在 Settings root 和 Network 页面 → 文字相同但页面不同 |
| **Persistent header** | "Settings" 跨导航存在 → `!IsStillMine(childObs)` 永不成立 → 子 Container 永远进不去 |
| **Dynamic content** | Badge、时间戳、广告、loading spinner → 文本变化 → 身份断裂 |
| **Scroll** | 元素内容大幅变化但仍是同一语义页面 → 文本变化 → 身份断裂 |

### 3.4 Observation Interpretation

当前 Runtime 对 Observation 完全透明——它接收元素列表，不理解屏幕含义。Observation Interpretation 需要回答：

| Interpretation 维度 | 定义 | 当前状态 |
|---|---|---|
| **current screen meaning** | 当前屏幕整体在做什么？（Settings root / WiFi detail / loading state / error dialog） | **缺失** — Runtime 没有"屏幕含义"的概念 |
| **ambiguity** | 当前观测有多模糊？（多个候选页面、多个候选元素） | **缺失** — 二值 Confidence 无法表达 |
| **conflicting evidence** | OCR 说 A、YOLO 说 B、previous state 说 C — 如何处理冲突？ | **缺失** — 没有证据冲突处理 |

### 3.5 关键区分（必须明确）

```
ObservedElement != SemanticElement
Observation != Page
```

- `ObservedElement` 是物理层事实（OCR 文字 + 开关状态 + 索引）。
- `SemanticElement` 是语义层认知（有身份、能力、状态的交互对象）。
- `Observation` 是单个 snapshot（元素列表）。
- `Page` 是语义层身份（有上下文、连续性、过渡证据的屏幕状态）。

当前 Runtime 只有物理层，没有语义层。Semantic Evidence Layer 就是这两层之间的中间层。

---

## 4. BELIEF_MODEL_GAP (P1)

### 4.1 核心问题修正

原描述："WorldBelief.Confidence 需要从二值升级为分级置信度"——这过于简化。

**真正的核心问题：当前 `Resolver output == Truth`。**

`Reconcile.FromObservation` 调用 `resolveSemanticPage` → 直接产出 `WorldBelief`，`Confidence` = 1.0（解析成功）或 0.0（失败）。Resolver 的输出直接被当作世界事实。没有证据融合、没有多源校验、没有冲突仲裁。

### 4.2 需要：Evidence Fusion

```text
Evidence sources:
  - OCR                    (文字识别)
  - vector similarity      (语义嵌入匹配)
  - previous state         (上一帧的信念)
  - transition history     (导航过渡历史)
  - VLM                    (视觉语言模型理解)
        ↓
  Evidence Fusion
        ↓
  Belief:
    Known          (多源一致，高置信)
    Probable       (单源或弱证据，中等置信)
    Unknown        (证据不足)
    Contradicted   (多源冲突，不可信)
```

### 4.3 挑战

| 场景 | 问题 |
|---|---|
| **模糊页面** | 80% 确定这是 "Settings root"，20% 可能是 "Network Settings" — 当前模型只能说 100% 或 Unknown |
| **Confidence is evidence, not truth** | 置信度是"我的观察有多可靠"，不是"世界事实有多真"。当前模型没有这个区分。 |
| **Confidence != Truth** | Resolver 说 Confidence=1.0 不等于世界事实为真。当前模型把两者等价。 |
| **冲突证据** | OCR 说 "Wi‑Fi"，YOLO 说 label，previous state 说上一个页面 — 谁赢？当前没有仲裁机制 |

### 4.4 判定

**BELIEF_MODEL_GAP (P1)** — 需要从 `Resolver output == Truth` 升级为 Evidence Fusion 模型。核心不是"加 confidence 数值"，而是建立多源证据融合 → 分级 Belief（Known/Probable/Unknown/Contradicted）的机制。明确 **Confidence != Truth**。

**依赖：** Semantic Evidence Layer（P0）——没有证据就没有可融合的东西。

---

## 5. ACTION_SEMANTIC_GAP (P1)

### 5.1 当前 Action 模型

当前 Action 类型：

| Action | 当前语义 |
|---|---|
| `Tap` | 执行原语——点击某个坐标/元素 |
| `Scroll` | 执行原语——滚动屏幕 |
| `SetSwitch` | 执行原语——设置开关状态 |
| `Back` | 执行原语——返回 |

**这些只是 execution primitive。** 它们描述"做什么动作"，但不描述"这个动作的语义含义"。

### 5.2 缺失：Action Semantic Meaning

一个 Action 的完整语义应该是：

```text
Action = {
  target semantic,      // 这个动作作用于什么语义对象？
  current context,      // 当前在什么页面/状态下做这个动作？
  expected effect        // 做完之后期望什么语义结果？
}
```

### 5.3 举例：WiFi 开启流程

```text
WiFi 意图："确保 WiFi 已开启"

第一次 Tap（点击 "Wi‑Fi" 菜单项）:
  target semantic  = Wi‑Fi 入口（navigable menu item）
  current context  = Settings root page
  expected effect  = Navigate to WiFi detail page

第二次 SetSwitch（打开 WiFi 开关）:
  target semantic  = Wi‑Fi toggle（controllable switch）
  current context  = WiFi detail page
  expected effect  = Change desired state to ON
```

当前 Runtime 不区分这两次动作的语义——它们都是"调用侧提供的 per-action grounding criterion"。Runtime 不知道第一次是导航、第二次是状态变更。

### 5.4 TypeLevelDispatchPolicy 的边界

**TypeLevelDispatchPolicy 解决的是：Category → Handling**

```text
Category (NavigableContainer / LeafNode / ...)
  → Handling (DispatchChild / VerifyLeaf / ...)
```

**但没有解决：Semantic Target → Action Meaning**

```text
Semantic Target (Wi‑Fi 入口 / Wi‑Fi 开关 / 返回按钮)
  → Action Meaning (Navigate / ChangeState / GoBack)
```

TypeLevelDispatchPolicy 告诉 Runtime "遇到 NavigableContainer 就 dispatch 子容器"，但不告诉 Runtime "Tap 这个元素是导航还是状态变更"。Action 的语义含义完全由调用侧的 handling policy 决定，不是 Action 本身的性质。

### 5.5 判定

**ACTION_SEMANTIC_GAP (P1)** — Action 需要从 execution primitive 升级为语义对象（target semantic + current context + expected effect）。TypeLevelDispatchPolicy 解决 Category→Handling，但不解决 Semantic Target→Action Meaning。

**依赖：** Semantic Evidence Layer（P0）——Action 的 target semantic 依赖元素语义身份；current context 依赖页面语义身份。

---

## 6. COMPILER_BOUNDARY_GAP (P1)

### 6.1 当前编译器边界

```text
Intent (NL: "确保 WiFi 已开启")
  → IntentSemanticEnvelope (Projection)
  → TypeLevelTraversalSpecification (Scope + Categories + Depth + Safety)
  → Agent (Runtime)
```

编译发生在**调用侧**（Planning namespace）。Runtime 接收已经编译好的 spec。编译器不属于 Runtime。

### 6.2 挑战原假设

原判定（P3）认为编译器只需产生约束（scope/depth/safety/policy），不需要产生期望和提示。

**重新挑战：Compiler 不负责生成 route（这是对的），但应该生成 Semantic Execution Contract。**

```text
Intent
  ↓
Semantic Execution Contract:
  - desired capability        // 这个意图需要什么能力？（区分 switch 和 label）
  - allowed interaction       // 允许哪些交互？（Tap / SetSwitch / 不允许 Drag）
  - expected semantic objects // 期望遇到什么语义对象？（Wi‑Fi 入口、Wi‑Fi 开关）
  - safety constraints        // 安全边界（read-only / 不允许修改其他设置）
  - completion meaning        // 什么算完成？（WiFi 状态 == ON）
  ↓
Runtime 执行
```

### 6.3 当前 vs 需要

| Contract 维度 | 当前 | 需要？ |
|---|---|---|
| **Goal** | ✅ 调用侧注入 | — |
| **Scope** (app + semantic root) | ✅ TypeLevelTaskScope | — |
| **Constraint** (depth, safety) | ✅ MaximumDepth, SafetyBoundary | — |
| **Dispatch Policy** (category→handling) | ✅ TypeLevelDispatchPolicy | — |
| **desired capability** | ❌ | **需要** — "这个意图需要区分 switch 和 label" |
| **allowed interaction** | ❌ | **需要** — "允许 Tap 和 SetSwitch，不允许 Drag" |
| **expected semantic objects** | ❌ | **需要** — "Wi‑Fi 开关应该在 Internet 页面，类型为 switch" |
| **safety constraints** | ✅ 部分（SafetyBoundary） | 需扩展为语义级安全 |
| **completion meaning** | ✅ 部分（GoalEvidence） | 需扩展为语义级完成含义 |

### 6.4 判定

**COMPILER_BOUNDARY_GAP (P1)** — 编译器应从只产生约束升级为产生 Semantic Execution Contract（desired capability + allowed interaction + expected semantic objects + safety constraints + completion meaning）。Runtime 接收 contract 并执行，但不生成 contract。

**优先级调整：P3 → P1。** 理由：Semantic Execution Contract 是 Action Semantics（P1）和 Belief Model（P1）的上游——没有 contract，Runtime 不知道"期望什么"，就无法判断"是否达成"。

**依赖：** 与 Semantic Evidence Layer 并行——Compiler 产生期望，Evidence Layer 产生观测，Belief 融合两者。

---

## 7. PERCEPTION_ROUTING_GAP (P2)

### 7.1 当前感知模型

Runtime 对感知流水线完全透明 — 它接收 `Observation`（元素列表），不关心这些元素是怎么来的（YOLO/OCR/VLM/LLM）。`TypeLevelElementCategory` 是调用侧用 `CategoryClassifier` 注入的。

### 7.2 Fast vs Slow Perception

| 层 | 组成 | 用途 | 当前状态 |
|---|---|---|---|
| **Fast Perception** | OCR、embedding（vector similarity）、semantic cache（deterministic rules）、previous belief | 快速候选匹配、页面识别、已知元素 grounding | **缺失** — 没有语义缓存，没有 prototype 复用 |
| **Slow Perception** | VLM、LLM | 未知页面理解、模糊元素消歧、复杂意图解析 | **缺失** — 没有 escalation path |

### 7.3 关键架构问题

**谁决定：什么时候升级慢智能？**

- Agent 拥有 completion authority (I-10)
- Traversal 拥有 local dispatch authority
- Container 拥有 page-local state
- 但没有组件拥有 "fast perception 不够 → 升级到 slow perception" 的 decision authority

**禁止：每一步调用 VLM。** 这是不可接受的成本和延迟。必须有 escalation gate——fast perception 够用时用 fast，不够时才升级 slow。

**谁拥有 escalation decision？**

| 选项 | 问题 |
|---|---|
| Agent 决定 | Agent 关心 completion，不关心 perception 细节 |
| Traversal 决定 | Traversal 关心 dispatch，不关心 perception |
| Container 决定 | Container 关心 page-local state，可能最合适 |
| 独立 Perception Router | 新组件——但增加复杂度 |

**谁拥有 evidence aggregation？**
- 多次观测的证据如何聚合？当前每个 Observation 都是独立的 snapshot
- 同一元素的多次 OCR 输出如何归一化？当前是调用侧的 9-case normalization

**谁拥有 final authority？**
- 如果 fast perception 说 "menu_item, 95% confidence" 但 slow perception 说 "text label, 80% confidence" — 谁决定？

### 7.4 判定

**PERCEPTION_ROUTING_GAP (P2)** — 需要定义：(a) fast/slow perception 之间的 escalation path 和 escalation gate，(b) evidence aggregation 的 ownership，(c) conflicting perception 的 final authority。禁止每步调用 VLM。

**依赖：** Semantic Evidence Layer（P0）+ Belief Model（P1）——routing 的输入是 evidence，routing 的输出是 belief。

---

## 8. SEMANTIC_MEMORY_GAP (P3)

### 8.1 当前状态

Runtime 无任何 memory 机制。每次 Run 从零开始。Container.Bind 重置一切。无跨 Run 学习。

### 8.2 Memory 必须建立在稳定 Semantic Evidence 上

**关键约束：错误分类会被长期强化。**

如果 Semantic Evidence Layer 不稳定（元素身份判断错误、页面身份判断错误），Memory 会固化这些错误：

```text
错误：Wi‑Fi 字幕被分类为 NavigableContainer
  → Memory 记录"Wi‑Fi 字幕是可导航容器"
  → 下次遇到 → 快速匹配 → 直接走错
  → 错误被长期强化
```

**关系链：**

```text
Semantic Evidence  (P0)
  ↓
Belief             (P1)
  ↓
Memory             (P3)
```

Memory 必须等 Evidence 和 Belief 稳定后才能建立。否则不是加速，而是错误放大。

### 8.3 未来 Memory 应该保存什么？

| 记忆类型 | 内容 | 用途 |
|---|---|---|
| **Page Prototype** | 语义页面的"典型"元素清单（文字、坐标、类型、稳定特征） | 下次遇到类似页面 → 快速识别 |
| **Element Prototype** | 语义元素的"典型"属性（"Wi‑Fi 开关通常在页面下半部分"） | grounding 加速 |
| **Transition Experience** | "点击 A → 到达 B"的成功/失败历史 | 导航决策 |
| **Failure Experience** | "上次按这个元素没反应"/"上次这个路径走不通" | 避免重复错误 |

### 8.4 Memory Authority 边界

| 类型 | 说明 |
|---|---|
| **Memory as acceleration** | 记忆提供"上次这个页面长这样"的 hint，但 fresh observation 仍然是 authoritative |
| **Memory as authority** | ❌ 禁止 — 记忆不能取代当前观测。"上次 Wi‑Fi 在这里" ≠ "现在 Wi‑Fi 还在这里" |

### 8.5 判定

**SEMANTIC_MEMORY_GAP (P3)** — 需要定义：(a) memory 保存什么（page/element/transition/failure prototype），(b) memory 的 authority 边界（acceleration only, not truth），(c) memory 的 owner。

**优先级调整：P3 保留（或可降低）。** 理由：Memory 不是当前瓶颈——先解决单次 run 内的语义准确性（P0/P1）。Memory 建立在稳定 Evidence 上，否则错误分类会被长期强化。

---

## 9. Reality Validation Context

### 9.1 已有的验证资产

| 等级 | 资产 | 状态 |
|---|---|---|
| **RECORDED_REALITY** (E4/E3) | EP-03 成功/失败 trace、EP-04 sim-replay、E-10 TraceReplay fixtures、Vision Golden | ✅ 已提取，已用于 L2-R 仿真 |
| **REALITY_SEEDED** (L2-R) | RealitySeededSettingsFixture — 真实元素数据 + 3 层深度 | ✅ 已有，6 个 proof 通过 |
| **SYNTHETIC** (E2/E1) | Capstone、U3F1、NormalWifiHappyPath、现有 494 个测试 | ✅ 已有 |

### 9.2 缺失的验证

| 场景 | 缺失的资产 | 影响 |
|---|---|---|
| **Popup / Obstruction** | 无真实 popup 截图或 recorded run | 无法验证 `IsLocalObstructionHypothesis` |
| **Recovery** | 无真实 drift/trap/recovery 记录 | 无法验证恢复语义 |
| **Drift** | 无外部 app 切换的 recorded trace | 无法验证 `IsAgentScopeDrift` |
| **Scroll Continuity** | 无真实 scroll 前后的元素清单对比 | 无法验证 viewport continuity |
| **Unknown Page** | 无 `resolveSemanticPage → null` 的真实案例 | `Confidence=0` 的路径未测试 |
| **Real State Mutation** | **无 Wi‑Fi OFF→ON before/after pair** | CP-06/CP-12 的 E4 证据缺失 |

### 9.3 定位

Reality Validation 是**验证支撑**，不是语义 Gap。它验证 Semantic Evidence Layer 和 Belief Model 是否对外部世界正确，但本身不提供语义能力。最关键的缺失是 **Wi‑Fi OFF→ON state-change pair**（无法验证完整的 desired-state 链路）。

---

## 10. Legacy Architecture Reassessment

对 feature/refactor 旧架构的概念价值评估。**禁止直接迁移旧代码。**

### 10.1 重新分类标准

| 决策 | 含义 |
|---|---|
| **KEEP** | 设计思想有价值，保留概念（不迁移代码） |
| **REBUILD** | 语义证据能力需要重新实现 |
| **REJECT** | 旧 FSM/Graph 机制已被取代，废弃 |

### 10.2 Legacy 概念评估

| Legacy Concept | Current Equivalent | Value | Decision |
|---|---|---|---|
| **ContainerSemanticsEngine** | `Container` + 调用侧 `resolveSemanticPage` | 概念有价值——页面语义引擎 | **REBUILD** — 需要重建为 Semantic Evidence Layer 的 Page Evidence 组件 |
| **PageState** | `WorldBelief` + `Container._semanticPageName` | 概念有价值——页面状态 | **REBUILD** — 需要重建为分级 Belief（非二值） |
| **SemanticPage** | 调用侧 `resolveSemanticPage` lambda | 概念有价值——语义页面身份 | **REBUILD** — 需要独立证据通道（非 self-referential oracle） |
| **Vision** (YOLO + OCR + fusion) | 调用侧提供 Observation | 概念正确——感知在 Runtime 外部 | **KEEP** (在调用侧) |
| **Memory** | 无（Runtime 无 memory） | 概念有价值——跨 run 学习 | **REBUILD** — 需等 Evidence 稳定后重建（P3） |
| **DynamicMatch / DynamicRules** | `TypeLevelDispatchPolicy` | 概念有价值 — 类型匹配 → handling 映射 | **KEEP** (已重建) |
| **PlanCompiler** (5-step) | `TypeLevelTraversalSpecification` 直接构造 | 编译器逻辑有价值但应留在调用侧 | **REBUILD** (Phase 5/6 重新设计为 Semantic Execution Contract) |
| **TraversalFSM** (8-state, 19-edge) | Agent open-world loop + Container | FSM 本身是 implementation detail，概念已被 Agent loop 取代 | **REJECT** |
| **GlobalFSM** (8-state) | Agent.RunAsync state machine | 同上 | **REJECT** |
| **IntentExtractor** (AI NL → IntentSlots) | 调用侧 Planning | 有价值的 NL→Intent 转换，但不属于 Runtime | **KEEP** (在调用侧) |
| **ScenarioPlanLoader** (Static JSON → Plan) | `IntentExecutionRepresentation.ClosedWorldConcrete` | 有价值的 closed-world 模式 | **KEEP** (已重建) |
| **ITraversalAdvisor** (Goal-directed next action) | Agent open-world loop + dispatch policy | 概念正确但 implementation 有缺陷 | **REBUILD** |
| **TraceTool VerifyEngine** | `GoalEvidence` + I-10 | 离线 verify → 在线 completion | **REJECT** (已被取代) |
| **StateFixture / StateFixtureBuilder** | `ScriptedEnvironment` + `ScreenConfig` | 有价值的确定性仿真模式 | **KEEP** (已重建) |
| **Analysis.jsonl** (per-page snapshots) | `ViewportExplorationObservations` | 有价值的 observation history | **KEEP** (已在 Container 中) |
| **Safety Policy** (settings-read-only-v1) | `TypeLevelSafetyBoundary` + `CandidateAuthorizationEvaluator` | 有价值的安全约束 | **KEEP** (已重建) |
| **OpenSpec Change Archive** (76 changes) | — | 有价值的历史词汇和证据链 | **KEEP** (参考) |

### 10.3 迁移禁令

- ❌ **禁止直接迁移旧代码** — 所有 REBUILD 项必须基于新 Semantic Evidence Model 重新设计
- ✅ **允许借鉴设计思想** — KEEP 项的概念可以指导新实现
- ✅ **必须拒绝旧 FSM/Graph 机制** — REJECT 项已被 Agent loop 取代，不再适用

---

## 11. Gap Priority Matrix

| Priority | # | Gap | Definition | Dependency |
|---|---|---|---|---|
| **P0** | 1 | **SEMANTIC_EVIDENCE_MODEL_GAP** | Runtime 缺少 Raw Observation → Semantic Evidence 的中间层。包含 Element Evidence（existence/identity/category/capability/state/grounding）、Page Evidence（semantic identity/context/continuity/transition evidence）、Observation Interpretation（current screen meaning/ambiguity/conflicting evidence）。明确 ObservedElement != SemanticElement，Observation != Page。 | 无 |
| **P1** | 2 | **BELIEF_MODEL_GAP** | 当前 Resolver output == Truth。需要 Evidence Fusion（OCR/vector/previous state/transition history/VLM → Known/Probable/Unknown/Contradicted）。Confidence != Truth。 | Semantic Evidence (P0) |
| **P1** | 3 | **ACTION_SEMANTIC_GAP** | Action 只是 execution primitive。需要 Action Semantic Meaning（target semantic + current context + expected effect）。TypeLevelDispatchPolicy 解决 Category→Handling，不解决 Semantic Target→Action Meaning。 | Semantic Evidence (P0) |
| **P1** | 4 | **COMPILER_BOUNDARY_GAP** | 编译器应产生 Semantic Execution Contract（desired capability + allowed interaction + expected semantic objects + safety constraints + completion meaning），不只产生约束。P3→P1。 | 与 Semantic Evidence 并行 |
| **P2** | 5 | **PERCEPTION_ROUTING_GAP** | Fast/Slow perception 之间需要 escalation path 和 escalation gate。禁止每步调用 VLM。需要 evidence aggregation ownership 和 conflicting perception final authority。 | Semantic Evidence (P0) + Belief (P1) |
| **P3** | 6 | **SEMANTIC_MEMORY_GAP** | Memory 必须建立在稳定 Semantic Evidence 上，否则错误分类被长期强化。关系：Evidence → Belief → Memory。P3 保留或可降低。 | Evidence (P0) + Belief (P1) |

---

## 12. Evolution Dependency Graph

```text
              Observation
                    |
                    v
          Semantic Evidence Layer        ← P0: 统一证据层
                    |
          +---------+---------+
          |                   |
          v                   v
       Belief             Action Semantics    ← P1: 并行分支
          |                   |
          v                   |
      Continuity              |
          |                   |
          v                   v
     Open World Execution     Execution Contract
                              ← P1: 语义执行契约


Compiler Contract (Semantic Execution Contract):
  parallel track — 与 Evidence 并行，产生期望/约束/完成含义


Memory:
  after evidence stabilization — Evidence (P0) → Belief (P1) → Memory (P3)


Perception Routing:
  after Evidence + Belief — 提供 fast/slow escalation gate
```

**关键依赖关系：**

1. **Semantic Evidence Layer 是所有上层的基础** — 没有 Evidence，Belief 无可融合、Action 无 target semantic、Memory 无稳定基础。
2. **Belief 和 Action Semantics 是并行分支** — 二者都依赖 Evidence，但互不依赖。可以并行推进。
3. **Compiler Contract 是并行轨道** — 不在 Evidence → Belief 链上，但为 Runtime 提供"期望什么"的上游输入。
4. **Memory 在最后** — 必须等 Evidence 和 Belief 稳定，否则错误分类被长期强化。
5. **Perception Routing 在 Evidence + Belief 之后** — routing 的输入是 evidence，输出是 belief。

---

## Key Findings

### 最大 5 个关键 Gap（修正后）

1. **SEMANTIC_EVIDENCE_MODEL_GAP** — Runtime 缺少 Raw Observation → Semantic Evidence 的中间层；Element 和 Page 是并行投影，不是线性依赖
2. **BELIEF_MODEL_GAP** — Resolver output == Truth；需要 Evidence Fusion；Confidence != Truth
3. **ACTION_SEMANTIC_GAP** — Action 只是 execution primitive；需要 target semantic + current context + expected effect
4. **COMPILER_BOUNDARY_GAP** — 编译器应产生 Semantic Execution Contract，不只产生约束（P3→P1）
5. **PERCEPTION_ROUTING_GAP** — Fast/Slow perception 之间需要 escalation gate；禁止每步调用 VLM

### 最大 3 个错误风险

1. **把 Element 和 Page 当作线性依赖** — 二者是 Observation 的并行投影，不是上下级关系。先做 Element 再做 Page 是错误顺序。
2. **把 Resolver output 当作 Truth** — Confidence != Truth。Resolver 输出是证据，不是世界事实。需要 Evidence Fusion。
3. **把调用侧注入当作"已有语义"** — `resolveSemanticPage` 和 `identityRule` 是调用侧提供的，不是 Runtime 的能力。当前模型是 self-consistent administrative shell，但对外部世界不可证伪。

---

## Architecture Review Revision

### 1. Original Assumption Challenged

**原假设：** Element Semantics 和 Page Semantics 是线性依赖关系——先定义 Element，再定义 Page，再定义 Belief，再定义 Continuity。

**挑战：** Element 和 Page 不是上下级关系。二者都是 Raw Observation 的不同语义投影。它们并行汇聚到 Semantic Belief，不存在"先做 Element 再做 Page"的顺序。原模型的最小单元假设是错误的——最小单元不是"Element"，而是"Observation → Evidence 投影"。

**影响：** 原 ELEMENT_SEMANTIC_GAP（P0）和 PAGE_SEMANTIC_GAP（P0）被合并升级为 SEMANTIC_EVIDENCE_MODEL_GAP（P0），统一为 Semantic Evidence Layer。

### 2. New Semantic Model

```text
Raw Observation
        |
        +----------------+
        |                |
        v                v
 Element Evidence    Page Evidence      ← 并行投影
        |                |
        +-------+--------+
                |
                v
        Semantic Belief                ← 证据融合
                |
                v
     Action / World Understanding     ← 语义行动
```

**三个关键区分：**
- `ObservedElement != SemanticElement`（物理层 vs 语义层）
- `Observation != Page`（snapshot vs 语义身份）
- `Confidence != Truth`（证据 vs 事实）

### 3. Priority Changes

| Gap | 原优先级 | 新优先级 | 变化原因 |
|---|---|---|---|
| SEMANTIC_EVIDENCE_MODEL_GAP | P0（拆为 Element + Page 两个） | P0（合并） | Element 和 Page 是并行投影，合并为统一 Evidence Layer |
| BELIEF_MODEL_GAP | P1 | P1 | 保留，但重新描述为 Evidence Fusion（非简单加 confidence） |
| ACTION_SEMANTIC_GAP | —（不存在） | P1 | 新增。Action 只是 execution primitive，需要语义含义 |
| COMPILER_BOUNDARY_GAP | P3 | P1 | 升级。编译器应产生 Semantic Execution Contract |
| PERCEPTION_ROUTING_GAP | P2 | P2 | 保留。强调 escalation gate，禁止每步 VLM |
| SEMANTIC_MEMORY_GAP | P3 | P3（或降低） | 保留。强调必须等 Evidence 稳定，否则错误强化 |
| REALITY_GAP | P1 | —（移出矩阵） | 降为验证支撑，不是语义 Gap |

### 4. First Recommended Challenge

**第一个推荐挑战：SEMANTIC_EVIDENCE_MODEL_GAP (P0) — 建立 Semantic Evidence Layer。**

建议从 **Element Evidence 的 identity 维度** 入手：

- **挑战问题：** 给定一个 Observation（元素列表），Runtime 能否独立判断"哪个元素是 Wi‑Fi 入口"——不依赖调用侧注入的 CategoryClassifier？
- **当前行为：** 5/16 元素有空文字被排除；"Wi‑Fi" 文字出现在入口、状态文本、说明文字中 → 无法消歧。
- **验证标准：** 在 RealitySeededSettingsFixture（真实元素数据）上，Runtime 能否独立产出 Element Evidence（identity + category + capability），而非依赖调用侧 lambda？
- **禁止偷渡：** 不能在 Evidence Layer 里调用调用侧的 oracle 来"定义"identity——这会重蹈 resolveSemanticPage 的 self-referential 覆辙。

**为什么是 identity 维度：** 它是最小的、可验证的、不依赖 Page Evidence 的投影。如果 Runtime 连"这个元素是什么"都判断不了，Page Evidence 和 Belief Fusion 都无从谈起。

---

## SEMANTIC_ARCHITECTURE_GAP_REVIEW_REVISED

> Production Changes: NONE
> Runtime Changes: NONE
