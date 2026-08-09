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

在有明确 Scenario Receipt 时，PlanStep 可以携带一个不可变的 evidence criterion，描述某项预期外部效果应如何由 fresh Observation 重新验证。criterion 仍然只是 hypothesis；只有对当前 Observation 的求值结果才是 evidence。

SC-P3-CAND-005 的 bounded branch-effect criterion 使用三值结果：

```text
true  = fresh evidence positively proves the effect holds
false = fresh evidence positively proves the effect does not hold
null  = current evidence cannot determine the effect
```

该 criterion 必须 deterministic、side-effect-free 且只读取传入 Observation。它不能读取或修改 Runtime owner，也不能把 Plan presence 变成世界事实或 Goal completion。

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

## 5.1 Bounded Candidate Authorization Evidence

对于 SC-P3-CAND-006 的 one-Observation bounded candidate set：

```text
observed candidate != authorized candidate != executed action
```

`CandidateAuthorizationEvidence` 是 Agent 对当前 Goal intent 与 supplied fresh Observation 中一个 candidate 的即时语义判断：

```text
true  = authorized
false = positively rejected
null  = current evidence cannot authorize
```

每个结果必须携带非空 Reason。可选的 Goal evaluator 必须 deterministic、side-effect-free，只读取传入的 Observation 与其中的 candidate；它不创建持久 authorization state，也不能把 authorization 解释成 dispatch、world effect、required work 或 Goal completion。最终完成仍只来自 satisfied GoalEvidence。

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
