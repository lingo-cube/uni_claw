# RUNTIME-STORAGE-002 — 任务持久化职责隔离、版本快照与可靠报告交付
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: a05176f3c66e0a3206f49dde463f51df1335975c
diagnosis: {path: changes/RUNTIME-STORAGE-002/evidence/slice1/diagnosis.packet.json, sha256: ca1b395ed8c63b394cd8c25767a4365ba42214f4bf13fffa4420e4aabbda5936}

## Intent（WHAT / WHY）
围绕一次Task Instance的生命周期组织持久化：可靠保存任务实例、必要执行过程、任务结果及恢复所需信息；Trace、诊断日志和完整World演变默认也保存，但不因辅助保存失败改写正式业务结果。核心可变状态优先提供最新状态，通过版本和增量正反推导保留期内的历史；辅助历史尽量增量。必须交付的报告拥有跨重启可查询、可重试的独立交付状态。

RUNTIME-STORAGE-001已关闭，完成的是原生异步与合法批量确认，不是本次职责隔离、快照/逆向历史或可靠报告交付。沿用其已接受提交边界和兼容性要求，不重开或改写封存结论。

## Scope
- 对现有生产存储逐项建立职责、Owner、真实消费方、可靠性、生命周期、读写契约、来源版本及保留规则清单；包含九条已迁移路径、Host任务/Session/启动存储、Settings覆盖账目、原始附件与各类输出。
- 隔离正式流程与辅助导出，覆盖咨询观察者、遍历恢复观察者及两正式Host的产物/收尾路径；保持规范化执行结果、正式咨询结算、业务资源消耗和Effect投递次数。
- 核心可变状态的最新读取、保留期内版本读取与变化读取；按实际buyer采用版本化增量和快照，正反推导数据状态而不执行领域操作。独立不可变事实保持完整记录，不强行差分。
- 辅助历史采用有界的增量/追加采集；快照由实际读取成本决定，不要求所有辅助日志支持逆向增量。
- 现有确定性报告的必交付要求、来源绑定、必要输入保全、交付状态及重启后的查询/重试；首个buyer为已有run-report/coverage报告路径，不增加新的报告种类。
- 生产异步提交与旧同步兼容分离；按真实buyer分清写入、最新/历史查询、辅助采集及报告交付职责；Host负责介质、作用域和释放，领域Owner保留语义初始化和事实认定。
- 明确保留/清理/归档契约、未交付报告来源保护和配额；仅在策略明确、保护条件可证明时启用相应自动回收。以测量优化重复序列化、写入体积和重建成本。

## Out of Scope
- 不实施自动续跑、完整恢复游标、执行路径剪枝或任何新的Effect重试/补偿编排；报告重试不扩大为任务重跑。
- 不建立通用数据库、WAL、消息平台、统一领域事件模型或跨Owner原子事务。
- 不新增诊断模型调用、不改变模型路由、不触达真机/模型，不增加远程发布、发送或外部报告投递。这里的交付指现有Host可校验、可读取的本地产物。
- 不原地迁移、覆盖、删除或重哈希已封存历史；旧格式读取保持，已有证据不重新标记为本次验证。
- 不扩大到OS崩溃、掉电、磁盘损坏或跨节点耐久性。共享脏工作区中其他任务改动一律保留。

## Decisions（用户已确认）
1. 分类按需求职责，而非目录、文件扩展名或是否可计算：任务实例/结果和必要执行/恢复信息为核心；诊断、Trace和完整World演变通常为辅助。某份World材料或原始附件若承担恢复、结果证明或必交付来源职责，对该用途按相应可靠性保护。
2. 核心保存最新可验证状态以及版本化变化，保留承诺范围内的正式事实顺序和关联，支持正反历史推导；不保留所有版本的重复全量对象。必要旧值、删除内容、顺序和前后版本不能省略。不可变事实通过完整记录和版本前缀读取，不强制生成逆向补丁。
3. 辅助默认保存、尽量增量，可按明确策略轮转、压缩和降低详细程度；允许采集缺口，但必须标记范围。已被任务证明、必交付要求或显式保留引用的材料不能作为普通辅助缓存清理。
4. 最新状态和变化必须对应同一已确认版本。单Owner原提交确认不削弱；入队不是成功，接受后的取消先结算，未知结果不能盲重试。跨来源只声明实际可验证的版本组合，不宣称全局原子快照。
5. 快照频率由增量字节、重建耗时和任务阶段实测确定；大对象不每步全量重写。快照可为读取加速，也可能承担恢复依据，两者必须在契约中区分。前者只有在完整可信历史可用时才允许失败回退。
6. 反向推导只还原数据，不回滚设备、不重新入证、不重跑World策略、不恢复执行许可，不改变现有只读恢复与自动续跑的边界。
7. 辅助失败、拥塞和有界关闭不能传播为正式咨询失败或改写已发生结果；辅助有独立错误、配额、等待/关闭策略。拒绝无限等待、无限积压、每条Task.Run和fire-and-forget充当可靠性方案。
8. 必交付报告要求在被接受时持久记录，并绑定稳定来源；生成/验证/发布结果单独结算。执行成功不等于报告已交付；报告失败不回写Runtime Outcome。重启后可发现欠缺交付并显式重试，重试不得再次咨询Agent或投递Effect。
9. 必交付报告的必要输入必须可恢复，不能只持久化pending标记。来源在交付未完成时受保护；若确实缺失或损坏，显式返回不可生成原因，不能伪造报告或重跑任务补材料。可选Trace缺失是否允许降级，由既有报告必需字段/完整性要求决定。
10. 保留政策与编码方式独立。新快照不自动授权删旧增量；核心身份、幂等、未决、结果/证明和报告来源分别检查。策略或期限未明确时不启用破坏性自动清理，不自行发明天数。
11. Host管理App/Project/Run/单操作资源作用域；Owner不转移。只抽取多个真实buyer共有机制，类型化、分页/流式和索引按实际消费需要接入，不预造统一接口。

