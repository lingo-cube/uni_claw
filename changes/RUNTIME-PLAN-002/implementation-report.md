# RUNTIME-PLAN-002 实施交付

## 做了什么

将已接受的D1-D19落实为持续执行的`traverse`：Agent提交一次Plan，Kernel内部动态发现、选择规则、进入/返回、执行操作所属的有序handlers；运行中可查询，受阻时保留原位置和消耗，结束后给出限定范围的执行报告。当前范围全部验收完成，Change已关闭；没有剩余阻塞决定。

## 原来的问题

旧Plan的固定步骤/ForEach不足以独立表达动态图探索及每个操作的前后检查。把“页面来过”当成任务已完成会漏掉共享页变化后的检查；把进度留在Control、把Finding不符合当调用失败、或者返回失败仍报旧位置，都会混淆事实、执行记录和控制职责。

## 这次修改

调用链：`AgentDecision.Plan → KernelRunDriver → ControlLoop.TraversalControl → 原StepAct/grounding/Assurance/Effect → 新Evidence/World → 独立Verify → AfterVerified → RunModel记录`。

| 打开IDE看哪里 | 行为 |
|---|---|
| `src/UniClaw.Kernel/Control/TraversalControl.cs` | 只保留探索栈、首匹配规则、导航义务/最佳深度去重和暂停游标；共享页每次实际到达处理本页当前检查，较浅到达重开展开机会 |
| `src/UniClaw.Kernel/Runtime/KernelRunDriver.cs` | 编排typed投影与Language Inspection@1.0，所有导航/返回重用原Effect及验证链；缺必需适配器启动零投递，运行失效/超时暂停；预算耗尽拒绝原位Continue |
| `src/UniClaw.Kernel/Run/RunModel.cs`、`Runtime/TraversalExecution.cs` | Run直接持有操作、handler、gap、完成工作及历史；查询不推进；未知剩余为null，暂停无EndReason，完成不推出Goal满足 |
| `src/UniClaw.Host/Capability/TraversalLanguageInspection.cs`、`SettingsCoverage/SettingsTraversalProjection.cs` | 通过任务绑定的注册Inspector检查当前原目标可见文字；目标过期、跨页或仅资源ID保持Unknown；普通clickable不自动授予导航许可 |
| `src/UniClaw.Agent.Dsh/DecisionJsonConverters.cs`、`ProtocolSchema.cs` | 正式producer/codec/consumer支持追加union，拒绝错型/未知kind/重复标识/非法绑定/版本；Schema、TS、Prompt revision19、profile pin及DSH镜像同步 |

没有增加Handler Registry或任意Execute，没有把语言判据、恢复策略、Goal裁决移入遍历器。旧001与ADR-0053保留原证据和状态。

## 验证结果

当前最终C#：Kernel 951/951、Simulation 216/216、Agent.Dsh 198/198、Host 349/349、Agent 29/29、Host.Dsh（排除Live）33/33，共1776/1776。新增遍历/handler Simulation 28项包含在216中，不重复加总。最终生成资产下DSH Node 45/45，退出码0。

关键行为证据：深路径先到C再浅到C仍能到D，真实轨迹`A,C,Back,Back,C,D,Back,Back`；等深两路到同一C取到`unchecked → checked`两次状态；缺失或矛盾动作后验挡住AfterVerified与下一Effect；祖先环与意外返回均停在真实位置；只读操作零Effect；必需/可选绑定、领域Unknown/Violation、异常/null/超时、暂停查询、Continue/Recovery/预算/取消与终态重入均有可执行分支。

历史28条身份、期望、execution载体保持；20条golden-bundle按当前Kernel源码重新认证且对应真实执行测试通过。8条kind=none没有成为本次库执行通过。逐项映射见`evidence/runtime-plan-002/implementation-20261019/carrier-execution.json`。

独立Standards与Spec三轮评审：最终各0项新增问题；前轮发现已修并复核。16项闭包/保护检查通过，45个冻结增量文件与最终评审快照一致。原始失败与修复回归日志保留，详见同目录`review-*/results.md`、`worker-logs/`及最终测试日志。

## 新发现与实际边界

评审推动修复Run记录Owner、祖先环位置和预算Continue；实际仿真另发现并修复返回位置不同步。Leader核对还修复共享页去重漏检查和缺typed适配器仍可能先导航的问题。

以上是确定性SimulationHost执行证据：Kernel/Run/World/Control/Assurance/Registry为真实模块，Agent、页面观察解释和机械Effect是明示替身；正常语言检查用注册的LanguageFormatInspector，异常与恢复的typed Finding为显式模拟。没有新增真机或真实模型验收，也不宣称数学完备性、全Settings穷尽或Goal完成。当前Settings live投影仍不能证明发现充分，必须报告部分完成；跨进程恢复仍在范围外。

## 下一步

本Change范围内无剩余实施项。四元组、状态和生成INDEX已同步。实施完成时工作保留在共享checkout，未提交、推送或合并；2026-10-20 Owner已进一步授权提交并推送，发布范围及核验见publication.md。入口恢复快照保持可用。后续若增加能力或真实环境验收，应沿相应Owner与明确的新验收范围继续。
