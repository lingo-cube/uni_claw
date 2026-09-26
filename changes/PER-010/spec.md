# PER-010 — Android UI Hierarchy Compatibility Layer

版本：v0.1.1（narrow amendment；design-only；保持 FROZEN）

## Intent（WHAT/WHY）

建立 Android UI Hierarchy Compatibility Layer，让 Product 依赖稳定的 typed observation，而不是依赖某一种 Android、UiAutomator 或 Accessibility XML 形状。本 change 只冻结设计和验收边界，不写 Product implementation。

兼容下限为 Android 9 / API 28。API 版本只用于 coverage 与 capability 事实，不产生按 Android major version 分裂的 Product schema。

## Scope

- 定义 `UiHierarchyObservation v1` 的稳定内部协议和 capture metadata。
- 定义 legacy uiautomator XML、AndroidX UiAutomator、AccessibilityNodeInfo / richer source 到稳定协议的 adapter 责任。
- 定义 capability、Observed / Unknown / Unsupported、checked 三态、occurrence identity、window、coverage、partial/failure 语义。
- 定义 acquisition bounded seam、Agent boundary、Grounding boundary。
- 建立 API/source/capability/field/failure matrix 与 fixture inventory。

## Out of Scope

- 不新增 Product public interface，不改 `WorldModel`、Evidence Ledger、Grounding、Assurance、Effect Boundary 或 AGT/RUN frozen boundary。
- 不把 raw XML 暴露给 UniAgent / DSH，不在 compatibility layer 内建立 belief、canonical identity、absence truth 或 fusion authority。
- 不为 API 28、29、30…分别维护 Product schema。
- 不承诺 DOM、WebView 内部树、Compose unmerged tree 或 OEM 私有字段始终可用。
- 不实现 adapter、真机采集或 Product tests；这些属于后续 implementation slices。

## Stable Observation Contract v1

### Capture result

Acquisition adapter 返回有界的 `UiHierarchyCaptureResult` 概念值：

| outcome | 含义 | Product 语义 |
|---|---|---|
| `Complete` | 树成功取得且声明覆盖完整 | 可生成完整 observation，但仍需 capability/字段状态检查 |
| `Empty` | 采集成功，树没有节点 | 这是 empty capture，不是世界中元素不存在 |
| `Partial` | 有树或窗口，但声明覆盖/字段不完整 | observation 带 coverage limitation；未给出的字段不补值 |
| `SourceUnavailable` | 超时、取消、权限/服务不可用或连接失败 | 没有 observation；带可分类诊断 |
| `Malformed` | 输入存在但 XML/节点结构不可解析 | fail-closed；保留 schema 诊断，不产生部分节点 |

`SourceUnavailable` 与 `Empty` 永远不可折叠；`Partial` 也不能被解释为完整页面。超时和取消必须有上界，adapter 不允许无限等待 dump。

### CaptureMetadata

每次 capture 至少携带：

```text
CaptureId
AndroidApiLevel
AcquirerKind
AcquirerVersion
HierarchyFormat
CaptureTimestamp
DeviceId / SessionCorrelation
ObservationCycleId（可选但推荐）
Capabilities
Coverage
```

`CaptureTimestamp`、`SessionCorrelation`、`ObservationCycleId` 是 provenance 与跨 source 对齐输入，不是 freshness 或 truth authority。Freshness 仍由具体消费场景的 Assurance judgment 决定。

`AcquirerKind` 至少区分 `LegacyUiAutomatorXml`、`AndroidXUiAutomator`、`AccessibilityNodeInfo`；`HierarchyFormat` 至少区分 `UiAutomatorXml`、`AccessibilityTree`、`ComposeSemantics`、`Unknown`。格式和采集器可以任意组合，不能由 Android major version 猜测。

### Windows

`Windows[]` 是 capture-local window occurrences，包含 occurrence-local ref、可观察 bounds、package/title 等 `ObservedValue`、focused/visible 等能力约束字段，以及可选 drawing order。没有 window capability 时必须标记 `Unsupported`，不得凭单树假造 single-window truth。

### Nodes

每个 node 是 capture/revision-local `NodeOccurrence`，包含：

```text
OccurrenceRef（opaque、仅当前 capture 有效）
ParentOccurrenceRef?（仅 association feature）
WindowOccurrenceRef?
SiblingOrder? / DrawingOrder?
Class
ResourceId
Package
Text
ContentDescription
Hint
Checkable / Checked
Enabled / Selected / Focused
Scrollable / Clickable / Focusable
VisibleToUser
Bounds
```

除 `OccurrenceRef` 等结构字段外，节点属性使用：

```text
ObservedValue<T>
├ State: Observed | Unknown | Unsupported
├ Value?
└ Provenance（capture/field/source/normalization）
```

规则：

- 字段缺失 ≠ `Observed(false)`；
- source unavailable ≠ element missing；
- acquirer 不具能力 ≠ 本次理论上可判定但未判定；
- malformed 字段不回退为默认值，记录 `Unknown` 或使整个 capture `Malformed`，由 adapter 的 field policy 明确选择。

### Checked

`Checked` 的值轴为：

```text
Checked | Unchecked | Partial
```

