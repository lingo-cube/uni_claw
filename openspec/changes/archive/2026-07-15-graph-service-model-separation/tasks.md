## 1. Infrastructure — directories + model types

- [x] 1.1 Create `src/UniClaw.Core/Graph/Abstractions/` and `src/UniClaw.Core/Graph/Services/` directories; add `using UniClaw.Core.Graph.Abstractions` and `using UniClaw.Core.Graph.Services` to `GlobalUsings.cs` if present, otherwise add to files that need them
- [x] 1.2 Extract `MatchableItem` record from `Models/DynamicMatcher.cs` → `Models/MatchableItem.cs` (namespace stays `.Graph.Models`; depend on `Domain.Models.Content` for `MenuItemType`, `ExpectedAction`)
- [x] 1.3 Extract `MatchResult` record from `Models/DynamicMatcher.cs` → `Models/MatchResult.cs` (namespace stays `.Graph.Models`; depend on `MatchAction` enum already in `TraversalNode.cs`)
- [x] 1.4 `dotnet build` — verify 0 errors

## 2. Interfaces — Abstractions/ extraction

- [x] 2.1 Create `Abstractions/IDynamicMatcher.cs`: `Match(MatchCondition, MatchableItem) → MatchResult` + `MatchAll(List<DynamicRule>, List<MatchableItem>) → List<MatchResult>`; namespace `UniClaw.Core.Graph.Abstractions`
- [x] 2.2 Create `Abstractions/IPlanCompiler.cs`: `Compile(IntentSlots) → TraversalPlan`; namespace `UniClaw.Core.Graph.Abstractions`
- [x] 2.3 Create `Abstractions/ITemplateInstantiator.cs`: `Instantiate(Template, Dictionary<string, object>, List<string>) → TraversalNode`; namespace `UniClaw.Core.Graph.Abstractions`
- [x] 2.4 Move `ITemplateRegistry` from `Models/Template.cs` → `Abstractions/ITemplateRegistry.cs`; change namespace from `.Graph.Models` → `.Graph.Abstractions`; update `Template.cs` to remove the interface
- [x] 2.5 Make service classes implement interfaces: `DynamicMatcher : IDynamicMatcher`, `PlanCompiler : IPlanCompiler`, `TemplateInstantiator : ITemplateInstantiator`
- [x] 2.6 `dotnet build` — verify 0 errors (all interface references resolve)

## 3. Template.cs split — 1 file → 3 files

- [x] 3.1 Move `ITemplateRegistry` from `Models/Template.cs` → `Abstractions/ITemplateRegistry.cs` (already done in 2.4; verify `Template.cs` no longer contains it)
- [x] 3.2 Move `PlaceholderResolver` static class from `Models/Template.cs` → `Services/PlaceholderResolver.cs`; namespace `UniClaw.Core.Graph.Services`; add `using UniClaw.Core.Graph.Models` for Template type references
- [x] 3.3 Move `TemplateValidator` static class from `Models/Template.cs` → `Services/TemplateValidator.cs`; namespace `UniClaw.Core.Graph.Services`
- [x] 3.4 Verify `Models/Template.cs` now contains ONLY `Template` sealed record class
- [x] 3.5 `dotnet build` — verify 0 errors

## 4. Services/ — move service implementations

- [x] 4.1 Move `DynamicMatcher` class from `Models/DynamicMatcher.cs` → `Services/DynamicMatcher.cs`; change namespace to `UniClaw.Core.Graph.Services`; remove `MatchableItem`/`MatchResult` (already extracted in 1.2/1.3); add `using UniClaw.Core.Graph.Models` + `using UniClaw.Core.Graph.Abstractions`
- [x] 4.2 Move `PlanCompiler` class from `Models/PlanCompiler.cs` → `Services/PlanCompiler.cs`; change namespace to `UniClaw.Core.Graph.Services`; add `using UniClaw.Core.Graph.Models` + `using UniClaw.Core.Graph.Abstractions`
- [x] 4.3 Move `TemplateInstantiator` class from `Models/TemplateInstantiator.cs` → `Services/TemplateInstantiator.cs`; change namespace to `UniClaw.Core.Graph.Services`; add `using UniClaw.Core.Graph.Models` + `using UniClaw.Core.Graph.Abstractions`
- [x] 4.4 `dotnet build` — verify 0 errors (all service class references resolve from new namespace)

## 5. TraversalEngine — interface injection

- [x] 5.1 In `TraversalEngine.cs`: change `private readonly DynamicMatcher _matcher` → `private readonly IDynamicMatcher _matcher`; change `private readonly TemplateInstantiator _instantiator` → `private readonly ITemplateInstantiator _instantiator`; keep `= new()` default initialization
- [x] 5.2 Add `using UniClaw.Core.Graph.Abstractions` to `TraversalEngine.cs` if not already present
- [x] 5.3 `dotnet build` — verify 0 errors

## 6. Consumers + tests

- [x] 6.1 Update `tests/UniClaw.Core.Tests/Graph/GraphTests.cs`: add `using UniClaw.Core.Graph.Services` + `using UniClaw.Core.Graph.Abstractions` if tests reference moved types
- [x] 6.2 `dotnet build` — verify 0 errors; `dotnet test` — verify 665 tests all green

## 7. Guard + docs

- [x] 7.1 Add `GraphAbstractions_Has4Interfaces` guard test to `ArchitectureGuardTests.cs`: scan `Graph/Abstractions/` directory, assert exactly 4 `.cs` files all define `interface` (not class/record/enum)
- [x] 7.2 Update `docs/system/layers/graph.md` §1: replace flat listing with three-directory structure (Models/, Abstractions/, Services/); add interface inventory table
- [x] 7.3 Append D-N decision to `docs/system/decisions/log.md`: D-28 Graph 层三目录分离 + 接口提取
- [x] 7.4 `dotnet build` clean (0 errors, 0 functional warnings); `dotnet test` full suite 665+ tests green; `openspec validate graph-service-model-separation` (if available)
