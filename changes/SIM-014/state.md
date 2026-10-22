# SIM-014 — 两个真实组件的协议仿真、回放与共享故障验证
lifecycle_state: closed · disposition: none · depth: standard · base: 9bc15b8974f5fb5746074e373e6c132bc4df0089

## Intent / Scope
用户接受SIM-013边界并要求继续下一步。验证Semantic Perception执行与World页面观察两个实际buyer，再提取重复机制。范围为新增tests/UniClaw.Simulation.Tests/ComponentReplay/、ComponentSemanticReplayTests.cs、ComponentWorldReplayTests.cs、ComponentReplayGovernanceTests.cs、Fixtures/ComponentReplay/、SCN-REPLAY-008/009、本Change/evidence。优先纯测试侧增量，不改产品源码、协议、注册能力、旧测试/录制/fixture/认证。真实Host接通在两切片后独立推进，本轮不启设备或真实模型，不提交推送。

## Decisions
- 已获授权的公开缝：ISemanticPerception.ObserveSemanticsAsync / SemanticPerceptionExecutor，Ledger.Admit，World.JudgeRelevance+Reconcile / PageObservationCodec；同消费者闭包切换外部供给实现，不调用internal或反射。
- 感知L2真件不替换：UniPerceptionCapability经既有Product Registry注册/取回；外部executor为脚本与固定回放两种受控实现。它们是L3供给，不实现L1；不声称真实provider/模型已运行，也不声称已证明任意L2实现完全等价。
- World和Ledger真件；初始布局/子页源自SIM-012同3条原XML冻结typed导入。身份及语义响应是明确受控替身，原始模型输出/逐帧时间未录得仍披露；新Oracle独立固定，不参与输入构造。
- 只提取两buyer重复的原录制布局读取、capture错配一次及注入回执、构成hash验证；组件专属typed协议保持分开，无万能Execute/产品仿真分支/新Registry。

## Acceptance
1. 两组件公开缝专项：固定成功/错capture/恢复、重复以及适用的Unavailable/Conflicted/取消/迟到语义；同一消费闭包切换受控供给，独立expected不读actual回填。
2. 同三组原录制可复用为组件输入；原XML/导入/旧fixture hash不变，新的模拟响应有synthetic provenance；一个新数据场景通过参数发现而无需修改产品组件或Runner。
3. 同一capture错配故障在两个buyer触发，均保留不可变原输入与注入回执；拒绝时不错误归属/产生有效观察，去除一次故障后恢复，不盲重发。
4. 测试侧实际构成记录产品协议/实现、adapter、录制和oracle指纹；各类内容变更时在创建组件前拒绝，保留RED→GREEN及注入负例证据。hash证明内容一致，不冒充兼容证明或通用依赖解析。
5. 独立两轴评审、隔离当前源码full八工程及场景/TRX精确关联、保护hash；组件/协议变更验证清单落档。具体测试侧接口和提取依据归档，不新增产品核心签名。

## Verification
level: SCENARIO
method: 组件专项与构成guard RED/GREEN、故障/独立oracle负向自证、参数场景发现、独立评审、当前源码隔离full、来源/保护检查
expected: Acceptance1-5
actual: PASS — 两真实组件22例、guard8例；8工程2101/2101、37场景，原35条未变；70 pins、同Kernel程序集，独立评审无遗留问题。
evidence: evidence/sim-014/

## Status log
- 2026-10-22 UNDERSTAND→RESOLVE→PERSIST→PLAN：用户接受边界后执行两个buyer切片；入口核对SIM-013设计与现有公开执行缝，写前保护完成。仅新增测试侧模块。
- 2026-10-22 IMPLEMENT前置裁决：发现纯组件场景无Agent而既有C7标注不支持；用户明确批准仅组件隔离可Agent=N/A。SIM-015承载schema/两侧执法/负例矩阵，父线等待其验证；旧35条及产品协议保持。guard worker无有效RED证据，Leader接管修正nullable JSON寿命与目标文件变异测试，独立最小项目得到非执法5失败/3通过→执法8/8。
- 2026-10-22 IMPLEMENT→REVIEW：组件初始22/22；新增两条数据行后组件/标注33/33；共享故障禁用4/4失败，独立oracle改错2/2失败，均为有意负向自证；Spec独立评审无可行动偏差，Standards待评。取消用例补等供给完成信号，最终full将覆盖该补强。
- 2026-10-22 REVIEW→VERIFY：独立Spec与Standards完成；Standards首次将SIM-007既有Goal N/A误报为新例外，按原Owner修订与固定正例复核后撤销。最终候选进入当前源码隔离full，旧条目与product源码保护检查继续。
- 2026-10-22 VERIFY有界修复：首次full仅DocsMetadataTests.Architecture_DeclaresFrozenAuthority失败（新C7局部修订写ACCEPTED，仓库要求FROZEN/CLOSED/COMPONENT-BASELINE）；修订按既有Owner批准记CLOSED。保留attempt1日志；产品/组件行为、37场景与旧认证均无失败，未修改预期或源码。
- 2026-10-23 VERIFY→CLOSED：最终full PASS（8工程2101/2101；Simulation314；37场景=20 certified+17 descriptive）；两新场景精确关联14+8个实际通过用例。原35条、产品源码、录制/导入/旧fixture/认证未改；4项既有元数据修改均属已授权SIM-015。边界、接口与生命周期清单见changes/SIM-014/component-contracts.md；真实Host/设备/模型和大模型候选生成不计作已完成。
