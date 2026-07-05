## 1. Phase 2.1a — Enum Fixes (H-1 + Value Guards)

- [x] 1.1 Remove `DynamicMatch` member from `TraversalState` enum (9→8 values)
- [x] 1.2 grep confirm no code references `TraversalState.DynamicMatch`
- [x] 1.3 If references found, change to `ChildrenStrategy.DynamicMatch` and verify compilation
- [x] 1.4 Add TraversalState value count assertion test: `Assert.Equal(8, Enum.GetValues<TraversalState>().Length)`
- [x] 1.5 Add GlobalState value count assertion test: `Assert.Equal(8, Enum.GetValues<GlobalState>().Length)`
- [x] 1.6 Add NodeType value count assertion test: `Assert.Equal(8, Enum.GetValues<NodeType>().Length)`
- [x] 1.7 Add ErrorType value count assertion: `Assert.Equal(6, ...)`
- [x] 1.8 Add ErrorStrategy value count assertion: `Assert.Equal(5, ...)`
- [x] 1.9 Add PopupType value count assertion: `Assert.Equal(5, ...)`
- [x] 1.10 Add DismissStrategy value count assertion: `Assert.Equal(4, ...)`
- [x] 1.11 Add UrgencyLevel value count assertion: `Assert.Equal(4, ...)`
- [x] 1.12 Add BlockingType value count assertion: `Assert.Equal(3, ...)`
- [x] 1.13 Add FallbackAction value count assertion: `Assert.Equal(4, ...)`
- [x] 1.14 Run `dotnet build` + `dotnet test` — verify all Phase 2.1a tests pass incrementally

## 2. Phase 2.1b — Interface Attribution Fix (H-5 + M-14 Evaluation)

- [x] 2.1 Create `Graph/Models/ITraversalNode.cs` with `ITraversalNode` interface in `UniClaw.Core.Graph.Models` namespace
- [x] 2.2 Move `IStackFrame` interface to same file (or `Graph/Models/IStackFrame.cs`) in `UniClaw.Core.Graph.Models` namespace
- [x] 2.3 Remove `ITraversalNode` and `IStackFrame` from `TraversalState.cs`
- [x] 2.4 Update `TraversalNode.cs`: remove `using UniClaw.Core.StateMachine`
- [x] 2.5 Update `NodeStack.cs`: add `using UniClaw.Core.Graph.Models` (reference ITraversalNode/IStackFrame)
- [x] 2.6 Update all other files referencing ITraversalNode/IStackFrame: fix using statements
- [x] 2.7 Verify `TraversalState.cs` only contains: enum TraversalState + ITraversalContext + ITraversalStateMachine + INodeStack + IGraphTraversalEngine
- [x] 2.8 Check StateMachine layer for Graph.Models using (confirm one-way dependency StateMachine→Graph)
- [x] 2.9 Check Graph layer has NO StateMachine using (confirm no reverse dependency)
- [x] 2.10 Add dependency direction assertion test: TraversalNode.cs should not reference StateMachine namespace
- [x] 2.11 Evaluate M-14: produce GlobalState on ITraversalContext assessment document in `docs/refactor/`
- [x] 2.12 Run `dotnet build` + `dotnet test` — verify all Phase 2.1b tests pass incrementally

## 3. Phase 2.1c — Collection Isolation Fix (H-2 + Cast-back Blocking)

- [x] 3.1 Implement `ReadOnlySetWrapper` as private sealed class inside TraversalRuntimeContext.cs (wraps HashSet<string> as IReadOnlySet<string>, no inheritance from HashSet)
- [x] 3.2 Modify `GetVisitedChildrenReadOnly()`: use ReadOnlySetWrapper for nested HashSet<string> values instead of direct assignment
- [x] 3.3 Add VisitedChildren cast-back blocking test: `(HashSet<string>)visitedChildren["key"]` returns null or throws InvalidCastException
- [x] 3.4 Add VisitedChildren modification isolation test: modifications through ITraversalContext do not affect internal data
- [x] 3.5 Add safety annotation comments on VisitedPages/VisitedNodes properties: "接口级安全，cast-back 级需 Phase 3 改进"
- [x] 3.6 Confirm TraversalContextSnapshot isolation test covers snapshot independence (verify existing test)
- [x] 3.7 Run `dotnet build` + `dotnet test` — verify all Phase 2.1c tests pass incrementally

