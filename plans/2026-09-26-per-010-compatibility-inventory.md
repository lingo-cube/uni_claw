# PER-010 Compatibility Inventory（资料与夹具事实）

版本：v0.1.1；本次仅补充 checked capability 的机械映射规则，不改变 inventory 的事实边界。

> 范围：只记录 Android/AOSP/API、采集器和仓库夹具可核实的事实。本文不定义 authority、identity、缺失语义、fusion precedence 或 WorldModel ownership；这些栏位若无法由来源直接证明，标为 `UNKNOWN`，留给 PER-010 架构裁决。
>
> 盘点日期：2026-09-26。官方 API 说明链接均为 Android Developers；仓库路径是当前共享工作区内的证据路径。

## 1. 版本与采集通道事实

| 覆盖项 | 已核实事实 | 证据 | 尚未核实 |
|---|---|---|---|
| API 28 / Android 9 | `AccessibilityNodeInfo` 的基础节点属性（class、package、text/content-description、checkable/checked、clickable、enabled、focusable/focused、scrollable、selected、screen bounds）均在 API 14 起存在；`isHeading()` 在 API 28 起存在。 | [AccessibilityNodeInfo API reference](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo) | 没有仓库内 API 28 真机 XML；该版本 `uiautomator dump` 对每个可选属性的 OEM 输出差异 `UNKNOWN`。 |
| API 29 / Android 10 | `getBoundsInParent()` 在 API 29 弃用，官方建议使用 `getBoundsInScreen()`；`isVisibleToUser()` 在 API 16 起存在，官方注明 API 16–29 放大功能开启时可能错误返回 false。 | [AccessibilityNodeInfo bounds/visibility](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo) | API 29 实机/不同 OEM 的 XML 属性采样 `UNKNOWN`。 |
| API 30–33 | `AccessibilityWindowInfo` 的窗口快照模型自 API 21 起存在；API 30 起提供 `EXTRA_DATA_RENDERING_INFO_KEY`（节点额外渲染数据键）。`AccessibilityNodeInfo.isTextSelectable()` 在 API 33 起存在。 | [AccessibilityWindowInfo](https://developer.android.com/reference/android/view/accessibility/AccessibilityWindowInfo), [AccessibilityNodeInfo](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo) | API 30–33 XML fixture 和采集器版本对应关系 `UNKNOWN`。 |
| API 34 / Android 14 | `AccessibilityNodeInfo.getBoundsInWindow()`、`isAccessibilityDataSensitive()` 在 API 34 起存在；窗口对象仍可提供 display、layer、bounds、root 等信息。 | [AccessibilityNodeInfo](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo), [AccessibilityWindowInfo](https://developer.android.com/reference/android/view/accessibility/AccessibilityWindowInfo) | 仓库没有 API 34 hierarchy dump；`uiautomator` XML 是否携带 window/display 信息 `UNKNOWN`。 |
| API 35 / Android 15 | 仓库有经审核的真实 emulator 资产，来源写明 `emulator-5554 / uniclaw-lite-api35 / Android 15 / API 35`，显示 1080×1920、density 420；XML corpus 仍是传统 `<hierarchy><node ...>` 形状。 | `tests/UniClaw.Kernel.Tests/Perception/Corpus/artifacts/scenario-manifest.json:1`；`tests/UniClaw.Kernel.Tests/Perception/Corpus/artifacts/*.xml` | 该资产不证明所有 API 35/OEM 行为，也不包含 tri-state checked 的真实序列化。 |
| API 36 / Android 16 | `AccessibilityNodeInfo.getChecked()` 和 `CHECKED_STATE_FALSE/TRUE/PARTIAL` 在 API 36 起存在；`isChecked()` 在 API 36 弃用。`getBoundsInWindow()` 仍可用于窗口坐标。 | [AccessibilityNodeInfo API 36 constants](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo), [API 36.1 diff](https://developer.android.com/sdk/api_diff/36.1/changes/android.view.accessibility.AccessibilityNodeInfo) | API 36 真机 XML/UiAutomator wrapper 的字段形态、OEM 输出 `UNKNOWN`。 |
| AndroidX UiAutomator | AndroidX 提供 `androidx.test.uiautomator` 包及 `AccessibilityNodeInfoExt` 扩展；扩展 API 标为 2.4.0，含 screen bounds、children、descendants 等访问辅助。 | [UiAutomator package](https://developer.android.com/reference/androidx/test/uiautomator/package-summary), [AccessibilityNodeInfoExt](https://developer.android.com/reference/androidx/test/uiautomator/AccessibilityNodeInfoExt) | 仓库未记录 AndroidX UiAutomator 依赖版本与其 API 28–36 的逐版本输出矩阵。 |

兼容 band（Legacy 28–29、Standard 30–34、Modern/Rich 35/36+）目前只能作为测试覆盖分组；官方 API 证据显示能力常按方法/字段加入，而非严格随 Android major version 整体切换。实际 acquirer/version/capability 组合仍需运行时资料补齐（`UNKNOWN`）。

## 2. 字段可见性与 API 事实

| 字段 | AccessibilityNodeInfo/API 事实 | 传统 XML fixture 事实 | 资料边界 |
|---|---|---|---|
| `text` | 节点 `getText()` 是 `CharSequence`；官方模型描述 accessibility node tree 不必与 View hierarchy 一一对应。 | 7 个 XML 均有 `text` 属性；可为空，存在 XML entity 解码样例 `Network &amp; internet`。 | XML text 与屏幕像素文字的关系未由 API 证明，`UNKNOWN`。 |
| `resource-id` | `getViewIdResourceName()` 返回 source view 的 fully-qualified resource name。 | `resource-id` 总是出现于仓库 XML；可为空；示例 `com.android.settings:id/wifi_switch`、`com.uniclaw.fixture:id/scenario_title`。 | 空值、重复值和跨采集 occurrence 的语义不在本 inventory 裁决。 |
| `class` | `getClassName()` 自 API 14；返回节点来源 class。 | 全部 125 个样本节点含 `class`，有 `FrameLayout`、`LinearLayout`、`TextView`、`Button`、`Switch` 等。 | class 是否足以分类/授权 `UNKNOWN`。 |
| `package` | `getPackageName()` 为节点包名。 | 全部样本含 package；fixture 示例 `com.uniclaw.fixture`。 | OEM/system overlay 包名行为 `UNKNOWN`。 |
| `content-desc` | `getContentDescription()` 返回内容描述。 | 全部样本含 `content-desc`，可为空；NAV-03 子节点含 `Child A/B/C`。 | 描述是否等价于可见文字 `UNKNOWN`。 |
| `checkable` / `checked` | `isCheckable()`、`isChecked()` 自 API 14；官方明确 checked 仅在 checkable=true 时有意义。API 36 提供 `getChecked()` 三值常量，`isChecked()` 弃用。 | XML 每个节点含 boolean `checkable`/`checked`；测试 fixture 将 `checked="partial"` 作为前向兼容解析样例，但真实 API 35 manifest/XML 并未证明该值。 | legacy XML 是否能表达 partial、以及如何映射缺字段，均 `UNKNOWN`。 |
| `enabled` / `selected` / `focused` | `isEnabled()`、`isSelected()`、`isFocused()` 自 API 14；`isFocused()` 与 accessibility focus 是不同概念。 | 7 个 XML 均含这三个 boolean 属性，样本大多为 false/true 组合。 | 不同 OEM 对 focus 时点的采集一致性 `UNKNOWN`。 |
| `scrollable` / `clickable` | `isScrollable()`、`isClickable()` 自 API 14。 | XML 均含字段；SCROLL-01 有 `scrollable="true"` 节点；按钮有 `clickable="true"`。 | 字段表达能力不等于动作可执行保证，`UNKNOWN`。 |
| `bounds` | `getBoundsInScreen()` 自 API 14；API 34 新增 `getBoundsInWindow()`。窗口也有 screen bounds。 | XML `bounds` 形如 `[x1,y1][x2,y2]`；仓库解析测试归一化为 `x1,y1,x2,y2`。 | 坐标系、多窗口/放大/OEM 修正规则需另行验证。 |
| `visible-to-user` | `isVisibleToUser()` API 16；API 16–29 放大开启时官方记录可能错误 false。 | 传统 XML corpus 没有 `visible-to-user` 属性（字段计数为 0）。 | XML 缺字段不能据此推导不可见；具体缺失语义留 `UNKNOWN`。 |
| `drawing-order` | `getDrawingOrder()` 在 AccessibilityNodeInfo API reference 中提供；版本加入级别和 XML dump 序列化未在仓库证实。 | corpus 没有 `drawing-order` 属性（字段计数为 0），只有 `index`。 | `index` 与 drawing order 是否对应 `UNKNOWN`。 |
| `hint` | `getHintText()` 提供 hint；`isShowingHintText()` API 26 起提供 hint 标识。 | corpus 没有 `hint` 属性（字段计数为 0）。 | legacy XML/UiAutomator 是否导出 hint `UNKNOWN`。 |
| `window` | `AccessibilityWindowInfo` 自 API 21；可读 window id、display id、layer、screen bounds、root；AccessibilityService 需声明 retrieve window content 并设置 `FLAG_RETRIEVE_INTERACTIVE_WINDOWS` 才能读取交互窗口列表。 | 传统 XML 根为 `<hierarchy rotation="0">`，节点没有 window id/display/layer 字段。 | XML acquirer 的多窗口覆盖 `UNKNOWN`。 |

## 3. 仓库 fixture 盘点

### 3.1 真实/审阅资产

- `tests/UniClaw.Kernel.Tests/Perception/Corpus/artifacts/` 有 7 个 XML：`nav03-parent.xml`、`nav03-childa.xml`、`popup01-dialog.xml`、`popup04-page.xml`、`popup09-before.xml`、`scroll01-v1.xml`、`scroll01-v2.xml`。
- 逐文件统计：125 个 `<node>`；每个节点均含 `index,text,resource-id,class,package,content-desc,checkable,checked,clickable,enabled,focusable,focused,scrollable,long-clickable,password,selected,bounds`。
- 这些 XML 的包名为 `com.uniclaw.fixture`，分辨率 bounds 覆盖 1080×1920；NAV/POPUP/SCROLL 场景包含页面、弹窗和滚动状态。
- `scenario-manifest.json` 将 golden-run-v1 标为 `recordedReality`/`reviewed`，设备是 Android 15/API 35 emulator；该 provenance 不外推到 API 28/29/30/34/36。

### 3.2 解析与失败夹具

- `tests/UniClaw.Host.Tests/UiAutomatorDumpTests.cs` 的内嵌 XML 含两个重复 `com.android.settings:id/wifi_switch` 节点、一个 `Network &amp; internet` 文本节点；测试还覆盖 XML entity 解码、bounds 归一化、malformed XML 抛错、空 `<hierarchy>` 无节点结果。
- 同文件的 `checked="partial"` 仅是 parser forward-compatibility 样例，不是真机 API 36 采样。
- `tests/UniClaw.Kernel.Tests/Perception/Corpus/artifacts/*.xml` 没有 `visible-to-user`、`drawing-order`、`hint`、window metadata；没有证据证明缺字段对应任何布尔值。

## 4. Compose、WebView、多窗口、OEM 资料事实

### Compose

官方 Compose 文档描述：Compose composition 旁有独立 semantics tree；semantics tree 不包含 composable 的绘制信息。存在 merged 与 unmerged 两棵语义树；`mergeDescendants=true` 会把后代语义合并，测试框架默认使用 merged tree，而 accessibility services 使用 unmerged tree 并考虑 merge 设置。`clickable`/`toggleable` 等 modifier 可自动合并后代。来源：[Semantics](https://developer.android.com/develop/ui/compose/accessibility/semantics)、[Merging and clearing](https://developer.android.com/develop/ui/compose/accessibility/merging-clearing)、[Compose testing semantics](https://developer.android.com/develop/ui/compose/testing/semantics)。

可核实结论仅到此：同一 Compose UI 可因 merged/unmerged 选择呈现不同节点粒度；它不是传统 View hierarchy 的一一镜像。如何纳入 compatibility model、是否保留某种来源标记，`UNKNOWN`。

### WebView

仓库有 perception report frame 中的 `WebView Shell` 文本（例如 `platforms/perception/evaluation/reports/fsv001-v2/frames/*.json`），但没有配对 WebView accessibility tree/XML 与 API/OEM 元数据。是否存在 DOM→AccessibilityNodeInfo 映射、哪些字段可见、跨 WebView 版本差异，当前 `UNKNOWN`；不能以这些 OCR/perception JSON 推断 hierarchy 字段能力。

### 多窗口

官方 `AccessibilityWindowInfo` 将屏幕内容描述为一个或多个 window 的集合，window 可有 parent/child；API 提供 display id、layer、screen bounds、root。AccessibilityService 获取全部交互窗口依赖 `canRetrieveWindowContent` 元数据和 `FLAG_RETRIEVE_INTERACTIVE_WINDOWS`。Android 官方多窗口文档说明 API 24–30 可通过 `resizeableActivity` 影响 multi-window，API 31+ 大屏设备普遍支持 multi-window。[Window API](https://developer.android.com/reference/android/view/accessibility/AccessibilityWindowInfo)、[AccessibilityService flag](https://developer.android.com/reference/android/accessibilityservice/AccessibilityServiceInfo)、[multi-window](https://developer.android.com/develop/ui/views/layout/support-multi-window-mode)。仓库 XML 无 window 字段，因此实际 dump 覆盖 `UNKNOWN`。

### OEM 差异

本仓库没有同一场景跨 OEM、同 API level 的 paired XML/AccessibilityNodeInfo 采样，也没有可引用的 OEM 行为矩阵。OEM 对属性省略、包名/class、Compose/WebView bridge、window layering 的影响全部标为 `UNKNOWN`；不得从 `com.android.settings` 或 `com.uniclaw.fixture` 单一来源外推。

## 5. 能力矩阵（机械覆盖，不是 Product 语义）

| 能力/字段 | API 28–29 | API 30–34 | API 35 | API 36+ | 当前证据状态 |
|---|---|---|---|---|---|
| 基础节点属性（text/id/class/package/content-desc、boolean 状态/能力、screen bounds） | API reference 方法均已存在 | 同左 | 真实 XML corpus 有样本 | 同左，另有新 API | API 方法存在；逐 acquirer 输出 `UNKNOWN` |
| `visible-to-user` | API 方法存在，但 API 16–29 放大已知 caveat | 方法存在 | XML 未导出 | 方法存在 | 能力存在；XML 序列化 `UNKNOWN` |
| `drawing-order` | API reference 方法 | 同左 | XML 未导出 | 同左 | wrapper/XML 输出 `UNKNOWN` |
| `hint` / showing-hint | hint API；showing hint API 26+ | 同左 | XML 未导出 | 同左 | wrapper/XML 输出 `UNKNOWN` |
| window/display/layer | AccessibilityWindowInfo API 21+；Service flag | 同左 | XML 未导出 | 同左 | XML acquirer coverage `UNKNOWN` |
| checked tri-state | `isChecked()` boolean | `isChecked()` boolean | API 35 XML 实样为 boolean 资产；无 partial 实样 | `getChecked()` + FALSE/TRUE/PARTIAL API 36 | 仅 API 36 richer API 明确支持三态；旧通道如何表达 `UNKNOWN` |
| checked boolean exact / collapsed | `checkedBooleanExact` 不得从 AndroidApiLevel、checked attribute presence、class / role name alone、historical absence of Partial 或 empirical samples alone 推断；必须同时有 two-state contract、adapter capability metadata 的 Exact 声明和可追溯 contract / fixture evidence；否则为 `checkedBooleanCollapsed` | 传统 XML 仅证明 `true/false` 字段形状，不证明 domain 没有 Partial | API 35 fixture 仍是 boolean | API 36 richer source 可提供 tri-state | false→Unchecked 需要三项 exact proof；collapsed false→Unknown(`partial-unrepresentable`) |
| Compose semantics merged/unmerged | 取决于 app/Compose library，不由 Android API band 单独决定 | 同左 | 同左 | 同左 | 官方语义树事实；采集器输出对应关系 `UNKNOWN` |
| WebView | 版本/bridge 相关 | 同左 | 仓库只见 OCR 文本 | 同左 | `UNKNOWN` |
| multi-window | AccessibilityWindowInfo 能力早于 API 28；系统 policy 另有版本变化 | API 31+ 大屏 policy 变化 | XML 无 window metadata | 同左 | runtime window capture `UNKNOWN` |

## 6. v0.1.1 checked capability mapping（设计机械约束）

- `checkedTriState`：source 能完整表达 `Checked/Unchecked/Partial` 时按原值映射。
- `checkedBooleanExact`：仅当当前 semantic claim domain 有明确 two-state contract、adapter capability metadata 明确声明 Exact、且有可追溯 contract / fixture evidence 三项同时成立时，`true→Checked`、`false→Unchecked`；不得从 API level、checked attribute presence、class / role name alone、historical absence of Partial 或 empirical samples alone 推断。
- `checkedBooleanCollapsed`：只能表达 lossy boolean；`true→Checked`，而 `false` 在可能存在 Partial 且 acquisition 无法表达时必须为 `Unknown(reason=partial-unrepresentable)`，不得输出 `Unchecked`。
- Runtime 判断顺序固定为 `capability > acquirer > Android API level`；API level 只辅助选择 adapter，不能直接推出 claim authority。

## 7. 明确未核实项（保持 UNKNOWN）

1. API 28、29、30、34、36 各自真实 `uiautomator dump` 样本及 OEM 对字段省略/重命名/排序的差异。
2. `visible-to-user`、`drawing-order`、`hint`、window/display/layer 在传统 XML 中是否可导出，以及 AndroidX UiAutomator 各版本 wrapper 的等价字段。
3. API 36 `getChecked()` 三态经 UiAutomator/XML 的传输形态；现有 `partial` 只是 parser fixture。
4. Compose merged/unmerged semantics 经过 AccessibilityNodeInfo、UiAutomator XML、Compose test API 的具体映射与版本矩阵。
5. WebView DOM/accessibility bridge 的字段覆盖与版本/OEM矩阵。
6. multi-window、浮层、跨 display 的配对 screenshot + hierarchy capture 及时间/窗口关联事实。
7. OEM 差异数据；当前没有可将单一 emulator/fixture 结果推广为平台兼容保证的证据。

## 8. 证据路径索引

- `tests/UniClaw.Kernel.Tests/Perception/Corpus/artifacts/*.xml`：7 个 hierarchy XML 夹具。
- `tests/UniClaw.Kernel.Tests/Perception/Corpus/artifacts/scenario-manifest.json`：API 35 emulator provenance。
- `tests/UniClaw.Host.Tests/UiAutomatorDumpTests.cs`：解析字段、entity、bounds、duplicate、empty/malformed、partial forward-compat fixture。
- `changes/PER-009/state.md`、`changes/PER-009/mechanism.md`：既有 XML producer 的字段事实与 API 36 checked 研究记录；本 inventory 不把其中裁决转写为新的 PER-010 authority 规则。
- `platforms/perception/evaluation/reports/fsv001-v2/frames/*.json`：WebView Shell OCR/perception 文本；不含 hierarchy 能力证明。
