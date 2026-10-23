# World 不确定性只读供给（WORLD-UNCERTAINTY-001）

地位：COMPONENT-BASELINE 的实现说明；本页不新增产品权威。

本实现细化既有 P2/P3/P4/P25/P27；Owner 与执行权不变。工程来源为 src/UniClaw.Kernel/World、Control/WorldRecoveryControl.cs、Assurance/RuntimeAssurance.WorldImpact.cs。行为规格以 changes/WORLD-UNCERTAINTY-001/spec.md 为准；正式整体 Goal、完整恢复、通用补偿仍延期。

## 页面、范围与历史

Slice 固定于一个 World revision；CoreSemanticProjection 的 SliceId v2 对 revision/root/ordinal 规范化 scope/可选精确 capture-frame region 作结构编码。不同 scope 或 region 不复用引用。region 不是新 Container，也不是完整页面覆盖声明。

WorldModel.DerivePageView / UniKernel.ProjectWorldPage 从一个选定 revision 派生原观察、解释来源、已证导航访问引用、上下文边、累计内容和未知。实际 capture、identity/correction evidence、World 提交分轴；无法证明访问发生时 VisitSourceRef 为空，普通 capture 不计新访问。历史对象无当前执行资格。ContentDigest 只供纯表达比较，不证明 currency。

WorldAccumulatedContent 只消费 World 已接纳并核验的同页滚动链。位移来自 viewport 对应；固定栏不随滚动平移；同名元素不自动去重；历史动态状态与当前存在仍未知。不相容/多前驱/缺对应时只给当前片段及未知，不以铺满画布宣称全页完整。SliceContentPresentation 为产品纯投影，Simulation Replay 复用；Oracle 与对照继续仅在测试侧。

## 四类缺口与补证

WorldKnowledgeView 给出 Identity / Attribution / RelationBasis / ObservationCoverage，各项携带明确对象、局部范围、来源 revision、evidence 和冲突性质。未采集是 not_collected，不是零缺口；无关系不等于无关系存在，关系缺口必须有具体 buyer 的 parent demand，不能枚举所有节点对。最小 Contains 需同 capture 的独立 native parent/child 来源并经 World 核对，只连接已有 Container。

真实补证执行记录经 RunDriverInput.Observation.Execution 或安全边界 PollSupplementationExecutions 输入 Control。ExecutionId 去重；同执行的较高 ResultVersion 只更新结果历史，KnownExecutionCount 仍为 1。KnownExecutionCount=null 表示没有已采集的相关执行记录；已有记录也只是 recorded-executions-only 下界。调度请求数不是执行数，World revision 也不是尝试。

有界补证后复用原 Interruption/咨询出口。相同问题和相同结果只主动复核一次；新真实结果可再复核；每次相关咨询仍携带未解正文。只在安全决策边界处理，不打断 StepVerify，不重置 request quota/deadline、Run 预算或权限；当前实现不自动证明跨 capture 问题连续性。

## 纠正与沿用

ui.page.interpretation.correction 是独立派生观察，必须精确指定原 capture/旧解释/处理来源。只在原设备、session、cycle、time、frame 及合法独立 native context 支持充分时匹配已有 Container。World 原子发布新 revision，撤去受影响关系/位移与误归属语义；旧 W/S/原 evidence 不回写，独立新观察不退回旧 capture。仅由被撤销解释支撑的旧 signature 不再用于未来匹配；历史 Container 保留，不购入通用生命周期。

历史语义带源先可读；当前沿用需 World 显式配置的不可变 NativeContextSignatureProfile、独立 native resource/package 来源、原语义验证以及本轮精确对应。默认无 profile 就不沿用，不由 model score/自报 marker/旧标题自动验证。WorldSemanticKnowledge 只沿用稳定含义，不包含旧 state/locator/visible/enabled；当前 Slice 排除不同 capture 的历史 scoped semantic claim。Host PageSemanticReadSupply 先读 World 当前供给，匹配才跳过慢 provider；无对应继续真实识别。本线的 profile 正例是受控来源，未声称当前设备已有可用 profile 或真机复用通过。

## 实际消费与效果

Control 记录实际咨询正文供给的 page/element/meaning evidence；Assurance 记录实际 binding/receipt 和对应 post-action 核验来源。纠正只对真实消费引用求已知影响，不把记录等同消费，也不把未找到引用等同零影响。已知独立 capture 分支可保留；旧页面血缘缺失则 DependenciesUnknown，现有追踪范围以 DependencyCoverage 明示，模型内部推理及其他依赖未获证明。

未投递的受影响计划在下一执行边界失效并重新咨询。已投递回执及原核验不可变，另追加 EffectCorrectionReview；在途核验不能继续将撤回依据判通过。Unresolved/DependenciesUnknown 不授予机械逆操作或盲重发，并阻止据此宣称完整 Completion。恢复仍需当前状态、目标和授权经原链核验；无可靠来源则保持未决。terminal 后仅关联复核，不改 Outcome、不重开 delivery。

## Agent 与材料资格

P25 AgentDecisionContext 新增可选 WorldKnowledge / WorldRecovery / WorldEffectImpacts，由 Owner 在每次正式咨询即时投影，纳入 request fingerprint。缺字段的旧协议材料保留 null；gap enum 使用严格字符串 codec。schema/TypeScript/plugin 副本由 ProductProtocolSchemaGenerator 同步生成；prompt/profile 工件单独修订并重新钉扎。

实际 Kernel→严格 codec→DSH HTTP→consultationPrompt 的两次正文留存，只证明已构造传输内容。tools/audit-decision-input.py 对完整 provider request envelope、messages/tools、截断规则、配置版本及规范 JSON 摘要作材料资格判断；缺失材料可诊断但不进 same-input 统计分母。同源副本不升级。该资格检查不认证采集真实性，不证明模型质量或现场行为；本线受控 HTTP/provider 的正文材料仍是 diagnostic-only。
