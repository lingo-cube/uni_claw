# SIM-037 — 真实Agent在有限三层菜单仿真中的自主遍历
lifecycle_state: resolving · disposition: none · depth: standard · base: a05176f3
source_qualification: v1-v3为SIM036封存源；v4为HEAD加明确修复/窗口先决的独立发布候选，未整树带入共享并行修改
diagnosis: {path: evidence/v4/diagnosis-normal-007.packet.json, sha256: b8ac93226370c0eb523b672588dffde0065d16b69a1c5e5cfc1171e5475fbeea}

## Intent / Scope
用户授权继续“真实Agent＋仿真环境”的自主遍历验证。先以已有三层有限菜单正常场景验证真实Agent选择动作、滚动、返回、同名目标区分及终局判断；正常通过后逐项注入弹窗、错误返回和滚动发现失败。只拥有新测试载体、必要测试级复用声明及本Change。产品Runtime、旧测试行为/fixture/Oracle、Profile/Prompt/Skill、模型绑定/代理/凭据、其他Change和观察台不改，不启动设备。

## Decisions / Seams
- 使用用户已授权的公开SettingsCoverageRunner.Run、FeedNext/IEffectDriver和正式DshAgentAdapter/DshOpenedDecisionChannel/DshOpenedHttpPeer；采集/投递为合成，Kernel/Evidence/World/Control/Assurance和Agent决策为真件。
- 复用既有MenuWorld、独立menu.json与Audit，仅将测试类声明改partial以供新文件复用；Host.Tests链接既有DSH请求轨迹采集器源码，不建立产品接口/测试程序集循环依赖。原8项菜单测试断言和输入保持。
- Agent仅收到真实公开context及固定中文任务范围，不给隐藏菜单、Oracle、精确动作序列或运行中的评估答案。允许既有Act/Plan/NoAction等正式决策，所有动作仍由现行Runtime逐步验证。
- 原Audit的AllTargetsAccounted要求对禁止目标实际guard判拒，适用于原脚本；本自主任务不鼓励尝试禁手，采用原AllTraversableReached（8导航＋8正确返回）以及零禁手/开关投递的独立验收，不修改旧Oracle。未知目标不据自报推成已覆盖。
- 每个正式case至多1次同输入调用；新载体/环境缺陷修正须新版本并保留原尝试，禁止抽样重跑追绿。正常固定maxSteps=32、maxConsultRounds=36、每次咨询90秒、启动与测试共用570秒，回收预留30秒，外部wall-clock最多600秒，无续额。异常在正常通过后另行冻结；缺模型/凭据/握手失败为ENVIRONMENT_UNAVAILABLE，不能冒充仿真通过。
- 专用3081若空闲由本轮启动并保留PID租约；若他人占用，不停止/重载他人服务。每个case独立session，finally detach/dispose，持久配置指纹前后相同，仅停止本轮服务。

## Acceptance
A1 正常场景真实模型请求与submit_decision可追溯；原独立图8个导航、8条正确返回、两个父页Summary、滚动发现与最终Settings均得到实际步骤证明，全部Effect Verified，零禁手/开关投递；Agent终局与Kernel Completed及独立图同时成立，不以自报完成替代。
A2 正常通过后，分别验证窗口弹窗、错误返回、漏滚动的恢复或诚实有界停止；完整性不足不得宣称完成，每个异常独立来源和预期，失败保留。正常未通过时不扩大异常试跑，定位首个分歧。
A3 原79项基础回归保持，旧夹具/Oracle和非拥有封存源SHA不变；请求实际provider/model、输入/执行物、预算、清理与配置不变都有证据。双轴审查与四元组准确限定为有限合成环境的真实模型能力。

## Plan
固定SIM036源与三层Oracle→新增可选择真实Agent的测试级组合→公开缝确定性载体验证/原回归→独立两轴审查→启动专线/真实正常任务→通过后逐项异常→独立对账与来源/租约核验。

