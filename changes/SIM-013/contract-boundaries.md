# 仿真核心契约边界档案（候选，未冻结新接口）

> 对应[设计](design.md)。仅收集已有协议与实现职责，明确待验证问题；Authority NONE。本轮不新增Ixxx、通用Execute、Contract程序集或协议schema。

## 1. 已有契约与真实buyer

|现有契约/入口|产品责任|真实测试buyer|替换/回放范围|
|---|---|---|---|
|ObservationProposal / Ledger.Admit（P2）|生产者提案，Ledger接纳|Evidence输入验证、感知输出接入|重放proposal；来源、kind、时序按原协议。不能输入canonical Evidence替代接纳并声称整链通过。|
|EvidenceRecord / World.JudgeRelevance+Reconcile（P3）|Ledger拥有Evidence，World负责reconcile|World页面身份恢复|组件隔离可由真实Ledger产生合法record；若使用邻居替身输出，声明Ledger在范围外。|
|PageObservationCodec / ObservationNeed|capture/identity关联与恢复|最近三页录制消费者回归|真实World处理capture，身份补充可以受控；它不证明真实标题识别。|
|ISemanticPerception / ObserveSemanticsAsync|能力产生proposal及assessment|感知producer与Runtime消费|确定性executor或真实producer实现同一契约；不能只比较健康状态。|
|IUiElementPerception|现有元素协议面|感知可替换性|执行载荷仍有buyer冻结缺口，不能据marker宣布通用元素回放完成。|
|IEffectDriver / DispatchRequest-Result（P14/P15）|只处理已授权request，EB规范化receipt|交付故障与不重复派发|受控结果/超时/不明确输出；不动态签发授权或直接写receipt。|
|IFreshnessEvaluator、association/observation/continuity策略|各自已有语义范围|外部策略可替换测试|替换策略≠替换整个Assurance或World Owner。|
|RunDriverInputs.NextInput / ConsultAgent（含P25）|Host供外部输入，driver协调Owner链|整装、异步、遍历、录制恢复|使用Product AgentDecision与Observation类型；不复制union或发逐cycle指令。|
|ILanguageInspector|有界语言检查与Finding|handler消费及可替换测试|需以L1调用，不以Generic ICapability+内部同名方法自证契约。|
|ScenarioBuilder / ScenarioImporter / MinimalScenarioBundle|测试资产构造、派生、执行输入|参数变体及既有trace引用导入|均在测试侧，当前internal；不向Product源码提供服务或引入反向引用。|

## 2. 共享部分的候选职责

只考虑两个buyer实际重复的机制：资产内容校验与来源链、逐例组合/清理、确定性输入安排、实际构成记录、独立断言及分歧报告。它们不拥有组件协议、不决定产品语义，也不复制World/Run状态。

每个组件保留专属typed接入adapter；测试代码依赖契约与可替换的输入实现，组合根决定使用真件或回放。组件无需感知资产文件名、场景ID或“仿真模式”。

新增公共缝前必须有两个实现/真实buyer证据，以及正例、拒绝/Unknown、乱序/重复、取消/迟到、资源释放的可判定结果。若某项不适用，给出协议原因，不机械填满所有维度。

## 3. 版本与权威

契约版本描述跨组件语义；实现版本描述实现修订；内容摘要描述实际字节；场景版本描述输入/独立期望修订；运行构成锁定本次实际使用对象。各自有独立用途，不创建“所有地方同一个版本号”的要求。

已知兼容组合用契约测试证明。未知组合拒绝或标未支持；不以兼容扩展名义绕过现有DSH schemaHash严格匹配。生成投影不成为新的手写权威；旧资产迁移另留派生物，原资产不覆写。

## 4. 尚未确定的接口形状

- 组件回放描述能否复用既有bundle/resolved-config，哪些仅需测试侧记录。
- 当前同程序集internal组合面是否足以满足两个buyer，是否真的需要更小的只读adapter输入。
- 哪些故障会重复使用，是否需要共享fault adapter。
- 组件/协议/替身/场景影响映射的最小机器可验证表达。

以上须经最小切片验证后再归档实际签名与schema。不能把候选信息清单直接升级为核心协议。
