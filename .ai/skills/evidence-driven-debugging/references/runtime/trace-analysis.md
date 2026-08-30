# Runtime Trace and Occurrence Analysis v0

> Status: `P0_FROZEN`
> Authority: `NONE`

本 contract 关联现有 Agent TraceEvent、Activity/TraceRun/Span、Fusion causal trace、stage
evidence、frames 与 run report。它不合并 Trace model，不新增 event/span，不写回 Runtime。

## Trace Boundary

```text
TRACE != DEBUG IR
TRACE != DEBUGGER
TRACE != CONTROL
TRACE != AUTHORITY
```

Trace 是 evidence source。Debug IR 是其与 frame/stage/runtime evidence 的只读诊断投影。
缺失的 trace coverage 写入 `MissingEvidence`；不得由工具或 worker 合成“应该发生”的 event。

## Occurrence Correlation

按以下范围逐层收窄：

```text
RunId
→ ObservationSeq
→ OccurrenceId
→ StableKey
→ RowId
→ EvidenceRef
→ SpanId
```

Rules:

1. `RunId + ObservationSeq + OccurrenceId` 在其声明作用域内是最强显式 occurrence key；
2. `StableKey`、`RowId` 跨 observation 只产生 identity candidate；
3. `EvidenceRef`/explicit lineage 可证明同一 source artifact 或 derivation；
4. `SpanId` 关联执行边界，不自动证明某个 UI occurrence identity；
5. text、bounds、array index 只能 corroborate/reject candidate；单独使用一律不确认 identity；
6. `StableKey != SameOccurrence proof`；相同 key 可表示同一物理行的不同 rendering，未来帧
   也不能 retroactively 改写过去 occurrence；
7. 发生 key collision、one-to-many 或 contradictory provenance 时返回 `AMBIGUOUS`。

Correlation result 必须输出 `status`, `candidate keys`, `proof`, `counterevidence` 和 refs。
没有显式 lineage 或可审查组合证据时，不得把 `CANDIDATE` 升为 `CONFIRMED`。

## Terminal Causal Chain

从 terminal 向前投影：

```text
TerminalState
← last Runtime decision/state
← affordance/admission result
← canonical/normalized/fused observation
← raw/frame source
```

投影只返回已有因果/时间/identity refs。链断裂时保留断点并输出
`INSUFFICIENT_TRACE_COVERAGE`，不得用代码阅读填补为 runtime fact。Terminal chain 用于搜索
FDP，不等于 FDP。

## FirstBad Confirmation

`FirstBad.status=CONFIRMED` 需要：

- 之前存在 evidence-backed LastGood；
- current stage 的 input/decision/output 可观察；
- divergence 对目标语义有影响，不只是格式或 byte noise；
- refs 可定位同一 run/observation/occurrence 或明确 differential pair；
- alternative earlier divergence 已被 evidence 排除或写入 MissingEvidence。

任何一项缺失时保持 `UNRESOLVED`，路由 evidence collection。
