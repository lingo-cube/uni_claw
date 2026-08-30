# Runtime Debug IR v0

> Status: `P0_FROZEN`
> Authority: `NONE`
> Machine shape: [runtime-debug-ir.v0.schema.json](runtime-debug-ir.v0.schema.json)

Runtime Debug IR 是对已有 Runtime/validation evidence 的只读诊断投影。它不成为
Runtime Fact、WorldBelief、GoalEvidence、action input、repair authorization 或 architecture
authority。

## Construction Order

1. 写 `ExpectedReality` 与 `ObservedReality`，不写 root-cause 猜测；
2. 绑定 `TargetObservation`，再关联 `TargetOccurrence`；
3. 建立 Good/Bad comparison；没有 pair 时显式 `NOT_AVAILABLE`；
4. 按固定七阶段填写 `EvidenceChain`；
5. 从相邻证据阶段确认 `LastGood` 与 `FirstBad`；
6. 由 FirstBad 产生 seam 推导 `Owner`；
7. 填写 missing evidence、confidence 与 closed disposition；
8. 运行 repair gate；不通过时不得提出 implementation WorkItem。

## Required Fields and Explicit Absence

Schema 中所有业务字段都 REQUIRED。无法获得的内容必须用各字段自己的 explicit
state 表示，不能删除字段或写 `null` 规避判断。

- `TerminalState.status`: `OBSERVED | NOT_REACHED | UNAVAILABLE`
- comparison `status`: `AVAILABLE | NOT_AVAILABLE | NOT_APPLICABLE`
- chain-stage `status`: `PRESENT | MISSING | NOT_APPLICABLE`
- divergence `status`: `CONFIRMED | UNRESOLVED | NOT_APPLICABLE`
- occurrence `status`: `CONFIRMED | CANDIDATE | AMBIGUOUS | NOT_APPLICABLE`
- owner `status`: `CONFIRMED | CANDIDATE | UNRESOLVED`
- confidence `level`: `CONFIRMED | HIGH | MEDIUM | LOW | UNASSESSED`

`NOT_APPLICABLE` 必须附理由。不能用它掩盖应存在但未采集的 evidence；后者必须是
`MISSING` 并进入 `MissingEvidence`。

## Evidence Chain

固定顺序与 key：

```text
raw
→ normalized
→ fused
→ canonical
→ semanticAdmission
→ affordance
→ runtimeState
```

每个阶段记录 summary、input/decision/output EvidenceRef IDs。`LastGood` 是仍与
ExpectedReality 一致的最后阶段；`FirstBad` 是第一次产生与目标语义相关的错误
predicate、decision 或 output 的阶段。最终 exception、terminal 或 Unknown 不自动成为
FirstBad。

## Closed GapKind

```text
EVIDENCE_AVAILABILITY_GAP
CONTRACT_REGRESSION
REPRESENTATION_DRIFT
CORRELATION_GAP
COMPOSITION_GAP
DECISION_LOGIC_GAP
NUMERICAL_BOUNDARY_GAP
CAPABILITY_COVERAGE_GAP
BOUNDED_POLICY_GAP
ENVIRONMENT_GAP
TRACE_COVERAGE_GAP
ARCHITECTURE_OWNERSHIP_GAP
UNKNOWN
```

Worker 不得扩展 vocabulary。无法分类时用 `UNKNOWN`，并降低 Confidence 或路由到
evidence/gate；不要把 case 名写成新 GapKind。

## Closed Owner Domains

```text
AGENT
CONTAINER
TRAVERSAL
ENVIRONMENT
DEVICE_ADAPTER
RUNTIME_PERCEPTION
RUNTIME_WORLD
SEMANTIC_CAPABILITY
VISION_FUSION
TEST_HARNESS
VALIDATION_HARNESS
DEPLOYMENT_COMPOSITION
MULTI_OWNER_GATE
UNKNOWN
```

`Owner.domain` 只表示生产 FirstBad decision/output 的 ownership domain，不授予修改权。
`Owner.seam` 可记录当前 symbol/module；没有证据时 status=`UNRESOLVED`、domain=`UNKNOWN`。

## Closed Disposition

```text
EVIDENCE_COLLECTION
MINIMAL_REPAIR
ARCHITECTURE_GATE
ENVIRONMENT_GATE
INSUFFICIENT_EVIDENCE
```

Selection rules:

- `EVIDENCE_COLLECTION`: FDP/Owner 尚可通过已批准的 read-only capture/reader 补齐；
- `MINIMAL_REPAIR`: FDP 与 Owner 均 confirmed，证据完整，且 repair 不跨 architecture gate；
- `ARCHITECTURE_GATE`: contract、authority、owner 或 cross-layer identity 需要 Human 裁决；
- `ENVIRONMENT_GATE`: deployment/device/config/revision identity 阻止有效诊断或 fresh confirmation；
- `INSUFFICIENT_EVIDENCE`: 当前不能形成可证伪 FDP；下一动作只能是
  `EVIDENCE_COLLECTION`，不得 implementation。

## Hard Gates

```text
FirstBad.status != CONFIRMED → NO_IMPLEMENTATION
Owner.status != CONFIRMED    → NO_IMPLEMENTATION
Disposition != MINIMAL_REPAIR → NO_IMPLEMENTATION
MissingEvidence blocks FDP/OWNER/REPAIR → NO_IMPLEMENTATION
```

即使 IR 输出 `MINIMAL_REPAIR`，仍需独立 Leader/Human WorkItem；IR 本身没有授权。

## Determinism

- EvidenceRefs 按 `refId` 排序；MissingEvidence 按 `missingId` 排序；
- comparison axes 按 `name` 排序；
- 同一 immutable input 必须产生相同字段、顺序与 closed vocab；
- generated timestamp、absolute temp root 或 host-local noise 不进入 semantic comparison；
- inference 必须在 `summary`/`basis` 中标明，不能伪装成 observed fact。
