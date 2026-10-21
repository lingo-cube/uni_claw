# UniAgent Workbench 如何借鉴 Agnes Harness

日期：2026-10-21。状态：Research / Authority NONE。本文提供设计与实现参考，不授权实现、不冻结接口、不决定更换宿主或前端框架。对应 Change：[WORKBENCH-AGNES-RESEARCH-001](../../changes/WORKBENCH-AGNES-RESEARCH-001/state.md)。

## 1. 结论

**值得借鉴，优先借鉴“事件如何成为稳定、可交互的产品视图”，再借鉴消息卡片和工作台布局。保留现有 Workspace Core、Controller、ViewModel 与 Host adapter 的分层，逐步改善即可；本轮没有发现必须迁移到 Agnes 才能实现这些体验的理由。**

UniAgent 当前不是没有对话投影：已经有请求、决策、提交的回合分组，独立 Runtime 执行结果、Trace、Evidence、Metadata，以及宿主无关的查询与渲染层。主要问题是部分语义在归一化时丢失，持续同步和局部渲染基础较弱；默认界面也更接近运行记录查看器，尚未充分表达“用户目标 → Agent 选择 → 现实执行 → 结果证明”。[L1][L2][L3][L4]

| 优先级 | 借鉴内容 | 对 UniAgent 的直接价值 | 采用方式 |
| --- | --- | --- | --- |
| P0 | 类型化节点、稳定身份、来源与关联 | 避免把提交回执显示成决策，把宿主状态显示成产品结果 | 保留本项目语义，参考 Agnes 节点协议的组织方式 |
| P0 | 同一读取快照拆分多个视图 | 避免四次完整详情读取与跨版本拼接 | 先改现有 adapter 的读取组织，无需先建事件总线 |
| P1 | 有界 opening、patch、history、resync | 长任务能持续更新、翻旧记录，重连可恢复 | 复用现有 Runtime event cursor 基础，再补 UI 投影合同 |
| P1 | 按节点种类渲染，正文/工具/错误/附件分工 | 让任务过程可读、截图可定位、错误可追查 | 先用现有渲染栈拆纯函数与局部更新，不先换框架 |
| P1 | 对话主区 + 可收起详情区、保留阅读位置 | 降低日志与配置占据首屏的比例 | 复用现有布局与详情能力，重新安排默认展示 |
| P2 | 受限的消息内扩展槽 | 专用计划、覆盖率、设备截图可由独立 renderer 展示 | 有第二个真实 renderer buyer 后再冻结通用扩展缝 |

上游定位与版本见 [A0]；详细上游调用链与实现锚点见第 4 节。表中收益是工程判断，不是性能实测或迁移成本估算。

## 2. 调研范围和证据边界

- Agnes：官方 `AgnesAI-Labs/agnes-harness`，本机 `/Users/fran/Documents/Code/agnes-harness`；HEAD 为 `98bc11572ba162bcdf6ce2b29612a44508faf951`，工作区 clean。2026-10-21 用 `git ls-remote` 核对官方 main，返回同一 revision。上游引用固定 revision，避免后续漂移。[A0]
- UniClaw：`uni-harness`，HEAD `bce0ec3a981a4598b889bb0bf37d2b3e389aa41c`，以**本轮 dirty working tree**为准；不能把当前文件内容归于 HEAD 的已提交版本。
- 浏览器：只读现有 `127.0.0.1:3080` Workbench，打开既有 Android Settings 历史运行；页面有 3 个对话回合。未发起任务、调用模型/设备、启动或修改服务。部署 bundle 与当前源码未做 hash 一致性认证，因此页面观察与源码探针是两份独立证据。
- 浏览器 DOM/语义快照读取成功；`Page.captureScreenshot` 超时，未取得像素截图，**没有完成视觉精修、响应式或像素级验收**。Agnes UI 本轮未启动，布局与交互判断来自源码、官方指南及测试断言。
- 本地 4 组已有测试实际运行 72/72 通过；两项最小探针复核了回执覆盖理由和四次详情读取。没有运行 Agnes 测试、产品 Runtime 回归或真实端到端任务。[E1][E2][E3][E4]

