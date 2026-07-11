## ADDED Requirements

### Requirement: ITraceService defines pure read+query facade
ITraceService SHALL define 1 property and 12 methods: CurrentSession (TraceSession? getter), 5 flat read methods (GetExecutions, GetTransitions, GetErrors, GetPageTransitions, GetAICalls returning IReadOnlyList), 6 Node+Span query methods (ReconstructTree, GetNodeSpans, GetNodeVisitTimeline, GetStepTimeline, GetBySpanType, GetStepSpanGroup), and 1 export method (ExportTrace). ITraceService SHALL NOT include any write or session lifecycle methods (StartSessionAsync, EndSessionAsync, Record methods belong on ITraceRecorder). ITraceService is the read+query contract for analysis, dashboard, and ExpectedBehavior consumers.

#### Scenario: ITraceService has exactly 13 members
- **WHEN** ITraceService is inspected for method and property declarations
- **THEN** it declares exactly: CurrentSession (1 property), GetExecutions, GetTransitions, GetErrors, GetPageTransitions, GetAICalls (5 flat read), ReconstructTree, GetNodeSpans, GetNodeVisitTimeline, GetStepTimeline, GetBySpanType, GetStepSpanGroup (6 queries), ExportTrace (1 export)

#### Scenario: ITraceService has no write methods
- **WHEN** ITraceService is inspected for RecordXxxAsync or StartSessionAsync methods
- **THEN** no such methods are declared — write operations belong on ITraceRecorder only

### Requirement: InMemoryTraceService injects InMemoryTraceStorage concrete for index access
InMemoryTraceService SHALL inject InMemoryTraceStorage (concrete class, not ITraceStorage interface) in its constructor. This enables access to GetByNodeId and GetBySpanType index methods that are not on the ITraceStorage interface. Flat read methods SHALL delegate to _storage.GetXxx(). Query methods SHALL use _storage index methods where available (GetNodeSpans, GetNodeVisitTimeline use _storage.GetByNodeId; ReconstructTree, GetBySpanType use _storage.GetBySpanType) and flat list + LINQ filtering with TraceContext access pattern for cross-type queries (GetStepTimeline filters by Context?.StepNumber, GetStepSpanGroup filters by Context?.StepSpanId, GetNodeSpans for non-ExecutionRecord types filters by Context?.NodeId).

#### Scenario: InMemoryTraceService constructor takes InMemoryTraceStorage
- **WHEN** InMemoryTraceService is constructed
- **THEN** its constructor parameter type is InMemoryTraceStorage (concrete), not ITraceStorage (interface)

#### Scenario: ReconstructTree returns TraversalTree from DfsForward edges via Context.NodeId
- **WHEN** _storage contains ExecutionRecords with SpanType=DfsForward and ChildNodeId != null
- **THEN** ReconstructTree() returns TraversalTree with TreeEdge entries containing (Parent=Context?.NodeId, Child=ChildNodeId, Depth, EntryStep=Context?.StepNumber)

#### Scenario: GetNodeSpans aggregates all 5 record types by NodeId via Context?.NodeId
- **WHEN** GetNodeSpans("wifi_node") is called
- **THEN** returned NodeSpans contains: Executions from _storage.GetByNodeId("wifi_node"), Errors filtered by Context?.NodeId=="wifi_node", PageTransitions filtered by Context?.NodeId=="wifi_node", Transitions filtered by Context?.NodeId=="wifi_node"

#### Scenario: GetStepTimeline aggregates all 5 record types by StepNumber via Context?.StepNumber
- **WHEN** GetStepTimeline(5) is called
- **THEN** returned StepTimeline contains: Executions filtered by Context?.StepNumber==5, Transitions filtered by Context?.StepNumber==5, Errors filtered by Context?.StepNumber==5, PageTransitions filtered by Context?.StepNumber==5, AICalls filtered by Context?.StepNumber==5

#### Scenario: GetStepSpanGroup aggregates all 5 record types by StepSpanId via Context?.StepSpanId
- **WHEN** GetStepSpanGroup("abc-000005") is called
- **THEN** returned StepSpanGroup contains all 5 record types filtered by Context?.StepSpanId=="abc-000005"

#### Scenario: GetNodeVisitTimeline finds entry and exit from DfsForward/DfsBacktrack
- **WHEN** _storage.GetByNodeId("wifi_node") contains records with SpanType=DfsForward and SpanType=DfsBacktrack
- **THEN** GetNodeVisitTimeline("wifi_node") returns NodeVisitTimeline with EntryStep=DfsForward's Context?.StepNumber, ExitStep=DfsBacktrack's Context?.StepNumber

### Requirement: 6 query result types are sealed record classes with ImmutableArray fields
TraversalTree, TreeEdge, NodeSpans, NodeVisitTimeline, StepTimeline, StepSpanGroup SHALL all be sealed record classes with ImmutableArray fields. They SHALL be computed at query time from flat records + indexes, NOT stored in ITraceStorage.

#### Scenario: TraversalTree contains Edges and RootNodeId
- **WHEN** a TraversalTree is returned from ReconstructTree()
- **THEN** it has Edges (ImmutableArray<TreeEdge>) and RootNodeId (string)

#### Scenario: NodeSpans contains per-type ImmutableArrays
- **WHEN** a NodeSpans is returned from GetNodeSpans(nodeId)
- **THEN** it has NodeId (string), Executions, Errors, PageTransitions, Transitions (all ImmutableArray)
