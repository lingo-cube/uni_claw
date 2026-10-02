# AGT-009 Evidence — Drive 级弹窗清障闭环（Leader，合并后）

> worktree：../uni_claw-agt009（d4e71e7b）。等级：SCENARIO（端到端确定性场景，
> 贯穿 SettingsCoverageRunner → SettingsCoverageDirector → KernelRunDriver
> PlanExpand → SimulatedEffectDriver 真实 dispatch 链）。

## 验收 7：弹窗出现 → obstacle 分支 → 清障验证 → 回到原遍历

- method：`dotnet test tests/UniClaw.Host.Tests --filter FullyQualifiedName~PopupEpisode`
  （`PopupEpisode_ObstaclePlanClearsPopup_AndResumesTraversalToCompletion`）。
  场景：遍历进入首个二级页时 `com.android.permissioncontroller` 弹窗出现
  （窗口根 package ≠ hostPackage + alertTitle/parentPanel/button1/button2，
  与 e1 实录结构同形）；周期 batch 携带同分类器 `ui.overlay.popup` 声明。
- expected：
  1. Director 在 popup=present 时发出 obstacle directive（词汇优先序命中
     CANCEL），模型以受约束 advisory Plan 回答（恰一 ActItem=CANCEL tap +
     ObserveItem(popup)），无 deviation；
  2. ActItem 经真实 grounding → Effect Gate → dispatch（回执 DeliveryCompleted）
     落地清障；弹窗消失后同分类器声明转 absent；
  3. 清障后普通遍历恢复（后续仍有 enter/scroll/back 指令），覆盖任务完成
     （CoverageComplete、UncoveredItems 空）。
- actual：PASS（131/131 Host 套件含本项）。obstacle consult 恰一次
  （DirectiveKind=obstacle、DecisionKind=plan、Justification=
  obstacle-clearance-plan）；CANCEL 步骤有回执；run Completed；
  world.PopupActive=false。
- 诚实边界（留痕）：CANCEL tap 的通用 post-action-target-unique check 如实
  失败（click 型目标消失但路由未变——既有 Assurance 规则不允许无路由变化的
  目标消失）；弹窗清障本身由 `ui.overlay.popup` present→absent 的 typed 声明
  链证明（§1 「同一分类器前后测」），剩余计划（ObserveItem）按冻结决策 11
  由该验证失败废弃。放宽该 Assurance 规则或新增 Kernel 级 obstacle check
  需要显式 change 裁决（不在 AGT-009 允许修改面内）。
- evidence：tests/UniClaw.Host.Tests/SettingsCoverageScenarioTests.cs
  （SimulatedSettingsWorld popup episode + ObstaclePlanModelConsult + 测试）。