## Acceptance（先于实现）
| ID | 可判定完成条件 | 验证方法 / 预期行为 |
|---|---|---|
| A1 | 所有当前生产存储/产物都有职责、Owner、消费方、提交位置、生命周期、可靠性、配额及保留/重建声明；混合记录逐用途区分 | 生产装配/读写调用清单与架构守卫；覆盖两正式Host、九源、Host身份/启动及覆盖账目，不以文件名推断角色 |
| A2 | 辅助关闭/抛错/停滞/拥塞/关闭超时不污染正式业务结算 | 相同确定性Driver/Host场景两臂比较规范化结果、咨询Completed/Rejected、核心提交、业务资源计数、Effect调用/回执；咨询已成功时导出失败不得产生consultation-exception拒绝 |
| A3 | 最新状态、增量和提交位置一致；部分写入/确认丢失/取消/损坏不发布伪最新状态 | 注入写前、写中、提交后ack前故障与真实子进程Kill后新reader；已发生回执保留，未确认prepare零投递，不能静默退到旧版本并声称最新 |
| A4 | 核心状态在声明保留范围内可正反还原，且最新读取不必展开所有历史版本 | 各适用核心记录族的公开契约验证；修改/删除/顺序/空值/边界/未知版本、错基线、跨Run串源、重复/缺失增量均覆盖。还原值及集合语义与原记录一致，正反往返一致，未知范围明确不可用；零业务重执行 |
| A5 | 辅助增量可按基线还原并明确缺口；快照策略能限制需要重建的链，普通日志不强制逆向补丁 | World及已有报告/诊断buyer公开读写验证；独立快照与后续增量读取，缺失/损坏基线拒绝或显式不完整；不修改原Owner逻辑事实 |
| A6 | 必交付报告需求、必要输入及状态可跨重启恢复，完成可校验、重试幂等 | 在接受要求后、输入固定后、生成中、产物写出但交付未确认等窗口Kill；新Host发现同一要求并只生成/校验报告。产物与来源版本/摘要匹配才已交付；正式结果不变，Agent/Effect重试调用为0；来源缺失显式失败 |
| A7 | 提交、查询、辅助采集、报告交付有清晰的领域契约与Host资源归属；生产原生async不退回伪异步 | 公开契约/装配/生命周期测试；同步兼容集中且旧调用可用，初始化仍属原Owner、关闭有界且未确认项可见；不同介质运行相同适用契约，核心不依赖文件codec |
| A8 | 回收只作用于策略允许且不破坏承诺的数据 | 缺省未配置、活动任务、未决Effect、幂等身份、结果证明、待交付来源、已指定保留及增量基线保护用例；缺省不做破坏性清理，策略允许的辅助轮转不破坏保留区间还原，删除范围和原因可查询 |
| A9 | 旧格式和已有报告/恢复消费者兼容，原历史不改写 | 真实已有v1/v2载体及公开reader/报告工具对照；保留原来源身份、序号、逻辑值和比较语义；旧文件SHA不变，不把历史可读升级为继续执行权限 |
| A10 | 性能优化有同场景、同逻辑内容的成本证据，文档与配置唯一源同步 | 对增长型核心账目与World辅助历史比较实际保存字节、managed写/flush、最新/历史读取扫描与解码量、等待及峰值内存/积压。证明减少目标buyer的重复保存和最新读取展开量；单列逆向信息、快照和可靠报告的新增成本，不要求所有记录族都压缩或全部更快 |

每项验收最终填写method/expected/actual/evidence；需相关回归和独立Standards/Spec评审。旧测试数字只是上片封存记录，不能当作当前通过；实施时按精确候选确认既有失败与新增失败。

