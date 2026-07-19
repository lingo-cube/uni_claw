## ADDED Requirements

### Requirement: StepContext is a sealed record class encapsulating step dependencies

`StepContext` SHALL be a `sealed record class` that bundles all dependencies required for a single FSM step execution. It SHALL contain 15 fields: `context` (TraversalRuntimeContext), `state_machine` (TraversalFSM), `vision` (IVisionProvider), `action` (IActionExecutor), `child_mgr` (IDynamicChildManager), `node_registry` (INodeRegistry), `trace` (ITraceCoordinator), `snapshot_mgr` (IPageSnapshotManager), `stack` (INodeStackAdapter), `error_handler` (ErrorHandler?), `popup_handler` (PopupHandler?), `last_known_path` (string?), `last_recorded_path` (string?), `last_recorded_action` (string?), and `scroll_swipe` (ScrollSwipeConfig). `StepContext` SHALL be constructed once per step and SHALL NOT be mutated after construction (record immutability).

#### Scenario: StepContext contains all 15 dependency fields
- **WHEN** `StepContext` is inspected for field declarations
- **THEN** it contains exactly: `context`, `state_machine`, `vision`, `action`, `child_mgr`, `node_registry`, `trace`, `snapshot_mgr`, `stack`, `error_handler`, `popup_handler`, `last_known_path`, `last_recorded_path`, `last_recorded_action`, `scroll_swipe`

#### Scenario: StepContext is sealed record class
- **WHEN** the type declaration of `StepContext` is inspected
- **THEN** it is `sealed record class` (not mutable class)

#### Scenario: StepContext is immutable after construction
- **WHEN** a `StepContext` instance is created and an attempt is made to reassign one of its fields
- **THEN** the compiler rejects the assignment (record fields are init-only)

### Requirement: StepOrchestrator executes_step via 14-step interception layer wrapping TraversalFSM

`StepOrchestrator` SHALL be a sealed class that wraps `TraversalFSM.StepAsync()` with a 14-step interception layer. The `ExecuteStepAsync(ctx)` method SHALL execute steps 1 through 14 in strict sequential order, using `await` for async operations. No step SHALL be skipped unless its precondition is explicitly not met. The orchestrator SHALL NOT short-circuit the FSM transition; steps 8-10 are interception overlays on top of the FSM result, delegated to `IInterceptionHandler` (default implementation `InterceptionHandler`) rather than implemented inline. StepOrchestrator.ExecuteStepAsync() is invoked by `TraversalEngine.RunAsync()` per step iteration.

#### Scenario: StepOrchestrator called by TraversalEngine
- **WHEN** TraversalEngine.RunAsync() iterates the step loop
- **THEN** each iteration calls `await StepOrchestrator.ExecuteStepAsync(ctx)` and processes the StepResult (leaf-pop, child-push→NodeSelect, trace recording, termination checks)

#### Scenario: Step 1 creates NodeStackAdapter from context and node_registry
- **WHEN** `execute_step` begins
- **THEN** a `NodeStackAdapter` is constructed from `ctx.context` and `ctx.node_registry` before any other step executes

#### Scenario: Step 2 records step start via trace
- **WHEN** step 2 executes
- **THEN** `ctx.trace.record_step_start(node_id, result)` is called with the current node ID; if `ctx.trace.active=False`, the call is a no-op

#### Scenario: Step 3 calls state_machine.StepAsync and captures transition result
- **WHEN** step 3 executes
- **THEN** `await ctx.state_machine.StepAsync(ctx)` is invoked and the transition result is captured for subsequent interception steps
- **AND** no `.GetAwaiter().GetResult()` is present in the ExecuteStepAsync method

#### Scenario: Step 4 records page snapshot when path changed
- **WHEN** step 4 executes and `ctx.context.current_path` differs from `ctx.last_known_path`
- **THEN** `ctx.trace.record_page_analysis(ctx.context.current_page_analysis)` is called

#### Scenario: Step 4 skips page snapshot when path unchanged
- **WHEN** step 4 executes and `ctx.context.current_path` equals `ctx.last_known_path`
- **THEN** `record_page_analysis` is NOT called for this step

#### Scenario: Step 5 records action execution from handler metrics
- **WHEN** step 5 executes and the FSM handler produced action metrics
- **THEN** `ctx.trace.record_action_execution(action, target, success)` is called with the metrics values

#### Scenario: Step 6 records metrics spans
- **WHEN** step 6 executes and handler metrics contain sub-span data
- **THEN** `ctx.trace.record_metrics_as_spans(metrics)` is called, dispatching to ai_call/execution/restore/error sub-recorders

#### Scenario: Step 7 records state transition
- **WHEN** step 7 executes
- **THEN** `ctx.trace.record_state_transition(from_state, to_state)` is called with the FSM transition result

#### Scenario: Step 8 BRANCH interception calls TryHandleScrollAsync when needed
- **WHEN** step 8 BRANCH interception reaches the scroll decision point
- **THEN** `await TryHandleScrollAsync(ctx, currentFrame, ...)` is called

#### Scenario: Step 8 BRANCH interception does NOT trigger from PRECONDITION_CHECK
- **WHEN** the FSM transition results in a move to `BRANCH` state and the from_state is `PRECONDITION_CHECK`
- **THEN** the BRANCH interception logic is NOT executed; PRECONDITION_CHECK SHALL NOT be a valid source for BRANCH interception per decision D-1

#### Scenario: Step 9 NODE_SELECT calls PressBackAsync with await
- **WHEN** step 9 triggers back navigation (DYNAMIC_MATCH exhausted, depth > 1)
- **THEN** `await ctx.Action.PressBackAsync()` is called
- **AND** no `.GetAwaiter().GetResult()` is present

