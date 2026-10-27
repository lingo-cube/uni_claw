# AGENT-TRAVERSAL-001 — Agent遍历进度一致性与有界多步建议准入
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: a05176f3
diagnosis: {path: evidence/m/diagnosis.packet.json, sha256: 0e4f9f57ae09eedcb33c26b686f7e683e836bd8b38f9207ab4152e3b666d85ef}

## Intent
承接SIM037审计指出的产品问题：已验证进入事实与Agent进度摘要矛盾；多步Act/Plan准入不一致及拒收被表现为no-response。用户要求结合完整任务与当前上下文评价遍历决策，先核事实并聚焦grill，再按明确范围推进。

## Scope / Out of scope
切片P：修复Host只读Overview对configured根入口既有进入事实的投影，公开Ledger记录→Project→摘要/来源/可行动项回归。进入页只证明入口到达，不证明子树完成。
切片M：Owner Q1–Q4已确定并实施。Act为发起容器/覆盖层范围内的有界局部具体建议，可用同容器滚动后的可靠Slice；进入/返回/清障可作末步，范围变化后不投递tail。Plan可跨Slice表达后续阶段并逐步展开。已安装逐步Guard时延后Act检查；无Guard保守拒收；准确反馈原因/索引/剩余tail/本提案已验证receipt，保持既有安全与权限边界。
不修改Kernel/Evidence/World/Effect权威；不替换遍历策略、不预设DFS、不改旧case/Oracle/prelive pins、不继续提示词抽样、不触达设备或模型。观察台保持暂停；无提交、推送、基线晋升或服务重启。

## Decisions / Authority
- 执行仍逐步使用当前World接地、Guard、授权、投递与验证；前步未成功不得继续投递。这是既定原则，不重新询问。
- CANCEL只证明清障；返回允许调度/恢复，但不得丢失剩余工作或冒充子树完成。SIM034有限directive与SIM037自主任务保持区分。
- 进度投影恢复既有事实一致性；多步准入在Q1–Q4范围内完成，不能把本片完成推成SIM037真实遍历通过。
- Q1已决：正常规划一次可提交有界多步Act，已安装逐步Guard时与Plan一致；实际执行逐步准入/授权/验证，无Guard及超限仍拒收。用户明确选择“采用有界多步Act（推荐）”。该选择不授权改变局部directive或删除执行安全门。
- Q2由Q3/Q4细化：已知整体目标时允许灵活规划，跨Slice续行由Plan表达；普通Act离开当前容器/覆盖层范围即结束，不能以“清障优先”夹带底页tail。需要新的语义判断回Agent；Kernel不从总目标合成路线。实际用户单项范围、Contract、禁手和未知页面恢复边界不放宽；旧Host有限directive模式保持原约束，不把它当自主模式的全局单步门。
- 共享checkout保留所有既有修改。入口src/tests SHA及三个相关产品文件见evidence/entry/；使用私有源码/输出验证。RUNTIME-STORAGE-001当前已closed，与交接时进行中不同；本片不将SIM037旧封存当当前整合树认证。

## Q3/Q4：当前Slice与容器范围（已决）
用户明确回答：“Plan可以，Act最多知道滑动或者点击返回当前slice”。据此确认Plan可跨Slice表达后续安排；Act是由当前Slice事实支持的局部具体动作建议，不预写新页面或其他Slice内的具体动作。滚动/返回是用户举出的范围边界行为；多步允许不等于跨Slice预知。此前把“进入Network→进入后的返回”当普通多步Act允许样例不再适用，该跨Slice安排应由Plan表达。
Slice仍为World拥有的有版本/范围只读投影，不改作Contract授权范围，不扩大Slice迁就提案。滚动后仍须取最新Slice检查，不能一直沿用旧revision或旧binding；Plan跨Slice也不获得事实/权限权威。
Q4已决：用户回复“按推荐”，允许点击当前Slice可见的子页入口作为Act最后一步，进入后不含子页动作；用户进一步限定“顶多扩散到同容器滚动产生的已经可靠的Slice元素”。据此Act以发起时当前容器为范围锚点，允许同容器滚动更新可靠Slice后继续；离开容器后不得继续未执行尾部，目标/事实失效仍按既有出口回Agent。Plan保留跨Slice规则与阶段意图，不由Kernel补写路径。Q1–Q4已形成共享范围，不再重复确认。

