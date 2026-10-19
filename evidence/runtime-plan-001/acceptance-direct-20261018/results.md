# ADR-0053 直连条件下实施验收

2026-10-18。结论：**功能通过，整体性能验收未全部通过，RUNTIME-PLAN-001保持VERIFY，不关闭。**

## 做了什么

按已接受ADR-0053、实施计划和Owner要求，复核可组合Plan的动态候选、局部受阻、明确续行、逐动作验证、正式协议及不同结构买方；在Owner修改NO_PROXY后，用正式Flash和真实Settings重新建立Step、短Plan、规则Plan对照。没有修改产品行为或Prompt，没有更换为Pro，没有提交或推送。

## 原来的问题与本轮改动

旧功能实现已经过独立审查，但旧代理/Pro对照仍有性能门槛未过。Owner修改环境文件后，原3081进程尚未加载新快照。本轮仅重载已拥有的uniclaw-product专线，并给验收元数据增加transport摘要，防止代理与直连混算；没有用网络配置变化代替模型或设备验收。

正式调用链仍为DSH决策→AgentDecision.Plan/ContinuePlan→KernelRunDriver→当前动作guard→Control/grounding/Assurance/Effect→独立观察验证。每个真实动作继续检查许可、当前目标与结果，Plan不是事实或动作授权。

## 验证结果

| method | expected | actual | evidence |
|---|---|---|---|
| 当前实现身份与功能回归复核 | 当前产品与已审查产物相同，才能复用旧动态/安全/仿真证据 | Kernel/Host/Provider产物一致，既有产品实现无漂移，本次代码变化仅live test计量元数据（state与验收记录另行更新）；Host.Dsh新跑34/34；Kernel937、Host341、Agent29、Agent.Dsh184、Simulation188、Node44、Provider209的通过记录按身份复用 | [身份复核](reused-evidence-identity.json)、[回归日志索引](regression-evidence.json)、[独立复审](independent-review.md) |
| 动态A/B/C、Owner受阻、原位ContinuePlan与不同结构记录核验 | 同一核心处理新C、保留B原因、不重发已验证动作；不同买方不靠Settings特判 | 已有公开Driver及记录买方证据保持适用，未新增核心分支；不把受控输入或仿真称为第二真实设备 | [动态贯穿](../core/dynamic-green-attempt11.log)、[38项Plan回归](../core/compact-plan-focused.log)、[记录买方](../core/record-buyer.md) |
| 正式模型与真实Settings | 目标/权限/观察验证不变；实际模型提出符合模式的Plan；每步独立归属 | 10次Plan全部Completed、Owner CoverageComplete、模式与2/4动作独立验证通过；16个trial均为Flash，91次调用用量完整 | [模型与预检核对](formal-trial-audit.json)、[固定运行结果](plan-run-results.json) |
| 直连与条件隔离 | 真实专线加载Owner配置，网络条件进入同组指纹；不得混用旧性能 | 新PID85912，transport hash及环境文件hash逐trial校验；实际服务连接DeepSeek的443端口，并与正式模型turn匹配7个正向见证 | [transport](transport.json)、[运行时见证](runtime-direct-proof.json)、[socket记录](deepseek-runtime-sockets.jsonl) |
| 严格先冻结后运行 | 基线/公式/数值在任何Plan前固定，不追加追PASS | known3有效+1失败、候选2有效；固定10Plan全部执行；无late lock，全部Plan启动时间晚于冻结 | [预注册过程](procedure.md)、[冻结门槛](../comparison/thresholds-flash-direct.json)、[脚本预检失败记录](threshold-script-precheck.json) |

计时、token及调用均采用预注册的“全部尝试消耗÷有效完成数”。已知Step的一次模型非法JSON失败未删除，其4次调用、44447 tokens和时延进入基线分子。

| 范围 | 模式 | 有效/尝试 | 平均咨询 | 平均实际模型调用 | 平均token | 平均正式时延(s) | 全部Plan门槛 |
|---|---|---:|---:|---:|---:|---:|---|
| 已知2动作路径 | Step | 3/4 | 3.67 | 7.33 | 96345 | 26.60 | 基线 |
| 已知2动作路径 | 短Plan | 3/3 | 2 | 4.33 | 53831 | 23.26 | 通过 |
| 已知2动作路径 | 规则Plan | 3/3 | 2.33 | 5 | 64655 | 20.86 | **咨询数未过** |
| 当前2候选/4动作 | Step | 2/2 | 5 | 10 | 189770 | 36.22 | 基线 |
| 当前2候选/4动作 | 短Plan | 2/2 | 3 | 6 | 98449 | 30.97 | 通过 |
| 当前2候选/4动作 | 规则Plan | 2/2 | 2 | 4.5 | 59722 | 29.55 | 通过 |

已知路径调用门槛仍按最小有效Step减一计算，要求平均咨询≤2、实际调用≤5；没有因失败抬高调用门槛。规则Plan的2.33虽然低于成功Step的3次，仍未达到预设的整次咨询降幅，不能改口径判PASS。

时延基线26.60秒包含失败摊销；只看三次成功Step，正式时延均值21.91秒。因此短Plan的23.26秒仅通过预注册的运营摊销门槛，不能宣称每次已知短路径均提速。原始值、各门槛及全部失败成本见[机器可复算结果](../comparison/comparison-result-flash-direct.json)。

## 新发现与未过项

唯一未过的组级子门槛来自known RulePlan/8：第一份Plan完成两次动作后在末尾checkpoint暂停；第二次咨询时位置已是itemIndex=2、items.Count=2、目标覆盖已完成，模型仍提交continuePlan恢复空尾；第三次才提交NoAction。其他两次同组运行仅用2次咨询。

```text
Plan（进入、返回、checkpoint）→ 已完成检查点咨询
→ ContinuePlan（空尾）→ 再次咨询 → NoAction
```

Runtime保留了明确续行和独立完成核验，未重复dispatch，也未把Plan耗尽当成任务完成。此结果指向模型在已满足检查点上的冗余决策，不能归因于代理残留；[原始决策](../comparison/known-path/RulePlan/8/model-decisions.json)与[投影审计](known-path-decisions-audit.json)均保留。

## 边界与下一步

- 本批次为16次尝试、15个有效完成、91次模型调用、1340375 tokens，当前批次usage完整；Slow为0。无账单金额，不报告货币节省。
- 全部历史仍为65次尝试、55有效、422模型调用、已报告token下界9240999；旧13次缺usage调用保持未知，不回填零。旧代理/Pro收益没有进入本轮门槛。
- socket证据是采样正向见证，不是逐请求抓包；known Step/12窗口未取得独立socket快照。该trial仍通过相同PID/env/transport指纹约束，不能声称逐包证明了所有请求。
- 结果仅覆盖已知2动作及当前2候选/4动作；不证明全Settings遍历、现场新增C、第二真实设备、统计显著或代理导致此前延迟。
- 本轮已停止注册emulator-5556；新3081专线保持Flash和Owner直连配置，未触碰3080或其它设备。

下一步整改应针对“目标已满足、位置在Plan末尾时仍请求ContinuePlan”的冗余决策，保持既有完成核验和权限边界；若修改Prompt或行为，需以新产物建立新对照。本次仅验收，不实施该整改、不放宽门槛、不继续追加运行。