#### Scenario: Step 10 FRAME_COMPLETE interception overrides when DYNAMIC_MATCH has remaining children
- **WHEN** the FSM transition results in a move to `FRAME_COMPLETE` state and the current node's `children_strategy` is `DYNAMIC_MATCH` and `ctx.child_mgr.get_next_unvisited_child(current_node, ctx.context)` returns a child
- **THEN** the FRAME_COMPLETE transition is overridden: the remaining child is pushed onto the stack instead of completing the frame

#### Scenario: Step 10 FRAME_COMPLETE proceeds normally when DYNAMIC_MATCH has no remaining children
- **WHEN** the FSM transition results in a move to `FRAME_COMPLETE` state and `ctx.child_mgr.get_next_unvisited_child(current_node, ctx.context)` returns null (no remaining children)
- **THEN** the FRAME_COMPLETE transition proceeds without override; the frame is completed normally

#### Scenario: Step 11 determines next state considering overrides
- **WHEN** step 11 executes
- **THEN** the final next state is determined by considering: (a) the FSM transition result, (b) whether `should_complete_frame` was forced in step 8, (c) whether a child was pushed overriding FRAME_COMPLETE in step 10; the override logic takes precedence over the FSM result when triggered

#### Scenario: Step 12 updates visited_nodes
- **WHEN** step 12 executes
- **THEN** `ctx.context.MarkNodeVisited(current_node_id)` is called if the current node was successfully processed in this step

#### Scenario: Step 13 invalidates dynamic children cache when path changed
- **WHEN** step 13 executes and `ctx.context.current_path` differs from `ctx.last_known_path`
- **THEN** `ctx.child_mgr.invalidate(current_node_id)` is called to remove stale `_dynamic_children` entries; the `_generated_pairs` dedup set SHALL persist across invalidation (per DynamicChildManager spec)

#### Scenario: Step 13 skips invalidation when path unchanged
- **WHEN** step 13 executes and `ctx.context.current_path` equals `ctx.last_known_path`
- **THEN** `invalidate` is NOT called for this step

#### Scenario: Step 14 records step end via trace
- **WHEN** step 14 executes
- **THEN** `ctx.trace.record_step_end(node_id, result)` is called; if `ctx.trace.active=False`, the call is a no-op

#### Scenario: ExecuteStepAsync returns Task<StepResult>
- **WHEN** `ExecuteStepAsync` is invoked
- **THEN** it returns `Task<StepResult>` containing the 6 outcome fields

### Requirement: TryHandleScroll executes scroll as async operation+judgment

`TryHandleScrollAsync` SHALL be an `internal static async Task<bool>` method on `InterceptionHandler` (moved from `StepOrchestrator`). It SHALL NOT use `.GetAwaiter().GetResult()`. Instead:
1. Check `ctx.Vision.HasScroll()` and `ctx.Vision.IsEndOfList()` (sync, no change)
2. Resolve swipe config via `ctx.Vision.GetScrollSwipeConfig() ?? ctx.ScrollSwipe`
3. Execute swipe: `await ctx.Action.SwipeAsync(cfg.StartX, cfg.StartY, cfg.EndX, cfg.EndY, cfg.DurationMs)`
4. Re-analyze: `var after = await ctx.Vision.AnalyzeCurrentPageAsync()`
5. Judge: seen-set diff to determine if new elements were revealed

#### Scenario: TryHandleScrollAsync awaits SwipeAsync
- **WHEN** `TryHandleScrollAsync` executes the scroll operation
- **THEN** `await ctx.Action.SwipeAsync(...)` is called with coordinates from the resolved config
- **AND** no `.GetAwaiter().GetResult()` is present

#### Scenario: TryHandleScrollAsync awaits AnalyzeCurrentPageAsync
- **WHEN** `TryHandleScrollAsync` re-analyzes the page after swipe
- **THEN** `await ctx.Vision.AnalyzeCurrentPageAsync()` is called

#### Scenario: TryHandleScrollAsync uses config coordinates not consts
- **WHEN** `TryHandleScrollAsync` resolves swipe coordinates
- **THEN** the source is `ScrollSwipeConfig`, not hardcoded `const` fields

### Requirement: Anti-loop mechanism prevents BRANCH-to-NODE_SELECT infinite loops

The anti-loop mechanism in step 9 SHALL prevent infinite BRANCH-to-NODE_SELECT loops when a DYNAMIC_MATCH node has no remaining unvisited children. When triggered, the orchestrator SHALL execute `back + pop stack + return immediately`. The anti-loop MUST NOT push any node onto the stack and MUST NOT transition to NODE_SELECT. Three scenarios MUST be handled: normal loop (child exists), dead loop trigger (no child), and consecutive multiple triggers.

#### Scenario: Normal loop — DYNAMIC_MATCH has remaining children, normal push occurs
- **WHEN** step 9 executes for a NODE_SELECT transition with a DYNAMIC_MATCH node that has unvisited children
- **THEN** `get_next_unvisited_child` returns a child node; the child is pushed onto the stack via `ctx.stack.push(child)`; the orchestrator proceeds to step 10 and beyond; no anti-loop back+pop is executed

#### Scenario: Dead loop trigger — DYNAMIC_MATCH has no remaining children, forced back+pop
- **WHEN** step 9 executes for a NODE_SELECT transition with a DYNAMIC_MATCH node that has zero unvisited children remaining
- **THEN** `get_next_unvisited_child` returns null; anti-loop is triggered: (1) the orchestrator calls the back action (press_back equivalent), (2) `ctx.stack.pop()` is called to remove the current frame, (3) `execute_step` returns immediately with a `StepResult` indicating `frame_completed=True` and `child_pushed=False`

