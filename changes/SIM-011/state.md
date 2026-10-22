# SIM-011 — 受控 checked 状态正反例
lifecycle_state: closed · disposition: none · depth: standard · base: 9bc15b8974f5fb5746074e373e6c132bc4df0089

## Intent / Scope
用户明确选择先做受控仿真正反例、不启设备，真实Stay awake补采另行安排。沿已确认的公开KernelRunDriver/projector/Ledger/World/grounding/Effect/Assurance缝，证明受控关闭/开启的正例及既有拒绝路径。不调用内部resolver，不把旧XML升级为Exact，不发明新产品协议。

写入新ControlledCheckedReplayTests.cs、controlled-checked.json、SCN-REPLAY-006及本Change/evidence；若负例暴露产品guard违反已接受契约，先保留RED并另立最小前置修复任务，等待完成后复验。当前切片不接管src或既有fixtures/认证，不碰World设计、设备/真实模型/提交推送。

## Decisions / Acceptance
1. 独立固定输入与期望；synthetic/reviewed来源明确，不冒充采集/校准。
2. checked→unchecked的off与unchecked→checked的on沿真实产品链验证；记录实际receipt/verification/咨询相位。
3. Partial/Unknown/Unsupported/未变化/过期或等时post/歧义/checkable无效的拒绝逐例检出，不把DeliveryCompleted当通过；任何未实现负例保持RED，修复只在责任层另立范围。
4. 场景Trait/schema/TRX发现，独立Review与Verify，当前源码full八工程/保护hash，前置任务完成后才收口。

## Verification
level: SCENARIO
method: 新受控正反例专项、first divergence、独立评审、当前源码full与场景覆盖
expected: Acceptance1-4
actual: 同11状态输入/verified/phase期望，RED9/11→GREEN11/11；产品前置公开guard7/7；父线独占当前源码full八工程2064/2064、34/34场景（20certified+14descriptive），SCN006逐例11条完整展示名精确TRX join；独立两轴无阻塞；可执行输入零漂移，954项保护仅30项授权前置变化，20认证body/两digest不变，旧录制hash保持。
evidence: evidence/sim-011/

## Status log
- 2026-10-22 UNDERSTAND→RESOLVE→PERSIST→PLAN：用户已选定受控仿真，无设备；前置设计现状核验已完成，先做公开运行链正例，再逐一加入负例。写前保护快照已保存。
- 2026-10-22 PLAN→IMPLEMENT→VERIFY→RESOLVE：Leader直接完成新受控载体；首个off正例1/1，11例首跑9/11。false/missing checkable均错误verified；canonical probe排除旧capture和producer丢字段，确认消费者漏既有guard。保留RED，准备CHECKED-GUARD-001前置；不修改独立expected。
- 2026-10-22 RESOLVE续行：CHECKED-GUARD-001已开启独立前置会话01a163d9-6ad0-7c13-ae78-2de46d55d5e6，父线等待。父线仅给新载体补固定拒绝原因及first-divergence原因路径，原11输入和verified/phase期望不变；frozen-inputs.json为最终载体基准。产品前置禁止修改这些新文件。
- 2026-10-22 RESOLVE→IMPLEMENT→REVIEW：CHECKED-GUARD-001 CLOSED，两轴评审/2064测试/34场景证据已复核；父线接收前先核对冻结输入零漂移。最终只纠正fixture来源说明，删除未经逐项人工审阅的human-reviewed断言；11例输入及verified/phase期望与原RED逐项相同，见carrier-finalization.json。进入父线最终评审与当前源码验证。
- 2026-10-22 REVIEW→VERIFY→CLOSED：产品前置已等待到CLOSED；父线最终当前源码独占full与参数发现、固定输入、认证、保护及同Kernel DLL核验通过，两fresh worker分别Standards/Spec无阻塞。verification.json四元组就绪，报告区分受控状态动作证明与未做的真实补采/B1/B2/终局目标。未启设备/模型、未提交推送。
