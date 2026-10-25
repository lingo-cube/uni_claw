# RUNTIME-RECOVERY-003 — 原Owner身份归档与跨进程只读核对
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: a05176f3

## Intent / Scope
用户“继续”承接002下一步：生产保存原RunModel已接受身份与原Runtime资源策略依据，在真实非terminal子进程被kill后，从原归档校验身份并只读核对资源账目。恢复的是历史身份及确认资源，不恢复Run State/World/Control/Plan，不重新activation、重建新Run或重发动作。

拥有evidence/ownership.json列出的局部Owner身份投影/读取、Runtime归档协议与driver接入、Host严格文件归档及两Runner装配、公开白名单、公开生产缝测试/受控子进程carrier、相关README及本Change/INDEX/workitems。其余dirty不改。无设备/模型/服务/凭据/提交推送或晋升。

## Decisions / Authority
- 沿用已授权的原RunModel/driver/Host生产缝，测试只经公开接口。原Owner导出actual accepted View和RunId来源；只读校验复用Owner合同合法性及content-derived identity规则，不把资源文件的Identity或新Admit当作原Owner证明。
- 原Runtime同时归档实际策略配置及资源Identity。归档在资源初始化之后、任何外部咨询/Effect之前同步完成，失败阻断。归档为单次原事实，不能从旧资源日志迁移补造。
- 文件adapter只存/读原事实与严格版本/摘要/完整行，不拥有Run当前状态或继续许可。生产composition限定同一run目录的原resource/P25/exec；读回通过原Owner校验及Runtime关联才恢复sealed只读binding。
- 新记录加每次新建源的唯一RecordingId，并纳入资源frame摘要与原归档，防止内容派生RunId相同的不同录制源混用；旧格式仍可审计，不自动认证跨进程来源。
- 当前只支持单accepted View；原RunId可由Runtime提供或content-derived，Task/Session/View/generation/预算/策略均逐项核对。合法多View不在本片。
- 恢复身份不恢复canonical RunState；无权宣告旧Run仍active或已terminal。ResourceRead.ContinuationSupported保持false。只读源校验要求原writer已停止；不提供活跃并发一致性。

## Acceptance
1. 两正式Runner原生产路径在任何咨询/Effect前产生独立身份归档；原Owner身份/策略/RecordingId有生产来源，归档失败无外部调用。
2. 原driver对象销毁后，独立新进程只读恢复同一原身份及确认消耗/授额/资助依据；读不写源、不重复扣/授、不调用Admit/Activate/Drive继续。
3. 真实非terminal进程在安全等待点kill（无Cancel/Stop/Dispose伪造），父进程确认退出；另一个进程读取原事实。Pending/未知Effect、未确认操作或P25/exec后缀仍不完整，无重发。
4. 缺失/损坏/错Run/View/策略/源RecordingId/旧格式归档均拒绝认证，保留历史分项；Owner公开面及相关回归、双轴评审、保护hash、源锁与真实执行证据通过。

## Plan
Owner身份只读投影与实际归档→最小公开读回RED/GREEN→生产装配→独立kill/read carrier→反例与定向回归→独立评审→证据核验。

## Verification
level: SCENARIO
method: public production seams + controlled child-process kill/reopen + scoped regression/two-axis review
expected: A1-A4
actual: A1-A4 passed in bounded original-identity/resource read scope; new18 twice / Host74 / Kernel141 / controlled DSH5, real nonterminal kill+independent read settled/pending. Execution/RunState recovery unproven.
evidence: evidence/acceptance.json · evidence/verification.json · evidence/review-spec.md · evidence/review-standards.md

## Alternatives / Limits
拒绝序列化RunResourceBinding再开放JSON构造、直接信resource.Identity、重新Admit/Activate冒充原Run以及恢复整个driver对象。归档是原Owner历史事实的可信本地存储协议，不是抵抗恶意文件替换的签名认证。OS崩溃/断电、多Host与完整续跑均范围外。

## Status log
- 2026-10-24 UNDERSTAND→RESOLVE→PERSIST→PLAN：复验002最终证据通过，读实际RunModel及现有Task/Session/Activation存储，确认旧loader跳行且不含完整accepted Kernel依据。确定本片原事实独立归档、Owner纯读取校验与非terminal真子进程kill；不扩大到执行恢复。

- 2026-10-24 IMPLEMENT：公开Runner缺独立归档RED1/1→GREEN1/1，原Owner纯身份校验/Runtime政策校验/Host归档及v3 RecordingId接入。公开读回、同合同不同录制错配、缺失/坏源8/8。真实非terminal settled/pending子进程kill+独立reader两例通过；首两次probe编译缺Host namespace，保留日志。扩展18项首轮17绿/1红为fixture重复Drive导致两次咨询，已保留carrier快照并仅更正fixture，非产品回归RED。

- 2026-10-24 REVIEW/VERIFY：新增18/18、原Host合计74/74、Kernel含合同接纳141/141、受控DSH5/5通过。随后仅增强测试carrier：settled真kill从legacy grant换成goal-directed review grant（数额仍2步/2轮），并断言fundedItem/used fact/review历史，最终新18/18；生产源未变，v1原源快照与TRX保留，v2重新锁定并通知两轴复核差异。

- 2026-10-24 REVIEW→VERIFY→CLOSED：双轴含v2差异均无阻断；最终Host74/74、同源新增18/18重复、Kernel141/141、受控DSH5/5。独立verify.py对实际原文件/Nonce/PID/原源前缀/资助依据/TRX/pin核验通过；575个入场C#中仅9个owned变更，566个非owned保护源不变。原RunState/Outcome/World/Control/Plan、完整续跑、live并发/设备/模型/OS故障继续未证明。
