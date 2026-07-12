# Design: Scroll-Enabled Baseline Test

## Context

**Background:** Current `SimulationBaselineTests.cs` contains 2 baseline scenarios (full traversal + target search) using `StatefulMockVisionService` (no scroll support). The scroll simulation enhancement was completed and archived in change `scroll-simulation-enhancement`, with full infrastructure: `ScrollableMockVisionService`, `ScrollableMockActionExecutor`, `ScrollDataStore`, and `ScrollHandler`.

**Current State:** All scroll infrastructure exists but has no integration-level baseline tests demonstrating real traversal flows with scroll behavior.

**Constraints:**
- Must reuse existing scroll infrastructure (no new scroll logic)
- Must follow baseline test patterns from `SimulationBaselineTests.cs`
- ExpectedBehavior JSON must extend `numericAnchor` without breaking existing fields
- Fixtures must be data-driven (ScrollDataStore) for maintainability

**Stakeholders:**
- Baseline test consumers need scroll behavior examples
- Scroll infrastructure needs integration validation
- `docs/system/layers/simulation-baseline.md` needs scroll scenarios documented

## Goals / Non-Goals

**Goals:**
- Create `ScrollableBaselineTests.cs` with 6 scroll scenarios covering all scroll behaviors
- Demonstrate scroll integration across TraversalEngine, ScrollHandler, and IVisionProvider
- Provide scroll-specific verification metrics (scroll count, jump recovery, adaptive step)
- Document scroll scenarios in `simulation-baseline.md` §2

**Non-Goals:**
- Adding new scroll infrastructure (reuse existing components)
- Modifying non-scroll baseline tests
- Performance benchmarking (focus on correctness)
- Edge case exhaustive testing (unit tests cover edge cases)

## Decisions

### D1: Test Organization — Independent Test Class

**Decision:** Create `ScrollableBaselineTests.cs` as a new test class, not extend `SimulationBaselineTests.cs`.

**Rationale:**
- **Separation of Concerns:** Non-scroll vs scroll scenarios are distinct capabilities
- **Fixture Isolation:** Scroll fixtures (ScrollDataStore) don't mix with StateFixture
- **Clear Documentation:** Separate class makes scroll scenarios immediately discoverable
- **No Breaking Changes:** Existing baseline tests remain untouched

**Alternatives Considered:**
- **Extend existing class:** Rejected - would mix fixture types and obscure scroll scenarios
- **Nested class:** Rejected - harder to discover and run independently

### D2: Fixture Strategy — Hybrid Approach

**Decision:** One main WiFi list fixture (7 screens, 25 elements) + 2 special fixtures for jump recovery and adaptive step.

**Rationale:**
- **Main Fixture Coverage:** 7-screen WiFi list supports 4 scenarios (full traversal, scroll-back, dedup, boundaries)
- **Special Fixtures:** Sparse and overlapping fixtures require specific element distribution
- **Reuse vs Independence:** Main fixture reused 4x, special fixtures single-use for focused validation
- **Data-Driven:** All fixtures use ScrollDataStore (not hardcoded) for easy adjustment

**Alternatives Considered:**
- **One universal fixture:** Rejected - too complex, compromises scenario clarity
- **Six independent fixtures:** Rejected - excessive duplication, harder to maintain

### D3: ExpectedBehavior Extension — numericAnchor Growth

**Decision:** Extend `numericAnchor` with 7 scroll-specific fields while preserving existing fields.

**Rationale:**
- **Backward Compatibility:** Existing `totalSteps`, `visitedPagesCount`, etc. unchanged
- **Scroll Metrics:** New fields (`scrollCount`, `jumpDetected`, etc.) provide scroll-specific verification
- **Auto-Derive Strategy:** Use "auto_derive" for numeric values to avoid brittle exact assertions
- **Schema Flexibility:** `numericAnchor` is a loose map, no breaking schema change

**New Fields:**
```json
{
  "scrollCount": 6,              // Downward scroll count
  "scrollDistance": 1.0,         // Total scroll distance (0.0-1.0)
  "scrollUpCount": 1,            // Upward scroll count
  "jumpDetected": 0,             // Jump detection count
  "jumpRecovered": 0,            // Successful jump recovery count
  "finalProgress": 1.0,          // Final traversal progress
  "adaptiveStepIncreases": 0     // Adaptive step growth count
}
```

