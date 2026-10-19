# 新会话实施移交

日期：2026-10-18。Owner最新原话：“开启一个新会话，GPT-6.1 Sol 高 是 Leader ,安排 GPT-6 Luna当 Worker 把这个干完”。这是整体提案确认和实施授权，结束原仅文档阶段；不是要求重新grill。

## 模型与执行

- 新Leader固定GPT-6.1 Sol，reasoning high；Worker固定GPT-6 Luna。使用新会话内的sub-agent能力委派，具体文件/职责互斥，并明确告知Worker共享工作区、不得还原他人改动。用户明确指定模型，不静默换成GPT-5.6 Luna或其他型号；若精确模型无法派发，诚实报告具体限制。
- Worker必须获得可自包含的任务和验收，不灌入完整会话历史；Leader负责计划、边界、集成、评审和最终验收。无需为Worker再创建用户侧会话。
- 按UniFlow复验入口、同步最终批准状态、形成计划，推进实现→评审→验证；满足Acceptance才CLOSED。若真实环境验收失败，定位并修复；无法满足时如实保留未完成原因，不用单测冒充现场证明。
- 无需再次请求已获授权的设计/实现批准。只有真正的新产品含义、架构冲突或用户必须提供的信息才回问。未获提交/推送授权，不擅自提交、推送、清理共享改动。

## 必读入口

先读AGENTS.md与按需Skills（uniflow；设计/组件/诊断/评审Skill按实际工作触发），再读本目录state.md、proposal.md、decision-sop-draft.md。拟议ADR为docs/adr/0052-goal-directed-budget-review-and-continuation.md；Owner已授权按整体提案实施，新Leader应同步该ADR接受状态、0050/0051的部分替代说明、CONTEXT及协议事实，不将旧proposed文案误当成还要再次审批。

研究及源码审计：docs/research/goal-directed-budget-renewal-research-20261018.md、docs/research/mobile-ui-traversal-sop-calibration-20261018.md。现有证据：evidence/renewal-strategy-research-20261018/。研究及旧测试不是本次实现已通过的证据。

## 已确认行为

1. Agent依据目标判断推进，SOP为敲定预期→看结构→算待办→找路径→决定；待办允许粗粒度并逐步细化。
2. 根页观察即发现入口，保留全局清单并沿当前有价值路径深入；不固定先广或先深。局部受阻继续其他分支。本次范围为当前设备/账号/权限/系统状态下允许访问的菜单。
3. 复用World Model图，补真实页面身份、导航边及只读投影；不新造平行图。World Graph不持有目标、任务完成或预算权威。未访问入口不能冒充已观察目标页面。
4. overview同源事实服务Agent、人类和审计，独立呈现Agent判断；提供累计、上一轮、本笔额度的进展与未知，精简输入不等于重复完整历史。首版接入Agent输入与运行记录/报告，不另建控制台。
5. Runtime按额度主动触发明确复核；Agent判断/提案，Runtime校验/记录。不得只给可选窗口然后期待Agent想起申请；复核及一次纠正须预留资源。
6. 默认单笔续额为本Run初始获批步数/轮次各自20%向上取整，按需申请维度，有理由可调；当前96步/112轮对应20步/23轮。不是累计额度复利或每次必授。
7. 漏答给一次计入资源的明确纠正机会，仍漏答不授额并记录评估未完成，不伪造目标失败。授额/重规划不重置历史、消耗或失败事实；同一决定只授额一次。
8. 正常推进且下一段有依据可持续逐笔续额。偏差、同路线无新成果或证据不足时加强历史对照和判断；不因未经标定256步自动结束。明确时间/费用授权边界若存在仍执行；无此类边界不声称固定步数终止。
9. 修正旧Host停止路径与新策略的冲突：Director按停滞计数直接bounded-stop，以及账本滚动/失败/最终额度等出口需区分资源/安全约束、局部约束和复核信号；不能单凭计数替代Agent方向判断，也不能删除全部停止条件。

## 共享工作区与现场边界

- 路径/项目：/Users/fran/Documents/Code/spacex/uni_claw；移交前HEAD 2225c642、分支uni-harness。大量tracked/untracked为前序及并行工作；新会话必须重新保存状态和恢复依据，不reset/clean/stash扫荡，不搬到缺失这些未提交文件的空白checkout。
- SETTINGS-COVERAGE-006是已关闭的前序修复；SETTINGS-COVERAGE-005全遍历尚未完成。不要误改它们的证据或用其结果冒充本Change验收。
- 世界图现状：已有Containers/Relations证据执法，关系kind仅Contains/Overlays；ProductAssociationStrategy未产导航边；Settings feed固定ui.screen=android.settings；部分Driver/验证路径假设容器总数为1。图接入须同步处理当前交互锚定，历史页面不得参与当前grounding。
- 旧图测试18/18、入口状态测试1/1只证明原有基础。本Change须新增实际行为回归、场景和真实设备证据，并运行改动对应的生成/协议/回归检查；以proposal.md的反例和Acceptance为准，不重复无意义的大测试循环。
- 若使用现有本地测试环境，先读docs/agents/test-emulator.md并现场复核。此前专用DSH端口3081，3080不属于本任务；emulator-5556是借用设备，不擅自关闭。旧进程状态可能已变化，不凭移交记录直接断言在线。

完成汇报按docs/agents/work-reporting.md，用中文说明代码与行为变化、根因、验证证据、未完成项及下一步；不要只报Gate编号。