## Constraints
- 保留共享脏工作区和并发业务改动；候选只包含本片所有权，禁止用宽HEAD diff归属他人变更。
- 正式提交确认、发送前准备、接受后取消/未知语义、旧格式及已封存证据不削弱或改写。
- 测试只走已确认公开缝，禁真机/模型；数字、性能收益和故障范围按候选实证，不继承旧通过声明。
- Host借用领域接口并负责资源作用域/释放；辅助模块不能取得核心Owner或执行许可。

## Owner / Authority impact
- Run、Evidence、World、Control、Assurance、Effect及Host任务/Session/启动身份的既有Owner和提交语义保持。持久化adapter只确认保存，不推导业务事实、不取得恢复授权。
- 覆盖账目参与实时任务判定，不能因最终文件叫report就整体降为辅助；报告输出与其原事实来源分开。
- Host负责必交付报告的要求/交付状态，不成为第二Runtime Outcome或Effect Owner。报告与诊断读模型不得回写canonical source。
- 同一材料可以有多项用途和保护条件；可靠性不是必须复制成多份新真相。

## Assumptions / Alternatives / Residual risks
- 当前可用恢复读取以历史检查/只读重建为主；自动续跑未由本change授权。最新快照不是可执行checkpoint的同义词。
- 拒绝全存储统一CRUD、全部每步快照、只有单向新值却声称能反推、最新状态与历史各自无关联覆写、辅助失败进入业务catch、只存报告pending却丢唯一输入。
- 逆向信息可能增加体积，辅助增量不承担不需要的逆向成本；不能在方案阶段声称净字节或延迟必降。具体格式/阈值由buyer验证，不把工程参数变成人类阻塞项。
- 硬盘容量、OS调度仍是共享物理约束，异常/等待隔离不等于无限资源；配额和保留保护耗尽时显式报告/限制新接收，不吞核心记录、不承诺零物理争用。
- 多来源快照必须沿原身份/提交末位验证；缓存损坏的回退不得掩盖核心源损坏、末位回滚或尚未结算的写入。
- 当前没有启用旧历史删除的授权；任何格式替换、保留范围变化和自动回收须证明既有消费者与承诺不被破坏，不能靠关闭reader校验实现优化。

## Architecture / Source refs
- `docs/architecture/product-architecture-baseline-l0-l3.md`；`docs/adr/0013-run-trace-is-reference-oriented-non-authoritative-projection.md`；`docs/adr/0023-reliable-execution-source-is-effect-boundary-owned-with-append-only-file-journal.md`；`docs/adr/0055-async-batch-storage-requires-confirmed-commit.md`。
- `changes/RUNTIME-STORAGE-001/state.md`；`changes/WORLD-HISTORY-002/state.md`；`docs/agents/work-reporting.md`。
- 本轮分类表、失败/保留矩阵和接口职责：`storage-contract.md`；只读审查锚点及入口指纹：`evidence/design-entry.json`。
- 新格式/交付契约若触及现有ADR的冻结语义，先补充对应决策记录再实现；前片“不做周期快照”是当片scope，不据此改写其已封存范围。

## Verification
level: CONTRACT + DETERMINISTIC + SCENARIO（真实子进程中断；非设备/模型）
method: 各片固定入口RED→GREEN、原事实/真实旧载体SHA、原Owner确认与数据正反查询、实际报告/HTTP/进程Kill、独立双轴；最终在私有index导出的精确源上构建并回归，源字节与Git blob逐项核对。
expected: A1–A10按声明buyer和保留范围满足；辅助不改结算，核心确认与历史可判定，必要来源/交付重启可查且不重跑任务，旧源不改写，参数/成本取舍明确。
actual: 最终联合Host411/411、Dsh42/42、Python67/67；Kernel166/166的已验源与最终候选逐项一致。WorldStorageProbe与既有组件Replay载体独立编译通过；总源/结果核对811项通过。Host、World、Coverage和集成范围的两轴最终均无剩余finding；初始失败及修复记录保留。全设备/模型、OS掉电与任意历史格式未据此认证。
evidence: acceptance-matrix.md；各片README/verification；evidence/host-projection/verification.json；evidence/world-baselines/verification.json；evidence/coverage-delivery/verification.json；evidence/publish/verification.json、final-source-map.json、finalization.json。

## Plan
见`plans/runtime-storage-002.md`。先复现并隔离正式咨询/遍历观察错误，再做必交付报告、核心版本快照与辅助历史维护。用户已授权Leader实施及简单工作交Luna，公开测试缝沿本文件已确认验收，不重复请求确认。

## Status log
- 2026-10-25 UNDERSTAND→RESOLVE：已核对当前uni-harness/a05176f3及共享脏工作区；用户分轮确认任务实例/结果/恢复优先、核心最新＋版本增量正反推导、辅助默认增量、必交付报告跨重启可查可重试。无待决产品取舍；参数和机制由真实buyer验证。
- 2026-10-25 RESOLVE→PERSIST：用户要求检查后可开change；本次仅创建本state及storage-contract/入口事实记录并更新派生索引。未改产品代码、未开始实施、未运行真机/模型或重跑全量测试，前片保持closed。
- 2026-10-25 PERSIST→PLAN：用户授权“Leader直接开干，简单交Luna”。复核同branch/base，捕获首片入口SHA，落垂直切片计划与两项有限WorkItem；存储清单/回归测试由Luna独立拥有，Leader负责生产实现与最终验收。

