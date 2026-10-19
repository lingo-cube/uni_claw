# 从场景库与 UI 定义推导 PlanTree / Traversal（2026-10-19）

状态：候选设计与静态适配审计；不是 accepted 协议，不是实现通过。

## 1. 结论与本次范围

所有者要求：利用 Traversal，将 PlanTree 表达为抽象执行计划；结构必须从场景库和 UI 元素定义反推，并证明场景适配完备。

结论：这个方向符合已有 Traversal 责任，但**现有代码尚不满足适配完备**。菜单遍历适合“动态导航结构＋节点规则＋稳定遍历算法”；完整场景还要求顺序阶段、集合处理、目标态推进和有界等待。它们可以组织为同一产品执行计划中的策略节点，不要求模型逐项手写 If/Repeat/ForEach。具体策略目录先作为场景推导结果，不能一次冻结成全功能平台。

本次核对 150 条推理场景、28 条场景条目、18 个交互需求样例，以及 UI 节点 22 字段和窗口 7 字段。逐项清单无漏项，但**这只证明输入已归类，不证明每项已实现**。新高层 PlanTree 执行验证为 0，结论为 NOT_PROVEN。对当前代码做静态核对，不运行设备、模型或产品测试；保留旧现场结果和门槛，不重标成绩。

本文件沿用 RUNTIME-PLAN-001，进入 RESOLVE 收敛高层表达和验收；不修改 accepted ADR，不改产品实现。本轮候选语义需与既有 Plan 契约显式对齐后才能实施。

## 2. 输入与证据边界

| 输入 | 本轮核实 | 使用方式与限制 |
|---|---|---|
| [场景推理](../../docs/design/scenario-reasoning-v0.1.md) | SR-001～150，13 组、7 个压力轴 | 需求和反例输入；DRAFT/NONE，旧 ✅/D/◐ 不是当前实现结果 |
| [场景库](../../scenarios/README.md)与 SCN-*.json | 28 条；20 golden-bundle、8 execution=none；15 个不同 srRef | 保留每条期望、载体和 realization。JSON passing 与认证存在不代替本次执行 |
| [交互粒度场景](../../docs/design/decision-granularity-scenarios-v0.1.md) | A1～A6、B1～B7、C1～C5 共 18 例 | 补任务形状需求；车机和机械臂推演不冒充已有真实适配 |
| [UI typed observation](../../src/UniClaw.Kernel/Perception/UiHierarchy/UiHierarchyObservation.cs) | UiNodeObservation 22 字段，UiWindowOccurrence 7 字段 | 当前可观察字段定义；父子关系是 capture-local 特征，不等于页面导航关系 |
| [字段认识论](../../src/UniClaw.Kernel/Perception/UiHierarchy/ObservedValue.cs) | Observed / Unknown / Unsupported | 缺失、无能力、false 分开；不能静默补默认值 |
| [UIWorld 元素](../../src/UniClaw.Kernel/World/UiRealization/UiEntityModel.cs)与[关系](../../src/UniClaw.Kernel/World/UiRealization/ContainerAssociation.cs) | Container / revision-local Occurrence / LogicalItem；Contains / Overlays / NavigatesTo | World 承担事实权威；任务树不得重造 identity、coverage truth 或导航事实 |
| [架构基线 §14](../../docs/architecture/product-architecture-baseline-l0-l3.md) | Traversal 管未知结构探索、coverage、推进方向和控制权返回 | 不拥有 Goal/Belief/Completion；Control、Assurance、Effect、Run 分工保留 |
| [ADR-0053](../../docs/adr/0053-composable-product-plan-with-bounded-local-execution.md) | 产品提供声明语义，Agent 组合参数化，Runtime 逐步展开 | 不开放任意代码、不新建 Run 驱动，不自动扩大权限或跨进程恢复承诺 |

可复核产物：[输入快照与逐项 JSON](../../evidence/runtime-plan-001/plantree-derivation-20261019/inventory.json)、[逐项矩阵](../../evidence/runtime-plan-001/plantree-derivation-20261019/scenario-matrix.md)、[离线审计程序](../../evidence/runtime-plan-001/plantree-derivation-20261019/audit.py)。它们固定源文件哈希；输入漂移会使 --check 失败。28 条库条目与 150 条推理、18 例之间有重叠，不相加计算支持率。

