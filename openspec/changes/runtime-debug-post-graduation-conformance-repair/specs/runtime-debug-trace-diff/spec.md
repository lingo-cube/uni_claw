## MODIFIED Requirements

### Requirement: Packet-vs-packet chain diff
The Toolchain SHALL diff two valid EvidencePackets' required `EvidenceChain` stage-by-stage in canonical order, reporting per-stage present (UNCHANGED/CHANGED), statusAxis, and refsAxis (equality over input+decision+output refs), plus the first mechanically changed stage and goodOnly/badOnly ref lists. Both packets' stored LastGood/FirstBad SHALL be projected verbatim. The tool SHALL NOT infer the first semantically relevant change. Missing or malformed required chain data SHALL fail closed as `SCHEMA_VIOLATION` at packet validation.

#### Scenario: First mechanically changed stage
- **WHEN** good and bad chains differ only in the `raw` stage status
- **THEN** `firstMechanicallyChangedStage` SHALL be `raw`, the remaining stages SHALL report UNCHANGED, and the stored LastGood/FirstBad of both packets SHALL appear in the result

#### Scenario: Chain-less structural packets
- **WHEN** both Schema-valid generated packets store all seven chain stages as explicit `MISSING`
- **THEN** the command SHALL compare them in canonical order and SHALL report no mechanically changed stage when their stored stage facts and refs are equal
