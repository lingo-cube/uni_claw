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
返回语义步骤结果（成功或结构化 Trap），且步骤状态由 Traversal 唯一持有。

## SHALL

- SHALL Container 拥有其全部局部可变状态（当前 Observation / candidates / visited / local progress /
  完成判断），是该状态唯一 owner（I-2）。
- SHALL Container 能回答两个问题：当前 Observation 是否仍属于自己（still-mine）、局部执行是否完成。
- SHALL Container 的 Semantic Identity 在切片 1 由显式规则注入（页面名匹配），不得用 Fingerprint 当 identity（I-6）。
- SHALL Traversal 按 `Select → Check → Execute → Observe → Verify → Branch` 协议推进单步。
- SHALL Traversal 拥有单步执行状态（selected candidate / retry / step journal），不承担 Agent 级决策
  （不裁决 Container identity、不决定全局 Plan、不私自 PressBack 猜测恢复，I-8）。
- SHALL 步骤结果以语义 Result 表达（成功 / 可重试 / Trap），Trap ≠ Exception（§21、§45）。
- SHALL Container 切换（Navigate / Rebind / Invalidate / Switch Active Container）由 Agent 决策并执行；
  Container 不得修改 Agent 的全局目标或世界真相（§6）。
- SHALL 切片 1 不使用 FSM 表达 protocol（普通方法即可）；引入 FSM 须满足 §17 条件（I-7）。
