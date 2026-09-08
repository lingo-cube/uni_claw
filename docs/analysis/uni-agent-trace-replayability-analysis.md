# uni-agent Trace → Target Replayability Evidence

> DocumentType: `LEGACY_TRACE_REPLAYABILITY_ANALYSIS`
>
> Status: `CANDIDATE / NOT_ADOPTED`
>
> Authority: `NONE`
>
> Date: `2026-09-09`
>
> Scope: `uni-agent` 分支 Trace / Span / Event / Observability 真实实现代码 →
> UniClaw Target（uni-harness）未来 Trace / Replay / Simulation 迁移的证据整理
>
> Forbidden Boundary: 本文是 Legacy Evidence / Architecture Reference，不是
> uni-harness current architecture，不是已迁移 runtime capability，不构成任何
> 协议或实现承诺。uni-agent 提供 Evidence，不提供未来 Authority。所有结论
> 来自 `git show uni-agent:<path>` 的真实代码，不以旧报告为证据。

---

## 1. 概念处置表

| Legacy concept（真实类型） | Useful semantic | Problem / coupling | Evidence（file:symbol） | Target disposition |
|---|---|---|---|---|
| `RuntimeObservability`（static ActivitySource） | 有界 seam：layer/component 词表、no-throw、不缓存 | 全局静态；parent 经 ambient `Activity.Current`；tag 在 stop 时才可读 | `src/UniClaw.Runtime/Observability/RuntimeObservability.cs` | **Adapt**（去 ambient/静态；词表思想保留） |
| `RuntimeTraceRecorder` → `TraceRun` | per-run 捕获、freeze 成 immutable run；外来 trace 跳过+计数 | ctor 取 wall-clock epoch；`Stopwatch` 单点换算；进程级 AddActivityListener 互斥 | `src/UniClaw.Runtime.Harness/RuntimeTraceRecorder.cs` | **Adapt**（run-scoped claim 思想；时间源需 canonical clock 裁决） |
| `TraceSpan` | span 生命周期 correlation（SpanId/ParentSpanId 链） | 属性是 `string?` 袋；event 被 clamp 进 span 区间（失真 by design） | `src/UniClaw.Runtime.Harness/TraceRun.cs` | **Adapt**（parent 链保留；string-bag 收紧为 typed refs） |
| `ObservabilityEvent` / `RuntimeEventEnvelope` | 事件可携带 `EvidenceRefs`（逻辑引用而非内容） | EventId/Sequence 由 store 捏造；Reason 字符串前缀解析决策（`"viewport exploration "`） | `src/UniClaw.Runtime.DriverHost/Model/RuntimeEventEnvelope.cs` | **Adapt**（refs-not-content 保留；禁止字符串考古） |
| `EvidenceRef` / `EvidenceResolution` | 逻辑 locator（内容寻址 hash、`capture:<session>:record:<order>`，"never a path"） | 仅 capture-bundle 一种 backing；stringly locator | `src/UniClaw.Runtime.DriverHost/Model/EvidenceRef.cs` | **Adopt idea**（引用不复制；locator 形式重设计） |
| `EvidenceCatalog` | 双向索引（ByObservationSequence / ByActionId）——fact↔evidence 可关联的直接先例 | 只索引 capture records，不覆盖 span/fact | `src/UniClaw.Runtime.DriverHost/Projection/EvidenceCatalog.cs` | **Adopt idea**（正是 R-UW-02 需要的 index 形态） |
| `RuntimeEventStore` | append-only、one-writer、幂等 Append、cursor | 内存 Dictionary+lock 全局可变；`ReplaceRunEvents` 破坏纯 append（by declaration） | `src/UniClaw.Runtime.DriverHost/Store/RuntimeEventStore.cs` | **Reject**（重写历史的 carve-out 与 R-UW-06 冲突） |
| `TraceSpanReadModel` + closed vocabularies | 只读投影 + 封闭词表 + cursor fingerprint | 词表在 DriverHost 与 Runtime 双份硬编码 | `src/UniClaw.Runtime.DriverHost/Model/TraceSpanReadModel.cs` | **Defer**（read model 属未来 Trace change） |
| `TraceCaptureSession` / `CaptureRecord` | env-boundary journal、内容寻址 artifact、显式 CaptureState FSM | `CaptureRecord.Observation` **内嵌 domain 对象副本**（truth 复制） | `src/UniClaw.Runtime.Harness/Capture/TraceCaptureSession.cs` | **Adapt**（journal/FSM 保留；内嵌副本必须改引用） |
| `RunSnapshot` / `SnapshotField<T>` | 每字段分类 `DirectPublicProjection / DerivedReadModel / NotCurrentlyAvailable` + TruthSource | derived 摘要从 Agent.Trace 字符串再抽取 | `src/UniClaw.Runtime.DriverHost/Model/RunSnapshot.cs` | **Adopt idea**（truth 分类 = ADR-0011 精神） |
| `FileTraceCaptureStore` | 原子分段持久化 + checksums + refuse-overwrite | — | `src/UniClaw.Runtime.Harness/Capture/FileTraceCaptureStore.cs` | **Defer**（属未来 Trace change） |

