# ORIGIN-CORRELATION-001 — 原投递与同步采集批次的精确关联
lifecycle_state: closed · disposition: none · depth: standard · base: a05176f3

## Intent / Scope
用户“继续”承接RECOVERY006：完成ADR0048原动作与观察关联的最小同步生产闭环，消除Context标签自动提升Origin。原Effect Boundary实际Receipt→短生命周期不可自行构造link→原采集directive及batch显式echo→Ledger核验，关联原字段随Evidence保存。新入证身份v2含Origin及关联，旧v1历史继续只读、原ID不改。
拥有evidence/ownership.json列出缝、对应公开测试/README和本Change/INDEX/workitems。现有dirty与旧证据不改；无设备/模型/服务/凭据/提交推送或晋升。原同步正式feed与可替换仿真feed为buyer；异步迟到/历史重放不自动补关联，保守External/拒绝错link。不恢复World/Control/Plan/续跑或Evidence失效替代接口。

## Decisions
- ADR0048 §1-3优先于PROTOCOL-FIX001早期R1/R2实现：Context与Origin正交；producer自报只诊断，时间/producer名/lineage字符串不能mint关联。无link为External；错issuer/错kind/失效link拒绝入证，不提升。
- 原Effect Boundary只给自己实际持有的Receipt构造link，带原owner/receipt/intent/binding/可靠AttemptId（可空），不可从JSON或自报字符串构造。Ledger由原Kernel composition绑定Effect issuer；外部不能注册issuer。AttemptReport和Observation link不同kind，回流报告不能冒充效果观察。
- 每次同步NextInput前以原inflight Receipt建立请求link，正式采集完成后显式echo；driver只接受本次请求同一link（或无linkExternal），请求结束失效。不把当前Context或批次时间相邻当作关联，不替无echo来源补造关联。
- 新canonical ID v2以原v1内容摘要、实际Origin和显式关联字段生成，区分不同实际attempt。新admission版本保存check及字段；恢复按1/2版本读原ID/字段，不执行入证/关联策略，不把旧v1升级或重算Origin。
- 使用已授权Ledger/Kernel/driver/正式Runner及Effect Boundary公开缝测试，不用private setter/反射灌状态。纯历史恢复仍只读，来源字段不授予current或Effect authority。

## Acceptance
1. 无关联即使Context=post、producer自报或时间邻近也为External，不能满足MaterialEffect；合法link独立于Context，未知/错owner/kind/过期/link跨请求均拒绝或阻断。
2. 正式同步采集与受控替换源实际dispatch→capture→admission→verify闭环，原receipt/attempt准确沿载；AttemptReport不变为Observation proof，未关联不清偿MaterialEffect；错echo拒绝批次。既有局部状态verification不升级为因果或终局证明。
3. 新v2 ID区分Origin/实际attempt并保存读回关联，原v1 archive仍原ID字段只读；不得自动迁移旧Origin或给历史/迟到结果补当前关联。旧相关回归按新合同更新，不造未发生Receipt作正例。
4. RED/GREEN、相关回归、独立双轴review、源锁/保护、四元组；不外推异步迟到归因、设备/模型或完整恢复/续跑。

## Verification
level: SCENARIO
method: original public seams, controlled actual dispatch/capture and legacy/new history read
expected: A1-A4
actual: A1-A4 passed within synchronous controlled scope; Host167 / Kernel379 / controlled DSH5 on final v2; new11 + Evidence history34 repeated; 1 actual receipt + 16 correlated observations; legacy v1 33 canonical unchanged; two independent reviews have no remaining findings
evidence: evidence/README.md · evidence/verification.json · evidence/review-spec-v2.md · evidence/review-standards-v2.md

## Status log
- 2026-10-24 UNDERSTAND→RESOLVE→PERSIST→PLAN：006复验通过，核对ADR0048与现有产出/driver/历史源。Context自报无具体Receipt，v1Origin/ID策略与目标不符；选择原实际Receipt、同步请求echo、闭合link寿命和版本化新身份，保留旧历史只读。

- 2026-10-24 IMPLEMENT：无关联post自报的RED0/1→GREEN1/1；原Runner实际投递在保守External实现下RED1/2，关联接线后GREEN2/2。扩展issuer/kind/请求echo/异常/寿命/v1原源/v2原字段检查。保留编译失败和旧预期失败日志。
- 2026-10-24 语义修订：原Terminal fixtures用Context假定MaterialEffect；其终局owner测试改用Objective事实，明确负例保留未关联MaterialEffect不成立，实际关联MaterialEffect正例移至受控原driver。原层2/3自述、审批、dispatch anchor不能mint关联，四例改为TerminalNotProven。局部PostActionEffectVerification与run-level MaterialEffect/Outcome authority原本不同，本片只执法后者来源要求，不拓宽局部判定合同。
- 2026-10-24 身份迁移：所有新入证用v2，原v1算法只作历史读取；内部预测函数跟随当前身份。原WMP黄金常量因此5项变化，old/new及各次RED保存在identity-v2-golden-migration.json与before快照；10项独立public state/order/delta/replay oracle继续通过。旧实际006 archive九文件复制为固定v1兼容fixture，原文件不改。
- 2026-10-24 REVIEW：冻结entry→candidate差异，双轴独立评审；完整相关回归与独立源保护待最终验证。

- 2026-10-24 REVIEW→IMPLEMENT→REVIEW：Standards v1无发现；Spec v1 P2发现EvidenceRecord公开注释过期，已修。v2仅XML注释，无可执行代码变化；两个独立评审增量复核，最终同组回归。

- 2026-10-24 REVIEW完成：Spec v1 P2公开合同注释已修并经v2复核（0剩余）；Standards v1/v2均0。v2可执行代码与v1逐行一致（去XML注释），最终同组回归另跑全通过。
- 2026-10-24 VERIFY→CLOSED：最终v2 Host167/Kernel379/受控DSH5；新关联11及历史34在v1完整/重复和v2完整均通过。独立verify.py匹配原prepare/Receipt/Intent/Binding/AttemptId、16条原观察关联、v1实际archive原33 canonical与九原文件、v2 graph/ID及真实nonterminal派生/空历史Kill→不同PID只读。591个entry C#中22个owned改动，569个unowned不变；2新增C#均owned，38项最终SHA锁定。失败保留；未commit/push/设备/模型/live服务/完整恢复或续跑。
- 下一候选工程动作：将Evidence HistoryPosition纳入现有Run/Runtime共同恢复位置，使三份原记录能证明同一可读边界；尚未实现，不以本片Origin关联或只读历史代替三Owner共同点。