#### Scenario: Multiple consecutive anti-loop triggers — each triggers independently
- **WHEN** step 9 triggers anti-loop on node A (no remaining children), then on the next step the parent node B (also DYNAMIC_MATCH) has no remaining children
- **THEN** anti-loop triggers again for node B: back + pop + return immediately; each trigger is independent and does not carry state from the previous trigger; the stack depth decreases by 1 for each trigger

#### Scenario: Anti-loop does not transition to NODE_SELECT after back+pop
- **WHEN** anti-loop is triggered and back+pop+return is executed
- **THEN** the next state SHALL NOT be NODE_SELECT; the returned `StepResult.next_state` reflects the frame completion semantics, not a re-entry into NODE_SELECT for the exhausted node

### Requirement: FRAME_COMPLETE override intercepts premature frame completion

When the FSM transitions to `FRAME_COMPLETE` but the current node uses `DYNAMIC_MATCH` and still has unvisited children, step 10 SHALL override the transition: instead of completing the frame, the remaining child SHALL be pushed onto the stack. This override MUST preserve stack state consistency. Three scenarios MUST be handled: no remaining (normal completion), override (push remaining child), and state consistency after override.

#### Scenario: No remaining children — FRAME_COMPLETE proceeds normally
- **WHEN** step 10 executes for a FRAME_COMPLETE transition with a DYNAMIC_MATCH node where `get_next_unvisited_child` returns null
- **THEN** the override is NOT triggered; the frame completes normally; `StepResult.frame_completed=True` and `StepResult.child_pushed=False`

#### Scenario: Override — DYNAMIC_MATCH has remaining children, push overrides FRAME_COMPLETE
- **WHEN** step 10 executes for a FRAME_COMPLETE transition with a DYNAMIC_MATCH node where `get_next_unvisited_child` returns a child node
- **THEN** the override IS triggered: (1) the FRAME_COMPLETE transition is cancelled, (2) the remaining child is pushed onto the stack via `ctx.stack.push(child)`, (3) `StepResult.child_pushed=True` and `StepResult.frame_completed=False`, (4) the next state becomes NODE_SELECT (or the state appropriate for entering the new child), not FRAME_COMPLETE

#### Scenario: Override preserves stack state consistency
- **WHEN** step 10 overrides FRAME_COMPLETE by pushing a remaining child
- **THEN** the stack state is consistent: (1) the pushed child's frame is at the top of the stack, (2) the parent frame remains below it unchanged, (3) `ctx.context.CurrentFrame` points to the newly pushed child, (4) no orphaned or duplicate frames exist in the stack

#### Scenario: FRAME_COMPLETE override and anti-loop interaction — override precedes anti-loop in next step
- **WHEN** a step first triggers FRAME_COMPLETE override (step 10 pushes a child), and then on the next step anti-loop triggers for the pushed child (step 9, no remaining children)
- **THEN** the override and anti-loop interact correctly: (1) the override pushes a child onto the stack in step N, (2) in step N+1, the child enters NODE_SELECT, (3) anti-loop triggers because the child has no remaining children, (4) back + pop removes the child's frame, returning to the parent frame, (5) the stack depth at the end of step N+1 equals the stack depth before step N

### Requirement: BRANCH interception source states are restricted to EXECUTE, RESULT_VERIFY, and NODE_SELECT

The BRANCH interception in step 8 SHALL only activate when the from_state of the FSM transition is one of `EXECUTE`, `RESULT_VERIFY`, or `NODE_SELECT`. `PRECONDITION_CHECK` SHALL NOT be a valid source for BRANCH interception. This restriction aligns with decision D-1: the Python `VALID_TRANSITIONS` matrix defined `PRECONDITION_CHECK → BRANCH`, but the V6.7 handler never returns BRANCH from `PRECONDITION_CHECK` — that transition path is dead code and SHALL NOT be ported.

#### Scenario: BRANCH interception activates from EXECUTE
- **WHEN** the FSM transitions from `EXECUTE` to `BRANCH`
- **THEN** step 8 BRANCH interception logic executes: `get_next_unvisited_child` is called

#### Scenario: BRANCH interception activates from RESULT_VERIFY
- **WHEN** the FSM transitions from `RESULT_VERIFY` to `BRANCH`
- **THEN** step 8 BRANCH interception logic executes: `get_next_unvisited_child` is called

#### Scenario: BRANCH interception activates from NODE_SELECT
- **WHEN** the FSM transitions from `NODE_SELECT` to `BRANCH`
- **THEN** step 8 BRANCH interception logic executes: `get_next_unvisited_child` is called

#### Scenario: BRANCH interception does NOT activate from PRECONDITION_CHECK
- **WHEN** the FSM transitions from `PRECONDITION_CHECK` to `BRANCH`
- **THEN** step 8 BRANCH interception logic is NOT executed; the transition is treated as an invalid path and SHALL be rejected by the FSM transition matrix validation (per D-1, this transition is removed from the valid transitions set)

#### Scenario: BRANCH interception does NOT activate from non-listed states
- **WHEN** the FSM transitions to `BRANCH` from any state other than EXECUTE, RESULT_VERIFY, or NODE_SELECT (e.g., from ERROR_HANDLING or POPUP_HANDLING)
- **THEN** step 8 BRANCH interception logic is NOT executed for those source states; the BRANCH transition proceeds via normal FSM logic without child retrieval

### Requirement: DynamicChildManager generates and caches dynamic children with cross-invalidation dedup persistence

