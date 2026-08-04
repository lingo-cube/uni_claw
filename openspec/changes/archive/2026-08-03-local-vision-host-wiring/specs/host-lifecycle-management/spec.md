# host-lifecycle-management Specification

## ADDED Requirements

### Requirement: Python process starts before engine and stops after engine

The Host SHALL start the Python vision service process before `engine.RunAsync()` and SHALL stop it after the engine completes (normal or error). The `PythonVisionService` SHALL be created at `RunScenarioAsync` level, NOT inside `CreateProviders`.

#### Scenario: Process starts before engine

- **WHEN** `--provider local` is specified and `RunScenarioAsync` executes
- **THEN** `pythonService.StartAsync()` is called and completes (health check `warm:true`) before `engine.RunAsync()` is invoked

#### Scenario: Process stops after engine

- **WHEN** `engine.RunAsync()` completes or throws
- **THEN** `pythonService.DisposeAsync()` is called in the `finally` block, killing the Python process tree

#### Scenario: Process not created for non-local mode

- **WHEN** provider is not "local"
- **THEN** no `PythonVisionService` is created and no lifecycle management occurs
