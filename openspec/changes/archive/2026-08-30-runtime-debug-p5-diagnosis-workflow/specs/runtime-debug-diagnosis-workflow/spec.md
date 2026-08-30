## ADDED Requirements

### Requirement: One-pass diagnosis aggregation
The Toolchain SHALL aggregate, for one good/bad bundle pair and case id: the structural axes diff, the generated base packet, the FAILED/CANCELLED execution spans (recursively), replay fixture/dry-run facts and (when requested) the mechanical minimal slice — all from existing Core projections, deterministic and read-only.

#### Scenario: Red pair yields gateable material
- **WHEN** a bad bundle carries a FAILED traversal span and an extra asset
- **THEN** the diagnosis report SHALL include the changed axes, the failed span, the replay counts, and the mechanical minimizer result

### Requirement: Evidence gate projection
The Toolchain SHALL project the §12 implementation gate deterministically: fdpPresent (structural axes CHANGED or failed spans or mechanical dry-run failure), ownerPresent (stored Owner seam/domain), evidenceRefsPresent (non-empty evidenceIndex). Disposition SHALL be `EVIDENCE_COLLECTION` when FDP and evidence refs are present (blocked by GAPKIND_UNKNOWN/OWNER_UNRESOLVED as applicable) and `INSUFFICIENT_EVIDENCE` otherwise. The gate SHALL be a projection — never Runtime authority; semantic FDP/Owner/GapKind judgment SHALL remain the Agent's.

#### Scenario: No facts → insufficient evidence
- **WHEN** no FDP and no evidence refs are present
- **THEN** the gate SHALL report disposition INSUFFICIENT_EVIDENCE with FDP_ABSENT/EVIDENCEREFS_ABSENT blockers

### Requirement: Skill routing reference
The evidence-driven-debugging skill SHALL carry a routing reference listing the toolchain command sequence for E2/E3/E4 failures and the implementation gate rule (NO_FDP → NO_IMPLEMENTATION, NO_OWNER → NO_IMPLEMENTATION, else EVIDENCE_COLLECTION).

#### Scenario: Failure auto-routes to the toolchain
- **WHEN** an E2/E3/E4 failure is being debugged
- **THEN** the routing reference SHALL offer the deterministic command sequence and gate semantics without re-authoring
