## 1. DynamicChildManager Dedup Scope (D-89 — REVERTED)

- [x] 1.1 Keep `_generatedPairs` as `HashSet<(string fingerprint, string name)>` — the `(parentNodeId, childName)` change was reverted because it creates infinite nesting for non-navigable containers on the same page
- [x] 1.2 Keep `Generate()` dedup key construction as `(fingerprint.ToString(), childName)` — the `(node.NodeId, childName)` change was reverted for the same reason
- [x] 1.3 Verify `Invalidate()` preserves `_generatedPairs` (existing behavior: D-3) — confirmed unchanged
- [x] 1.4 Run existing DynamicChildManager unit tests — 8/8 pass, no regression

## 2. InterceptionHandler PressBack Logic Fix (D-90 — REVISED to parent-frame fingerprint comparison)

- [x] 2.1 In `OnDynamicMatchNodeSelect` (depth > 1, no remaining children, no navigation, no scroll): compare PARENT frame's cached fingerprint vs current page fingerprint (using `ctx.Context.NodeStack.Peek(1)` for parent frame + `ctx.ChildMgr.GetCachedFingerprint(parentFrame.NodeId)` + `ctx.SnapshotMgr.Fingerprint`)
- [x] 2.2 When `parentCachedFingerprint != null && parentCachedFingerprint == currentFingerprint` (parent on same page): Pop-only, no PressBack; set `result.FrameCompleted = false`, `result.NextState = NodeSelect`
- [x] 2.3 When `parentCachedFingerprint == null || parentCachedFingerprint != currentFingerprint` (parent on different page or unknown): execute `await ctx.Action.PressBackAsync()` then `ctx.Stack.Pop()`
- [x] 2.4 Verify `OnBranch` and `OnFrameComplete` are unaffected — confirmed: only OnDynamicMatchNodeSelect calls PressBack

## 3. Verify Engine Fix — Run Baseline Tests

- [x] 3.1 Run `dotnet test --filter "FullyQualifiedName~SimulationBaselineTests"` — both scenarios PASS: FullTraversal 18/18 elements, TargetSearch target_found
- [x] 3.2 Record actual numeric values: FullTraversal (TotalSteps=99, VisitedPages=19, ActionHistory=24, Elapsed=0.07s); TargetSearch (TotalSteps=66, VisitedPages=14, ActionHistory=14, Elapsed=0.00s)
- [x] 3.3 Run full test suite `dotnet test src/UniClaw.Core.sln` — 721/721 pass, 0 failures

## 4. Recalibrate Baseline JSON

- [x] 4.1 Update `settings-full-traversal.json`: totalSteps=99, visitedPagesCount=19, actionHistoryCount=24, elapsedSecondsMax=5.0
- [x] 4.2 Update `settings-target-search.json`: totalSteps=66, visitedPagesCount=14, actionHistoryCount=14, elapsedSecondsMax=3.0
- [x] 4.3 Run both baseline tests with updated JSON — Assert.True(report.AllPassed) for both

## 5. Documentation Update

- [x] 5.1 Update `docs/system/layers/simulation-baseline.md`: §1.1 and §1.2 baseline values updated; §1.3 comparison table updated; D-89/D-90 fix note added
- [x] 5.2 Add D-89 (reverted) and D-90 (revised) decisions to `docs/system/decisions/log.md`
