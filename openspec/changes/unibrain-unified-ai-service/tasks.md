## 1. UniBrain Interface Definitions + Types

- [x] 1.1 Create `src/UniClaw.Core/UniBrain/` directory and `IUniBrain.cs` with 3 sub-interface properties (PageAnalyzer, Advisor, Text)
- [x] 1.2 Create `UniBrainService.cs` — sealed class implementing IUniBrain, pure composition container with 3 constructor-injected properties
- [x] 1.3 Create `IPageAnalyzer.cs` — 3 methods: AnalyzeCurrentPageAsync, FindAppEntryAsync, VerifyPageTypeAsync
- [x] 1.4 Create `ITraversalAdvisor.cs` — 4 methods with Domain+BCL parameters only (no ITraversalContext reference)
- [x] 1.5 Create `ITextUnderstanding.cs` — 1 method: UnderstandTextAsync
- [x] 1.6 Create `IModelProvider.cs` — ProviderId + 3 Complete methods (Text, Vision, Multimodal)
- [x] 1.7 Create `UniBrainConfig.cs` — sealed record class with DefaultProvider, CapabilityRouting, EnableTrace
- [x] 1.8 Create `AppEntryPoint.cs` — sealed record class with validation (X/Y/Confidence 0-1 range)
- [x] 1.9 Create `ContextDecisionResult.cs` — sealed record class aligned with Python (7 fields: Result, Action, Target(string?), Params, Reasoning, Confidence, SafetyVerified)
- [x] 1.10 Create `DecisionResult.cs` — enum with 3 values (Success, Unsure, GiveUp)
- [x] 1.11 Create `ContainerInference.cs` — sealed record class (ContainerType, Confidence, MatchedTemplate)
- [x] 1.12 Create `PageTypeVerification.cs` + `MismatchDetails.cs` + `Suggestion.cs` — sealed record classes aligned with Python field names
- [x] 1.13 Create `SafetyScreeningResult.cs` + `SafetyEvaluation.cs` + `SafetyTag.cs` + `PageLevelGuidance.cs` — sealed record classes + enum (4 values: Safe, Caution, Skip, Unknown)
- [x] 1.14 Create `TextUnderstandingRequest.cs` + `TextUnderstandingResult.cs` — sealed record classes with validation
- [x] 1.15 Create `ModelRequest.cs` + `ModelResponse.cs` — sealed record classes aligned with Python AIResponse

## 2. IScreenStateProvider Separation

- [x] 2.1 Create `src/UniClaw.Core/Traversal/IScreenStateProvider.cs` — 4 methods (HasScroll, GetScrollProgress, IsEndOfList, GetScrollSwipeConfig) in Traversal namespace
- [x] 2.2 Remove scroll methods from IVisionProvider (HasScroll, GetScrollProgress, IsEndOfList, GetScrollSwipeConfig)
- [x] 2.3 Verify IVisionProvider still compiles with remaining AnalyzeCurrentPageAsync + FindAppEntryAsync methods

## 3. StepContext Injection Change

- [x] 3.1 Add `IScreenStateProvider ScreenState` property to StepContext (4th positional parameter, after Vision)
- [x] 3.2 Add `IScreenStateProvider screenState` parameter to TraversalEngine constructor
- [x] 3.3 Update ScrollableMockVisionService to implement IVisionProvider + IScreenStateProvider
- [x] 3.4 Update InterceptionHandler.cs scroll method calls: ctx.Vision → ctx.ScreenState
- [x] 3.5 Create `DefaultScreenStateProvider` for non-scroll test scenarios
- [x] 3.6 Fix all test construction sites (TraversalEngine, StepContext) for new ScreenState parameter
- [x] 3.7 Fix StatefulMockVisionTests scroll method references → DefaultScreenStateProvider tests

Note: Task 3.1 originally said "Add IUniBrain Brain property replacing IVisionProvider Vision" — deferred to Task Group 6.
IVisionProvider Vision is retained in StepContext during transition period (dual interface).

## 4. ArchitectureGuard Tests