## M实施范围与公开缝（按已批准链路）
Host仅对已强制接入逐步Guard的Act延后逐动作政策检查；无Guard保持保守限制。Kernel沿原StepAct/StepVerify机械执法Act发起容器/覆盖层范围及失败索引，保持Control/World/Assurance/Effect权威；每步新事实检查，Plan不受局部Act范围误限。公开测试缝为SettingsCoverageRunner.Run/RunAsync、KernelRunDriver/RunDriverInputs及SettingsActionGuard（均为交接已批准缝），用合成feed/effect隔离外部系统；旧Oracle/失败记录不改、未调用模型或设备。
同容器滚动、跨scope tail、Guard索引/原因、overlay确认禁手、unknown-route均有公开RED→GREEN；等待/失败/预算/无Guard/取消恢复及归属通过。AgentProjection、AgentActionProposal XML、P25/CONTEXT/README同步局部Act契约，不加wire字段。M独立固定源/执行物见evidence/m/candidate-final.json；P旧候选保持。共享树后续5文件漂移独立列出，本片不认证其最新整合状态。

## Acceptance
P1 公开记录已验证root→Network进入且无TargetMark时，Report的entered与Overview一致，来源可追溯，不再产生root:Network重复待办。
P2 只有观察、未验证、同页动作或其他父页同名进入不能伪造根入口到达；显式skip仍是skip；已进入页的未访子项保持独立待办。
P3 固定当前源码的相关投影/Runner/Director/策略回归通过或准确列出入口既有失败；旧SIM037记录检查仍RED，不能改旧证据追绿。
M1 按Q1–Q4公开Runner/Guard/Runtime验证局部安全多步、同容器滚动新事实、跨scope tail零投递、Plan跨Slice；前步等待/失败后步零投递、后步危险/未知拒收、无Guard/超预算拒收、取消/同实例恢复/显式重提及step/receipt归属准确；自主模式逐步清障禁手/未知路由约束保持，拒收反馈不伪装no-response。不以Host调度建议冒充Contract限制，不以知道总目标冒充动作许可。

## Alternatives / Open decisions
- 已采纳：正常模式允许已安装逐步Guard的有界多步Act，与Plan共用逐步检查原则；Act另受发起容器/覆盖层局部范围约束，Plan可跨Slice；无Guard保守拒收。
- 已拒绝：正常模式Act始终单步，连续建议必须改写为Plan。
- 最终Q2/Q3/Q4：Plan可表达清障后的跨Slice续行，Act不夹带新scope动作；清障后也不预定返回。Host有限directive仍经ValidateAdherence限定单项；自主组合依靠逐步Guard完整执法，已补入popup/unknown-route既有规则。无剩余Human Decision。

## Verification
level: DETERMINISTIC + SCENARIO
method: 私有固定源码，公开Ledger/Project/Runner/Guard缝，TRX与源码SHA绑定，旧记录一致性检查
expected: P1–P3、M1；宽测准确对照入口已有失败，不改旧pin/Oracle，不认证并行后续树或真实Agent整树能力。
actual: P的13投影/214相关回归已通过且原私有源/执行物保留；M新29/29、原79买方79/79，Spec/Standards最终0，m/verify.py通过。最终宽测Kernel1302/1305（入口1300/1303，同3失败）、Host941/956（入口914/929，同14失败，均1模型Skip）、DSH86/87（入口同86/87，同partial失败）；不称全绿。M固定2364输入/21执行程序集/15拥有文件，965保护源码不变、5并行owner文件单列，55旧审计输入及P封存保持。完整四元组/资格/限制见m/acceptance.md、m/attempts.md；本片完成并关闭，不关闭SIM037。
evidence: evidence/m/acceptance.md；evidence/m/verification.json；evidence/m/candidate-final.json；evidence/m/owned.patch；evidence/m/review-spec.md；evidence/m/review-standards.md；evidence/acceptance.md；../SIM-037/evidence/command-semantics-20261025/

## Residual risks
投影矛盾不等于整轮失败的唯一原因；修好摘要不能宣称真实Agent已完成三层遍历。缺页面范围的blocked scroll摘要另作风险，不宣称Root滚动被禁止。
M宽测原有3/14/1失败未修，其中Host12项旧源码pin阻断并非回放通过；并行5文件未整合认证，真实Agent/设备未复验，SIM037 A1仍FAIL/A2未运行。本片范围只证明固定产品候选的确定性契约，不推广为整树/真实设备/新共享树基线。

