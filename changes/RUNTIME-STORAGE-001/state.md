# RUNTIME-STORAGE-001 — 各类存储的原生异步与有界批量提交
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: a05176f3

## Intent / Scope
所有者澄清“各种存储要一步批量存储”指“异步＋批量写入，减少阻塞和磁盘次数”，不是跨存储事务。先完成并封存RECOVERY-016，再优先实施本片。覆盖Run、Evidence、World、Control、Runtime Plan、资源、咨询、执行记录和原身份归档；诊断Trace及普通Host产物保持各自可靠性等级。

## Decisions / Architecture
- ADR-0055明确替代ADR-0023第3条禁令；Owner、发送前真实提交、三值失败语义和现有来源/序列/末位校验不变。用户已确定方向，无待决人类许可。
- 核心依赖领域抽象，Host装配文件/其他介质。原生async必须贯穿Owner确认和Driver/Host等待链；不能只把同步写包在Task.Run，也不能让原Append在仅入队时返回成功。
- 同一Owner串行；关键队列有记录数/字节/时间上限，背压不丢记录。批次只合并可同时确认的原记录，不跨Agent调用或Effect投递屏障。Trace可丢弃规则不能用于关键记录。
- 第一条真实批量buyer是一次观察内的多条Evidence admission；World已将一次外层操作的多项delta合为一条原记录。不能为了声称全局batch而假装顺序等待的begin/confirm可以合并。其余领域均迁移原生async，批次收益按实际可合并写入证明。
- API形状、批次阈值和介质布局由首个公开buyer及实测冻结，不先建通用WAL/数据库平台。单条提交也要工作；逐领域切换，旧同步读取和已封存数据保持兼容。

## Acceptance（先于实现）
1. 全部领域有明确异步写入和提交确认契约，生产路径使用真实异步等待；有界队列满时等待，不阻塞调用者线程或丢关键记录。同一Run/Owner不并发修改。
2. 一次观察内多条Evidence原结果（含duplicate/rejected）按序保留，批量确认后才供World消费；文件真实写入/flush计数少于逐条基线。不能只测试List序列化或API存在。
3. 原Run/World/Control/Plan/资源/P25/Effect的确认屏障逐项保持；写前、批中、提交后ack丢失、取消及关闭均有公开契约和实际Kill/新reader证据。异步Execution prepare未确认时零投递。
4. 八领域＋归档生产接线和旧读取兼容通过；性能同时报告场景、记录数、批次数、写/flush次数、等待与峰值积压，不把单线程因果屏障或后台线程切换当作性能收益。相关回归、双轴review及独立四元组通过后才关闭。

## Out of scope
不实施跨Owner事务、数据库、自动续跑或路径优化；RECOVERY-014剩余完整游标/Control接收保持后续独立切片。无真实buyer证据不预造统一存储平台。

## 已完成Slice 1：Evidence观察批量
本次“下一步”落实第一条完整buyer链：IAsyncEvidenceJournal→EvidenceLedger.AdmitBatchAsync→UniKernel.ProcessBatchAsync→Driver/两个正式Host的异步观察等待。同步入口为显式兼容入口，其他领域依然同步，本片通过不等于A1–A4全域完成。测试缝沿已授权的公开journal/Owner/Driver/Host，按TDD与codebase-design执行，不需要再次向用户确认。

片内验收：S1 同批accepted/duplicate/rejected/lineage等价，未ack不发布；S2 文件真实异步批次有数量/字节上限、原格式/head/EOF兼容，实际write/flush少于逐条基线；S3 Driver等待时不咨询/投递/重拉，同步或第二个异步Drive被串行门拒绝，失败锁存/取消不把已写当未写；S4 真实Kill后两个新reader、原SHA及相关回归/独立双轴review/验证通过。单Owner只允许一条在途操作，不预造通用多生产者队列；全域背压/关闭随后续领域迁移完成。

## 已完成Slice 2：World运行期原生异步
详见`world-async-slice.md`（B1–B5）。IAsyncWorldJournal→World八类操作→UniKernel观察/冲突/动作后验证/contract demand→KernelRunDriver→两个正式Host已真实await；FileWorldJournal使用原生async写入/flush/head及关闭，原同步入口共用同一候选确认逻辑。等待不发布、失败锁存、接受后取消先结算、单Owner零等待准入；编码frame默认16MiB可配，不增加跨操作队列。v2增量/v1只读保持，构造初始化及其他领域仍同步。Run后续迁移见Slice 3，不关闭父A1–A4。

