# Scenario 02 — Agent Recovery

## Goal

继续执行：

```text
Enable WiFi
```

## Drift

执行到 Network Container 后，外部环境意外变为：

```text
Launcher
```

此时：

```text
Expected:
Network Settings

Observed:
Launcher
```

## Required Behavior

系统必须：

```text
Detect mismatch
→ emit Agent-scope Trap
→ Agent Recovery
→ restore RecoveryAnchor
→ verify Settings Main
→ recover expected execution position
→ rebind / reconstruct Network Container
→ continue
→ Enable WiFi
→ Completed
```

## Forbidden Behavior

不能：

- 直接从任务头重新执行全部流程；
- Traversal 私自 PressBack 猜测恢复；
- 仅因为一次恢复动作返回成功就假设恢复成功。

## Purpose

该 Scenario 锁定：

- Agent Scope Authority；
- RecoveryAnchor；
- Recovery verification；
- Runtime progress recovery；
- Trap escalation。
