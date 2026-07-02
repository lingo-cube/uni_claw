## Context

The C# rewrite of `uni_claw` (branch `feature/refactor`) is rebuilding the Python codebase on .NET 8. Phase 1 establishes the Domain layer — the pure data + invariants + domain-parsing core that every upper layer (Graph, StateMachine, Traversal, Trace, AI) will depend on.

A first cut of the Domain layer already compiles and passes tests (`src/UniClaw.Core/Domain/Models/Vision/*`, `…/Common/*`), but it was written before the refined Phase 1 PRD ([03-phase1-prd.md](../../../docs/refactor/03-phase1-prd.md)) and diverges from it in ways that matter:

- It retains out-of-scope enum values (`Wait`/`LongPress` on `Operation`; `ResourceId`/`ElementType` on `Target`; `Unknown` on `TypeHint`) and a pixel-space type (`BoundingBoxPixel` + `ToPixel`) that smuggles platform I/O into Domain.
- It lacks `DomainValidationException`, so illegal objects can be constructed silently.
- It lacks the entire `Content` model set (12 types) and `Mappings` (`ElementTypeMapper`).
- Collections are not yet uniformly `ImmutableArray<T>` with construction-time sorting.

The PRD fixes the scope: **core-model establishment** — types/fields/invariants/immutability/domain-parsing/ownership. Serialization is reduced to minimal single-direction (object → JSON); no DTO; complex persistence/round-trip is deferred.

**Source of truth** for per-type fields/actions: [02 §5](../../docs/refactor/02-phase1-domain-refactor.md). This design does not re-list every field; it encodes the decisions and invariants.

**Constraints**:
- .NET 8 (LTS), BCL only. No upper-layer or third-party domain dependencies.
- Domain has no I/O (no file/DB/network/ADB/pixel conversion) and no reverse dependency on upper layers.
- One type, one location (no cross-namespace duplicate definitions).
- Single source for `PageAnalysis`/`PopupInfo`: the Content layer owns the full version; the AI-layer simplified versions remain only until a later phase replaces them (R-4).
- Records are the data source of truth; interfaces are for behavior/services only — **no interfaces over data records**.

**Stakeholders**:
- Phase 2+ authors (Graph/StateMachine/Trace/AI) — they consume these contracts.
- Reviewers of the PRD (this design must be reviewable against §3–§10 of the PRD).

## Goals / Non-Goals

**Goals**:
- Bring the Vision (7) and Common (3) types into full conformance with the PRD (deletions, restrictions, validation, immutability, parsing).
- Port the Content (12) types and Mappings from their Python sources as validated immutable records.
- Enforce fail-fast construction validation via `DomainValidationException` (field name + illegal value).
- Make all Domain collections `ImmutableArray<T>` with `with`-independent copies; sort `FlattenedScreen.elements` by `(y,x)` at construction.
- Provide single-direction JSON (object → JSON, camelCase); delete hand-written `to_dict`/`from_dict`; introduce no DTO.
- Validate the riskiest .NET 8 behaviors (construction throws, `ImmutableArray` + `with`) with a 3-type Spike before mass porting.
- Domain-layer test coverage > 80%, all green; `dotnet build` 0 errors / 0 warnings.

**Non-Goals**:
- JSON → object round-trip / persistence / config loading (deferred; R-6).
- DTOs (explicitly out — PRD §4.4, §6).
- Replacing the AI-layer simplified `PageAnalysis`/`PopupInfo` (deferred; R-4).
- Graph / StateMachine / Traversal / Trace layer work (Phase 2+).
- Real device / ADB / pixel conversion (never in Domain).
- Horizontal scrolling, nested scroll containers, gesture simulation (out of scope).
- 1:1 mirroring of Python file structure (杂货袋 files are split by responsibility — PRD §4.7).

## Decisions

### 1. Records as the single data source; no interfaces over data

**Decision**: Domain data types are `readonly record struct` / `sealed record class`. The Content layer owns the full `PageAnalysis`/`PopupInfo`; upper layers reference the concrete type directly — no simplified copy, no interface abstraction over data.

**Rationale**: Matches PRD §4.10 (no interfaces over data records) and §5.2 (single-source principle). Interfaces are reserved for behavior/services (e.g. `IAIStrategyAdvisor`). Avoids parallel simplified types drifting apart.

**Alternatives Considered**:
- Interface-per-record (`IPageAnalysis`): rejected — duplicates the data contract, invites drift, gains nothing (records already carry equality/copy semantics).
- Keep AI-layer simplified `PageAnalysis`/`PopupInfo` as a parallel source: rejected — violates single-source (R-4 is a transition risk, not an endorsed end state).

### 2. Construction-time fail-fast validation via `DomainValidationException`

**Decision**: Each record validates in its primary constructor. Illegal values (zero/negative width/height, `confidence` out of `[0,1]`, empty enum, illegal `action`/`by`/`role`, etc.) throw `DomainValidationException(fieldName, illegalValue)`. No silent coercion to a default.

**Rationale**: PRD §4.3, §6, §7.1. Fail-fast at the boundary prevents illegal state from propagating into Graph/StateMachine.

**Alternatives Considered**:
- Silent defaulting (clamp/normalize): rejected — masks bugs upstream.
- Validated factory + public mutable constructor: rejected — records should not expose mutability; validation belongs in the constructor.

**Risk (R-3)**: .NET 8 primary-constructor throws must be confirmed to surface correctly during both direct construction and (later) deserialization. Mitigated by the Spike (Decision 5) and by accepting that JSON → object round-trip will throw on illegal/missing input (deferred limitation, R-6).

