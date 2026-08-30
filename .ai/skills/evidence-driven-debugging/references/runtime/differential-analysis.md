# Runtime Differential Analysis v0

> Status: `P0_FROZEN`
> Authority: `NONE`

存在可比较的 Good/Bad pair 时，优先使用 differential workflow；不得从 terminal symptom
直接推断 FDP。

## Workflow

```text
Good
vs
Bad
→ comparison identity and controlled axes
→ unchanged evidence
→ changed evidence
→ first semantically relevant divergence
→ LAST_GOOD / FIRST_BAD
→ GapKind / Owner candidate
```

## Pair Admission

记录每个 axis：scenario、device/environment、locale、model、config、pipeline、deployment
receipt、runtime revision、capture format、observation stage。每个 axis 标为：

```text
CONTROLLED | INTENTIONALLY_CHANGED | UNKNOWN
```

关键 axis `UNKNOWN` 且可能解释差异时，不得产生 repair disposition；返回
`ENVIRONMENT_GATE` 或 `EVIDENCE_COLLECTION`。

Good/Bad 可以是：

- 同 run 相邻 observation；
- 同 capture 的不同 occurrence；
- pre/post deterministic replay；
- 明确 identity 的不同 real runs。

历史 contract/code 只能作为期望对照，不能单独成为 Good runtime observation。

## Diff Layers

每层分别输出 `unchanged`, `changed`, `missing`：

```text
source identity
raw
normalized
fused
canonical
semantic admission
affordance
runtime state
terminal
```

先找到最后一个语义仍相同的层，再找第一个改变目标 predicate/decision/output 的层。
第一个 JSON key、array position、float bit 或 log text 差异不是自动 FDP。

## Fail-Closed Results

- pair identity mismatch → `ENVIRONMENT_GATE`；
- occurrence ambiguous → `EVIDENCE_COLLECTION`；
- changed stage exists but predicate missing → `INSUFFICIENT_EVIDENCE`；
- only terminal differs with no causal bridge → `INSUFFICIENT_EVIDENCE`；
- multiple plausible first divergences → keep `FirstBad=UNRESOLVED`, list alternatives in
  `MissingEvidence`/notes, no implementation.

## Replay Boundary

Replay may confirm a predicate and minimize a falsifier. It does not prove current real-device
state, deployment identity or fresh confirmation. Preserve source EvidenceRefs, extraction
rules and counterexamples. Automatic extraction/minimization is P2 and not authorized here.
