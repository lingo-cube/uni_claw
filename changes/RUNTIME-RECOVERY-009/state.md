# RUNTIME-RECOVERY-009 — World原操作持久记录与独立历史读取
lifecycle_state: closed · disposition: none · depth: standard · base: a05176f3

## Intent / Scope
用户“进入下一步”授权实施008审计§6：原World完整操作的持久记录、独立纯历史读取、故障拒绝与实际非terminal进程Kill读取。拥有evidence/ownership.json内原World/Kernel/正式Host组合/文件存储/Probe/公开测试与README及本Change/INDEX/review WorkItems，保留其他dirty及并发SIM-029。没有World当前现场恢复、Control/Plan/coverage恢复、四方共同位置、执行续跑或设备/模型/live服务/提交推送。

## Decisions / Authority
- baseline§12/18/20、ADR0012/14/15/16/24/0054、RECOVERY001恢复契约及008字段/路径审计为依据。World保持唯一belief/reconciliation Owner，Host只保存Owner输出。P22/demand不变成Evidence；原Evidence引用从指定原readonly Ledger解析，历史读取不重新Admit/Reconcile/调用strategy或Outcome判断。
- 首片以World完整操作为提交单位：8个外层写入口覆盖9个revision发布点与独立relevance/demand/log变化；未变化的幂等操作不造事件。原修订/新增日志以增量保存，必要demand/来源映射/页面门限/撤回状态及原输入上下文完整保留，索引/metrics不作为原权威。配置与原Run recording/Evidence recording显式绑定。
- 持久World使用工作状态候选：完成外层操作后收集完整原输出，恢复已发布状态，等待journal确认后一次发布候选。失败关闭后续Owner/Kernel/Driver消费，不重试；确认已落盘但ack失败可历史读取，原writer仍锁存。单Owner同步执行，不宣称并发Owner/掉电/攻击者同时重写全部源与head。
- 历史入口返回独立不可变历史数据（原JSON字段与顺序），不返回可注入Kernel的WorldModel或Current/Binding。原录制GUID、原Run录制、原Evidence录制、schema/序列/原revision父链/必需侧状态/原accepted引用验证失败即拒绝。正式Host从初始化起写World；共享四方一致性另片证明。
- 评审细化：日志源/生效依赖必须来自原accepted且已处理记录；未应用纠正/语义提议中的未知ID逐值绑定原source Claim，不提升为accepted依赖；association candidates为原strategy proposal输出（可含被拒候选），只对生效belief依据执法，不读档重跑strategy。相同owner schema附加JSON字段原样保留，必需17字段不可缺，未知schema拒绝。
- 沿已授权的World/Kernel/Host/原文件/子进程公开缝做TDD；本轮不再请求常规测试接口确认。实际文件adapter与故障adapter是变化点，不创建Registry capability或新生命周期。

## Acceptance
1. 正式两Host写原World文件；独立reader保留原17根字段、原日志/需求/来源/撤回/配置/输入与原身份，不重判、不写源、不生成当前事实，旧无World源不默认为空。
2. 普通/冲突销案、overlay/context/semantic/correction、导航/viewport/continuity与仅demand/relevance/log变化覆盖完整操作边界；特别Publish后的原日志/门限/映射不丢失。幂等无变化不追加；原集合顺序在历史数据中保留。
3. 持久确认前外部只见原已发布状态；提交前/后ack故障都锁存Owner/Kernel/Driver，后续不新增Effect/consult/World；提交后ack故障可读原已提交记录但不许原writer继续。
4. 缺源/坏源/半写/整尾回退/未提交后缀/head错误/未知版本、错World/Run/Evidence源、错序列/parent/原accepted依赖/缺副状态全部拒绝；readonly源不能初始化或追加。
5. 原公开接口RED→GREEN、相关回归、重复读取源SHA不变、实际非terminal Kill→不同reader PID，给出完整原字段/来源及限制；不以合成/单测当真机/当前恢复/续跑。
6. 对相应正式受控规模记录新增存储/读取代价，避免每次重写全部revision/log前缀；双轴独立review、明确owned增量/保护原输入及四元组，可复验且不修改旧证据。

## Plan
先用原World无revision的demand与正常revision证明文件/历史入口缺口；接入候选状态事务与增量原记录、独立reader/文件adapter；按行为逐项增加故障/拒绝与全路径覆盖，接两正式组合和Probe；回归/代价观测/独立review/原源核验。

## Verification
level: SCENARIO
method: original public World/Kernel/Host seams and controlled independent process Kill/read
expected: A1-A6
actual: Final World 62/62 passed without exclusions; 619 build inputs stable; original native/history/order/source/PID checks passed; Kernel prior 550 behavior plus separately fixed surface 1, controlled DSH 5; wide Host remains historical 693/706 with 13 unrecertified gates; current/four-owner/continuation false
evidence: evidence/