### D4: Verification Strategy — Range-Based Assertions

**Decision:** Use range-based assertions (`>=`, `Contains`, `>0`) instead of exact numeric values for Phase B.

**Rationale:**
- **C# vs Python Differences:** DFS ordering and step counts differ between implementations
- **Future Upgrade Path:** Phase C can upgrade to exact values after C# baseline confirmed
- **Existing Pattern:** Matches `SimulationBaselineTests` Phase B strategy
- **Focus on Correctness:** Validates behavior (did scroll? did visit all?) not exact numbers

**Alternatives Considered:**
- **Exact values from day 1:** Rejected - brittle, C# runtime unknown until first run
- **Auto-derive from Python:** Rejected - Python values don't apply to C# traversal engine

### D5: Scenario Naming — Descriptive Convention

**Decision:** Use `<Fixture>_<Scenario>_<Verification>` naming pattern.

**Examples:**
- `WiFiList_ScrollThroughAllScreens_AllNetworksVisited`
- `WiFiList_ScrollBackToTop_ProgressRevertsCorrectly`
- `SparseList_JumpRecovery_AllElementsVisited`

**Rationale:**
- **Self-Documenting:** Test name tells the full scenario story
- **Groupable:** Same fixture prefix groups related tests
- **Verification Clear:** Last clause explicitly states what's being verified

## Risks / Trade-offs

### R1: TraversalResult Property Mismatch

**Risk:** Test assertions may reference properties not present on `TraversalResult` (e.g., `Completed`, `StepCount`, `VisitedElements`, `FoundTarget`).

**Mitigation:** Phase 1 includes dependency check to confirm `TraversalResult` shape. If mismatch occurs, adjust assertions or create adapter pattern.

### R2: Scroll Not Auto-Integrated into TraversalEngine

**Risk:** ScrollHandler may not be automatically wired into TraversalEngine, requiring manual scroll triggering in tests.

**Mitigation:** Design supports both automatic and manual scroll modes. If manual triggering required, tests will invoke `IVisionProvider.Scroll(progress)` directly as part of traversal loop.

### R3: ExpectedBehavior JSON Schema Constraints

**Risk:** `numericAnchor` extensions may not fit existing JSON schema or deserialization logic.

**Mitigation:** `numericAnchor` is designed as a loose map (string keys, object values) — no schema change needed. Deserialization already supports unknown keys.

### R4: Special Fixture Data Complexity

**Risk:** Sparse jump and overlapping adaptive fixtures may require careful element positioning to trigger expected behaviors (jump detection, step growth).

**Mitigation:** Fixtures designed with conservative gaps/overlap ratios. First test runs will validate; adjustments made in Phase B if behaviors don't trigger as expected.

## Migration Plan

### Phase 1: Dependency Confirmation (P0)
1. Check `TraversalResult` property definitions
2. Confirm `ScrollHandler` integration in `TraversalEngine`
3. Verify `ScrollDataStore` API for fixture creation

### Phase 2: Core Implementation (P1)
1. Create `ScrollableBaselineTests.cs` with main fixture
2. Implement Scenario 1 (full screen traversal)
3. Create first ExpectedBehavior JSON
4. Run, adjust `numericAnchor` values to actual runtime

### Phase 3: Main Fixture Scenarios (P2)
1. Implement Scenarios 2-4 (scroll-back, dedup, boundaries)
2. Create corresponding ExpectedBehavior JSONs
3. Verify all pass with range-based assertions

### Phase 4: Special Fixture Scenarios (P3)
1. Implement special fixtures (sparse, overlapping)
2. Implement Scenarios 5-6 (jump recovery, adaptive step)
3. Create corresponding ExpectedBehavior JSONs

### Phase 5: Documentation Sync (P4)
1. Update `docs/system/layers/simulation-baseline.md` with §2 scroll scenarios
2. Add scroll vs non-scroll comparison table
3. Add detailed XML comments to each test method

**Rollback Strategy:** Each phase can be independently rolled back by deleting the added files. No breaking changes to existing code.

## Open Questions

### Q1: TraversalResult Property Access ✅ RESOLVED

**Question:** Does `TraversalResult` expose `Completed`, `StepCount`, `VisitedElements`, `VisitedPages`, and `FoundTarget` properties directly?

