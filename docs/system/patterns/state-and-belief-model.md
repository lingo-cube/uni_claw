# State, Belief, Memory and Plan Model

## 1. Observation

Observation 表示：

> "某一时刻从现实世界采集到的证据。"

例如：

```text
Observation
{
    Screenshot
    Elements
    OCR
    DetectedControls
    ForegroundApp
    PopupSignals
    Fingerprint
    Timestamp
}
```

Observation 是事实证据载体，但它可能：

- 不完整；
- 有识别错误；
- 有延迟；
- 低置信度。

因此：

```text
Observation ≠ Semantic Truth
```

Observation 是 World Belief 的输入。

---

## 2. World Model / World Belief

World Belief 表示：

> Agent 根据当前 Observation、历史、Memory 和语义推断后，对现实形成的当前最佳判断。

例如：

```text
WorldBelief
{
    ForegroundApplication
    SemanticPage
    ActiveContainer
    Confidence
    Evidence
    DriftStatus
}
```

World Belief 必须允许：

```text
Unknown
Uncertain
Conflicting
```

不要强迫系统在证据不足时给出假确定答案。

重要语义判断建议携带：

- Confidence
- Evidence
- Source
- Timestamp / Freshness

新的 Observation 可以修正旧 World Belief。

---

## 3. Runtime State

Runtime State 与 World Belief 必须严格分离。

Runtime State 表示程序为了执行维护的内部状态，例如：

- CurrentTraversalStep
- SelectedNode
- RetryCount
- VisitedCandidate
- LastAction
- ActionJournal
- LocalProgress

World Belief 表示程序当前认为现实是什么。

禁止将二者混入巨大 Context。

---

## 4. Memory

Memory 表示过去积累的知识，例如：

- 某种页面的语义模式；
- 某种 Container Identity；
- 某 App 页面结构经验；
- 元素匹配经验；
- Recovery 成功路径；
- AI 分析结果。

但：

```text
Memory is not truth.
```

Memory 只能提供：

```text
Prior / Advice / Evidence
```

新的 Observation 可以否定 Memory。

高置信度当前证据优先于历史 Memory。

---

## 5. Plan

Plan 表示：

> "为了完成 Goal，目前预计可以采取的执行结构。"

Plan 是：

```text
Executable Hypothesis
/
Execution Prior
```

因此：

```text
Plan ≠ World Model
```

如果世界变化，系统可以：

- Re-ground；
- 修正；
- Re-plan。

禁止因为 Plan 中存在 Node A，就默认现实一定存在 Node A。

---

## 6. Graph

Graph 可以表示：

- Plan；
- Container 内部局部执行结构；
- 已发现导航关系。

但每一种 Graph 必须拥有明确语义。

禁止一个 Graph 同时表示：

```text
Plan
+
Reality
+
History
+
Navigation
+
Execution Stack
```

第一阶段建议：

```text
Local Traversal Graph belongs to Container.
```

不要假设真实 GUI World 是永久稳定的一棵树。