- 2026-10-25 PLAN→IMPLEMENT：首个公开Driver RED已复现：相同有效NoAction，禁用observer为Completed、observer IOException为AgentDecisionFailed。定位证据见evidence/slice1/diagnosis.packet.json；开始首片实现，父A1–A10尚未完成。

- 2026-10-25 IMPLEMENT证据更正：Luna首轮日志被后续重跑覆盖，旧diagnosis-unverified-v1.json不作验收依据；隔离入口副本baseline-red-v2重新证明P25 Completed→Rejected，候选同例GREEN。正常Run结果可为TerminalNotProven，前条“Completed”仅对正式咨询结算成立；原失败资料保留。

## 首片进度与下一步
首片（A2主体/A7局部）已完成：Driver诊断队列与两Host受限产物、独立关闭/状态；新增6例，两个旧观察消费者适配新关闭契约。纯v6相关Kernel25/25、Host12/12，生产沿v4/测试v6独立两轴0；自包含985输入＋26程序集绑定，新增仅1个测试overlay复用封存输入归档。

扩大共享工作区回归为Kernel1300/1303、Host915通过/13失败/1跳过、DSH86/87，分别在首片README说明入口对照与证据级别；不声称全绿。并发Act-scope/failed-step未撤销，单列parallel patch，不归本片。父A1–A10未全部完成，不关闭。

该条是首片结束时的历史下一步；第二片进展及当前下一动作见下文。自动续跑/路径剪枝仍不在本change。

- 2026-10-25 IMPLEMENT→REVIEW→IMPLEMENT→REVIEW→VERIFY（首片完成）：咨询/Host原入口行为RED→有界单消费者和同一Host配额writer GREEN；Spec/Standards发现的运行回调绕额度、全量JSON后验限额、拥塞断言缺少条数均修正，最终两轴0。外部并发Act业务hunks从纯候选排除且原样保留共享源；纯v6 Kernel25/25、Host12/12，入口三个Kernel失败与DSH partial失败独立复现，Host旧fixture门/随机OwnerId单列。父范围继续实施，不把首片验证当作全部关闭。

## 第二片进度与恢复入口
run-report 首个buyer已实现：`RequiredReportDelivery`持久接受要求、固定脚本/输入并校验发布，`RuntimeToolHost.Reports`经registry调用、重开查询/仅报告重试；`ReportDeliveryEndpoints`提供HTTP读取和重试。Runtime显式requireReport在attach/dispatch前接受，实际执行先终态，随后`UpdateArtifacts`只更新产物可用性。原Coverage等Owner结果保持原写路径，不声明其全部接入新交付协议。

- 2026-10-25 IMPLEMENT（第二片）：先保存入口13文件及公开行为RED；Luna实现输入清单、公开测试/HTTP端点/探针，Leader实现交付存储、实际工具/Runtime入口及最后审查。来源缺失、生成失败、私有内容变化、非合作超时、已交付后损坏均有独立状态/可读性验证。
- 2026-10-25 REVIEW→IMPLEMENT→REVIEW（第二片）：修复错Run来源绑定、Host单杀后遗留Python并发及正常关闭遗漏追踪。新增错源2例、真实Host-only Kill及正常关闭3例；子Python自己持OS锁，遗留本体存活时新Host显式busy。Standards关于另造报告批量队列的初始意见，按ADR-0055单逻辑确认/因果屏障条款复判撤回，保留原评审和适用性补充；不声称批量收益。
- 2026-10-25 VERIFY（第二片完成）：冻结v2的1007输入/44程序集绑定；隔离Host23/23、DSH50/50，输入CLI2/2，旧PNL006 golden一致。4个进程Kill窗口及原immutable文件SHA保持通过，Host-only Kill从重叠2调用RED变为1调用/busy GREEN，释放后同id交付且重复不再生成。两轴v2均0；首片封存摘要不变。纯DSH准备阶段缺readonly registry/旧退出脚本的失败保留，补齐输入后50/50；共享全量Host958通过/14失败/1跳过、DSH92/93属于较早v1观察，不能当最终纯候选通过。新增存储成本样例89475字节含兼容镜像，仅是成本测量，无压缩/延迟收益声明。

第二片结束时的下一步为资源最新/正反历史；当前进展见下方第三片。父A1–A10仍open，不自动删除旧增量或续跑任务。共享Host README并发新增SettingsPageScene登记原样保留，单列parallel-readme-v2.patch，不归本片。未提交/推送，未运行真机或产品模型。

