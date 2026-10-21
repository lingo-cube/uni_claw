# RUNTIME-PLAN-005 — 真实Settings遍历核心检查与有界接入
lifecycle_state: closed · disposition: none · depth: standard · base: bce0ec3a

## Intent（WHAT/WHY）
Owner授权自行检查核心点，无问题后推进真实Settings接入。验证已有Traversal不是固定页面脚本：导航经独立验证后进入子页，新World候选驱动同一规则，遍历栈维护返回；未知、安全及发现不足保持诚实。

## Scope / Out of Scope
范围：Kernel既有Traversal调用链审计与相关仿真；Kernel栈/当前页一致性及Settings Host投影/条件目录既有契约内修复；真实UniAgent生成并交Kernel执行的有界Settings试验；必要的回归及双轴评审。
范围外：新增通用Capability、重设计世界图/遍历协议、静默放行未知、滚动穷尽或全部Settings完成声明、RUNTIME-PLAN-001性能对照、提交推送。

## Decisions
- 沿用003/004正式协议与Profile26/Prompt25/Skill4（仅修正候选/全局条件指导，charter与Skill不变）；纯Host投影适配不新增Capability Registry条目。
- World拥有当前事实，Control拥有遍历进度；导航只有独立验证通过才能推进栈，返回需匹配父页。
- 真实验收输入及门槛在调用前预注册；模拟结果不替代真实设备，脚本Plan不冒充真实Agent生成。
- 发现不全时保留discovery-incomplete/gaps，即使局部遍历阶段结束也不认定整个任务成功。
- 公共测试缝沿用已批准的ProjectTraversal、RunDriverInputs与Driver查询/执行，不另建内部测试接口。

## Acceptance
- A1：审计新页重读、验证屏障、栈/返回/循环/深度、未知/安全、异步handler和完成语义；相关既有仿真及必要反例通过。
- A2：真实来源的条件目录与候选事实一致；结构文本不会被错误声明为可点击导航；未知可操作元素仍不放行；只读开关零Effect；有公共缝反例证明。
- A3：注册模拟器/专用DSH环境预检通过；真实Agent输出经过正式codec/Kernel，至少一次有界导航进入、观察子页、验证返回，并有源输出、World/遍历状态及Effect证据。若环境或语义阻塞，保留真实失败并明确恢复条件，不伪报通过。
- A4：真实任务目标/授权不为通过而放宽；未穷尽范围报告部分覆盖；双轴独立评审及相关协议/仿真回归通过，四元组可复现。

## Constraints
保留共享脏工作区；入口快照见evidence/runtime-plan-005/entry。开发模型按Owner既有Leader/Worker授权；产品保持Flash绑定与代理策略；enabled/low基线失败后，在Owner已授权模型调整范围内公告并受控评估enabled/high，配置与结果分批留证。编译由Leader串行管理。仅管理本次拥有的模拟器租约与3081服务。

## Verification
verification:
  level: ENVIRONMENT
  method: "核心代码审计、既有公共缝反例与仿真、预注册真实Agent→Kernel→Settings验收、独立双轴评审"
  expected: "A1-A4满足，有界进入/新观察/返回证据完整，未知与未穷尽不冒充完成"
  actual: "A1-A4通过；CommandCode epoch007为3/3，每轮2咨询/4Verified点击，子页World新观察与返回证据齐全；共12Verified点击、零开关Effect。discovery=false/partial保留，非全部Settings或性能对照。"
  evidence: "evidence/runtime-plan-005/"

## Status log
- 2026-10-20 · UNDERSTAND→PERSIST→PLAN · Owner授权检查后推进；共享现场已快照，范围和验收已固定，开始独立核心审计及环境预检。

- 2026-10-20 · PLAN→IMPLEMENT · 核心18/18及遍历仿真32/32通过；独立审计发现World当前页与Control栈顶缺失一致性检查，先反例修复。Host条件目录缺口已RED，纯文字标题假设已证伪。

- 2026-10-20 · IMPLEMENT · 核对正式协议发现AvailablePlanConditions是全局If目录，候选条件应由来源说明声明。撤回混合目录方案，修正Prompt原先要求Traversal条件也来自全局目录的错误句，Prompt25/Profile26；旧catalog-red仅作诊断证据，不作为最终契约。configured-only范围外候选反例RED后已GREEN，动态未知保持Unknown。