## 3. UniAgent 现在如何形成界面，具体缺口在哪里

### 3.1 当前调用链与可保留基础

```text
DSH sessionQuery.readSession(hostSessionId) + 显式关联的 Runtime artifacts
  → task-workbench/index.js: sessionDetailCore
  → uniAgentProjection / uniAgentGroupProjection / runStagesProjection
  → panel.session：conversationGroups、conversation、runStages、三类 Trace、Evidence
  → browser/entry.js: createCapabilities（宿主响应归一化与身份映射）
  → WorkspaceQueryCore → workspace-controller → workspace-view-model
  → render-workspace-html → mount → container.innerHTML
```

这是现有 DSH 浏览器入口的实际链路；另有 `workspace-capabilities.js` 与 Runtime HTTP adapter，不应把一个入口的限制扩大为全部后端都没有相应能力。`WorkspaceQueryCore` 已有来源、错误和 revision 防旧读；Controller 已有选择失效与异步请求 token 隔离；布局已支持任务栏伸缩、收起和持久化。[L1][L2][L3][L5][L6]

### 3.2 已复核的呈现与投影问题

| 当前代码行为 | 用户看到什么/实际风险 | 建议 |
| --- | --- | --- |
| `normalizeConversationStage.text` 在 `justification` 之前选择 `result.text`；renderer 又优先显示该 text | “Uni-Agent 决策”正文为 `submit_decision accepted`，下一张“提交结果”重复同一句；真实理由被盖住。DOM 与最小探针均复现 | decision 只展示选择/理由/计划，submission 只展示接收结果；不改变事实含义 |
| Host 请求卡有 `screen/runId/decisionId/budgetRemaining`；归一化白名单没有保留这些字段，也没有保留 decision 的 `callId/spec` | 页面难以就地解释“当时看见什么、选了什么、对应哪个调用”；该字段丢失发生在展示读模型中，不等于 Runtime 没有记录 | 让节点携带明确 correlation 与按需详情引用；截图使用受控 artifact resolver |
| 分组遇到请求顺序开启新组，`groupId=round-N`；同组后来的 decision 会覆盖前一个 | 对正常顺序记录够用；历史窗口前插、重复/迟到请求、多个决策时，不能据此保证稳定身份或因果正确 | 优先用真实 DecisionId 和 consultation correlation 归组；只有源确实缺失时用带标记的 orphan |
| `conversationRounds` 有 stages 时直接使用 stages；Host 的 stages 只放 request/decision/submission | extras 中的其他对话卡可能不进入主线；不是所有 assistant 文本都应显示，但省略应是显式展示策略 | 按消息来源和用途定义 visibility，保留可追溯入口；不直接 dump 宿主 prompt |
| 当前 `decisionFromCall` 主要提取 `proposal.steps`；正式 Plan 使用 `proposal.items`，并有 `PlanExecutionView` | 新式 Plan 可能只剩 kind/理由，嵌套计划及执行位置无法靠现有 steps 列表表达；这是静态字段核查，未运行真实 Plan 工作台场景 | 计划正文取正式 Plan 结构，进度取 Runtime 的执行投影，不能从模型文字补进度 |
| refresh 并发请求 session/timeline/traces/evidence；DSH browser adapter 分别调用同一个完整 `remote.session` | 最小计数探针得到 4 次；同一 session 的不同读取可能来自不同快照，且重复扫描/投影。尚未测定真实耗时 | 一次 refresh 获取共享快照，再拆成四个 pane；同一 generation 内去重，下一次 refresh 失效 |
| adapter 可为每次响应加客户端 `observedAt`；Core 只比较数值 revision 的大小 | 记录了观察时间，但不代表后端事实版本或跨 pane 一致性 | 读取时间与 Owner revision/cursor 分开，不以时间戳代替一致性 token |
| 每次 publish 重新生成全页 HTML，mount 整体替换 `innerHTML` | 持续更新下有焦点、折叠、文本选择与滚动位置丢失风险；本轮未复现所有交互后果 | 稳定 node ID、局部更新、会话独立的阅读状态；先有行为测试再选渲染技术 |