- 2026-10-25 IMPLEMENT（第三片开始）：复验第二片封存通过、同uni-harness/a05176f3。以资源账目累计快照为首个增长核心buyer，冻结resource-version-contract.md：原Owner/记录接口及严格恢复不变，Host新v4增量+checkpoint+原子head及只读正反版本查询。父A3/A4/A10按实测逐项推进，不扩大为自动续跑或全记录族完成。

## 第三片资源家族进度与恢复入口
- 2026-10-25 IMPLEMENT：FileRunResourceJournal新文件切v4，完整Initialize事实保留；确认快照的可逆结构变化保存原值/新值、列表splice及object存在性/顺序。按ResourceVersionOptions记录/字节阈值生成读取基线，head同时绑定log末位、长度与已确认基线引用。原Runtime记录接口/计费/来源/只读恢复资格不变，未造generic DB或第二Owner。
- 2026-10-25 IMPLEMENT/兼容：新ReadLatestVersion/ReadVersion仅返回数据、该版本最新已确认快照及读取成本，不认证未展开历史。旧v3物理fault测试显式生成旧格式夹具，断言原样保留；v4另有基线/尾部/old-head/跨recording/取消/head发布失败/禁止可写重开/非法首帧等公开回归。原5参constructor ABI保留，新options overload独立。
- 2026-10-25 REVIEW→IMPLEMENT→REVIEW：修复full replay漏掉原Initialize Sources双空guard；Leader复核增加最新结果与head hash/lastOffset/length绑定，拒绝重复末帧伪latest；格式探测对非法JSON shape回到原Inspect unreadable语义。三者均有真实RED→GREEN。初评对CreateNew与全历史外源重hash的泛化要求经完整原源复判撤回/收窄，保留原报告和applicability，不改旧评审。
- 2026-10-25 VERIFY：冻结candidate-v4的15文件，以前两片纯源合成1015输入/48程序集；Host181/181（含资源相关恢复与前片Host消费者）、DSH50/50，额外原非终态/待确认真实Kill2/2并保留所有原文件SHA与snapshot目录。v4两轴无剩余finding，首二片封存不变。成本probe同输入SHA/同logical latest，16/64/256三档空间与latest扫描减少；256档40.3MB→3.6MB、40.3MB→0.76MB，writer分配/耗时增加，snapshot额外写flush另列。源prefix hashing未测、无全局速度/峰值/断电结论。

当前下一步：核对其他适用核心可变记录族的latest/版本读取成本与已有原生变化（不可变事实不强制逆向），再处理World辅助基线/增量及保留维护。资源首家族完成不能关闭父A1–A10。默认仍不自动删log/旧baseline、不追加恢复、不重跑业务。未提交/推送，未调用设备或产品模型；共享tools README并发登记保留并单列parallel patch。

- 2026-10-25 HANDOFF（用户明确）：剩余产品存储与测试资产治理交由“存储验收后的测试资产治理”接续，同Leader协调、分Owner/Change验收；本任务停止并行实施。配置与父根/分类相对路径、旧布局引用、消费者与清理验收约定见next-handoff.md；不冒认父范围完成，不因联合推进跳过来源保护。

- 2026-10-25 SPLIT/ALLOCATION（用户进一步要求）：原聊天保留产品RUNTIME-STORAGE-002；目标聊天负责新TEST-ASSET-LAYOUT-001与TEST-ASSET-AUTOMATION-001。前条“本任务停止产品实施/全部交接”由本分配替代；最新Owner/文件责任与依赖见work-allocation.md，代码和既有证据全部保留。

- 2026-10-25 IMPLEMENT（第四片）：用户“那你干吧”后复核同branch/base及第三片封存，复用剩余核心盘点；Run原生转换事实不强制差分，不为无buyer的查询形状另造接口。先按writer-retention-contract.md消除Control/RuntimePlan/Run文件writer重复序列化缓存；入口两生产文件已冻结，Luna仅做公开测试，Leader负责实现与验收。父范围仍open。

## 第四片进度与当前恢复入口
- 2026-10-25 IMPLEMENT→REVIEW→VERIFY：FileCommittedHistory两buyer及FileRunStateJournal仅保留writer确认端点；显式历史读取从原文件校验同RecordingId/序号/hash，原readonly缓存/格式/批量/异步屏障保持。Control公开单例retained-memory 54.8MB RED→4.3MB GREEN；Run同7例批次50.7MB RED→0.27MB GREEN。不是峰值/吞吐承诺。
- 2026-10-25 REVIEW→IMPLEMENT→REVIEW：定向回归188/190暴露资源前片遗漏：旧夹具补v4初始Snapshot；有效夹具仍证明payload超限没有锁死writer。将freeze/限额guard纳入原fault catch，取消与序号冲突原语义保持，不重写slice3封存。Spec/Standards原候选和扩展最终均0。
- 2026-10-25 VERIFY（第四片完成）：当前共享候选定向209/209、零跳过，含原异步/批量/失败屏障、严格恢复/格式兼容及进程Kill。源码/输入在各次执行期间均未变化，结果与程序集绑定见evidence/slice4/regression-final.json；未声称全量/纯隔离/设备模型验收。通过新的本地资产配置落独立Run和分类路径，每轮已完成回执，未造完整源码树副本，无多余临时树可删。详见evidence/slice4/README.md。