## Status log
- 2026-10-24 UNDERSTAND→RESOLVE→PERSIST→PLAN：008来源/九写入点/侧状态/只读边界审计已复核。常规实现与测试接口沿持续授权；历史数据与live WorldModel分离，先原记录/读取再四方共同位置。无须用户作格式/工程次序选择。
- 2026-10-24 IMPLEMENT：原正式Host无World源行为RED0/1→GREEN1/1；原revision及仅demand读RED0/1→GREEN2/2；提交callback重入先错误新增Evidence的RED0/1→GREEN3/3，补Kernel消费期间阻止重入。reader误将普通claim subject当typed引用RED0/3→GREEN28/28，改为只验证schema明确位置。Driver原过程World持久异常外抛RED0/1→typed WorldPersistenceException与原Driver失败出口。记录按完整操作候选确认后发布，typed记录/侧状态/原配置身份与纯历史JSON入口已接两Host。
- 2026-10-24 IMPLEMENT：40项定向行为通过，涵盖真实原Driver已核验导航/viewport并逐一比较全部127/156个World修订字段/顺序、已应用纠正及撤回/拒绝纠正/context/semantic/overlay、公开Driver路径冲突销案、continuity提交及非revision日志/demand、文件完整性/原源/依赖拒绝、确认前后故障与非terminal进程Kill+不同reader重复只读。另追加原空文件拒绝和配置二进制身份检查，最终回归执行中。曾出现测试oracle/夹具不合原规范（日志误断言异常确切类型、场景只导出8个选择修订、JSON等价转义差、单项layout不足、NoAction空完成、缺Task绑定）；保留全部尝试，未修改原业务判断适配夹具。
- 2026-10-24 VERIFY环境范围：未排除的Host构建再次因并发SIM-029新增SettingsTraversalIntegrationTests.cs缺UniClaw.Agent引用而失败（entry已有外部源，未修改）。定向与既有Host回归用唯一显式DefaultItemExcludes隔离该新增文件，相关原代码/测试及本轮测试仍全部编译；不声称共享workspace完整Host suite已通过。最终证据将单列这一未解决外部编译门。
- 2026-10-24 IMPLEMENT→REVIEW：锁19文件候选及entry精确diff；World持久化/读取代码已定。完整Host尝试706项中693通过、13未通过：12个旧recorded source guards因entry已变化的Ledger指纹拒绝，新候选与原录制兼容性未获认证，保留旧manifest/oracle；旧WorldOperationReplay完整串比较因新Origin签发者OwnerId每次不同失败，移除本轮全部owned增量的独立副本同位置复测RED，未为绿改写该旧oracle。新World检查41项全部通过（含两真实非terminal Kill模式各两个reader），Kernel551项里550行为通过、1白名单排序误插已单项修正通过；受控DSH5/5。200观察/401操作/200修订约2.36MB，写约706ms读155ms为本机受控样本，非长任务性能认证。为独立证明完整原字段/顺序和源未写，补native导出的全原World历史及reader前SHA工件，正定向复验4例；不重复已通过的其余行为。
- 2026-10-24 REVIEW→IMPLEMENT：Standards v1零发现；Spec v1发现日志typed来源校验遗漏及附加字段误拒。日志14个篡改用例RED（原未知提议合法读1项通过）；按原applied决定/原source payload验证，不重判。独立原帧/17字段/全127与156修订/两种Kill各两个reader/源SHA核验通过，保留原verification v1待候选修订后重新核验。并发FiniteMenuGraphSimulationTests.cs又引入不存在的EvidenceLedger.ComputeEvidenceId导致green和extension尝试编译阻断，明确加入第二个测试编译隔离项；非本轮实现失败，不修改该外部文件。
- 2026-10-24 REVIEW修订v2→v3：原日志accepted/processed来源和拒绝提议payload绑定、同schema附加字段保序已修；Spec v2另发现撤回完整快照可混入无关accepted ID。四项有效RED→新增累计原撤回日志与每行快照精确集合核对、原relevance input绑定→受影响7/7。原claimEvolution夹具必须同producer异显式scope才Revise（仅改时间不足），该处两次夹具失败保留且不当产品RED；故上条14项失败中只有12项是有效漏校验RED。
- 2026-10-24 VERIFY：最终候选v3的62项World测试无任何排除参数全部通过，619项C#/项目/构建输入前后SHA完全一致；原帧/head/EOF、原Evidence身份、17必需字段/附加字段、全部127/156修订的值与顺序、两种非terminal Kill各两个reader及原源SHA独立核验exit0。v1/v2原生文件与失败工件不覆盖。早前%3B排除列表实际未生效，MSBuild Compile项已核实并纠正记录；外部两项编译缺陷已被并发编辑修复，最终不再排除。宽Host13旧门未重认证，未重跑并声称全绿。
- 2026-10-24 REVIEW→VERIFY：独立Standards v3与Spec v3均确认19 SHA匹配，无未解决可行动项；v1/v2发现和修复过程完整保留。World仅原记录/纯历史读取，当前现场、四方共同位置和执行续跑仍关闭；无设备/模型/live服务/提交推送。复验脚本与A1–A6四元组见evidence/README.md。
- 2026-10-24 VERIFY→CLOSED：最终双轴评审和证据/复验说明齐备后，独立verify-before-close退出0，A1–A6在声明范围内满足，19 owned候选与619最终构建输入未漂移。原宽Host13门及历史失败原样保留，不作为本片通过证据。下一步将World原操作位置纳入Runtime资源确认点，认证原Run/Resource/Evidence/World四方共同历史位置；当前恢复/执行续跑继续不宣称。
