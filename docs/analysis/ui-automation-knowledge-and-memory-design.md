# UI 自动化测试的知识与记忆系统设计分析

> 日期：2026-10-07
> Status: DRAFT（设计梳理/调研，未授权实现）
> Authority: `NONE`（分析产物，不是架构决策；与 ADR / baseline 冲突时以后者为准）
> 状态：需求分析/设计草案，未授权产品实现

## 设计问题

“让 UniAgent 对不同系统做 UI 自动化测试”不是单纯的记忆检索问题。它至少同时需要知道：平台如何暴露 UI、目标系统有哪些稳定语义、当前设备是什么配置、某个任务的成功条件是什么，以及过去的操作经验是否仍然适用。

这些内容不能放入一个没有类型的 Memory 表。规范知识、设备配置、历史策略和当前事实有不同的来源、生命周期和可信度。

## 需要存储的知识

| 知识类 | 例子 | 主要来源 | 是否能直接指导当前动作 |
|---|---|---|---|
| 平台机制知识 | Android accessibility/UI Automator 如何定位节点；iOS XCUI 查询；Windows UI Automation control patterns；WebDriver 元素查找 | 官方规范、SDK 文档 | 只能提供策略候选，仍需当前 UI 验证 |
| 系统语义目录 | Settings 的网络、显示、权限等概念；系统动作、Intent、能力和限制 | 官方系统文档、SDK/API reference | 可生成观察和导航计划 |
| 应用/页面语义 | 包名、页面角色、控件语义、可访问性标识、稳定 selector | 应用元数据、真实 UI hierarchy/DOM、验证运行 | 仅在适用版本和 scope 内使用 |
| 设备/环境配置 | 厂商、型号、API/OS、ROM、语言、分辨率、权限、网络、应用版本 | 设备 profile、启动探针、运行时查询 | 用于筛选知识，不是当前页面事实 |
| 任务与验收知识 | 前置条件、允许 effect、成功/失败条件、需要的 post-action observation | UniClaw contract/scenario | 约束执行与验证 |
| 历史策略 | 某机型的入口别名、滚动策略、已知失败、恢复办法 | 已验证 Run/evidence、人工修订 | 只能作为 prior/hypothesis |
| 当前事实 | 当前前台窗口、节点、checked 状态、权限和网络状态 | 实时 Perception/Evidence | 可以进入当前世界模型，但不应由 Memory 代替 |

最后一类不是普通 Memory。它属于实时 Evidence/WorldBelief 链路；Memory 只能保存其历史引用。

## 官方文档应该承担什么角色

官方文档适合建立“平台机制知识”和“系统语义目录”的初始版本，而不适合直接证明当前设备状态。

