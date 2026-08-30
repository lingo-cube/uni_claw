## MODIFIED Requirements

### Requirement: Terminal causal chain projection
The Toolchain SHALL project one valid packet's stored TerminalState and EvidenceChain in the frozen order `raw → normalized → fused → canonical → semanticAdmission → affordance → runtimeState`. Stored LastGood/FirstBad and GapKind/Confidence/Disposition/Owner SHALL be surfaced only when present, grouped as STORED facts, with Owner limited to status/domain/seam/basis. The tool SHALL NOT recompute diagnosis; absent optional fields SHALL remain absent, while malformed required fields SHALL have already failed closed at packet validation.

#### Scenario: Historical packet surfaces stored diagnosis
- **WHEN** a valid packet stores GapKind, Owner, Disposition, LastGood, and FirstBad
- **THEN** the terminal-chain projection SHALL include them verbatim with a STORED marker and the full canonical stage chain

#### Scenario: Structural packet has terminal only
- **WHEN** a valid generated P0 packet stores all seven stages as `MISSING` and its divergence/owner/confidence fields as unresolved or unassessed
- **THEN** the projection SHALL preserve those stored states in canonical order without manufacturing `null`, recomputing FDP, or promoting confidence