来源：[L1]–[L7]；复现证据：[E1][E2][E3]。其中“可能/风险”是根据代码推断，并非本轮已经观测到的全部故障。

### 3.3 借鉴前必须保留的一条边界

现有代码已经明确“提交成功不等于动作成功”，这是应该保留的设计。另发现 `runStagesProjection` 在没有 completion anchors/coverage 时会退到 UniFlow `VERIFIED/VERIFICATION_FAILED`，生成名为“验证结果”的 stage。它带有 `source: UniFlow trace`，但通用绿色验证标签仍可能被理解为产品已验证。[L1][L8]

后续设计应把“开发流程验证”“Runtime 效果证明”“Goal 满意度评价”分别呈现。不能为了补齐界面卡片，把其中一种当成另一种的后备证据。本轮只记录这个静态发现，没有修改现有判定或追认任何历史 PASS。[L8]

## 4. Agnes 的对应设计与实现

### 4.1 事件先形成 UI 读模型，浏览器不重复解释原始事件

```text
已提交 Core ledger → UIProjectionCell / Session UI projection
  → daemon 的 opening / patch / history RPC（连接基线与游标）
  → SDK UIProjectionSync（事件唤醒同步、连续性校验、重开）
  → Web createLiveProjection（短期 preview 叠层）
  → TimelineNodeHost → ConversationMessages / 专用卡片 / 受限 slots
```

Core 的 `UIProjectionCell` 从 committed ledger 重建，后续在 post-commit callback 应用；daemon 明确只比较 Core 产生的 timeline、不自行发明 UI 语义。这种“源事实 → 读模型 → 渲染”的链路可以借鉴；**UniClaw 的 UI 读模型仍须分别从 Runtime/UniAgent/Host Owner 消费事实，不能新增一个吞并所有产品 Authority 的 UI Core。**[A1][A2]

协议的 `UINode` 是按 kind 区分的联合；assistant 的 text/thinking、tool 的状态与结果、approval、artifact 等有各自字段；`UITurn.nodeIds` 定义归属，turn error 与 tool failure 分开。patch 是带 ID 与位置的 upsert/remove，能更新旧节点，不是只追加文字。[A3]

**对本项目的价值**：保留 decision/receipt/attempt/verdict 的不同字段与身份，所有入口消费同一个读模型版本；renderer 不从 `accepted` 文本猜“执行成功”，也不按前后相邻推断因果关系。

### 4.2 有界历史是同步协议的一部分

Agnes opening 同时限制节点数与序列化字节，返回 fixed cut（本次历史读取的截止点）、窗口坐标和绑定 session/generation/surface 的签名 cursor。history 在同一个 cut 上向前翻页；patch 对应当前 generation 与读取水位。窗口 baseline 缺失时返回 `UI_PROJECTION_RESYNC_REQUIRED`，而不是退化成无界全读。[A2][A3]

SDK 的 `UIProjectionSync` 校验 opening/patch/history 的连续性；事件用于标记“有更新”，经 debounce 和单飞拉取补丁；失配、gap、重连等走重新 opening，旧在途结果不能继续拼入新基线。Web 配置 opening 最多 500 nodes、history 每页 100，这只是上游策略，不是给 UniAgent 推荐的固定数字。[A4][A5]

