## ADDED Requirements

### Requirement: UniBrainFactory builds a UniBrainService from UniBrainConfig and credentials

The Core layer SHALL provide a `UniBrainFactory` (or equivalent Core builder) that accepts a `UniBrainConfig` together with provider credentials and returns a fully assembled `IUniBrain` (`UniBrainService`). The factory SHALL be the single AI injection point Host uses — Host hands `UniBrainConfig` + credentials to the factory and receives `IUniBrain`; Host SHALL NOT hand-`new` `PageAnalyzer`, `TraversalAdvisor`, `TextUnderstanding`, or `IModelProvider` instances to assemble the facade.

The factory SHALL internally:

- Resolve the `IModelProvider`/`IModelRouter` transport backing the selected providers.
- Construct the three sub-interface implementations (`IPageAnalyzer`, `ITraversalAdvisor`, `ITextUnderstanding`) per `UniBrainConfig.DefaultProvider` and `UniBrainConfig.CapabilityRouting`.
- Return a `UniBrainService` composing those three sub-interface instances.

Real visual providers (`AnthropicModelProvider`, `OpenAiCompatibleVisionProvider`) SHALL be selected by `UniBrainConfig.DefaultProvider` and/or `CapabilityRouting` inside the factory; Host supplies credentials via config, never hardcodes a provider. Mock/replay providers (`MockModelProvider`) SHALL likewise be selectable by config so that the replay link shape is a config selection inside UniBrain, not a Host-owned provider.

#### Scenario: Factory builds facade from config with default provider

- **WHEN** `UniBrainFactory.Create` is called with a `UniBrainConfig` whose `DefaultProvider` is `"anthropic"` and `CapabilityRouting` is null, plus valid Anthropic credentials
- **THEN** the factory resolves an `AnthropicModelProvider` from the credentials
- **THEN** the factory constructs `IPageAnalyzer`, `ITraversalAdvisor`, and `ITextUnderstanding` implementations all backed by that provider
- **THEN** the factory returns a `UniBrainService` composing those three instances
- **THEN** the returned `IUniBrain.PageAnalyzer` is backed by the Anthropic provider, not a mock

#### Scenario: Factory honors capability routing

- **WHEN** `UniBrainFactory.Create` is called with `UniBrainConfig.CapabilityRouting = {"page_analysis": "anthropic", "traversal_advisor": "deepseek", "text_understanding": "deepseek"}`
- **THEN** the factory constructs the `IPageAnalyzer` backed by the Anthropic provider and the `ITraversalAdvisor`/`ITextUnderstanding` backed by the DeepSeek provider
- **THEN** the returned `UniBrainService` composes those three sub-interface implementations

#### Scenario: Mock provider selected by config produces a replay facade

- **WHEN** `UniBrainFactory.Create` is called with `UniBrainConfig.DefaultProvider = "mock"` and a mock fixture (no real credentials)
- **THEN** the factory selects `MockModelProvider` (with vision replay support) instead of any real provider
- **THEN** the returned `IUniBrain` is a fully composed `UniBrainService` whose sub-interfaces replay from the mock fixture
- **THEN** no real provider class (`AnthropicModelProvider`, `OpenAiCompatibleVisionProvider`) is instantiated

#### Scenario: Credentials supplied via config, not hardcoded

- **WHEN** Host assembles `IUniBrain` for a real provider
- **THEN** Host passes provider credentials into the factory through the credential parameter/object, not by constructing a provider with a hardcoded API key
- **THEN** the factory, not Host, instantiates the concrete provider class from those credentials

### Requirement: UniBrainConfig remains credential-free; credentials flow through a separate channel

`UniBrainConfig` SHALL NOT contain provider credentials or API keys (preserving the existing credential-free invariant). The factory SHALL accept credentials through a parameter/object distinct from `UniBrainConfig` — credentials and composition configuration travel in separate channels.

#### Scenario: UniBrainConfig has no credential fields

- **WHEN** the `UniBrainConfig` record is inspected
- **THEN** it exposes only `DefaultProvider`, `CapabilityRouting`, and `EnableTrace`
- **THEN** no field or property of `UniBrainConfig` is an API key, token, or secret

#### Scenario: Factory receives credentials separately from UniBrainConfig

- **WHEN** `UniBrainFactory.Create` is invoked
- **THEN** the call signature accepts `UniBrainConfig` and a separate credentials parameter/object
- **THEN** credentials are not extracted from the `UniBrainConfig` argument

### Requirement: Host assembles IUniBrain only through the factory

Host SHALL obtain `IUniBrain` exclusively via `UniBrainFactory` (or equivalent Core builder). Host SHALL NOT directly construct `PageAnalyzer`, `TraversalAdvisor`, `TextUnderstanding`, or `IModelProvider` instances. The composition of the three sub-interfaces into `UniBrainService` SHALL happen inside the factory, not in Host code.

#### Scenario: Host composition produces IUniBrain via the factory with no direct sub-interface construction

- **WHEN** Host configures its AI capabilities for a run
- **THEN** Host invokes `UniBrainFactory.Create` with a `UniBrainConfig` and credentials
- **THEN** Host receives an `IUniBrain` and injects it as the single AI seam
- **THEN** Host code contains no `new PageAnalyzer(...)`, `new TraversalAdvisor(...)`, `new TextUnderstanding(...)`, or `new <Provider>ModelProvider(...)` construction

#### Scenario: No Host-owned duplicate AI provider

- **WHEN** a mock/replay run is configured
- **THEN** Host selects the mock via `UniBrainConfig.DefaultProvider = "mock"` and obtains `IUniBrain` from the factory
- **THEN** Host defines no Host-local `IModelProvider` implementation duplicating Core's `MockModelProvider`