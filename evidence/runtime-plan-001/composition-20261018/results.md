# 可组合控制增量：本轮结果

当前保持VERIFY。Kernel已能执行有限条件、重复和嵌套候选策略；公开回归与不同结构场景通过。真实模型已生成并执行一份按新页面事实继续的8动作策略，但固定批次的生成稳定性和全部预注册收益门槛未通过，不能宣布ADR-0053实施整体验收完成。

## 做了什么、原来的问题

原首片以单层ForEach、根页候选和检查点为主，无法充分表达原D1-D5要求的“新页面仍由已有策略处理”。这次仍用同一KernelRunDriver，加If/Repeat有限块组合、父块帧、候选继承/遮蔽/恢复、ControlPath准确续行及条件证据审计；没有新增Run调度器。Kernel不包含菜单名、UI路由或记录业务判断。

## 这次修改与调用链

正式DSH Plan/ContinuePlan → KernelRunDriver的块/候选位置 → RunDriverInputs的原Owner条件/候选投影 → 当前动作guard → Control/grounding/Assurance/Effect → 新观察与独立验证。Unknown条件不走Else，不为Effect授权；暂停及在途屏障、预算和旧版本证据继续保留。

打开IDE主要看AgentDecision.cs、PlanExecution.cs、RunDriverInputs.cs、KernelRunDriver.cs、SettingsPlanFacts.cs和SettingsCoverageLedger.cs。正式codec/Schema/Prompt双副本同步，Profile18、schema11eb6875…、prompt b6602c2c…；不同结构记录买方只替换Owner投影，仍走相同Driver。设计取UI页面/控件/进入返回的事实边界，以及“控制位置＋运行数据＋条件转移”的计算理解；没有以图灵完备、无限循环或动态任意代码为目标。

真实设备另发现并修复Host两处事实投影错误：工具栏页标题被首菜单行替代；每页候选上限被每次观察重置。两者都有公开缝的实际RED→GREEN。RouteKey仍为title/source/up元组，新增真实collapsing_toolbar来源；原Security & privacy语料之前把Settings列表行误当页标题，已在当前测试明确更正，历史Change保留。

## 验证结果

| method | expected | actual | evidence |
|---|---|---|---|
| 公开Driver组合/暂停/Unknown/不同结构记录买方 | 新事实选分支；准确恢复；无欠证Effect；同一核心 | Kernel944/944；记录买方原型与条件变体均通过 | kernel-final4.txt；KernelRunDriverPlanTests.cs |
| UI仿真与Owner投影 | 不预列子页；进入/返回；开关只记录；范围一致 | 场景6个独立verified receipt、2咨询；深度/重复观察上限及真实页标题回归通过；Host346/346 | ui-scenario/summary.json；route-red/green2.txt；frontier-limit-red.txt；host-route-final6.txt |
| 正式通道与相关回归 | 合法组合可传输；非法结构拒绝；生成物同步 | Agent29/29、Agent.Dsh185/185、Host.Dsh34/34、Simulation188/188、Node44/44 | agent-final2.txt；agent-dsh-final185.txt；host-dsh-common-goal8.txt；simulation-route-final7.txt；node-final3.txt |
| 同模型同条件真实对照 | 先基线/固定门槛，再两任务×两Plan模式各2次；失败全部入账 | 最终4Step有效，8Plan中正式有效5、实际范围完成6；已知组合组全收益门槛通过，另外三组未通过 | common-goal-epoch/thresholds.json、live-result.json、live-metrics.json |
| 实际模型/出口/计量审计 | 当前Flash、逐调用正usage、正式计时、实际服务直连见证 | 最终12会话109调用、3,127,567报告token；全部usage正数；12/12服务PID→DeepSeek443见证；无慢感知调用 | common-goal-epoch/formal-audit.json、transport.json；各trial/socket-witness.json |

默认不启用的live测试不作为设备证据。费用未取得provider账单，不推算货币节省；模型会话时间不是纯网络耗时。场景认证只刷新20份执行fixture的来源元数据，28份检查无违反，不改场景期望。

最终数值门槛在任何新Plan之前于2026-10-18T16:02:44.646908Z冻结。已知路径基线平均3咨询/6实际模型/84,198token/21.80秒；嵌套基线平均9咨询/19模型/688,337.5token/77.18秒。每组各2个固定样本，没有结果触发替补。

| 最终组 | 正式有效 | 咨询/模型调用 | 报告token | 正式时间 | 判定 |
|---|---:|---:|---:|---:|---|
| 已知短Plan | 2/2 | 3 / 6.5 | 100,009.5 | 27.68秒 | 四项收益未过 |
| 已知组合Plan | 2/2 | 1 / 2.5 | 24,018.5 | 18.48秒 | 全部门槛通过 |
| 嵌套短Plan | 1/2 | 15 / 32 | 1,199,453 | 188.19秒 | 包含失败成本后未过 |
| 嵌套组合Plan | 0/2 | 无正式有效完成均值 | 无正式有效完成均值 | 无正式有效完成均值 | 形态/功能门未过 |

前三行均按全部固定尝试成本/正式有效完成数计算。原live-result.json在valid=0时为避免除零使用max(valid,1)，该组values因此只是两次尝试总量（4咨询/9模型/134,987token/71.06秒），不得读成有效完成平均或性能通过。原始结果不覆盖。

## 新发现：能运行的策略与不稳定的生成

嵌套组合第1份初始提案虽有If+Repeat，但实际body把Network & internet与Connected devices写成同一页连续动作；进入第一项后，第二项当前不存在，guard正确拒绝。后续提案还被decision-schema-invalid拒绝，最终TerminalNotProven，只验证1个动作。不能由模型justification所述的正确策略替代实际程序。

第2份真实完成，但生成的是嵌套ForEach+If，没有Repeat；预注册强制If+Repeat，故modeConformant=false，仍记正式失败，不能事后放宽。它也确实证明另一种合法组合可工作：根候选逐项进入，当前页候选发现Internet/Pair new device，进入后逐层返回；这两个子入口名字不在初始Plan字面目标中。8个独立receipt、8次条件求值、2咨询、4模型、71,445token、51.02秒，Owner CoverageComplete；没有点击switch。完整提案及逐步证据在comparison/nested-scope/ComposedPlan/composition-r18-commongoal-2/。

因此：该样本是实际能力证据，不是本批正式PASS，也不能据单样本宣称泛化策略稳定。后续语义验收应允许不同合法组合，避免把指定语法当成用户目标；规则须在新试验前确定，当前数据不重标为通过。

两份此前基线不足的epoch全部保留：首批5Step、60调用、1,808,383token；页身份修复后批5Step、43调用、822,216token。均未启动Plan或冻结数字门槛。后者发现原实验只有Composed指导明确范围，现已将同一scopedObjective实际提供给所有模式，再独立重建最终基线。三批合计22尝试、212实际模型调用、5,758,166报告token；不混用不同产物/指令的收益分母。

## 下一步与工作区状态

优先收敛“提案实际控制流符合目标、按当前页事实继续、终局选择”的策略生成，并在下一批之前把验收定义为目标行为与证据，允许If/ForEach/Repeat的不同合法组合。当前失败没有证明需要增加Kernel操作或第二执行器。保留VERIFY，不追样本或改当前门槛。

设备lease已按注册停止；专用3081保留Profile18/Flash，3080未动，家目录代理/凭据未改。入口快照、44文件相关增量和双轴独立评审可复核；不清理共享dirty修改、不提交推送。Owner关于UI理解与计算模型的要求已按明确“记住”指令保存为独立记忆更新笔记。
