# C7 局部修订：组件隔离与 Agent 未参与
DocumentType: SIMULATION_C7_COMPONENT_DELTA
Status: CLOSED（Owner 已批准的 C7 局部修订） · Date: 2026-10-22 · Change: SIM-015
Authority: COMPONENT（产品基线优先）

用户于 2026-10-22 明确选择“扩展组件场景标注，严格限定范围”，满足仿真基线 v0.1 §7 的 C7 Human Gate。本修订接续 v0.2 C7 amendment 与 [SIM-007 专有载体评估修订](simulation-baseline-v0.2-c7-kind-none-delta.md)，仅增加无 Agent 参与的组件隔离范围；C1–C6、C8–C9 和既有整装构成保持有效。

只有同时满足以下条件的场景，可以把 agentDecisionRealization 标为 not-applicable：testScope=component、source=component-test、execution.kind=none；components 非空且不含 agent/uni-agent；两个 Agent realization 均为 not-applicable；expectations.status=not-applicable、agentConsultations=0、classification 和 goalSatisfaction 均为 null。effect 组件可作为被测对象，因此不统一强制 effects=0。

N/A 表示该载体没有装配或调用 Agent，不是 Agent double，也不是 Agent 决策、Goal 达成或整装 Run 的成功证明。组件真实构成与结果由专有组件测试证明；场景 status 仍由实际测试结果推导。省略 testScope 的既有场景保留原装配锚点，decision 仍只能为 real/double。golden-bundle 不得使用本例外，不修改旧场景及其认证。

机械执法使用 scenarios/schema.json、tools/scenario-coverage.py 和测试侧 ComponentScenarioScope；三者消费同一份固定 annotation-cases.json 正反例。SIM-014 首批两个组件载体以真实 UniPerceptionCapability 和 World 为被测对象，邻接输入受控；没有 Agent、真实设备或 live model。此修订不新增产品协议、Capability 或通用 Runtime 分支。
