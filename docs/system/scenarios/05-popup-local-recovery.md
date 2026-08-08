# Scenario 05 — Popup Local Recovery

## Situation

当前 Container：

```text
Network Settings
```

突然出现系统 Popup。

Popup 不改变底层页面语义。

## Required Behavior

```text
Container-level Trap
→ Local Recovery
→ dismiss popup
→ Observe
→ verify Container still valid
→ continue
```

## Forbidden Behavior

不得无条件升级为 Agent Recovery。

## Purpose

该 Scenario 锁定：

- Container Scope Recovery；
- Local semantic continuity；
- Recovery 必须重新 Observe + Verify。
