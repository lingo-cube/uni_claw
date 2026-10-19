# 持续遍历正式执行契约

日期：2026-10-19。语义依据D1-D19；工程类型见`src/UniClaw.Kernel/Runtime/TraversalExecution.cs`及`PlanItem.TraverseItem`。这是当前实施契约，历史protocol-sketch仍是演变草案。验收是否完成以state与implementation证据为准。

## 输入及封闭词汇

沿用P25 `AgentDecision.Plan`和原PlanId/Version/MaxLocalOperations/ContinuePlan，不添加状态/结果型AgentDecision。Plan追加`traverse`项：source必须为本任务装配提供的来源；rules为1..16个有序项；maxDepth为0..16（根深度0）；returnStep为既有AgentActionStep。静态Plan块深度和节点数继续原边界。

每条规则包含唯一ruleId、condition和独立operation。条件来自当前Owner引用投影；`always`是显式兜底。前项Unknown且影响选择时暂停，不跳到后项；多匹配只执行首个确定命中规则，不合并操作。操作operationId为模板标识，具体执行和handler结果以运行时唯一OperationId关联。ReadOnly必须step=null，Navigate必须给语义step；`$candidate`只绑定当前目标语义，不携带坐标或旧定位。

每个操作拥有before及afterVerified数组，各0..8项且顺序为计划声明顺序。第一条实现只支持现有`runtime.language-inspector`/Language Inspection@1.0，handler参数为handlerId、capabilityId、protocolVersion、expectedLanguage、required、requirePass。没有新Handler Registry、反射调用或万能Execute；增加能力须以真实买方定义独立typed缝，不靠任意字符串调用。

`required`控制可用性：启动前缺必需绑定不启动，运行中缺失暂停；显式可选缺失才可跳过并记录。`requirePass`是继续条件，默认false；不符合或领域Unknown可记录继续，显式要求通过时才暂停。Finding的原始Disposition/Status/Diagnostic及Correlation保留；调用异常、超时、不可用不伪装调用完成。能力定义领域含义，Kernel不推断语言规则或Goal是否完成。

以下仅展示traverse项，外层仍是正式Plan，参数由真实任务绑定提供：

```json
{"kind":"traverse","source":"settings.menu","maxDepth":2,"returnStep":{"targetRole":"ui.element","targetDescriptor":"Navigate up","effectClass":"tap","desiredState":null},"rules":[{"ruleId":"switch-label","condition":"readOnly","operation":{"operationId":"record-switch","kind":"ReadOnly","step":null,"before":[{"handlerId":"label","capabilityId":"runtime.language-inspector","protocolVersion":"1.0","expectedLanguage":"en","required":true,"requirePass":false}],"afterVerified":[]}},{"ruleId":"safe-menu","condition":"navigation","operation":{"operationId":"open-menu","kind":"Navigate","step":{"targetRole":"$candidate","targetDescriptor":"$candidate","effectClass":"tap","desiredState":null},"before":[],"afterVerified":[]}}]}
```

## Owner与执行链

World拥有当前页、目标、关系和证据。source是装配提供的命名投影来源；Graph.Scope沿用当前Run/Contract的既有导航范围，两者不能混同。TraversalObservation显式给出WorldGraphProjection及当前候选引用，consumer核对当前revision、页、occurrence和证据；来源不授予Effect许可。历史图节点不成为可执行目标。发现穷尽是独立有证据声明，不能由空候选推出。当前Settings live投影只对任务明确分类的入口给navigation=true；泛化clickable保留Unknown，完整层级抓取也不证明虚拟/滚动菜单穷尽。

ControlLoop持有TraversalControl，内聚展开/返回/深度/义务去重及控制游标；driver调用这个模块及原Owner缝。每次已验证到达都处理本页当前检查；导航展开按源页/规则/候选/操作义务及最佳到达深度去重，不把“页面来过”当成所有检查结果仍有效。更浅到达可重开受深度约束的展开，已验证落到路径祖先则同步实际位置并暂停等待修订。每个Navigate仍经Control→grounding→Assurance→Effect Boundary→新证据/World→独立动作验证；afterVerified与下次Effect不能越过验证。ReadOnly记录当前目标状态/来源并调用其handlers，零额外点击。需要新决策时原执行暂停；已发生动作、handler位置与消耗保留，Recovery收到原Owner执行事实后重入正常闭环。

检查输入显式关联当前Run、具体OperationId、时机、World revision、容器及目标。Host跨域适配器只接受任务绑定的ILanguageInspector实例，按当前唯一目标投影语义文字标签；它不宣称检查所有像素/整页文字。目标过期/消失不得替换为另一目标。调用有有界等待；迟到结果不能补写Owner状态或产生Effect。

## 查询、续行与报告

`KernelRunDriver.TraversalExecution`从RunModel的canonical记录读取；PlanExecutionView.Traversal将同一执行信息带回正式DecisionContext，不建第二套状态协议。查询不调用Drive、咨询、投递或恢复。KnownRemainingWork只表示当前已观察的待处理前沿，尚未观察时为null，不能把它解释为整任务未知部分已穷尽。

沿用`continuePlan`及完整PlanExecutionPosition精确关联。续行在当前事实/授权重检后接回同一位置，不能清空工作量或重发已验证动作。新Plan版本只接替未执行部分；Run保留历史记录，跨进程恢复不在本次承诺中。

TraversalExecutionView在运行/暂停时EndReason为空，结束后给出正常/部分/提前停止原因，完成工作、操作状态输入快照、handler调用与typed结果以及全部未完成/排除/未知/深度/发现缺口。它是限定范围的执行报告，不能授权Effect、覆盖Assurance或证明Goal。候选空、栈空、回首页、handler全返回均不能单独宣布任务满足。

## 正式协议闭包

C#公开类型→ProductProtocolSchemaGenerator→JSON Schema/TypeScript，ProductProtocolJson producer/codec→DshAgentAdapter consumer及DSH插件验证使用同一生成资产。Prompt/manifest/schemaHash/prompt-hash/profile装配hash及DSH镜像同步。新增union为兼容扩展；既有protocol/schema版本保持，schemaHash握手阻止旧codec误收新载荷。语言能力协议不升级、不改变既有Finding领域语义；不兼容协议版本仍拒绝。

未知kind、错型/缺参数、重复标识、非法绑定/能力版本、非法ReadOnly.Step及深度/数量越界由正式codec拒收；越权Effect仍由Runtime与原Owner链逐动作拒收。静态闭包或旧场景通过不代替本机制SimulationHost实际执行结果。