## 3. 从 UI 字段反推消费义务

不能把 Class、Clickable、Role 单独当成“导航项”或“允许点击”的证明。分类要消费有来源的语义、作用域、状态和任务授权；发生歧义就保持未知。规则匹配须有明确优先级，冲突不能任取一条。

| 元素/交互族 | 现有定义提供的线索 | 推导出的计划行为 | 还需要证明什么 |
|---|---|---|---|
| 页面/菜单入口 | ParentOccurrenceRef、Package、Text、Class、Clickable；World 导航关系 | Traverse 展开子树，返回父位置 | 只导航的行为语义、安全分类、实际到达页、可返回路径 |
| 折叠区、Tab、局部展开 | Selected、Class、Text、层级与前后观察 | 同页结构继续发现，或经验证的导航 | 展开与状态改变的差别；不能按按钮外观统一处理 |
| 列表/滚动容器 | Scrollable、Bounds、SiblingOrder、前后 revision | 发现候选、去重、继续滚动 | 虚拟列表、嵌套滚动、排序变化、末端证据；一屏无候选不等于穷尽 |
| 开关/复选/单选 | Checkable、Checked、Selected、Enabled | 只读任务记录；明确授权任务推进到目标态 | 不把不同可选控件全部等同二态 switch；partial/未知单列 |
| 输入框/敏感输入 | Focusable、Focused、Hint、Password、Class | 定位字段、输入、读取校验结果 | 当前 typed node 没有完整可编辑操作/输入约束定义；具体输入 driver、遮盖和脱敏待证 |
| 普通/提交/危险按钮 | Clickable、Enabled、Text、Class | 由明确动作语义选择观察、操作或拒绝 | tap 只是物理动作，不能推导 submit/delete/navigate；需目标语义与授权匹配 |
| 数值/滑块/拖拽 | Class、Bounds，可能有外部 claim | Achieve 或有反馈的专门算法 | 当前 node 没有 range/value/action-list 专属字段；连续动作支持也不能从 tap 推导 |
| 弹窗/多窗口/双屏 | WindowOccurrenceRef、Package、Focused、Visible、DrawingOrder、Overlays | 中断受影响分支，补证/恢复或返回 Agent | 不默认点确定/关闭；窗口字段不等于双屏关联已适配 |
| 地图/画布/不可访问控件 | Bounds、空间信息、视觉语义；树可能仅见一个大容器 | 一个具体、受约束的领域策略节点 | 地图状态、手势能力、反馈和终止判据都需具体实现；“有扩展叶子”不算覆盖 |

22 个节点字段及 7 个窗口字段逐一对应的义务在矩阵末节，未把几何、文本或父路径当永久身份。安全敏感信息只需要保留脱敏后的可判定投影，不能为了“信息完备”泄露原值。

## 4. 从任务场景反推 PlanTree 的形状

这里的 PlanTree 是 Product Execution Plan 的候选结构名，不另建与 AgentPlanProposal 并行的产品计划系统，也不是开发 Leader 的 Plan。

| 场景压力 | 必需的执行语义 | 候选高层形状 | 首片处理 |
|---|---|---|---|
| A1、SR-002/003/004/099～108 | 动态发现、进入子树、返回、去重、覆盖缺口 | Traverse：根、来源、节点规则、深度、结束与未知处置 | 真实买方明确，优先落地 |
| A3、B1、B3、SR-008/104 | 有依赖的前后阶段，每步验证后推进 | Sequence：有序子任务 | 复用已有顺序 Plan，避免新同义控制器 |
| B4、C2、C5 | 每个条目有不同绑定/参数，集合可动态更新 | ForEach：候选集合＋条目任务 | 复用现有候选机制；字段等具体适配另证 |
| A2、A4、C1、SCN-POLICY-001～011 | 围绕目标态重复动作，已满足时零动作 | Achieve：目标、动作规则、停滞与上限 | 先核对现有 Policy 能覆盖的语义，不能仅改名代替适配 |
| B7、SR-051/052 | 等待外部可观察条件，未知不能当成功 | Await：条件、观察方式、超时和退出 | 对齐现有 Defer/等待，不能许诺跨进程续行 |
| B5、C3、C4 | 新语义判断或专门反馈算法 | 返回 Agent；有真实买方后才定义具体策略 | 不靠万能 Custom 节点将未支持场景计为通过 |