`DynamicChildManager` SHALL be a sealed class managing dynamic child node generation for `STATIC` and `DYNAMIC_MATCH` children strategies. For `STATIC` strategy, `get_next_unvisited_child` SHALL iterate over the node's `static_children` list. For `DYNAMIC_MATCH` strategy, `get_next_unvisited_child` SHALL generate children via the 9-step pipeline if not cached, then iterate cached children. The `generate` pipeline SHALL execute 9 steps including dedup. Cache invalidation SHALL remove `_dynamic_children` entries but SHALL NOT remove `_generated_pairs` dedup set entries — dedup SHALL persist across invalidation.

#### Scenario: get_next_unvisited_child with STATIC strategy iterates static_children
- **WHEN** `get_next_unvisited_child(node, context)` is called on a node with `children_strategy=STATIC`
- **THEN** the method iterates over `node.static_children` and returns the first child whose ID is NOT in `context.visited_nodes`; if all children are visited, returns null

#### Scenario: get_next_unvisited_child with DYNAMIC_MATCH uses cached children when available
- **WHEN** `get_next_unvisited_child(node, context)` is called on a node with `children_strategy=DYNAMIC_MATCH` and `_dynamic_children` contains an entry for `node.node_id`
- **THEN** the cached children list is used; `generate` is NOT called; the first unvisited child from the cached list is returned

#### Scenario: get_next_unvisited_child with DYNAMIC_MATCH generates when not cached
- **WHEN** `get_next_unvisited_child(node, context)` is called on a node with `children_strategy=DYNAMIC_MATCH` and `_dynamic_children` does NOT contain an entry for `node.node_id`
- **THEN** the `generate(node, context)` pipeline is invoked to produce the children list; the result is cached in `_dynamic_children[node.node_id]`; the first unvisited child is returned

#### Scenario: generate pipeline step 1 computes page fingerprint
- **WHEN** the `generate` pipeline begins
- **THEN** step 1 calls `PageSnapshotManager.fingerprint(context.current_page_analysis)` to compute the page fingerprint for dedup

#### Scenario: generate pipeline step 2 converts DynamicRules to matcher rules
- **WHEN** generate pipeline step 2 executes
- **THEN** the node's `dynamic_match_rules` are converted into `MatchCondition` objects suitable for `DynamicMatcher`

#### Scenario: generate pipeline step 3 extracts items from page_analysis
- **WHEN** generate pipeline step 3 executes
- **THEN** items are extracted from `context.current_page_analysis` for matching against the converted rules

#### Scenario: generate pipeline step 4 calls DynamicMatcher.match_all
- **WHEN** generate pipeline step 4 executes
- **THEN** `DynamicMatcher.match_all(match_conditions, items)` is called to produce `MatchResult` entries for each item

#### Scenario: generate pipeline step 5 instantiates child nodes for GENERATE_CHILD actions
- **WHEN** a `MatchResult` has `action=GenerateChild`
- **THEN** `TemplateInstantiator.instantiate(template, context, parent_path)` is called to produce a `TraversalNode` child

#### Scenario: generate pipeline step 6 dedup via _generated_pairs set
- **WHEN** generate pipeline step 6 executes for a candidate child
- **THEN** the pair `(page_fingerprint, child.name)` is checked against `_generated_pairs`; if the pair already exists in `_generated_pairs`, the child is skipped (dedup); if the pair does NOT exist, the child is kept and the pair is added to `_generated_pairs`

#### Scenario: generate pipeline step 7 sets precondition path
- **WHEN** generate pipeline step 7 executes for a retained child
- **THEN** `child.precondition.path` is set to `list(context.current_path) + [child.name]`

#### Scenario: generate pipeline step 8 registers child in node_registry
- **WHEN** generate pipeline step 8 executes for a retained child
- **THEN** `ctx.node_registry.register(child)` is called so the child is available for subsequent lookups

#### Scenario: generate pipeline step 9 records dynamic lifecycle trace event
- **WHEN** generate pipeline step 9 executes for a retained child
- **THEN** `ctx.trace.record_dynamic_lifecycle(event, child.node_id, parent_id, rule_id, element_id)` is called to record the generation event

#### Scenario: invalidate removes _dynamic_children entry but preserves _generated_pairs
- **WHEN** `invalidate(node_id)` is called
- **THEN** the entry for `node_id` is removed from `_dynamic_children`; however, entries in `_generated_pairs` that were created during the generation of that node's children SHALL remain in `_generated_pairs` — dedup state persists across invalidation events

#### Scenario: dedup prevents re-generation of same element after invalidation
- **WHEN** `invalidate(node_id)` removes a `_dynamic_children` cache entry, and subsequently `get_next_unvisited_child` triggers `generate` again for the same node on the same page (same fingerprint)
- **THEN** the `_generated_pairs` set still contains the `(fingerprint, child.name)` pairs from the previous generation; any child whose name+fingerprint pair already exists in `_generated_pairs` is skipped and NOT regenerated

#### Scenario: dedup allows new elements on a changed page
- **WHEN** `invalidate(node_id)` removes a cache entry, and subsequently the page has changed (different fingerprint), and `generate` produces a child with a name that did NOT appear in `_generated_pairs` for the new fingerprint
- **THEN** the new child is NOT skipped by dedup; it is retained, registered, and added to `_generated_pairs` with the new fingerprint

### Requirement: TraceCoordinator provides 16+ span type methods with active gate and Log-and-Continue pattern

`TraceCoordinator` SHALL be a sealed class that provides 16+ span type methods for recording trace events during traversal. All methods SHALL be no-op when `active=False` (recorder is null or no `trace_id`). All write operations SHALL use the "Log and Continue" pattern: try-catch wrapping where failures are logged as warnings and MUST NOT interrupt the traversal. ULID generation SHALL produce 26-character Crockford Base32 identifiers. Trace level gates SHALL control which span types are recorded based on `plan.entry_config.trace_level`.

