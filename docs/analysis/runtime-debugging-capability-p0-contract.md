# Runtime Debugging Capability P0 Contract

> DocumentType: `NON_NORMATIVE_DEBUGGING_WORK_CONTRACT`
> Status: `P0_CONTRACT_FROZEN_FOR_WORKER_OUTPUT / IMPLEMENTATION_NOT_AUTHORIZED`
> Date: 2026-08-30
> Gate: `PROJECT_LEADER_RUNTIME_DEBUGGING_CAPABILITY_P0_CONTRACT_GATE`
> Authority: `NONE`
> Scope: offline Runtime evidence projection into Debug IR and Evidence Packet
> AuthorityDelta: `NONE`
> ArchitectureDelta: `NONE`
> RuntimeBehaviorDelta: `NONE`

本文冻结 P0 的最小工作契约，使 worker 能稳定地产出：

```text
Runtime Evidence → Debug IR → FDP → Owner → Disposition
```

它不是 Runtime Architecture Contract、OpenSpec Spec、production wire/API、Trace
model、debugger service、CLI implementation、正式 Roadmap 或 repair authorization。

## 1. Frozen Outcome

P0 冻结以下内容：

- Runtime Debug IR v0 的字段、closed vocabularies 与 repair gates；
- Evidence Packet v0 的引用、完整性与大 artifact 边界；
- Good/Bad differential 与 occurrence correlation 规则；
- 现有 `evidence-driven-debugging` Skill 的 Runtime 扩展路由；
- 五个真实案例的表达能力样例；
- 五个候选只读 tooling interface 的 contract。

P0 没有冻结 storage/retention service、wire transport、CLI implementation、自动
owner authority、自动 repair、new trace coverage 或任何 Runtime behavior。

## 2. Canonical Contract Files

| Contract | Canonical file |
|---|---|
| Runtime Debug IR semantics | [Debug IR contract](../../.ai/skills/evidence-driven-debugging/references/runtime/debug-ir-schema.md) |
| Runtime Debug IR machine schema | [runtime-debug-ir.v0.schema.json](../../.ai/skills/evidence-driven-debugging/references/runtime/runtime-debug-ir.v0.schema.json) |
| Evidence Packet semantics | [Evidence Packet contract](../../.ai/skills/evidence-driven-debugging/references/runtime/evidence-packet.md) |
| Evidence Packet machine schema | [runtime-debug-evidence-packet.v0.schema.json](../../.ai/skills/evidence-driven-debugging/references/runtime/runtime-debug-evidence-packet.v0.schema.json) |
| Trace and occurrence correlation | [Trace Analysis contract](../../.ai/skills/evidence-driven-debugging/references/runtime/trace-analysis.md) |
| Good/Bad analysis | [Differential Analysis contract](../../.ai/skills/evidence-driven-debugging/references/runtime/differential-analysis.md) |
| Tooling surface | [Tooling contract](../../.ai/skills/evidence-driven-debugging/references/runtime/tooling-contract.md) |
| Acceptance corpus | [Five case mappings](../../.ai/skills/evidence-driven-debugging/references/runtime/acceptance-examples.md) |

Machine schemas constrain shape. Markdown contracts constrain semantics. If they appear
to conflict, fail closed and request a P0 contract correction; a worker must not invent a
field, enum value, identity rule or disposition.

## 3. Runtime Debug IR v0

Every field below is present in every IR. `UNRESOLVED`, `MISSING`,
`NOT_AVAILABLE` and `NOT_APPLICABLE` are explicit values, not permission to omit a field.