- [x] 4.1 Add `UniBrain_DoesNotReferenceStateMachine` guard test — asserts UniBrain namespace has zero StateMachine references
- [x] 4.2 Add `UniBrain_DoesNotReferenceTraversal` guard test — asserts UniBrain namespace has zero Traversal references
- [x] 4.3 Add `IUniBrain_Has3SubInterfaces` guard test — asserts IUniBrain has exactly 3 properties of correct types
- [x] 4.4 Add `IScreenStateProvider_Has4Methods` guard test — asserts 4 public methods on IScreenStateProvider
- [x] 4.5 Add `StateMachine_ReferencesUniBrainForIUniBrain` guard test — acknowledged upward reference, verifies only through interface
- [x] 4.6 Add `Traversal_ReferencesUniBrainForIUniBrain` guard test — acknowledged upward reference, verifies only through interface
- [x] 4.7 Add `DecisionResult_Has3Values` guard test (bonus — enum value lock)
- [x] 4.8 Add `SafetyTag_Has4Values` guard test (bonus — enum value lock)

## 5. Mock Composition Migration (Simulation Layer)

- [ ] 5.1 Create `MockPageAnalyzer` implementing IPageAnalyzer — constructed from StateFixture, returns fixture PageAnalysis for AnalyzeCurrentPageAsync
- [ ] 5.2 Create `MockTraversalAdvisor` implementing ITraversalAdvisor — returns ContextDecisionResult(Result=GiveUp) for all 4 methods
- [ ] 5.3 Create `MockTextUnderstanding` implementing ITextUnderstanding — returns fixed TextUnderstandingResult(Category="mock", Confidence=1.0)
- [ ] 5.4 Create `MockScreenStateProvider` implementing IScreenStateProvider — returns programmed scroll values from fixture
- [ ] 5.5 Update baseline tests to compose `new UniBrainService(MockPageAnalyzer, MockTraversalAdvisor, MockTextUnderstanding)` + MockScreenStateProvider
- [ ] 5.6 Update ScrollableBaselineTests to use MockScreenStateProvider for scroll queries instead of ScrollableMockVisionService scroll methods
- [ ] 5.7 Verify all baseline tests pass with new mock composition

## 6. Engine Consumer Code Migration

- [ ] 6.1 Migrate TraversalFSM call sites: ctx.Vision → ctx.Brain.PageAnalyzer + ctx.ScreenState
- [ ] 6.2 Migrate StepOrchestrator call sites: ctx.Vision → ctx.Brain.PageAnalyzer + ctx.ScreenState (including TryHandleScroll — partially done in TG3)
- [ ] 6.3 Migrate Handler call sites: ctx.Vision → ctx.Brain.PageAnalyzer + ctx.ScreenState
- [ ] 6.4 Migrate TraversalEngine.Initialize: IVisionProvider parameter → IUniBrain + IScreenStateProvider
- [ ] 6.5 Verify all 849 tests pass after consumer code migration

## 7. Delete Old AI/ Directory + IVisionProvider

- [ ] 7.1 Delete `src/UniClaw.Core/AI/` directory (IAIStrategyAdvisor and related types migrated to UniBrain/)
- [ ] 7.2 Remove IVisionProvider interface definition from StepContext.cs (or its original file)
- [ ] 7.3 Remove StatefulMockVisionService from Simulation/ (replaced by MockPageAnalyzer)
- [ ] 7.4 Remove ScrollableMockVisionService scroll methods (replaced by MockScreenStateProvider)
- [ ] 7.5 Verify project compiles and all 849 tests pass with no residual IVisionProvider/IAIStrategyAdvisor references

## 8. Host Project Skeletons (deferred — requires external dependencies)

- [ ] 8.1 Create `src/UniClaw.ClaudeProvider/` project skeleton — ClaudePageAnalyzer, ClaudeTraversalAdvisor, ClaudeTextUnderstanding, AnthropicModelProvider stubs
- [ ] 8.2 Create `src/UniClaw.DeepSeekProvider/` project skeleton — DeepSeekTraversalAdvisor, DeepSeekTextUnderstanding, DeepSeekModelProvider stubs
- [ ] 8.3 Create `src/UniClaw.Device/` project skeleton — AdbScreenCapture, AdbScreenStateProvider, AdbActionExecutor stubs
- [ ] 8.4 Add project references from Host projects to UniClaw.Core
- [ ] 8.5 Verify Host project stubs compile (no runtime dependencies required yet)
