# Grill 第9轮：能力可用性与handler输入

日期：2026-10-19。状态：D9/D10已接受；Q11/Q12待答，仅文档设计。

## 前提与只读事实

D9/D10确定能力操作引用及计划显式排序。当前没有通用handlers编排协议，能力引用不能绕过Owner或共享不同信任域Registry。

- 语言校验固定ID为runtime.language-inspector，typed接口ILanguageInspector.Inspect；Finding默认非权威，进入Assurance需另立Promotion。见[src/UniClaw.Kernel/Capability/LanguageInspection.cs](../../src/UniClaw.Kernel/Capability/LanguageInspection.cs)第8-29行。
- 当前语言Hook在observation proposals经Kernel.Process后、下一次input前执行，按capture分组；Finding加入诊断记录，不阻断Effect。见[HostRunner.cs](../../src/UniClaw.Host/HostRunner.cs)第212-231行及[LanguageInspectionPostCommit.cs](../../src/UniClaw.Host/Capability/LanguageInspectionPostCommit.cs)第44-87、157-204行。
- 语言runner只消费Host校验后的task-scoped binding，不能任意从Registry选实例。见[LanguageInspectionBinding.cs](../../src/UniClaw.Host/Capability/LanguageInspectionBinding.cs)第43-59、99-127行。
- 点击经Control intent→Bind→Assurance→Effects.Dispatch，EffectBoundary gate后调用IEffectDriver.Deliver；已有Plan Act后有StepVerify屏障。见[UniKernel.cs](../../src/UniClaw.Kernel/UniKernel.cs)第707-767行、[EffectBoundary.cs](../../src/UniClaw.Kernel/Effects/EffectBoundary.cs)第149-218行、[KernelRunDriver.cs](../../src/UniClaw.Kernel/Runtime/KernelRunDriver.cs)第893-950、1059-1131行。
- Registry只暴露注册/解析/生命周期事实，无通用handler/operation执行面。见[CapabilityHub.cs](../../src/UniClaw.Kernel/Capability/CapabilityHub.cs)第47-59、136-176行。以上由复用explorer只读核对，无修改/测试。

## Q11：哪些能力可选，不可用怎么办

A（推荐）：只选本任务已提供且绑定有效的能力/操作，沿既有信任域与Owner缝执行。必需能力在计划采纳前缺失则不启动，运行中失效则暂停并交代原因；仅显式声明为可选的步骤可以跳过并记入报告，不自动替换为别的能力。

B：仍限定本任务能力范围，但任一步骤不可用都结束本次执行，返回未完成报告；不保留执行等待恢复。

这是可用性处置，不赋予计划跨Registry信任域或调用未暴露操作的权限。实际错误码/重试次数/新能力注册不在本题冻结。

## Q12：每步处理哪个对象、哪份界面数据

A（推荐）：每步明确作用于命中节点、当前页或已验证到达的新页；输入由Kernel在该步开始时经Owner读取缝取得，保留观察关联。点击必须经过既有完成验证，后续步骤才读取新页面；原节点引用不能悄悄变成新节点。

B：整个有序列表始终消费匹配时的同一份节点/页面快照；新页处理交给下一次规则匹配。

例子：检查当前页语言→点击导航→Kernel确认到达→检查新页语言。选择A允许在同一处理序列中显式表达这两个页面范围；选择B则新页检查在下一轮匹配表达。语言Finding的后续流程处置另题裁决，不把检查当自动授权/阻断。

Q11与Q12独立：可用性处置不决定输入对象绑定，输入绑定也不决定缺失时暂停还是结束，可以同轮回答。

## 后续依赖

结果消费/检查不符合时的流程处置、多个规则同时匹配的选择及未知、节点记录来源与接收者仍待裁决；不重新打开既有语言检测算法或既定安全权威。

## 答复

Owner未选择Q11/Q12的A/B，而是明确“不同的act绑定不同的handlers，比如检查某一项语言，绑定在元素的操作上”。据此记录D11：绑定单位是具体操作。Q12由该澄清替换，不推定点击后的输入或时机；Q11仍待答。后续见[第10轮](grill-round-10.md)。
