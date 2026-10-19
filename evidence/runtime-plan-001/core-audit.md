# RUNTIME-PLAN-001 核心审计

日期：2026-10-18  
范围：只读审计 `KernelRunDriver` 的线性 Plan 展开、逐项执行、预算复核与恢复缝；未运行构建、测试或设备。ADR-0053 与 D1-D10 视为已冻结，本记录只给出实现落点与可证伪建议。

## 现状调用链

`AgentDecision.Plan` 在 `KernelRunDriver.Drive` 的 `NeedDecision` 分支先调用 `ValidateDecision`；Plan 分支委托 `ValidatePlanProposal` 检查非空、至多 16 项、closed union、Act 字段/effect class/desired state，然后用 ActItem 数量对 `CurrentBudget(view).StepsRemaining` 做整份预算预检（`KernelRunDriver.cs:1902-1912,2202-2235`）。采纳时仅将 `Items` 与 index=0 放入 Driver 的 ephemeral 字段（`KernelRunDriver.cs:771-779`；字段声明 `:268-281`）。公共形状在 `AgentDecision.cs:215-251`，目前只有 `ActItem`、`ObserveItem`、`ControlItem`，proposal 只有 Items/Justification。

`PlanExpand` 每次读取当前 index：Act 先过世界冲突检查，再复核剩余步预算，之后物化为 `_planStep` 并跳入既有 `StepAct`（`KernelRunDriver.cs:1132-1170`）。`StepAct` 经 Control intent、当前 grounding、Assurance/Effect gate 后最多 dispatch 一次；收到 receipt 后转 `StepVerify`，必须拉取 post-action evidence 并验证成功才把 `(consultCounter, planItemIndex, receiptId)` 写入 `CompletedSteps`，再递增 index 回 `PlanExpand`（`KernelRunDriver.cs:996-1016`）。现有动作链已具备所需的串行屏障，动态 Plan 应继续物化单步进入此链，不新增绕过 Kernel 的执行路径。

Plan 失效、耗尽或显式控制最终经 `ExitPlan` 清空 `_planItems`、index 和 `_planStep` 并回 `NeedDecision`（`KernelRunDriver.cs:2146-2167`）。预算续额审查也在 `NeedDecision`：只在决策边界生成 review、消费 review response，并通过 `ProcessRenewal` 记账增额；当前预算是初始额度+grant-已消耗（`:366-400,461-553,1255-1260,1351-1371`）。因此现状支持“让出后重新咨询/重规划”，不支持“同一 Plan 明确续行”：现有续额通过本身不会保存或恢复已清空的 Plan 版本与位置。

## 当前缺口与建议落点

1. **静态列表不能表达新发现项。** `ValidatePlanProposal` 全量遍历有限 `Items`，`PlanExpand` 只按 list index 前进，没有候选源/规则或迭代边界。最小扩展可在 `PlanItem` closed union 增加 `ForEachItem(Source, Body, MaxIterations)`，只容纳一层、Body 只容纳 Act/Observe；展开器把每个候选映射为现有一次性 item 并推进显式局部游标，保留整体 `MaxPlanItems`/最大操作数边界。候选集合投影作为 `RunDriverInputs` 的只读 owner seam，提供 stable scope、revision、候选 key/role/descriptor、Ready/Blocked/Unknown 与 evidence refs。候选只是待评估事实，不是执行许可；实际 Act 仍走 `PlanExpand` 的冲突/预算检查及 `StepAct` 的当前 grounding、Effect gate 和验证。

2. **缺少逐候选、逐动作的事实 guard。** 现有通用门只在 Act 物化前查冲突和预算（`:1150-1176`），之后 grounding 负责现实目标可执行性；它不能表达业务规则的前提、允许绕过的已证阻碍或 Unknown。建议在候选选择和 Act 物化前调用一个独立的只读 guard 投影/评估缝，按当前 source revision 与当前世界重算。Blocked 只能在有明确 owner evidence 且规则声明可绕过时记为受阻并继续；Unknown、冲突、过期 revision 进入受阻/补证/Agent 边界，不得当作 false 或可跳过。此逻辑不应进入 Kernel 的 Settings 特判；SettingsActionPolicy 是 Host 侧既有 guard 可参考的买方/复用位置（`SettingsActionPolicy.cs:188-347`），但核心只消费通用输入输出。

