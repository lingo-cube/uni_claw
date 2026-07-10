## MODIFIED Requirements

### Requirement: ExecutionRecord includes SpanType semantic classification

ExecutionRecord SHALL be extended with a `SpanType? SpanType = null` field after the `Status` field. The field MUST be backward compatible — existing code that constructs ExecutionRecord without SpanType MUST continue to work (default null). SpanType provides semantic classification of trace execution records (e.g., RestoreOp, SkipDangerous, DfsForward) for operation_rules and trace_integrity verification.

#### Scenario: ExecutionRecord with SpanType
- **WHEN** an ExecutionRecord is constructed with Action="toggle", Status="success", SpanType=SpanType.RestoreOp
- **THEN** the record MUST contain SpanType=SpanType.RestoreOp as a semantic classification tag

#### Scenario: Backward compatibility — existing constructors unaffected
- **WHEN** existing code constructs ExecutionRecord without SpanType parameter
- **THEN** SpanType MUST default to null
- **THEN** no existing test or production code MUST break

#### Scenario: SpanType enables operation_rules verification
- **WHEN** ExecutionRecord.SpanType = RestoreOp is present in trace data
- **THEN** operation_rules verification CAN check restore_ops behavior
- **WHEN** ExecutionRecord.SpanType = SkipDangerous is present in trace data
- **THEN** operation_rules verification CAN check skip_dangerous behavior

### Requirement: ITraceRecorder records and retrieves PageTransition

ITraceRecorder SHALL be extended with 2 new methods: `RecordPageTransitionAsync(PageTransition transition, CancellationToken ct)` and `GetPageTransitionsAsync(CancellationToken ct)` returning `Task<List<PageTransition>>`. These MUST be added to the existing interface alongside the 5 existing Record methods and Get methods.

#### Scenario: RecordPageTransitionAsync stores PageTransition
- **WHEN** RecordPageTransitionAsync is called with a PageTransition(FromPage="home", ToPage="wifi", TransitionType="forward")
- **THEN** the PageTransition MUST be stored and retrievable via GetPageTransitionsAsync

#### Scenario: GetPageTransitionsAsync retrieves all page transitions
- **WHEN** GetPageTransitionsAsync is called
- **THEN** it MUST return all previously recorded PageTransition records as a List

#### Scenario: PageTransition enables trace_integrity verification
- **WHEN** PageTransition records are present in trace data with TransitionType values
- **THEN** trace_integrity verification CAN check page_transitions (forward/back/sub_page/popup_dismiss patterns)
