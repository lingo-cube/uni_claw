# RUNTIME-RECOVERY-015 — 原历史恢复读取的存储抽象
lifecycle_state: closed · disposition: none · depth: standard · base: a05176f3

## Intent / Scope
Owner明确要求继续时做抽象依赖、遵循架构原则。先解除已发现的FileRunRecoveryArchive恢复读取编排与文件的直接耦合，再接014的Runtime Plan记录。真实buyer是相同原Run/资源/Evidence/World/Control历史，可由文件或内存原来源提供给同一只读查询；当前不建设数据库或通用存储平台。

## Decisions / Architecture
- Run/Evidence/World/Control的原历史解释、身份及共同点核验仍归原Kernel模块；文件路径/格式/校验链/head/原文件前缀证明留Host适配器。Runtime只读Reader依赖IRunRecoveryArchive与IRunRecoverySources，不引用Host/File实现，不拥有激活或执行权。
- 新IRunRecoverySources只暴露本Run既有四种Owner历史接口及已按backend原始来源核验的资源读取；不是通用IStorage、Registry capability或另一个Owner。原资源账本解释仍调用RunResourceAccounting，不以可读取替代来源/提交末位核验。
- Reader不写入/初始化/关闭调用者来源；来源生命周期由适配器/装配方持有。文件兼容入口保留原API，按一次查询作用域创建并释放原只读文件来源。缺源按既有规则拒绝对应查询，可读的独立历史保留。
- 公共只读Reader/原存储接口/FileRunRecoveryArchive原入口是本片测试缝；Owner已授权抽象依赖及正常工程验证，不新增setter或越过私有缝。测试内存来源保存真实公开生产路径生成的原记录，移除原目录后验证核心查询不访问文件；不据此宣称数据库耐久性或新故障范围。

## Acceptance
1. 同一核心Reader经内存原来源，在原文件目录不存在时查回原Run、资源、Evidence、World和Control历史及四方共同点，原身份/源序列保持；零来源写入/激活/Effect，ContinuationSupported保持false。
2. 原FileRunRecoveryArchive所有查询通过该Reader，原文件格式/版本/校验/拒绝与部分可读历史行为保持；缺源、错Run、错来源位置、不匹配共同点、资源前缀或提交末位未核验均不能误报一致。
3. Kernel Reader无文件路径/流/Host adapter依赖；文件细节留适配器，来源正确释放；公开面白名单及相关Kernel/Host/DSH回归通过。内存/file两个adapter走同一真实查询，不以interface存在冒充整套可替换。
4. dirty入口锁定，无本轮对非owned输入的写入；并发输入变化单列并验证相关集成，010–014封存保持；双轴review、四元组及可复验记录齐全。无数据库实现、RuntimePlan持久、Task/期限/跨进程续跑、路径优化或提交推送。014保存设计保留，下一片沿新读取缝接入。

## Plan
锁输入→公开查询RED→抽出来源接口/核心只读Reader并保留文件兼容入口→内存完整读取和故障拒绝→既有文件回归/新进程读取→冻结双轴review→独立验证。

## Verification
level: DETERMINISTIC + SCENARIO
method: public Reader + two original-source adapters; original files removed; existing File query/crash-reader regressions
expected: A1–A4
actual: 新增9项公开抽象读取契约通过；当前并发固定版本上Host265/DSH5及未变Kernel472通过。原文件移除后读取、backend不可用保留分项、原关联/末位未核验拒绝均有正负证据。7原文件API委托同Reader；2实际非terminal Kill与4新reader、原源SHA保持。626非owned输入未变、6并发输入单列固定，010–014封存保持。Standards硬规范0/启发式0，Spec初次证据聚合Partial已补齐并独立复核闭合；verify-before-close exit 0。A1–A4完成，仍无数据库或RuntimePlan持久实现。
evidence: evidence/

## Status log
- 2026-10-25 UNDERSTAND→RESOLVE→PERSIST→PLAN→IMPLEMENT：已核当前HEAD/014设计与封存；生产写入经接口，恢复查询仍在FileRunRecoveryArchive直接打开文件。确定本片先抽离原读取编排与介质验证职责，不改旧格式与Owner，不引入无真实buyer的数据库框架；Plan原状态仍按014后续实现。
- 2026-10-25 IMPLEMENT复验：原公开读取契约RED→GREEN、backend不可用行为RED→GREEN及9项契约通过；第一组Host265/Kernel472/DSH5通过。随后源集核验拒绝并发漂移：另一路新增/修改6个文件（World流式文件实现、页面识别及其测试）。原entry/初稿与失败核验保留，6项另存SHA/副本、不归本片或回滚；其余626非owned原输入未变。按并发当前版本重跑Host/DSH相关集成，不重复未变Kernel检查。
- 2026-10-25 IMPLEMENT→REVIEW：9候选v2固定于dirty entry增量；原身份文件读写前缀保持，核心Reader无文件或Host依赖，文件7入口委托同Reader。Host265/Kernel472/DSH5范围通过，新增9契约与原Kill/独立reader通过；并发当前版本Host/DSH复验通过。verify-first exit 0，进入双轴独立review，不归属或覆盖6并发输入。
- 2026-10-25 REVIEW补证→REVIEW复核：Standards硬规范0/启发式0；Spec v1唯一Partial是聚合报告尚未生成。双轴报告齐备后补review-summary.json，Spec v2独立复核闭合、无新增发现；9候选未变，不重复测试。
- 2026-10-25 REVIEW→VERIFY：两轴无剩余项，进入最终当前候选/来源/并发固定输入/原封存与证据核验。该核验不替代评审，不扩大为数据库或RuntimePlan实现。
- 2026-10-25 VERIFY→CLOSED：verify-before-close exit 0，9候选v2及副本、原身份文件读写前缀、7兼容查询、公开契约和Host265/Kernel472/DSH5范围结果、2真实Kill/4新reader与原源SHA均匹配。626非owned原输入保持、6并发输入另列保留并在当前版本复验，010–014封存806/82/125/123/74文件保持。无数据库、Task/期限、自动续跑、路径优化或提交推送。下一工程片按014设计沿存储抽象新增Runtime Plan原操作日志与只读查询，优先同图节点冻结、实际采纳及Control接收确认。