当前剩余：辅助输出中仍有全量诊断镜像，需核对实际报告/诊断消费者后改为有界增量；World现有生产消费者均需完整前缀，现有增量可读，尚无证据支持新增latest/周期快照读取面（evidence/world-buyer-inventory.md），不以盘点代替父A5/A10成本验收。父change继续open，总体保护/维护与A1–A10汇总尚未完成。未提交/推送。

- 2026-10-25 PLAN→IMPLEMENT（第五片）：用户要求下一步；复核同branch/base及第四片实际209结果。定位普通Host每次咨询全量覆写且ConcurrentQueue从不释放，真实buyer为gen-run-report及必交付输入固定。按consultation-log-contract.md推进有界待写+逐行增量+一次兼容数组，先报告CLI行为RED；共用原额度/Driver诊断消费者，不另造worker/Owner。资产聊天继续自己的诊断/重放接线，本片只改产品报告专属工具与测试。

## 第五片进度与当前恢复入口
- 2026-10-25 IMPLEMENT→REVIEW→VERIFY：HostRunner改为ConsultationArtifactLog有界待写/原Driver单消费者Drain，AuxiliaryArtifacts共额度追加，关闭后一次受限兼容数组。gen-run-report新增量优先/旧JSON兼容，损坏/序号及计数缺口/全部丢弃无文件均partial；catalog声明新增量，使原报告固定来源覆盖该文件。
- 2026-10-25 VERIFY（第五片完成）：四个真实CLI行为RED→GREEN；最终Host32/32、DSH报告24/24、Python9/9及原PNL006字节golden均通过，含原交付Kill回归及Prepare后删除原输入仍生成原咨询内容的新公开用例。两轴独立评审0。32条同逻辑合成数据，旧累计覆写28,852→新增量+一次镜像5,415 bytes，仅是写入payload成本，不是吞吐/峰值/最终占用结论。完整证据及首RED并行漂移披露见evidence/slice5/README.md；最终各轮源码/输入稳定。
- 2026-10-25 资产收尾：每轮外置独立Run、分类路径与固定配置绑定/摘要/必要日志均由现有adapter登记，实际回执完成；测试自有临时目录在finally清理，本片未造整树副本或删历史。父change仍open，不提交/推送、不调用设备模型，其他聊天诊断/资产接线保留。

当前下一步：先对父A1–A10逐项汇总已证与剩余缺口，复核其他核心族latest/历史适用性、World成本及产品来源保护/默认不删除；不要再凭接口形状另加存储机制。首五片的局部通过不等于父范围已经全部验收。

- 2026-10-25 VERIFY/AUDIT（父范围）：用户要求下一步，复验同branch/base及slice4/5所属源摘要一致；启动A1现状与A3/A4/A5/A10独立适用性核对。旧inventory中“尚无资源版本化/尚无可靠报告”等过时陈述已按当前源码更正。World完整恢复reader仍需要全前缀，不能把没有新API当作缺陷或完成；先给现有128观测成本用例补实际完整读取观测，不改产品格式/Owner、不弱化既有断言。父change保持open。

- 2026-10-25 AUDIT→IMPLEMENT→REVIEW→VERIFY：World成本补测定位完整row解码后Serialize、ReadRecords再Deserialize及整文件字符串复制。按world-reader-contract.md先128MiB预算RED约159MB，再缓存不可变记录/独立返回数组，中间约137MB仍未通过；补逐行读取及读前/后原EOF换行检查，最终130,457,392 bytes、World53/53，Kernel原观察/提交屏障52/52。两轴原范围及增量均0，原格式/全部逐条JSON与严格恢复断言保持；未把单次耗时当稳定速度。
- 2026-10-25 父总验收结论：**不关闭**。acceptance-matrix.md逐项填写A1–A10，核心A4资格/变化读取、World基线/链长策略及Coverage独立交付仍是剩余项；default不删除分支与已完成切片分别声明，不擅自降低条件。证据及执行期其他Owner的单个Python测试漂移说明见evidence/acceptance/README.md，未重跑无关全量、未撤销并行源，必要成本/失败/最终资产已登记收尾。

当前下一动作以acceptance-matrix.md剩余顺序为准：先测Control.State/Plan.Versions实际增长并确定核心最新/变化资格，再推进World基线与Coverage交付/附件保护。不把“没有现成reader”作为用户已确认核心要求的豁免。未提交/推送、未调用设备模型。

