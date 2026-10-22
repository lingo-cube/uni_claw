# Settings任务要求到完成结果的实际映射

Status: READ_ONLY / EVIDENCE-BOUNDED
Authority: NONE
日期：2026-10-22。Leader完成限定源码核验；Worker未完成最初核验、未产出本文件，其回复不作为已完成映射的证据。本轮不运行测试、模型、设备，不修改产品。

## 结论

当前不是“任务只有回首页要求，也没有其他完成检查”。详细要求在Objective和Coverage报告中，goal-directed模式的任务证据屏障会挡住报告未满足项。真正断开的部分是：这条Settings运行链未见构造PrimaryGoal并调用正式UniAgent.Evaluate；Runner将Runtime终局分类字符串和CoverageReport并列返回。独立Goal评价器存在，不代表此运行链已经消费它。

本结论限定`SettingsCoverageRunner`及`UniClaw.Host.Dsh/Program.cs`的Settings coverage分支，以及Host/Host.Dsh的C#调用点检索；不推断所有其他Host或外部适配器均未集成。

## 要求逐项映射

主要路径：`product/tasks/settings-coverage.yaml`→`SettingsCoverageConfig`→`SettingsCoverageLedger.Recompute/Report`→`SettingsCoverageRunner.CheckTaskEvidenceBarrier`→`KernelRunDriver.TerminalEvaluation`。

| 要求 | 当前依据/判断（Ledger.cs:467-621） | 到终局与Goal的情况 |
|---|---|---|
| rootPage | 接纳后的root route被观察，root-page.Covered | 另有obj-end-on-root mandatory义务，注意看过首页与最终位于首页是两件事 |
| 配置targetPages | 按目标的verified离开根页记录，另接受显式AgentSkipped；first-level-all-visible.Covered | 报告缺项进入Host屏障；没有每个目标独立义务的Outcome记录。标签entered包含Skip需结合原D4双指标阅读 |
| scrollDiscoveredEntries | 在已验证scroll之后观察到的候选，后续进入或显式AgentSkipped；计数与配置阈值比较 | 报告/屏障消费；不是证明滚动列表总量或全部范围发现穷尽 |
| secondLevelPages | 根页verified进入后的distinct route数，与配置阈值比较 | 报告/屏障消费；计数要求不等于每条业务检查均通过 |
| backNavigation | 非根→根、back描述符的verified返回记录 | 报告/屏障消费；不是仅凭tap receipt通过 |
| repeatedEntries | 同一入口至少两条不同receipt的verified进入，达到配置数量 | 报告/屏障消费；任务明确要求的重复验证可以有价值，不应把所有重访一律判无进展 |
| 显式nested-pages | DeepTraversal开启时按配置目标检查nestedEntered | 仅启用时生成报告要求；范围依配置，不等于整个系统树 |
| dynamic root/nested | 动态已观察前沿为空分别令对应Covered成立，另报告visited/skipped | 只能证明当前登记前沿已处置；Traversal投影DiscoveryComplete仍为false，不能升级为全Settings发现完成 |
| 可选语言/能力handler | Runner接现有binding和InspectPlanLanguage，执行记录归原Plan/Run链；required和requirePass按原契约 | 本轮未证明所有检查要求自动形成独立CoverageItem/Run义务；不能仅凭handler调用返回声明业务通过，需按具体Task映射 |

## 现有完成屏障和结果出口