3. **超预算只有整份拒绝，无检查点/续行协议。** 采纳时 Act 数量若高于当前预算即拒绝（`:1909-1911`）；运行中单步预算不足时 `ExitPlan` 把位置一并丢弃（`:1166-1176,2154-2167`）。在 Plan 中加入 `CheckpointItem`，只在动作已完成验证、候选处理结果已记账的合法边界暂停；review/grant 后需要显式 `ContinuePlan` 引用原 `PlanId+Version+位置` 才恢复。仅有增额、普通 Act 或 NoAction 不可隐式续跑。新版本替换未执行部分须走同一决策边界并明确结束旧版本。

4. **Attempt 与受阻证据不连续。** `CompletedSteps` 仅记录成功验证项（`KernelRunDriver.cs:166,996-1000`），失败会清掉 Plan，再次咨询只带 pending failure reason/index（`:2139-2165`）；没有稳定 Plan id/version、候选键、受阻 disposition、原位、单次 attempt/receipt 状态的统一历史。建议每次实际 attempt 先分配唯一 attempt id；将候选证据、guard 结果、dispatch receipt（若有）、验证结果、消费/阻塞处置追加到 run history。receipt 只证明 dispatch 回执，不等于验证完成；处理中断续跑要继续同 attempt 的结果接收/对账，禁止重复 dispatch。

## 最小公共组合与测试缝

- 沿用 `AgentDecision.Plan` 作为提案入口、`PlanItem` closed union 表达 ForEach/Checkpoint、`RunDriverInputs` 作为 owner 只读候选和 guard seam；避免每个业务操作新增 Capability，也不把业务来源、场景名或 Settings 概念写入 Kernel。
- 沿用 `ActItem → AgentActionStep → StepAct → StepVerify` 执行链，保留当前逐项预算、grounding、Assurance、Effect gate、receipt、post-action evidence 与 verify；候选集合 revision 用于确定候选快照/变更判断，不可替代当前动作 guard。
- 在 `KernelRunDriverPlanTests.ComposePlanWorld` 的组合夹具（`KernelRunDriverPlanTests.cs:148-178`）扩充可变 owner 投影及 guard stub；无需新建平行 Driver。现有测试已覆盖多 Act 串行屏障与等待续驱（`:189-248`）、形状和超预算 fail-closed（`:250-361`）、验证失败废弃余项（`:404-447`）、冲突失效（`:450` 起）、Replan 全局预算（`:503-526`）及 Observe 零 Effect。它们是动态切片的复用基线，不代表动态行为已存在。
- 最小新增场景应证明：初始候选 A/B，A 验证后出现 C 且同一规则仍覆盖；B 的 owner-backed Blocked 可留证后继续，Unknown/冲突不跳过；每个候选实际动作都重新 guard；Checkpoint 只在前项已对账后让出；增额单独不产生动作，精确 ContinuePlan 引用恢复原位置且不重发已验证项；新 Plan version 替换剩余内容；Plan history 能按 attempt 关联受阻、receipt 和 verification。测试断言观察到的 Driver/owner 调用、receipts、completed anchors、续行位置与 history，不做字段存在性测试。

## 核心结论

核心无需重写执行器：将静态 cursor 从 list index 泛化为有限、可审计的组合游标，并在既有单步执行前接入 owner 事实投影/guard，即可复用当前安全动作链。主要实现冲突是 `ExitPlan` 与 `NeedDecision` 的无条件 Plan 清理；续行必须作为显式决策形状保存 Plan identity/version/位置并经当前事实重校验，不能仅在预算 ledger 增额后恢复内存列表。现有预算 review ledger、overview progress projection 与 renewal ledger 可复用其消费/证据习惯，但其 `NextItemKey`（`:473-488,530-545`）不等同于 Plan cursor，不能充当续行授权或恢复位置。
