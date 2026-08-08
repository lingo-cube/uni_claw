# Container Runtime Layer

## 1. 定义

Container 是 UniClaw 的核心 Runtime Concept。

正式定义：

> 一个"语义页面范围内"的局部运行状态域。

Container 不是：

- UI Control
- Screenshot
- Traversal Node
- Task
- Frame
- FSM
- App
- 单纯页面 DTO

---

## 2. Semantic Page Boundary

例如 Android Settings Main。

顶部观察：

```text
Settings Main
WiFi
Bluetooth
Apps
```

向下 Scroll 后：

```text
Settings Main
Accessibility
Security
About
```

视觉内容发生巨大变化，但语义页面仍然是：

```text
Settings Main
```

因此仍属于同一个 Container。

而：

```text
Settings Main
→ Network & Internet
```

通常意味着进入新的 Semantic Page，因此进入新的 Container。

---

## 3. Container 负责

Container 负责：

- Semantic Identity；
- 当前 Observation；
- 当前页面局部状态；
- Local Progress；
- 当前可见元素；
- visited / failed / scroll 等局部 bookkeeping；
- Local Traversal Graph；
- Local Grounding；
- Traversal Runtime；
- 页面范围内的局部恢复；
- 判断当前 Container 是否完成；
- 判断当前 Observation 是否仍可能属于自己。

Container 回答：

> "在当前这个语义页面范围内，我应该如何继续完成局部执行？"

---

## 4. Local Belief 与 Agent Authority

Container 的判断属于 Local Belief。

Agent 拥有更高 Semantic Authority。

Agent 可以：

- Rebind Container
- Invalidate Container
- Correct Container Identity
- Switch Active Container

Container 不得反过来修改 Agent 的：

- Global Goal
- Task-global Plan truth
- World-level semantic authority

---

## 5. Local Traversal Graph

第一阶段建议：

```text
Local Traversal Graph belongs to Container.
```

不要试图把整个 App / Device 世界建成永久 UI Tree。

Container 之间第一阶段可以首先使用：

```text
Active Container Stack
```

只有真实 Requirement 证明有价值后，再考虑：

```text
Container Navigation Graph
```

---

## 6. Dynamic Grounding

静态 Plan 与当前 Observation 之间需要 Grounding。

例如：

```text
LocalPlan:
Find "WiFi"

Current Observation:
[
  "Internet",
  "WiFi",
  "Bluetooth"
]
```

Grounding：

```text
Plan Requirement
+
Observation
+
Rules
+
Memory
→
Grounded Candidate
```

Dynamic Match 的本质应理解为 Grounding，而不是永久生成世界事实。

Grounding Result 必须能够根据新的 Observation 重新计算。

---

## 7. Semantic Identity、Snapshot 与 Fingerprint

必须严格区分：

### Semantic Identity

"这是哪个语义页面 / Container？"

### Snapshot

"当前时刻看到了什么？"

### Fingerprint

"当前 Observation 是否发生明显变化？"

因此：

```text
FingerprintChanged
≠ ContainerChanged

FingerprintChanged
≠ NavigationOccurred

FingerprintChanged
≠ ShouldPressBack
```

Fingerprint 只能作为廉价 Observation Evidence。

禁止将它作为强页面 Identity。

---

## 8. Container Recovery Scope

Container Scope 表示：

问题仍然处于当前语义页面范围。

例如：

- Popup；
- 页面局部结构改变；
- Scroll 状态异常；
- 当前 Candidate 消失；
- 需要重新 Ground。

这些问题由 Container 负责局部恢复。

如果 Container 无法证明恢复成功，应向 Agent Scope Escalate。
