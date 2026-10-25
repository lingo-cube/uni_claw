# RUNTIME-RECOVERY-016 — 原Runtime Plan采纳日志与一次冻结图
lifecycle_state: closed · disposition: none · depth: standard · base: a05176f3

## Intent / Scope
接015已落地的存储抽象，按014先实现最小原Plan采纳切片：完整嵌套内容一次冻结，记录V7通过后的原采纳/版本及旧Traversal接替的未闭合边界，确认后发布给原执行器。用户要求继续并核对抽象，故写入经IRuntimePlanJournal，读取经015的IRunRecoverySources/RunRecoveryReader；Host只装配文件adapter。

## Decisions / Architecture
- 本片是014完整游标设计的首个实施切片，不宣称执行中32项游标、13项帧或Control工作接收已持久。图保存八类现有节点的完整值和原顺序、稳定节点/块路径；内存正文只冻结一次，当前Loop/Frame沿同一冻结节点继续，历史读取不重建可执行Driver。
- 原P25响应与V7采纳分开。来源中adopt是原提交点，提交后ack丢失不证明原进程已发布或执行；原Driver只在成功ack后发布新Plan。版本表同次确认，不从旧提议占用版本。
- 替换旧Traversal先记录replacement-begin，再调用原Control弃置，最后确认新采纳。跨Owner不伪装原子回滚；中断保留未闭合边界并阻断，不能盲重发或补造Control完成。
- 文件原来源有独立GUID、原Run身份和原Run录制关联、连续schema/seq/hash/head/EOF；归档升级v5并保留v1–v4旧查询与v4/v5 Control读取。旧无Plan源不可用，合法初始化且未采纳才是已知空历史。文件与测试内存同一公开读写缝；Reader不关闭调用者来源。
- 公共Driver/原journal/原历史读取/File兼容入口为本片测试缝，沿用户已授权的正常工程验证；无setter、反射私有Driver、注册新Capability/Owner、第二执行器、数据库或通用checkpoint平台。

## Acceptance
1. 原采纳图保留八类节点、嵌套正文、规则/handler完整值、原PlanId/Version/工作量及稳定节点/块路径；外部集合后改不改变采纳内容或原执行。嵌套循环出栈继续保持原节点关联，V7/同版本/Unknown等原语义不变。
2. 初始化、接替前边界、实际采纳均保存原身份/原输入/版本；写前/提交后ack失败不发布新Plan、不占用内存新版本、不开始新typed或Effect；后续Drive持续阻断。旧Control已提交事实不回滚，未闭合接替明确可查。
3. 原文件和内存来源经同一只读Reader查回；缺/错源、schema/序列/head/EOF及非法图/版本关系拒绝。新归档和旧v1–v4核心/Control读取兼容；真实nonterminal Kill后两个新reader读回原提交与原源SHA，无激活/续跑。
4. 原相关Kernel/Host/DSH回归、双轴review及独立四元组核验通过；保护dirty输入/旧封存，并发变更另列保留。无完整游标/Control接收共同点、Task/期限、自动续跑、路径优化或提交推送。

## Plan
公开Driver原日志RED→冻结图/确认后采纳→版本和替换边界→文件adapter/归档与抽象读端→失败/图结构/只读拒绝→非terminal Kill/双reader→范围回归→冻结评审→最终核验。

## Verification
level: DETERMINISTIC + SCENARIO
method: public Driver and original journal/history seams; file and memory sources; actual subprocess Kill
expected: A1–A4
actual: Kernel502 / Host282 / DSH5通过，新增30个公开Plan与17个文件case；真实非terminal Plan Kill/2新reader及2个Control Kill/4新reader、原源SHA保持；删文件后同Reader可读原内存来源。22候选、623未归属输入及010–015封存保持；Standards/Spec均0，独立verify exit0。A1–A4完成；无完整游标/Control接收或异步批量实现。
evidence: evidence/

## Status log
- 2026-10-25 UNDERSTAND→RESOLVE→PERSIST→PLAN→IMPLEMENT：复核015读取抽象及010–015封存保持；当前HEAD/dirty输入已锁。本片落实原Plan采纳与完整冻结图，后续仍需014的执行游标/Control交接。核心依赖接口，文件细节留Host，不把本片扩张为自动恢复。
- IMPLEMENT：完整冻结图、IRuntimePlanJournal、采纳/接替边界、文件adapter、v5归档及统一Reader已接两个正式Runner。公开Driver 27个新增case及相关Kernel共499通过；文件17个case首次有1个fixture缺ProjectTraversal，补齐后通过，含实际Kill与双reader/删文件后内存读取。第一次Host回归281/282，旧Control probe的ready文件创建早于JSON写完而读到空内容（既有fixture竞争，见host-regression.trx）；原日志/协议未失败，保留尝试并有界重验，不改旧probe。
- IMPLEMENT→REVIEW：第二次Host 281/282在旧Traversal probe出现相同ready发布竞争。已将两个probe加入明确ownership，仅将ready改为写完后rename发布；保护其原source快照。原读端另补3个规则/handler/return-step漏拒绝RED，修后Kernel502/502；Host282/282通过，真实Plan Kill和两个Control Kill/双reader/SHA保留。候选22文件已冻结。
- 用户澄清后续存储要求为“异步＋批量写入，减少阻塞和磁盘次数”。进入下一独立片，需保持真正持久确认后发布，不能将入队视为成功；本片未实现异步批量。
- REVIEW→VERIFY：Standards硬规范0/启发式0，Spec缺漏0/越界0/错误实现0；两轴各核22/22候选SHA。独立verify-pre-review已通过；不扩大为游标/Control接收或异步批量，进入最终四元组核验。
- VERIFY→CLOSED：最终verify.py exit0；22候选/原测试程序集、623非owned构建输入、010–015封存806/82/125/123/74/136文件及全部四元组匹配。采纳日志与完整图完成，完整运行游标/Control接收仍未实施；用户新要求的异步批量存储优先进入下一片，保留真正提交后ack及Owner边界。未提交或推送。
