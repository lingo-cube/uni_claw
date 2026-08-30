# runtime-debug-trace-diff Specification

## Purpose
定义两个 Evidence Packet 的固定七阶段机械 trace diff，报告 status/ref 变化与首个机械变化阶段，同时明确不推断 first semantically relevant divergence。

## Requirements

### Requirement: Packet-vs-packet chain diff
The Toolchain SHALL diff two EvidencePackets' `EvidenceChain` stage-by-stage in chain order, reporting per-stage present (UNCHANGED/CHANGED/ADDED/REMOVED), statusAxis, and refsAxis (equality over input+decision+output refs), plus the first mechanically changed stage and goodOnly/badOnly ref lists. Both packets' stored LastGood/FirstBad SHALL be projected verbatim. The tool SHALL NOT infer the first semantically relevant change. Either packet lacking an EvidenceChain SHALL fail closed with `INSUFFICIENT_TRACE_COVERAGE`.

#### Scenario: First mechanically changed stage
- **WHEN** good and bad chains differ only in the `raw` stage status
- **THEN** `firstMechanicallyChangedStage` SHALL be `raw`, the remaining stages SHALL report UNCHANGED, and the stored LastGood/FirstBad of both packets SHALL appear in the result

#### Scenario: Chain-less structural packets
- **WHEN** both packets are machine-generated structural packets without an EvidenceChain
- **THEN** the command SHALL return `INSUFFICIENT_TRACE_COVERAGE` and SHALL NOT emit a diff
