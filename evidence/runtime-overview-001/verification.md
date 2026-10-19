# ADR-0052 / RUNTIME-OVERVIEW-001 实施验收（2026-10-18）

最新结果：Owner继续后完成独立复核并修复P1/P2；最终版本以本文末尾“独立复核后验收”及review-followup/证据为准。以下首轮结果与失败尝试保留作历史，不冒充最终源版本验证。

## 做了什么、原来的问题与修改

原调用链只传可选续额窗口与本屏计数；Settings身份固定为单容器，Host按计数硬停。现在 `KernelRunDriver → AgentDecisionContext.ExecutionOverview/BudgetReview → AgentDecision.ReviewResponse → Runtime规则/账本` 形成明确复核；`SettingsTraversalLiveFeed → WorldModel → UniKernel.VerifyPostActionEffect → NavigatesTo → overview.WorldGraph` 提供观察背书的页面图。完整修改入口见 implementation-files.txt，最终源哈希见 implementation-hashes.json；共享入口恢复依据见 entry/，不是HEAD至今全部dirty改动均归本Change。

- Runtime分别保存初始、累计、剩余与实际授额区间；20%以初始额度为基数。每次咨询只取一次同源投影，保留相邻overview差量、旧预期/实际及必要历史，判断单列。只有真实grant重置区间起点。
- 漏答最多一次计入原咨询资源的纠正；同一已处理触发锁存到新授额区间、资源维度或稳定事实变化。观察事件号、画面重复、成功动作数不当作新的目标成果；同路线无新事实拒绝续额，偏差时提供更多历史。
- World只持页面/观察背书的导航边。导航登记入口为同assembly内部，由独立验证成功后调用，双端revision/锚点/accepted evidence均核对；图不承接完成、资源或Effect许可。
- Driver、UniKernel验证、PolicyLease、层级与frame投影均按当前锚点消费，多页历史不混入本步grounding。根页观察积累全局入口；配置种子、已观察、验证、受阻、跳过和未知分别投影。
- Goal模式Director只装饰事实并校验动作权限，由Agent选择深入/返回/换路；旧模式兼容。停滞/滚动/连续失败不再单独宣判全局目标失败；明确授权总边界与动作安全策略保留。Runner不再按初始额度计算第二个循环硬顶。
- 终局仅到根页不足以完成本任务；CheckTaskEvidenceBarrier只能拒绝缺失任务证据，不能授予完成。已有Assurance/Outcome及完成锚点核验继续有效。
- 协议、严格codec、文件journal、Prompt与profile revision 12、schema/TS及插件副本同步；动态输入前部明确本轮复核号/纠正次数/必答形状。强制复核时不在adapter内部免费追加no-submit咨询。

## Review（实现质量，与Verify分开）

GPT-6 Luna图/Runtime/Coverage/协议切片的代码已落盘，Leader按spec与standards两个轴复查并修复：多页hierarchy owner=null、投影先于verification导致滚动发现错序、旧消费单容器、拒续误重置区间、旧观察号绕过无成果检测、复核机械重开、旧字段授额绕过、旧预期与新计划混配、完成只核对root以及profile钉扎测试陈旧等问题。对应回归和失败尝试保留。

两名独立评审Worker因GPT-6 Luna额度限制未完成，因此不声称完成独立双评审；最终两轴审查由Leader执行，无剩余已知阻塞缺陷。2026-10-18 Owner最新限制为最多一个Luna Worker；已无运行中Worker，后续不得并行安排。未换其他模型。

## Verify四元组

| method | expected | actual | evidence |
|---|---|---|---|
| dotnet test tests/UniClaw.Kernel.Tests --no-restore | 核心规则、图/当前锚点、主动复核、纠正、段统计与资源边界回归 | 908/908；预算14例含96/112→20/23、无任意累计顶跨256、同路线仅新观察refs仍拒、显式授权总顶拒绝 | kernel-verified.txt；segment-history-final-2.txt；authorized-total-final.txt |
| dotnet test tests/UniClaw.Host.Tests --no-restore | 全局已发现/未知、深入、局部受阻、旧策略与任务完成证明 | 331/331；新NoAction提前完成用例返回task-evidence-unproven；本组未启用真实设备不冒充现场 | host-verified.txt；host-completion-final.txt |
| dotnet test tests/UniClaw.Agent.Dsh.Tests --no-restore | 严格新旧载荷、schema生成/codec/闭包、回放 | 178/178 | agent-dsh-full.txt |
| dotnet test tests/UniClaw.Agent.Tests / tests/UniClaw.Host.Dsh.Tests --no-restore | profile/prompt revision及正式装配同步 | 29/29、33/33 | agent-profile-verified.txt；host-dsh-verified.txt |
| scenario_certify --change RUNTIME-OVERVIEW-001 --all；Simulation测试 | causal source变化再认证，行为不改写 | 20个块刷新，28文件检查0违规；仿真188/188 | scenario-certification-final.txt；scenario-check.txt；simulation-verified.txt |
| node --test dsh/uniclaw-decision-channel/tests/plugin.test.mjs | 新复核严格拒绝/顶层投影与既有插件行为 | 40/40 | plugin-final-3.txt |
| prompt hash、workspace schema、部署漂移及diff检查 | 单源产物与正式3081实例一致 | 两份Prompt hash通过；13 schema及examples通过；部署39文件clean；diff无空白错误 | workspace-schemas.txt；deployed-drift-check.txt；diff-check.txt |
| DSH_TEST_PERCEPTION_LIVE=1 UNICLAW_ANDROID_DEVICE=emulator-5558 dotnet test tests/UniClaw.Host.Tests --filter RuntimeOverviewLiveTests | 真机实际进入/返回、同源overview及有资源记账的明确续额 | 单独启用1/1；2次独立验证导航、2页面2边、一次授额；声明的小范围覆盖完成，未观察分支仍Unknown | device-verified.txt；environment-summary.json device条目及其run目录 |
| 专用3081 + 新emulator-5558，live-scoped-config.yaml真实模型探针 | 真模型消费overview，显式申请、规则记账，多维资源连续；缺证不假完成 | 4次有效双维授额，每次1步/2轮；5次派发，2组页面进入/返回，3页面4边；滚动入口未覆盖且完成锚点无法核验，停在既有AwaitingCompletionAdjudication，没有全遍历成功证据 | live-model-final-2.txt；environment-summary.json model-final条目及其consult-bridge/facts/graph/preflight |