## Verification
level: SCENARIO + ENVIRONMENT
method: 固定源码和输入、正式DSH真实请求/决策轨迹、独立8导航8返回审计、原回归与资源/配置核对
expected: A1–A3
actual: v1–v3原记录保持（19次semantic咨询、正常均FAIL）；v4发布候选153项确定性检查通过、无Skip，原79按名称逐项确认。normal004–006为0模型咨询的环境/接线失败；normal007实际8咨询，2/8导航/2/8返回、6投递5Verified、零危险，Kernel Completed但独立全图FAIL。两次产品修复未在本样本再出现原字段/控制项模式，仍未证明全遍历。A1 FAIL、A2未运行，Change保持resolving；来源、租约、配置与归档见v4记录。
evidence: evidence/

## Status log
- 2026-10-25 UNDERSTAND→RESOLVE→PERSIST→PLAN：用户授权执行推荐的真实Agent＋有限仿真，固定SIM036已验证源与原绑定。正常先行，失败不扩量；仿真不同于真实设备覆盖。
- 2026-10-25 PLAN→IMPLEMENT：新增测试级真实DSH组合、原图复用和独立反例；原合同仅根页强制义务，Kernel Completed不能代替8导航/8返回审计。新载体的PlanId/初始预期问题已定位修正，carrier/和carrier-observed/原失败保留，carrier-v3/两项通过；尚无真实模型PASS。
- 2026-10-25 REVIEW→IMPLEMENT→REVIEW：Spec预审发现启动60秒与测试600秒串联会超外部上限；改为单一monotonic截止时间，启动/测试共570秒、回收预留30秒，实际总时长超600不通过。原评审发现保留；正常真实模型尚未启动。Standards初审0发现。
- 2026-10-25 REVIEW→VERIFY：Spec时间阻塞已由复核关闭，Standards补审无问题；原79项实际通过、两项新载体通过，真实模型尚未执行。按prelive-review.json锁源/执行物/输入与评审，进入唯一normal-001正式任务。
- 2026-10-25 VERIFY→RESOLVE→IMPLEMENT：normal-001真实7咨询、2/8导航/2/8返回后诚实Blocked，78秒、own服务/测试回收且配置不变。新载体同时把所有安全根目标设为configured又要求1个非configured滚动入口，并将禁止Factory reset留作动态待办；完整原脚本也无法满足声明。冻结v1源/档案不改；v2仅修新载体，先公开Runner证明Goal可满足，再另注册输入不同的normal-002。子菜单输入实际已到Agent，未归因采集遗漏或模型能力。
- 2026-10-25 IMPLEMENT→REVIEW（v2）：公开Runner完整8导航/8返回先复现两个不可达指标；仅将Apps从configured改为动态发现后滚动指标通过、禁止目标待办仍失败，再仅显式排除Factory reset后所有声明条目和Kernel Completed均通过。保留两次确定性RED及原真实FAIL；v1私有源不变。v2中文任务、模型、产品、菜单Oracle及32/36/600预算全部不变，独立A1不放宽，补强成功载体需全部声明可满足。
- 2026-10-25 REVIEW→VERIFY（v2）：两轴窄预审0新阻断，载体完整图＋全部声明＋Completed已证可满足；原79项v2全部通过。按v2/prelive-review锁定实际源/执行物/新输入，进入唯一normal-002；v1正常失败保持。
- 2026-10-25 VERIFY→RESOLVE→IMPLEMENT（v3）：normal-002四真实咨询、五步全Verified、2/8导航2/8返回；第四响应两步Act被现行Host policy拒收转null，Kernel表为no-response，非模型/网络无响应。捕获完整原因并保留v2源。新增公开Runner Act/Plan对照：同两意图multi-Act零投递，Plan两步验证通过；仅在新测试任务声明现有单步Act/多步Plan及每层滚动/子树范围，产品/全局Prompt/Skill/模型/预算不改。v3四载体通过；第三个输入独立注册，不将旧FAIL改成PASS。
- 2026-10-25 REVIEW→VERIFY（v3）：Spec无阻断，Standards一项非阻断目录生命周期重复建议保持最小实现；4项载体、原79项通过。固定v3独立输入、实际执行物和评审快照，启动唯一normal-003。v1/v2原评审SHA以前缀精确归档保存，根报告仅追加各阶段复核；不改原prelive pin。
- 2026-10-25 VERIFY→RESOLVE（v3实际失败）：normal-003八真实咨询、2/8导航/2/8返回，6投递5验证、零危险，最后诚实Blocked。首个分歧为Plan把Navigate up写作targetRole且缺targetDescriptor，Runtime明确反馈missing target descriptor；后续跳过可见Details再上滚使有限单向viewport不可回取，并把Reobserve后的尾部当可执行阶段。权威/安全门正确拒绝，未证明产品实现回归。A1保持FAIL、A2按前置门未启动；保留三个独立输入及19真实咨询，不继续重采追绿，不关闭Change或全遍历父任务。
- 2026-10-25 最终实况REVIEW（不关闭）：Spec证据误报0、A1完成阻断1保持；Standards新增硬违规0，保留1项非阻断重复建议。verify.py确认三版本各948固定输入、十九次实际semantic咨询、所有失败/租约/配置不变与仅测试落地；A1 FAIL/A2未运行，处于RESOLVE，不冒充完成。

