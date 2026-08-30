## ADDED Requirements

### Requirement: Terminal causal chain projection
The Toolchain SHALL project one packet's stored TerminalState, the ordered EvidenceChain stages (stage/status/summary/refs), and stored LastGood/FirstBad. When the packet stores GapKind, Confidence, Disposition, or Owner, the projection SHALL surface them as `storedDiagnostics` marked STORED facts (Owner limited to status/domain/seam/basis). The tool SHALL NOT recompute any diagnosis; absent fields SHALL stay absent.

#### Scenario: Historical packet surfaces stored diagnosis
- **WHEN** a packet stores GapKind, Owner, Disposition, LastGood, and FirstBad
- **THEN** the terminal-chain projection SHALL include them verbatim with a STORED marker and the full ordered stage chain

#### Scenario: Structural packet has terminal only
- **WHEN** a machine-generated packet stores TerminalState but no chain or diagnosis fields
- **THEN** the projection SHALL report the terminal state with an empty chain and empty storedDiagnostics, without fabricating either