外层仍使用 `ObservedValue<CheckedState>`，因此可表达 `Unknown` 与 `Unsupported`。能力必须区分：`checkedTriState` 可完整表达 `Checked/Unchecked/Partial`；`checkedBooleanExact` 只在 source 能证明当前 claim domain 只有二态时表达 `Checked/Unchecked`；`checkedBooleanCollapsed` 只能表达 lossy boolean。legacy XML 的 `checked=true` 可映射为 `Checked`；`checked=false` 仅在二态 domain 已被 source 证明时映射为 `Unchecked`，否则输出 `Unknown`，reason=`partial-unrepresentable`。不得把 boolean 伪造为 `Partial`，也不得在字段缺失时伪造 `Unchecked`。`checkable=false` 是后续状态权威使用的 validity guard，不会抹掉 raw observed checked evidence。

### Capabilities

Capabilities 是 adapter 声明并可被测试的能力集合，至少覆盖：

```text
checkedBooleanExact
checkedBooleanCollapsed
checkedTriState
drawingOrder
hint
windowSupport
visibility
semanticText
contentDescription
composeSemantics
webViewInnerContent
```

能力按 source/acquirer/version 事实映射，不按 Android major version 硬编码。`Unsupported` 只表示能力不存在；`Unknown` 表示能力理论上存在但此次结果不足。

### Identity and association

XML node / hierarchy node 永远是 occurrence，不是 canonical entity identity。`index`、XPath、hierarchy path、bounds、resource-id、Compose key 或 provider node id 都不能单独成为跨 revision 永久 identity。它们只能作为 association features 交给 WorldModel 既有的 occurrence/continuity 语义使用。scroll、recycled list、Compose merge/unmerge、window 重排都允许 occurrence 改变。

## Source compatibility strategy

```text
Android UI source
  ├ legacy uiautomator XML
  ├ AndroidX UiAutomator
  └ AccessibilityNodeInfo / richer source
        ↓ adapter
Normalization
        ↓ UiHierarchyObservation v1
Evidence Ledger
        ↓ WorldModel
```

Compatibility bands 只用于测试覆盖：Legacy API 28–29、Standard API 30–34、Modern/Rich API 35/36+。运行时根据 `AndroidApiLevel + AcquirerKind + AcquirerVersion + Capabilities` 选择 adapter 行为；Product 不读取 band 名称。

Compose semantics 视为语义树 realization，不假设它等同传统 View tree；merged / unmerged 选择由 `HierarchyFormat`、capability 和 provenance 记录。WebView 只在 source 实际提供 inner content 时归一化，不能从 host node 推出 DOM truth。OEM 差异通过 capability/field state/diagnostic 暴露，不新造 OEM Product schema。

## Acquisition boundary

采集器是 external capability seam，必须支持：timeout、cancellation、source unavailable、malformed hierarchy、empty hierarchy、partial result。每个请求带调用方 budget；超时后返回 bounded diagnostic，并确保进程/临时文件清理。

## Agent and Grounding boundaries

UniAgent / DSH 只消费 `WorldModel` 派生的 bounded decision context；不得解析 raw XML、自己建立 UI truth 或自己实现 identity/fusion。bounds、resource-id 和其他 node fields 只能作为 grounding evidence，必须经过：

```text
World belief → Control Intent → Grounding → Assurance → Effect
```

禁止 `XML bounds → direct click`。

## Failure and absence matrix

| 情况 | observation 层输出 | 禁止解释 |
|---|---|---|
| 字段未出现在 node | `ObservedValue.Unknown` | false/empty/absent |
| acquirer 没有该能力 | `ObservedValue.Unsupported` | Unknown 或 false |
| dump 超时/取消 | `SourceUnavailable` + diagnostic | empty hierarchy |
| 服务不可用/权限拒绝 | `SourceUnavailable` + diagnostic | 页面没有元素 |
| XML 结构非法 | `Malformed` | 解析到部分可信节点 |
| 合法空树 | `Empty` + zero nodes | 世界 absence |
| 部分窗口/裁剪/虚拟化 | `Partial` + coverage | 全页面 absence |
| checked=false 且 `checkedBooleanExact` 已证明二态 | `Observed(Unchecked)` | Partial |
| checked=false 且为 `checkedBooleanCollapsed`，domain 可能含 Partial | `Unknown` + `partial-unrepresentable` | Unchecked |
| checked=partial 且 tri-state capability | `Observed(Partial)` | Unchecked |

## Grill findings disposition

原冻结设计已完成完整 PER-010 checklist；本次 v0.1.1 focused re-grill 只复查
F1 checked ambiguity。API 36 tri-state-capable environment 中，legacy XML
`checked=false` 按 capability 只能得到 `Unchecked`（已证明二态）或
`Unknown(partial-unrepresentable)`（lossy boolean），不得无条件输出
`Observed(Unchecked)`。结果 `PASS`；没有需要修改 Product baseline、WorldModel
authority、Grounding authority 或 AGT/RUN frozen boundary 的项，设计状态保持
`FROZEN`。

## References

- `changes/PER-009/state.md`
- `changes/PER-009/mechanism.md`
- `docs/architecture/product-architecture-baseline-l0-l3.md`
- `docs/architecture/uworld-protocol-baseline-l4.md`
- `docs/architecture/perception-provider-baseline-v0.1.md`
- `src/UniClaw.Host/UiAutomatorDump.cs`（现有 producer 事实，不是新协议）
- `plans/2026-09-26-per-010-compatibility-inventory.md`
