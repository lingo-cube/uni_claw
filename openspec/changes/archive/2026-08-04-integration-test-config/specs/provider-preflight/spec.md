# provider-preflight Specification

## Purpose
Verify, at test assembly time and before any Host run, that the runtime preconditions for the scenario's effective provider hold — credentials env, secrets files, and local asset paths — failing fast with actionable messages when they do not.

## ADDED Requirements

### Requirement: Assembly-time preflight invocation

The integration test harness SHALL invoke `ProviderPreflight.Check(scenario, repoRoot)` during test assembly for each scenario's effective (post-override) provider, before the Host run starts. Check SHALL fail fast by throwing with a message stating what is missing and how to supply it.

#### Scenario: Preflight passes for a ready provider

- **WHEN** the effective provider's credentials and paths are present
- **THEN** `Check` returns without throwing and the test proceeds

#### Scenario: Preflight fails for a missing credential

- **WHEN** the effective provider requires a credential env that is unset
- **THEN** `Check` throws naming the missing variable

### Requirement: Per-provider precondition rules

`ProviderPreflight.Check` SHALL apply the following rules: `mock` SHALL pass without checks; `local` SHALL require `DEEPSEEK_API_KEY` plus the existence of the `yoloModel` and `labelMapping` files resolved relative to repo root, and SHALL fail when the `visionServer` section is absent; `claude` SHALL require `ANTHROPIC_API_KEY`; `sensenova` SHALL require `SENSENOVA_API_KEY` or `~/.litellm/secrets.json`; `qwen` SHALL require `QWEN_API_KEY` or `~/.litellm/secrets.json`.

#### Scenario: Mock always passes

- **WHEN** the effective provider is `mock`
- **THEN** `Check` succeeds without inspecting credentials or paths

#### Scenario: Local missing DeepSeek key fails

- **WHEN** the effective provider is `local` and `DEEPSEEK_API_KEY` is unset
- **THEN** `Check` throws requiring the key

#### Scenario: Local missing vision asset files fails

- **WHEN** the effective provider is `local` and `yoloModel`/`labelMapping` files do not exist
- **THEN** `Check` throws naming the missing file path

#### Scenario: Local without visionServer section fails

- **WHEN** the effective provider is `local` and no `visionServer` section exists
- **THEN** `Check` throws

#### Scenario: Claude missing API key fails

- **WHEN** the effective provider is `claude` and `ANTHROPIC_API_KEY` is unset
- **THEN** `Check` throws requiring the key

#### Scenario: Sensenova or qwen key via secrets file

- **WHEN** the effective provider is `sensenova` or `qwen` and its key env is unset but `~/.litellm/secrets.json` exists
- **THEN** `Check` succeeds
