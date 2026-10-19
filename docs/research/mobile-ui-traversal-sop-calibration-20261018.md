# 手机 UI 菜单遍历 SOP 校准与 World Graph 现状

日期：2026-10-18。Research / Authority: NONE。关联RUNTIME-OVERVIEW-001；只做代码核验、既有测试与一手资料比较，未修改产品实现或启动设备遍历。Owner已确认Q6按预期满足情况报告，Q7局部受阻其他继续，并要求检查复用世界模型的图。

## 1. 结论

保留Owner确定的顺序：敲定预期 → 根据预期看结构 → 算待办 → 找路径 → 作决定。执行后要以观察和验证结果闭环到下一overview。

执行手机菜单任务还需补齐：真实导航关系不强制为树；未知结构持续发现；待办包括探索义务；路径逐步验证并允许局部改道；访问事实与验收满足分别判断；相同页面标题不能直接代表同一页面。

用户提到的World Graph已有基础实现及证据约束，但正式Settings realization没有构建跨页面导航图。应沿既有World Model补齐实际买方需要的图事实及投影，不在Coverage/overview里建立平行图真相源。

## 2. 一手资料核验

| 来源 | 观察到的具体做法 | 借鉴与限制 |
|---|---|---|
| [DroidBot源码：UTG](https://raw.githubusercontent.com/honeynet/droidbot/master/droidbot/utg.py)、[探索策略](https://raw.githubusercontent.com/honeynet/droidbot/master/droidbot/input_policy.py) | 维护状态迁移图、事件探索状态、可达状态与导航步骤，策略支持DFS/BFS。 | 图适合表达回跳/多入口及重访；已尝试事件不等于验收成功，图也不是完整现实。其偏好按钮和重启行为不能作为我们的授权依据。 |
| [Stoat作者仓库](https://github.com/tingsu/stoat) | 基于GUI行为模型生成测试，并以覆盖与序列多样性改进探索。 | 支持持续建模与探索的思路；是研究工具，不能把旧平台实验当作当前设备能力证明，也不能把代码覆盖等同目标完成。 |
| [AndroidWorld任务指南](https://github.com/google-research/android_world/blob/main/docs/tasks_guide.md)、[系统任务实际校验](https://raw.githubusercontent.com/google-research/android_world/main/android_world/task_evals/single/system.py) | 任务有初始化与成功检查；例如亮度/Wi-Fi任务读取系统设置判断结果，而非只统计点击。 | 先有任务预期再验证结果。这里只借鉴独立校验思想，不引入其任务所需的修改设置权限或模拟器重置。 |
| [Android UI Automator官方文档](https://developer.android.com/training/testing/other-components/ui-automator) | 使用条件查找与有界等待，支持稳定性等待；文档明确无障碍树稳定不保证应用完全空闲。 | 观察需等待预期条件，稳定画面/成功派发均不足以证明目标完成；此处不建议为本Change迁移工具框架。 |
| [Maestro等待说明](https://docs.maestro.dev/maestro-flows/flow-control-and-logic/wait-commands) | 用可见/不可见断言轮询等待条件，满足则继续，超时则失败。 | 可将等待的预期与失败出口显式化；其自动重试不直接变成我们无限重试或预算依据。 |

这些是不同技术路线的公开实例，不意味着行业统一采用某个图Schema或固定续额百分比。以下属于结合仓库现状的设计判断。

## 3. 从实际执行视角校准五步

### 敲定预期

先说明设备/账号/权限/系统状态与允许操作范围下，要验证什么；全遍历是否只是当前可访问菜单，还是包含切换状态触发的新页面，需要Owner确认。不能用maxDepth或maxEntries配置反向缩小原预期。

### 根据预期看结构

页面可按层级展示，但导航关系应保留多入口、返回、弹窗与条件关系。相同标题不强行合并；滚动视口变化也不自动铸造新页面。未访问入口是候选，不提前制造目标页面的canonical identity。

### 算待办

同时列出已发现入口的验收待办与发现新入口的探索待办：例如未检查的滚动区域、未展开分组、允许且需要切换的标签页。已知队列清空时仍检查探索义务。新增发现可扩大已知分母，不能因此判定倒退。

### 找路径

选择有观察依据且允许执行的下一段路线。历史图中的边是当前相信的关系，不保证本次仍可执行；回访时仍需核对当前观察。受阻不等于永久不可达，记录原因和状态前提，其他分支可继续。如何分配一级广度和局部深度仍需确定。

### 作决定与执行闭环

Agent对照目标预期、近期overview及本笔额度结果判断继续/探索/恢复/重规划/受阻/提交完成。每步都需观察预期结果；回执只证明投递事实。图提供世界结构，验收记录提供任务事实，两者关联后支持判断，不互相替代。

## 4. 本仓已有的图模型

| 检查点 | 当前代码事实 | 判断 |
|---|---|---|
| 权威归属 | 产品基线§12及CONTEXT的ContainerGraph规定World Model拥有图，投影从revision派生。 | 已有明确归属，不需新图组件抢权威。 |
| 存储形状 | `WorldBeliefRevision.cs:83`包含WorldGraph、Containers、Relations及EvidenceBasis。 | 骨架已做；其中WorldGraph目前是subject字符串列表，不能只看名字就当作可寻路的typed导航图。 |
| 图关系 | `ContainerAssociation.cs:25`的关系kind目前是Contains/Overlays，关系带两端身份及EvidenceBasis。 | 可表达部分局部结构；尚无已实现的跨页导航关系。Contains不能冒充“点击可到达”。 |
| Owner执法 | `WorldModel.cs`的Associate核对关系两端已知、supporting evidence有效后才写入revision。 | 存在真正的关系入模执法，不只是文档。 |
| 正式生产者 | `ProductAssociationStrategy.cs`所有分支都返回空Relations。 | 当前正式策略没有产生图边。 |
| 页面身份 | `SettingsTraversalLiveFeed.cs`每轮ui.screen值固定android.settings，另写ui.screen.route。 | 目前Settings各页共享容器，route变化不是页面节点图。不能把route字符串直接视为canonical identity。 |
| 消费端约束 | `KernelRunDriver.cs`DeriveScreenSummary与`UniKernel.cs:481`验证等路径假定Containers总数为1。 | 不能简单把每个route改成新container就宣布接入；多容器锚定/投影选择必须随真实图买方补齐。 |
| 遍历与overview | Settings账本目前用routes、target marks与frontier集合做覆盖选择；未消费导航图。 | 缺少图投影消费与Goal/任务事实关联。 |

核验：现有UIWorldAssociationTests及ProductAssociationStrategyTests的运行日志为`evidence/renewal-strategy-research-20261018/world-graph-existing-tests.log`。这些测试只证明既有图与关联能力；不能证明真实页面导航图已建立。

## 5. 建议补齐的范围（设计输入，未实现）

1. 在既有World Model/UI realization内形成有观察依据的页面/交互上下文身份与导航关系；沿当前关联、reconciliation及evidence gates接入。必要关系kind与身份规则以真实遍历买方确定，不一次预建所有taxonomy。
2. 边的依据是实际前后观察；执行回执/TransitionContext用于关联，不能只凭“准备点击”或“发出点击”写入世界关系。P22仍为non-evidentiary prior。
3. 提供带来源revision、范围及不确定性的只读图投影；overview/遍历消费投影。规划路径是可推翻假设，不回写图为真。
4. 任务的已访问、验收、受阻与剩余义务仍在原Owner侧记录，引用图中身份/观察依据；World Graph不保存任务完成、授权或续额结论。
5. 接入真实页面图时，修正单容器消费假设，明确当前screen/container锚定；保留历史/其他节点不等于本轮所有节点都可供grounding。

不是引入通用图数据库或第二份可变页面图，也不把Coverage账本直接提升为WorldBelief。

## 6. 其他需要纠正的现状假设

- 当前配置nested maxDepth=1、maxEntriesPerPage=1，只反映局部采样限制；不能据此给“全分支完成”结论。
- 当前策略在非根页先选子入口，存在深挖分支而其他一级待办长期未执行的可能。真实全遍历执行92次Effect后一级仍3/7；这不是单独证明策略错误，但说明一级广度/深度如何分配应显式可见。
- `ContextReferenceIndex`有目录信息，但当前prod profile只发布submit_decision。不能假定Agent已有按引用拉取任意历史的工具。先明确Runtime在overview/复核时主动提供哪些材料；若需新增取证缝，另按真实消费者设计。
- 图的visited、探索动作已尝试、验收满足及因策略排除必须分别显示；不能借图覆盖率取代任务验收。

## 7. 带回grill的决定

- Q8（Owner已按推荐确认）：本次范围为当前设备/权限/账号/系统状态下允许访问的菜单，条件不足的入口明确记录；不为枚举条件页面额外切换状态，扩展状态组合需另行声明预期和授权。
- Q9（Owner反馈后修订，已按推荐确认）：入口发现清单与执行顺序分开。撤回先核验一级再深入的推荐；观察即可发现可见入口，不必先逐个点击。全局清单配合局部深入，由Agent结合连续overview中的本段预期与成果、阻碍和下一路径判断是否切换；避免用“还有其他一级项”直接推出必须返回。这是本项目确认的策略方向，不是行业资料证明的唯一最优算法。

图归属与已有单Owner约束是已查明事实，不让Owner重复选择。页面身份、导航边证据、投影消费和恢复验证在语义明确后再收敛协议与实现计划。

## 8. 根页入口已发现、未访问：当前已有部分实现

- `SettingsCoverageLedger.RecordObservation`保存路由与观察到的入口；`DynamicRootCandidates`汇集历史根页观察，无需先点击。`DynamicRootFrontier`与`DynamicRootVisited`分别推导待访问与已验证进入项。
- 现有测试`DynamicRootEntries_ExposeFrontierVisitedAndStructuralSkipReasons`先观察Accessibility但不进入，report已将其列为frontier；验证进入后移到visited。这支持“深度执行也能知道其他入口”的事实前提。
- 配置TargetPages与动态根候选分开记账，动态候选排除configured-target；全局视图需要合并对应任务事实并去重，不能把configured-target作为策略跳过，也不能把未观察的配置种子算已发现。
- `CoverageSnapshot`只提供当前可见项；Runner构造`HeartbeatTraversal`时`Frontier=snapshot.Visible`，不是历史全局待办。内部数据已存在，但其正常咨询消费及结构语义尚未完整接通。
- 已发现入口不等于已观察目标页面、已验证进入不等于该分支预期均满足；根页未查看的滚动区域仍是未知。overview应同时呈现这些事实，不将它们压成一个“完成率”。

核验（2026-10-18）：`dotnet test tests/UniClaw.Host.Tests --no-restore --filter FullyQualifiedName~DynamicRootEntries_ExposeFrontierVisitedAndStructuralSkipReasons --verbosity quiet`，预期观察可产生未访问入口、验证进入后移到visited，实际1/1通过、退出码0。仅证明账本行为；未作设备遍历或正式overview消费验收。测试有NuGet漏洞缓存访问NU1900提示，与此行为断言无关。
