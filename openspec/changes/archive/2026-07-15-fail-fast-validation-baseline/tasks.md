## 1. C-1: Graph model constructor validation — TraversalNode records

- [x] 1.1 `Precondition`: primary constructor → manual constructor + `TimeoutSeconds > 0 && <= 300` validation; update call sites if any fail
- [x] 1.2 `DynamicRule`: primary → manual + `RuleId`/`ChildTemplate` non-empty validation
- [x] 1.3 `ChildrenStrategy`: primary → manual + `MaxChildren >= 0 && <= 10000` validation
- [x] 1.4 `ErrorPolicy`: primary → manual + `MaxRetries >= 0 && <= 100` validation
- [x] 1.5 `ExitCondition`: primary → manual + conditional `MaxDepth > 0 && <= 1000` when `DepthLimited`
- [x] 1.6 `TraversalNode`: primary → manual + `NodeId`/`Name` non-empty validation; container type guard
- [x] 1.7 `dotnet build` — verify 0 errors; `dotnet test` — fix any failing tests with illegal values

## 2. C-1: Graph model constructor validation — TraversalPlan records

- [x] 2.1 `CompletionPolicy`: primary → manual + `TargetName` non-empty (TargetFound) + `TimeoutSeconds > 0 && <= 86400` + `MaxSteps >= 1 && <= 1000000`
- [x] 2.2 `EntryPolicy`: primary → manual + `TimeoutSeconds > 0 && <= 300`
- [x] 2.3 `EntryConfig`: add upper bound to existing lower-bound checks (WaitTimeoutSeconds ≤ 300, etc.)
- [x] 2.4 `dotnet build` + `dotnet test` — verify

## 3. C-4: TraversalPlan root node validation

- [x] 3.1 `TraversalPlan` constructor: add `RootNode` null check + type (Container/Screen) + `Operation.Type == NoAction` assertion
- [x] 3.2 `PlanCompiler.BuildRootNode()`: add comment referencing validation in TraversalPlan constructor
- [x] 3.3 `dotnet build` + `dotnet test` — verify

## 4. C-2: PlaceholderResolver + TemplateInstantiator

- [x] 4.1 `PlaceholderResolver.Resolve()`: call `HasUnresolvedPlaceholders()` after substitution; throw `DomainValidationException` if unresolved
- [x] 4.2 `TemplateInstantiator.CreateOperation()`: pass `meta` dict → `Target.Meta`
- [x] 4.3 `TemplateInstantiator.CreateOperation()`: parse `restore.target`/`restore.params` → `RestoreAction`
- [x] 4.4 `TemplateInstantiator.CreatePrecondition()`: read `ui_condition` → `Precondition.UiCondition`
- [x] 4.5 `dotnet build` + `dotnet test` — verify

## 5. C-3: ErrorPolicy wiring

- [x] 5.1 `ErrorStrategySelector.SelectStrategy()`: read `node.ErrorPolicy` when non-null; use `MaxRetries` from policy
- [x] 5.2 Map `ErrorPolicy.OnError` to strategy chain selection (Retry→Retry, Skip→Skip, Abort→Abort, Backtrack→Backtrack, Fallback→use FallbackTarget)
- [x] 5.3 Preserve existing hardcoded defaults when `node.ErrorPolicy == null`
- [x] 5.4 `dotnet build` + `dotnet test` — verify ErrorHandler tests pass

## 6. C-8: P3 five items

- [x] 6.1 `ContentNode.ToMarkdown()`: add recursive markdown tree output method
- [x] 6.2 `Region.Id`: add non-null/non-empty validation in constructor
- [x] 6.3 `TypeHint`: add `[JsonPropertyName]` attribute to each of 8 enum values
- [x] 6.4 `TypeHintExtensions.IsCanonical(string)`: add method distinguishing exact values from aliases
- [x] 6.5 `dotnet build` + `dotnet test` — verify

## 7. Validation

- [x] 7.1 `dotnet build` clean (0 errors)
- [x] 7.2 `dotnet test` full suite: 670+ tests green, no regression
- [x] 7.3 Verify C-1: each validated record throws on illegal value (smoke test via existing tests or new assertions)
- [x] 7.4 `openspec validate fail-fast-validation-baseline` (if available)
