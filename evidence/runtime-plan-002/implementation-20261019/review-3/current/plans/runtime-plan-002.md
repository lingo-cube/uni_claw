# RUNTIME-PLAN-002 实施计划（2026-10-19）

## 范围和已定工程契约

D1-D19及implementation-handoff为语义权威。当前HEAD 2225c642，所有入口修改视为他人/001/002设计所有；entry.json记录逐文件hash，/tmp/uniclaw-runtime-plan-002-entry-20261019.tar.gz保存可恢复副本。不reset/clean、不提交/推送。

PlanItem.TraverseItem(Source,Rules,MaxDepth,ReturnStep)以追加union成员保持旧Plan兼容。规则有序，Condition由当前World引用投影提供三值结果，`always`仅是显式兜底；Unknown阻止选择后项。TraversalOperation有独立OperationId、ReadOnly/Navigate以及各自Before/AfterVerified有序PlanLanguageHandler。Navigate.Step里的$candidate绑定当前语义目标，ReturnStep重用原Act链；ReadOnly.Step必须null且零Effect。MaxDepth root=0，0允许处理根但不进入子页。来源仅接受任务可用Source。

handler闭合到既有Language Inspection@1.0 typed协议，不增加Capability/Registry/任意Execute。每个binding声明HandlerId/CapabilityId/ProtocolVersion/ExpectedLanguage/Required/RequirePass，Required=可用性，RequirePass=显式继续条件。默认Finding只记录；未完成/异常/不可用不伪称已执行。Before使用操作前World版本，AfterVerified必须在原Assurance验证成功后取新版本；ReadOnly无Effect，自身处理后进入AfterVerified。原目标消失保持Unknown/暂停，不能替换。运行时可用性每次检查；启动前缺必需binding零Effect。

TraversalObservation是显式Owner投影缝：Graph源于World，候选仅引用该版本的当前目标，所有证据及当前页关联由driver验证。DiscoveryComplete必须有证据，空列表不能代替穷尽。RunModel是TraversalExecutionView记录Owner；Control只保留探索所需栈/游标/去重（非第二Run）。较浅到达须重新打开之前较深到达的展开机会，去重必须关联页面/规则/操作义务，路径环不能无限展开。

暂停沿用既有ContinuePlan关联同一PlanExecutionPosition，保留已发生动作、handler游标和消耗。Recovery通过ObserveTraversalRecovery获取位置、尝试、handler结果及预算事实；不在driver增加恢复算法。查询不调用推进/咨询/Effect；暂停不出最终报告。Completed/Abandoned才有EndReason，覆盖有Gap则Partial，不宣称Goal完成。

## 垂直切片及写入责任

1. Leader：类型/协议语义，Run记录缝，World投影及Host语言binding、组合接线、文档与整体验收。
2. WI-RUNTIME-PLAN-002-EXECUTION：Control/TraversalControl及KernelRunDriver集成；只写该Controller、driver、指定Kernel测试。先只读零Effect tracer，再动态进入/返回、暂停续行。
3. WI-RUNTIME-PLAN-002-PROTOCOL：Agent.Dsh codec/Schema/版本执法、生成物、Prompt/manifest/hash、DSH镜像及协议测试。依赖步骤1类型，和步骤2无写入重叠。
4. 核心稳定后串行派发SimulationHost/ScenarioRunner测试：真实Owners，显式外部仿真实现，覆盖交接8类验收，补失败分支。
5. Leader审阅+code-review双轴，处置问题；执行Kernel/Agent.Dsh/Host/Simulation/DSH相关测试、公开面/闭包/生成漂移检查；Kernel hash最终稳定后重新认证并实际执行场景。
6. 四元组证据同步state/INDEX/CONTEXT/正式协议；所有Acceptance通过才关闭。

## 可判定Acceptance（替代前阶段仅文档验收的当前实施范围）

A1 真实Kernel仿真动态发现/兄弟/进入返回，首项规则/Unknown/安全零投递。
A2 操作独立handler顺序和前后World版本；注册语言同协议调用；只读零点击；不符合仅记录继续/必需暂停/异常诚实记录。
A3 缺失/矛盾后验不提前后置或下次Effect；暂停查询零推进；明确续行保留预算动作，Recovery事实可观察。
A4 环/共享子页/不同义务/先深后浅root-A-C和root-C-D不漏展开；深度/禁入/消失/发现不足/返回失败如实报告。
A5 工作预算、取消/超时/能力失效均有可执行负向分支，不新增Run/复活终态。
A6 producer→codec→consumer正向及未知kind/缺参数/错类型/非法绑定/版本/越权负向；生成Schema/TS/Prompt/manifest/hash/DSH镜像一致无漂移。
A7 28条历史场景身份/期望保留，Kernel变更重新认证；有execution carrier的场景实际运行，none保持未执行且不计通过。
A8 Review问题处置、正式协议/CONTEXT/文档/状态同步；001历史及ADR-0053旧成绩不改写。

测试缝已由Owner交接明确：SimulationHost/ScenarioRunner真实Kernel全链、正式producer→codec→consumer、现有Host typed binding，按tdd在这些缝逐片实施。无需为同一已批准测试面再询问。