**对本项目的价值**：先统一当前一次读取的快照，再利用 Runtime HTTP 已有 event cursor 补充有界 UI 同步。原始 event cursor 与 UI 节点窗口 cursor 不是同一个合同，不能直接改名等同；只有正确关联事实版本后，才谈补丁和历史前插。游标要绑定什么由真实查询边界决定，无需照搬 Agnes 的每连接 32 个 baseline、签名算法或 surface 枚举。[L6][A2]

### 4.3 流式显示与正式结果分开

`createLiveProjection` 在正式 timeline 上用 `PreviewMerger` 叠加流式内容；preview 不触发逐 token 重做完整投影。重连时可短暂保留已经显示的文字，等新预览或正式结果替换；最终消息与迟到 preview 的覆盖规则有对应测试断言。上游关联使用 `effectId`，它是 Agnes inference/preview 的身份语义，不能直接映射成本项目现实 Effect/Attempt。[A5]

**对本项目的价值**：先把正式决策与实际执行结果投影正确。以后若展示生成中的文字，明确标为“生成中”，用真实模型调用身份关联；尚未接受的候选内容不显示成已采纳决策，也不生成执行成功状态。本项目不必为第一版先增加 token 预览通道。

### 4.4 消息、附件与详情由专用 renderer 承担

真实 Web 路径中，`conversation-message-adapter.tsx` 把消息交给 `ConversationMessages`，提供 Markdown、工具卡、turn actions、插件槽与附件 reader。assistant 正文与 thinking 分区；工具状态、审批与错误各有对应字段/呈现，参数与结果可以逐层查看。[A6][A7]

需要特别区分两个源码面：`web-units/conversation/attachments.ts` 是一个隐藏的预留扩展槽，**并不意味着 Agnes 没有消息附件界面**。`web-ui/conversation/messages.tsx` 已在 user 的 ContentBlock 中处理图片缩略图/预览组、引用图片的读取与 unavailable 状态，以及文件名称/MIME 列表；附件不是独立 `attachment UINode`。因此可以参考“消息内附件 + 按引用读取”，但不能把该空槽当成完整附件渲染器直接复制。[A6]

对 UniAgent，设备截图首先是带观察时刻、页面和 Run/Decision 关联的 Evidence 引用，不等同于用户上传图片。缩略图、原图和读取失败可以借鉴；证据关联及权限继续走本项目 resolver。错误、审批都不能仅靠渲染一个按钮就视为后端生命周期已经实现。[L8][A6][A7]

### 4.5 工作台外壳、局部更新与长记录

Agnes 外壳把 session navigation、主区、conversation/trace tab、可隐藏 rightbar 与 composer 分成明确区域；trace/rightbar 初始隐藏。窄屏侧栏处理 inert、Escape、焦点返回和 transcript 阅读位置。这更值得借鉴的是“默认主区聚焦任务，管理和扩展按需打开”，而不是复制颜色和间距。[A8]

`TimelineNodeHost` 使用稳定节点/turn 身份更新 store；处理接近底部才自动跟随、向上阅读出现新内容按钮、前插历史保持相对阅读位置、一次 earlier 加载以及 session reset 后旧请求失效。相应测试包含 article、手动展开、焦点/选区保持等断言。[A9]

高密度 Trace 另有固定行高虚拟窗口；**不能据此宣称普通 Markdown 对话也已具备任意可变高度虚拟化**。对 UniAgent，先有界首屏与分页，再依据真实长任务测量是否需要对话虚拟化。[A9]

扩展通过声明的 slot 和 owner claim 进入，缺插件有 native/fallback；移除时 dispose。可参考 renderer 隔离和 fallback，但不要把 Agnes/DSH 专用 slot 名称、DOM 标记与服务注入合同搬进共享产品协议。[A7]

上述均为固定源码与测试断言核查；本轮未运行上游测试，没有 Agnes 浏览器流畅度、实际内存或读屏通过证据。

## 5. 建议的 UniAgent 对话与界面形态

### 5.1 主线按产品因果组织，细节按用途展开

