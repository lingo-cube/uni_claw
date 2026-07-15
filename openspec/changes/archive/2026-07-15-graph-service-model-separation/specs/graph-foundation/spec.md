## ADDED Requirements

### Requirement: Graph layer SHALL have three-directory structure with separated concerns

The Graph layer SHALL consist of three directories: `Abstractions/` (service interfaces), `Models/` (data records, enums, and pure interfaces like `ITraversalNode`), and `Services/` (service implementations). Each directory SHALL have a distinct namespace (`UniClaw.Core.Graph.Abstractions`, `.Graph.Models`, `.Graph.Services`) and SHALL respect the dependency direction: Models → Domain only; Abstractions → Models + Domain; Services → Abstractions + Models + Domain. Models MUST NOT reference Abstractions or Services.

#### Scenario: Models directory contains only data types
- **WHEN** all files in `Graph/Models/` are inspected
- **THEN** every file SHALL be a data record, enum, or pure interface (`ITraversalNode`)
- **AND** no service implementation class (DynamicMatcher, PlanCompiler, TemplateInstantiator, PlaceholderResolver, TemplateValidator) SHALL reside in Models/

#### Scenario: Abstractions directory contains only interfaces
- **WHEN** all files in `Graph/Abstractions/` are inspected
- **THEN** every file SHALL be an interface definition
- **AND** no implementation code SHALL reside in Abstractions/

#### Scenario: Services directory contains only implementations
- **WHEN** all files in `Graph/Services/` are inspected
- **THEN** every class SHALL implement at least one interface from Abstractions/ or be a static utility (PlaceholderResolver, TemplateValidator)
- **AND** every class SHALL be in namespace `UniClaw.Core.Graph.Services`

### Requirement: Graph services SHALL expose interfaces for DI and testability

Every service class in `Graph/Services/` that performs logic SHALL have a corresponding interface in `Graph/Abstractions/`. Specifically: `DynamicMatcher` SHALL implement `IDynamicMatcher`, `PlanCompiler` SHALL implement `IPlanCompiler`, `TemplateInstantiator` SHALL implement `ITemplateInstantiator`. `ITemplateRegistry` SHALL be moved from `Models/Template.cs` to `Abstractions/ITemplateRegistry.cs` with namespace changed to `UniClaw.Core.Graph.Abstractions`.

#### Scenario: DynamicMatcher implements IDynamicMatcher
- **WHEN** `DynamicMatcher` class is inspected
- **THEN** it SHALL declare `public sealed class DynamicMatcher : IDynamicMatcher`
- **AND** all public methods (Match, MatchAll) SHALL be declared in `IDynamicMatcher`

#### Scenario: PlanCompiler implements IPlanCompiler
- **WHEN** `PlanCompiler` class is inspected
- **THEN** it SHALL declare `public sealed class PlanCompiler : IPlanCompiler`
- **AND** the Compile method SHALL be declared in `IPlanCompiler`

#### Scenario: TemplateInstantiator implements ITemplateInstantiator
- **WHEN** `TemplateInstantiator` class is inspected
- **THEN** it SHALL declare `public sealed class TemplateInstantiator : ITemplateInstantiator`
- **AND** the Instantiate method SHALL be declared in `ITemplateInstantiator`

#### Scenario: Abstractions directory locked at 4 interfaces
- **WHEN** `Graph/Abstractions/` directory is inspected
- **THEN** it SHALL contain exactly 4 interfaces: IDynamicMatcher, IPlanCompiler, ITemplateInstantiator, ITemplateRegistry
- **AND** a CI-blocking guard test (`GraphAbstractions_Has4Interfaces`) SHALL enforce this count

### Requirement: Template.cs SHALL be split by type responsibility

The `Template.cs` file currently containing 4 types SHALL be split into separate files by type: `Template` record stays in `Models/Template.cs`, `ITemplateRegistry` interface moves to `Abstractions/ITemplateRegistry.cs`, `PlaceholderResolver` static class moves to `Services/PlaceholderResolver.cs`, `TemplateValidator` static class moves to `Services/TemplateValidator.cs`.

#### Scenario: Template.cs contains only Template record
- **WHEN** `Models/Template.cs` is inspected
- **THEN** it SHALL contain only the `Template` sealed record class
- **AND** no `ITemplateRegistry`, `PlaceholderResolver`, or `TemplateValidator` type SHALL remain in the file

#### Scenario: PlaceholderResolver and TemplateValidator are in Services namespace
- **WHEN** `PlaceholderResolver` and `TemplateValidator` classes are inspected
- **THEN** their namespace SHALL be `UniClaw.Core.Graph.Services`

### Requirement: TraversalEngine SHALL depend on Graph service interfaces, not concrete types

`TraversalEngine` SHALL declare its `DynamicMatcher` and `TemplateInstantiator` dependencies as interface types (`IDynamicMatcher` and `ITemplateInstantiator`) rather than concrete types. Default implementations SHALL be instantiated as `new DynamicMatcher()` and `new TemplateInstantiator()` respectively, preserving backward compatibility.

#### Scenario: TraversalEngine fields use interface types
- **WHEN** `TraversalEngine` private fields are inspected
- **THEN** the DynamicMatcher field SHALL be typed as `IDynamicMatcher`
- **AND** the TemplateInstantiator field SHALL be typed as `ITemplateInstantiator`

#### Scenario: TraversalEngine constructs default implementations
- **WHEN** `TraversalEngine` is constructed
- **THEN** `_matcher` SHALL be initialized as `new DynamicMatcher()`
- **AND** `_instantiator` SHALL be initialized as `new TemplateInstantiator()`

### Requirement: MatchableItem and MatchResult SHALL reside in Models as interface parameter types

`MatchableItem` and `MatchResult` records, which are parameter/return types of `IDynamicMatcher` interface methods, SHALL be extracted from `Services/DynamicMatcher.cs` into separate files in `Models/` (`MatchableItem.cs` and `MatchResult.cs`). This ensures Abstractions/ can reference these types without depending on Services/.

#### Scenario: MatchableItem and MatchResult in Models namespace
- **WHEN** `MatchableItem` and `MatchResult` files are inspected
- **THEN** their namespace SHALL be `UniClaw.Core.Graph.Models`
- **AND** they SHALL remain sealed record classes with unchanged field definitions