# Scenario 01 — Normal WiFi Execution

## Goal

```text
Enable WiFi
```

## Fake World

### Screen 1 — Settings Main

元素：

```text
Network & Internet
```

动作：

```text
Click Network
```

### Screen 2 — Network Settings

元素：

```text
WiFi
```

动作：

```text
Click WiFi
```

### Screen 3 — WiFi Settings

元素：

```text
WiFi Switch = OFF
```

动作：

```text
Enable
```

### Screen 3' — WiFi Settings

元素：

```text
WiFi Switch = ON
```

## Expected Lifecycle

```text
Run Initialize
→ Startup
→ RecoveryAnchor established
→ Bind Settings Container
→ Traverse
→ Navigate
→ Bind Network Container
→ Traverse
→ Bind WiFi Container
→ Execute
→ Verify
→ Goal Completed
→ Run Completed
```

## Completion Rule

Completion 必须来自：

```text
Observed WiFi Switch = ON
```

不能来自：

- all nodes visited；
- graph exhausted；
- no children remaining。

## Purpose

该 Scenario 用于验证：

- Startup；
- RecoveryAnchor；
- Agent / Container / Traversal / Environment 控制权；
- Action 后重新 Observe；
- Container semantic transition；
- Goal Evidence completion。
