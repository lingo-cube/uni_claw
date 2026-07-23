## Why

IModelProvider is pure transport (call + retry + timeout). ModelRequest only accepts raw Prompt + SystemPrompt strings. Sub-interface implementations (ClaudePageAnalyzer, DeepSeekTraversalAdvisor etc.) must manage prompt templates and variable injection themselves, leading to prompts scattered across implementations, no variable validation, no centralized prompt registry, and misalignment with Python PromptManager.

## What Changes

- Add `PromptTemplate` sealed record class: capability key + system/user template text + declared variables (ImmutableArray). Constructor fail-fast validates capability non-empty, at least one prompt non-empty, and declared variables appear in template text. `Resolve()` method does declared-variable iteration (`string.Replace` per variable), missing required variables throw DomainValidationException, extra variables silently ignored, undeclared `{foo}` stays untouched.
- Add `ResolvedPrompt` sealed record class: named return type for `Resolve()` with `System` + `User` fields (replaces raw ValueTuple for semantic clarity).
- Add `IPromptLibrary` interface: `GetTemplate(capability) → PromptTemplate?`, `GetCapabilities() → IReadOnlyList<string>`, `ValidateCapability(capability) → bool`.
- Add `PromptLibrary` sealed class (default implementation): ImmutableDictionary-backed, params-array convenience constructor, immutable after construction.

## Capabilities

### New Capabilities

- `prompt-template-engine`: Prompt template definition (PromptTemplate + ResolvedPrompt), variable injection via declared-variable iteration, and centralized template registry (IPromptLibrary + PromptLibrary).

### Modified Capabilities

(none — IPromptLibrary is injected into sub-interface implementations, not surfaced on IUniBrain facade)

## Impact

- **New files**: 4 types in `src/UniClaw.Core/UniBrain/` (PromptTemplate.cs, ResolvedPrompt.cs, IPromptLibrary.cs, PromptLibrary.cs)
- **New test files**: 2 in `tests/UniClaw.Core.Tests/UniBrain/` (PromptTemplateTests.cs — 10 scenarios, PromptLibraryTests.cs — 5 scenarios)
- **No existing code changes**: Phase 1 is pure additive. Sub-interface implementations will inject IPromptLibrary in future phases when they move from stubs to real implementations.
- **Python alignment**: Maps to `PromptTemplate` dataclass + `PromptManager` class (src/ai/prompts/manager.py). Version, metadata, file I/O, hot reload deferred to Phase 3-B.
