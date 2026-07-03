## 1. Phase 2.0 — Architecture Fixes + Trace Foundation

- [x] 1.1 Delete AI-layer simplified PageAnalysis (3 fields) and PopupInfo (3 fields) from IAIStrategyAdvisor.cs
- [x] 1.2 Update IAIStrategyAdvisor 5 method signatures to reference Domain PageAnalysis (UniClaw.Core.Domain.Models.Content.PageAnalysis)
- [x] 1.3 Move NodeType enum (8 values) from StateMachine/TraversalState.cs to Domain/Models/Content/EnumsAndCoordinate.cs
- [x] 1.4 Add [JsonPropertyName] attributes to NodeType enum values + create NodeTypeExtensions (Values/FromValue/IsValid)
- [x] 1.5 Update 6 using references from UniClaw.Core.StateMachine → UniClaw.Core.Domain.Models.Content
- [x] 1.6 Move NodeData record from AI layer to Graph layer
- [x] 1.7 Implement TraceNode hierarchy: TraceNode (base record), SessionNode, StepNode, SpanNode (all sealed record classes)
- [x] 1.8 Supplement ITraceRecorder interface: add StartSessionAsync/EndSessionAsync + 4 span recording + 5 query methods
- [x] 1.9 Implement TraversalRuntimeContext: sealed class with 26 mutable fields (align Python src/trace/context.py)
- [x] 1.10 Refactor ITraversalContext interface: VisitedPages→IReadOnlySet<string>, VisitedChildren→IReadOnlyDictionary<string,IReadOnlySet<string>>, CurrentPath→IReadOnlyList<string>, add VisitedNodes→IReadOnlySet<string>
- [x] 1.11 Implement TraversalRuntimeContext readonly view isolation: .AsReadOnly() wrapper for CurrentPath, direct HashSet expose for VisitedPages/VisitedNodes/VisitedChildren, guard against cast-back
- [x] 1.12 Implement TraversalContextSnapshot: sealed record class with 8 ImmutableArray/ImmutableHashSet/ImmutableDictionary fields for AI advisor
- [x] 1.13 Implement TraversalRuntimeContext.CreateReadOnlySnapshot() method mapping internal fields to TraversalContextSnapshot
- [x] 1.14 Add engine-internal mutation methods to TraversalRuntimeContext: AppendPath/PopPath, MarkVisited/MarkNodeVisited, IncrementStepCount/IncrementRetryCount/IncrementConsecutiveErrors, ResetConsecutiveErrors
- [x] 1.15 Add TODO placeholder comments for IScrollHandler and IPageSnapshot reserved interface positions
- [x] 1.16 Implement ULID generator: 26-char Crockford Base32, 10-char timestamp + 16-char random
- [x] 1.17 Write unit tests: TraceNode hierarchy construction + serialization, ITraceRecorder method signatures, TraversalRuntimeContext field updates, ITraversalContext readonly isolation, TraversalContextSnapshot snapshot independence
- [x] 1.18 Run `dotnet test` — verify all Phase 2.0 tests pass incrementally

## 2. Phase 2.1 — Graph Foundation

- [x] 2.1 Supplement TraversalPlan: add 6 missing fields (entry_app, plan_name, plan_id, entry_config, static_nodes rename, template_registry)
- [x] 2.2 Implement EntryConfig: sealed record class (WaitMode, TraceLevel enums + 4 numeric fields with DomainValidationException)
- [x] 2.3 Implement PlanCompiler: TEMPLATE_SETS (4 values), compile() 6-step flow, validate_slots, build_entry_policy, build_root_node, build_completion_policy, build_static_nodes
- [x] 2.4 Implement DynamicMatcher: MatchCondition matching logic (MenuItemType, ExpectedAction, text pattern Exact/Contains, index range, custom dict), MatchResult
- [x] 2.5 Implement TemplateInstantiator: 7-step instantiate() flow (PlaceholderResolver integration, Operation/Precondition/ChildrenStrategy/ErrorPolicy construction, V6.9 path concatenation)
- [x] 2.6 Write unit tests: TraversalPlan construction + entry_app required, EntryConfig validation, PlanCompiler TEMPLATE_SETS + 6 compile scenarios, DynamicMatcher match/unmatch/multi-condition, TemplateInstantiator placeholder resolution + node assembly
- [x] 2.7 Run `dotnet test` — verify all Phase 2.1 tests pass incrementally

## 3. Phase 2.2 — State Machine Core

