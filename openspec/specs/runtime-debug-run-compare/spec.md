# runtime-debug-run-compare Specification

## Purpose
定义两个显式 capture bundle 之间 terminal、record 与 verified asset 的结构事实差分，为后续语义判断提供机械 Good/Bad 对照而不推断 FirstBad。

## Requirements

### Requirement: Paired-bundle structural diff
The Toolchain SHALL compare two capture bundles with `run-compare` and report per-axis verdicts (terminal / records / assets as UNCHANGED or CHANGED), per-run deterministicInputDigest, terminal and records facts, and an asset diff (added by absence in good, removed by absence in bad, changedOrSame by artifact id with hash equality). The tool SHALL NOT infer the first semantically relevant change.

#### Scenario: Structural difference between good and bad bundles
- **WHEN** the bad bundle adds an artifact, changes asset hashes, and records a different terminal outcome
- **THEN** the axes SHALL report CHANGED, `added` SHALL list the new artifact, and the changed/shared artifacts SHALL be marked CHANGED

#### Scenario: Identical bundles
- **WHEN** two bundles carry the same terminal, records, and asset contents
- **THEN** all axes SHALL report UNCHANGED and every shared asset SHALL be marked UNCHANGED

### Requirement: Fail-closed bundle pairing
Both bundle directories SHALL be read through the checksum-verified bundle adapter; a missing or malformed bundle SHALL fail closed with the corresponding status before any comparison output.

#### Scenario: Missing bundle fails closed
- **WHEN** either bundle directory does not exist
- **THEN** the command SHALL return `EVIDENCE_UNAVAILABLE` and SHALL NOT emit a comparison
