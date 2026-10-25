# RUNTIME-RECOVERY-010 — Run/资源/Evidence/World共同历史位置
lifecycle_state: closed · disposition: none · depth: standard · base: a05176f3

## Intent / Scope
用户“下一步”授权沿009交付接入四方原历史位置。Driver原资源Initialize/Begin/Confirm携带原World操作位置，World-only变化由既有checkpoint确认；原archive绑定World录制；独立只读入口认证四方精确末位并保留可读分项。拥有evidence/ownership.json内11个源/测试/README及本Change/INDEX/双轴review WorkItems；其他dirty与旧证据不动。

## Decisions / Authority
- 沿product baseline§12/18/20、ADR0054及RECOVERY001契约§4.2、007三方与009原World数据读取。Runtime负责共同提交/比较，World保持原决定Owner，Host只存/读原源；不新增Registry capability或第二套生命周期。
- 记录WorldHistoryPosition在原资源确认记录中，必须晚于对应World ack，原source GUID从初始化绑定且位置不后退、不半途补绑/脱绑。原archive升级payload schema并保持旧v1/v2资源/两方/三方只读语义；旧无World关联不自动认证四方。
- 新四方结果保留原三方读取结果及可认证的原World历史；精确World末位/原Run/Evidence关联不符则Point=null。World文件可读不等于四方成功；不退回旧World/Run/Evidence前缀、不补记，不改原budget/P25/Effect消费语义。
- 停止原writer是读取前提；范围为可信本机单writer的已确认文件与真实进程Kill。ContinuationSupported恒false，不恢复当前World/Control/Plan/coverage，不激活/Drive/重判/续跑，不跑设备/模型/live服务，不提交推送。
- 复用持续授权的公开Driver/原文件adapter/readonly reader/子进程测试缝；不再询问常规工程选择。source GUID及字段形状是工程选择，不创造新的执行许可。

## Acceptance
1. 两正式Host经原Driver资源记录从初始化至确认携带原World位置；archive绑定真实World录制GUID。新四方只读入口对原Run/资源/Evidence/World精确末位认证，完整位置不可伪造为Current/执行权。
2. 仅World relevance/demand/continuity日志增长而Run/Evidence不动时，读出明确gap；原Driver正常安全返回的既有checkpoint可确认新末位，资源账目不变，不增咨询/Effect或Run/Evidence事实。
3. World缺/坏/半写/head/回退/后缀/错源/错位置、原依赖错误或资源World关联非法转移均拒绝共同点；旧无World关联仍可两方/三方读取，不能自动升级。坏World保留三方，坏Run/资源仍保留可读World/Evidence，缺必要Evidence则World不冒充认证。
4. checkpoint确认前/后故障保持既有失败锁存、不重试、不额外执行；只有所有原记录已确认才可认证四方；实际非terminal Kill后不同reader PID重复读取，原源SHA不变；有World未确认后缀时明确不一致。
5. 公开行为RED→GREEN、原三方/World/资源相关回归与白名单检查；独立双轴评审、source保护和可复验四元组。旧宽Host13个门不冒充通过、不修改旧manifest/oracle。

## Plan
先用正式Host原资源记录证明World位置缺口RED；最小接入World位置/原archive源绑定；实现独立四方比较；按World-only gap、非法来源/位置、故障、真实Kill补拒绝及读取证据，相关回归后锁精确dirty-entry增量进行独立评审与验收。

## Verification
level: SCENARIO
method: original public Driver/resource/archive/World readonly seams and controlled independent Kill/read
expected: A1-A5
actual: Host recovery 228/228 (36 new four-way), Kernel related 410/410, controlled DSH 5/5; independent native/endpoints/PID/readonly/source checks passed; no current or continuation; prior wide Host 13 gates not recertified
evidence: evidence/

## Status log
- 2026-10-24 UNDERSTAND→RESOLVE→PERSIST→PLAN：复核009 CLOSED与原19个候选输入SHA未变，base实况a05176f3。当前共同点仅三方；资源checkpoint只比较Run/Evidence位置，World-only操作会留未关联后缀。按已有恢复契约补四方原历史位置，保持current/continuation关闭；11输入责任与entry快照已锁，保留其余dirty。

- 2026-10-24 IMPLEMENT→REVIEW：正式World位置缺口与四方reader缺口各行为RED0/1→GREEN；11文件精确候选已锁，618个既有C#/项目输入除7个owned既有输入外，611个均未漂移；另有2个owned新增C#，最终输入620个。最终Host相关228/228（新四方36项，旧三方/资源/Evidence/World回归，无排除），Kernel相关410/410含public whitelist通过；DSH受控模拟复验中。World-only gap/确认不改变账目与其他Owner；缺/坏/错源/位置转移/旧schema及故障覆盖，三种非terminal实际Kill各两个reader，均无current/continuation权限。两处测试夹具失败（无revision调用continuity、文件swap时仍持writer sharing锁）保留，未为绿改业务。
- 2026-10-24 VERIFY证据：DSH受控模拟5/5通过；独立Python核验exit0，原archive/Run/Resource/Evidence/World帧、head、原GUID、精确末位、原World字段与顺序及三种Kill各两个新reader/PID/源SHA均通过，4种World-only变化在原checkpoint前后资源快照/Run/Evidence位置不变。009封存426文件SHA逐一核对未变；未重跑/改写其旧源锁或13个旧门。证据README、复验脚本及程序集指纹已保存。
- 2026-10-24 REVIEW→VERIFY：独立Spec与Standards均核对11/11 SHA通过、零可行动发现。最终保护加入根Directory.Build.props（与009已验证输入一致），总621输入；源码/项目清单新增/删除也严格检测。两Review WorkItem done；A1–A5四元组及受控/实际Kill/旧门限制已持久化，待最终独立核验后关闭。
- 2026-10-24 VERIFY→CLOSED：最终verify-before-close退出0，A1–A5在声明范围内满足；11候选与621最终输入无漂移，原009封存426文件未改变。支持四方精确原历史共同点；Current World、Control/Plan/coverage及执行续跑仍关闭，无设备/模型/live服务/提交推送。下一步审计Control/Plan原采纳版本、工作位置、未决handler与原等待期限的持久来源及恢复缝，再据真实buyer确定实施边界。