- [x] 3.1 Implement TraversalFSM: 8 TraversalState enum values (exclude DynamicMatch), transition matrix (PRECONDITION_CHECK→{EXECUTE, ERROR_HANDLING} only, no BRANCH per D-1), step() try-catch dispatch
- [x] 3.2 Implement CompletionDetector: 5-priority detect_completion() chain (TIMEOUT/MAX_DEPTH/empty/all_visited/incomplete), pure computation NO cache
- [x] 3.3 Implement FallbackDecider: decide_fallback() decision rules (timeout→BACK, complete→suggested, !can_continue→BACK, incomplete→SKIP), pure computation NO cache
- [x] 3.4 Implement ContainerActionExecutor: Hook Dispatch table (Dictionary<FallbackAction, Func>), 4 hooks (BACK/AUTO_ESCAPE/SKIP/ABORT), exception fallback to BACK
- [x] 3.5 Implement ErrorClassifier: 6 ErrorType values, priority chain pattern matching (substring, not regex)
- [x] 3.6 Implement ErrorStrategySelector: 6 ErrorType × strategy priority chains, applicability checks (RETRY/BACKTRACK/SKIP/CONTINUE/ABORT)
- [x] 3.7 Implement RecoveryExecutor: Hook Dispatch table (Dictionary<ErrorStrategy, Func>), 5 hooks (exponential backoff for RETRY min(2^retry,10)), exception fallback to ABORT
- [x] 3.8 Implement PopupDetector: regex pattern matching (4 popup types, case-insensitive)
- [x] 3.9 Implement PopupClassifier: 5 sub-methods (determine_popup_type, find_dismiss_target, determine_dismiss_strategy, determine_urgency, determine_blocking_type)
- [x] 3.10 Implement PopupType/UrgencyLevel/BlockingType enums + dismiss button priorities per type
- [x] 3.11 Implement PopupActionExecutor: Hook Dispatch table (Dictionary<PopupType, Func>), exception fallback to back
- [x] 3.12 Implement StateRestorer: preserve/restore/validate lifecycle (save current_node_id+node_stack+current_state+execution_result+timestamp, restore, validate → mark failed on failure)
- [x] 3.13 Implement PopupHandler orchestration: 6-step handle_popup() flow (detect→classify→preserve→handle→restore→validate)
- [x] 3.14 Implement GlobalFSM: 8 GlobalState enum, transition matrix (Error→Recovering→Initializing→Traversing, not Error→Traversing), callback mechanism, transition_history
- [x] 3.15 Write unit tests: TraversalFSM transition matrix (PRECONDITION_CHECK→BRANCH rejected), CompletionDetector 5 scenarios (no cache), FallbackDecider 5 reasons (no cache), ContainerActionExecutor 4 hooks + exception fallback, ErrorClassifier 6 types + priority chain, ErrorStrategySelector 6 chains + applicability, RecoveryExecutor 5 hooks + backoff, PopupHandler 5 types × dismiss + StateRestorer preserve/restore/validate, GlobalFSM transitions + callbacks
- [x] 3.16 Write TraversalRuntimeContext tests: CreateReadOnlySnapshot isolation (snapshot created → engine modifies → snapshot unaffected)
- [x] 3.17 Run `dotnet test` — verify all Phase 2.2 tests pass incrementally

## 4. Phase 2.3 — Traversal Engine Subsystems

- [x] 4.1 Implement StepContext: value object encapsulating step dependencies (context, state_machine, vision, action, child_mgr, node_registry, trace, snapshot_mgr, stack, last_known_path, etc.)
- [x] 4.2 Implement StepOrchestrator: 14-step execute_step() flow (setup → trace → FSM.step → snapshots → interception → visited_nodes → invalidation → trace end)
- [x] 4.3 Implement BRANCH interception: only from EXECUTE/RESULT_VERIFY/NODE_SELECT (NOT PreconditionCheck) → push unvisited child or force frame completion
- [x] 4.4 Implement Anti-loop mechanism: DYNAMIC_MATCH no remaining children → back + pop stack + return immediately (prevent BRANCH→NODE_SELECT infinite loop)
- [x] 4.5 Implement FRAME_COMPLETE interception override: DYNAMIC_MATCH has remaining children → override to push remaining child instead of completing frame
- [x] 4.6 Implement DynamicChildManager: get_next_unvisited_child (STATIC/DYNAMIC_MATCH), generate pipeline (9 steps + dedup), cache invalidation (_generated_pairs persists across invalidation)
- [x] 4.7 Implement TraceCoordinator: 16+ span type methods, active gate (null/inactive = no-op), Log-and-Continue pattern, Trace level gates
- [x] 4.8 Implement EntryPolicyExecutor: strategy chain (primary → fallback → BIND_CURRENT_SCREEN), 3 strategies, fast/polling wait condition
- [x] 4.9 Implement PageCacheManager: update(path, page_info) + restore(path) with TTL
- [x] 4.10 Implement PageSnapshotManager: fingerprint(page_analysis) + has_changed(before, after) pure functions
- [x] 4.11 Write unit tests: StepOrchestrator BRANCH interception sources, Anti-loop 3 scenarios (normal/dead loop/consecutive), FRAME_COMPLETE override 3 scenarios (no remaining/override/stack consistency), Anti-loop + override interaction
- [x] 4.12 Write unit tests: DynamicChildManager cache hit/invalidation/dedup persistence, TraceCoordinator 16+ methods + active no-op + Log-and-Continue, EntryPolicyExecutor 3 strategies + fallback chain + wait modes, PageCacheManager update/restore/null, PageSnapshotManager fingerprint determinism + has_changed
- [x] 4.13 Run `dotnet test` — verify all Phase 2.3 tests pass incrementally

## 5. Final Verification

- [x] 5.1 Verify AC-1: `dotnet build` 0 errors 0 warnings
- [x] 5.2 Verify AC-2: `dotnet test` all tests pass
- [x] 5.3 Verify AC-3: grep confirms AI simplified PageAnalysis/PopupInfo deleted
- [x] 5.4 Verify AC-4: grep confirms NodeType in Domain.Models.Content
- [x] 5.5 Verify AC-8: TraversalFSM PRECONDITION_CHECK→BRANCH transition rejected
- [x] 5.6 Verify AC-9: TraversalRuntimeContext 26 fields + ITraversalContext readonly + CreateReadOnlySnapshot
- [x] 5.7 Verify AC-11: CompletionDetector/FallbackDecider no caching (pure computation)
- [x] 5.8 Verify AC-12: TraversalContextSnapshot independence from engine modifications