- `SettingsCoverageRunner.cs:343-352`在goal-directed模式注入CheckTaskEvidenceBarrier，先同步原Owner事实，再读取Report.UncoveredItems；缺项返回字符串原因。
- `KernelRunDriver.cs:1788-1796`在TerminalEvaluation先查该屏障；缺项时返回TerminalNotProven，不发明成功结论。屏障只可扣住证明，不能凭null授予完成或Effect。
- `KernelRunDriver.cs:3852-3864`再经原UniKernel.EvaluateTerminal取得RuntimeOutcome。RunDriveStatus.Completed表示产生terminal envelope，不能仅按这个枚举认定Goal成功；还要看分类与满足语义。本轮未复现Settings的非Completion terminal组合。
- `RuntimeOutcome.cs:15-25`保留RunId、分类、ObligationStatus、依据/Proof及效果/情境引用等，没有完整CoverageReport字段。本装配显式mandatory义务主要是obj-end-on-root；不因此否认前置任务屏障的作用，也不假称每条任务要求都已成为可独立引用的Outcome义务。
- `SettingsCoverageRunner.cs:98-108,504-549,596-600`分别落Coverage报告和facts，RunResult仅以string返回分类并携带独立Report；没有GoalEvaluation结果。
- `UniClaw.Host.Dsh/Program.cs:300-342`的coverage分支打印报告，Completed后再依据CoverageComplete决定CLI退出码；未见调用正式Goal评价器。Host/Host.Dsh C#检索未找到new UniAgent或PrimaryGoal/Evaluate的正式接线。
- `UniAgent.cs:38-104`的现有Evaluate消费RuntimeOutcome；GoalCriterion支持ClassificationIs/ObligationFulfilled，缺义务为Unverifiable→Undetermined/NeedsFollowUp。不能让它直接回读Host CoverageReport、raw Evidence或Receipt，从而绕过Assurance建立第二套完成判断。

## Skip不是本轮重新裁决的权限或指标

原已接受决定在`docs/analysis/agt019-followup-optimization-and-research-2026-10-17.md:225-235`：D2分ReachedOverlay/RouteEntered/Skipped/Pending；D4明确taskCoverage=reached+skipped、verifiedReachRate=reached/total，Agent Skip零Effect、与structural-skip分账、预算耗尽不得静默Skip。

因此，Ledger的有限处置统计接受显式AgentSkip，不能直接当作本轮新发现的未授权行为或去重做已批准的跳过机制。Q12也不把“用途未知暂不点”自动翻译成Agent已显式Skip。原双指标保持；“已处置”不能在报告和Goal中改称“全部实际访问”。完整动态遍历目标仍保留，Stage有限结果与该目标分别表达。

## 需要补的设计与Owner

1. Task/Goal authoring明确同一份要求及范围；Host只供事实/限制，按原权威把可证明结果进入Runtime结论，再供UniAgent评价。不能只拼Objective或依赖CLI退出码。
2. 逐项确定要求由现有语义承载还是存在真实新缝需求。允许复用已有接口；如需新增公共模型/接口，另列可审阅稿，先有buyer与反例，不把所有任务统计写成World事实。
3. 结果至少能让消费者分清：必要要求已证明、必要要求未证、明确排除/跳过及本阶段未承诺范围。这里是行为要求，不是新enum或已冻结协议。
4. 保留原Owner：World世界事实，任务Owner过程事实，Assurance证明，Run记录，UniAgent最终Goal评价。全树发现与额外检查的证据不足保持未证，不缩减原任务追绿。

## 当前产品取舍（Q13：已接受延期方向）

原候选A为首期接通要求→证据→运行结论→Goal；B为正式Goal接通后续独立切片。用户最新提出先聚焦World、可以延后，已按B的延期方向记录。本期World不负责新增Host报告或整体Goal交付，现有有限报告/屏障仅作为事实及后续依赖；不声称正式Goal闭环。Q12未知用途不点击、最终动态目标及原Owner不变，无新增产品实施授权。

恢复条件：后续明确承诺这条任务的端到端完成评价时，由独立切片继续逐项要求映射、原Assurance/RuntimeOutcome结论与UniAgent消费接线。World只提供所需世界事实/缺口，不能替任务Owner记录进展或替Goal判完成。本期四类缺口到Agent的必要只读出口仍保留。

## 验证四元组

method：限定源码、配置、既有Skip裁决的只读链路与调用点检索。

expected：区分目标文字、Host限制、Run终局和正式Goal评价，旧指标不被误作新缺陷。

actual：上述有限映射可定位；正式Settings Goal评价接线未见。额外handler到Goal的全部语义、真实场景及负向结果未运行，未宣称全局所有Host缺同一能力。

evidence：上述路径/行及`evidence/q12-source-manifest.json`、`evidence/q12-verification.json`。
