# 受控状态证明的覆盖边界

公开缝：KernelRunDriver输入/Agent决策→TypedHierarchyProposalProjector→真实EvidenceLedger→WorldModel→grounding→EffectBoundary→RuntimeAssurance；读取实际receipt、verification、咨询相位、canonical post证据及Slice。不直接调用internal resolver，也不输入World/Slice/canonical ID。

| 案例 | 独立期望 | 首次实跑 → 修复后 |
|---|---|---|
| checked→unchecked/off | verified、StepVerified | PASS |
| unchecked→checked/on | verified、StepVerified | PASS |
| Partial | insufficient partial、VerificationFailed | PASS |
| Unknown/Unsupported来源 | 无checked claim、VerificationFailed | PASS，各一例；projector保留外层来源的输入记录，consumer只可见无claim，不能声称保留了最终Unknown/Unsupported细分 |
| 未变化checked/off | desired-state-not-satisfied、VerificationFailed | PASS |
| 早于/等于dispatch的post | capture-not-after-dispatch、VerificationFailed | PASS，各一例 |
| post双同名目标 | occurrence-ambiguous、VerificationFailed | PASS |
| checkable=false | 不可获状态权威、VerificationFailed | RED：实际verified/StepVerified → 同输入GREEN：false/VerificationFailed |
| checkable缺失 | 不可获状态权威、VerificationFailed | RED：实际verified/StepVerified → 同输入GREEN：false/VerificationFailed |

来源是独立指定三态值的reviewed synthetic fixture，使用MapTriState和真实产品消费链；没有声称采集器确实具备三态能力，也未给旧XML配置ExactProof。关闭/开启证明只指受控状态动作验证，载体没有目标评估，最终TerminalNotProven，不能写成任务终局Completed。

补采设计24条不是本轮11个测试；proof内容/范围/版本信任、真实XML三态适配、取消与设备复采均未被这组仿真覆盖。checkable修复另由CHECKED-GUARD-001负责，该前置已CLOSED，父线保持原状态输入及verified/phase期望，已独立复验2064/2064与34/34。来源说明纠正和拒绝原因断言收紧见carrier-finalization.json。

最终发现链：SCN-REPLAY-006完整展示名逐例精确join新鲜TRX，11/11 Passed；两个含空格的原失败子例没有被方法名级聚合掩盖。见evidence/sim-011/controlled-discovery.json。
