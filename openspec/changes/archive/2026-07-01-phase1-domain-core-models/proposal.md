## Why

The C# rewrite (`feature/refactor`) shipped a rough first cut of the Domain layer, but it falls short of the refined Phase 1 design captured in [03-phase1-prd.md](../../../docs/refactor/03-phase1-prd.md):

- `BoundingBox` still carries `BoundingBoxPixel` + `ToPixel` (pixel conversion is I/O/平台细节, violates “Domain 无 I/O”).
- `TypeHint` still defines `Unknown` and falls back to it on unrecognized input — the PRD requires deleting `Unknown` and falling back to `Text`.
- `Operation` still allows `Wait`/`LongPress`; `Target` still allows `ResourceId`/`ElementType` — all out of scope for Phase 1 and removed by the PRD.
- `Content/` (12 types ported from `content_models.py`) and `Mappings/` (`ElementTypeMapper`, `AndroidWidgetClass`, …) are **not implemented at all**.
- There is no `DomainValidationException`; construction-time fail-fast validation is not enforced, so illegal objects can be silently constructed.

Phase 1 is the foundation every later layer (Graph, StateMachine, Trace, AI) builds on. Locking the core models — types, fields, invariants, immutability, domain parsing, ownership — to a single source of truth now prevents drift, accidental mutable state, and reverse dependencies that are expensive to undo later.

## What Changes

- **Correct Vision models (7 types)** to PRD §5.1: delete `BoundingBoxPixel`/`ToPixel`; normalize `BoundingBox` to `[0,1]` with `w/h>0`; delete `TypeHint.Unknown` and route unrecognized `FromString` to `Text` (precise → alias → fallback); restrict `Region.role`; make `FlattenedElement.bbox` nullable with `0.001` default and `confidence∈[0,1]`; nest `ScreenHints.Extra`; make `FlattenedScreen.elements` an `ImmutableArray` that is sorted by `(y,x)` at construction.
- **Port Content models (12 new types)** from `content_models.py`: `Coordinate`, `Direction`, `MenuInfo`, `MenuItemType`, `ExpectedAction`, `MenuItem`, `PopupInfo`, `PageAnalysis`, `VisitFingerprint`, `ContentNode`, `ContentTree`, `SimulationState` — as `record` types with construction validation (pydantic `BaseModel` → C# record + fail-fast).
- **Correct Common/Operations (3 types)** to PRD §5.3: `Operation.action∈{click,swipe,back,input_text,no_action}` (delete `Wait`/`LongPress`); `Target.by∈{text,coordinate,ui_index}` (delete `ResourceId`/`ElementType`); `RestoreAction` reuses Operation validation.
- **Port Mappings** from `element_type_mapper.py`: `AndroidWidgetClass` (enum), `ElementTypeMapper` (class), and `map_android_class`/`to_menu_item_type`/`to_expected_action` as static methods depending on `Models.Content` and `Models.Vision`.
- **Add cross-cutting validation & immutability**: `DomainValidationException` (carries field name + illegal value); construction-time validation on every model; `readonly record struct`/`sealed record class`; collections as `ImmutableArray<T>` with `with`-independence.
- **Minimal single-direction serialization**: objects → JSON (camelCase via global `PropertyNameCasePolicy=CamelCase`, `[JsonPropertyName]` only as override); delete hand-written `to_dict`/`from_dict`; **no DTO**; JSON → object round-trip is an acknowledged, deferred limitation.
- **Spike first** (R-3/R-5): `BoundingBox` (construction throws), `FlattenedScreen` (`ImmutableArray` + sort + `with`), one enum (`TypeHint`) — to validate .NET 8 construction-throw and `ImmutableArray` behavior before mass porting.
- **Tests**: construction-validation negative suites, `FromString` paths, `with`-independence, full-table mapper scan, single-direction serialization only.

## Capabilities

### New Capabilities

- `domain-vision-models`: Vision domain models (`BoundingBox`, `TypeHint`, `SelectionState`, `Region`, `FlattenedElement`, `ScreenHints`, `FlattenedScreen`) with normalized coordinates, restricted enums/roles, `ImmutableArray` collections, construction-time sorting, and `FromString` domain parsing.
- `domain-content-models`: Content/menu-structure domain models (12 types) ported from `content_models.py` as validated immutable records; the single source for `PageAnalysis`/`PopupInfo` (AI-layer simplified versions defer to a later phase).
- `domain-common-operations`: `Operation`/`Target`/`RestoreAction` with PRD-restricted `action`/`by` enumerations and default-empty params.
- `domain-type-mappings`: `AndroidWidgetClass` enum + `ElementTypeMapper` static methods mapping Android widget classes → `TypeHint`/`MenuItemType`/`ExpectedAction`, depending only on `Models.Content`/`Models.Vision`.
- `domain-validation-immutability`: `DomainValidationException`; construction-time fail-fast validation; `ImmutableArray<T>` collections with `with`-independent copies; single-direction JSON serialization (camelCase), no DTO.

### Modified Capabilities

- None at the capability-contract level (no upstream OpenSpec spec yet exists for the C# Domain layer; this change establishes the baseline).

## Impact

- **Affected Code**:
  - `src/UniClaw.Core/Domain/Models/Vision/*.cs` — corrections (deletions + validation + `ImmutableArray`).
  - `src/UniClaw.Core/Domain/Models/Common/*.cs` — enumeration reductions + validation.
  - `src/UniClaw.Core/Domain/Models/Content/` — **new** (12 files).
  - `src/UniClaw.Core/Domain/Mappings/` — **new** (`AndroidWidgetClass.cs`, `ElementTypeMapper.cs`).
  - `src/UniClaw.Core/Domain/DomainValidationException.cs` — **new**.
  - `tests/UniClaw.Core.Tests/Domain/**` — new/updated test suites.

- **API Changes (C# Domain surface)**:
  - `BoundingBox`: remove `BoundingBoxPixel` type and `ToPixel(...)`; normalize to `[0,1]`, throw on `w/h<=0`.
  - `TypeHint`: remove `Unknown`; `FromString` fallback → `Text`; add `Values`/`IsValid`.
  - `SelectionState`: `FromString` alias mapping (`checked`/`highlight`→`Selected`; `inactive`/`hidden`→`Disabled`).
  - `FlattenedScreen`: `elements` becomes `ImmutableArray<FlattenedElement>` sorted by `(y,x)` at construction.
  - `Operation`/`RestoreAction`: `action` reduced to `{click,swipe,back,input_text,no_action}`.
  - `Target`: `by` reduced to `{text,coordinate,ui_index}`.
  - New: `DomainValidationException`, 12 Content types, `AndroidWidgetClass`, `ElementTypeMapper`.

- **Dependencies**:
  - .NET 8 (LTS), BCL only (`System.Text.Json`, `System.Collections.Immutable`). No upper-layer or third-party domain dependencies.
  - `Mappings` depends on `Models.Content` + `Models.Vision` (all in-scope).
  - Downstream (Phase 2): Graph layer will reference `Common` (Operation/Target/RestoreAction).

- **Systems**:
  - Domain layer only — no I/O, no upper-layer coupling. No production runtime/ADB/pixel-conversion impact.
  - Risk R-4: `PageAnalysis`/`PopupInfo` simplified versions still live in `AI/IAIStrategyAdvisor.cs`; they are not replaced this phase (single-source transition deferred).

- **Known Limitations (deferred)**:
  - JSON → object round-trip is not guaranteed (record construction validation will throw on missing/illegal fields). Resolved in a later phase when persistence/config-load demands it (DTO/factory or relaxed validation).
