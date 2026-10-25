# RUNTIME-RECOVERY-005 — 原Run状态与资源账目的共同只读核对
lifecycle_state: closed · disposition: none · depth: standard · base: a05176f3

## Intent / Scope
用户“进入下一步”承接004：为原Runtime资源记录关联原Run Owner历史版本，读回时共同核对最后提交，不从两个独立读取成功推断一致。仅单View、可信本地停止writer后的只读查询；不恢复World/Control/Plan/当前Evidence，不开放执行续跑或Outcome重发。
拥有evidence/ownership.json内局部生产缝、公开进程carrier/测试/README及本Change/INDEX/workitems；其余dirty与旧证据保留。无设备、模型、服务、凭据、提交推送或晋升。

## Decisions / Authority
- RunModel只投影自身已提交历史的RecordingId/Sequence；Runtime各资源Initialize/Begin/Confirm记录当时版本。版本缺失的旧源可分别读历史，但不认证共同恢复点。不比较Progress.Acts与资源消费口径。
- Driver正常返回前，若Owner历史较最后资源提交更新，且没有未决操作，则以非canonical资源checkpoint记录关联及同一账目；原terminal仍由Assurance→RunModel产生。checkpoint不能增加消耗/授额或修改复核依据。
- 新资源文件增加独立提交head，帧Flush→head Flush/原子替换→ack；共同读取必须核对head与整个EOF，完整帧/操作对丢失也不能回退旧账。原resource-only入口保留历史兼容；共同入口不fallback认证。
- Host只严格存/读原事实及source字节依据；共同语义由Runtime检查：原只读Owner、resource已完整、两个原source版本相符、资源末位已认证。所有结果ContinuationSupported=false；失败仍保留能够独立认证的原状态/分项，不补记、不重发。
- 测试沿此前已授权的原Host archive/正式Runner、RunModel/Kernel/driver公开缝和子进程carrier；不加private/反射缝，不另问例行工程决策。

## Acceptance
1. 正式Runner/driver实际资源记录关联原Owner版本；非terminal/真实terminal共同读取返回相符的精确原版本、原账目，重复读取不改七个源文件，不激活执行。
2. 原Owner已提交而资源未确认、P25/exec未决/后缀、旧无关联、错Run/RecordingId/版本或非法checkpoint，均不报告共同一致，不回退旧Owner/账目。资源提交异常按实际落盘区分：提交前不完整；完整提交后ack失败可只读认证原共同历史点，但原driver保持失败锁存且不重试。所有结果均不可续跑。
3. 新资源head缺失/不符/整帧或整操作尾丢失/未提交后缀拒绝共同认证；非terminal、terminal及缺口实际子进程Kill→独立读取有原PID/源/计数证据。
4. 新增测试RED→GREEN及适当原回归、独立双轴review、源锁/非owned保护、独立证据核验和四元组；未证明范围显式保留。

## Plan
共同读取行为RED→Owner版本/资源关联与只读结果→提交末位与terminal checkpoint→真实中断及反例→双轴评审/回归/证据。

## Verification
level: SCENARIO
method: public production composition/Owner seams and actual controlled Kill/read
expected: A1-A4
actual: A1-A4 passed; final Host122/new24 twice/Kernel159/controlled DSH5. Actual receipt plus settled/terminal-linked/gap/pending process Kill/read; common read only, no execution resume.
evidence: evidence/acceptance.json · evidence/verification.json · evidence/review-spec.md · evidence/review-standards.md

## Status log
- 2026-10-24 UNDERSTAND→RESOLVE→PERSIST→PLAN：004独立核验通过。读实际顺序：consult/dispatch/verify先Begin；Kernel内提交cycle/action；driver再Confirm；terminal可在最后资源Confirm后提交。现无原版本关联，资源帧也无独立末位。确定Runtime只读一致性检查及两journal原head，不恢复执行。

- 2026-10-24 IMPLEMENT：共同入口不存在为公开surface RED0/1→GREEN1/1；随后用正式Runner/driver行为替代surface测试。首轮19/20的missing-link为fixture保留了新checkpoint却删关联，调整为合法旧流（无checkpoint）后20/20，不计产品RED。新增真实反例consistency-red0/2复现初始link缺失及资源Terminal与原Owner冲突仍误报一致，补Runtime两条拒绝规则；未提交后缀改用有效新链且不更新head。

- 2026-10-24 REVIEW/VERIFY：v1新23/23重复、Host121/121、Kernel159/159、受控DSH5/5；四种真实Kill/read共同点/缺口均符预期，独立源/末位/PID/保护核验通过。补一个正式Runner实际dispatch1/verified1后的共同读取买方（receipt1/1），仅测试文件变化，生产源不变；锁v2并重录Host与新增用例，v1证据不覆盖。

- 2026-10-24 RESOLVE澄清A2措辞：原“资源写入前后失败均不一致”过宽，与原Decisions按实际提交事实认证及已通过afterCommit测试不符。明确完整提交后ack失败可共同只读、原driver仍锁存不重试；未提交/未决仍不完整。未改变生产或测试实现，交Spec补核措辞。

- 2026-10-24 REVIEW→VERIFY→CLOSED：Spec v1 P2实际Receipt买方缺口由v2单例补齐，含A2提交/ack边界措辞复核后无未决finding；Standards v1/v2均0硬违例0smell。最终Host122/122、新24/24重复、Kernel159/159、受控DSH5/5；独立原frame/head/位置/Receipt/PID/TRX/pin核验通过。586入场C#中仅9个owned变化，577非owned不变；14源锁匹配，仅测试一处异于v1。共同点仅Run/Runtime历史；World/Control/Plan/当前Evidence及续跑未恢复，下一片先核对EvidenceLedger原事实与血缘持久来源。
