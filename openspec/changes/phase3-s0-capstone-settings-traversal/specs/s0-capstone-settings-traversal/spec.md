## ADDED Requirements

### Requirement: Construct the deterministic S0 external world without encoding production conclusions
The integration fixture SHALL define a deterministic external Settings world with an approved semantic navigation tree of safe reachable pages to at least four levels, visible elements, dispatch outcomes, world transitions, Observation data, one dangerous visible mutation candidate, one local Popup/Overlay obstruction, and one external drift to Launcher/desktop. The fixture SHALL NOT encode Container identity, Recovery authority, progress completion, or Goal success, and SHALL NOT define the concrete route that the Runtime must traverse.

#### Scenario: World exposes structure, not answers
- **WHEN** the integration run starts with traversal intent, allowed scope, a depth bound, and safety constraints but no pre-enumerated page/action list
- **THEN** the world determines outcomes deterministically from the action sequence and the Runtime derives route/progress/completion from evidence

#### Scenario: Dangerous candidate is visible but not executable
- **WHEN** a visible destructive candidate (reset, delete/clear-data, uninstall, or equivalent) is observed
- **THEN** the Runtime never dispatches it and records explicit rejected/denied evidence

### Requirement: Schedule exactly one Popup disturbance and exactly one external drift
The fixture SHALL schedule exactly one local Popup/Overlay obstruction and exactly one external Agent-scope drift to Launcher/desktop at deterministic points of the run. No additional disturbance of either class SHALL occur.

#### Scenario: Popup is handled with verified continuity
- **WHEN** the Popup obstruction occurs during traversal
- **THEN** the Runtime applies the frozen SC-P3-002 bounded local handling and proves fresh verified Container continuity before continuing

#### Scenario: Drift recovery reconciles, never silently continues
- **WHEN** the external drift to Launcher/desktop occurs after a completed branch
- **THEN** the Runtime applies the frozen SC-P2-001/SC-P3-CAND-005/SC-P3-CAND-009 path: re-enter Settings, restore a trusted semantic position, Observe, Verify, reconcile fresh evidence, and never count the re-entry as new progress or fabricate retained progress

### Requirement: Compose the traversal exclusively from frozen capabilities
The integration run SHALL exercise the frozen capabilities in composition and SHALL add zero production semantics. The complete route SHALL NOT be encoded up front merely to make the Capstone pass; branch discovery SHALL come from fresh external-world evidence within the approved scope.

#### Scenario: Discovery composes with progress and safety
- **WHEN** fresh evidence discovers a required branch absent from the initial Plan
- **THEN** the frozen SC-P3-CAND-008/004/006 composition authorizes, executes, records, and never double-counts the branch without any new production surface

### Requirement: Complete the Run only on independently satisfied GoalEvidence
The Runtime SHALL complete the Run only when GoalEvidence proves all of: (1) every approved reachable safe branch within depth `<= 4` is complete; (2) dangerous visible actions were not dispatched; (3) no approved branch remains unresolved; (4) Popup handling was followed by fresh verified Container continuity; (5) external drift recovery was followed by fresh verification and reconciliation; (6) already-proven traversal progress was neither fabricated nor silently discarded; (7) equal inputs replay to equal state (below). Plan exhaustion, action dispatch, Recovery dispatch, a changed viewport snapshot, or local Container completion SHALL NOT independently satisfy this Goal.

#### Scenario: Incomplete approved branch blocks completion
- **WHEN** an approved reachable safe branch within depth `<= 4` remains unresolved
- **THEN** the Run does not complete and the unresolved branch is explicitly recorded

#### Scenario: Dangerous dispatch is prohibited
- **WHEN** a visible dangerous candidate could be dispatched
- **THEN** the Run records zero dangerous dispatches and the final state proves it

### Requirement: Replay equal inputs to equal state
For equal RunId, Goal inputs, external-world inputs, disturbance schedule, and action sequence, the integration run SHALL replay to equal progress, ActionHistory, Observations, journal, Trace, GoalEvidence, and final RunState.

#### Scenario: Deterministic replay holds
- **WHEN** the same integration run is executed twice with equal inputs
- **THEN** the second execution reproduces the first execution's state exactly

### Requirement: Add zero production surface
This change SHALL add no production model type, field, enum, interface, component, or mutable state; SHALL make no production ownership or authority change; and SHALL NOT modify any frozen production behavior. Any implementation that requires production change SHALL stop and return to the Semantic Gate.

#### Scenario: Test-side-only purchase
- **WHEN** the integration fixture and harness are implemented
- **THEN** the production delta audit reports zero and all frozen slice regressions pass unchanged

### Requirement: Stop and extract a bounded candidate on any new Reality Distinction
If execution exposes a Reality Distinction not purchasable by the frozen composition, the implementer SHALL stop, extract exactly one bounded Candidate Scenario, run its Semantic Gate (human), prove/freeze that capability, and only then return to the Capstone. No such candidate is pre-approved by this change.

#### Scenario: New distinction stops the run
- **WHEN** the integration run observes behavior that none of the 13 frozen capabilities can express
- **THEN** the run stops and one bounded candidate registration is produced for the Semantic Gate