## 已完成Slice 3：Run状态与遍历事实原生异步
详见`run-async-slice.md`（C1–C5）。IAsyncRunStateJournal→RunModel admission/cycle/action/terminal及遍历事实→UniKernel→KernelRunDriver→两个正式Host已真实await。首次View/RunId/State同样确认后才发布；单Owner零等待准入、接受后取消结算、失败/ack丢失锁存。FileRunStateJournal原生async frame/head并请求WriteThrough，保留原刷盘请求及v1/v2读取；正常异步关闭等待已接受记录。新增Running等待取消窗口已补Cancelled结算，有公开RED→GREEN；attempt reflux使用已存在的Evidence异步批次。构造、Control/Plan/资源/P25/执行/归档及completion/cancel证据仍有同步路径。下一动作Control遍历日志及Driver原生async，不重做恢复游标或Control/Runtime接收协议；父A1–A4仍未全域完成。

## Alternatives / Risk
拒绝fire-and-forget、原Append入队即成功、复用可丢Trace、Task.Run假异步及把prepare/receipt合批；它们会削弱原确认或不减少实际磁盘操作。原接口均同步，改造涉及Owner与调用链，不能将“接口已有抽象”误报为“已异步化”。批量等待过长会增加单步延迟；真实因果串行处只能获得非阻塞等待，不保证减少该处flush。

## 已完成Group A（Slice 4）：Control＋Runtime Plan
详见`control-plan-async-group.md`。两领域初始化/运行期确认沿原Owner→Kernel/Driver→正式Host贯通await；两个文件adapter共用Host内部FileCommittedHistory，保留各自schema/严格度/身份/head/EOF及刷盘请求。v1评审M1后找到配置规则＋启动的合法双记录buyer，v2保留原两行/顺序，最多2条/整批16MiB、窗口0共同确认后发布；旧不支持批次journal逐条行为保持。实际Driver场景10条记录的frame/head写入及flush各10→9，Plan仍2→2，接替三阶段不合批。主要运行期5/9，下一组B资源＋P25＋执行，之后C归档/初始化/收尾；父A1–A4继续未全域完成。

## 已完成Group B（Slice 5）：资源＋P25＋可靠执行
详见`resource-consult-execution-group.md`。Driver等待资源initialize/begin/confirm及P25 reserve/complete/reject；EffectBoundary→Kernel/Driver等待prepare后才delivery，结果保存失败仍保留真实Receipt并阻断后续发送。三文件adapter原生async写/flush、资源源前缀异步hash、正式Host异步关闭；领域接口/各自格式/三值/旧同步异常语义保持。实际delivery后的submission＋receipt原两帧合批，最多2条/默认16MiB/窗口0；原3帧/1492字节/1delivery保持，write/flush/批次3→2。八运行期领域完成原生异步接线，将身份归档计入总路径为8/9；全域初始化/收尾仍未完成。

## Plan
见`async-batch-storage-plan.md`及`grouped-storage-plan.md`。首三片与A/B/C三组均完成，父A1–A4逐项映射到`evidence/slice6/README.md`。九路径原生异步接线、初始化、关闭、完成/取消/裁决回流及两Host直接产物完成；合法批次/等待/峰值及旧读取/故障证据成立。不引入跨域事务、队列/数据库或跨屏障凑批。本change关闭；执行路径剪枝、恢复游标/自动续跑仍是原已记录的独立后续，不因存储完成而自动实现。

## 已完成Group C（Slice 6）：身份归档与统一收尾
详见`archive-init-close-group.md`及`evidence/slice6/README.md`。原身份经IAsyncRunRecoveryArchive确认后才绑定/外部操作；Evidence/World marker及World Owner原initialize通过异步factory确认。completion/cancel/adjudication回流走原ProcessBatchAsync，裁决共用Drive门；两Host直接产物/诊断观察者等待异步写出与关闭。新增暂停消息循环测试复现同步桥阻塞，Driver/两Host await显式ConfigureAwait(false)后通过；另一Host通过原启动失败路径离线验证输出/关闭，不触达设备/模型。九路径完成，原格式/Owner/故障范围保持，普通产物不升级可靠性等级。