- Android 官方 UI Automator 文档描述通过 accessibility window nodes、层级和 selector 查找并操作 UI，适合生成 Android observation/action strategy。[Android UI Automator](https://developer.android.com/training/testing/other-components/ui-automator)
- Apple 的 XCUIAutomation 通过 `XCUIElementQuery` 定义 UI 元素查询，适合建立 iOS 查询能力目录和测试接口知识。[Apple XCUIAutomation](https://developer.apple.com/documentation/XCUIAutomation)
- Windows UI Automation 以属性和 control patterns 暴露控件能力，例如 Invoke、Scroll、Toggle、Value；这应进入 Windows capability catalog，而不是写成某个页面的固定坐标。[Microsoft UI Automation](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-controlpatternsoverview)
- WebDriver 是跨语言、跨平台的浏览器控制协议，定义了元素查找和后续操作的协议语义；Web 场景应优先存 selector 语义和 DOM 关系，而不是截图坐标。[W3C WebDriver](https://www.w3.org/TR/webdriver/)

文档采集结果必须带版本、发布日期、来源 URL、章节定位和适用平台。文档中的“通常”“推荐”应被标记为 guidance；只有规范性 API/状态定义才进入 normative catalog。

## 知识获取流水线

```text
官方文档/SDK
  → 文档快照与版本标识
  → 结构化抽取（能力、状态、selector、限制、示例）
  → 人工/规则审阅
  → Normative Catalog

真实设备启动探针
  → DeviceProfile / RuntimeProfile
  → 当前系统和应用身份

实时 UI 感知
  → UI tree / accessibility tree / DOM / screenshot
  → ObservationProposal
  → accepted Evidence

已验证 Run
  → action/effect/post-action evidence
  → 可复核历史经验候选
  → Memory record（带 provenance、适用范围、失效条件）
```

不要让模型直接把网页内容或一次对话写进长期 Memory。写入应经过抽取、去重、适用范围判断、来源绑定和验证门槛；无法证明的内容只保留为候选或临时上下文。

## 建议的存储分层

### Normative Catalog

保存平台和系统的稳定机制知识：能力名、调用方式、支持状态、版本范围、限制、官方来源。它应是可版本化的文档/结构化目录，不是向量库中的无来源文本。

### Profile Catalog

保存设备和应用的身份与配置：`manufacturer`、`model`、`os/apiLevel`、`rom`、`formFactor`、`locale`、`appPackage`、`appVersion`、`display`、`availableCapabilities`。它用于检索过滤和环境选择，不代表当前 UI 已经处于某状态。

### Strategy Memory

保存已验证的导航先验、selector 候选、失败模式和恢复策略。每条记录必须有 `scope`、`observedAt`、`validUntil`、`provenance`、`confidence`、`supersedes` 和 `safetyClass`。坐标、截图和层级快照只作为 artifact 引用，不作为无期限的事实文本。

### Evidence Archive

保存真实运行中的原始截图、UI hierarchy、DOM、请求/响应、effect receipt 和 post-action observation。它由 Evidence/Run 相关 owner 管理；Memory 可以引用它，但不能把 archive 中的旧事实自动提升为当前事实。

## UniAgent 的召回接口

建议让 UniAgent 消费一个窄而深的 `MemoryRecall` 接口，而不是直接看到某个第三方 MCP 的搜索工具：

```text
recall(
  taskIntent,
  targetSemantic,
  deviceProfile,
  currentContext,
  freshnessPolicy,
  allowedKnowledgeKinds
) → ranked MemoryRecall[]
```

每个返回项至少包括：

```text
recordId, kind, claimOrStrategy, applicability,
provenance, observedAt, validUntil, confidence,
conflicts, supersedes, requiredObservation
```

`deviceProfile` 应由 UniClaw 根据真实设备身份生成，不能让模型自由填写。服务端先做权限和 scope 过滤，再做关键词/全文/向量/图检索。返回结果必须告诉 UniAgent“为什么命中”和“还需要观察什么”。

## 以 Settings 自动化为例

用户目标是“打开 Wi-Fi 并确认已连接”时，知识链应是：

1. 从 Normative Catalog 取得该平台可用的观察和操作能力。
2. 从 Profile Catalog 确认当前 Android API、厂商、Settings 包和语言。
3. 从 Strategy Memory 召回该机型可能的入口和历史失败模式。
4. 由实时 Perception 获取当前 Settings hierarchy。
5. 用 accepted Evidence 选择实际目标 occurrence。
6. 执行受授权 effect。
7. 通过 post-action observation 验证结果。
8. 只有经过复核的导航/恢复经验，才形成新的 Strategy Memory candidate。

记忆提供的是“可能从哪里开始、要注意什么”；它不能直接证明 Wi-Fi 当前已打开。

## 第三方与自建的共通工作

无论使用 DSH 第三方 Memory 还是自建 Memory，都必须先完成：知识分类、来源模型、scope/applicability、写入门槛、召回时机、失效/冲突规则、权限隔离和验收场景。第三方只可替代存储、索引、部分摘要和检索实现。

DSH 适合作为 adapter：它通过 MCP 暴露 memory tools，但 UniClaw 应在 adapter 内把第三方工具映射成稳定的 `memory.recall`、`memory.propose`、`memory.get`。设备 scope 和安全过滤必须在服务端强制执行，不能依赖模型遵守 prompt。

## 第一阶段建议

先不要做全平台通用知识库，也不要先做复杂知识图谱。用 Android Settings 的 Wi-Fi、Display、Permissions 三条场景建立最小纵向切片：

- 官方文档采集一个平台机制目录；
- 启动时生成一个真实 DeviceProfile；
- 真实 UI hierarchy 作为 Evidence；
- 仅保存三类 Strategy Memory：入口先验、selector 候选、失败恢复；
- 用两个 API/厂商组合验证 scope 过滤；
- 用一个升级导致失效的记录验证 supersede/expiry；
- 用一个 Memory 与实时 UI 冲突的场景验证 Evidence 优先。

这个切片通过后，再决定是否需要 Mem0、Graphiti 或自有混合索引。存储技术不应先于知识分类和验收语义冻结。

## 危险动作知识与 Agent 常识的边界

“Factory reset”“Erase all data”“Remove account”“OEM unlocking”等内容不应只存成 Memory，也不应只写进 system prompt。它们属于 **Action Safety Policy**：动作分类、目标模式、默认处置和升级路径必须由 Kernel/Effect Guard 机械执行。

Agent 可以知道这些词通常危险，但这只能帮助它提出观察或解释，不能作为放行动作的依据。模型可能漏读标签、遇到本地化文本、把相似菜单误判为目标，或者在上下文压缩后忘掉提示；因此安全规则必须是 fail-closed 的 allow/forbidden policy：

```text
candidate action
  → target identity + effect class
  → explicit allowlist / forbidden policy
  → contract and permission check
  → fresh binding and pre-dispatch guard
  → dispatch or safe-stop
```

当前 Settings coverage fixture 已采用这个方向：`observe/scroll/navigate/back` 为 safe action，目标 Wi-Fi toggle 单独允许，`destructive`、`account-removal`、`credential-change`、`developer-debug`、`permission-grant` 和 `unknown-action` 拒绝；未知目标默认 `reject`（`.dsh/profiles/settings-coverage.yaml`、`testsets/android-settings/forbidden-action-policy.json`）。这份 fixture 不是 Memory，它是测试场景的策略输入；产品化时需要把同一语义提升为有 owner、有版本、有验证的安全策略模块。

官方文档和历史经验可以帮助完成三件事：识别平台动作类别、补充不同语言/厂商的危险标签、提供观察路径。它们不能改变默认拒绝，也不能替代当前 target binding。对危险动作，建议分成三层：

1. **类别禁用**：所有 `factory-reset`、`erase-data`、`remove-account` 等 effect class 默认不可用。
2. **目标识别**：将英文、中文、系统本地化词、resource id、accessibility label 映射到危险类别；映射不确定时归 `unknown-action`。
3. **授权升级**：若未来确实需要测试破坏性动作，必须进入独立的显式授权/专用测试 profile，绑定测试设备、数据恢复方案和一次性 contract；普通 UI 遍历运行不能通过临时对话放行。
