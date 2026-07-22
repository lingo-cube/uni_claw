## MODIFIED Requirements

### Requirement: ScrollableMockVisionService.FindElementAt searches scroll data elements

The existing ScrollableMockVisionService SHALL be replaced by two separate mock classes:
1. **MockPageAnalyzer** (implements IPageAnalyzer): Handles page analysis behavior previously in StatefulMockVisionService/ScrollableMockVisionService. Constructed from StateFixture, returns fixture PageAnalysis for AnalyzeCurrentPageAsync.
2. **MockScreenStateProvider** (implements IScreenStateProvider): Handles scroll state behavior previously in ScrollableMockVisionService. Returns programmed values for HasScroll, GetScrollProgress, IsEndOfList, GetScrollSwipeConfig.

Simulation baseline tests SHALL compose mock brain as:
```csharp
var mockBrain = new UniBrainService(
    new MockPageAnalyzer(fixture),
    new MockTraversalAdvisor(),
    new MockTextUnderstanding());
var mockScreenState = new MockScreenStateProvider(scrollFixture);
```

#### Scenario: MockPageAnalyzer replaces StatefulMockVisionService page analysis behavior
- **WHEN** MockPageAnalyzer.AnalyzeCurrentPageAsync is called
- **THEN** returns PageAnalysis constructed from StateFixture (same logic as former StatefulMockVisionService)

#### Scenario: MockScreenStateProvider replaces ScrollableMockVisionService scroll behavior
- **WHEN** MockScreenStateProvider.HasScroll() is called
- **THEN** returns programmed scroll state from fixture
- **WHEN** MockScreenStateProvider.GetScrollSwipeConfig() is called
- **THEN** returns ScrollSwipeConfig from fixture data

#### Scenario: Baseline test composition uses UniBrainService + MockScreenStateProvider
- **WHEN** baseline test constructs mock services
- **THEN** StepContext receives IUniBrain (mockBrain) and IScreenStateProvider (mockScreenState) as two independent injection points

## ADDED Requirements

### Requirement: MockTraversalAdvisor returns default GiveUp decision

MockTraversalAdvisor SHALL implement ITraversalAdvisor. All 4 methods SHALL return ContextDecisionResult with Result=GiveUp, Confidence=0.0. VerifyPageTypeAsync on MockPageAnalyzer SHALL return PageTypeVerification with IsMatch=false.

#### Scenario: MockTraversalAdvisor.DecideNextActionAsync returns GiveUp
- **WHEN** MockTraversalAdvisor.DecideNextActionAsync is called
- **THEN** returns ContextDecisionResult(Result=GiveUp, Confidence=0.0)

### Requirement: MockTextUnderstanding returns fixed result

MockTextUnderstanding SHALL implement ITextUnderstanding. UnderstandTextAsync SHALL return TextUnderstandingResult with Category="mock", Confidence=1.0, Entities=Empty.

#### Scenario: MockTextUnderstanding returns fixed result
- **WHEN** MockTextUnderstanding.UnderstandTextAsync is called
- **THEN** returns TextUnderstandingResult(Category="mock", Confidence=1.0, Entities=[])