## Verification
level: DETERMINISTIC + SCENARIO
method: public domain journal/Owner/Driver seams, production file and memory adapters, actual subprocess Kill and I/O counters
expected: A1–A4
actual: 全范围完成并关闭。九路径原生async、初始化/关闭与完成/取消/裁决回流确认通过；本片新21例、Kernel1297/1300、Host356/357、DSH24/24，仅原四失败。三合法批次减少真实managed写/flush，九源计数/等待/峰值、旧读取、归档实际Kill及两同步桥成立。23候选/667输入/26程序集、644保护输入/3并行输入单列和1456原封存保持；两轴0、verify exit0。逐项四元组见evidence/slice6/README.md。
actual_slice1: Evidence观察首片S1–S4已完成：公开Owner/Kernel/Driver/正式Host及CLI/HTTP异步等待贯通，原v1文件分块批量；64条frame/head写入与flush调用从64次到1次（非物理磁盘syscall）。新增Owner/Driver 9例、文件6例通过；Host288/288、DSH24/24，Kernel1195/1198，同3失败在入口源码复现。提交后ack前真实Kill与两个新reader保持原payload及来源SHA；双轴最终问题均0，verify通过，631非本片构建输入及前片239封存项不变。全域A1–A4仍未完成，其他存储/初始化/队列及completion/attempt reflux/cancel证据仍有同步路径；父change继续implementing。
evidence: evidence/slice6/README.md，evidence/slice6/verification.json，evidence/slice6/review-summary.json，evidence/slice6/candidate-verification-binding-v3.json，evidence/entry.json，evidence/slice1/README.md，evidence/slice1/verification.json，evidence/slice1/review-summary.json，evidence/slice2/README.md，evidence/slice2/verification.json，evidence/slice3/README.md，evidence/slice3/verification.json，world-async-slice.md，run-async-slice.md，async-batch-storage-plan.md，ADR-0055

actual_slice2: B1–B5已完成。新World Owner/Kernel/Driver10/10、原生文件/实际Kill4/4；最终Kernel1205/1208、Host147/148、DSH24/24，同4失败在649精确入口源码复现。真实非terminal Driver在文件提交后Owner ack前Kill，Owner seq2/源3条/发布revision0；两个新reader原3操作/1修订及SHA不变。16候选固定、640非本片输入及WORLD-HISTORY-002前片382封存不变；Standards/Spec各0、verify通过。父change仍implementing，尚未完成全部领域、初始化和全部合法批次计数。

actual_slice3: C1–C5已完成。新Run Owner/Kernel/Driver18/18、文件/关闭/实际Kill5/5；最终Kernel1223/1226、Host200/201、DSH24/24，同3+1旧失败与入口653同源基线一致。旧README违规据原TRX核对为9→7，本片补齐两处所改Run文件登记（以原TRX为准，不沿用旧摘要的数量）。真实Driver Owner位5/源6条/handler0，两个新reader读回原6条及Running历史，SHA不变；取消后补Cancelled且不调用handler。17候选/26程序集绑定、642非本片输入/前片120封存保持；双轴各0、verify通过。父change仍implementing，无额外Run合批、初始化或其他域异步完成声明。

actual_slice4: Group A D1–D5 v2完成。新26＋10例全通过；最终Kernel1249/1252、Host292/293、DSH24/24，同3+1旧失败。Control原10行/operation1保持，frame/head write/flush各10→9（配置＋启动2→1），Plan仍2→2；两实际Kill/四reader原行/SHA不变。20候选/26程序集/660实际输入固定，642非本片输入不变，1外部DSH未选测试按原/新SHA单列，不覆盖或归为本片；前片122封存保持。Standards M1经合法双记录批次及实际计数关闭，v2两轴0、verify通过；父implementing，B/C未完成。

