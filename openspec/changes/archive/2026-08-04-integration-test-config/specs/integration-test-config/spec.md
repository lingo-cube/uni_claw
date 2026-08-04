# integration-test-config Specification

## Purpose
Provide a versioned, fail-fast configuration file (`integration.config.json`) as the single source of truth for integration test running parameters, with a three-layer validation chain (file structure → effective configuration after env override → runtime preconditions) and an L2 env override channel.

## ADDED Requirements

### Requirement: Versioned schema with fail-fast validation

The project SHALL provide `tests/UniClaw.Host.Tests/Integration/integration.config.json` with a `schema` field that MUST equal `uniclaw.integrationConfig.v1`. `IntegrationConfigLoader.Load(path?)` SHALL parse and validate the file and SHALL throw `InvalidOperationException` (fail-fast) with an actionable message when the schema version mismatches, required sections are missing, or the file cannot be read.

#### Scenario: Valid config loads

- **WHEN** `Load()` runs against the default `integration.config.json`
- **THEN** it returns a parsed `IntegrationConfig` without throwing

#### Scenario: Schema version mismatch

- **WHEN** the file's `schema` value differs from `uniclaw.integrationConfig.v1`
- **THEN** `Load()` throws `InvalidOperationException` naming the expected schema

#### Scenario: Missing file

- **WHEN** `Load(path)` is called with a non-existent path
- **THEN** it throws `InvalidOperationException` indicating the missing file

### Requirement: Emulator section constraints

The `emulator` section SHALL be required and SHALL carry `serial` (default `"auto"` = single online device resolution), `outputRoot` (required, relative to repo root), `runNaming` (UTC format, default `yyyyMMddTHHmmssZ`), `keepRuns` (≥ 0), and `recordBaseline` (boolean). Validation SHALL fail-fast on `keepRuns < 0` or missing `outputRoot`.

#### Scenario: Emulator defaults apply

- **WHEN** `serial`/`runNaming` are omitted
- **THEN** loader fills `"auto"` and `yyyyMMddTHHmmssZ` respectively

#### Scenario: Negative keepRuns rejected

- **WHEN** `emulator.keepRuns` is `-1`
- **THEN** `Load()` throws `InvalidOperationException`

### Requirement: Provider section with ownership rules

The `providers` section SHALL be keyed by provider id from the known set {local, sensenova, claude, qwen, mock}. Cloud providers (sensenova/claude/qwen) SHALL require a non-empty `model`; local/mock SHALL NOT require one. `visionServer` SHALL be permitted only under `local`, with `ocrBackend` ∈ {rapidocr, paddleocr}. `intentModel` SHALL be permitted only under `sensenova`.

#### Scenario: Cloud provider without model rejected

- **WHEN** a cloud provider block omits `model`
- **THEN** `Load()` throws `InvalidOperationException`

#### Scenario: Local and mock without model load

- **WHEN** `local` and `mock` blocks omit `model`
- **THEN** `Load()` succeeds

#### Scenario: Vision server on non-local provider rejected

- **WHEN** `visionServer` is declared under a non-`local` provider
- **THEN** `Load()` throws `InvalidOperationException`

#### Scenario: Invalid OCR backend rejected

- **WHEN** `ocrBackend` is not `rapidocr` or `paddleocr`
- **THEN** `Load()` throws `InvalidOperationException`

#### Scenario: Intent model on non-sensenova rejected

- **WHEN** `intentModel` is declared under a non-`sensenova` provider
- **THEN** `Load()` throws `InvalidOperationException`

### Requirement: Scenario section binding rules

The `scenarios` section SHALL require `id`, `file`, and `scope` per entry, SHALL require `provider` to reference an existing provider id, SHALL require `mode` ∈ {direct, legacy, interactive}, and SHALL require `timeoutSeconds` > 0 when present.

#### Scenario: Unknown provider reference rejected

- **WHEN** a scenario's `provider` is not in the providers section
- **THEN** `Load()` throws `InvalidOperationException`

#### Scenario: Invalid mode rejected

- **WHEN** a scenario's `mode` is not in the known set
- **THEN** `Load()` throws `InvalidOperationException`

### Requirement: Effective-configuration validation after env override

`ResolveScenario(config, scenarioId, providerOverride, modelOverride)` and `ResolveScenarioByFile(config, scenarioFile, scope)` SHALL apply L2 env overrides (`UNICLAW_INTEGRATION_PROVIDER`, `UNICLAW_INTEGRATION_MODEL`) and SHALL re-validate the effective configuration: a scenario switched to a cloud provider with an empty model SHALL fail-fast; non-cloud providers with an empty model SHALL load.

#### Scenario: Env override to cloud provider with empty model fails

- **WHEN** `UNICLAW_INTEGRATION_PROVIDER=sensenova` overrides a local-bound scenario and no model is available
- **THEN** resolution throws `InvalidOperationException`

#### Scenario: Env override to local provider with empty model loads

- **WHEN** a cloud-bound scenario is overridden to `local`/`mock` without a model
- **THEN** resolution succeeds

### Requirement: L2 env override channel

The loader SHALL treat `UNICLAW_INTEGRATION_PROVIDER`/`UNICLAW_INTEGRATION_MODEL` as per-run selectors: when set, they SHALL override the file values; when unset, file values SHALL apply. File values SHALL NOT override set env values.

#### Scenario: Env override wins over file

- **WHEN** `UNICLAW_INTEGRATION_PROVIDER` is set and the file binds a different provider
- **THEN** the resolved scenario uses the env provider

#### Scenario: File value applies when env unset

- **WHEN** no provider env is set
- **THEN** the resolved scenario uses the file's provider binding
