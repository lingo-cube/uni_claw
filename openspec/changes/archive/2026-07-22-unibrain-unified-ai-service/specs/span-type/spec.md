## ADDED Requirements

### Requirement: AICallRecord.Capability values include UniBrain capability names

AICallRecord.Capability field SHALL accept the following 8 UniBrain capability string values (aligned with sub-interface methods):

| Capability value | Source method |
|-----------------|---------------|
| "page_analysis" | IPageAnalyzer.AnalyzeCurrentPageAsync |
| "find_app_entry" | IPageAnalyzer.FindAppEntryAsync |
| "page_type_verify" | IPageAnalyzer.VerifyPageTypeAsync |
| "container_inference" | ITraversalAdvisor.InferContainerTypeAsync |
| "next_action" | ITraversalAdvisor.DecideNextActionAsync |
| "exception_recovery" | ITraversalAdvisor.HandleExceptionAsync |
| "safety_screening" | ITraversalAdvisor.ScreenSafetyAsync |
| "text_understanding" | ITextUnderstanding.UnderstandTextAsync |

SpanType enum value count SHALL remain locked at 11 (D-E8). New capabilities do NOT add new SpanType values — they are distinguished by AICallRecord.Capability string field, not by SpanType. SpanType only distinguishes broad categories (PageAnalysis, AICall, StateDecision).

#### Scenario: AICallRecord distinguishes capability within SpanType.AICall
- **WHEN** ClaudePageAnalyzer records AICallRecord after AnalyzeCurrentPageAsync
- **THEN** SpanType=AICall, Capability="page_analysis"
- **WHEN** DeepSeekTraversalAdvisor records AICallRecord after DecideNextActionAsync
- **THEN** SpanType=AICall, Capability="next_action"

#### Scenario: SpanType value count remains 11
- **WHEN** ArchitectureGuardTests checks SpanType value count
- **THEN** assertion remains Enum.GetValues<SpanType>().Length == 11 (no new values added for UniBrain)
