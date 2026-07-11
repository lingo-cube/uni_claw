## ADDED Requirements

### Requirement: ITraceStorage defines shared synchronous storage contract
ITraceStorage SHALL define 13 methods organized into three categories: session lifecycle (SetSession, EndSession, CurrentSession getter), synchronous write (AddExecution, AddTransition, AddError, AddPageTransition, AddAICall), and synchronous read (GetExecutions, GetTransitions, GetErrors, GetPageTransitions, GetAICalls, Export). All write and read methods SHALL be synchronous (void return for write, IReadOnlyList return for read). ITraceStorage is the shared backend for ITraceRecorder (write wrapper) and ITraceService (read+query facade).

#### Scenario: ITraceStorage has exactly 13 members
- **WHEN** ITraceStorage is inspected for method and property declarations
- **THEN** it declares exactly: SetSession (void), EndSession (void), CurrentSession (TraceSession? getter), AddExecution (void), AddTransition (void), AddError (void), AddPageTransition (void), AddAICall (void), GetExecutions (IReadOnlyList), GetTransitions (IReadOnlyList), GetErrors (IReadOnlyList), GetPageTransitions (IReadOnlyList), GetAICalls (IReadOnlyList), Export (string)

#### Scenario: Write methods are synchronous
- **WHEN** AddExecution is called with an ExecutionRecord
- **THEN** the record is appended to internal storage immediately; no Task is returned

#### Scenario: Read methods return IReadOnlyList
- **WHEN** GetExecutions is called
- **THEN** it returns IReadOnlyList<ExecutionRecord> providing direct access to the internal list (not a copy)

### Requirement: InMemoryTraceStorage implements ITraceStorage with incremental indexes using TraceContext access pattern
InMemoryTraceStorage SHALL implement ITraceStorage with 5 flat lists for storage and 2 incrementally-built Dictionary indexes (_byNodeId, _bySpanType). Indexes SHALL be updated synchronously during AddExecution (O(1) per write). The _byNodeId index key SHALL be accessed via `r.Context?.NodeId` (TraceContext encapsulation — NodeId is not a direct field on ExecutionRecord). The _bySpanType index key SHALL be accessed via `r.SpanType` (SpanType is a direct ExecutionRecord field). Index methods GetByNodeId and GetBySpanType SHALL be InMemoryTraceStorage-specific (NOT on ITraceStorage interface) per ISP principle — different storage backends may not support index queries.

#### Scenario: _byNodeId index groups ExecutionRecords by Context.NodeId
- **WHEN** AddExecution is called with ExecutionRecord(Context=new TraceContext(NodeId="wifi_node"))
- **THEN** _byNodeId["wifi_node"] contains this record; GetByNodeId("wifi_node") returns it

#### Scenario: _bySpanType index groups ExecutionRecords by SpanType
- **WHEN** AddExecution is called with ExecutionRecord(SpanType=SpanType.DfsForward)
- **THEN** _bySpanType[SpanType.DfsForward] contains this record; GetBySpanType(SpanType.DfsForward) returns it

#### Scenario: Records with null Context are not indexed by _byNodeId
- **WHEN** AddExecution is called with ExecutionRecord(Context=null)
- **THEN** the record is added to _executions list but NOT added to _byNodeId index

#### Scenario: Records with null Context.NodeId are not indexed by _byNodeId
- **WHEN** AddExecution is called with ExecutionRecord(Context=new TraceContext(NodeId=null))
- **THEN** the record is added to _executions list but NOT added to _byNodeId index (Context?.NodeId resolves to null)

#### Scenario: Records with null SpanType are not indexed by _bySpanType
- **WHEN** AddExecution is called with ExecutionRecord(SpanType=null)
- **THEN** the record is added to _executions list but NOT added to _bySpanType index

#### Scenario: Session lifecycle manages TraceSession state
- **WHEN** SetSession is called with a new TraceSession(TraceId="abc", StartTime=now)
- **THEN** CurrentSession returns this session; EndSession sets EndTime to DateTimeOffset.UtcNow

#### Scenario: Index methods are NOT on ITraceStorage interface
- **WHEN** ITraceStorage interface is inspected for GetByNodeId or GetBySpanType methods
- **THEN** these methods are NOT declared on the interface; they exist only on InMemoryTraceStorage concrete class
