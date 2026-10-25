# RUNTIME-STORAGE-001 — 各类存储的原生异步与有界批量提交
lifecycle_state: implementing · disposition: none · depth: decision-heavy · base: a05176f3

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

## 当前实施片：Evidence观察批量
本次“下一步”落实第一条完整buyer链：IAsyncEvidenceJournal→EvidenceLedger.AdmitBatchAsync→UniKernel.ProcessBatchAsync→Driver/两个正式Host的异步观察等待。同步入口为显式兼容入口，其他领域依然同步，本片通过不等于A1–A4全域完成。测试缝沿已授权的公开journal/Owner/Driver/Host，按TDD与codebase-design执行，不需要再次向用户确认。

片内验收：S1 同批accepted/duplicate/rejected/lineage等价，未ack不发布；S2 文件真实异步批次有数量/字节上限、原格式/head/EOF兼容，实际write/flush少于逐条基线；S3 Driver等待时不咨询/投递/重拉，同步或第二个异步Drive被串行门拒绝，失败锁存/取消不把已写当未写；S4 真实Kill后两个新reader、原SHA及相关回归/独立双轴review/验证通过。单Owner只允许一条在途操作，不预造通用多生产者队列；全域背压/关闭随后续领域迁移完成。

## Alternatives / Risk
拒绝fire-and-forget、原Append入队即成功、复用可丢Trace、Task.Run假异步及把prepare/receipt合批；它们会削弱原确认或不减少实际磁盘操作。原接口均同步，改造涉及Owner与调用链，不能将“接口已有抽象”误报为“已异步化”。批量等待过长会增加单步延迟；真实因果串行处只能获得非阻塞等待，不保证减少该处flush。

## Plan
见`async-batch-storage-plan.md`：先固定异步/批量/故障契约与基线→公开Evidence多admission RED→原生批量adapter和Owner确认→Driver/Host等待链→其余领域逐项迁移→端到端计数/失败/Kill→独立评审与核验。

## Verification
level: DETERMINISTIC + SCENARIO
method: public domain journal/Owner/Driver seams, production file and memory adapters, actual subprocess Kill and I/O counters
expected: A1–A4
actual: Evidence观察首片S1–S4已完成：公开Owner/Kernel/Driver/正式Host及CLI/HTTP异步等待贯通，原v1文件分块批量；64条frame/head写入与flush调用从64次到1次（非物理磁盘syscall）。新增Owner/Driver 9例、文件6例通过；Host288/288、DSH24/24，Kernel1195/1198，同3失败在入口源码复现。提交后ack前真实Kill与两个新reader保持原payload及来源SHA；双轴最终问题均0，verify通过，631非本片构建输入及前片239封存项不变。全域A1–A4仍未完成，其他存储/初始化/队列及completion/attempt reflux/cancel证据仍有同步路径；父change继续implementing。
evidence: evidence/entry.json，evidence/slice1/README.md，evidence/slice1/verification.json，evidence/slice1/review-summary.json，async-batch-storage-plan.md，ADR-0055

## Status log
- 2026-10-25 UNDERSTAND→RESOLVE→PERSIST→PLAN：用户已澄清异步＋批量语义。核实当前8领域同步接口、原归档一次写入、Trace已有异步批量；DriveAsync仍同步调用DriveCore，非存储全链异步。发现ADR-0023明确禁令，按用户新决策记录ADR-0055窄替代，保留发送前真实提交。RECOVERY-016已封存239工件，当前643构建输入固定；本片尚未修改产品源码。
- 2026-10-25 IMPLEMENT（首片）：公开入证接口RED→GREEN，原Owner候选确认与Driver异步等待贯通；文件64条一批真实写入/flush各一次、旧读取格式兼容。初始8个Owner/Driver与6个文件case通过，Host288/DSH24通过；真实非terminal批次提交后ack前Kill/两个新reader/原SHA保持。Kernel全量1194/1197，其3个失败在入口源码载体同资源配置中复现；载体首次漏embedded资源的编译失败保留，已补新/触及Evidence源文件登记，剩余10处旧登记问题不归本片。
- REVIEW→IMPLEMENT（首片v2）：Spec M1为等待上限未明确；当前无收集/排队，改为收集窗口0、写门非阻塞Wait(0)，非法重入在接受前拒绝，已接受I/O仍须等待真实确认。全域共享队列背压保留未来。Standards M1为每批复制全部历史；改为本批overlay＋已确认原表只读查询，并补公开跨批parent验证。Kernel最终1195/1198，仍仅原3失败；22候选v1/失败评审保留，v2复核与最终验证进行中。
- 2026-10-25 REVIEW→VERIFY（首片完成）：Spec v2为0；Standards v2指出中间TRX与最终计数选择混淆，补明确最终kernel-final/host-final/dsh-final与22候选/程序集绑定后，Standards v3为0。verify.py最终exit0；首片S1–S4证据独立封存，不关闭父change或宣称全部存储已异步化。下一工程动作：沿World外层操作delta→World journal→Kernel/Driver等待链迁移原生异步，保持原确认后发布及旧读取兼容，再依次迁移剩余领域。
