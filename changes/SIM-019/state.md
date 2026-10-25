# SIM-019 — 窗口证据驱动弹窗与有界unknown恢复
lifecycle_state: closed · disposition: none · depth: decision-heavy

## Intent / Scope
2026-10-24用户在明确补采预算含义后回复“可以”，采用SIM-018保守行为组合并授权完成实现和验证。Host采集配对窗口/XML，World拥有capture/cycle/session关联与当前popup；缺失/冲突暂停清障与遍历，复用Control计数和deadline，耗尽有界停止，迟到仅审计。原始材料和独立预期保持。
范围包括Host采集/分类、typed overlay observation、World派生与ObservationNeed、既有Runtime补采调度、Director消费、配置与文档、针对性测试和注册模拟器验证。不新建Capability，不自动提交推送/晋升基线，不把XML-only旧case重新签成新窗口case，不改变其他产品Owner。

## Decisions
- D1 user 2026-10-24：采用SIM-018 proposal的保守运行行为，之前明确未决项在本scope内已裁决；不再为普通实现/验证节点请求继续。
- D2 current ADB Host管线扩展，不进CapabilityRegistry；新窗口信息属于Host来源材料，不把Android type/token写入Kernel通用Window字段。Kernel只接收带完整correlation的overlay观察候选，World派生当前状态。
- D3 typed overlay与PageCapture关联；capture显式要求overlay证据时，每个新capture先使旧present失去当前资格。缺失/错关联/迟到不得恢复控制；同capture矛盾为conflict，新capture可重建。
- D4 既有XML-only入口留作历史兼容，不用于新live判定。新窗口入口有独立新case输入；历史RED/旧Oracle不改，不谎称旧XML独自能证明窗口事实。
- D5 Host观察补采配置独立于慢模型触发：缺省每Run累计4次、同一阻塞位置总计15秒（4×本轮最大2.861秒+余量），上限仍16/30；复用现有ObservationRecoveryControl，identity与overlay共享Run额度，不因capture改变刷新deadline。配置可覆盖，超时诚实失败。
- D6 确认的测试缝：公开SettingsPopupClassifier窗口入口、World.Reconcile经EvidenceLedger、KernelRunDriver.Drive、SettingsCoverageConfig.Load；真实ADB采集与同Raw输入离线重放。此前用户已采纳这些调用链，不重复确认测试缝。

## Acceptance
|ID|method|expected|actual|evidence|
|---|---|---|---|---|
|A1|新窗口公开入口raw案例RED→GREEN|跨包普通页absent，同包dialog present，取消后absent；来源损坏/过渡/关联不足unknown；相互矛盾conflict|PASS：同manifest/carrier/input/expected，旧启发式受控消融2红4绿，候选连续两次6绿；定向完整Host106/106含坏来源；conflict由World检验|evidence/same-case-proof.json；sealed-case-red.trx；sealed-case-green-1.trx；sealed-case-green-2.trx；sealed-host-green.trx|
|A2|World公开证据入口|旧present后新capture无续证→unknown/Need；错Run/cycle/capture及迟到仅审计；已确认身份不消除overlay Need|PASS：真实EvidenceLedger/World两项测试覆盖新capture、错关联、迟到、冲突保持、新capture恢复及直接grounding阻断|evidence/driver-world-green.trx；tests/UniClaw.Kernel.Tests/World/OverlayObservationTests.cs|
|A3|实际Driver公开Drive|unknown时零清障/普通Effect；新证据恢复同执行位置不重发已投递动作；共用次数/时限，耗尽/取消有界返回|PASS：新Driver三分支与既有身份/取消/Control回归合计14/14；未知期间无新consult/effect，恢复不重发，额度耗尽/超时保持失败；取消经既有共用路径回归|evidence/driver-routing-ablation-red.trx；driver-world-green.trx|
|A4|注册模拟器新采集及真实实现|新Classifier/World实际消费default apps、真实SIM dialog与取消后，保留raw和输出；不足分支确实阻断|PASS：最终评审代码/程序集在注册API35三类各两批一致；缺来源unknown、错capture不替换；身份未提供，未宣称完整Agent live|evidence/verified-live/summary.json；verified-live/hashes.json；verified-live/review-source-verified.json|
|A5|双轴review、回归、协议/认证及保护hash|0遗留阻断，相关Host/Kernel/Simulation和工具通过，旧工件不变；变更的实现pins仅显式记录不重签旧case|PASS：Standards/Spec各0；八工程2218/2218、工具44/44、41场景认证check；27保护hash不变，20场景仅认证元数据，输入/Oracle不改|evidence/review.md；regression-final/summary.json；verification.json；composition-migration.json；composition-final-migration.jsonl；certification-migration.json|

