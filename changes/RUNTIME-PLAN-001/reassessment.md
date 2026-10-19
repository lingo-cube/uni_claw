# RUNTIME-PLAN-001 — 泛化执行能力重新评估（2026-10-18）

性质：对原目标、当前源码与验收范围的复核；不是新接口决定或产品实现。本轮由Owner要求“重新评估，然后确认要不要提一个change”。

## 结论与Change归属

需要继续修改产品，但不另建重复Change。RUNTIME-PLAN-001尚未关闭，D1-D5已经要求Agent按任务组合基础操作/条件，Runtime按当前事实推进；本次讨论没有另提出一个独立产品目标。应在原Change中纠正首片实现与验收对原目标覆盖不足的问题，按UniFlow A7的语义/假设失败边从VERIFY回RESOLVE。ADR-0053继续accepted，不以“状态机”这个解释性称呼新增ADR或第二个执行器。

这不是否定已通过的有限场景，也不是将原已授权任务扩成任意程序平台。D10允许首版收紧到真实买方需要的有限组合；问题是首片收紧后，不能将有限候选循环与小范围现场完成等同于泛化目标已经实现。

## 重新校准“状态机”这个说法

KernelRunDriver已经有NeedDecision、PlanExpand、StepAct、StepVerify等执行阶段。缺口不是“产品没有状态机”，而是Agent目前能表达的任务控制逻辑较少。

- 执行状态：当前展开到哪里、是否等待结果、能否继续，由Runtime持有。
- 策略内容：依据哪些已提供事实选择分支、重复哪段操作、何时返回或交回模型，由Agent在受支持语义内组合。
- 世界及任务事实：菜单/控件类型、已访问与已记录状态、阻碍及完成证明仍由原Owner提供；Plan不能自行伪造。

可以用“带运行数据的条件转移”帮助理解，但不由此预选任意状态图、递归DSL或完整行为树框架。动作逐项执行与验证仍然必要；没有新语义判断时逐项重新请求完整Agent决策，并非必要的执行条件。

## 原目标与当前实现对照

| 原目标 / expected | 当前行为 / actual | method / evidence |
|---|---|---|
| 产品基础操作/条件由Agent按任务组合，不逐任务固化完整流程 | PlanItem目前只有Act、Observe、Control、ForEach、Checkpoint。已有候选Ready/Blocked/Unknown与guard内建分支，但没有Agent可组合的通用typed条件分支表达 | 源码静态对照：[AgentDecision](../../src/UniClaw.Kernel/Runtime/AgentDecision.cs)，与[state D3-D5](state.md)、[proposal §6.2](proposal.md)对照 |
| 按当前新事实继续，已支持规则覆盖的新页面不必重新咨询 | ForEach逐轮重读候选是有效基础；Body仅允许Act/Observe，没有可供模型在Body内组合的条件/控制子流程 | 源码静态对照：[KernelRunDriver.ValidatePlanProposal](../../src/UniClaw.Kernel/Runtime/KernelRunDriver.cs)，[首片契约](implementation-contract.md) |
| 菜单进入、信息记录、发现新入口等行为可按事实组合 | Settings只发布settings.root-navigation；Source描述要求每次进入后回根页。当前非根页返回Unknown；未提供跨层待办/返回上下文与控件分类记录的完整组合证据 | 源码静态对照：[SettingsPlanFacts](../../src/UniClaw.Host/SettingsCoverage/SettingsPlanFacts.cs)，与[proposal §7](proposal.md)对照 |
| 不同结构买方证明共同机制的可复用范围 | 记录买方证明同一ForEach与Owner事实/动作缝可替换，不证明Agent能组合不同条件路径或多层页面推进 | 测试源码核读：[KernelRunDriverPlanTests.RecordBuyer](../../tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverPlanTests.cs)；历史运行证据[record-buyer](../../evidence/runtime-plan-001/core/record-buyer.md) |
| 泛化行为及收益都需分别成立 | 真实10次Plan完成的是注册的2/4动作小范围任务；既有性能组仍有未过项。即使消除最后一次多余咨询，也不能补足策略组合证据 | 历史证据范围复核：[直连批次结果](../../evidence/runtime-plan-001/acceptance-direct-20261018/results.md)，[verification](verification.md) |

