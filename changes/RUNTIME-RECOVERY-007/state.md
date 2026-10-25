# RUNTIME-RECOVERY-007 — 原Run/Runtime/Evidence共同只读位置
lifecycle_state: closed · disposition: none · depth: standard · base: a05176f3

## Intent / Scope
用户“下一步”承接ORIGIN-CORRELATION001：把原Evidence HistoryPosition接入既有Run/Runtime资源提交，停止writer后只读比较三份原历史是否处在同一已提交边界。每个Owner原格式/身份/语义仍归原Owner；不恢复World/Control/Plan/current或执行，不重放Outcome。拥有evidence/ownership.json内局部生产缝/公开测试与子进程carrier/README及本Change/INDEX/workitems，其余dirty与旧证据保留；无设备/模型/服务/凭据/提交推送或晋升。

## Decisions / Authority
- 基线§11/13/15/17、ADR0054、RECOVERY001 recovery-contract§2/§4的原Owner版本与发布顺序为权威。Ledger已同步提交后HistoryPosition可被Runtime资源Initialize/Begin/Confirm引用；seq0为真实空历史，不能据缺源默认为空。
- Driver正常返回且无未决操作时，任一Run/Evidence位置较最后资源提交更新，都以原checkpoint Begin/Confirm关联；checkpoint只更新原Owner位置及既有terminal投影，不改消耗/授额/其他账目。提交未确认仍保留pending，不补记原Evidence或回退旧Owner位置。
- 现有InspectRecovery仍只表示Run/Runtime两方，兼容旧资源无Evidence link；新增InspectEvidenceRecovery返回单独三方结果，保留两方独立结果及可读原Evidence，不把两个/三个分别读取成功当作共同一致。只有两个原readonly Owner、完整资源原head/EOF/P25/exec对账、同原Evidence recording及精确末位相符才发布三方Point；永远ContinuationSupported=false。
- Resource v3添加可空EvidenceHistoryPosition；旧源全无该字段继续原独立/两方读，但不能获三方认证。新流不可中途补初始关联/改源/倒序/失去关联。当前单View、可信本机、停止writer；不宣称活跃并发、掉电、对手同时重写全部source/head覆盖。
- 沿此前持续授权的原Ledger/RunModel/Kernel/driver/Host archive/正式Runner/实际子进程Kill-read缝验证，不用反射/private状态、造Receipt或新增第二流程。TDD先公开现有资源提交的实际缺口，再做最小原位置投影与三方比较。

## Acceptance
1. 正式Runner/driver的资源记录关联原Evidence精确位置；空/非空、实际受控投递核验后、terminal及非terminal安全返回可共同只读，多次读取不改九个原源，不新增入证/Reconcile/执行。
2. Evidence已提交后资源位置未发布、缺源/head/整尾回退/未提交后缀、错Run/原源/位置、旧无关联/部分补关联/倒序均不能发布三方Point；保留可独立认证的Run/资源/历史Evidence。pending及持久失败不补确认、不重试；完整三方提交后ack失败可认证原历史，但原driver继续锁存。
3. Run未变而Evidence仅duplicate/rejected/无关事实增长也更新位置；不从canonical数量或Run.Progress猜admission进度。实际nonterminal子进程Kill→不同PID三方读，含对齐/证据后缀缺口/原空历史，终态不冒充crash可续跑。
4. 行为RED/GREEN、相关回归、双轴独立review、源锁/非owned保护、原文件/末位/PID/TRX独立核验与四元组；明确没有Evidence生命周期完整/三方当前现场/完整覆盖续跑。

## Verification
level: SCENARIO
method: original public Owner/Host/Runner seams and actual controlled Kill/read
expected: A1-A4
actual: A1-A4 passed in original three-history read-only scope; final Host199 / Kernel379 / controlled DSH5, new32 repeated; five modes actual Kill/read with different PIDs; original native legacy retained; no execution continuation
evidence: evidence/README.md · evidence/verification.json · evidence/review-spec-v1.md · evidence/review-standards-v1.md

## Status log
- 2026-10-24 UNDERSTAND→RESOLVE→PERSIST→PLAN：上一片原源/测试/评审独立复验通过。读取005/006与原实现：resource没有Evidence位置，checkpoint只看Run位置；决定用原资源提交作共同历史标记，增量只读比较，不引入新当前状态或执行恢复入口。

- 2026-10-24 IMPLEMENT：正式Runner原resource缺Evidence位置RED0/1→GREEN1/1；随后扩展三方公开读取、完整只读/空/terminal/原实际受控Receipt、Evidence-only accepted/duplicate/rejected后缀及checkpoint、不完整/错源/旧native兼容和提交前后故障。首26项通过；另新增5模式真实Kill/read和旧native九文件复核，32/32通过。没有生产源意外失败或另行缺陷立案。
- 2026-10-24 REVIEW：锁entry→候选diff与SHA，双轴独立评审；最终相关回归、重复及原源独立核对待执行。

- 2026-10-24 REVIEW→VERIFY→CLOSED：Spec无可行动发现；Standards0硬违例0smell。最终源Host199/199、Kernel379/379、受控DSH5/5、新32/32重复通过。独立verify.py核对原三源frame/head/archive/position、Evidence v1/v2原id/血缘、P25/exec字节stamp、actual Kill不同PID与TRX通过；核验脚本两次自身索引/命名问题已修且失败留verification-attempts.json，不涉及生产/测试变化。593个entry C#仅7owned变化、586非owned不变，2新增C#均owned，12源锁定并留final-source。原真实受控Receipt1/verified1、仅Evidence增长的3种checkpoint、原空历史、terminal/pending/legacy/不一致均按预期，九个原源重复读取不变。无设备/模型/live服务/commit/push或续跑许可。
- 下一候选工程动作：检查World原状态与revision/关联/冲突的持久来源，明确历史只读恢复与必须重新观察的当前事实边界；三方历史共同点不替代World/Control/Plan现场恢复或Coverage续跑。