## Status log
- 2026-10-25 RESOLVE（Q3 Slice边界）：用户提出建议不超过当前Slice区域。核对baseline §Slice、CONTEXT Slice/Consumer View及ADR-0053/组合契约；区分当前事实投影范围、Contract任务范围与跨新事实展开的条件Plan。记录两种解释及潜在ADR冲突，未把该意见擅自定为禁止所有跨Slice规划，准入代码保持未改。
- 2026-10-25 UNDERSTAND→RESOLVE：核对交接四文档及当前源码；登记独立产品Change，保存入口SHA，复现冻结记录矛盾。P切片按既定语义构造公开回归；M切片只读核实并准备聚焦grill。
- 2026-10-25 P切片RESOLVE→PERSIST→PLAN→IMPLEMENT：公开Ledger记录已验证root→Network而无TargetMark，Report到达与Project pending矛盾得到确定性RED；Project读取既有EnteredRootDescriptors，显式skip保持优先、保留成功验证来源及独立子项待办，GREEN。未改Ledger/Runtime权威或新增状态。
- 2026-10-25 P切片IMPLEMENT→REVIEW：新增10/10、原投影3/3及相关214/214通过；精确红源/TRX与最终候选/程序集保留，旧55输入及972非拥有源码不变。M分支已异步发出两项聚焦问题，等待用户选择；仅P进入独立两轴review，整体仍resolving。
- 2026-10-25 P切片REVIEW→VERIFY：独立Spec与Standards均0，verify.py逐SHA与TRX核验通过。P切片完成，整体仍RESOLVE等待M的Q1/Q2，不将片内通过或真实Agent安全停止当作全任务完成。验收四元组及下一代码动作见evidence/acceptance.md。
- 2026-10-25 RESOLVE（Q1已决）：用户明确采纳正常规划有界多步Act；复核当前src/tests与P最终候选无漂移、P拥有文件SHA保持。已保存裁决及公开回归范围，Q2仍未回答；准入/反馈代码保持原样，不把未回答视为同意。
- 2026-10-25 RESOLVE（切换grill-with-docs）：用户要求改用grill-with-docs，组合grilling/domain-modeling；已核对ADR-0053，不重新裁决逐步验证或普通换页可继续原则。CONTEXT补记已决的Agent Action Proposal术语；Q2改用“本轮明确只要求关闭弹窗时，是否必须结束本段并重新询问Agent，而不能附带之后动作”的例子解释，尚无用户回答。没有把普通遍历中的所有清障升格为强制单步咨询规则。
- 2026-10-25 RESOLVE（Q2方向修订）：用户提出知道整体目标时应灵活避免死局；撤回局部directive一概强制单步的默认推荐，按清障优先、验证后有条件续行的方向细化。只读定位到Host单步注入/ValidateAdherence与goal-directed提前返回的两条路径；AGT019禁手/重授权/unknown-route限制仍为已接受约束。当前证据不支持Kernel死锁结论，也尚未证明自主分支这些安全约束的逐步完整接线。事实记录见evidence/local-continuation-design.md；本轮只改设计记录，未改准入/Kernel。
- 2026-10-25 RESOLVE（Q3已决）：用户允许Plan跨Slice，Act局限当前Slice，并举滚动/返回。已更新CONTEXT两术语，限定Q1有界多步与Q2灵活续行不扩张Act范围；将跨Slice具体Act样例撤回为Plan场景。剩余Q4只问已可见子页入口能否作为Act最后一步，准入代码仍未改。
- 2026-10-25 Q4已决→M公开回归：用户采纳入口作为Act末步并限定同容器滚动后的可靠Slice元素。已保存M当前1508输入独立源码副本；公开Runner首条“同容器滚动→新可见入口末步进入”回归启动。无新的Human Decision，不再以抽象A/B提问阻断；按TDD一次一片推进。
- 2026-10-25 M RESOLVE→PERSIST→PLAN→IMPLEMENT：公开Runner条数门RED→延后installed Guard GREEN；确认页身份后的跨容器/overlay tail RED→局部scope游标 GREEN；普通Act后步索引0 RED→清理前捕获正确index GREEN；popup确认/unknown-route既有约束在逐步Guard RED→GREEN。全部载体错误和无效mtime对照保留，不把它们计作产品RED，见m/attempts.md。当前活动诊断转引m/diagnosis.packet.json；原P诊断仍保留在SIM037审计目录。
- 2026-10-25 M IMPLEMENT→REVIEW→VERIFY→CLOSED：新Host27＋Kernel2全过，原79买方逐例复用通过；入口对照宽测同3/14/1失败，不改旧pins/Oracle。两轴最终0（不同overlay未证项由other-popup直接Act反例关闭），m/verify.py核验2364输入/21程序集/旧55输入/P封存/并行5文件与29+79及失败集合通过。仅此产品切片关闭，无模型/设备/提交/推送/基线晋升；真实正常路径和最新共享树整合仍为后续工作。