首片无需把所有候选形状做成新 union 成员。Traversal 作为较深的 Module，隐藏栈、frontier、动态发现、返回、去重和预算内推进；模型只学习范围、规则和参数。既有 If/Repeat/ForEach 可保留为底层组合能力或兼容输入，不作为菜单任务的主要模型接口。

### 4.1 同一计划的公共信息

这些是语义槽位，不是已冻结 wire 字段；能引用既有契约的就引用，不复制完整 policy/store。

| 信息 | 来源与约束 |
|---|---|
| Plan 身份、版本、父子位置 | 复用现有 PlanId/Version/续行语义；改计划不抹掉已完成动作、失败和预算 |
| 任务目标及阶段预期 | Agent 提议，必须关联原 Goal/Contract；不是完成证明 |
| 输入与作用域 | 命名的 Owner 投影/能力及版本、任务根和范围；不携带未来页面的 occurrence/坐标 |
| 策略类型与参数 | 只能选择产品已声明、可验证的语义；未知类型拒绝 |
| 节点规则与子任务 | 匹配条件、处理动作、目标值/记录要求；规则不构成授权 |
| 安全约束引用 | 当前 Contract 与产品策略；计划只能收紧，不能扩权 |
| 前提、预期结果、未知处置 | 由声明的判据和证据引用求值；Unknown 不走否定分支或默认点击 |
| 深度、工作量与停止条件 | 受 Run 授权预算约束；参数不能自行授额 |
| 完成条件与覆盖缺口 | 局部算法结束信号交给原任务证明链；跳过、未知、失败不伪装已检查 |

### 4.2 菜单首片的完整表达

```text
计划：只读检查设置
策略：Traverse
起点与返回位置：任务指定的设置首页
范围：当前设置应用、已授权的可安全到达页面
来源：当前页面的有依据节点及已验证导航关系
节点规则：确认安全的导航项进入子树；普通控件记录；禁止节点记录原因；未知节点按覆盖契约留缺口或暂停
安全约束：引用本Run授权与产品动作策略；每次操作前重新检查
发现规则：枚举可见节点，按已支持滚动方式发现剩余节点；无法证明末端时保留未穷尽
遍历规则：维护当前父路径、已处理项和待处理项；处理完子树后经验证返回；回路不重复展开
深度：以计划根为0的明确定义；与现有Host深度配置的换算必须经适配验收
上限：使用已授权Run预算和局部工作量上限
操作结果：每次现实动作仍需新观察和独立验证；在途动作未对账前不推进或重发
局部结束：没有剩余待处理项，发现范围已交代，且返回起点
任务结束：原任务证明链确认目标满足；有必须覆盖的未知分支则不能报告全覆盖
```

“未知节点可记录并继续”仅适用于显式允许保留覆盖缺口的节点处理规则。它不是把现有 Guard Unknown / Condition Unknown 改成 Else 或 ContinueOnBlocked。当前事实、授权或返回路径未知时，仍按既有暂停/再咨询/停止出口处理。必须覆盖的分支不能通过 skip 获得完成。

### 4.3 谁负责推进

```text
Agent提交/修订Product Execution Plan
→ Runtime校验并保存采纳版本、位置与执行记录
→ Traversal按Owner事实投影选下一处理项，提供控制选择依据
→ Control签发意图
→ 当前目标接地与绑定、Assurance、Effect Gate与唯一投递
→ 新观察、证据接纳、World更新、独立验证
→ Runtime记录结果后推进/暂停；任务完成仍由既有证明与终局链处理
```

这是语义职责链，不指定新类/注册方式。现有 IControlPolicy 只收 ContractView＋Slice；若需要消费任务进度，要显式设计、验证该 seam，不能给 Traversal 一个可随意改写 Run 的回调或让 Host 另起循环。任务的 visited/frontier/stack 属受 Runtime 管理的执行进度或其派生状态，不写成 World 事实；World 只按证据更新页面与关系。

## 5. 150 条场景施加的闭合要求

