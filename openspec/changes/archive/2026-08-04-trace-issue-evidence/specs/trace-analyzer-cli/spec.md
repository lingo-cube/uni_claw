## ADDED Requirements

### Requirement: diagnose supplements issue fingerprints from issues.jsonl

`diagnose` SHALL include an `issue_fingerprints` evidence entry when the run has issue records AND `result.json`'s `issueFingerprints` is empty. The evidence text SHALL carry the fingerprint and the issue summary, which embeds the D-192 failure detail (e.g. `target_page_identity_not_verified: Post-action page identity '<empty>' did not match the scenario success identities.`) so the real failure reason is consumable without reading issues.jsonl directly. When `result.json` already carries fingerprints, issues.jsonl SHALL NOT duplicate them. If issues exist but provide no fingerprint (malformed/absent field), the evidence entry SHALL be omitted rather than emit an empty fingerprint.

#### Scenario: verification failure with empty result fingerprints gets issue evidence

- **WHEN** `uni-claw trace diagnose --run <dir> --format json` runs on a run whose result.json has empty `issueFingerprints` but issues.jsonl contains one verification issue with fingerprint + summary embedding the failure detail
- **THEN** the JSON `evidence` array contains an `issue_fingerprints` entry whose text includes the fingerprint and the issue summary, and the verdict confidence is raised above the empty-evidence floor

#### Scenario: result fingerprints present prevent duplication

- **WHEN** result.json's `issueFingerprints` is non-empty and issues.jsonl also exists
- **THEN** the evidence reflects only the result.json fingerprints (no duplicate entries from issues.jsonl)

#### Scenario: issues without fingerprints are omitted

- **WHEN** issues.jsonl entries lack a usable fingerprint
- **THEN** no `issue_fingerprints` evidence entry is emitted