actual_slice5: Group B E1–E5完成。新35＋13例全通过；最终Kernel1284/1287、Host343/344、DSH24/24，同3+1旧失败。原三执行帧/1492字节/1delivery/1receipt保持，write/flush/批次3→2；四实际Kill/八新reader原数据/SHA不变。Driver准备等待取消先复现delivery1，再贯通token证明0。20候选/663实际输入/26程序集固定，646保护输入/前片342封存保持；两轴0、verify exit0。父implementing，身份归档计入总路径为8/9，下一C组。

actual_final: Group C F1–F5及父A1–A4完成。新13＋8例通过；最终Kernel1297/1300、Host356/357、DSH24/24，同原3+1失败，旧README缺项5→4。v3只补Host独立用例，Kernel/DSH源码及程序集未变复用v2，Host全相关选择重跑。九原生来源23语义记录＋2 marker，frame写/flush各24、head各19；实际1delivery/1receipt。Evidence扣marker64→1、Control10→9、执行3→2，World增量7,951,934→1,107,547字节保持。归档提交后ack前真实Kill/两新reader原身份/历史/SHA不变；初始化/取消/关闭/完成/裁决确认与两个同步桥通过。23候选/667实际输入/26程序集、644未变输入/3并行输入单列，1456相关原封存逐SHA保持；两轴最终0、verify通过。父范围全部完成，状态closed；保留四个既有失败及原故障范围，不声称真机/模型、OS崩溃或掉电验证。