```text
任务目标/约束
  → 本轮观察摘要（页面与证据时刻）
  → UniAgent 决策（选择、理由、有界计划）
  → 决策接收状态
  → Runtime 执行（动作/Attempt/回执/中断）
  → 当前观察验证与 Runtime Outcome
  → UniAgent 对 Goal 满意度的评价（如已有）
```

这是候选展示结构，不是增加七个 Runtime 状态。每一项只在对应事实存在时展示；不要求每轮都有所有节点，也不强行把所有 Runtime 动作等同于一次模型工具调用。一次 Plan 可对应多次本地动作，应折叠为“计划卡 → 当前执行位置 → 按需展开 attempts”。[L7][L8]

具体示例：“确认 Wi-Fi 已开启并保持开启”。主线应表达“看到设置首页 → 选择进入网络与互联网，理由是目标控件在其下 → 决策已接收 → 实际点击、重新观察 → 确认 Wi-Fi 开启”。`submit_decision accepted` 只占接收状态；原始 JSON、token、模型参数、底层事件放详情。若只确认收到回执，则只显示回执，不补“Wi-Fi 已开启”。

建议的最小节点信息分三组，不在本轮冻结 schema：

- **身份与来源**：稳定 node ID、Product Session、Run、真实 consultation/Decision/Attempt 关联、Owner source、源 revision；缺失的键保持缺失，不由 UI 伪造。
- **可读内容**：kind、简短标题/正文、状态与阶段、完整内容的受控引用；候选计划、流式预览与正式接受结果在语义上分开。
- **详情和关联**：原始事件引用、截图/证据引用、plan item/attempt 链接；展示权限由已有查询边界执法。

投影可以持久缓存，但必须可从 Owner 事实重建；刷新/断连不产生新 Run，不重新提交决策，不触发动作，不执行 Goal 评价。流式文本只能作为预览；收到正式事实后按明确关联替换。参考 Agnes 的投影思想，事实权威仍遵循本项目基线。[L8]

### 5.2 默认布局建议

```text
┌任务/历史导航┬────────────── 当前任务标题与运行状态 ──────────────┐
│项目、任务   │                                                 │
│状态、更新时间│  对话与执行过程主区          │ 按需打开的详情区    │
│            │  用户目标、观察摘要          │ 选中决策/动作/证据   │
│可收起       │  决策卡、计划卡、执行卡      │ 截图、计划进度       │
│            │  结果与验证卡                │ Trace / 原始事件     │
│            │  新内容提示 / 回到最新       │ 来源与关联信息       │
└────────────┴─────────────────────────────┴───────────────────┘
```

这是一张信息架构草图，不是已实现/视觉验收的界面。借鉴重点：

1. **主区优先回答“现在做什么、为什么、结果如何”**。现有 Trace/Evidence/工具和 Metadata 可继续使用，但默认不要同时把整片技术详情铺在对话上下与侧面。
2. **点击节点定位同一个事实**。选中决策时打开决策依据；选中动作打开前后截图、回执、验证与对应 Trace。详情可在桌面固定侧区，在窄屏用抽屉；关闭后回到原阅读位置。
3. **轻量主线，逐层展开**。普通成功调用一行摘要，失败/审批/UnknownOutcome 明确突出；原始输入输出默认折叠，错误原因保持可见。思考内容只有源实际提供且允许展示时才出现，可折叠；不把它当决策证明。
4. **长记录尊重阅读位置**。用户在底部才自动跟随；向上阅读时提示新内容；加载历史不跳动；切换任务保存各自滚动与展开项。不能靠每次追加后无条件滚到底实现。
5. **状态分开**。网络已断开、模型处理中、决策已接收、Run 正在执行、取消请求已发出、实际终止、效果未知各有文字，不合并成一个 loading/completed。
6. **输入和操作跟随已授权产品缝**。本轮不建议直接加入通用聊天 composer、“立即发送”或任意重试按钮。补充需求、停止、继续、审批均须映射既有产品命令；没有命令能力时显示只读状态。