#### Scenario: record_state_transition records from and to states
- **WHEN** `TraceCoordinator.record_state_transition(from_state, to_state)` is called with `active=True`
- **THEN** a span with `span_type="state_transition"` is recorded containing both state values

#### Scenario: record_root_node_pushed records INITIALIZING-to-TRAVERSING transition
- **WHEN** `TraceCoordinator.record_root_node_pushed(node_id)` is called with `active=True`
- **THEN** a span recording the INITIALIZING to TRVERSING transition is emitted with the root node ID

#### Scenario: record_page_analysis records page snapshot span
- **WHEN** `TraceCoordinator.record_page_analysis(page_analysis)` is called with `active=True`
- **THEN** a span with `span_type="page_snapshot"` is recorded containing the page analysis data

#### Scenario: record_action_execution records execution span
- **WHEN** `TraceCoordinator.record_action_execution(action, target, success)` is called with `active=True`
- **THEN** a span with `span_type="execution"` is recorded containing the action, target, and success flag

#### Scenario: record_metrics_as_spans dispatches to sub-recorders
- **WHEN** `TraceCoordinator.record_metrics_as_spans(metrics)` is called with `active=True`
- **THEN** the method dispatches metric entries to ai_call, execution, restore, and error sub-recorders based on the metric type

#### Scenario: record_skip_span records dynamic_matching skip
- **WHEN** `TraceCoordinator.record_skip_span(match_result)` is called with `active=True`
- **THEN** a span with `span_type="dynamic_matching"` and `action="skip_element"` is recorded

#### Scenario: record_execution_span records execution including is_restore flag
- **WHEN** `TraceCoordinator.record_execution_span(ex)` is called with `active=True`
- **THEN** the span includes the `is_restore` flag from the execution data

#### Scenario: record_ai_call_span records AI capability, latency, and tokens
- **WHEN** `TraceCoordinator.record_ai_call_span(ai)` is called with `active=True`
- **THEN** the span records AI capability, latency_ms, and token_count fields

#### Scenario: record_error_span records error type, message, and severity
- **WHEN** `TraceCoordinator.record_error_span(error_type, message, severity)` is called with `active=True`
- **THEN** a span with `span_type="error"` is recorded containing the error_type string, message, and severity level

#### Scenario: record_decision records stack_depth and current_path
- **WHEN** `TraceCoordinator.record_decision(decision, ctx)` is called with `active=True`
- **THEN** the span records the decision string, stack_depth (int), and current_path (list of strings) from the context

#### Scenario: record_page_transition records PageTransitionSpan
- **WHEN** `TraceCoordinator.record_page_transition(from_path, to_path, transition_type)` is called with `active=True`
- **THEN** a `PageTransitionSpan` is recorded containing from_path, to_path, and transition_type

#### Scenario: record_dynamic_lifecycle records DynamicNodeLifecycleSpan
- **WHEN** `TraceCoordinator.record_dynamic_lifecycle(event, node_id, parent_id, rule_id, element_id)` is called with `active=True`
- **THEN** a `DynamicNodeLifecycleSpan` is recorded containing all five parameters

#### Scenario: record_state_decision records StateDecisionSpan
- **WHEN** `TraceCoordinator.record_state_decision(decision, node_id, metadata)` is called with `active=True`
- **THEN** a `StateDecisionSpan` is recorded containing the decision, node_id, and metadata dict

#### Scenario: record_step_start records step boundary start
- **WHEN** `TraceCoordinator.record_step_start(node_id, result)` is called with `active=True`
- **THEN** a step boundary start span is recorded with the node_id

#### Scenario: record_step_end records step boundary end
- **WHEN** `TraceCoordinator.record_step_end(node_id, result)` is called with `active=True`
- **THEN** a step boundary end span is recorded with the node_id and result

#### Scenario: All methods are no-op when active=False
- **WHEN** `TraceCoordinator` has `active=False` (recorder is null or trace_id is unset)
- **THEN** every one of the 16+ span methods returns immediately without executing any logic or producing any output

#### Scenario: Trace write failure triggers Log-and-Continue, not traversal interruption
- **WHEN** any `TraceCoordinator` write method (e.g., `record_state_transition`) throws an exception during execution
- **THEN** the exception is caught, a warning is logged with the method name and exception summary, and the traversal step continues; the exception MUST NOT propagate to the caller or interrupt the step orchestrator

#### Scenario: should_record_entry_attempt gates based on trace_level
- **WHEN** `TraceCoordinator.should_record_entry_attempt()` is called and `plan.entry_config.trace_level` is `Basic` or higher
- **THEN** the method returns `true`; when `trace_level` is `None`, it returns `false`

#### Scenario: should_record_vision_call gates based on trace_level
- **WHEN** `TraceCoordinator.should_record_vision_call()` is called and `plan.entry_config.trace_level` is `Detailed` or higher
- **THEN** the method returns `true`; when `trace_level` is `None` or `Basic`, it returns `false`

#### Scenario: ULID generation produces 26-char Crockford Base32 identifiers
- **WHEN** `TraceCoordinator` generates a span_id
- **THEN** the ULID is exactly 26 characters using Crockford Base32 encoding; first 10 characters encode the 48-bit millisecond timestamp, last 16 characters encode the 80-bit random component; ULIDs within the same millisecond are monotonically sortable

### Requirement: EntryPolicyExecutor executes strategy chain with 3 strategies and wait condition verification

`EntryPolicyExecutor` SHALL be a sealed class that executes the entry policy to bring the device to the target app's starting state. It SHALL build a strategy chain via `_build_chain`: (1) primary strategy from `policy.strategy`, (2) fallback strategy from `policy.fallback` if different from primary, (3) always append `BIND_CURRENT_SCREEN` as final fallback. Three strategy types SHALL be supported: `DIRECT_DEEPLINK`, `COLD_LAUNCH`, and `BIND_CURRENT_SCREEN`. Wait condition verification SHALL support two modes: `fast` (single check) and `polling` (repeated checks until timeout).