- 2026-10-20 · IMPLEMENT→REVIEW · 暂停期间同producer新scope观察反例确证旧代码实际点击unrelated/Surprise（path-resume-red3）；栈/World页guard后33/33仿真通过。Host29/29、协议16/16、Profile19/19、Node49/49；Profile/Prompt递增的两处初始化指纹断言同步，Host.Dsh当前45/45。相对入口18文件delta冻结独立双轴评审，真实模型调用仍0。

- 2026-10-20 · REVIEW→IMPLEMENT · Standards0；Spec2：预注册需显式冻结并核对input/expected，readonly允许Unknown不能要求字符串已知态。已补JSON结构严格比对与两入口各一次断言，取消不当known-state前提。另记录既有SettingsTraversalLiveFeed将WiFi two-state proof用于通用捕获的独立感知限制；本片证明World关联记录/零开关Effect，不认证所有开关状态语义，后续单独收紧。

- 2026-10-20 · IMPLEMENT→REVIEW · 第二轮发现harness实际预算128/32与注册12/12不一致及未绑定设备，现实际六项额度均12，注册核对直接读取实际config，设备serial+注册lease状态绑定。Leader另发现Agent预构造RunId不等于Kernel真实RunId，改为首个真实context懒绑定并单独落runtime-binding证据。尚未调用模型；第三轮复核冻结18项。

- 2026-10-20 · REVIEW→VERIFY · 第三轮Spec0/Standards0；312项源码/工件与任务input/expected、Profile26/Prompt25/Skill4/Flash enabled-low已在首次调用前冻结，正式epoch001为3个独立会话；每次4验证tap、2咨询、每咨询至多1采纳前修订，World记录允许Unknown并保留discovery-incomplete。开始真实模型/注册设备验收。

- 2026-10-20 · VERIFY→IMPLEMENT · epoch001首例真实模型生成合法Traverse，1咨询、0Effect，但FileConsultationJournal私有PlanItem持久化union遗漏Traverse，Complete写入抛unknown durable PlanItem type；其余2例未调用，保留失败批，不继续浪费模型。Host日志适配缝需补合法Plan读写，不改变产品wire/Kernel权威。

- 2026-10-20 · IMPLEMENT→REVIEW · 日志公开往返反例17ms复现同一异常；只补私有Read/Write Traverse union两处分支，Host相关34/34通过，原真实失败需新epoch重跑。角色oracle从强制$candidate修正为当前Source合法的$candidate或ui.element同义绑定，Task说明相应明确；所有行为/调用/修订阈值与Goal/授权保持。第四轮增量独立评审中。

- 2026-10-20 · REVIEW→VERIFY · 第四轮Spec0/Standards0，312项源与同一Goal/授权、完整input/expected于调用前冻结epoch002，沿用3次独立会话与原数值门槛。Host日志新往返及相关集34/34通过，开始修复后真实复验。

- 2026-10-20 · VERIFY→IMPLEMENT · epoch002首例实际4/4 Effect Verified、2咨询，Runtime Completed/有界CoverageComplete，Traversal Completed+endReason partial+discovery false。冻结harness错误拒绝有真实4个plan-attempt锚的合法completion，且后续错误期望endReason completed；保留正式失败，不重标通过。按既有RunModel语义改oracle为允许缺省completion、如有则逐项匹配该请求真实Verified+receipt的plan-attempt引用；endReason要求partial。Goal/授权及所有数值阈值未改，无产品实现改动，新epoch重验。

- 2026-10-20 · REVIEW→VERIFY · 第五轮两轴0发现，20项delta hash一致；epoch003在首次调用前冻结312项源、任务input/expected及原数值门槛，允许当前Verified真实锚和canonical partial，不改变Goal/授权。原epoch001与002均保留失败批；开始修正oracle后的3次正式运行。

- 2026-10-20 · VERIFY→IMPLEMENT · epoch003正式3次全跑，UI遍历3/3完成、12/12点击Verified、开关0Effect，但终局验收2/3：第三次收尾两次provider MALFORMED_RESPONSE（非maxTokens，输出551/382 tokens），未形成submit_decision，触发既有一次修订上限后no-response。严格保留失败，不降低3/3门槛。只读定位DSH translate.ts严格JSON.parse；未保存原始坏字节，归因限于模型/provider输出解析边界。下一片仅简化本Task的已满足收尾指令为短NoAction、免不必要completion；输入改动调用前另冻结，无Kernel自动补JSON/扩大重试或模型变更。

