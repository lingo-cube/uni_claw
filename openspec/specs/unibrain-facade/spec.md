## ADDED Requirements

### Requirement: IUniBrain defines unified AI service facade with 3 sub-interface properties

IUniBrain SHALL define a public interface with exactly 3 read-only properties:
- `IPageAnalyzer PageAnalyzer { get; }`
- `ITraversalAdvisor Advisor { get; }`
- `ITextUnderstanding Text { get; }`

IUniBrain SHALL be the sole AI injection point for TraversalEngine and StepContext. No other AI interface (IVisionProvider, IAIStrategyAdvisor) SHALL be injected into engine components simultaneously.

#### Scenario: Engine injects IUniBrain as single AI seam
- **WHEN** TraversalEngine or StepContext is constructed
- **THEN** it receives exactly one IUniBrain instance, providing access to all 3 AI capabilities through sub-interface properties

#### Scenario: IUniBrain facade exposes all 3 sub-interfaces
- **WHEN** consumer accesses `brain.PageAnalyzer`
- **THEN** returns IPageAnalyzer instance
- **WHEN** consumer accesses `brain.Advisor`
- **THEN** returns ITraversalAdvisor instance
- **WHEN** consumer accesses `brain.Text`
- **THEN** returns ITextUnderstanding instance

### Requirement: UniBrainService is pure composition container (sealed class, not record)

UniBrainService SHALL be a sealed class (not record) implementing IUniBrain. It SHALL hold exactly 3 constructor-injected sub-interface instances as read-only properties. UniBrainService SHALL NOT:
- Hold IModelProvider reference
- Hold configuration state
- Perform routing or dispatch logic
- Contain any AI capability implementation

#### Scenario: UniBrainService composes 3 injected sub-interface implementations
- **WHEN** UniBrainService is constructed with pageAnalyzer, advisor, text instances
- **THEN** PageAnalyzer property returns the injected IPageAnalyzer
- **THEN** Advisor property returns the injected ITraversalAdvisor
- **THEN** Text property returns the injected ITextUnderstanding

#### Scenario: UniBrainService does not route or dispatch
- **WHEN** consumer calls brain.PageAnalyzer.AnalyzeCurrentPageAsync()
- **THEN** UniBrainService delegates directly to injected PageAnalyzer instance with zero routing logic

### Requirement: UniBrainConfig drives composition via DI configuration

UniBrainConfig SHALL be a sealed record class with:
- `string DefaultProvider = "deepseek"`
- `ImmutableDictionary<string, string>? CapabilityRouting = null`
- `bool EnableTrace = true`

UniBrainConfig SHALL NOT contain provider credentials or API keys. CapabilityRouting keys SHALL be capability names ("page_analysis", "traversal_advisor", "text_understanding"), values SHALL be provider identifiers.

#### Scenario: Configuration determines which provider powers each capability
- **WHEN** UniBrainConfig.CapabilityRouting = {"page_analysis": "claude", "traversal_advisor": "deepseek"}
- **THEN** composition root creates ClaudePageAnalyzer for PageAnalyzer and DeepSeekTraversalAdvisor for Advisor

#### Scenario: Default provider fills unspecified capability routing
- **WHEN** CapabilityRouting is null or missing a key
- **THEN** DefaultProvider identifier is used for that capability