| 组 | 数量 | 反推约束 | 不能用树结构替代的能力 |
|---|---:|---|---|
| G1 正常任务 | 8 | 顺序、发现、动作与零动作 | 文本输入和返回的具体 effect 支持 |
| G2 感知异常 | 16 | 观察质量/新鲜性、冲突和补证出口 | 假时间检测、错误感知校正能力 |
| G3 绑定歧义 | 12 | 当前、唯一、有依据的目标；禁用/遮挡不可误点 | 具体 grounding、消歧与重定位 |
| G4 权限安全 | 14 | 每步授权、范围、撤销/抢占、终态拒绝 | 授权机制和真实行为分类 |
| G5 可靠执行 | 12 | 在途先对账，迟到、重复、重启分别处理 | 持久化恢复编排，不能由节点 Running 推出 |
| G6 终局证明 | 12 | 遍历结束≠目标满足；receipt成功≠世界改变 | 原 Owner 的义务求值与 Outcome Proof |
| G7 身份连续性 | 14 | 页面图/遍历树分开；返回、滚动、重渲染和跨域 | 证据支持的 identity/continuity 与具体域适配 |
| G8 历史更正 | 10 | 引用当前 revision；历史、冲突和因果保持区别 | Evidence/World 演化能力 |
| G9 遍历决策 | 10 | 动态发现、范围、停滞、预算、准确续行 | 跨进程续行、穷尽证明和恢复适配 |
| G10 激活基数 | 8 | 只在合法 Run 内推进，终态不复活 | Run 生命周期执法 |
| G11 记忆治理 | 6 | 记忆只能是 prior；开发治理单列 | SR-120/122 为 Harness/ADR，不计产品 UI 支持率 |
| G12 对抗完整性 | 8 | 页面内容不能授权，来源与完整性显式 | 哈希不证明采集时间真实；检测能力另证 |
| G13 交叉组合 | 20 | 弹窗×预算、撤销×在途、抢占×投递等组合路径 | 单轴各通过不能直接证明组合无竞态 |

G7 包含文件系统与机器人地图反例，G11 包含非产品 UI 场景；都保留在输入账本，不能删掉后宣称“全场景”。每条具体要求见逐项矩阵。本次不沿用旧文档中“合取即可推出组合无漏洞”的推断，交叉发生顺序与状态共享要用运行轨迹证明。

## 6. 什么才叫“适配完备”

### 6.1 四层判定，分别出结果

1. **表达完备**：对固定范围内每个需求，都能给出无需虚构事实、权限、能力的有限计划，以及正常/未知/拒绝/超时/失败出口；不能仅填一个任意 CustomTask 或“回 Agent”就算自动适配。
2. **适配完备**：计划消费的事实、行为语义、动作能力和验证判据在目标环境有具体实现及证据；字段 Unknown/Unsupported 的处理可验证。只有扩展接口不算已有能力。
3. **执行完备**：完整链路的全部必需分支有可重放轨迹；覆盖身份变化、预算、在途与外部干扰，旧动作不重放，未知不变成成功。
4. **现场适用**：明确设备/应用/版本/Provider/权限和当前能力，按预注册目标行为检验成功率、覆盖缺口、全部消耗及收益。仿真不能替代真实适配，单次成功不能证明稳定。

本轮仅完成静态追溯；不以“无漏项=表达完备”，更不以“表达完备=产品适配完备”。冻结输入清单能证明的是这批需求可核对，不能证明所有未来 UI 都可处理。

### 6.2 菜单 Traversal 的有条件覆盖论证

固定一次目标范围，令 G 为该范围内真实存在、授权允许探索且可经支持操作发现的页面关系；运行时只保留当前发现的部分及一个遍历生成树。G 可以有共享子页、回边；不能要求现实导航天然是一棵树。

必须先成立的前提：

- 范围/最大深度/允许操作的含义固定，任务相关状态稳定，或变化后能使已完成标记失效；
- G 在此次遍历中有限，页面与条目可可靠判同，不能因重复观察无限生成“新页面”；
- 页面内枚举和滚动有完整发现条件；无限信息流或无法证明末端时不能宣称全覆盖；
- 每个需探索的入口能安全执行、验证到达，处理后存在可用的已验证返回/重定位路径；
- 策略公平选择待处理项，动作和验证可最终得到明确结果，预算足以完成这一范围。

