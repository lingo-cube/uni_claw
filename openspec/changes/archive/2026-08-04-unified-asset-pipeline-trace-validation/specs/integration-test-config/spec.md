## ADDED Requirements

### Requirement: Storage section for asset backend

The config SHALL carry an optional `storage` section with a single `backend` key: `"location"` = `"emulator.outputRoot"` (path reference, no duplicated fields — the storage space root IS the emulator output root). Validation SHALL fail-fast when `storage.backend` references a location that does not exist. `providers.local` SHALL additionally accept an optional `evidenceStorage` boolean gate — when false/absent, the local provider SHALL NOT submit vision-evidence assets (gated asset).

#### Scenario: storage section references emulator.outputRoot
- **WHEN** the config declares `storage: { "backend": { "type": "location", "path": "emulator.outputRoot" } }`
- **THEN** loading succeeds and the effective storage root resolves to the emulator outputRoot value

#### Scenario: unknown storage location fails fast
- **WHEN** `storage.backend.path` references a non-existent section
- **THEN** `Load()` throws `InvalidOperationException`

#### Scenario: evidenceStorage gate suppresses evidence assets
- **WHEN** `providers.local.evidenceStorage` is false or absent and the run executes with the local provider
- **THEN** the run produces no vision-evidence assets (and no ai.evidence events for them), while normal screenshots/analysis continue
