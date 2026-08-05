## ADDED Requirements

### Requirement: IdentityMatches rejects empty or whitespace-only names

`LocateOneItemRule.IdentityMatches` SHALL return `false` when either `actual` or `expected` is null, empty, or whitespace-only. This prevents empty OCR results from falsely matching expected identities via `string.Contains("")` which is always true.

#### Scenario: Empty actual name does not match

- **WHEN** `IdentityMatches("", "About device")` is called
- **THEN** the method returns `false`

#### Scenario: Whitespace-only actual name does not match

- **WHEN** `IdentityMatches("   ", "About device")` is called
- **THEN** the method returns `false`

#### Scenario: Null actual name does not match

- **WHEN** `IdentityMatches(null, "About device")` is called
- **THEN** the method returns `false`

#### Scenario: Empty expected name does not match

- **WHEN** `IdentityMatches("About emulated device", "")` is called
- **THEN** the method returns `false`

#### Scenario: Valid names still match via containment

- **WHEN** `IdentityMatches("About emulated device", "About device")` is called
- **THEN** the method returns `true`

### Requirement: VisualPageAnalyzer writes analysis snapshots in local provider path

In `CreateRunServices`, when `accessor is not null`（local vision provider active），the `VisualPageAnalyzer` SHALL be wrapped with `AnalysisWritingDecorator` so that post-target page analysis snapshots are submitted to the trace pipeline and persisted to `analysis.jsonl`.

#### Scenario: Post-target analysis snapshot written

- **WHEN** `VisualPageAnalyzer.AnalyzeCurrentPageAsync()` completes during post-target verification in a local provider run
- **THEN** the analysis snapshot is submitted to `ITracePipeline` via `AnalysisWritingDecorator.SubmitSnapshot`
- **THEN** the snapshot appears in `analysis.jsonl` after pipeline drain

#### Scenario: Non-local provider path unchanged

- **WHEN** `accessor` is null（non-local / AI provider）
- **THEN** `VisualPageAnalyzer` remains the raw `IPageAnalyzer` without decorator wrapping

### Requirement: AssetSubmission supports append mode for line-based assets

`AssetSubmission` SHALL include an `Append` flag (default `false`). When `Append` is `true`, `FileAssetStore.WriteAsync` SHALL use `FileMode.Append` instead of atomic `tmp+move`, enabling append-only JSONL semantics for sequential analysis snapshots.

#### Scenario: Append write adds to existing file

- **WHEN** `FileAssetStore.WriteAsync` is called with `Append=true` and the target file already exists
- **THEN** the new bytes are appended to the existing file content

#### Scenario: Append write creates file if not exists

- **WHEN** `FileAssetStore.WriteAsync` is called with `Append=true` and the target file does not exist
- **THEN** the file is created and the bytes are written

#### Scenario: Non-append write still uses atomic tmp+move

- **WHEN** `FileAssetStore.WriteAsync` is called with `Append=false` (default)
- **THEN** the existing `AssetStagingWriter.WriteBytesAsync` tmp+move path is used

#### Scenario: AnalysisWritingDecorator submits with Append flag

- **WHEN** `AnalysisWritingDecorator.SubmitSnapshot` submits an analysis line to the pipeline
- **THEN** the `AssetSubmission` carries `Append = true`