### 5.3 机制可借鉴，代码直接搬入需谨慎

| 对象 | 判断 | 原因 |
| --- | --- | --- |
| bounded opening/patch/history、stable ID、preview 分离 | 优先参考设计并适配 | 通用展示同步机制，不要求替换产品 Owner |
| 工具折叠、Markdown、附件、滚动策略、设计 token | 可逐项参考 | 需要沿用本项目 artifact/权限/来源；不能放宽现有转义与只读边界 |
| `@agnes/web-ui`/`web-units` 整包引入 | 先做依赖与协议兼容评估 | 当前上游存在 workspace/private 依赖链；本地是无构建 Client + CommonJS/纯 HTML 路径，并非可直接替换同接口包 |
| React、assistant-ui、XMarkdown 等前端栈 | 暂无必须迁移的证据 | 类型化节点与局部渲染的行为目标可以先独立于框架验证 |
| Agnes session/turn/tool 成功模型 | 不能直接充当 Product Run/Decision/Attempt/Outcome | 同名“工具调用”并不代表同一产品语义；计划和现实动作的对应也不一定一对一 |
| follow-up 队列、停止重发、审批交互 | 保留为产品设计候选 | 这些按钮会改变任务语义，必须先确认产品命令、并发、幂等和终止合同 |
| 插件可插 UI 槽 | 有真实 buyer 后引入最小缝 | renderer 只能展示与发出受控意图，不能拿到写 Owner 状态或直接发设备 effect 的旁路 |

## 6. 后续落地顺序与验收建议（未实施）

| 切片 | 主要修改点 | 可判定验收 |
| --- | --- | --- |
| 1. 修正读模型与卡片语义（P0） | `index.js` 的投影、`workspace-view-model.js`、`render-workspace-html.js` | 同时有 justification 与 accepted 回执时，理由在决策卡、回执在提交卡；缺证据时无成功标签；screen/Decision/call 引用不丢；Plan items 可读；跨 ID 不串组 |
| 2. 同一快照服务各 pane（P0） | `browser/entry.js` adapter 与 Controller 的 refresh | 同一 refresh 中四个读取共用一次详情与同一源版本；刷新失效；切任务丢弃迟到响应；部分来源不可用仍明确标识。先用计数与错序响应测试验证，再测真实延迟 |
| 3. 类型化节点和消息 renderer（P1） | 从现有 renderer 拆出只读消息/计划/动作/验证/附件 renderer | 任一节点可定位原始来源和详情；原始 JSON 不占默认正文；UnknownOutcome/拒绝/失败可辨；渲染失败有局部 fallback，不破坏整页 |
| 4. 长记录和增量同步（P1） | Owner read projection + Host adapter + Core/Controller；复用 Runtime event cursor | 固定事实的全量 fold 与 opening+patch+history 得到同一逻辑视图；重复/乱序/gap/重连/源 generation 变化不重复、不漏；客户端预览不能变为正式结果；首屏有字节/节点预算，历史不做无界全读 |
| 5. 主区/详情/滚动体验（P1） | 局部 DOM 更新、layout、样式与受控操作 | 长对话上翻时新内容不抢位置；历史前插锚点稳定；任务间状态隔离；窄屏可读、键盘可操作、焦点可返回；用真实截图另行视觉验收 |
| 6. 专用 renderer 扩展（P2，可选） | 真实截图/计划/覆盖率 buyer 的最小 renderer registry | 未知 kind 使用通用 fallback；卸载后可读；renderer 异常被隔离；不增加设备执行入口、不改变 Owner verdict |

建议先以现有历史运行和确定性 fixtures 完成前两片，再选一个只读实时任务验证同步。框架迁移、Agnes realization 和产品命令变更另行决策；不是这些切片的隐含前置条件。

## 7. 验证与来源导航

### 7.1 本轮已做的验证

