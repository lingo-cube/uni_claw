# Scenario 04 — Uncertain Action

## Situation

执行 Click。

设备实际上已经完成页面跳转。

但 Action transport 返回：

```text
Timeout
```

## Required Behavior

系统不能直接再次 Click。

正确流程：

```text
Action result uncertain
→ Observe
→ discover target world already reached
→ mark action effectively successful
→ continue
```

## Purpose

该 Scenario 锁定：

- Action transport result 不等于 World result；
- 非幂等动作不能盲目 retry；
- Action 后 Observation 是权威验证来源。