## 2. 关键问题的证据回答

1. **Trace 能否引用 domain facts 而非 string log？** 两层设计：Span 层只有
   string attributes（纯 log）；Event 层可携带 `EvidenceRefs` + `EvidenceCatalog`
   索引（引用 facts）。→ Target 应吸收"事件引用 canonical artifacts"这一层，
   弃 string-bag span。
2. **Span 是否具备稳定 lifecycle correlation？** 是（W3C TraceId 认领 run、
   ParentSpanId 链、CorrelationId 复用）；最弱处：event/span id 为 Activity id
   或 store 捏造（`evt-{run}-{n}`），非稳定语义身份。
3. **Fact/Evidence 双向索引思路？** `ByObservationSequence` / `ByActionId` /
   locator 索引已存在；无 fact-id→span 索引。
4. **Legacy implementation accidents：** string 前缀决策解析、event clamp 进
   span 区间、wall-clock↔monotonic 单点换算、`artifact-0001` 位置 id、双份
   词表、`ReplaceRunEvents`、ambient parenting。
5. **值得吸收：** EvidenceRef 逻辑 locator；A/B/C source 分类（C 类永不发射）；
   SnapshotField truth 分类；store-assigned Sequence 仅排序 + 稳定 EventId 去重；
   cursor fingerprint；capture/runtime outcome 分离；原子分段持久化。
6. **违反 Owner/Authority baseline 的设计（须拒绝/重构）：** `CaptureRecord`
   内嵌 domain 对象副本（truth duplication）；`ReplaceRunEvents` 重写 run 流；
   payload 携带决策内容而非引用；`TraceRun.RunId` 与 AgentStateSnapshot.RunId
   双身份静默优先。→ Target 原则：**Trace may reference truth; Trace does not
   become truth.** 每个事实恰一个 owner，Trace 只持引用。

## 3. 与 UIW-001 Replayability Invariants 的对位

| R-UW | Legacy 佐证 |
|---|---|
| R-UW-01 | EvidenceRef/EvidenceCatalog 证明"引用而非内容"可索引 canonical facts |
| R-UW-02 | RuntimeEventEnvelope（EvidenceRefs + CorrelationId + causation）= causality 载体先例 |
| R-UW-03 | SnapshotField truth 分类 = decision 可解释性的投影纪律 |
| R-UW-05 | EvidenceRefs 与事件 payload 分离 = prior/evidence 不压平的先例 |
| R-UW-06 | TraceRun freeze + FileStore refuse-overwrite = immutability for replay |
| R-UW-07 | wall-clock ctor / ambient Activity.Current / 全局 store = 反例清单（target 禁止） |

## 4. 候选后续 change（本轮不 Apply）

```text
TRC-xxx — Target Trace / Lifecycle Reference Baseline
```

触发条件（须真实 buyer 出现）：Replay/diagnostics 的 target consumer 落地；
或 Observation Control / Traversal 需要 lifecycle index。届时按
grill → ADR → change 流程裁决（含 canonical clock Deferred ⑪ 的连带裁决）。
