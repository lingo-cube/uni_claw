# SIM-012 — 最近一周原始页面录制接入安全恢复回放
lifecycle_state: closed · disposition: none · depth: standard · base: 9bc15b8974f5fb5746074e373e6c132bc4df0089

## Intent / Scope
用户扩展旧录制到最近一周。窗口按上海时间2026-10-16 00:00至2026-10-23 00:00（不含）；依据录制内容时间，不依赖文件修改时间。优先纳入已有原始前后XML/动作的SIMs、All apps、Default apps三条10月20日UTC录制。
仅新增RecentRecordingReplayTests、RecentRecordingImportTests、独立固定recent-recordings.json及导入资产、SCN-REPLAY-007、此Change/evidence。公开缝沿已接入的Host XML parser/typed projector/occurrence strategy离线导入，Simulation使用PageObservationCodec与KernelRunDriver，经World/grounding/Effect/Assurance验证。本切片只验证一个前向hop，受控Plan MaxLocalOperations=1，恢复后不触发额外返回动作；Simulation不依赖Host；不修改产品源码/旧fixture/既有认证。

## Decisions / Boundaries
原始录制、历史派生诊断、当前确定性回放分开。页面身份补充使用明确标注的受控感知替身，不冒充原始模型输出、自动标题识别修复或真机验证。固定输入不含canonical World ID；World负责身份与owner。历史失败事实可追溯原录制，但旧反射诊断不升级为当前公开链路复现。
不启设备/真实模型，不执行录制坐标/设置动作，不接管WORLD-UNCERTAINTY设计，不提交推送。最近一周全量自动录制导入、真实Host采集/识别回归如尚无公共离线缝，明确列gap而非宣告已完成。

## Acceptance
1. 三条录制以原始内容时间、前后capture/动作/文件SHA固定，来源独立于派生诊断。原文件不改；Host公开parser导入与冻结布局一致。
2. 每条公开KernelRunDriver回放：新页面缺身份→暂停/ownerless；错误capture身份→仍暂停；同Run匹配身份→恢复验证并归属新页面，无重发动作，旧World snapshot不变。
3. 独立固定预期检查目标文字、新页面控件、深度/收据/验证/owner及参数场景TRX发现；至少一个针对oracle的故障注入被检出，不能通过改期望掩盖失败。
4. 独立评审与当前源码隔离验证，保护hash零非授权变化，报告区分历史错误与当前安全恢复，不把确定性感知替身PASS称为真实Host修复。

## Verification
level: SCENARIO
method: 来源hash与Host导入核验、三条实际录制布局公开运行链、故障注入、参数发现与当前源码完整验证、独立评审
expected: Acceptance1-4
actual: Host导入3/3、公开运行链3/3，旧签名producer注入3/3被独立owner oracle检出；当前源码隔离full八工程2070/2070，35/35场景（20certified+15descriptive），新增三个完整展示名精确TRX join；两轴独立评审无阻断；2204保护项/25冻结输入零漂移、同Kernel构建artifact、无产品源码修改。仅完成三条代表录制的受控消费者恢复回归，未完成真实Host producer或原模型整段Run回放。
evidence: evidence/sim-012/

## Status log
- 2026-10-22 UNDERSTAND→RESOLVE→PERSIST→PLAN：上一轮2064/34证据与可执行输入复验；用户扩展最近一周，确定三条原始录制候选及既有公共缝，不扩产品协议。
- 2026-10-22 PLAN→IMPLEMENT→REVIEW：Host导入3/3、公开运行链3/3；注入旧签名复用后独立owner oracle全部3/3拒绝，固定期望/录制未改。初版测试载体nullable断言与不必要回程分别失败并保留，修正仅限测试侧nullable处理及单hop预算，不当产品RED。Spec fresh评审无阻断，首个Standards worker无有效输出已拒绝，重新有界派发。
- 2026-10-22 REVIEW→VERIFY→CLOSED：有效fresh Spec与有界Standards评审均无阻断；full首次缺参数/缺restore均保留为入口失败，补齐条件后独占完整运行PASS。原fixture/源码/认证不变，source与冻结输入保护均通过。四元组及剩余producer/整段Run gap见verification.json、report.md。本次CLOSED只指有界三条新录制回归，不代表用户整体目标全部完成。