#### Scenario: Strategy chain includes primary, fallback, and BIND_CURRENT_SCREEN
- **WHEN** `_build_chain(policy)` is called with `policy.strategy=DIRECT_DEEPLINK` and `policy.fallback=COLD_LAUNCH`
- **THEN** the chain is: [DIRECT_DEEPLINK, COLD_LAUNCH, BIND_CURRENT_SCREEN] — three strategies in order

#### Scenario: Strategy chain omits duplicate fallback when same as primary
- **WHEN** `_build_chain(policy)` is called with `policy.strategy=DIRECT_DEEPLINK` and `policy.fallback=DIRECT_DEEPLINK`
- **THEN** the chain is: [DIRECT_DEEPLINK, BIND_CURRENT_SCREEN] — the duplicate is omitted, BIND_CURRENT_SCREEN is always appended

#### Scenario: Strategy chain always includes BIND_CURRENT_SCREEN as final fallback
- **WHEN** `_build_chain(policy)` is called with any policy configuration
- **THEN** the last element of the chain is always `BIND_CURRENT_SCREEN`

#### Scenario: DIRECT_DEEPLINK strategy sends deeplink and waits
- **WHEN** the `DIRECT_DEEPLINK` strategy executes
- **THEN** a deeplink intent is sent to the device, followed by waiting `action_delay_ms` milliseconds

#### Scenario: COLD_LAUNCH strategy navigates home, finds icon, clicks, and waits
- **WHEN** the `COLD_LAUNCH` strategy executes
- **THEN** the sequence is: press_home → find_app_icon → click_icon → wait `action_delay_ms` milliseconds

#### Scenario: BIND_CURRENT_SCREEN strategy waits assuming already on target
- **WHEN** the `BIND_CURRENT_SCREEN` strategy executes
- **THEN** only `action_delay_ms` milliseconds of waiting occurs; no navigation actions are performed

#### Scenario: Fast wait mode performs single check
- **WHEN** wait condition verification runs in `fast` mode (`entry_config.wait_mode=Fast`)
- **THEN** a single condition check is performed; the result is accepted immediately without retry

#### Scenario: Polling wait mode performs repeated checks until timeout
- **WHEN** wait condition verification runs in `polling` mode (`entry_config.wait_mode=Polling`) with `wait_timeout_seconds=10.0` and `wait_interval_ms=500`
- **THEN** condition checks are performed every 500ms until the condition passes or 10 seconds elapse; on timeout, the strategy is considered failed and the next strategy in the chain is attempted

#### Scenario: Strategy chain advances on failure
- **WHEN** the primary strategy (e.g., DIRECT_DEEPLINK) fails (deeplink not received or wait condition timeout)
- **THEN** the executor advances to the next strategy in the chain (e.g., COLD_LAUNCH) and attempts it

#### Scenario: BIND_CURRENT_SCREEN always succeeds as final fallback
- **WHEN** all preceding strategies in the chain fail and `BIND_CURRENT_SCREEN` executes
- **THEN** it always returns success (it assumes the device is already on the correct screen); traversal proceeds regardless of actual state

### Requirement: PageCacheManager provides update and restore operations storing PageCacheInfo in context

`PageCacheManager` SHALL be a sealed class providing two operations: `update(path, page_info)` and `restore(path)`. `update` SHALL store a `PageCacheInfo` (containing items, timestamp, and screen_hash) in `context.page_cache[path]`. `restore` SHALL return the cached `PageCacheInfo.items` for the given path, or null if no cache entry exists. `PageCacheManager` SHALL NOT implement TTL expiration or size limits in Phase 2; those are deferred to Phase 3.

#### Scenario: update stores PageCacheInfo in context.page_cache
- **WHEN** `PageCacheManager.update("/home/settings", page_info)` is called where `page_info` contains items, a timestamp, and a screen_hash
- **THEN** `context.page_cache["/home/settings"]` contains the `PageCacheInfo` with all three fields

#### Scenario: restore returns cached items for existing path
- **WHEN** `PageCacheManager.restore("/home/settings")` is called after a previous `update` for that path
- **THEN** the method returns the items stored in the `PageCacheInfo` for that path

#### Scenario: restore returns null for non-existent path
- **WHEN** `PageCacheManager.restore("/unknown/path")` is called and no cache entry exists for that path
- **THEN** the method returns null (no items)

#### Scenario: PageCacheInfo is a sealed record class with 3 fields
- **WHEN** `PageCacheInfo` type declaration is inspected
- **THEN** it is `sealed record class` with fields: `items` (IReadOnlyList<MenuItem>), `timestamp` (DateTimeOffset), and `screen_hash` (int)

#### Scenario: PageCacheManager does not implement TTL or size limits in Phase 2
- **WHEN** `PageCacheManager` implementation is inspected for TTL expiration or cache size limit logic
- **THEN** no such logic exists; Phase 2 defers these concerns

### Requirement: PageSnapshotManager provides pure-function fingerprint and has_changed operations

`PageSnapshotManager` SHALL be a sealed class with pure-function semantics (no mutable state). `fingerprint(page_analysis)` SHALL compute an integer hash from sorted `(type, name)` tuples extracted from `page_analysis.items`, returning 0 for null or empty input. `has_changed(before, after)` SHALL return `true` when `fingerprint(before) != fingerprint(after)` and `false` when they are equal. Both methods SHALL be deterministic: the same input MUST always produce the same output.