在这些前提下，对可达路径长度作归纳：根被处理；任意已处理页面的全部合规入口经完整枚举进入待处理集合；每个待处理入口最终被执行并确认目标页，或判定目标已在相同任务上下文处理。于是下一层可达页面被处理，重复直到有限范围耗尽。visited 阻止环重复展开；返回帧保存剩余兄弟，不遗漏后续入口。局部工作量和 Run 预算负责约束每轮推进，预算先耗尽时输出部分覆盖而非全覆盖。

这个论证依赖发现完备、身份正确、返回有效和调度公平，**不能由 PlanTree 形状单独证明**。安全证明同样依赖行为分类与真实操作语义匹配；“Gate 收到了允许的 tap”不等于“该 tap 在现实中只导航”。

有禁入/未知分支时，只能证明明确限定范围内的可达覆盖，并列出排除和未探测子树。未经观察不能推导被跳过子树的内容。“所有设置页面”与“授权可安全到达的设置页面”必须在验收目标中明确区分。

### 6.3 组合的约束

叶子任务必须走现有完整执行链；父节点只能在所需子结果成立时推进，必须保留未知、失败和在途状态；子任务有效权限是父范围进一步收紧后的权限，不得扩大。修改版本后保持历史、预算与已投递动作对账。

这些约束提供结构归纳的证明义务，不代替组合测试。尤其要检验“检查之后环境变化”的时间窗口、不同节点共享状态、弹窗与预算同时触发、已投递后撤销等反例；不能只证明每个节点单独有效。

## 7. 当前实现与候选结构的实际缺口

| 缺口 | 代码/证据 | 实际含义与后续验证 |
|---|---|---|
| G1 高层 Traverse 节点缺失 | AgentDecision.cs 的 PlanItem 为 Act/ForEach/If/Repeat/Checkpoint/Observe/Control；KernelRunDriver 解释这些块 | 模型仍承担进入/返回和循环的程序拼接；要验证一份声明式 Traverse 能自动处理运行中新发现子树 |
| G2 普通 tap 被默认归导航 | SettingsActionPolicy.cs：排除危险模式和开关后，tap/click 映射 navigate | 静态反例：可见、非开关、未命中危险模式的“提交申请”按钮会走 navigate 分类；缺少足以区分真实效果的前提。本轮未运行反例；需公开入口 RED→GREEN，不能把 blacklist 当完整安全分类 |
| G3 执行投影损失记录面 | UiHierarchyOccurrenceStrategy.Build 跳过 Enabled!=true，以及非可点且非switch的普通节点；多数可点项为 ui.element；Kernel咨询摘要据Role重建能力并写 Enabled:true | 该动作候选投影不能独自承担“记录全部控件”；已有 CoverageLedger 可能保留额外观察，要审计实际消费者，不能直接断言全链数据丢失 |
| G4 页面返回身份未闭合 | SettingsPageIdentity.Derive 用 deliveredTarget 后缀 :Navigate up 识别返回；SettingsTraversalLiveFeed 传 lastReceipt.TargetSubject；TargetBinding 的UI绑定为 occurrence引用 | 语义目标与回执引用形状不匹配，可能把已返回页铸成新上下文；需要真实receipt形状的公开回归。只做静态复核，本轮未修 |
| G5 真实动作支持集有限 | AdbEffectDriver.IsSupportedEffect 仅 tap/click/set-switch/swipe-up/swipe-down，live复用支持校验 | 输入文字、独立系统返回、滑块、双指地图手势不能按旧SR文档“词表内✅”继承当前支持；本轮源检索未见这些扩展 |
| G6 领域事实/验证能力未齐 | UiNodeObservation 没有 range/value/action-list 专属字段；地图/双屏/驾驶限制需另外具体证据 | 不能只给每类一个策略名称就算已适配；无具体buyer的能力不预造 |
| G7 发现/覆盖结束需要正式判据 | SettingsPlanFacts 的空集合明确不证明全树完成 | 新 Traversal 要消费“本页发现是否穷尽/哪些还未知”的有依据状态，不能以栈空代替目标证明 |
| G8 未证明新协议生成稳定 | [现有现场报告](../../evidence/runtime-plan-001/composition-20261018/results.md)：有8动作/2咨询的行为完成例，也有控制流错误和形态失败 | 旧样本证明部分机制可工作，不证明高层PlanTree已落地或普遍稳定；新试验须事先按行为而非特定If/Repeat拼法注册 |

