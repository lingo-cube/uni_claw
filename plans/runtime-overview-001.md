# ADR-0052 / RUNTIME-OVERVIEW-001 实施计划

Owner整体提案已批准；2026-10-18恢复入口复验：uni-harness，HEAD 2225c642006e85290a6e98db82348a94fe3a66f8；共享脏工作区恢复依据见evidence/runtime-overview-001/entry/。不提交或推送。

## 实施切片与所有权

1. World页面导航（独立Worker）：既有World Containers/Relations、实际前后观察身份/导航证据、只读投影及图回归。不得拥有任务完成或预算判断。
2. Runtime明确复核（独立Worker）：KernelRunDriver、AgentPlanPolicy、AgentDecision、ConsultationTypes与新overview/review类型，主动复核、20%初始基数、一次计费纠正、幂等及独立维度、加严历史对照，附确定性回归。
3. Coverage事实与兼容（独立Worker）：Config/Director/Ledger的全局事实只读投影、配置种子与已发现区分、局部受阻与计数硬停分类，附覆盖场景回归。
4. Leader集成：Runner/LiveFeed与当前交互锚点消费；Dsh序列化、产品协议/Prompt/生成物、ADR-0050/0051部分替代、CONTEXT；运行记录同源overview；新增端到端确定性与现场证据。
5. Leader独立审查并处置，再按Acceptance验证。证据四元组逐项落盘；失败修复后重验必要范围，不能用旧图18/18或SETTINGS-COVERAGE-005/006代替本次证据。

## 验收缝

- WorldModel.Apply/只读WorldBelief：进入、返回、多入口、失败、不确定性与当前锚点，历史页不能参与当前grounding。
- KernelRunDriver.Drive/ConsultAgent：资源预留、显式复核关联、漏答一次纠正、正常续额跨256、重放与部分维度、未知/完成/偏差，累计消耗不重置。
- Coverage Ledger/Director/Runner：根页观察即全局待办，深入/受阻/新增/未知不伪造完成；同源overview送Agent且保留运行报告。
- Dsh通道：新载荷往返、旧载荷兼容、严格字段/枚举/相关性拒绝、schema与prompt哈希一致。
- 正式环境：先核查3081专线和注册设备，preflight落盘；新的观察/进入/返回及复核证据分别记录，3080不使用，借用设备不关闭。

## 完成约束

独立复核续行：Owner限制最多一个Luna Worker；复用同一Worker顺序做Spec/Standards，Leader处置P1/P2并重验。旧切片列表为首轮执行记录，不是后续并行授权。新增事项只读投影/NextItemKey及逐事项成果执法，资源纠正至少预留两轮；最终证据见evidence/runtime-overview-001/review-followup/。

新Run证据与确定性证据分开；Assurance/Outcome及动作授权保持原Owner。没有真实验收证据的项目保留未完成；范围全部完成且文档同步才CLOSED。
