# provider-env-injection Specification

## Purpose
Injection of provider-specific environment variables from `integration.config.json` into the test run at assembly time (L1 → L4 channel), so test code and manual exports no longer hardcode run parameters, while preserving hand-set/CI env priority.

## ADDED Requirements

### Requirement: Local provider vision env injection

For a scenario whose effective provider is `local`, the test harness SHALL inject `providers.local.visionServer` fields as environment variables before the Host run: `socket` → `UNICLAW_VISION_SOCK`, `port` → `UNICLAW_VISION_PORT`, `ompThreads` → `UNICLAW_OMP_THREADS`, `ocrBackend` → `UNICLAW_OCR_BACKEND`, `ocrTextScore` → `UNICLAW_OCR_TEXT_SCORE`, `yoloModel` → `UNICLAW_YOLO_MODEL`, `labelMapping` → `UNICLAW_LABEL_MAPPING`. Relative paths (`yoloModel`, `labelMapping`) SHALL be resolved to repo-root absolute paths at injection time.

#### Scenario: Vision env injected from config

- **WHEN** a `local` scenario runs with a populated `visionServer` section
- **THEN** all mapped variables are set in the test process env, with relative paths resolved against repo root

#### Scenario: No injection for non-local providers

- **WHEN** the effective provider is not `local`
- **THEN** no vision env variables are injected

### Requirement: Sensenova intent model injection

For a scenario whose effective provider is `sensenova`, the harness SHALL inject `providers.sensenova.intentModel` as `SENSENOVA_MODEL` when present. When absent, the Host default SHALL apply.

#### Scenario: Intent model injected from config

- **WHEN** a `sensenova` scenario declares `intentModel`
- **THEN** `SENSENOVA_MODEL` is set to that value before the Host run

#### Scenario: Intent model absent uses Host default

- **WHEN** a `sensenova` scenario omits `intentModel`
- **THEN** no `SENSENOVA_MODEL` injection occurs and the Host default applies

### Requirement: Hand-set env priority

Injection SHALL NOT overwrite already-set environment variables: when the target variable is already set in the process env, the injected value SHALL be ignored (set-if-absent semantics), preserving hand-set and CI overrides.

#### Scenario: Pre-set env wins over injection

- **WHEN** `UNICLAW_YOLO_MODEL` is already set before a `local` scenario runs
- **THEN** the pre-set value remains effective

### Requirement: No hardcoded run parameters in test code

The integration test harness SHALL derive provider, model, mode, timeout, and output root from the resolved configuration, and SHALL NOT hardcode these running parameters in test code.

#### Scenario: Run parameters come from config

- **WHEN** a scenario runs through `RunScenarioAsync`
- **THEN** provider/model/mode/timeout/outputRoot are taken from the resolved `IntegrationConfig` values