G2 是安全分类的静态缺口，不声称已有真实危险动作发生。G4 的具体设备反例来自前次审计，本次重新核对代码路径，未重新运行设备。所有缺口均只记录，不顺手修产品代码。

现有政策还允许 targetedActionClasses=toggle:Wi-Fi。只读遍历不能因为引用了这个 policy 就取得切换权；必须结合当前任务授权及更窄的节点规则，逐步验证。策略引用不是把整个 policy 的能力无条件授给计划。

## 8. 可判定的后续验收

各行必须保存 method/expected/actual/evidence；无运行结果先标待验，不填 PASS。

| 层次 | 必须检验的行为 | 通过标准 |
|---|---|---|
| 契约 | Traverse、规则优先级、安全引用、未知、深度与预算，Plan续行兼容 | 合法对象可校验；冲突/未知语义拒绝；不能扩权；正式codec/Schema/Prompt同步 |
| 元素适配 | 禁用、只读文本、Checkable/Checked/Selected区别、敏感输入、Unknown/Unsupported、窗口 | 每个输入等价类有消费者及期望；不遗漏需记录项，不从Clickable推导航 |
| 遍历正常流 | 动态新菜单、深层子树、同名不同路径、共享子页、回路、滚动/虚拟列表、折叠和Tab | 一份Traverse不预列所有子入口，准确展开/返回/去重；无需模型手写父子返回循环 |
| 遍历边界 | 未知、危险入口、无返回路、深度0/边界、末端未知、动态刷新、预算耗尽 | 零越权动作；覆盖缺口显式；只在相同定义范围内判断完成；停止后不复活 |
| 安全反例 | 非开关未知按钮、伪装导航、黑名单遗漏、内容伪造授权、观察后目标变化 | 不能因普通tap或可点就放行；按当前目标及授权检查，未知不执行 |
| 可靠性组合 | 在途迟到/未知、检查点续行、版本修订、弹窗×预算、撤销×在途、用户抢占 | 不重发未决Effect、不重复已完成项、不绕串行验证；不支持的恢复诚实退出 |
| 复用 | 同一策略用于不同菜单拓扑/应用适配；已有记录买方保持回归 | 相同Runtime/Traversal算法，替换已有缝的事实/动作/验证适配；无场景名写入Kernel |
| 其他交互族 | 18样例的具体能力与轨迹 | 逐项实现或显式保持未支持；回Agent是合法退出，不计该任务已自动适配 |
| 场景库回归 | 28条原载体期望保持；新增适配后登记真实载体/trait | 执行结果与期望join，不读JSON自报passing；认证按现有变更协议进行 |
| 现场 | 新注册的Android/车机实际范围、统一目标/授权/逐步验证 | 任务覆盖、安全、稳定性与成本分别报告；全失败计成本；不混旧模型/环境基线 |

首片以菜单 Traverse 及 G2/G4 的真实反例收敛为先，随后按其他交互买方推进，不先开发没有现实适配输入的地图/机器人平台。不把修复动作混进本轮静态推导。

## 9. 本次验证四元组

- method：读取当前源码/正式基线，解析推理场景表、SCN条目、交互样例表和C#节点/窗口字段；保存源哈希及逐项责任、能力需求和待证结果；重跑离线 --check 与文档链接检查。
- expected：150/28/18/22+7输入逐项可追溯；既有期望/载体不变；未执行的新协议不出现PASS；定位无法证明适配完备的具体反例。
- actual：静态清单生成并核对，150条推理、28条库条目（20golden/8描述）、18交互样例、29字段均映射；新PlanTree运行数0，implementation_verdict=NOT_PROVEN。无产品、场景期望、设备、模型或代理修改。
- evidence：[inventory](../../evidence/runtime-plan-001/plantree-derivation-20261019/inventory.json)、[矩阵](../../evidence/runtime-plan-001/plantree-derivation-20261019/scenario-matrix.md)、[检查结果](../../evidence/runtime-plan-001/plantree-derivation-20261019/verification.json)、[改动前快照](../../evidence/runtime-plan-001/plantree-derivation-20261019/entry.json)。
