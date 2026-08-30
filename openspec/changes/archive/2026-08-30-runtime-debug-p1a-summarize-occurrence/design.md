## Context

P0 已冻结并通过五个历史案例的 Evidence Packet v0 conformance。现有 Runtime/Harness/DriverHost 包含只读 trace 投影能力，但直接依赖这些 production assemblies 会扩大工具边界，并可能把调试命令误接到真实 IO 或 Runtime authority。P1a 只需要消费已经生成的 packet；动机与范围见 `proposal.md`。

## Goals / Non-Goals

**Goals:**

- 提供一个零外部依赖、离线、read-only 的本地命令入口。
- 固定 canonical JSON envelope、closed status、typed occurrence selector 与稳定排序。
- 使用五个 P0 fixtures 验证真实案例可读，并覆盖 invalid、ambiguous、insufficient cases。
- 让后续 P1 能复用 packet reader、projection 与 result envelope，而不预先实现 P1b。

**Non-Goals:**

- 不读取任意 capture bundle、stage artifact、frame 或 live Runtime。
- 不实现完整 JSON Schema Draft 2020-12 引擎；只执行 P1a 所需的 fail-closed structural/semantic validation。
- 不计算 FDP、root cause、Owner、Disposition 或 repair eligibility；只保留/投影已存事实中被命令 contract 允许的部分。
- 不实现 `trace-diff`、`terminal-chain`、`packet`、replay extraction、automatic minimization 或 trace completeness scoring。

## Decisions

### 1. 独立 Python 工具，不引用 production projects

实现位于 `tools/runtime_debug/`，可执行入口为 `tools/runtime-debug`，仅使用 Python standard library。它不引用或启动 Runtime、Harness、DriverHost、PhysicalHost、Adapters 或 VisionHost。

选择原因：P1a 的 source of truth 是 packet JSON，而不是 production object model。独立工具使 read-only/no-authority 边界可由依赖图和测试直接证明。

备选方案：新增 DriverHost sibling console project 可复用 C# model，但会新增 project/packaging 并扩大依赖；把 CLI 放入 Runtime/PhysicalHost 会混淆 authority，拒绝。

### 2. P1a 仅接受单个 Evidence Packet v0 文件

命令形式冻结为：

```text
runtime-debug summarize <packet>
runtime-debug occurrence <packet> --occurrence-id <value>
runtime-debug occurrence <packet> --stable-key <value>
runtime-debug occurrence <packet> --row-id <value>
runtime-debug occurrence <packet> --evidence-ref <value>
```

不接受目录、glob、多 source、run-id shortcut 或隐式 latest。单 packet scope 避免把 discovery、capture parsing 和 cross-run identity 偷渡进 P1a；后续扩展必须经过独立 Gate。

### 3. 固定 machine envelope 和 canonical serialization

所有 stdout 使用一个 JSON object：

```text
contractVersion, command, status, source, result, diagnostics
```

- `contractVersion` 固定为 `runtime-debug-cli.p1a`；
- `source` 只含 packetVersion、packetId 和 packet 内的 sourceIdentity；
- `result` 在成功时保存命令投影，失败时为 `null`；
- `diagnostics` 是稳定排序的 `{code, message, evidenceRefs}` 数组；
- serializer 使用 UTF-8、按 key 排序、固定 separators、单个 trailing newline；
- 不输出 timestamp、absolute resolved path、process id、stack trace 或环境相关字段。

closed status 到 exit code 的映射固定为：`OK=0`，`INVALID_INPUT=2`，`EVIDENCE_UNAVAILABLE=3`，`IDENTITY_MISMATCH=4`，`AMBIGUOUS_OCCURRENCE=5`，`INSUFFICIENT_TRACE_COVERAGE=6`，`SCHEMA_VIOLATION=7`。

### 4. P1a reader 做最小 fail-closed conformance validation

reader 验证：顶层 object、packetVersion、P1a 必需字段/类型、Debug IR schema version、EvidenceIndex refId 唯一、Debug IR 所用 EvidenceRefs 均可解析，以及命令投影实际访问字段的必需结构。验证失败返回 `SCHEMA_VIOLATION`。

P1a 不宣称替代正式 Draft 2020-12 validator。schema conformance corpus 仍由 P0 gate 维护；这里的 reader 只保证不会把缺失或错型字段投影成成功结果。

### 5. Occurrence candidate 从 packet 内两类事实构造

`TargetOccurrence` 是唯一诊断 occurrence record；`EvidenceIndex[*].selector` 是相关 evidence 坐标。查询先匹配 TargetOccurrence 的对应 typed field，再关联其 EvidenceRefs；`--evidence-ref` 也可直接定位该 target 或一个 indexed evidence coordinate。

一个 result candidate 包含：stored target status、RunId/ObservationSeq/OccurrenceId/StableKey/RowId/SpanIds、proof、counterevidence 和按 refId 排序的 linked evidence metadata。工具不读取 EvidenceRef URI 指向的内容。

若 selector 只命中 indexed evidence、但无法关联到完整 TargetOccurrence，返回 `INSUFFICIENT_TRACE_COVERAGE`；若同一 selector 对应多个互不相容的存储 identity tuple，返回 `AMBIGUOUS_OCCURRENCE`；EvidenceRef integrity 为 `IDENTITY_MISMATCH` 时返回同名状态。排序键为 `RunId(null last) → ObservationSeq(null last) → OccurrenceId → StableKey → RowId → refId`。

### 6. Summary 是受限投影，不是诊断器

summary result 仅包含：`terminalState`、`targetObservation`、`targetOccurrence` 的 stored scope/status、`evidenceAvailability`（计数、integrity、refs）、`missingEvidence`、`repairBlockers`。不输出或重算 FirstBad、GapKind、Owner、Disposition、repair eligible conclusion。

## Risks / Trade-offs

- [P0 packet 只有一个 TargetOccurrence，无法形成完整跨 frame timeline] → 缺失坐标返回 `INSUFFICIENT_TRACE_COVERAGE`；P1b 或新的 evidence contract 需另行 Gate。
- [stdlib reader 不是通用 JSON Schema engine] → 明确限制能力声明；P0 schema validator 继续独立验证 fixtures，P1a 测试覆盖所有被访问字段与 fail-closed 路径。
- [canonical output 未来扩展可能破坏 golden tests] → 使用显式 `contractVersion`；字段或语义变化必须升级 contract 并通过 OpenSpec。
- [packet 内 URI 可能指向大 artifact 或外部路径] → P1a 永不解引用 URI，只投影 metadata。
- [CLI 被误当成 repair authority] → 输出省略 repair eligibility、Owner 与 Disposition，README 和 tests 固定 non-interface 与 authority 边界。

## Migration Plan

该能力为 additive local tooling，无 production migration。回滚仅删除独立工具、入口与对应 tests；P0 packet、Runtime、Trace 和现有 evidence 均不变。
