## ADDED Requirements

### Requirement: Qwen provider registered as first-class vision provider

The Host SHALL register `"qwen"` as a valid provider identifier alongside `"mock"`, `"claude"`, and `"sensenova"`. The qwen provider SHALL use `OpenAiCompatibleVisionProvider` with a configurable base URL and model.

#### Scenario: Qwen appears in provider help text
- **WHEN** the CLI prints usage help
- **THEN** `qwen` appears in the provider options list

#### Scenario: Qwen provider passes readiness check
- **WHEN** `QWEN_API_KEY` is set and `ProviderId` is `"qwen"`
- **THEN** `ProviderReady` returns true

### Requirement: Qwen API key loaded from env or secrets file

The Host SHALL load the Qwen API key from `QWEN_API_KEY` environment variable first, then fall back to `QWEN_API_KEY` in `~/.litellm/secrets.json`. Missing key SHALL throw `HostPreparationException` with an actionable message.

#### Scenario: Key loaded from environment variable
- **WHEN** `QWEN_API_KEY` env var is set to a non-empty value
- **THEN** `LoadQwenApiKey()` returns that value

#### Scenario: Key loaded from secrets file fallback
- **WHEN** `QWEN_API_KEY` env var is empty or missing, but `~/.litellm/secrets.json` contains `QWEN_API_KEY`
- **THEN** `LoadQwenApiKey()` returns the value from the secrets file

#### Scenario: Missing key throws with actionable message
- **WHEN** neither env var nor secrets file has `QWEN_API_KEY`
- **THEN** `HostPreparationException` is thrown with message mentioning `QWEN_API_KEY` and `~/.litellm/secrets.json`

### Requirement: Qwen model default with priority resolution

The Host SHALL resolve the model for qwen in priority order: `--model` CLI argument, `UNICLAW_MODEL` env var, `QWEN_MODEL` env var, then `"qwen3.7-plus"` as hardcoded default.

#### Scenario: Explicit model via CLI flag
- **WHEN** user passes `--model qwen3.8-max-preview` with `--provider qwen`
- **THEN** the qwen provider is configured with model `qwen3.8-max-preview`

#### Scenario: QWEN_MODEL env var fallback
- **WHEN** no `--model` flag and no `UNICLAW_MODEL`, but `QWEN_MODEL=qwen3.7-plus` is set
- **THEN** the qwen provider uses `qwen3.7-plus`

#### Scenario: Hardcoded default
- **WHEN** no `--model`, `UNICLAW_MODEL`, or `QWEN_MODEL` is set
- **THEN** the qwen provider uses `qwen3.7-plus`

### Requirement: Base URL configurable via QWEN_BASE_URL

The Host SHALL read `QWEN_BASE_URL` environment variable for the Qwen endpoint URL, defaulting to `https://token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1`.

#### Scenario: Custom base URL
- **WHEN** `QWEN_BASE_URL` is set to a custom endpoint
- **THEN** the qwen provider sends requests to that URL

#### Scenario: Default base URL
- **WHEN** `QWEN_BASE_URL` is not set
- **THEN** the Alibaba Cloud Model Studio endpoint is used

### Requirement: Two-stage mode registers deepseek S2 provider

When `UNICLAW_VISION_MODE=two_stage` and provider is `qwen`, the Host SHALL register an additional `"deepseek"` provider in the provider dictionary for Stage 2 text-only reasoning.

#### Scenario: Two-stage mode adds deepseek provider
- **WHEN** `UNICLAW_VISION_MODE=two_stage` and `--provider qwen`
- **THEN** the provider dictionary contains both `"qwen"` and `"deepseek"` entries

#### Scenario: Single-stage mode does not add deepseek
- **WHEN** `UNICLAW_VISION_MODE` is not `two_stage` and `--provider qwen`
- **THEN** the provider dictionary contains only `"qwen"`

### Requirement: DeepSeek model configurable via DEEPSEEK_MODEL

The Stage 2 provider SHALL use the model from `DEEPSEEK_MODEL` env var, defaulting to `deepseek-v4-flash-0731`.

#### Scenario: Custom deepseek model
- **WHEN** `DEEPSEEK_MODEL=deepseek-v4-pro` and two-stage mode is enabled
- **THEN** the deepseek provider uses model `deepseek-v4-pro`