本轮没有执行设备、模型、构建或测试。上表的源码结论是本轮静态核对；测试通过及现场数值是保留的历史证据，没有作为本轮新跑结果。

## 应在原Change补齐什么

1. 先修订首片实现契约：明确真实买方所需的条件输入、满足/不满足/未知处理、结果后的转移、局部运行数据与返回上下文。目录和表达形状由场景反例验证后收敛，不先冻结一套通用状态机语言。
2. 保持Agent负责策略组合、Runtime负责机械推进。场景只提供事实/判据/既有动作适配，不能把完整遍历循环移进Host，再用一个工具名包装。
3. 现有逐动作Control、定位、许可、独立验证、唯一在途Effect及明确预算续行继续作为基础。事实条件Unknown不能当false；Policy/guard未知出口保持。已发动作未对账时等待原结果，咨询或换分支不能触发重复派发。
4. 当前实装的候选循环、精确ContinuePlan、版本/attempt/receipt与审计机制继续复用；不因本轮讨论推倒重建。

这里确定的是修正方向与验收缺口。具体新增节点、谓词、状态字段和公开协议尚未定稿；如果选定实现必须改变ADR-0053明确排除项或更上层权威，再对该具体取舍作新决定。

## 需要补成可判定的验收

以下是用于下一轮契约收敛的行为要求，不把尚未执行的场景写成通过：

- **有界的多层Settings段**：初始提案不列出未来所有页面名称；进入子页后发现新的允许菜单及可记录控件，按已声明事实分支处理，并沿有证据的返回上下文继续其他待办。规则仍覆盖的普通页面变化不要求完整Agent再决策。记录开关是观察/记录，不等于切换或功能已测。
- **策略组合确实来自Agent**：同一执行核心接收至少两种不同控制组合；分支与转移可在提案中审计，不仅是相同ForEach替换Source。Settings与记录核验买方不增加核心场景特判。
- **受阻、未知与续行**：已证明不适用的项只能沿声明的合法路径处理；未知保持未知；需要新语义判断或复核时才交回Agent。预算边界及在途/对账/版本替换不重复执行已确认动作。
- **证据范围分开**：有限场景证明表达和执行能力，不能证明所有Settings页面已遍历。完整遍历及既有SETTINGS-COVERAGE-005的剩余工作保持原归属。
- **收益独立重验**：行为成立后，以同目标/模型/权限/观察验证重建适用对照；先固定样本和门槛再读Plan结果，保留失败成本。当前历史门槛不迁移成新场景的通过结论，不为追PASS追加旧批次。

## 本轮验证四元组

```yaml
verification:
  level: CONTRACT
  method: "静态对照D1-D10、proposal §6-10、ADR-0053、首片契约、PlanItem/ValidatePlanProposal/SettingsPlanFacts/RecordBuyer；复核历史现场证据覆盖范围；检查文档链接、格式与生成索引"
  expected: "分清原目标与新增需求、有限首片与泛化能力、既有执行状态机与可表达策略；确定Change归属并保留真实历史"
  actual: "确认原目标已包含可组合条件与新页面继续；当前单层候选循环只提供部分基础。沿用RUNTIME-PLAN-001，VERIFY回RESOLVE；不创建重复Change、不冻结新DSL、不改变产品代码或重跑现场"
  evidence: "本文件及其源码/设计/历史证据链接；state.md；verification.md；ADR-0053；changes/INDEX.md"
```

补充业界证据见[机制对照](../../docs/research/runtime-plan-generalization-industry-20261018.md)。它是设计输入，不是Android实测或框架选型决定。

文档修改前副本与共享工作区status保存于`/var/folders/_3/lbphn3p95f3dj8ds3z1874q00000gn/T/uniclaw-plan-reassessment-49yebjjb/`。本轮不修改产品、Prompt、Schema、模型/代理配置或已冻结现场产物，不提交推送。