#### Scenario: fingerprint computes hash from sorted type-name tuples
- **WHEN** `PageSnapshotManager.fingerprint(page_analysis)` is called with a `PageAnalysis` containing items with types ["switch", "button"] and names ["wifi", "sound"]
- **THEN** the method extracts tuples [("button", "sound"), ("switch", "wifi")] (sorted alphabetically by type then name) and computes a deterministic integer hash from these tuples

#### Scenario: fingerprint returns 0 for null input
- **WHEN** `PageSnapshotManager.fingerprint(null)` is called
- **THEN** the method returns 0

#### Scenario: fingerprint returns 0 for empty items
- **WHEN** `PageSnapshotManager.fingerprint(page_analysis)` is called with a `PageAnalysis` whose items list is empty
- **THEN** the method returns 0

#### Scenario: fingerprint is deterministic for same input
- **WHEN** `PageSnapshotManager.fingerprint(page_analysis)` is called twice with the same `PageAnalysis` instance (or an identical copy)
- **THEN** both calls return the same integer value

#### Scenario: has_changed returns true when fingerprints differ
- **WHEN** `PageSnapshotManager.has_changed(before, after)` is called and `fingerprint(before) != fingerprint(after)`
- **THEN** the method returns `true`

#### Scenario: has_changed returns false when fingerprints are equal
- **WHEN** `PageSnapshotManager.has_changed(before, after)` is called and `fingerprint(before) == fingerprint(after)`
- **THEN** the method returns `false`

#### Scenario: has_changed returns true when before is null and after is not
- **WHEN** `PageSnapshotManager.has_changed(null, after)` is called where `after` has non-empty items
- **THEN** the method returns `true` (0 != computed_hash)

#### Scenario: PageSnapshotManager has no mutable state
- **WHEN** `PageSnapshotManager` is inspected for mutable fields or properties with setters
- **THEN** no mutable state exists; the class operates as a pure function holder

### Requirement: NodeStackAdapter adapts NodeStack and INodeRegistry for orchestrator consumption

`NodeStackAdapter` SHALL be a sealed class constructed from `TraversalRuntimeContext` and `INodeRegistry`. It SHALL wrap the `NodeStack` operations needed by the step orchestrator (push, pop, peek) with registry lookups that resolve node IDs to `TraversalNode` instances. The adapter SHALL NOT introduce new stack semantics; it SHALL delegate all stack operations to the underlying `NodeStack`.

#### Scenario: NodeStackAdapter push resolves node ID via registry before pushing
- **WHEN** `NodeStackAdapter.push(child)` is called
- **THEN** the child node is registered in the node_registry if not already present, and then `NodeStack.push(child.node_id)` is called

#### Scenario: NodeStackAdapter pop delegates to NodeStack.pop and resolves node
- **WHEN** `NodeStackAdapter.pop()` is called
- **THEN** it delegates to the underlying `NodeStack.pop()` to remove the top frame, then resolves the popped node_id via `node_registry.get(node_id)` to return the corresponding `TraversalNode`

#### Scenario: NodeStackAdapter peek resolves top frame to TraversalNode
- **WHEN** `NodeStackAdapter.peek()` is called
- **THEN** it calls `NodeStack.peek()` to get the top node_id, then resolves it via `node_registry.get(node_id)` to return the corresponding `TraversalNode`

### Requirement: StepResult is a sealed record class capturing orchestrator step outcome

`StepResult` SHALL be a `sealed record class` containing: `next_state` (TraversalState), `path_changed` (bool), `child_pushed` (bool), `frame_completed` (bool), `anti_loop_triggered` (bool), `frame_override_triggered` (bool). These flags SHALL capture which interception logic was activated during the step, enabling downstream logic and testing to verify interception behavior without inspecting internal orchestrator state.

#### Scenario: StepResult contains all 6 outcome fields
- **WHEN** `StepResult` is inspected for field declarations
- **THEN** it contains exactly: `next_state`, `path_changed`, `child_pushed`, `frame_completed`, `anti_loop_triggered`, `frame_override_triggered`

#### Scenario: StepResult is sealed record class
- **WHEN** the type declaration of `StepResult` is inspected
- **THEN** it is `sealed record class`

#### Scenario: StepResult reflects anti-loop trigger
- **WHEN** `execute_step` triggers anti-loop in step 9 (DYNAMIC_MATCH, no remaining children)
- **THEN** the returned `StepResult` has `anti_loop_triggered=True`, `child_pushed=False`, `frame_completed=True`

#### Scenario: StepResult reflects FRAME_COMPLETE override
- **WHEN** `execute_step` triggers FRAME_COMPLETE override in step 10 (DYNAMIC_MATCH, remaining child exists)
- **THEN** the returned `StepResult` has `frame_override_triggered=True`, `child_pushed=True`, `frame_completed=False`

#### Scenario: StepResult reflects normal step without interception
- **WHEN** `execute_step` completes without triggering anti-loop or FRAME_COMPLETE override
- **THEN** the returned `StepResult` has `anti_loop_triggered=False` and `frame_override_triggered=False`; `child_pushed` and `frame_completed` reflect the FSM transition result

### Requirement: StepOrchestrator SHALL delegate FSM interception to IInterceptionHandler

`StepOrchestrator` SHALL delegate FSM transition interception logic (steps 8-10) to an `IInterceptionHandler` interface rather than implementing it inline. The handler SHALL be injected via constructor (`IInterceptionHandler`) with a default implementation of `new InterceptionHandler()`. The orchestrator SHALL only apply interception overrides when an interception condition matches; when no interception condition matches, the FSM's original `nextState` SHALL be preserved.

