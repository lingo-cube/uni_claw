# WORLD-SEMANTICS-002 — World语义关联的职责与可替换实现设计
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: bce0ec3a981a4598b889bb0bf37d2b3e389aa41c

## Intent / Scope
用户2026-10-21先要求设计，随后明确授权语义感知协议/Jev与World关联进入实现并联合验收。本Change承接WORLD-SEMANTICS-001检查，负责World身份时序/冲突保护、重访签名稳定性及既有关联替换缝消费；自动遍历通过既有插件替换使用新实现，不另建业务接线工程。统一导航见[World相关清单](../WORLD-CHANGES.md)。

## 已有材料 / 分工
- WORLD-SEMANTICS-001：已完成的当前代码职责检查、专项测试和诊断探针，保留原状态/证据，不把检查完成当成World缺陷已修复。
- JEV-SEMANTIC-CAP-001：负责上游语义感知执行载荷、Jev实现、共同公开面/文档与scenario验收；World复用其唯一协议和官方projector，不另造Jev专属载荷。
- World检查会话：01a15a5f-bc04-7b92-93e2-7ec00a397854，本Change记录用户后续独立立项要求；未发送额外会话消息或改变其状态。

## Design Constraints（用户指令及既有约束）
- 优先通过既有关联/能力插件替换，保持消费方协议和World所有权，不重复建设图、Slice图或运行时调用链。
- World仍决定ContainerIdentity及关系接纳；P22仅实际attempt/effect prior，不携带expected page/plan。前页/操作事实、观察推断、预期符合性保持分离。
- 前后页面身份、观察/revision关联与语义变化不是同一判断；导航边与同页视口转换沿用既有入口。
- 替换面是否充足须依据代码复核，不把“用户期望插件可替换”冒充现有实现已证明；若既有协议或World自身时序/冲突执法有缺口，在本Change及相关成员中明确设计，不通过provider越权弥补。
- 本Change实现WORLD-SEMANTICS-001已复现的当前ui.screen迟到回退、冲突推进身份与重访签名不稳定修复，并完成辅助语义消费；仍不扩展检查报告列出的所有关系/身份语义问题。

## 归组与后续设计
- 用户已澄清按World相关统一归组，无需指定两个会话。已通过../WORLD-CHANGES.md集中列出WORLD-SEMANTICS-001/002、WRC-001/002/003及UIW基础变更，保留各独立记录，不移动、合并或重开。
- 后续实施授权后分工已明确：本Change负责World与既有关联消费安全；主会话负责唯一语义能力协议/Jev供货、共同文档和scenario最终认证。当前World源码冻结，等待主会话联合收尾，不因归组而合并Owner或声明全部共享检查已绿。

## Assumptions / Alternatives / Authority
初始阶段只有设计授权；2026-10-21主会话转交用户后续明确实施授权，取代此前“先不实现”的阶段限制。各Change独立保留状态、证据与责任，不移动、合并既有检查或回退并行实现。仍不提交/推送、不改设备。

## 实现分工与保护范围（2026-10-21续行）
- 本会话：WorldModel内部接纳保护、关联策略及World测试；Host SettingsPageIdentity与既有签名测试。必要的新关联策略仅消费主会话已经落盘的唯一结果投影，不猜跨组件形状。
- 主会话：JEV-SEMANTIC-CAP-001、Perception/Capability、Jev realization、能力注册及联合验收；本会话不编辑这些文件、共享CONTEXT/协议/生成物/索引，不修改UniKernel或live feed的公共接线。
- 必要调用方增量：SettingsTraversalLiveFeed仅对既有SettingsPageIdentity调用传入accepted前页描述唯一性；没有增加Jev业务调用或另建接线工程。负责文件最终12项及冻结哈希见evidence/source-freeze.json。
- 基线：evidence/entry/files保留负责文件完整内容；owned-before.patch、owned-before-hashes.json、status.txt；protected-before-hashes.json覆盖852个源码/测试/协议等文件。已有他人修改均保留，差异以本快照而非HEAD计算。

## Acceptance（实现前可判定）
1. A(t1)→B(t3)→迟到A(t2)不回退当前身份/当前页面claim，历史Evidence仍保留；同页较新再确认后迟到异页也不得回退。
2. 当前页面claim本次发生冲突时，不得凭该声明New/Matched或追加关系；显式冲突与两端证据保留。新的无冲突观察仍可正常建立/延续身份。
3. 同一入口重访、occurrence id变化时，基于同一已观察语义区分信息得到稳定签名/World身份；缺少区分依据时不靠opaque id造身份。同名但不同已观察入口上下文不被无条件合并。
4. 导航/滚动/Scope/Slice/grounding既有行为保持；P22/预期页不进入证据或身份权威；新语义消费沿既有策略缝，接口契约等主会话落盘后复核。
5. 留下RED/GREEN、专项与必要回归结果、增量diff、并行文件归因和准确联合验收剩余项；不宣称真机或Jev联调已完成。