## 4. Phase 2.1d — Behavior Supplement Fixes

- [x] 4.1 H-4: Add scope legality validation in PlanCompiler._validate_slots (scope must be TEMPLATE_SETS key or "target_path"; target_path requires non-empty target)
- [x] 4.2 M-9: Add `TextMatchMode` enum (Exact/Contains) and `TextMatchMode` field to `MatchCondition` record (default Contains)
- [x] 4.3 M-9: Update DynamicMatcher.match logic: Exact mode uses string equality, Contains uses substring match
- [x] 4.4 M-4: Add `Console.WriteLine($"[TraceCoordinator Warning] {ex.GetType().Name}: {ex.Message}")` in LogAndContinue catch block
- [x] 4.5 H-10: Replace PageSnapshotManager.Fingerprint's `string.GetHashCode()` with deterministic character-based hash (`hash * 31 + char`)
- [x] 4.6 H-6: Change PreservedState record: `NodeStackDepth int` → `NodeStackFrames List<StackFrame>` (save complete stack contents)
- [x] 4.7 H-7: Update RestoreState to restore all 5 fields (CurrentFrame, NodeStack, GlobalState, LastError, ExecutionResult)
- [x] 4.8 H-7: Update ValidateRestoredState to compare restored values against preserved values (not just structural checks)
- [x] 4.9 H-8: Add top-level try-catch in PopupHandler.HandlePopup, fallback to `new PopupHandlingResult(false, "back_fallback", ...)`
- [x] 4.10 Add PlanCompiler scope validation test: invalid scope throws DomainValidationException
- [x] 4.11 Add DynamicMatcher Exact mode test: exact match succeeds, substring match fails in Exact mode
- [x] 4.12 Add DynamicMatcher Contains mode test: substring match succeeds in Contains mode
- [x] 4.13 Add TextMatchMode default Contains test: MatchCondition without TextMatchMode defaults to Contains
- [x] 4.14 Add PageSnapshotManager deterministic hash test: same input produces same hash across multiple calls
- [x] 4.15 Add StateRestorer complete save/restore/compare test: preserve full stack, restore all 5 fields, validate matches
- [x] 4.16 Add PopupHandler top-level exception fallback test: any step exception → back_fallback result
- [x] 4.17 Run `dotnet build` + `dotnet test` — verify all Phase 2.1d tests pass incrementally

## 5. Final Verification

- [x] 5.1 Verify AC-1: `dotnet build` 0 errors
- [x] 5.2 Verify AC-2: `dotnet test` all tests pass
- [x] 5.3 Verify AC-3: `Enum.GetValues<TraversalState>().Length == 8` (DynamicMatch removed)
- [x] 5.4 Verify AC-4: ITraversalNode in `UniClaw.Core.Graph.Models` namespace (grep confirm)
- [x] 5.5 Verify AC-5: TraversalNode.cs has no `using UniClaw.Core.StateMachine` (grep confirm)
- [x] 5.6 Verify AC-6: VisitedChildren nested set cast-back blocked (test assertion)
- [x] 5.7 Verify AC-7: PageSnapshotManager.Fingerprint deterministic across calls (test assertion)
- [x] 5.8 Verify AC-8: PlanCompiler invalid scope throws DomainValidationException (test assertion)
- [x] 5.9 Verify AC-9: StateRestorer preserves complete stack + restores all fields (test assertion)
- [x] 5.10 Verify AC-10: PopupHandler top-level exception fallback to back (test assertion)
- [x] 5.11 Verify AC-11: All 10 enum value count guard assertions pass (test assertion)
