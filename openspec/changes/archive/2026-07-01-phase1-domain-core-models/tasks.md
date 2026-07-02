## 1. Spike — model pattern validation (R-3 / R-5)

- [x] 1.1 Create `src/UniClaw.Core/Domain/DomainValidationException.cs` with field-name + illegal-value carriers
- [x] 1.2 Spike `BoundingBox`: normalize to `[0,1]`, `w/h>0`, construction throws `DomainValidationException`; no `BoundingBoxPixel`/`ToPixel`
- [x] 1.3 Spike `FlattenedScreen`: `ImmutableArray<FlattenedElement>` + construction sort by `(y,x)` + `with`-independent copy
- [x] 1.4 Spike `TypeHint` enum: delete `Unknown`; `FromString` precise → alias → fallback `Text`; add `Values`/`IsValid`
- [x] 1.5 Write spike tests asserting: construction-throw behavior on .NET 8, `ImmutableArray` `with` independence, `FromString` paths
- [x] 1.6 Verify: `dotnet test` for the three spike types is green; record any .NET 8 behavior surprises in design.md

## 2. Validation & serialization foundation

- [x] 2.1 Finalize `DomainValidationException` (message format: field name + illegal value) and place under `Domain/`
- [x] 2.2 Configure global `JsonSerializerOptions { PropertyNameCasePolicy = CamelCase }` for Domain serialization
- [x] 2.3 Confirm no hand-written `ToDictionary`/`FromDictionary` exist on Domain records (delete any found)
- [x] 2.4 Confirm no DTO types are introduced in Domain
- [x] 2.5 Add a serialization test base: object → JSON (camelCase) only; explicitly skip JSON → object round-trip

## 3. Vision model corrections (7 types) — PRD §5.1

- [x] 3.1 `BoundingBox`: remove `BoundingBoxPixel` type and `ToPixel(...)`; enforce `[0,1]` + `w/h>0`
- [x] 3.2 `TypeHint`: remove `Unknown`; `FromString` precise → alias → `Text`; add `Values`/`IsValid`
- [x] 3.3 `SelectionState`: `FromString` alias mapping (`checked`/`highlight`→`Selected`; `inactive`/`hidden`→`Disabled`)
- [x] 3.4 `Region`: restrict `role` to the allowed set; illegal `role` throws `DomainValidationException`
- [x] 3.5 `FlattenedElement`: nullable `bbox` with `0.001` default; `confidence∈[0,1]` validation
- [x] 3.6 `ScreenHints`: nest `extra` as an independent nested field (not flattened to root)
- [x] 3.7 `FlattenedScreen`: `elements` as `ImmutableArray<FlattenedElement>`; construction sort by `(y,x)`; `with` independence
- [x] 3.8 Update/rewrite Vision tests: negative construction suites, `FromString` paths, `with` independence, sort order
- [x] 3.9 Verify: `dotnet test` for Vision models is green

## 4. Common / Operations corrections (3 types) — PRD §5.3

- [x] 4.1 `Operation`: reduce `action` to `{click,swipe,back,input_text,no_action}`; delete `Wait`/`LongPress`; `params` default empty; illegal `action` throws
- [x] 4.2 `Target`: reduce `by` to `{text,coordinate,ui_index}`; delete `ResourceId`/`ElementType`; illegal `by` throws
- [x] 4.3 `RestoreAction`: apply the same `action` validation and `params` default as `Operation`
- [x] 4.4 Update Common tests: enumeration membership assertions + negative-construction suites
- [x] 4.5 Verify: `dotnet test` for Common models is green; downstream Graph-layer references (if any current) still compile

## 5. Content models port (12 types) — PRD §5.2

- [x] 5.1 Create `src/UniClaw.Core/Domain/Models/Content/` namespace
- [x] 5.2 Port `Coordinate` and `Direction` (foundational, no Content deps)
- [x] 5.3 Port `MenuItemType` and `ExpectedAction` (enums, used by Mappings)
- [x] 5.4 Port `MenuInfo` and `MenuItem`
- [x] 5.5 Port `PopupInfo` and `PageAnalysis` (full versions; single source per R-4)
- [x] 5.6 Port `VisitFingerprint`
- [x] 5.7 Port `ContentNode` (data record; ContentTree → Phase 2 StateMachine, out of scope)
- [x] 5.8 ContentTree and SimulationState **out of Phase 1 scope** (ContentTree → Phase 2 StateMachine; SimulationState → simulation module, deprecated)
- [x] 5.9 For each: translate pydantic validation → primary-constructor `DomainValidationException`; collections as `ImmutableArray<T>`
- [x] 5.10 Confirm `PageAnalysis`/`PopupInfo` are referenced from Content (not AI) as canonical; leave AI simplified versions in place with a TODO marker for the later-phase swap
- [x] 5.11 Write Content tests: construction-validation negatives, `with` independence on collection-bearing records, camelCase single-direction serialization
- [x] 5.12 Verify: `dotnet test` for Content models is green

## 6. Mappings port — PRD §5.4

- [x] 6.1 Create `src/UniClaw.Core/Domain/Mappings/AndroidWidgetClass.cs` enum (every widget class from `element_type_mapper.py`)
- [x] 6.2 Create `src/UniClaw.Core/Domain/Mappings/ElementTypeMapper.cs` with `map_android_class`/`to_menu_item_type`/`to_expected_action` static methods
- [x] 6.3 Port the full widget-class → `(TypeHint, MenuItemType, ExpectedAction)` table row-for-row from `element_type_mapper.py`
- [x] 6.4 Confirm Mappings depend only on `Models.Content` + `Models.Vision` (no upper-layer usings)
- [x] 6.5 Write Mappings tests: full-table scan asserting every `AndroidWidgetClass` maps to the same triple as the Python source; fallback behavior for unmapped classes
- [x] 6.6 Verify: `dotnet test` for Mappings is green

## 7. Domain-wide invariants & gates

- [x] 7.1 Grep-assert no `BoundingBoxPixel`, no `Unknown` on `TypeHint`, no `Wait`/`LongPress` on Operation, no `ResourceId`/`ElementType` on Target
- [x] 7.2 Grep-assert no `List<T>`/`IList<T>` exposed on Domain records; collections are `ImmutableArray<T>`
- [x] 7.3 Grep-assert no `ToDictionary`/`FromDictionary` and no DTO types in Domain
- [x] 7.4 Grep-assert Domain has no upper-layer (`Graph`/`StateMachine`/`Traversal`/`Trace`/`AI`) usings and no I/O APIs
- [x] 7.5 `dotnet build`: 0 errors, 0 warnings
- [x] 7.6 `dotnet test` with coverage: all green, Domain-layer coverage > 80%
- [x] 7.7 Update the Phase 1 summary doc + memory note to reflect PRD-conformant completion (current summary doc is stale)

## 8. Review & handoff

- [x] 8.1 Self-review artifacts against PRD §3–§10 (every principle/risk addressed)
- [x] 8.2 Confirm Spike findings recorded in design.md (R-3/R-5 resolved)
- [x] 8.3 Mark change ready for `/opsx:apply` implementation