#### Scenario: Branch interception delegates to handler
- **WHEN** `ExecuteStepAsync` detects `nextState == TraversalState.Branch` AND `fromState` is in `BranchAllowedSources`
- **THEN** the orchestrator SHALL call `_handler.OnBranch(ctx, fromState)` and apply the returned `InterceptionResult`
- **AND** the orchestrator SHALL NOT contain inline branch/push/scroll/navigation logic

#### Scenario: No interception match preserves FSM state
- **WHEN** `ExecuteStepAsync` detects `nextState` is `ResultVerify` (no interception condition matches)
- **THEN** `nextState` SHALL remain `ResultVerify` (unchanged by any interception)
- **AND** `childPushed`, `frameCompleted`, `frameOverrideTriggered` SHALL remain false

#### Scenario: Default handler is constructed for backward compatibility
- **WHEN** `StepOrchestrator` is constructed without an explicit `IInterceptionHandler` parameter
- **THEN** it SHALL default to `new InterceptionHandler()`

### Requirement: InterceptionResult SHALL be a value type encapsulating FSM override state

The result of FSM interception SHALL be represented as `InterceptionResult`, a `record struct` with four fields: `NextState` (the possibly-overridden next FSM state), `ChildPushed` (whether a child was pushed onto the node stack), `FrameCompleted` (whether the current frame should be marked complete), and `FrameOverrideTriggered` (whether a `FrameComplete` transition was overridden to `NodeSelect`). The struct SHALL be mutable (not `readonly`) to support `ref` mutation by internal helper methods.

#### Scenario: InterceptionResult carries all override state
- **WHEN** `OnBranch` returns an `InterceptionResult`
- **THEN** the caller SHALL extract `NextState`, `ChildPushed`, `FrameCompleted`, and `FrameOverrideTriggered` from it
- **AND** no `ref bool` or `ref TraversalState` parameters SHALL appear on public interface methods

#### Scenario: Default InterceptionResult is a safe no-op
- **WHEN** `default(InterceptionResult)` is evaluated
- **THEN** `NextState` is `default(TraversalState)`, and all three `bool` fields are `false`
- **AND** the orchestrator's `intercepted` flag SHALL prevent this default from being applied to FSM state

### Requirement: InterceptionHandler SHALL own all FSM override logic

`InterceptionHandler` SHALL implement `IInterceptionHandler` and contain all FSM interception/override logic previously inline in `StepOrchestrator`: Branch interception (step 8), DynamicMatch child resolution with navigation/scroll/PressBack (step 9), and FrameComplete override for DynamicMatch nodes with remaining children (step 10). It SHALL also own the helper methods `TryHandleNavigation` (private), `TryHandleScrollAsync` (internal static -- direct contract tests retained), `FromFrame` (private static), `GetElementIds` (private static), and the instance field `_lastPushedChildNodeId`.

InterceptionHandler SHALL delegate container completion judgment to `ContainerHandler`. It SHALL NOT directly set `FrameCompleted` — instead, it SHALL call `ContainerHandler.HandleContainer()` and translate the returned `ContainerActionResult` (Back/AutoEscape/Skip → FrameCompleted=true; Abort → no FrameCompleted). InterceptionHandler SHALL retain only event detection (navigation, scroll, child count, fingerprint). ContainerHandler SHALL be the sole authority for container completion.

#### Scenario: InterceptionHandler contains all override logic
- **WHEN** `StepOrchestrator` source is inspected
- **THEN** no branch/dynamic/frame override logic SHALL remain
- **AND** no `TryHandleNavigation`, `TryHandleScrollAsync`, `FromFrame`, or `GetElementIds` methods SHALL remain
- **AND** no `_lastPushedChildNodeId` field SHALL remain

#### Scenario: InterceptionHandler can be mocked for testing
- **WHEN** a test constructs `StepOrchestrator` with a mock `IInterceptionHandler`
- **THEN** interception behavior SHALL be controllable via the mock without executing real scroll/navigation logic

#### Scenario: InterceptionHandler delegates completion judgment to ContainerHandler
- **WHEN** `OnFrameComplete` hook is invoked
- **THEN** `InterceptionHandler` SHALL call `ContainerHandler.HandleContainer()` to determine completion
- **AND** `InterceptionHandler` SHALL NOT directly set `result.FrameCompleted = true`

#### Scenario: InterceptionHandler translates ContainerActionResult to FrameCompleted
- **WHEN** `ContainerHandler.HandleContainer()` returns `ContainerActionResult` with Action = `Back`
- **THEN** `InterceptionHandler` sets `FrameCompleted = true`
- **WHEN** `ContainerHandler.HandleContainer()` returns `ContainerActionResult` with Action = `Abort`
- **THEN** `InterceptionHandler` does NOT set `FrameCompleted`

### Requirement: StepOrchestrator SHALL retain lifecycle orchestration but not interception

After decomposition, `StepOrchestrator` SHALL retain only: trace lifecycle calls (steps 2, 4, 5, 7, 14), FSM dispatch (step 3), path change detection (step 4 shared logic), visited node bookkeeping (step 12), and conditional routing to `IInterceptionHandler` (steps 8-10). `BranchAllowedSources` SHALL remain in `StepOrchestrator` as it is an orchestration guard condition, not interception logic.

StepOrchestrator SHALL also inject `ContainerHandler` and pass it to `InterceptionHandler` (or make it available via `StepContext`), enabling InterceptionHandler to delegate completion judgment.

#### Scenario: StepOrchestrator contains no override logic
- **WHEN** `StepOrchestrator.ExecuteStepAsync` is inspected
- **THEN** it SHALL contain only trace calls, FSM dispatch, visited bookkeeping, and delegation to `IInterceptionHandler`
- **AND** it SHALL NOT directly call `GetNextUnvisitedChild`, `Push`, `Pop`, `PressBackAsync`, `SwipeAsync`, or `AnalyzeCurrentPageAsync`
