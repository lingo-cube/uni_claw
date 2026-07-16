## 1. Core types & schema (C-11 foundation)

- [ ] 1.1 Add `ElementCoverageMode` enum (`Exact`, `Subset`, `LegacyRatio`) under `Simulation/ExpectedBehavior/`
- [ ] 1.2 Add `ElementMiss` sealed record class (`Id` string, `Reason` string)
- [ ] 1.3 Modify `ElementCoverageExpectation` record: remove `RequiredRatio`; add `Mode` (ElementCoverageMode) and `AllowedMisses` (ImmutableArray<ElementMiss>, default empty)
- [ ] 1.4 Update `ExpectedBehaviorDto.ElementCoverageExpectationDto`: add `Mode` + `AllowedMisses` parsing; keep `RequiredRatio` parsing for `legacy_ratio` transitional
- [ ] 1.5 Update `ExpectedBehavior.FromJson`: map new DTO fields; when JSON has `requiredRatio` but no `mode`, set `Mode=LegacyRatio`
- [ ] 1.6 Sync `ArchitectureGuardTests.cs` if it locks `ElementCoverageExpectation` shape (grep for ElementCoverage references in guard tests)

## 2. Derivation data path (truthful ground-truth universe)

- [ ] 2.1 Promote `SimulatedScreen.LastPageIndex` to reusable (internal) helper
- [ ] 2.2 Add `SimulatedScreen.GetScrollableUniverse()` → enumerates all registered sources' `GetPage(0..LastPageIndex)`, returns `(PageId, ElementId, Text)` set; throw `DomainValidationException` on `TotalCount==null` (infinite stream, D-8)
- [ ] 2.3 Add `ExpectedBehavior.WithDerivation(StateFixture fixture, SimulatedScreen screen)` — merges fixture derivation (page_coverage / element_coverage chrome / collision_proof) + scroll universe into `ElementCoverage.Required`; auto-derive `Mode` from plan CompletionPolicy (TargetFound→Subset, else Exact) unless JSON `mode` explicitly set
- [ ] 2.4 Keep existing `WithFixtureDerivation(fixture)` working (no-scroll scenarios + transitional coexistence)

## 3. Verify rewrite (exact set-diff + subset guard)

- [ ] 3.1 Rewrite `VerifyElementCoverage` to extract tapped set via **exact equality** on `element_id` (HashSet<string>, not substring Contains) — D-7
- [ ] 3.2 Implement `Exact` path: `matched=Required∩tapped`, `missed=Required−tapped`, `extra=tapped−Required`; pass iff `missed⊆AllowedMisses.Ids` AND `extra=∅`; single aggregate rule `element_coverage:completeness`, Message/Actual enumerate missed/extra IDs
- [ ] 3.3 Implement `Subset` path (over-traversal guard): locate target tap (`element_id` contains `CompletionPolicy.TargetName`), assert no new element tap after it
- [ ] 3.4 Implement `LegacyRatio` transitional path: preserve old ratio-threshold behavior
- [ ] 3.5 Ensure `AllPassed` continues to treat `element_coverage:completeness` as blocking and `numeric_anchor.*` as informational

## 4. Test call-site wiring

- [ ] 4.1 `HierarchyBaselineTests` — switch `LoadHierarchyExpectedBehavior` to `WithDerivation(fixture, screen)`; pass plan for Mode auto-derivation
- [ ] 4.2 `ScrollableBaselineTests` — switch `LoadScrollExpectedBehavior` to `WithDerivation`
- [ ] 4.3 `LongListBaselineTests` — switch `LoadLongListExpectedBehavior` to `WithDerivation`
- [ ] 4.4 `MultiBranchNavigationTests` — switch to `WithDerivation` where applicable

## 5. JSON migration (expected → mode)

- [ ] 5.1 Migrate `hierarchy/*.json` (4 files): full-traversal/multi-scroll/scroll-deep-back → `exact`; target-search → `subset`
- [ ] 5.2 Migrate `scroll/*.json` (6 files): boundary/scroll-all/scroll-back-to-top/dedup/overlapping/sparse → `exact` or `subset` per semantics
- [ ] 5.3 Migrate `long-list/*.json` (3 files): full-traversal scenarios → `exact`
- [ ] 5.4 Migrate `settings/*.json` (2 files): full-traversal → `exact`; target-search → `subset`
- [ ] 5.5 Drop `requiredRatio` from all migrated files; add `allowedMisses` only where triage confirms a legitimate miss

## 6. Triage red tests (the value step)

- [ ] 6.1 Run full baseline suite; collect every `element_coverage:completeness` FAIL with its `missed`/`extra` lists
- [ ] 6.2 For each missed element: classify engine-bug vs legitimate-unreachable
- [ ] 6.3 Fix engine bugs (separate commits, regression-guarded)
- [ ] 6.4 Add legitimate-unreachable items to `allowedMisses` with concrete `Reason`; record each in `docs/system/decisions/log.md`
- [ ] 6.5 Confirm `hierarchy-full-traversal` (the 85.7% case) converges — root-cause the storage-page self-transition per design Open Question

## 7. Negative-test validation

- [ ] 7.1 Temporarily remove one `WithScrollablePage` → assert `element_coverage:completeness` FAILs with the missing scroll IDs in `missed` (then revert)
- [ ] 7.2 Inject a phantom tap → assert `extra` flagged (then revert)
- [ ] 7.3 Inject a post-target tap in a TargetFound scenario → assert subset guard FAILs (then revert)

## 8. Cleanup & docs sync

- [ ] 8.1 Remove `LegacyRatio` transitional path + `RequiredRatio` field/DTO once all JSON migrated and suite green
- [ ] 8.2 Update `docs/system/layers/simulation-baseline.md` — document exact/subset modes, derivation from fixture ∪ scroll universe, numeric_anchor downgrade
- [ ] 8.3 Run `dotnet build` (0 errors) and `dotnet test` (all green, count ≥ prior)
- [ ] 8.4 Record decision in `docs/system/decisions/log.md` (completeness-proof baseline established; reference design doc + this change)