- 2026-10-20 · REVIEW→VERIFY · 第六轮两轴0新增发现，模型/协议/Kernel/预算/修订次数不变；epoch004冻结312项源和更短的满足态结束指令，原3/3门槛保持。旧批所有成功/失败原样保留，开始最后这一输入候选的固定3次验收。

- 2026-10-20 · VERIFY→RESOLVE→PERSIST · epoch004完整3次最终1/3，后两次首次计划即两次MALFORMED_RESPONSE后零Effect停止，简短收尾不能解释首次生成故障。依Owner既有模型调整授权并提前公告，仅将专用3081同一Flash的两处effort low改high（支持列表仅off/low/high/max，未用medium）；私有备份、两字段diff和hash已落thinking-high-config-change.json，未改代理/产品源/预算/门槛。epoch005与004的312项源、Goal/Task/input/expected完全相同，只变effort，调用前重新冻结。

- 2026-10-20 · PERSIST→REVIEW→VERIFY · high专用服务PID50124已加载相同Prompt25/Skill4，004/005的Goal/Task/input/expected/312源/全部数值门槛逐字段比较一致，客户端只请求相同provider/name，effort由宿主唯一受控字段提供。epoch005在调用前冻结，开始固定3次真实运行，结果不用于宣称长期或全UI稳定性。

- 2026-10-20 · VERIFY · epoch005三例均被DeepSeek QUOTA/HTTP402余额不足拒绝，无high质量结论；停止新增请求并恢复原low配置，旧设备租约已回收。前述将high首例等同malformed的推测被原始turn/end证据纠正。

- 2026-10-20 · VERIFY→RESOLVE→PERSIST→VERIFY · Owner明确授权“改成CommandCode里的deepseek flash模型，继续重试”。从现有CommandCode目录选择deepseek/deepseek-v4.1-flash（当前官方Flash目录也标注V4.1）；仅新增/选择DSH绑定并调整专用3081默认，不改Profile/Prompt/Skill/协议/Kernel或代理/凭据。私有备份及hash见commandcode-binding-change.json。新注册租约与epoch006在首次调用前冻结；312项源仅binding一项变化，Goal/Task/expected/原3/3门槛不变。供应商/adapter更换不作为同模型性能对照。

- 2026-10-20 · VERIFY · epoch006首例CommandCode首稿submit accepted、正式codec通过，验收callback在Kernel前因额外mcp-resource-servers section（无关Jev工具说明）拒绝，零Effect。余2例不调用，保留整批失败；专用3081仅移除无关Jev sidecar，不放宽原两产品section精确校验，CommandCode模型provider/代理/凭据不变。epoch007首次调用前重新冻结同312源码/Goal/Task/预算/3次门槛。实际CommandCode header无reasoningEffort，007如实冻结null并记录宿主默认low，不把provider默认等同官方enabled/low。

- 2026-10-20 · VERIFY→CLOSED · Owner授权的CommandCode V4.1 Flash最终epoch007为3/3，6次语义咨询、12Verified tap、零开关Effect、3fresh sessions；1次采纳前修订在原上限内。Leader逐项核对312源零漂移、2入口各一次/2返回、子页新World只读记录、真实receipt/completion锚、discovery=false/partial及精确2段上下文。模型管理14/14；既有核心/仿真/协议与Node检查通过，review7两轴无未解决产品问题。A1-A4四元组见verification.json，所有15次尝试/23咨询保留。正式绑定保留CommandCode，原官方malformed与通用switch证明限制不伪称修复；本Scope完成，无阻塞Human Decision，无提交/推送。

- 2026-10-20 · 收尾 · 当前注册设备租约已回收，3081保留授权CommandCode绑定。312源/21评审hash复核、7 WorkItem schema与owned diff检查通过，HEAD未变。共享宿主目录在最后TRX之后mtime发生更新（JS引用改同目标inline目录），当前provider/model仍相同；原冻结记录不改，未覆盖未知写者修改，详情final-environment.json。
