# SIM-014 Spec Review

**结论：未发现受控范围内的可行动规格偏差；有边界通过。**核对依据为 SIM-014/SIM-015 接受范围、两个组件测试、测试侧构成 guard、固定 oracle、场景条目及增长证据。未构建或运行测试。

- 语义 Perception 测试经 Product Registry 取得真实 `UniPerceptionCapability`，通过公开 executor seam 装配 scripted/frozen-response 两种 L3 受控供给；失败/拒绝在供给调用前发生，校正请求经真实 Ledger 投影；Unavailable、Conflicted intent、response misbinding、重复均由独立字面 oracle 判定。取消测试使用不响应取消的 Hold 供给，先观察真实 L2 返回 Cancelled，再释放并等待供给确实完成，验证后续请求为 Duplicate 且已返回结果保持不变（`ComponentSemanticReplayTests.cs:16-43,46-54,57-105,107-179`）。这满足已接受的同消费者闭包与晚到语义。
- World 测试使用真实 `EvidenceLedger` 与 `WorldModel`，两种布局来源均经过相同消费者。错 capture identity 后页面/occurrence 保持无主，匹配身份恢复容器归属；重复 reconcile、矛盾身份与显式晚到旧布局覆盖均有断言，冻结原修订和输入也有逐字节序列化比较（`ComponentWorldReplayTests.cs:22-89,91-114`）。共享故障有单次 receipt，源输入保持不变。
- Oracle 独立固定成功/失败状态、页面内容及 World 计数；增长证据显示仅 suite 新增两个 source ID，并固定 runner、adapter、产品文件等构成哈希（`Fixtures/ComponentReplay/oracle.json`、`evidence/sim-014/data-only-growth.json`）。Guard 对五类构成 pin 做 SHA-256 校验，在创建组件前拒绝内容改变；治理测试覆盖每类变异及工厂零调用（`ReplayCompositionGuard.cs:6-50`、`ComponentReplayGovernanceTests.cs:15-49`）。这是内容一致性证明，不扩张为兼容性证明或通用依赖解析。
- SCN-REPLAY-008/009 的 Agent 与 Goal Evaluation N/A 均限定 `component-test`、显式 `testScope=component`、`execution.kind=none` 和不含 agent 的非空组件清单；SIM-015 同时记载跨语言与整装约束。场景 N/A 不表示产品通过。

**结论边界：**范围只证明两个已选 buyer 的真实组件经公开 seam 消费受控 L3 输入；语义响应/身份是合成输入，原始模型回答、实时设备及任意 L2 等价性均未证明。SIM-014 state 中仍为 `actual: NOTRUN`，本评审未将既有计划态/验证状态冒充为运行通过；这里只判断实现规格对应关系。未发现超出已披露限制的缺陷。