preflight均先于正式运行；设备由注册脚本克隆启动，本任务仅拥有5558，验收后按lease回收，未对5556借用设备执行启停；3080未使用。实际真实模型按当前产品绑定为deepseek-official/deepseek-flash，开发Worker模型仍为指定GPT-6 Luna，二者不可混淆。

## 提案反例索引

根页只观察即发现/种子未观察/新增范围与Unknown：SettingsCoverageOverviewProjectorTests。深入当前分支不强制返根、停滞不Host硬停：SettingsCoverageDirectorPlanTests。多入口、同名上下文、返回、失败无成功边：WorldObservedNavigationTests与KernelRunDriverNavigationGraphTests。必要路径、区间/相邻计数、重规划、不重置初始额度、20%及部分维度：KernelRunDriverBudgetRenewalTests及真实模型事实。漏答/两次no-response、同路线仅新增观察、跨256、显式授权顶：同预算回归。动作权限仍由SettingsActionGuard回归。已知待办与实际完成证据不同：GoalDirectedCompletion_RequiresDeclaredCoverageEvidenceBeyondRootPresence及最终真实模型拒绝完成路径。

## 新发现与下一步

SETTINGS-COVERAGE-005全遍历未完成，SETTINGS-COVERAGE-006仍是前序已关闭修复；均未算作本Change成果。真实模型仍可能漏答或提交错误完成锚点，本次分别验证了记账纠正/不授额和既有裁定出口，不承诺任意模型语义正确或固定总步数停机。无阻塞开发范围的Human Decision；最终探针的业务完成锚点仍待既有裁定，不在本Change中代为认可。未提交或推送。

补充：environment-summary.json中的model-pre-barrier是失败基线，曾在仅根页证明下返回Completed但coverage未齐；已由CheckTaskEvidenceBarrier及新增提前完成回归修正，不作为成功验收证据。

## 独立复核后验收

同一个指定GPT-6 Luna Worker顺序完成Spec/Standards审查，发现往返旧状态可重复授额与阈值1无纠正资源；处置与后续复查见review-followup/review.md。删除当前branch成果键；Owner投影稳定事项及其来源、相关稳定成果，Agent引用NextItemKey，Runtime逐事项累计已资助成果。换措辞、旧事实重现或别处新事实均不支持重复资助同事项；未资助的当前事项可有界尝试，同事项自身有新成果可继续。阈值与初始轮次预留两次正常计费咨询，余1轮不新开无法纠正的复核。

| method | expected | actual | evidence（review-followup/） |
|---|---|---|---|
| 六组.NET回归 | 原有行为及本轮续额/纠正反例均成立 | Kernel915、Host334、Agent.Dsh178、Simulation188、Agent29、Host.Dsh33，合计1677/1677 | kernel-final-3.txt、host-final-2.txt、agent-dsh-final.txt、simulation-final.txt、agent-full.txt、host-dsh-full.txt |
| 插件全套与更新事项载荷定向测试 | 新字段通过严格schema且保留顶层关联 | 40/40；更新nextItemKey载荷1/1 | plugin-full.txt、plugin-item-wire.txt |
| 协议/Prompt/部署/场景检查 | 生成物同源且正式3081加载一致 | profile/prompt revision13；两份Prompt哈希通过；部署39文件clean；28场景0违规；13 workspace schemas通过；diff检查通过 | schema-generation-final.txt、prompt-check.txt、deploy-check.txt、scenario-check-final.txt、workspace-schemas.txt、diff-check-final.txt |
| 显式启用RuntimeOverviewLiveTests | 真实进入/返回及待办事项续授，未知保留，导航成果并入scope事项 | 1/1；2次验证导航、2页面2边、1次授额；声明的小范围完成 | device-final.txt、environment-summary.json device及其run目录 |
| 专用3081真实模型小范围探针 | 模型能引用Owner事项并续额，漏答纠正计费，错误完成不放行 | 3次双维有效授额（root:Connected devices、requirement:scroll-discovered-entry、explore:unobserved-scope）；一次漏答后attempt2成功；6次派发；最终AwaitingCompletionAdjudication/completion-anchors-unverified，scroll-discovered-entry未覆盖 | live-model.txt、environment-summary.json model及其consult-bridge/facts/preflight |

模型探针先于最后一处Runner把已验证导航成果并入scope事项的加法映射；该映射由最终device-final.txt现场回归证明。核心逐事项执法、codec、schema和Prompt在模型探针中均已使用本轮版本，不把模型探针夸大成最终全部源码的相同快照。最终源码哈希保存在review-followup/implementation-hashes.json，首轮根目录哈希不覆写。

本轮经注册脚本新建自有emulator-5556（启动时无在线设备），已按对应lease回收；3081原专用实例已重载新工件，3080未操作。产品真实模型为当前绑定deepseek-official/deepseek-flash，开发Worker仍为指定GPT-6 Luna。SETTINGS-COVERAGE-005全遍历继续保持未完成；未提交推送。
