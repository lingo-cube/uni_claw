# Grill 第11轮：检查结果处置与规则命中

日期：2026-10-19。D12-D14已按Owner接受记录。仅文档设计。

## 已定前提

每个具体操作有自己的handlers，操作前/完成并验证后分别有序；只读操作不要求交互。任务有效能力才能引用，必需能力缺失/失效不得静默跳过。语言Finding仍是诊断，既有安全/Control/Assurance/Effect权威不变。

## 现有hook事实（复用explorer只读核对，无修改/测试）

- 已有动作前受限Host回调GuardStep，约束执行但不授予Effect权威；没有CapabilityId/operation/Registry binding。见[RunDriverInputs.cs](../../src/UniClaw.Kernel/Runtime/RunDriverInputs.cs)第79-84、121-122行、[KernelRunDriver.cs](../../src/UniClaw.Kernel/Runtime/KernelRunDriver.cs)第893-936行、[SettingsCoverageRunner.cs](../../src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs)第250-294行。
- 已有语言专用Host post-commit hook InspectAfterCommit，只消费已绑定inspector与observation batch，不是通用操作hook。见[LanguageInspectionPostCommit.cs](../../src/UniClaw.Host/Capability/LanguageInspectionPostCommit.cs)第9-14、44-75行。
- StepAct/StepVerify是内部DrivePhase，不是可绑定扩展协议；验证后结果写内部plan attempt。Settings Host的_flushVerifiedSteps也是私有记账Action。见[KernelRunDriver.cs](../../src/UniClaw.Kernel/Runtime/KernelRunDriver.cs)第112-132、1116-1168行及[SettingsCoverageRunner.cs](../../src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs)第634-641行。
- 当前没有正式定义并注册的BeforeAct/AfterVerified或通用operation-hook协议。禁止在UniKernel堆任意callback/hook集合，见[能力集成协议](../../docs/capability-hub/customization-integration-development-protocol-v0.1.md)第383-389行。

因此复用既有执行边界不等于已有计划绑定契约；后续需经显式typed缝对接，不另建万能hook总线。

## Q15：检查发现不符合时怎样影响执行

A（推荐）：计划区分“记录并继续”与“必须满足才继续”。前者记录Finding继续；后者在不符合/Unknown时暂停并交代原因，不把Finding当作安全授权或Assurance裁决。未声明阻断条件的语言检查默认记录并继续。能力自身不可用仍按D12，不混同业务检查不符合。
B：所有检查只记录，检查不符合永不影响当前操作流程；需要暂停交给后续Agent修订。

具体继续条件须由能力声明的typed结果/显式计划契约表达，不开放任意脚本；若消费需要新Promotion/Runtime Contract，仍须实现既有Owner缝，不由Inspector直接阻断Effect。

## Q16：多个节点规则同时匹配

A（推荐）：规则显式排序，首个确定匹配项选定该节点的处理操作及其handlers；不隐式合并多组交互。前面的匹配为Unknown且可能改变选择时，保留未知并按既定未知处置，不能当作false滑过去。多个检查可以明确放在选中操作的handlers。安全检查始终独立逐动作执法，不受规则首项匹配豁免。
B：执行所有匹配规则的操作，各组按顺序串行；点击换页后重新判断剩余规则是否仍适用。

两题独立：Finding消费不决定多规则组合，多规则组合不决定检查是否阻断。节点记录投影沿Run记录/证据Owner，不新增事实权威；字段级绑定、typed适配、完整场景证明仍在本轮之外。

## 答复

Owner回复“按推荐”，Q15=A（D15）、Q16=A（D16）。当前范围语义分支已逐项回应，收敛到[完整流程复核稿](flow-contract.md)，等待整体共同理解确认；字段/typed绑定/场景证明不隐式扩入本轮，无实现授权。
