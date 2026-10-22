# 组件离线回放交付（SIM-014，前置 SIM-015）

已接入真实 Semantic Perception 和 World 两个组件的公开调用链；本轮闭环完成，真实 Host/模型整装接通仍属后续工作。

原来的 SimulationHost 主要证明整装 Kernel 路径，既有 Perception 替换测试只到注册/健康检查；缺少真实语义执行的同消费者回放，也无法如实标注没有 Agent 的组件测试。此次保留真组件，替换其外部供给，并把失败、恢复、数据增长和构成变更验证落入同一回归入口。

看代码先打开 `tests/UniClaw.Simulation.Tests/ComponentSemanticReplayTests.cs` 与 `ComponentWorldReplayTests.cs`：前者经 Product Registry 取真实 UniPerceptionCapability，在公开 SemanticPerceptionExecutor 上切换脚本/冻结响应，再经真实 projector 与 Ledger；后者在真实 Ledger/World 上切换两种同源布局供应。共享 `ComponentReplay/RecordedComponentInputs.cs`、`CaptureMismatchFault.cs`、`ReplayCompositionGuard.cs` 只抽取两个 buyer 实际重复的机制，未新增产品协议、Capability、Owner 或仿真开关。

错捕获引用进入感知时，在调用供给前被拒绝；恢复正确引用后可执行，重复不再调用。World 收到错捕获身份时保持无主，匹配身份后才分配容器，矛盾身份再次变成未知；旧布局晚到不能替换当前页。感知还覆盖不可用、意图冲突、响应错配、取消及供给晚到；取消后明确等待被释放的供给结束再检查结果。两条新场景关联的全部失败面也带 Scenario trait。

| 验证 | 实际结果 | 证据 |
|---|---|---|
| 真实组件公开闭包 | 感知14、World8，全部通过 | simulation-full.trx、component-discovery.json、actual/component-*.json |
| 全量模块回归 | 8工程2101/2101；Simulation314 | full.txt、module-results.json |
| 场景真值链 | 37/37；20 certified、17 descriptive；原35条不改 | full.txt、protection-check.json |
| 五类构成门 | 8例通过；关闭执法时5类变更均被测试发现 | guard-red/green.trx、ComponentReplayGovernanceTests |
| 故障/独立预期敏感性 | 禁用共享故障4/4失败；错误oracle2/2失败 | shared-fault-disabled.trx、wrong-independent-oracle.trx |
| 数据增长 | 初始SIMs后，仅加All apps、Default apps数据行即被发现；产品/Runner无需修改 | suite-before-growth.json、data-only-growth.json、component-green.trx |
| 严格Agent未参与标注 | 同一19例（3正16反）由Schema/Python/C#消费；Python工具10/10；旧整装锚点保留 | ../sim-015/、simulation-full.trx |
| 来源与构成 | 3组2026-10-20原XML及冻结导入12文件核对通过；70 pins；7份Kernel DLL一致，22份组件输出同一程序集 | source-provenance.json、runtime-artifact-check.json |
| 独立评审/保护 | 无遗留问题；2217保护项中2213未变，4项为授权元数据修改；执行源码无漂移 | review-*.md、review-disposition.json、protection-check.json |

新增场景只声明本地组件测试结果，不声明 Agent 决策或 Goal 达成。SIM-015 由用户批准的 C7 局部修订约束 testScope=component、component-test、kind=none、零咨询、双N/A、空Goal/分类和无Agent组件清单。SIM-007 既有的专有载体 Goal N/A 继续有效；decision N/A 的新例外仅适用于组件隔离。

构成门在创建组件前核对产品契约/实现、adapter、回放输入、oracle 的内容。版本号不变而内容变化也会拒绝；更新必须搭乘致因Change，测试没有自动接受新hash或回写oracle路径。它证明70个明确来源的一致性，不是全依赖解析或语义兼容证明。接口、提取依据、协议/组件变更验证清单见 `changes/SIM-014/component-contracts.md`。

首次full只失败在新架构文档的冻结状态标记，已按Owner裁决补齐CLOSED并保留 attempt1-*；最终full所有组通过，没有改产品代码或预期来消除失败。Standards首次把SIM-007既有Goal N/A误报为越权，独立复核canonical修订与共享正例后撤销，处置证据保留。

后续需接真实 Host 离线输入/输出入口，再分别验证真实识别、真实Provider与模型、原完整Run逐决策重放及设备补采；大模型生成目前仅有候选流程设计，未声称已运行生成器。最近一周录制只选取三组代表样本，未宣称整周穷尽。原录制布局是真实来源，身份、语义回复、目录、意图和回放时钟是受控输入，不能据此认证原模型行为或精确开关关闭状态。本轮未启设备、真实模型、提交或推送。

复现：使用 `evidence/sim-014/make-snapshot.py` 创建隔离副本，先还原依赖，再执行 `tools/verify-change SIM-014 --scope full`；工具专项为 `python3 -m unittest discover -s tools/tests`，公开协议生成检查为 `python3 tools/gen-protocol-machine.py --check`。固定输入、manifest、oracle与当前源码缺一不可，不自动重新签认；最终副本及精确来源hash见 final-snapshot.json。
