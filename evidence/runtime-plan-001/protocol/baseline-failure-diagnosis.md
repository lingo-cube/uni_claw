# 首次真实Step失败定位（E4）
Expected：在当前允许范围进入Network & internet，独立观察后使用当页Navigate up返回根页；三组共享目标/权限/观察验证。
Observed：真实Kernel有一次已验证进入、两节点一条验证导航边；第二轮Agent上下文Screen指向Internet子页，Elements=[]。同周期hierarchyAvailable=true、693条proposal，XML含Navigate up；不是设备/观察不可用。首轮Step真实样本保留，status=AgentDecisionFailed/no-response，1/2动作验证，不作为有效基线。
First Divergence：ConsultAgentV2调用DeriveElementSummaries()未传anchor，而新helper默认只在单容器时返回元素；World保留历史页后发生多容器，Agent丢失当页元素。GuardStep显式传anchor未发生该缺口。Owner=Kernel咨询只读投影，不是World identity/遍历/权限/完成裁决。
Feedback loop：现有KernelRunDriverNavigationGraphTests.VerifiedEntryAndReturn_AddGraphEdges_WhileGroundingUsesOnlyCurrentPage新增子页Elements断言，从公开Driver复现Collection[]（core/current-elements-red2.log）；默认使用既有CurrentInteractionContainerId后43/43通过（core/current-elements-green.log），仍只过滤当前页。
第二缺口：同会话retry重建pending遗漏decisionId，合法当前ID被拒expected undefined，连续重复工具调用后超时。Owner=DSH通道私有pending生命周期；与候选/遍历/Effect authority分开，Worker以公开route/tool seam反例处理，另见retry-context-fix.md。
根因支持：桥接本地context已Elements=[]，排除传输删字段；同周期真实XML/trace与导航边排除环境失败；确定性两页反例在World occurrence归属正确时仍失败，定位默认投影。仍需原始真实场景重跑，单元GREEN不算现场恢复。
消耗：该失败会话26次实际模型消息/attempt，其中1次中断未报告usage；已报告totalTokens=334050，仅为可得消耗下界，不能写全量token=334050或零费用。原始会话正文不复制到仓库，collector只保存usage与次数；货币账单不可得。
Boundary：不改World/container association/当前元素Owner，不改动作许可或未知出口，不补坐标/固定点击fallback，不改Provider/模型/代理。escalation=none；后续若触及authority或已决选择才升级裁决。

## 后续基线准备样本
Step/2：2/2动作独立验证，Owner CoverageComplete；NoAction附带dispatch:intent-1、dispatch:intent-2，实际这些是intent而非receipt，Kernel正确AwaitingCompletionAdjudication。统一harness提示澄清已满足mandatory时省略completion，不改变Kernel完成执法；后续全部模式共享同一提示/编译产物。
Step/3：第二咨询的原始模型尝试及一次既有retry均返回未闭合的工具参数，provider记MALFORMED_RESPONSE（DeepSeek Messages stream: tool input is invalid JSON），未形成tool/call；1/2动作，Runtime no-response。没有放宽JSON/补齐语义、增加重试次数、换模型或代理。该样本属于模型输出失败，保留无usage的实际attempt并标记token下界。
