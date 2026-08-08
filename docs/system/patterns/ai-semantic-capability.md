# AI Semantic Capability

## 1. AI 的定位

AI 是可插拔能力。

不是 Runtime 核心流程的唯一实现。

优先路径：

```text
Fast Vision
+
Deterministic Rules
+
Memory
```

用于高频运行。

---

## 2. LLM / VLM 适用场景

LLM / VLM 更适合：

- Startup 首次语义识别；
- Unknown Page；
- Container Identity 低置信度；
- Grounding 无法可靠完成；
- Recovery 需要复杂判断；
- Plan 修复；
- 新页面语义学习。

不要：

```text
Every Step → LLM
```

系统必须允许：

```text
AI unavailable
```

此时核心确定性 Runtime 仍能工作到合理程度。

---

## 3. AI 输出不是事实

AI 输出不能直接成为 World Truth。

正确关系：

```text
AI Output
→ Semantic Evidence
→ Agent Decision
→ World Belief
```

---

## 4. 异步语义能力

对于不阻塞当前安全执行的判断，可以允许：

```text
Background Semantic Analysis
```

例如：

Fast path：

> 当前大概率仍属于当前 Container，可以继续安全观察。

后台 VLM 对页面进行更深分析。

返回后：

- 与当前 Belief 一致 → update Memory；
- 与当前 Belief 冲突 → emit reconciliation signal。

异步结果必须携带 Observation Identity / Timestamp。

禁止旧 Observation 的 AI 结果覆盖更新的 World State。