| Field | Required | Meaning |
|---|---:|---|
| `ExpectedReality` | yes | Human-readable expected world/system outcome, without implementation guesses |
| `ObservedReality` | yes | Evidence-backed facts only |
| `TerminalState` | yes | Observed terminal, not reached, or unavailable; terminal is not automatically FDP |
| `TargetObservation` | yes | Explicit RunId + ObservationSeq scope or an unresolved marker |
| `TargetOccurrence` | yes | Correlation result and proof; `StableKey` alone is never identity proof |
| `GoodComparison` | yes | Good side or explicit `NOT_AVAILABLE` |
| `BadComparison` | yes | Bad side or explicit `NOT_AVAILABLE` |
| `EvidenceChain` | yes | `raw → normalized → fused → canonical → semantic admission → affordance → runtime state`; every stage present as `PRESENT`, `MISSING`, or justified `NOT_APPLICABLE` |
| `LastGood` | yes | Last evidence-backed stage still aligned with expected reality |
| `FirstBad` | yes | First semantically relevant evidence-backed divergence or `UNRESOLVED` |
| `GapKind` | yes | Closed diagnostic class from the schema; `UNKNOWN` is explicit |
| `Owner` | yes | Seam producing the first bad decision/output, with status and evidence; unresolved owner is explicit |
| `EvidenceRefs` | yes | IDs resolved by the packet EvidenceIndex; no heavy artifact bodies |
| `MissingEvidence` | yes | Empty array only when no known evidence gap remains for the stated claim |
| `Confidence` | yes | Claim confidence plus evidence-backed basis |
| `Disposition` | yes | One value from the closed set below |

Closed `Disposition`:

```text
EVIDENCE_COLLECTION
MINIMAL_REPAIR
ARCHITECTURE_GATE
ENVIRONMENT_GATE
INSUFFICIENT_EVIDENCE
```

No alias, suffix, combined value, free text or worker-defined disposition is allowed.

## 4. Repair Gate

The following equations are normative for worker output routing, but do not authorize a
repair:

```text
NO_FDP   → NO_IMPLEMENTATION
NO_OWNER → NO_IMPLEMENTATION
INSUFFICIENT_EVIDENCE → EVIDENCE_COLLECTION only
```

`MINIMAL_REPAIR` is valid only when all conditions hold:

1. `FirstBad.status == CONFIRMED` and points to evidence;
2. `Owner.status == CONFIRMED` and identifies the seam that produced FirstBad;
3. every stage needed to prove the LastGood→FirstBad boundary is present;
4. every referenced evidence item resolves and has no identity mismatch;
5. Good/Bad controlled axes are recorded when a differential claim is used;
6. the candidate work changes neither architecture/authority/lifecycle nor an unapproved
   cross-owner contract;
7. a separate Human/Leader WorkItem authorizes implementation.

If (1) or (2) fails, the packet is still useful but cannot enter implementation. If a
missing fact prevents a falsifiable FDP, set `Disposition=INSUFFICIENT_EVIDENCE`; the only
permitted next work is an `EVIDENCE_COLLECTION` WorkItem.

## 5. Differential and Occurrence Rules

When a comparable Good/Bad pair exists, it takes precedence over a terminal-only reading:

```text
Good vs Bad
→ controlled axes
→ unchanged evidence
→ changed evidence
→ first semantically relevant divergence
→ LastGood / FirstBad
```

The first byte/array/order difference is not automatically the FDP. The selected divergence
must change a predicate, decision or semantic output relevant to the observed gap. Terminal
symptoms may delimit the search; they cannot prove the FDP.

Occurrence correlation examines these keys in order:

```text
RunId → ObservationSeq → OccurrenceId → StableKey → RowId → EvidenceRef → SpanId
```

The list is a narrowing order, not an identity theorem. `StableKey != SameOccurrence proof`.
Text, bounds or array index can corroborate a candidate but cannot independently establish
identity. Ambiguity remains `AMBIGUOUS`; it is never resolved by guessing.

## 6. Evidence and Trace Boundary

EvidenceRefs point to existing evidence in place. Large frames, stage dumps, trace bundles,
screenshots and replay artifacts are not copied into Debug IR or packet JSON. A reference
records explicit URI/path, kind, selector, integrity state and digest when available.

```text
TRACE != DEBUG IR
TRACE != DEBUGGER
TRACE != CONTROL
TRACE != AUTHORITY

DEBUG IR = read-only diagnostic projection of existing evidence/trace
```

This gate adds no generic Trace architecture. A trace coverage gap is represented as
`MissingEvidence`; it does not authorize new events, spans, persistence or Runtime APIs.

## 7. Skill Hierarchy Decision

The existing `.ai/skills/evidence-driven-debugging/` can carry this extension. The project has
one canonical `SKILL.md` and no established competing Runtime sub-skill hierarchy. P0 therefore
uses:

```text
evidence-driven-debugging/
├── SKILL.md
└── references/
    └── runtime/
        ├── debug-ir-schema.md
        ├── evidence-packet.md
        ├── trace-analysis.md
        ├── differential-analysis.md
        ├── tooling-contract.md
        ├── acceptance-examples.md
        └── *.schema.json
```

`references/runtime/` preserves the requested Runtime separation while following progressive
disclosure: the Skill entry remains short and loads detailed contracts only when needed. No
new Skill discovery link or duplicate body is created.

## 8. Acceptance Result

The v0 fields express all five required real cases without case-specific schema fields:

| Case | LastGood → FirstBad | GapKind | Owner | Contract result |
|---|---|---|---|---|
| checkbox adapter regression | canonical checkbox available → adapter normalization absent | `CONTRACT_REGRESSION` | `DEVICE_ADAPTER` | expressible; terminal and target occurrence remain distinct |
| Search icon / ChildOf | parent/child evidence available → relation absent at semantic composition | `COMPOSITION_GAP` | `SEMANTIC_CAPABILITY` | expressible without erasing icon role |
| Fusion NOOP fallback | uniform-list NOOP evidence → count-only delegation skips fallback | `DECISION_LOGIC_GAP` | `VISION_FUSION` | expressible with operator trace refs |
| projection bounds rounding | valid fused bounds → projection reconstructs invalid width | `NUMERICAL_BOUNDARY_GAP` | `RUNTIME_PERCEPTION` | expressible with one occurrence and frame-level consequence |
| source normalizer order drift | same-source spatial order stable → array-order predicate reports reversal | `REPRESENTATION_DRIFT` | `RUNTIME_WORLD` | expressible with Good/Bad controlled axes |

The detailed mappings are diagnostic fixtures only. They do not reopen, re-fix or graduate
any bug, and they do not convert working-tree evidence into committed capability.

## 9. Tooling Authorization Boundary

P0 freezes only command contracts for `summarize`, `occurrence`, `trace-diff`,
`terminal-chain` and `packet`. Every candidate command is:

```text
READ_ONLY
DETERMINISTIC
NO_RUNTIME_AUTHORITY
NO_TRACE_MUTATION
```

No executable, package, Runtime service, DriverHost wire, device operation or automatic repair
has been implemented by this gate.

## 10. Worker / Leader Boundary

After a separate implementation gate, a DeepSeek worker may mechanically implement schema
validation, explicit EvidenceRef readers, deterministic projections/renderers, golden fixtures
and offline CLI parsing inside a frozen WorkItem. It may not choose architecture, invent enums,
resolve ambiguous occurrence identity, assert owner without evidence, add trace coverage,
change Runtime, authorize repair or create a formal Roadmap.

Leader/Human gate remains required for new Trace/Runtime evidence, storage/retention/privacy,
wire/API, cross-layer identity, automatic owner/repair routing, formal Roadmap, OpenSpec scope and
all implementation authorization.

## 11. Stop Conditions

Stop and return a Human Gate packet if:

- a required Debug IR field needs a new Runtime wire/API;
- the requested output would change Trace authority or make Trace control behavior;
- existing evidence cannot express the relevant LastGood/FirstBad boundary;
- occurrence identity stays ambiguous;
- owner or architecture boundary cannot be resolved from existing evidence.

This P0 design did not trigger the first three structural STOP conditions: the five accepted
cases are expressible through existing evidence and explicit missing/ambiguous states. This is
not evidence that every future Runtime bug has complete trace coverage.

## 12. Documentation Governance Correction

The accepted landscape is now
[runtime-debugging-capability-landscape.md](runtime-debugging-capability-landscape.md) under
`docs/analysis/`. It remains `Authority: NONE` and is removed from
`docs/decisions/index.md`. The migration manifest is recorded in
[docs/analysis/AGENTS.md](AGENTS.md). No formal Roadmap was created.

## 13. Gate Result

```text
PROJECT_LEADER_RUNTIME_DEBUGGING_CAPABILITY_P0_CONTRACT_RESULT
P0_CONTRACT: FROZEN_FOR_WORKER_OUTPUT
P0_IMPLEMENTATION: NOT_AUTHORIZED
GENERIC_TRACE_ARCHITECTURE: NOT_AUTHORIZED
RUNTIME_BEHAVIOR_CHANGE: NONE
FORMAL_ROADMAP: NOT_CREATED
NEXT: HUMAN_GATE
```