| method | expected | actual | evidence |
| --- | --- | --- | --- |
| Agnes 本地 HEAD/status 与官方远端 main 对照 | 固定、可追溯源码 | HEAD 与远端同为 `98bc115…`，本地 clean | verification.json，固定上游链接 |
| 决策呈现最小探针 | 核对回执与理由优先级 | justification 存在，但 text 选择回执，渲染不包含理由；decisionId/callId 也未进入归一化 stage | [E1] |
| adapter 四种读取计数探针 | 核对实际详情读取次数 | `remote.session` 4 次；未测性能耗时 | [E2] |
| 只读浏览现有 Workbench | 核对页面结构及已加载历史 | 3 回合，决策与提交均显示 accepted；DOM 成功，像素截图超时 | [E3] |
| 4 组已有本地测试 | 识别现有测试基线 | 72/72 pass；这不覆盖新建议或以上缺口 | [E4] |
| 链接、来源文件与修改范围 | 新文档可导航；仅新增研究文件 | 最终结果见 verification.json | [E5] |

本轮研究核查等级为 CONTRACT，附两项 DETERMINISTIC 探针与一次现有服务的浏览器只读观察；不构成新功能的 ENVIRONMENT 验收。

范围核查：新增内容为本报告、独立 Change State 与本轮 evidence 目录。研究期间既有 tracked diff 指纹发生变化，status 新增了其他并行任务对 `changes/RUNTIME-IDENTITY-001/state.md` 和 `src/UniClaw.Agent.Dsh/README.md` 的修改。本轮未写入这两处，也没有回退它们；因此不声称全工作区前后内容不变。已有未跟踪文件未逐个计算内容指纹。

### 7.2 本地来源