## Status log
- 2026-10-25 UNDERSTAND→RESOLVE→PERSIST→PLAN：用户已澄清异步＋批量语义。核实当前8领域同步接口、原归档一次写入、Trace已有异步批量；DriveAsync仍同步调用DriveCore，非存储全链异步。发现ADR-0023明确禁令，按用户新决策记录ADR-0055窄替代，保留发送前真实提交。RECOVERY-016已封存239工件，当前643构建输入固定；本片尚未修改产品源码。
- 2026-10-25 IMPLEMENT（首片）：公开入证接口RED→GREEN，原Owner候选确认与Driver异步等待贯通；文件64条一批真实写入/flush各一次、旧读取格式兼容。初始8个Owner/Driver与6个文件case通过，Host288/DSH24通过；真实非terminal批次提交后ack前Kill/两个新reader/原SHA保持。Kernel全量1194/1197，其3个失败在入口源码载体同资源配置中复现；载体首次漏embedded资源的编译失败保留，已补新/触及Evidence源文件登记，剩余10处旧登记问题不归本片。
- REVIEW→IMPLEMENT（首片v2）：Spec M1为等待上限未明确；当前无收集/排队，改为收集窗口0、写门非阻塞Wait(0)，非法重入在接受前拒绝，已接受I/O仍须等待真实确认。全域共享队列背压保留未来。Standards M1为每批复制全部历史；改为本批overlay＋已确认原表只读查询，并补公开跨批parent验证。Kernel最终1195/1198，仍仅原3失败；22候选v1/失败评审保留，v2复核与最终验证进行中。
- 2026-10-25 REVIEW→VERIFY（首片完成）：Spec v2为0；Standards v2指出中间TRX与最终计数选择混淆，补明确最终kernel-final/host-final/dsh-final与22候选/程序集绑定后，Standards v3为0。verify.py最终exit0；首片S1–S4证据独立封存，不关闭父change或宣称全部存储已异步化。下一工程动作：沿World外层操作delta→World journal→Kernel/Driver等待链迁移原生异步，保持原确认后发布及旧读取兼容，再依次迁移剩余领域。
- 2026-10-25 所有者追问并授权WorldHistory改增量：先完成独立WORLD-HISTORY-002。澄清此前“外层操作delta”仅指新增修订/日志后缀，修订本身原仍全量；现生产文件v2已改修订增量，typed写入不先生成完整revision JSON，旧v1可读，128次World观测保存量减少86.1%，两轴0且verify通过。此编码改造不改变本父change的异步范围与状态；World异步接口/Owner/Kernel/Driver等待链仍是下一实施动作，其他领域未宣称完成。
- 2026-10-25 IMPLEMENT→REVIEW→VERIFY（Slice 2完成）：World Owner接口缺失RED、Kernel同步桥超时RED、文件缺异步关闭RED→真实await GREEN；八操作共享确认后发布，Kernel观察门与Driver门保持。16候选/最终三组TRX/26程序集固定，两轴0；独立verify通过。新10+4例、Kernel1205/1208、Host147/148、DSH24/24；原4失败在649精确入口源码载体复现并归档，首次Python提取参数不兼容/测试前提错误的尝试保留。真实Driver ack前Kill/两个新reader与640非本片输入、前片382封存通过。父状态保持implementing，下一动作Run原生async；初始化及其他领域不得报完成。
- 2026-10-25 IMPLEMENT→REVIEW→VERIFY（Slice 3完成）：用户批准Run迁移。公开Run接口/Kernel同步桥/文件异步关闭RED→GREEN，贯通核心状态及遍历记录；保留刷盘请求与原读取。Running保存等待取消的新增窗口RED→Cancelled GREEN；首次Kill fixture漏合同版本关联，诊断后仅修fixture，不放宽产品条件。新18+5例、Kernel1223/1226、Host200/201、DSH24/24；同3+1旧失败复用与653入口源码逐项相同的前片封存基线，884文件归档。两轴0，最终verify exit0；两次核验工具旧README数量/消息期待修正日志保留，642非本片输入、前片120封存及23新case保持。父仍implementing，下一动作Control遍历journal/Owner/Driver原生async。
- 2026-10-25 所有者要求相似改造分组：剩余六类按三组实施，A Control＋Plan、B 资源＋P25＋执行、C 归档＋初始化＋收尾；沿原确认边界复用机制与回归，不另造存储平台。A的656入口输入/前片122封存固定，20候选边界。Control/Plan异步接口及Owner/Driver贯通，Host两buyer共用FileCommittedHistory；22个新Kernel行为与9个文件/关闭/真实双Kill用例通过，统一最终回归/双轴review进行中。
- 2026-10-25 REVIEW→IMPLEMENT→REVIEW→VERIFY（Group A v2完成）：Standards v1 M1为缺真实批次收益；补配置＋启动的可选原生双行批次、共同确认与实际计数，不支持时保留旧行为。原10条Control记录和结果保持，write/flush/head各10→9，局部2→1；新26＋10例，Kernel1249/1252、Host292/293、DSH24/24仅原3+1失败。双Kill四reader、旧读取/严格度、642保护输入/1并发测试单列及前片122封存保持；v2两轴0，verify exit0。v1候选/报告/回归及fixture失败、核验工具首次基线文件名错误均保留，不覆盖封存或并发代码。主要运行期5/9，父implementing，下一B组。
- 2026-10-25 IMPLEMENT→REVIEW→VERIFY（Group B完成）：资源/P25/执行沿领域async缝、Driver/EffectBoundary/Kernel与正式Host贯通；原格式/来源/确认语义保持。实际结果两原帧合批，原3帧/1492字节/1delivery不变，write/flush/批次3→2；新35＋13例、Kernel1284/1287、Host343/344、DSH24/24同旧3+1失败。Driver取消token未传至prepare确认处的行为RED为delivery1，修复后0；旧同步P25异常兼容及执行字节限额Failure处理回归已修正。20候选/663输入/26程序集、646保护输入及前片342封存通过，两轴各0。首次核验工具比较SHA大小写错误仅修工具，原日志保留，verify v2 exit0；父implementing，下一C组归档和全域初始化/收尾。
- 2026-10-25 IMPLEMENT→REVIEW→IMPLEMENT→REVIEW→VERIFY→CLOSED：C组原生归档、初始化factory、完成/取消/裁决回流和Host直接产物/关闭完成。首次统一Host回归因同步上下文阻塞被本任务中止，不将26例中止结果冒充完整回归；暂停消息循环RED后补ConfigureAwait(false)，完整v2通过。Spec v2 M1要求另一正式Host桥，v3补原启动失败/产物/关闭离线回归1/1，无产品变更；Host全相关357例重跑，Kernel/DSH同源结果复用，最终13＋8新例通过且同旧3+1失败。23候选、667输入/26程序集、644保护输入/3并行测试输入单列、1456原封存保持；两轴最终0，verify exit0。九路径和父A1–A4四元组全部成立，关闭本change；路径剪枝/续跑仍独立后续。
