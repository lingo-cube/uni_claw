## Requirements

### Requirement: PromptTemplate defines a capability-keyed prompt with variable placeholders

PromptTemplate SHALL be a sealed record class with fields: Capability (string), SystemPrompt (string), UserPrompt (string), Variables (ImmutableArray<string>). The template SHALL use `{variable_name}` single-brace placeholder syntax aligned with Python PromptTemplate. Constructor SHALL perform fail-fast validation:
1. Capability MUST be non-null and non-whitespace — violation → DomainValidationException("PromptTemplate.Capability", value)
2. At least one of SystemPrompt or UserPrompt MUST be non-null and non-whitespace — violation → DomainValidationException("PromptTemplate.SystemPrompt+UserPrompt", "(both empty)")
3. Every declared variable MUST appear in SystemPrompt or UserPrompt as `{var_name}` — violation → DomainValidationException("PromptTemplate.Variables", message identifying the undeclared variable)

#### Scenario: Valid template constructed
- **WHEN** PromptTemplate is created with Capability="page_analysis", SystemPrompt="Analyze {goal}", UserPrompt="", Variables=["goal"]
- **THEN** construction succeeds with SystemPrompt="Analyze {goal}", UserPrompt="", Variables=["goal"]

#### Scenario: Empty capability rejected
- **WHEN** PromptTemplate is created with Capability="", SystemPrompt="test", UserPrompt="test", Variables=[]
- **THEN** DomainValidationException is thrown with FieldName="PromptTemplate.Capability"

#### Scenario: Both prompts empty rejected
- **WHEN** PromptTemplate is created with Capability="test", SystemPrompt="", UserPrompt="", Variables=[]
- **THEN** DomainValidationException is thrown with FieldName="PromptTemplate.SystemPrompt+UserPrompt"

#### Scenario: Declared variable not in template text rejected
- **WHEN** PromptTemplate is created with Capability="test", SystemPrompt="hello", UserPrompt="", Variables=["missing_var"]
- **THEN** DomainValidationException is thrown with FieldName="PromptTemplate.Variables" and message contains "missing_var"

### Requirement: PromptTemplate.Resolve replaces declared variables with provided values

Resolve SHALL accept an IReadOnlyDictionary<string, string> and return a ResolvedPrompt. Resolve SHALL iterate the Variables list and perform string.Replace("{var_name}", value) for each declared variable on both SystemPrompt and UserPrompt. Missing required variables SHALL throw DomainValidationException("PromptTemplate.Resolve") listing all missing variable names. Extra variables not in the Variables list SHALL be silently ignored. Undeclared `{foo}` patterns SHALL remain untouched in the output.

#### Scenario: All variables correctly replaced
- **WHEN** template has Variables=["goal", "page_type"] and UserPrompt="Goal: {goal}\nType: {page_type}" is resolved with {"goal": "Find settings", "page_type": "home"}
- **THEN** ResolvedPrompt.User equals "Goal: Find settings\nType: home"

#### Scenario: Missing variable triggers DomainValidationException
- **WHEN** template has Variables=["goal", "ctx"] and is resolved with {"goal": "test"} (missing "ctx")
- **THEN** DomainValidationException is thrown listing "ctx" as missing

#### Scenario: Extra variables silently ignored
- **WHEN** template has Variables=["goal"] and is resolved with {"goal": "test", "extra": "unused"}
- **THEN** Resolve succeeds and "extra" is not used in replacement

#### Scenario: No-variables template returns unchanged
- **WHEN** template has Variables=Empty and SystemPrompt="static prompt"
- **THEN** ResolvedPrompt.System equals "static prompt" unchanged

#### Scenario: Variable replaced in both system and user prompts
- **WHEN** template has Variables=["role"] and SystemPrompt="You are {role}", UserPrompt="Act as {role}"
- **THEN** both ResolvedPrompt.System and ResolvedPrompt.User contain the replaced value

#### Scenario: Repeated variable occurrences all replaced
- **WHEN** template has Variables=["goal"] and UserPrompt="{goal} and {goal}"
- **THEN** ResolvedPrompt.User has both occurrences replaced

#### Scenario: Undeclared brace patterns stay untouched
- **WHEN** template has Variables=["goal"] and UserPrompt="{goal} — JSON example: {\"key\": \"val\"}"
- **THEN** only {goal} is replaced; {\"key\": \"val\"} remains as-is

### Requirement: ResolvedPrompt is a named return type for resolved prompts

ResolvedPrompt SHALL be a sealed record class with positional fields System (string) and User (string). ResolvedPrompt fields SHALL directly map to ModelRequest.Prompt (User) and ModelRequest.SystemPrompt (System).

#### Scenario: ResolvedPrompt maps to ModelRequest
- **WHEN** ResolvedPrompt is created with System="You are an expert", User="Analyze screen"
- **THEN** ModelRequest can be constructed with Prompt=resolved.User, SystemPrompt=resolved.System

### Requirement: IPromptLibrary provides capability-keyed template retrieval

IPromptLibrary SHALL be an interface with three methods:
1. GetTemplate(string capability) → PromptTemplate? — returns null if capability not found (no exception)
2. GetCapabilities() → IReadOnlyList<string> — lists all registered capability keys
3. ValidateCapability(string capability) → bool — returns true if capability is registered, false otherwise (no exception)

IPromptLibrary SHALL NOT be surfaced on IUniBrain facade — prompt management is a sub-interface implementation concern.

#### Scenario: GetTemplate returns matching template
- **WHEN** PromptLibrary contains capability "page_analysis"
- **THEN** GetTemplate("page_analysis") returns the PromptTemplate with that capability

#### Scenario: GetTemplate returns null for unknown capability
- **WHEN** PromptLibrary does not contain capability "unknown"
- **THEN** GetTemplate("unknown") returns null

#### Scenario: ValidateCapability returns true for existing capability
- **WHEN** PromptLibrary contains capability "page_analysis"
- **THEN** ValidateCapability("page_analysis") returns true

#### Scenario: ValidateCapability returns false for missing capability
- **WHEN** PromptLibrary does not contain capability "unknown"
- **THEN** ValidateCapability("unknown") returns false

### Requirement: PromptLibrary is an immutable in-memory template registry

PromptLibrary SHALL be a sealed class implementing IPromptLibrary, backed by ImmutableDictionary<string, PromptTemplate>. PromptLibrary SHALL provide two constructors:
1. PromptLibrary(ImmutableDictionary<string, PromptTemplate> templates) — for DI container use
2. PromptLibrary(params PromptTemplate[] templates) — convenience constructor that builds ImmutableDictionary from array (key = Capability)

Duplicate capability keys in the params constructor SHALL throw ArgumentException (fail-fast). PromptLibrary SHALL be immutable after construction — no template addition or removal.

#### Scenario: Params constructor builds dictionary from templates
- **WHEN** PromptLibrary is created with two PromptTemplate instances (Capability="a", Capability="b")
- **THEN** GetCapabilities() returns ["a", "b"]

#### Scenario: Duplicate capability in params constructor throws ArgumentException
- **WHEN** PromptLibrary is created with two PromptTemplate instances with the same Capability value
- **THEN** ArgumentException is thrown