- 2026-10-25 PLAN/CONTROL-PLAN：用户要求下一步；同branch/base，沿原Control/Plan生产journal与实际Driver只读遍历链建立增长观测：单plan多候选、多个planId采纳、同planId版本递增三种场景。先区分State/Versions累计重复与Graph原采纳事实，记录保存/完整读取成本；Program入口由Leader拥有，独立probe由Luna编写，不以伪造journal行替代真实Owner。入口4文件已保存到evidence/control-plan/entry。

- 2026-10-25 CONTROL-PLAN/IMPLEMENT→REVIEW→VERIFY：三模式16/64/128完成实际原提交/完整只读恢复；发现先前盘点遗漏Plan接替的latest热查询。Begin/Commit原全量RuntimePlanHistory仅用于两个序号，现成功Append后更新私有序号，失败/丢ack/pending保持原阻断。真实128不同ID采纳256MiB预算RED464.6MB→GREEN，最终独立样本40.2MB（初始化＋Drive窗口，不是纯方法分配）。Host28/28、Kernel167/167，两轴最终0；Standards指标命名意见已修，保留原报告。所有文件格式/原始事实/恢复与物理字节未改变。
- 2026-10-25 CONTROL-PLAN范围结论：128候选Control391行10,392,380 bytes，累计State逻辑2,762,840/末状态11,178；不同Plan ID累计Versions359,323/末表2,837，同ID升版仅7,181/29。下一持久化片优先Control累计State连同原输入/结果来源关系，Plan不同ID的Versions按实测接入；Graph原采纳事实保持。详见control-plan-contract.md及evidence/control-plan/README.md。未冒称A4冷启动版本/最新读取完成，父change不关闭。

当前下一动作：实施上述Control持久化版本增量/周期基线并独立验证净成本、双向还原及原提交/恢复；随后处理适用的Plan Versions，World基线及Coverage交付/附件保护仍按总验收表保留。未提交/推送、未调用设备模型，运行资产均按独立Run登记收尾。

- 2026-10-25 CORE-VERSIONS/PLAN→IMPLEMENT：用户要求继续并将后续两项交另一会话串行承接。本聊天实施Control/Plan可逆增量与周期快照，精确入口在evidence/core-versions/entry.json，契约见core-versions-contract.md。World与Coverage按第一项验收后交接，不提前解除依赖；父范围仍open。

- 2026-10-25 CORE-VERSIONS/IMPLEMENT→REVIEW→VERIFY（第一项完成）：Control/Plan新写v2可逆State/Versions增量与周期独立基线，原事实/旧v1/严格完整恢复保持；新增数据latest及双向版本查询，不授继续执行。合法2行batch仍一次原提交；writer读遵守gate/fault/确认末位。原9MiB预算RED10,392,573 bytes→最终7,919,706（含全部基线/head）；同一原记录不同Plan ID824,511→370,407，同ID312,354→363,188，额外成本明确。最终相关Host159/159，两独立评审各0；封存资源v4的513记录摘要与首/中/末双向版本保持，65项保留证据核对通过，详见evidence/core-versions/README.md。第一轮中间43失败根因/并行资产源漂移单列，最终输入稳定；未改写旧验收。
- 2026-10-25 CORE-VERSIONS交接：用户已授权第一项完成后向已有“存储验收后的测试资产治理”传递另两项。具体指令/顺序/文件责任见continuation-handoff.md；World验收后由目标会话按同授权接续Coverage，不重复等待人类确认。父RUNTIME-STORAGE-002保持open，总体关闭仍需两后续片及A1–A10汇总；不提交推送/不运行设备模型/不启用历史删除。

- 2026-10-25 World接续准入：原Leader正式交接已收到，资产治理当前片已完成（65项及双轴v3、旧100保留）。按WI-STORAGE002-WORLD-CONTINUATION独立推进World；保留Control/Plan/资源封存与共享dirty，先通过既有FileWorldJournal/WorldHistoryRead公开缝验证基线消费，再核对有界版本查询与成本。Coverage保持pending，父Change不关闭。

- 2026-10-26 HOST-IDENTITY/UNDERSTAND→RESOLVE：总验收核定发现原TaskInstance/ProductSession Create/Bind先改内存再Append，Dispose后writer=null配合?.静默跳写。原公开Store缝4/4 RED，返回身份/绑定存在内存而新reader不存在；真实观察见evidence/host-identity/red-observations.json。诊断引用diagnosis: {path: changes/RUNTIME-STORAGE-002/evidence/host-identity/diagnosis.packet.json, sha256: 91fe8bdf34f99db9243718bc8f930058d4ecccd59222264674b13f0c1c4299f0}。这属于父A1/A3/身份幂等原范围，本聊天负责，World/Coverage继续由既有会话接续；不回改RUNTIME-IDENTITY-001封存结论。

