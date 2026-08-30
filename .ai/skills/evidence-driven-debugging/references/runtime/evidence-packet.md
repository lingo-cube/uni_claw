# Runtime Debug Evidence Packet v0

> Status: `P0_FROZEN`
> Authority: `NONE`
> Machine shape: [runtime-debug-evidence-packet.v0.schema.json](runtime-debug-evidence-packet.v0.schema.json)

Evidence Packet 将一个 Runtime Debug IR 与它引用的现有 evidence 做成可审查、可交接的
manifest。Packet 不复制大 artifact，不补造 missing evidence，也不授权 Runtime repair。

## Required

- `packetVersion`, `packetId`；
- `sourceIdentity`: 至少显式 RunId；缺 capture/trace/receipt/revision/environment 时用
  `MissingEvidence` 表达相应 claim 限制；
- `debugIr`: 完整 Runtime Debug IR v0；
- `evidenceIndex`: IR 中所有 EvidenceRef ID 的唯一解析表；
- `repairGate`: deterministic eligibility 与 blockers；
- `generation`: producer identity、schema digest、deterministic input digest。

## Optional

- `derivedViews`: summarize、occurrence timeline、trace diff、terminal chain 的小体积派生
  projection；
- `notes`: non-authoritative reviewer notes；
- `sourceIdentity` 中不适用或未产生的 capture/span/deployment identity。

Optional 不表示可忽略关键证据。影响 FDP、Owner、repair、cross-run comparison 或 fresh
real confirmation 的缺失必须进入 `MissingEvidence` 并触发 blocker。

## EvidenceRef

EvidenceRef 只索引已有证据：

```text
RefId
Kind
Uri
Selector { RunId, ObservationSeq, OccurrenceId, StableKey, RowId,
           EvidenceRef, SpanId, FrameId, JsonPointer, LineAnchor }
Digest
Integrity
MediaType
Summary
```

`Kind` 只能是 schema closed set：run report、Runtime/Span/Fusion trace、stage artifact、
frame、observation、action history、replay、test result、receipt、decision 或 code symbol。

Rules:

- `Uri` 必须显式；不得扫描、猜 `latest` 或隐式选择最近 run；
- 大体积 frame/stage/trace/replay 保留在原处，Packet 只存 ref/selector/digest；
- 进入 `MINIMAL_REPAIR` 的关键 artifact 必须 `integrity=VERIFIED` 且有 digest；
- 临时路径可作为诊断输入，但未固定 digest/provenance 时不能支撑可复现 repair claim；
- `CODE_SYMBOL` 只能佐证 owner/seam，不能替代 runtime evidence；
- historical report/Decision 只能作为 comparison/context，不能冒充当前 run fact。

## Missing Evidence That Blocks Repair

下列任一情况使 `repairGate.eligible=false`：

- `FirstBad` 未确认或其相邻 LastGood→FirstBad 证据缺段；
- `Owner` 未确认；
- target observation/occurrence ambiguity 会改变 FDP；
- Good/Bad pair 的 model/config/pipeline/runtime revision/environment 轴未知，且差异结论依赖
  这些轴；
- 关键 EvidenceRef 缺失、unverified 或 identity mismatch；
- fresh-real claim 缺 run↔deployment receipt/revision/environment 绑定；
- `Disposition` 不是 `MINIMAL_REPAIR`；
- 需要 architecture/environment gate。

Raw stage 不总是所有诊断的 repair blocker。例如已有 deterministic component input 可以
直接证明 component FDP；此时 raw 可以 `NOT_APPLICABLE`，但必须说明 claim scope 仅到该
component。若声称 raw→terminal end-to-end causal chain，raw 缺失就是 blocker。

## Repair Gate Equations

```text
NO_FDP   → NO_IMPLEMENTATION
NO_OWNER → NO_IMPLEMENTATION
INSUFFICIENT_EVIDENCE → EVIDENCE_COLLECTION only
```

`repairGate.blockers` 是 closed set。Packet generator 只能机械计算 blockers，不能修改
Disposition、选择 owner 或生成 authorization。

## Integrity and Identity

- Packet semantic digest 排除生成时间、展示格式和 host-local absolute prefix；
- all ref IDs unique，所有内部 ref 都必须解析；
- EvidenceRef selector 与 artifact 内容冲突时 fail closed；
- receipt/model/config/pipeline/deployment 或 runtime revision 不匹配时标
  `IDENTITY_MISMATCH`；
- Good/Bad 可跨 identity 比较仅当比较目的显式且差异轴被记录；不得把结果当同部署回归证明。

## No Artifact Duplication

Packet JSON 不内联 screenshot bytes、frame arrays、full trace dumps、stage views、model files
或 replay corpus。小体积 derived summary 可以内联，但必须可由 EvidenceRefs 重新计算，且
不能成为比源 artifact 更高的 authority。
