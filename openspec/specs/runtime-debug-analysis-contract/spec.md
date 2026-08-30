# runtime-debug-analysis-contract Specification

## Purpose
TBD - created by archiving change runtime-debugging-toolchain. Update Purpose after archive.

## Requirements

### Requirement: Structural facts before machine diagnosis
Analysis SHALL emit structural facts first — run summary, terminal causal chain, occurrence timeline, unknown-obligation analysis, trace/run compare, evidence-chain extraction, first-blocker extraction — and SHALL machine-generate a base Evidence Packet; semantic FDP / Owner / GapKind / Disposition diagnosis SHALL then be performed by the Agent/Skill over the Debug IR with FACT, INFERENCE, and MISSING_EVIDENCE explicitly separated, never presenting inference as trace fact.

#### Scenario: Packet is machine-generatable
- **WHEN** a run's evidence is available
- **THEN** the Toolchain SHALL be able to produce a base Evidence Packet (Debug IR v0 shape) deterministically, and the Agent SHALL add only semantic-diagnosis sections

#### Scenario: Diagnosis separates fact from inference
- **WHEN** an Agent diagnoses a Debug IR
- **THEN** the diagnosis SHALL label each claim FACT, INFERENCE, or MISSING_EVIDENCE and SHALL NOT state an inference as a recorded trace fact

### Requirement: Skill routing trigger
When a Runtime / FSM / Traversal / Perception / Fusion / Semantic / Completeness E2/E3/E4 validation failure occurs, the debugging workflow SHALL be offered from the existing skill without Leader re-authoring: Freeze Reality → Query Run → Find First Blocker → Build Evidence Packet → Find Good/Bad Pair → Trace Diff → locate LAST_GOOD / FIRST_BAD → FDP → Owner → GapKind → Disposition. Implementation work SHALL require FDP, Owner, and EvidenceRefs present; otherwise only EVIDENCE_COLLECTION is permitted.

#### Scenario: Failure auto-routes to the debugging workflow
- **WHEN** an E2/E3/E4 validation failure is recorded
- **THEN** the skill SHALL offer the runtime debugging workflow automatically and SHALL produce only non-authoritative diagnostic output

#### Scenario: Evidence gate for implementation
- **WHEN** an implementation WorkItem is proposed without an established FDP, Owner, and EvidenceRef set
- **THEN** the workflow SHALL restrict permitted action to EVIDENCE_COLLECTION
