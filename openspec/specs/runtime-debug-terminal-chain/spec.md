# runtime-debug-terminal-chain Specification

## Purpose
定义单个 Evidence Packet 的 terminal、固定七阶段 causal chain 与已存诊断字段投影，使 Agent 能审查 stored facts 而不由 tooling 重算诊断或取得 authority。

## Requirements

### Requirement: Terminal causal chain projection
The Toolchain SHALL project one packet's stored TerminalState, the ordered EvidenceChain stages (stage/status/summary/refs), and stored LastGood/FirstBad. When the packet stores GapKind, Confidence, Disposition, or Owner, the projection SHALL surface them as `storedDiagnostics` marked STORED facts (Owner limited to status/domain/seam/basis). The tool SHALL NOT recompute any diagnosis; absent fields SHALL stay absent.

#### Scenario: Historical packet surfaces stored diagnosis
- **WHEN** a packet stores GapKind, Owner, Disposition, LastGood, and FirstBad
- **THEN** the terminal-chain projection SHALL include them verbatim with a STORED marker and the full ordered stage chain

#### Scenario: Structural packet has terminal only
- **WHEN** a machine-generated packet stores TerminalState but no chain or diagnosis fields
- **THEN** the projection SHALL report the terminal state with an empty chain and empty storedDiagnostics, without fabricating either
