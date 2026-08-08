# Container 与 Traversal

## Requirement

Container 是语义页面范围的局部运行状态域；Traversal 是局部、确定性的执行 Kernel；
二者受 Agent 控制，Container 切换（Navigate / Rebind / Switch）authority 在 Agent。

## Motivation

宪章 §6（Container 回答"这个语义页面范围内如何继续"，Agent 拥有更高语义 Authority）、
§7（Traversal 回答"如何可靠执行这一小步"，Select → Check → Execute → Verify → Branch）、
§56（Spine: Agent → Container → Traversal → Environment）、
I-1（依赖方向）、I-2（单 owner）、I-3（单 authority）、I-8（低层 escalate 不偷权）。

## Scenario

Given Settings Main Container 已绑定（局部状态含当前 Observation，候选元素含 "Network & Internet"）；
When Traversal 执行步骤"点击 Network & Internet 并导航到 Network Settings"；
Then 按 `Select → Check → Execute → Observe → Verify → Branch` 推进，
返回 `TraversalStepResult`（`Succeeded | Failed(原因)`），且步骤状态由 Traversal 唯一持有。
无法推进时（SC-P1-004）以结构化失败结果上报，Agent 是最终 failure authority
（Trap 模型 Phase 2 引入，本阶段不实现 — 裁决 4）。

## SHALL

- SHALL Container 拥有其全部局部可变状态（当前 Observation / candidates / visited / local progress /
  完成判断），是该状态唯一 owner（I-2）。
- SHALL Container 能回答两个问题：当前 Observation 是否仍属于自己（still-mine）、局部执行是否完成。
- SHALL Container 的 Semantic Identity 在切片 1 由显式规则注入（页面名匹配）；Phase 1 Observation
  无 Fingerprint（裁决 2，Fingerprint 字段与机制 DEFER 到 Scroll Identity Scenario），
  I-6 原则「Fingerprint 是 evidence，不是 identity」保留。
- SHALL Traversal 按 `Select → Check → Execute → Observe → Verify → Branch` 协议推进单步。
- SHALL Traversal 拥有单步执行状态（selected candidate / retry / step journal），不承担 Agent 级决策
  （不裁决 Container identity、不决定全局 Plan、不私自 PressBack 猜测恢复，I-8）。
- SHALL 步骤结果以 `TraversalStepResult` 表达：`Succeeded | Failed(FailureReason)`（结构化结果，
  非异常、非静默；§45）。Result 不携带 Expected / Observed 世界快照字段
  （当前 assertion 不需要，裁决 4）；Trap 作为一等模型由 Phase 2（§60-E）
  Failure / Recovery Scenario 引入，本阶段不实现（§21 概念保留在宪章，裁决 4）。
- SHALL Traversal 无法推进时必须返回 `TraversalStepResult.Failed(原因)`（如目标元素在当前
  Observation 无匹配），不得自行判定 Run 失败、不得自行恢复（I-8：lower scope 可 escalate，
  不得 steal higher-scope authority — SC-P1-004）；Run 终止 authority 在 Agent。
- SHALL Container 收到步骤失败结果后不自行恢复、不判定 Run 失败，须将结果上报 Agent
  （I-8 — SC-P1-004；Container 只读转交，不裁决全局目标）。
- SHALL Traversal.Select 的 grounding 仅使用 Text + SwitchState? 证据（裁决 3）；
  同一 Text 多个候选时，`SetSwitch` 目标优先选择携带非 null SwitchState 的元素
  （state-bearing 优先 — SC-P1-005）。不引入 coordinate / hierarchy 模型
  （coordinate-based / hierarchy-based grounding 均 DEFER 到未来场景购买）。
- SHALL Container 切换（Navigate / Rebind / Invalidate / Switch Active Container）由 Agent 决策并执行；
  Container 不得修改 Agent 的全局目标或世界真相（§6）。
- SHALL 切片 1 不使用 FSM 表达 protocol（普通方法即可）；引入 FSM 须满足 §17 条件（I-7）。
