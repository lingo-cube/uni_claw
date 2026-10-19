# ADR-0053 首片实现契约（2026-10-18）
本文件冻结工程形状，语义继承ADR-0053及已确认D1-D10；不是新生命周期或新Capability协议。已批准测试缝为KernelRunDriver/RunDriverInputs、正式codec/Schema、Settings逐动作guard及journal。入口线性Plan/预算回归37/37。

## 最小公共形状
- AgentPlanProposal(Items, Justification, PlanId=null, Version=1, StageExpectation=null, MaxLocalOperations=128)。旧进程内线性Plan保留；正式新Plan要求非空PlanId及正版本。新版本只在咨询的对账边界采纳，原ID版本严格增加；同版本恢复专用ContinuePlan，不能当新采纳重置工作量。
- PlanItem.ForEachItem(string Source, IReadOnlyList<PlanItem> Body, int MaxIterations, bool ContinueOnBlocked=false)：不允许嵌套ForEach，Body只允许ActItem/ObserveItem。每轮重新查询同source当前集合，stable key去重；新项在范围内继续。ActItem.TargetRole及TargetDescriptor中的精确token `$candidate` 分别绑定候选Role和Descriptor；禁止模板拼接或表达式。Body也可含固定语义动作（例如Navigate up）。
- PlanItem.CheckpointItem(string? Reason)：消费节点后在合法边界咨询，保留下一执行位置；不是完成。
- PlanCandidate(string Key, string TargetRole, string TargetDescriptor, PlanCandidateDisposition Disposition, IReadOnlyList<string> EvidenceRefs, string? Reason=null)。Disposition闭合为Ready/Blocked/Unknown。Blocked必须有原因及来源证据，且ForEach显式ContinueOnBlocked=true才可跳过；否则暂停回Agent。只记受阻不标完成；Unknown立即回Agent，不能按false/Blocked继续。
- PlanCandidateSet(string ScopeKey, string Revision, IReadOnlyList<PlanCandidate> Candidates)：ScopeKey锁定一次循环的语义范围；选下一项时范围失配回Agent，不因普通Revision变化自动废弃。选中候选后Body靠逐动作事实检查，不把旧定位带到未来页面。查询null/重复key/缺source证据/异常均fail closed回Agent。
- RunDriverInputs.ProjectPlanCandidates: Func<string, PlanCandidateSet?>?；AvailablePlanSources: IReadOnlyDictionary<string,string>?只读描述；GuardStep: Func<AgentActionStep, AgentDecisionContext, PlanStepGuardResult>?。GuardStep适用于所有单步Act及Plan/Policy物化，输入从当前Kernel事实现建，不再仅在咨询时批量放行。PlanStepGuardResult(PlanStepGuardVerdict Verdict,string Reason,IReadOnlyList<string>? EvidenceRefs=null)，枚举Allow/AlreadySatisfied/Reject/Unknown；后三者零新Effect；AlreadySatisfied仅消费当前动作，Reject/Unknown回Agent，不隐式换路。该回调仅约束，不授予Effect或Control意图。
- AgentDecision.ContinuePlan(string DecisionId, PlanExecutionPosition Position, BudgetReviewResponse? ReviewResponse=null)：只可恢复已暂停且仍适用Plan；必须精确引用公开Position，不创建/重置Plan。授额独立处理，NoAction仍终局评估并明确废弃旧Plan；Act/Policy/Blocked/Skip处置旧执行内容。Defer合法补证可保留暂停Plan，之后仍须明确ContinuePlan。
- PlanExecutionPosition(string PlanId,int Version,int ItemIndex,int Iteration,int BodyIndex,string? CandidateKey,int WorkUsed)：准确恢复引用。PlanExecutionView含Position、Status、StageExpectation、InterruptionReason、累计实际Attempts和受阻证据。AgentDecisionContext及ExecutionOverview各追加PlanExecution、context另含AvailablePlanSources。数据来自同一Driver投影。
- PlanAttempt记录唯一AttemptId、Position、DecisionId、语义Step（可空）、Status、ReceiptId（可空）、EvidenceRefs/Reason。采用全Run单调attempt序号，Receipt在派发时关联；历史不随Plan修订/授额清空。新completion锚`plan-attempt:{AttemptId}`只匹配Verified，旧step锚保留线性语义，不用重复item index冒充不同迭代锚。

## 展开及恢复约束
观察、选候选、执行、跳过受阻、条件/控制推进都计本地工作；MaxLocalOperations是Plan本地界限，不是全Run累计资源顶。WaitingForInput重试不重复收费；恢复不清零工作量。工作耗尽必须咨询并修订，ContinuePlan不能自动刷新配额。循环MaxIterations控制本节点处理项数，达到上限进入下一项/检查点，不宣称全树完成。
每次新动作前重检预算、当前guard与事实；阈值复核在StepVerify对账后/PlanExpand触发，暂停保留位置。采用一次安全边界复核锁存避免同位置无消耗重复咨询；获得资源不等于继续执行。若继续未获资源而动作预算为零，零Effect停止/回Agent，不重新启动无界复核。
已发动作在途时继续接收同次结果，无重复dispatch；UnknownOutcome/Failed维持恢复屏障，不能将新Drive误作重发。失败保留attempt与receipt，只有Owner已证明且可安全绕开的候选阻碍允许跳过，实际guard/grounding/verification失败默认回Agent。
Policy Guard Unknown及lease失效的既有回Agent语义保持，显式Observe不得充当隐式fallback。

## 首片与后续验证
首片通过公开Driver组合：单Plan处理A；B有Owner来源证据受阻；A之后发现C并由原规则处理；中间资源复核且明确ContinuePlan原位置；验证完成动作不重复、attempt/receipt唯一及全部消耗保留。随后同核心替换为记录集合买方（无页面/返回语义），验证未知、失败、修订、NoAction和零Effect工作耗尽。正式wire与Settings接入随后串行集成，未完成不得宣称首片全部现场通过。

## 现场收益定位后的咨询投影
真实ShortPlan首次完成2动作但约131万token、3咨询，RulePlan首次完成2动作但4咨询，均未达预注册门槛。完整PlanAttempt原始EvidenceRefs在每次咨询重复携带导致明显输入膨胀。Driver公开审计视图与持久plan-execution继续保留全部eID、候选/guard/receipt及历史；Agent咨询视图保留同一attempt身份/位置/decision/动作/状态/receipt/原因，EvidenceRefs改为指向该实际审计记录的plan-attempt:<AttemptId>。这不是任意截断，完成核验仍只接受Verified，Blocked保留Owner来源证据。Settings Source说明明确返回属于每次body内部，以免模型将返回放在循环外导致正确scope中断。此增量须新同条件Step基线与Plan前冻结门槛；旧样本/门槛永久保留，不混用或重写旧收益结论。