## Owner / Assumptions / Alternatives
Evidence接纳、World状态、Control预算、Effect执行边界不变。窗口来源只资格化API35当前ADB目标；前后投影一致不证明原子，同期窗口/截图/XML变化应unknown；未知可见窗口不忽略。拒绝单纯删包名分支判absent、null不清旧present、双份弹窗真相、无限补采、仅以资源ID宣称窗口事实。

diagnosis: {path: changes/SIM-019/evidence/diagnosis.packet.json, sha256: 798bcf240203e837ecd025a9575493f92c32172f9f9f60d952c83cd26e0b528b}
原SIM-018诊断包escalation为历史未决，本包按path/hash引用；D1记录后续用户采纳，不回写原包。

## Verification
level: ENVIRONMENT
method: targeted raw component/World/Driver tests + registered API35 + reviews + recertification
expected: A1-A5
actual: A1-A5 PASS。八工程2218/2218、工具44/44、41场景认证check、双轴review各0；最终代码注册API35六批source/classifier/World一致。Driver在确定性真实实现验证，未声明完整Agent/模型live；旧XML-only历史case仍原样保留。
evidence: evidence/verification.json

## Status log
- 2026-10-24 UNDERSTAND→RESOLVE→PERSIST→PLAN：用户已采纳保守行为；诊断与16批实采复验，基线及dirty保护快照就位。纯Host管线不进Registry，新subject与required flag按真实buyer有界加入；预算15秒/4次作为可配置起点，接线后再实测。
- 2026-10-24 PLAN→IMPLEMENT：新窗口入口raw反例RED→GREEN；World当前性、冲突、错/迟到材料及共享Drive额度接线完成。源边界、配置15000ms/4次、Owner投影防旧present均落代码；同封存case旧启发式受控RED、新候选GREEN两次。实际API35采集/分类/World链路三类各两批正确，缺来源/错capture受控反例通过；身份未提供，未宣称完整Agent live。
- 2026-10-24 扩大回归：Host460/460、Simulation314/314；Kernel1106/1107，剩余新协议文档元数据已补，待复验。旧composition仅同步本次实现来源，20份场景仅认证块变化，expectations/execution与入场完全一致；工具44/44、registry及41场景认证check通过。独立评审输入固定。
- 2026-10-24 用户再次明确“完成后没有需要我裁决的继续干不要等”：普通实现、评审和验证自行推进，不重复请求继续；本scope无新的Owner未决项。
- 2026-10-24 IMPLEMENT→REVIEW：独立Standards硬规范0/异味0，Spec缺陷0；51文件评审输入未变化。Kernel协议元数据修正后1107/1107，其余五工程337/337，总计八工程2218/2218。
- 2026-10-24 REVIEW→VERIFY：最终评审代码重建程序，在注册API35持有lease内完成preflight及default apps absent、SIM dialog present、BACK后absent各连续两批；raw、程序集hash与截图保留，身份未提供，未调用模型/Effect。lease回收、adb离线，27保护hash一致。
- 2026-10-24 VERIFY→CLOSED：A1-A5四元组齐备，报告/协议/来源pins及认证记录同步，无阻塞Human Decision；本地关闭，不提交推送、不晋升旧封存case。