## Plan / 已确认测试缝
按用户“先完成可独立的缺陷保护与既有关联替换缝测试”指令，使用既有Ledger.Admit→World.Reconcile→Current/DeriveSlice、IAssociationStrategy.Propose及现有SettingsPageIdentityTests缝，逐项RED→最小修复→GREEN；不增加新公共测试接口。随后读取上游execution-contract.md完成World策略消费与联合确定性验收。

## Verification
level: SCENARIO
method: Ledger/World/Host回归RED→GREEN；Registry→L1执行→官方SemanticObservationProjector→P2→World→Slice替换联测；Kernel/Host/相关Simulation回归；自审与增量/保护哈希核对（命令见report.md）
expected: A1-A5；不回退身份、不从冲突/类别造身份、不用临时id区分重访；辅助语义明确关联当前capture，意图不入当前fact，原导航/滚动/P23/状态冲突语义保持
actual: 最终专项100/100；Host全量400/400；相关Simulation70/70；当前完整Kernel1009/1012，剩3项为旧analysis元数据、Simulation多引用及两个其他文件README登记；World自审已完成、12个负责源码/测试已冻结。未在本会话调用真实模型或设备，未提交/推送
evidence: evidence/world-reviewed-final.trx；host-final.trx；simulation-world-regression.trx；kernel-reviewed-final.trx；test-summary.json；source-freeze.json；preservation-check.json；owned-incremental.patch；review.md；integration-notes.md

## Remaining / 联合验收交接
- 主会话2026-10-21联合收尾已完成：共同协议/公开面/文档同步，场景28文件0违规；最终Host400/400、Simulation250/250、Kernel1011/1014（同3项范围外RED）。实际.NET Jev最终回放12例P2→Slice全部关联、身份保持且未造导航边；用途11/12、关系12/12、符合性11/12+1Conflicted，作为辅助语义限制保留。详见../JEV-SEMANTIC-CAP-001/report.md及evidence/jev-semantic-cap-001/verification-summary.json。本段以下保留原交接记录，不代表仍待实施。
- 主会话完成共同协议/公开面/文档与scenario认证，记录3个已有/并行共享RED的最终处置。本Change不修改这些超出分工的文件，不把“本范围verified”冒充全仓全绿或共同验收已CLOSED。
- World消费最小关联要求见integration-notes.md：accepted语义依据必须引用当前身份观察，且匹配exact capture和原观察时间；typed来源还核对session/cycle。现有身份观察没有所需相关性时保留Insufficient，不靠PageKey强合并。
- 仍未实现“仅凭语义类别区分同route新实体”、新的覆盖层/包含关系生命周期或语义目的地Effect验证；这些不在本轮已有协议载荷能独立证明的范围。

## Status log
- 2026-10-21 UNDERSTAND→RESOLVE：用户要求World独立Change并与另外两会话Change归组；复验已有检查已完成，另立本设计Change；澄清成员身份，保持设计阶段。
- 2026-10-21 RESOLVE：用户答“放WORLD相关的一起就行”；建立WORLD-CHANGES.md统一导航，独立Change/证据不移动合并。归组事项完成，继续保留设计阶段。
- 2026-10-21 RESOLVE→PERSIST→PLAN：接收主会话转交的用户明确实施/联合验收授权，核对当前文件与既有探针，保存负责文件恢复快照及852个保护文件哈希；先独立修复已复现行为，上游语义契约尚未落盘，不猜新载荷。
- 2026-10-21 PLAN→IMPLEMENT：迟到与冲突、New反证、重访/缺描述/重复描述逐项RED→GREEN；既有测试fixture显式区分不同capture和同scope矛盾。读取上游唯一执行契约，复用SemanticPageClaim/官方projector，实现Owner有依据的辅助scoped语义消费。
- 2026-10-21 IMPLEMENT→REVIEW→IMPLEMENT：全量发现最初通用claim保护过宽，收窄为当前ui.screen保护，保留普通状态权威裁决；自审发现重复入口描述和未声明JSON字段风险，反例RED后修复。未改上游文件，失败证据全部保留。
- 2026-10-21 IMPLEMENT→REVIEW→VERIFY：自审见review.md，最终World专项100、Host400、Simulation70通过；完整Kernel1009/1012，仅3个共享RED，准确归因。记录12个负责源码/测试冻结哈希，停止源码修改；主会话继续共同文档与scenario，当前保持verified等待联合收尾。
- 2026-10-21 VERIFY→CLOSED（主会话联合收尾）：JEV-SEMANTIC-CAP-001评审/回归/真实.NET回放与scenario完成；12项World冻结文件无漂移。保留三项范围外全仓RED和模型误判/冲突限制，关闭的是本次身份保护与辅助语义组件范围，不宣称实时设备遍历、所有World语义或全仓检查全绿。
