## 1. Core Types

- [x] 1.1 Create `ResolvedPrompt.cs` — sealed record class with positional fields System + User, namespace UniClaw.Core.UniBrain
- [x] 1.2 Create `PromptTemplate.cs` — sealed record class with Capability, SystemPrompt, UserPrompt, Variables (ImmutableArray<string>). Constructor fail-fast validation: Capability non-empty, at least one prompt non-empty, declared variables appear in template text (D-2). Resolve method: declared-variable iteration via string.Replace (D-1), missing → DomainValidationException, extra → ignored, undeclared → untouched. Returns ResolvedPrompt (D-3).
- [x] 1.3 Create `IPromptLibrary.cs` — interface with GetTemplate(capability) → PromptTemplate?, GetCapabilities() → IReadOnlyList<string>, ValidateCapability(capability) → bool (D-4)
- [x] 1.4 Create `PromptLibrary.cs` — sealed class implementing IPromptLibrary, ImmutableDictionary<string, PromptTemplate> backed. Two constructors: ImmutableDictionary and params PromptTemplate[]. Duplicate capability → ArgumentException.

## 2. Tests

- [x] 2.1 Create `PromptTemplateTests.cs` — 10 scenarios: normal replacement, missing variable → DVE, extra variables ignored, no-variables template, variable in system prompt, underscore/number variable names, repeated variable, empty capability → DVE, both prompts empty → DVE, declared variable not in text → DVE
- [x] 2.2 Create `PromptLibraryTests.cs` — 5 scenarios: GetTemplate found, GetTemplate unknown → null, GetCapabilities, ValidateCapability true, ValidateCapability false. Also test duplicate capability → ArgumentException.
- [x] 2.3 Run full test suite — `dotnet test src/UniClaw.Core.sln` — verify 849 existing + 15 new = 864 all pass, 0 errors

## 3. Verification

- [x] 3.1 Build check — `dotnet build src/UniClaw.Core.sln` — 0 errors
- [x] 3.2 Architecture guard check — existing enum/DI guard tests still pass (no new enum values, no upward dependency from Domain)
