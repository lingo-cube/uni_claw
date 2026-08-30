# Runtime Debug Tooling Contract v0

> Status: `P0_INTERFACE_ONLY / IMPLEMENTATION_NOT_AUTHORIZED`
> Authority: `NONE`

所有候选命令必须满足：

```text
READ_ONLY
DETERMINISTIC
NO_RUNTIME_AUTHORITY
NO_TRACE_MUTATION
```

公共规则：input 必须显式；禁止猜 `latest`；禁止启动 Runtime/设备/重跑；禁止修改
artifact/trace/receipt；JSON 是 canonical output，Markdown 只是 derived view；ambiguity、
missing evidence、identity mismatch 均 fail closed。

Closed command status:

```text
OK
INVALID_INPUT
EVIDENCE_UNAVAILABLE
IDENTITY_MISMATCH
AMBIGUOUS_OCCURRENCE
INSUFFICIENT_TRACE_COVERAGE
SCHEMA_VIOLATION
```

## `runtime-debug summarize <run>`

Input：显式 run report、capture manifest 或 packet ref，且只能一个 source root。

Output：source identity、terminal state、available/missing evidence、receipt/revision identity、
observation/trace range 与 unresolved blockers。不得输出 root cause 或 repair authorization。

## `runtime-debug occurrence <id>`

Input：显式 run scope + 一个 selector。`<id>` 可以是 OccurrenceId、StableKey、RowId 或
EvidenceRef，但 selector kind 必须显式，不得按字符串猜类型。

Output：按 ObservationSeq 排序的 occurrence candidates、correlation status/proof、type/role/
bounds/source changes 与 linked evidence refs。多解返回 `AMBIGUOUS_OCCURRENCE`。

## `runtime-debug trace-diff <good> <bad>`

Input：两个显式 refs + 可选 stage/occurrence/terminal scope。

Output：controlled/intentionally-changed/unknown axes、unchanged evidence、changed evidence、
LastGood/FirstBad candidate 与 coverage gaps。不同 identity 只有在 comparison purpose 明确且
变化轴被记录时可比较；否则 `IDENTITY_MISMATCH`。

## `runtime-debug terminal-chain <run>`

Input：显式 run/capture/packet ref。

Output：terminal → last Runtime decision/state → affordance/admission → canonical/fused/
normalized/raw 的已证因果链。任何断点都输出 `INSUFFICIENT_TRACE_COVERAGE`，不通过读代码
补成 runtime fact。

## `runtime-debug packet <run>`

Input：显式 source + optional target selector + optional explicit Good/Bad refs。

Output：符合 `runtime-debug-evidence-packet.v0` 的 packet。工具可以机械生成 evidence index、
MissingEvidence 和 repair blockers；`GapKind`、`Owner`、`Confidence`、`Disposition` 在无充分
evidence 时必须保持 `UNKNOWN/UNRESOLVED/UNASSESSED/INSUFFICIENT_EVIDENCE`，不得猜测。

## Determinism Contract

- explicit input content + tool/schema version 相同 → canonical JSON byte-equivalent；
- arrays 使用各 schema 定义的稳定排序；
- path normalization 不改变 referenced content identity；
- timestamp 不进入 semantic digest；
- tool 不访问网络、设备、Runtime process 或隐式 repository state；
- error 不产生 partial packet pretending success。

## Non-Interfaces

```text
runtime-debug fix
runtime-debug retry
runtime-debug click
runtime-debug collect-without-explicit-scope
runtime-debug choose-owner
runtime-debug promote-receipt
runtime-debug mutate-trace
runtime-debug graduate
```

这些接口不属于 P0/P1 候选 contract。