**Answer:** `TraversalResult` has:
- ✅ `Success` (not `Completed`)
- ✅ `TotalSteps` (not `StepCount`)
- ✅ `VisitedPages` (as `ImmutableArray<string>`)
- ❌ `VisitedElements` - NOT available (use `ActionHistory` or elementCoverage verification)
- ✅ `FoundTarget` via `CompletionReason == Reasons.TargetFound`

**Resolution:** Tests use `ExpectedBehavior.Verify(result)` which handles these property mappings.

### Q2: ScrollHandler Auto-Integration Status ⚠️ PARTIAL

**Question:** Is `ScrollHandler` automatically integrated into `TraversalEngine` traversal loop?

**Answer:** ScrollHandler is integrated in `TraversalFSM.TryHandleScroll`, but:
- ⚠️ **Only works with `Static` children strategy** (not `DynamicMatch`)
- ⚠️ **Has loop bug** with Static + scroll (see Implementation Findings below)

**Resolution:** Tests must use `Static` children strategy for now. FSM fix required for full integration.

## Implementation Findings

### Issue 1: DynamicMatch Scroll Not Supported

**Location:** `TraversalFSM.HandleBranch()` line 349-350

**Problem:** For `DynamicMatch` children strategy, FSM returns `NodeSelect` immediately without checking scroll:

```csharp
if (strategy == ChildrenStrategyType.DynamicMatch)
    return TraversalState.NodeSelect;  // No scroll check
```

**Impact:** Scroll baseline tests using DynamicMatch cannot trigger scroll functionality.

**Workaround:** Use `Static` children strategy (define nodes as static children).

### Issue 2: Static + Scroll Loop Bug

**Location:** `TraversalFSM.TryHandleScroll()` line 403-414

**Problem:** Scroll triggers but creates infinite loop:

1. All static children visited → `TryHandleScroll` called
2. Scroll executes → `ResetVisitedChildren(node.NodeId)` called
3. FSM checks again → sees "unvisited" children
4. Loop repeats until `MaxSteps` (1000) exhausted

**Root Cause:** Scroll logic assumes scroll discovers **new/different elements**, but Static children have fixed IDs. After scroll, same nodes are revisited repeatedly.

**Test Evidence:** `WiFiList_ScrollThroughAllScreens_AllNetworksVisited` hits `MaxSteps` with `CompletionReason = "max_steps"`.

### Required Fix: FSM Loop Prevention

**Needed Enhancement:** `TryHandleScroll` must check:
1. **Progress advanced?** - `newProgress > currentProgress + epsilon`
2. **New elements visible?** - Deduplicated element count increased after scroll
3. **Only reset VisitedChildren if genuinely new content**

**Follow-up Change:** Create `fsm-scroll-loop-fix` change to implement this fix.

## Updated Implementation Status

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 1: Dependency Confirmation | ✅ Complete | All 4 checks done |
| Phase 2: Core Implementation | ✅ Complete | DynamicMatch rewrite, all tests pass |
| Phase 3: Main Fixture Scenarios | ✅ Complete | 4 scenarios implemented |
| Phase 4: Special Fixture Scenarios | ✅ Complete | 2 scenarios implemented |
| Phase 5: Documentation Sync | ⚠️ Partial | XML comments done, docs pending |
| Phase 6: Verification | ✅ Complete | 695/695 tests pass, 0 regressions |

### Resolution: FSM Loop Bug + Test Architecture

The original issue was twofold:
1. **FSM scroll loop bug** — Fixed in archived change `2026-07-12-fsm-scroll-loop-fix` (D1-D5 loop prevention)
2. **Test architecture** — Original test used Static children + fixture placeholders, but:
   - `FindElementAt` only searched fixture elements, not scroll data
   - Static children IDs didn't match scroll data element names
   - TargetType.Text on Click operations couldn't resolve to coordinates

**Resolution approach:**
- Switched to **DynamicMatch** strategy (matching existing `SimulationBaselineTests` pattern)
- Enhanced `ScrollableMockVisionService.FindElementAt` to search scroll data visible elements as fallback
- Fixed `HandleBranch` DynamicMatch fallback to preserve `NodeSelect` when no StepContext (regression fix)
- All 6 tests now pass with ExpectedBehavior-driven verification
