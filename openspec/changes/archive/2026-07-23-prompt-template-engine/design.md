## Context

UniBrain AI layer currently has 16 files: facade (IUniBrain + UniBrainService), 4 sub-interfaces (IModelProvider, IPageAnalyzer, ITraversalAdvisor, ITextUnderstanding), result types, and config. All provider implementations are stubs throwing NotImplementedException. No prompt-related code exists — ModelRequest takes raw Prompt + SystemPrompt strings, and sub-interface implementations will need to manage prompt content when they move from stubs to real code.

Python has a mature PromptManager system (src/ai/prompts/manager.py): PromptTemplate dataclass with 6 fields (capability, version, system_prompt, user_template, variables, metadata), YAML front matter file storage, variable injection via str.replace, and hot reload. However, version control and hot reload are dormant in practice — no production code uses them.

The design spec is already written at `docs/superpowers/specs/2026-07-23-prompt-template-engine-design.md`.

## Goals / Non-Goals

**Goals:**
- Centralize prompt template management in a reusable, type-safe library
- Provide fail-fast variable validation (missing variables caught at Resolve, declared-but-unused caught at construction)
- Align with Python PromptManager's core mechanics (capability key, declared-variable iteration, {variable} placeholder syntax)
- Enable DI injection of IPromptLibrary into sub-interface implementations
- Maintain project conventions: sealed record class, ImmutableArray, DomainValidationException

**Non-Goals:**
- File-based template loading (YAML/Markdown) — deferred to Phase 3-B
- Hot reload — deferred to Phase 3-B
- Version control — deferred (dormant in Python)
- Conditional/loop template syntax ({#if}, {#each}) — YAGNI
- Built-in business prompt templates — Host project responsibility
- Surfacing IPromptLibrary on IUniBrain facade — prompt management is sub-interface internal concern

## Decisions

### D-1: Variable replacement — declared-variable iteration

Iterate `Variables` list, do `string.Replace("{var_name}", value)` for each declared variable. Undeclared `{foo}` stays untouched (safe for JSON/code examples). Extra input variables silently ignored.

**Alternative rejected**: Regex `\{(\w+)\}` scan — would reject templates with literal brace content like JSON examples `{\"key\": \"val\"}`.

### D-2: Constructor validation — declared variables must appear in template text

Every name in `Variables` must appear as `{var_name}` in SystemPrompt or UserPrompt. Catches typos at construction (declaring "goal" but writing {gola}). Folds Python's separate `validate_prompt()` into fail-fast construction.

### D-3: ResolvedPrompt named return type

`Resolve()` returns `ResolvedPrompt` (sealed record class: System + User) instead of raw `(string, string)` ValueTuple. Named types give IDE discoverability and self-documenting API. Aligns with project convention.

**Alternative rejected**: Raw ValueTuple — requires positional memory (item1=system, item2=user) with no semantic clarity.

### D-4: IPromptLibrary includes ValidateCapability

3 methods: GetTemplate, GetCapabilities, ValidateCapability. ValidateCapability returns bool (no exception) — diagnostic method aligned with Python's `validate_prompt()`. GetTemplate returns PromptTemplate? (null = not found).

## Risks / Trade-offs

- **[Risk: Undeclared {var} patterns silently ignored]** → Mitigation: D-2 construction validation catches declared-but-not-present variables. Undeclared {foo} staying untouched is intentional — safe for JSON/code examples. If a user mistakenly writes {gola} instead of {goal}, D-2 catches it because "goal" won't appear in text.
- **[Risk: string.Replace is case-sensitive and doesn't support partial matches]** → Mitigation: Variable names must be exact identifiers (alphanumeric + underscore). string.Replace matches exactly, which is correct for this use case.
- **[Risk: PromptLibrary params constructor throws ArgumentException on duplicate capability]** → Mitigation: This is a programming error (registering same capability twice), not a domain validation error. ArgumentException is appropriate. Future file-based loader may need different handling.
