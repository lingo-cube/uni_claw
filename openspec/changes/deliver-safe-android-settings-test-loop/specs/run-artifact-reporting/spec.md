## ADDED Requirements

### Requirement: Every run has one isolated and self-describing asset directory

Each scenario execution SHALL write to `artifacts/runs/<scenario-id>/<run-id>/` or an equivalent configured root with the same logical isolation. The directory SHALL contain `manifest.json`, `scenario.snapshot.json`, `plan.json`, `steps/`, trace assets, `issues.jsonl`, and `result.json`. A run MUST NOT overwrite assets owned by another run ID.

#### Scenario: Two runs use the same scenario
- **WHEN** the same scenario executes twice
- **THEN** each execution has a distinct run directory and both retain independently readable manifests and results

### Requirement: Manifest captures reproducibility inputs

The manifest SHALL record run ID, optional iteration/parent IDs, scenario ID and hash, safety policy version/hash, source revision when available, selected device serial, Android/API identity, app/package identity, provider/model/mode, asset schema versions, start time, and output paths. Secret values MUST be omitted or redacted.

#### Scenario: Run manifest is inspected
- **WHEN** a run begins successfully
- **THEN** its manifest identifies the exact scenario, policy, device, model, and schema versions needed to interpret its evidence

### Requirement: Step evidence preserves causal order

Each attempted step SHALL have a numbered directory containing pre-action screenshot/UI hierarchy, normalized analysis, step plan, safety decision, post-decision or post-action evidence, and verification result as applicable. Each document SHALL carry the run ID, step number, timestamp, and page fingerprint needed to correlate it with trace records.

#### Scenario: Allowed action step is inspected
- **WHEN** a step plans and executes an allowed action
- **THEN** its directory provides enough correlated evidence to reconstruct what was observed, planned, allowed, executed, and verified in order

#### Scenario: Failure occurs before action
- **WHEN** screenshot capture, analysis, planning, or safety evaluation fails
- **THEN** all evidence completed before the failure is retained and the missing later phases are explicitly represented in the step/result status

### Requirement: Issues are append-only structured feedback

Problems exposed during preparation or execution SHALL be appended to `issues.jsonl`. Each issue SHALL include issue ID, stable fingerprint, category, phase, severity, summary, run/step correlation, evidence paths, first-seen timestamp, occurrence count or repetition link, and disposition. Categories SHALL distinguish device, perception, planning, safety, action, verification, traversal, provider, and reporting concerns.

#### Scenario: Same failure repeats
- **WHEN** equivalent verification failures occur in multiple iterations
- **THEN** each run preserves its occurrence and aggregate reporting groups them by stable fingerprint without deleting prior records

#### Scenario: New failure is exposed
- **WHEN** a previously unseen page-analysis error occurs
- **THEN** a new issue fingerprint is recorded with paths to the relevant screenshot, analysis/error, and trace evidence

### Requirement: Final result never overstates completion

The final result SHALL report status, completion reason, discovered/visited/skipped/failed entries, action counts, safety decisions, budget consumption, trace location, issue summary, and success-criteria evaluation. It MUST distinguish success, incomplete coverage, safety-blocked progress, operational failure, and cancellation using a versioned string vocabulary. Missing end-of-list proof or failed target-page verification MUST prevent a successful result.

#### Scenario: Enumeration reaches budget before end proof
- **WHEN** first-level enumeration exhausts its scroll budget without verified end-of-list
- **THEN** `result.json` reports incomplete coverage and the aggregate does not count the run as successful

#### Scenario: Locate target is verified
- **WHEN** target-page identity verification passes within all budgets
- **THEN** `result.json` reports success and references the matching step evidence and trace

### Requirement: Repeated runs produce an aggregate iteration report

A repeated execution SHALL produce an aggregate report with ordered child run IDs, success rate, longest consecutive success count, per-phase latency, action and safety-decision totals, issue fingerprints grouped as new/repeated/disappeared, and the scenario/policy hashes used by each child run.

#### Scenario: Ten-run stability gate is evaluated
- **WHEN** ten child runs finish
- **THEN** the aggregate states whether all ten succeeded consecutively and identifies every failure position and issue fingerprint

### Requirement: Sensitive values are excluded from persisted assets

The reporting pipeline SHALL redact API keys, authorization headers, provider credentials, and configured secret values before writing manifests, trace records, exception details, model request/response metadata, or issue records. Screenshot and UI text collection SHALL be limited to the selected emulator Settings scenario and SHALL NOT upload or persist unrelated application screens as successful scenario evidence.

#### Scenario: Provider exception contains an authorization header
- **WHEN** a provider exception includes a credential-bearing header or configured secret substring
- **THEN** persisted error, trace, and issue assets contain a redaction marker and not the secret value

#### Scenario: Runner observes another application
- **WHEN** reset or page verification detects a non-Settings application
- **THEN** the run stops with a boundary failure and does not treat that screen as valid Settings traversal evidence