## 诊断索引
- normal-001：evidence/diagnosis.packet.json，sha256=988412d224f026aa6dd3b85ca27f3b84e9a0f69a435f09678e47a48d87905376（新载体目标资格）。
- normal-002：evidence/diagnosis-normal-002.packet.json，sha256=24b164de09c374dbfa15dd046eddfeef2dbdd4604809ec6b4408ab277c8b8f88（Host调用形状对齐）。
- normal-003：evidence/diagnosis-normal-003.packet.json，sha256=785a3be047fda83e32227cae68b08a8059c75ce386aae887ecfe0888f2908f94（Agent计划绑定与探索）。

## v4：修复后的独立正常验证（2026-10-27）
用户授权恢复正常真实Agent仿真，并提交/推送本次工作。原v1–v3及19次咨询、诊断、失败和prelive pins保留。v4以HEAD为基础逐项应用已审查的产品修复和必需载体依赖，排除共享树无关存储/契约作者等并行改动；独立normal-004只运行一次，保持8导航/8返回、零禁手、32步/36咨询/600秒门槛。修改测试任务的过时“Act只允许单步”约束为已接受的当前容器局部Act、跨页Plan规则；不放宽覆盖要求，不把正确安全停止当完整通过。原79买方在发布候选上复验，正常未通过前不扩展异常。
- 2026-10-27 RESOLVE→PERSIST→PLAN：固定最小发布候选与normal-004输入；记录先决文件缺失导致的编译失败，未调用模型。真实调用须等待接线与买方检查及prelive审查通过。
- 2026-10-27 PLAN→IMPLEMENT→REVIEW→VERIFY（v4）：HEAD最小候选补入原窗口/overlay证明及测试先决，旧79按名称逐例确认通过；最终Host77/77、DSH13/13、Simulation33/33、Profile19/19、Node9/9，均零Skip。规范发现1项多带来源注释已剔除，最终Spec/Standards均无剩余阻断；prelive-review锁源码、输入、执行物与两轴结论，准备唯一normal-004。
- 2026-10-27 VERIFY→RESOLVE（v4实际结果）：normal004插件缺文件、normal005覆盖未替换根入口、normal006匿名预检缺认证均为环境不可用且零模型咨询，原记录保留。使用临时私有模块覆盖和正式C#认证预检后，normal007实际8咨询、2/8导航/2/8返回、6投递5Verified、零危险；Kernel Completed但独立图FAIL。字段漏填和Reobserve尾部模式本样本未出现；仍先滚过Details并以局部根页义务结束。A1 FAIL、A2未启动；没有重采模型或扩大修复，自有进程回收与配置不变通过，准备按授权提交本次明确范围。
- 2026-10-27 发布准备：Kernel局部Act取消/恢复2/2补验通过，确定性检查合计153；独立结果复核无误报阻断。按精确候选stage源码和经检查的合成输入/决策、紧凑结果；旧原始轨迹/构建归档仅本地保留，不将整个共享证据树推送。