### 3. `ImmutableArray<T>` for all Domain collections; `with` independence

**Decision**: Collection-valued fields are `ImmutableArray<T>`. `FlattenedScreen.elements` is sorted by `(y,x)` at construction. `with` expressions produce independent copies (no shared mutable backing store).

**Rationale**: PRD §3 (immutability), §4.8, §7.3, §9 R-5. `ImmutableArray` is a struct, allocation-light, and structurally immutable.

**Alternatives Considered**:
- `IReadOnlyList<T>` over a `List<T>`: rejected — the backing `List<T>` can be mutated by the originator; not truly immutable.
- `FrozenSet<T>`/`FrozenDictionary<TKey,TValue>`: rejected for now — keyed lookup is not a Domain requirement at this phase; revisit if a model genuinely needs set/map semantics.

**Risk (R-5)**: `ImmutableArray` behavior under `with` and serialization must be confirmed by the Spike.

### 4. Minimal single-direction serialization; no DTO; camelCase

**Decision**: Configure `JsonSerializerOptions { PropertyNameCasePolicy = CamelCase }` globally. `[JsonPropertyName]` is used only to override. Delete all hand-written `ToDictionary`/`FromDictionary`. No DTOs. Only object → JSON is guaranteed; JSON → object is an acknowledged deferred limitation.

**Rationale**: PRD §6 (序列化最小·单向), §4.4. Keeps Phase 1 focused on models; avoids speculative persistence infrastructure (YAGNI).

**Alternatives Considered**:
- Introduce DTOs now for clean round-trip: rejected — premature; PRD explicitly forbids DTO this phase.
- Keep snake_case to mirror Python: rejected — PRD §6 mandates camelCase, no Python baggage.

### 5. Spike before mass porting (R-3 / R-5)

**Decision**: Before porting Content/Mappings, implement three representative types end-to-end (no DTO, no round-trip):
- `BoundingBox` — construction throws `DomainValidationException`.
- `FlattenedScreen` — `ImmutableArray` + construction sort + `with`-independent copy.
- `TypeHint` (enum) — `FromString` precise/alias/fallback.

**Rationale**: PRD §8. Validates the two riskiest .NET 8 behaviors cheaply before committing to the 12-type Content port and the full mapper table.

**Alternatives Considered**:
- Skip the Spike and port everything linearly: rejected — R-3/R-5 are exactly the kind of risks that are cheap to de-risk early and expensive to discover late.

### 6. Domain parsing stays as static methods, tested independently

**Decision**: `FromString` and alias mappings remain `static` methods on the enum/record (domain semantics, not serialization). They are unit-tested separately from JSON.

**Rationale**: PRD §6 (领域解析), §7.2. Parsing is a domain concern, not a serialization concern; conflating them is what made the Python layer tangled.

### 7. Mappings depend only on Content + Vision

**Decision**: `ElementTypeMapper` / `map_android_class` / `to_menu_item_type` / `to_expected_action` are static methods in `Domain/Mappings/`, depending on `Models.Content` (`MenuItemType`/`ExpectedAction`) and `Models.Vision` (`TypeHint`). No dependency on upper layers.

**Rationale**: PRD §5.4, §2 (dependency direction `Mappings → Models.{Content,Vision}`).

**Risk (R-2)**: the Android widget-class → type table is large and data-heavy; must be checked row-by-row against `element_type_mapper.py`. Mitigated by a full-table scan test (§7.4).

## Risks / Open Questions

- **R-1 (workload)**: 12 Content types is the bulk of the effort. Mitigation: per-type tasks in `tasks.md`, ported in dependency order; reuse the Spike patterns.
- **R-2 (mapper table)**: `element_type_mapper` mapping volume. Mitigation: full-table scan test mirrors the Python source verbatim.
- **R-3 (construction throws)**: RESOLVED by Spike. `.NET 8` primary-constructor/record throw behavior confirmed: explicit constructors with `init` properties throw at construction as expected. **Key finding**: `readonly record struct` has an implicit parameterless zero-init constructor (`new T()` / `default(T)`) that **bypasses** the validating primary constructor, silently producing an illegal (zero-area) object — a direct violation of PRD §4.3. **Resolution**: all invariant-bearing Domain model types are `sealed record class` (no parameterless ctor; `default` = null, never an illegal value-object). `record struct` is reserved for value types with no invariants (none in Phase 1).
- **R-5 (ImmutableArray + with/serialization)**: RESOLVED by Spike. `ImmutableArray<T>` is a struct; `with` on a `sealed record class` copies the struct field by value, so replacing the field yields a fully independent collection (original unchanged). `System.Text.Json` serializes `ImmutableArray<T>` / `ImmutableDictionary<TKey,TValue>` natively. **Finding**: a `default(ImmutableArray<T>)` (uninitialized) is distinct from empty and serializes/iterates incorrectly — construction must reject it via `IsDefault` and normalize to `Empty` where empty is valid (e.g. `ScreenHints.Regions`).
- **R-4 (PageAnalysis/PopupInfo dual existence)**: AI-layer simplified versions coexist until a later phase. Mitigation: documented as deferred; single-source is the target, not the starting state.
- **R-6 (single-direction serialization)**: JSON → object round-trip not guaranteed. Mitigation: acknowledged limitation; tests assert object → JSON only (§7.5). Reopened when persistence/config-load arrives.
- **Open**: exact namespace naming for `Common`/`Mappings` (PRD §2 note allows `Operations/`/`Services/` rename during implementation) — non-blocking; finalize at implementation time, keep one-type-one-location.