- 2026-10-26 HOST-IDENTITY/IMPLEMENT→REVIEW→VERIFY：两原Store先校验可写、成功Append后发布双索引；Dispose与写入共原_sync，编码/写异常后锁死。四公开用例RED→GREEN，原8例正常幂等/1:1/绑定/重开保持，最终12/12、两轴各0、22项保留证据核对通过；入口/候选/诊断与源码绑定见evidence/host-identity/README.md。首次RED无逐例stdout，补观察后仍同4例RED，两个记录均保留。旧JSON行格式/API/原Load语义保持，不把本片升级为跨进程/OS故障或投影历史保证。
- 2026-10-26 当前分工与剩余：接续会话已开始World基线公开RED/实现，其验收后继续Coverage；本聊天保留Host RuntimeRunStore混合身份/生命周期/展示字段资格核定及最终A1–A10集成验收。acceptance-matrix.md主表已同步Control/Plan已完成和Host尚待核定状态，避免旧表与追加进展冲突。必要运行资产已登记收尾，原8例遗留临时目录模式仅改为本轮独占清理，未清理历史他人目录。父change不关闭。

- 2026-10-26 World接续IMPLEMENT→REVIEW→VERIFY完成：FileWorldJournal新v3周期独立基线同次提交，真实完整reader与新增数据version reader（固定打开head/全物理校验/有界重建/不授执行许可）已验；154项相关公开用例、真实旧v1/v2原消费者和六源SHA保持、同ID成本与双轴0通过。证据evidence/world-baselines/README.md/verification.json，原Core/身份/封存未改；新独占工具cache精确收尾，全部源/Run/失败保留。用户明确补充一行异步测试格式期望授权。World WorkItem done，Coverage按原顺序in_progress；父Change不关闭，最终整体提交/推送由原Leader统一集成，本聊天不自行暂存提交。

## 2026-10-26联合发布准备（当前）
Host完整处置完成：RuntimeRunStore的Host权威字段逐项核定并实现确认最新摘要、不可变原event＋可逆投影revision、显式旧源保留起点和只读正反查询；坏核心snapshot不再伪装成不存在并重铸幂等Run。Activation及Task/Session统一先确认追加再发布内存索引，关闭和未知写入拒绝继续变更。公开入口真实5例RED，Host最终组件32/32、Task/Session12/12，两轴最终均0；见host-projection-contract.md与evidence/host-projection/、evidence/host-identity/。

World已完成v3周期基线及真实latest/区间reader154/154、两轴0、真实旧v1/v2来源SHA保持；数据reader不取得执行许可，也不冒称完整历史认证。默认32修订及8/64成本取舍见evidence/world-baselines/README.md。

用户本轮明确授权：监听接续会话，Coverage完成后一并验收、提交、推送并保持仓库健康。历史“当时不提交/推送”记录不改写；最终发布由Leader单独执行。共享工作树保留，真实index仍未暂存；独立index从原HEAD建立精确候选并构建。必要前置按closed Owner闭合，native Overlay三provider与首片封存完全一致，后续Act/authoredExpected业务hunk仍隔离。预合并视图Host360/360、Kernel166/166、DSH39/39、Python67/67；它们是Coverage最终合入前的组件/集成证据，不替代最终候选复验。原始大件与历史失败保留在本地受保护位置，不以整目录暂存混入并行源码。

父Change仍待Coverage最终交接、精确提交树复验和资产收尾，不在此提前关闭。

## Coverage接续局部验收（2026-10-26）
SettingsCoverageRunner执行前接受要求，原Ledger报告与steps冻结为独立source/附件ZIP并复用RequiredReportDelivery六态；失败仅落报告健康，不改Run判定。固定私有输入支持跨重启仅报告重试，Host/Dsh/HTTP共用路径与摘要保护。最终公开Host233/233、Dsh19/19、旧报告Python9/9；四真实Kill窗口、必要PNG/XML、原输入丢失/损坏、RunId与大小写歧义均覆盖；初审3类缺口已修复，双轴v2各0。四元组/精确22文件与必要test partial counterpart、来源用途、所有中间Run及实际资产收尾见coverage-delivery-contract.md、evidence/coverage-delivery/README.md、verification.json、finalization.json、integration-manifest.json。WI-STORAGE002-COVERAGE-CONTINUATION done；父仍implemented/open待原Leader总验收与统一精确提交。本聊天无暂存/提交/推送、无历史GC或live设备模型。

## 2026-10-26 VERIFY→CLOSED（最终）
Host身份/启动/完整投影、World基线与Coverage两项接续全部完成。最终私有提交源Host411/411、Dsh42/42、Python67/67；Kernel166例的已验源与最终源一致，两个必要工具编译通过，源与结果核对811项通过，独立双轴最终无剩余finding。实际删除本任务独占的源码/index/编译副本，共4072文件、逻辑326983905字节；必要日志、源清单、所有独立测试Run与原封存材料保留，标准资产回执completed=true，见evidence/publish/finalization.json。父A1–A10按acceptance-matrix.md的声明范围满足，无设备/模型/自动续跑/历史删除或全产品保证。开发资产已完成四片一并发布，原父任务的级联删除、自动GC和迁移未追加。本轮按用户最新授权由Leader统一提交推送。
