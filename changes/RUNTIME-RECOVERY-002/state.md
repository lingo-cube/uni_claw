# RUNTIME-RECOVERY-002 — 生产资源账目持久记录与只读重开
lifecycle_state: closed · disposition: none · depth: standard · base: a05176f3

## Intent / Scope
用户承接RUNTIME-RECOVERY-001要求下一步，实施已接受ADR-0054的首个生产切片。保留原driver计费/授额语义，在原P25/dispatch前可靠登记操作，安全边界确认资源快照及原事实关联；Host提供可重开文件源。读回只返回确认账目和未决，不恢复执行现场。

拥有Kernel Runtime资源记录协议/只读解释Module、driver局部接入与RunDriverInputs、两正式Host composition/file adapter、公开面白名单、本片测试/文档/证据/WorkItems/INDEX。其余共享dirty源和旧case不改。无Capability Registry新增、设备/模型/服务、提交推送或晋升。

## Decisions
- 验收缝沿已交接的生产Interface：公开KernelRunDriver/RunDriverInputs与Host文件adapter读回；测试不使用反射/private setter，不需对已授权工程细节再次提问。
- 当前仅支持新建、单accepted View的连续生产记录；新driver遇已有账目明确read-only，不据余额继续。合法多View恢复尚不支持，保持原记录，不误判错Run或重置消耗。完整revision/World/Control/Plan恢复及TRAJECTORY A6均范围外。
- 操作Begin在外部咨询/实际ground+dispatch之前写成；Confirm在原响应/授额及计费处理后的安全边界写成。私有内存可暂存本轮结果，但下一次外部咨询/Effect前必须确认成功；异常中断保留Begin未决，不在finally伪造Confirm。
- 文件源同步Flush、严格版本/连续帧/摘要链、排他写入；不静默跳过损坏，不把旧无初始化文件当新零账。确认账目与继续许可分开；已有账目/未决/源失败一律禁止新driver继续。
- 确认快照只包含资源所需事实：原Run/View/策略、消费/授额、receipt/P25关联、授拒与复核/已资助依据。不是整个driver checkpoint、RunState或当前World权威。每个记录源由同一运行的单写者声明覆盖范围。

## Acceptance
1. 新公开Runner任务实际产出持久资源账目；非null Receipt步耗、Completed/Rejected咨询、Runtime已接受grant均保留原口径；原提案不能自动成为grant。
2. 真实文件重开得原确认计数/余额与授拒及资助依据，重复读不增扣；已有记录不开放新driver继续；读不写原文件。
3. Begin/Confirm故障、资源提交失败、Pending/未知Effect、错身份/不完整旧源、损坏/截断、双writer有明确反例；原Receipt不因资源失败被吞掉，后续Effect/咨询为零。
4. 公开surface与适当buyer回归、独立双轴review、保护hash、源/测试快照与真实TRX留证。未执行的设计矩阵项保持未证明。

## Verification
level: SCENARIO
method: TDD public driver/file seams, controlled failures, public Runner composition, scoped regression and two-axis review
expected: A1-A4
actual: A1-A4 passed within original-binding lifetime; Host56 / resource29 twice / Kernel137 / controlled DSH5;561 protected unowned sources unchanged. Cross-process binding/Run resume not implemented or proven.
evidence: evidence/acceptance.json · evidence/verification.json · evidence/review-spec-v3.md · evidence/review-standards-v3.md

## Status log
- 2026-10-24 UNDERSTAND→RESOLVE→PERSIST→PLAN：复验设计v2、实际driver消费/授额/装配缝，确定先生产记录+只读重开与阻断，不恢复执行。精确before与入场源hash已保存。实现按一条公开行为RED→GREEN递进。

- 2026-10-24 PLAN→IMPLEMENT：公开Runner缺资源文件RED1/1→接入生产源后GREEN1/1。新文件/Runtime缝与Begin/Confirm确认已落地。首轮receipt测试因源不断重复Act和取消早于身份补证而失败，均保留为fixture尝试；合法fixture的receipt-red3为9绿/1红（post-action已Verified但账目仍旧pending），补verify关联记录后10/10。FileShare在本平台未阻断第二writer，改原子CreateNew、已有源只读；相应反例已绿。
- 2026-10-24 IMPLEMENT：补原授拒/goal已资助依据/P25旧源/资源失败等17项；P25 settlement中断两例先RED2/2，driver保留原异常并锁存未完成操作后GREEN17/17，禁止后续Drive补确认/再咨询。原Runtime定向137/137已过（最后锁存修改前），最终同源回归随后重录。新artifact导出只读复制代表文件；评审输入v1/entry-relative diff已锁，进入双轴review。

- 2026-10-24 REVIEW→IMPLEMENT：双轴v1发现等待verify提前Confirm与原Owner来源未核对。新增waiting-red1/1→green1/1、source-alone-red1/1→green1/1。verify等待保持同一Begin直到原Verify调用结算；文件格式升v2，把每帧原P25/exec完整前缀length+SHA256纳入摘要链，初始化要求两源为空。生产HostRunner统一file P25，两Host都显式绑定实际原source路径。读回须提供原Run完整identity和两源，缺失/变化/旧资源前缀/错身份一律不完整；不经旧源Load静默跳坏行。28项资源测试已过，进入v2复审。
- 证据沿革：receipt-red3原carrier与green2/green4前版本仍存；v2因读回现在需独立原identity和source资格，公开Runner carrier增加原上下文身份核对及qualified read，最终文件SHA不再声称与red3相同。旧failed/green/TRX/源pin均保留。

- 2026-10-24 REVIEW(v2)→IMPLEMENT：Spec轴通过；Standards指出expectedIdentity可从待读文件自取再传回。将qualified输入改成原driver签发的sealed RunResourceBinding（非公开构造、无JSON恢复），仅在真实Owner/View的初始化记录成功后签发；已有源新driver不能签发。两Runner结果提供JsonIgnore的原binding对象。原binding的跨进程持久恢复仍未实现，不能据文件Identity自证。新增原Owner/新reader绑定用例先编译RED（公开缝缺失，非运行失败TRX），实现后资源29/29；最终新增JSON不可重构断言后重录。v2原源/TRX保留，v3重新锁定及复审。

- 2026-10-24 VERIFY：v3 Host56/56、资源重复29/29；Kernel136/137，唯一失败为新增Binding白名单位置未按Ordinal排序（不是缺类型/产品行为失败）。保留失败TRX和原名单快照，只调整Binding/Identity顺序，v3-1仅此测试声明差异；重录Kernel并通知双轴复核。

- 2026-10-24 REVIEW→VERIFY→CLOSED：v3及仅测试排序差异v3-1双轴均无阻断；最终同源Host56/56、资源重复29/29、Kernel137/137、受控DSH5/5；独立verify.py核对frame/原源字节/源pin/TRX/保护hash通过，561个受保护非owned C#源不变。四元组及明确未证明项见acceptance.json。原binding仅同进程保留对象可用于认证，跨进程丢失时保持不完整；本片未关闭TRAJECTORY A6，不声称完整恢复或设计矩阵全PASS。