- [L1] [DSH 投影与分组、运行阶段](../../dsh/uniclaw-task-workbench/src/index.js#L838)、[详情聚合](../../dsh/uniclaw-task-workbench/src/index.js#L1043)。
- [L2] [ViewModel 与 stage 归一化](../../web/uniclaw-workspace/src/features/workspace-view-model.js#L117)、[HTML 呈现](../../web/uniclaw-workspace/src/ui/render-workspace-html.js#L22)。
- [L3] [Controller 读取与 refresh](../../web/uniclaw-workspace/src/features/workspace-controller.js#L223)、[App 发布链](../../web/uniclaw-workspace/src/app/create-workspace-app.js#L25)。
- [L4] [现有 Workbench 说明](../../dsh/uniclaw-task-workbench/README.md)。
- [L5] [DSH Browser adapter](../../web/uniclaw-workspace/src/browser/entry.js#L97)、[整体 mount](../../web/uniclaw-workspace/src/browser/entry.js#L169)、[另一条 DSH capability 装配](../../dsh/uniclaw-task-workbench/src/workspace-capabilities.js#L115)。
- [L6] [查询 Core 防旧读](../../web/uniclaw-workspace/src/core/index.js#L193)、[布局行为](../../web/uniclaw-workspace/src/ui/workspace-layout.js)、[Runtime HTTP events cursor](../../web/uniclaw-workspace/src/adapters/runtime-http.js#L45)。
- [L7] [正式 AgentPlanProposal](../../src/UniClaw.Kernel/Runtime/AgentDecision.cs#L276)、[Plan 执行投影](../../src/UniClaw.Kernel/Runtime/PlanExecution.cs#L25)、[正式 decision codec](../../src/UniClaw.Agent.Dsh/DecisionJsonConverters.cs#L161)。
- [L8] [产品架构与事实 Owner](../architecture/product-architecture-baseline-l0-l3.md#L90)、[UniAgent realization 边界](../architecture/uniagent-realization-baseline-v0.1.md#L20)。
- [L9] [现有工作台测试](../../dsh/uniclaw-task-workbench/tests/workbench.test.mjs#L632)、[renderer 测试](../../web/uniclaw-workspace/tests/workspace-renderer.test.js)、[view model 测试](../../web/uniclaw-workspace/tests/workspace-view-model.test.js)。

### 7.3 上游来源

- [A0] [Agnes 官方仓库](https://github.com/AgnesAI-Labs/agnes-harness)、[固定版本 Web 指南](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/docs/guide/web.zh-CN.md)。

- [A1] [Core UIProjectionCell](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/core/src/project/ui.ts#L193)：committed ledger 的派生投影。
- [A2] [daemon 投影、baseline、cursor 与窗口化 RPC](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/daemon/src/local/methods/agnes.ts#L162)：diff 在 L287，opening/history/patch 在 L917-L1166。
- [A3] [UI timeline 与 patch 协议](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/protocol/gen/ts/agnes-v1.ts#L94)：节点联合、turn、window、patch/replace。
- [A4] [SDK 同步器](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/sdk/src/ui-projection-sync.ts#L532)：opening、patch、history 连续性与重开。
- [A5] [Web live projection 与 preview](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/web/src/live-projection.ts#L11)：首屏/历史预算与临时预览。
- [A6] [消息 adapter](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/web/src/conversation-message-adapter.tsx#L120)：配套 messages 和占位槽见下。
- [A7] [Conversation 与扩展槽](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/web-units/src/conversation.ts#L44)：Web node/turn 与 fallback 行为见测试。
- [A8] [实际 HTML 外壳](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/web/public/index.html#L35)：对应 shell.ts 的侧栏行为。
- [A9] [TimelineNodeHost 与滚动](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/web/src/timeline-node-host.tsx#L24)：普通对话滚动与 Trace 虚拟窗口分别处理。
- 附件与正文实现：[UserMessageImages / UserMessageFiles](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/web-ui/src/conversation/messages.tsx#L228)、[隐藏的 attachments 扩展占位](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/web-units/src/conversation/attachments.ts#L6)。
- 布局与 Trace：[侧栏键盘/焦点行为](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/web/src/shell.ts#L7)、[外壳样式](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/web/public/style.css#L1240)、[Trace 固定行高虚拟窗口](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/web-units/src/trace-virtual-window.ts#L34)。
- 上游测试源码（本轮未运行）：[daemon 窗口/游标](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/daemon/test/session-extras.test.ts#L340)、[SDK 重连与并发](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/sdk/test/ui-projection-sync.test.ts#L249)、[Web preview 合并](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/web/test/live-projection.test.ts#L195)、[node host 渲染/滚动](https://github.com/AgnesAI-Labs/agnes-harness/blob/98bc11572ba162bcdf6ce2b29612a44508faf951/packages/web/test/timeline-node-host.test.ts#L312)。

### 7.4 本轮证据

- 两项探针复现：`node evidence/workbench-agnes-research-20261021/reproduce-probes.cjs`；[复现脚本](../../evidence/workbench-agnes-research-20261021/reproduce-probes.cjs)。
- [E1] [决策显示探针](../../evidence/workbench-agnes-research-20261021/decision-display-probe.json)。
- [E2] [重复详情读取探针](../../evidence/workbench-agnes-research-20261021/session-read-probe.json)。
- [E3] [浏览器观察](../../evidence/workbench-agnes-research-20261021/browser-observation.json)、[现有工作台完整语义快照](../../evidence/workbench-agnes-research-20261021/current-workbench-snapshot.txt)。
- [E4] [本轮 72 项测试输出](../../evidence/workbench-agnes-research-20261021/focused-tests.txt)。执行：`node --test web/uniclaw-workspace/tests/workspace-view-model.test.js web/uniclaw-workspace/tests/workspace-renderer.test.js web/uniclaw-workspace/tests/browser-bridge.test.js dsh/uniclaw-task-workbench/tests/workbench.test.mjs`。
- [E5] [版本、链接与范围核查](../../evidence/workbench-agnes-research-20261021/verification.json)。
