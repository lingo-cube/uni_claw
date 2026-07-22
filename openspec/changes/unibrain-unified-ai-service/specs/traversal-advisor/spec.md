## ADDED Requirements

### Requirement: ITraversalAdvisor defines 4 methods with Domain+BCL parameters only

ITraversalAdvisor SHALL define exactly 4 async methods:
- `Task<ContainerInference> InferContainerTypeAsync(PageAnalysis pageAnalysis, string? currentNodeId = null, CancellationToken ct = default)`
- `Task<ContextDecisionResult> DecideNextActionAsync(string goal, PageAnalysis pageAnalysis, string? currentNodeId = null, int? depth = null, CancellationToken ct = default)`
- `Task<ContextDecisionResult> HandleExceptionAsync(Exception exception, PageAnalysis pageAnalysis, string? currentNodeId = null, CancellationToken ct = default)`
- `Task<SafetyScreeningResult> ScreenSafetyAsync(PageAnalysis pageAnalysis, string instruction, string? pageType = null, CancellationToken ct = default)`

ITraversalAdvisor SHALL NOT reference ITraversalContext (StateMachine interface). All method parameters SHALL be Domain types + BCL types (string, int, Exception, CancellationToken).

#### Scenario: DecideNextActionAsync receives goal and page analysis
- **WHEN** handler calls advisor.DecideNextActionAsync("find WiFi settings", pageAnalysis, "node_1", 3)
- **THEN** returns ContextDecisionResult with Result, Action, Target, Params, Reasoning, Confidence, SafetyVerified

#### Scenario: InferContainerTypeAsync infers container from page analysis
- **WHEN** handler calls advisor.InferContainerTypeAsync(pageAnalysis, "node_2")
- **THEN** returns ContainerInference with ContainerType, Confidence, MatchedTemplate

#### Scenario: HandleExceptionAsync provides recovery plan
- **WHEN** handler calls advisor.HandleExceptionAsync(exception, pageAnalysis, "node_3")
- **THEN** returns ContextDecisionResult with recovery action recommendation

#### Scenario: ScreenSafetyAsync evaluates safety
- **WHEN** handler calls advisor.ScreenSafetyAsync(pageAnalysis, "tap button", "popup")
- **THEN** returns SafetyScreeningResult with Evaluations and PageLevelGuidance

#### Scenario: ITraversalAdvisor methods have zero ITraversalContext references
- **WHEN** ITraversalAdvisor interface is inspected
- **THEN** no method parameter references ITraversalContext or any StateMachine namespace type

### Requirement: ContextDecisionResult aligns with Python ai_types.ContextDecisionResult fields

ContextDecisionResult SHALL be a sealed record class with:
- `DecisionResult Result` (enum: Success, Unsure, GiveUp)
- `string? Action = null`
- `string? Target = null` (string?, NOT object?)
- `ImmutableDictionary<string, object>? Params = null` (aligned with Python params)
- `string? Reasoning = null` (aligned with Python reasoning)
- `double Confidence = 0.0`
- `bool SafetyVerified = true` (aligned with Python safety_verified)

#### Scenario: ContextDecisionResult carries full decision context
- **WHEN** TraversalAdvisor returns ContextDecisionResult with Result=Success, Action="tap", Target="button_1", Params={"timeout": 5000}, Reasoning="visible button", Confidence=0.95
- **THEN** all 7 fields are accessible and correctly typed

### Requirement: DecisionResult enum has 3 locked values

DecisionResult SHALL define exactly 3 enum values: Success, Unsure, GiveUp. Adding/removing values SHALL require constitution change flow.

#### Scenario: DecisionResult covers all possible outcomes
- **WHEN** traversal decision is made
- **THEN** result is one of Success (action found), Unsure (ambiguous), or GiveUp (no viable action)

### Requirement: ContainerInference carries inferred container type

ContainerInference SHALL be a sealed record class with:
- `string ContainerType`
- `double Confidence`
- `string? MatchedTemplate = null`

#### Scenario: ContainerInference reports type with confidence
- **WHEN** advisor infers container type from page analysis
- **THEN** ContainerInference provides ContainerType string, Confidence score, and optional MatchedTemplate

### Requirement: SafetyScreeningResult carries evaluations and guidance

SafetyScreeningResult SHALL be a sealed record class with:
- `ImmutableArray<SafetyEvaluation> Evaluations`
- `PageLevelGuidance PageLevelGuidance`

SafetyEvaluation SHALL be a sealed record class with:
- `string Name`
- `SafetyTag SafetyTag` (enum: Safe, Caution, Skip, Unknown)
- `double Confidence`
- `string? Reason = null`

PageLevelGuidance SHALL be a sealed record class with:
- `bool OverallSafeToProceed`
- `int RecommendedMaxParallel = 1`

SafetyTag SHALL define exactly 4 enum values: Safe, Caution, Skip, Unknown. Adding/removing values SHALL require constitution change flow.

#### Scenario: SafetyScreeningResult evaluates multiple dimensions
- **WHEN** advisor screens page safety
- **THEN** SafetyScreeningResult contains array of SafetyEvaluation items and PageLevelGuidance with overall assessment

### Requirement: PageTypeVerification carries match result with mismatch details

PageTypeVerification SHALL be a sealed record class with:
- `bool IsMatch`
- `double Confidence`
- `string? ActualType = null`
- `string? Reasoning = null`
- `MismatchDetails? Mismatch = null`

MismatchDetails SHALL be a sealed record class with:
- `ImmutableArray<string> MissingItems` (aligned with Python missing_items)
- `ImmutableArray<string> UnexpectedItems` (aligned with Python unexpected_items)
- `string? TypeConflict = null` (aligned with Python type_conflict)

#### Scenario: PageTypeVerification reports mismatch details
- **WHEN** page type does not match expected
- **THEN** PageTypeVerification.IsMatch=false, Mismatch contains MissingItems, UnexpectedItems, and TypeConflict

### Requirement: Suggestion carries action recommendation

Suggestion SHALL be a sealed record class with:
- `string Action`
- `string? Target = null` (string?, NOT object?)
- `string? Reason = null`

#### Scenario: Suggestion recommends action with reason
- **WHEN** traversal advisor produces suggestion
- **THEN** Suggestion.Action, Suggestion.Target (string?), Suggestion.Reason are accessible
