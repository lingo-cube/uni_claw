## 1. Core types & schema (C-11 foundation)

- [x] 1.1 Add `ElementCoverageMode` enum (`Exact`, `Subset`, `LegacyRatio`) under `Simulation/ExpectedBehavior/`
- [x] 1.2 Add `ElementMiss` sealed record class (`Id` string, `Reason` string)
- [x] 1.3 Modify `ElementCoverageExpectation` record: remove `RequiredRatio`; add `Mode` (ElementCoverageMode) and `AllowedMisses` (ImmutableArray<ElementMiss>, default empty)
- [x] 1.4 Update `ExpectedBehaviorDto.ElementCoverageExpectationDto`: add `Mode` + `AllowedMisses` parsing; keep `RequiredRatio` parsing for `legacy_ratio` transitional
- [x] 1.5 Update `ExpectedBehavior.FromJson`: map new DTO fields; when JSON has `requiredRatio` but no `mode`, set `Mode=LegacyRatio`
- [x] 1.6 Sync `ArchitectureGuardTests.cs` if it locks `ElementCoverageExpectation` shape (grep for ElementCoverage references in guard tests) — no-op: guard does not lock ElementCoverageExpectation shape

## 2. Derivation data path (truthful ground-truth universe)

- [x] 2.1 Promote `SimulatedScreen.LastPageIndex` to reusable (internal) helper
- [x] 2.2 Add `SimulatedScreen.GetScrollableUniverse()` → enumerates all registered sources' `GetPage(0..LastPageIndex)`, returns `(PageId, ElementId, Text)` set; throw `DomainValidationException` on `TotalCount==null` (infinite stream, D-8)
- [x] 2.3 Add `ExpectedBehavior.WithDerivation(StateFixture fixture, SimulatedScreen screen)` — merges fixture derivation (page_coverage / element_coverage chrome / collision_proof) + scroll universe into `ElementCoverage.Required`; auto-derive `Mode` from plan CompletionPolicy (TargetFound→Subset, else Exact) unless JSON `mode` explicitly set
- [x] 2.4 Keep existing `WithFixtureDerivation(fixture)` working (no-scroll scenarios + transitional coexistence) — refactored to shared `Derive` core; gained optional `CompletionPolicy?` for Mode auto-derive + TargetName capture

## 3. Verify rewrite (exact set-diff + subset guard)

- [x] 3.1 Rewrite `VerifyElementCoverage` to extract tapped set via **exact equality** on `element_id` (HashSet<string>, not substring Contains) — D-7
- [x] 3.2 Implement `Exact` path: `matched=Required∩tapped`, `missed=Required−tapped`, `extra=tapped−Required`; pass iff `missed⊆AllowedMisses.Ids` AND `extra=∅`; single aggregate rule `element_coverage:completeness`, Message/Actual enumerate missed/extra IDs
- [x] 3.3 Implement `Subset` path (over-traversal guard): locate target tap (`element_id` normalized-contains `CompletionPolicy.TargetName`), assert no new element tap after it; MarkAndStop (target_found, not tapped) handled — pass on completion=target_found
- [x] 3.4 Implement `LegacyRatio` transitional path: preserve old ratio-threshold behavior
- [x] 3.5 Ensure `AllPassed` continues to treat `element_coverage:completeness` as blocking and `numeric_anchor.*` as informational

## 4. Test call-site wiring

- [x] 4.1 `HierarchyBaselineTests` — switch `LoadHierarchyExpectedBehavior` to `WithDerivation(fixture, screen)`; pass plan for Mode auto-derivation
- [x] 4.2 `ScrollableBaselineTests` — switch `LoadScrollExpectedBehavior` to `WithDerivation`
- [x] 4.3 `LongListBaselineTests` — switch `LoadLongListExpectedBehavior` to `WithDerivation`
- [x] 4.4 `MultiBranchNavigationTests` — switch to `WithDerivation` where applicable — N/A: this test uses direct result asserts (no ExpectedBehavior/JSON)

## 5. JSON migration (expected → mode)

- [x] 5.1 Migrate `hierarchy/*.json` (4 files): full-traversal/multi-scroll/scroll-deep-back → `exact`; target-search → `subset`
- [x] 5.2 Migrate `scroll/*.json` (6 active files): boundary/scroll-all/scroll-back-to-top/dedup/overlapping/sparse → `exact`
- [x] 5.3 Migrate `long-list/*.json` (4 files): full-traversal scenarios → `exact`; jump-termination → `exact` (+allowedMisses)
- [x] 5.4 Migrate `settings/*.json` (2 files): full-traversal → `exact`; target-search → `subset`
- [x] 5.5 Drop `requiredRatio` from all migrated files; add `allowedMisses` only where triage confirms a legitimate miss (jump-termination Jump_8..15 — D-jump)
- [x] 5.6 (orphan cleanup) 4 unreferenced legacy fixtures (persistent-dedup, wifi-list-target-search, overlapping-adaptive, wifi-list-full-traversal) still carry `requiredRatio`; left as-is — not loaded at runtime, inert under legacy path (see D-86 deferred §8.1)

## 6. Triage red tests (the value step)

- [x] 6.1 Run full baseline suite; collect every `element_coverage:completeness` FAIL with its `missed`/`extra` lists — 6 FAILs collected
- [x] 6.2 For each missed element: classify engine-bug vs legitimate-unreachable — hierarchy misses = fixture bug (D-87); jump = legitimate (D-jump); settings/hierarchy-target-search = MarkAndStop guard + target-name data
- [x] 6.3 Fix engine bugs (separate commits, regression-guarded) — root cause was fixture data (storage self-transition, D-87) + test data (App15→App_15) + MarkAndStop guard; no engine-code bug found
- [x] 6.4 Add legitimate-unreachable items to `allowedMisses` with concrete `Reason`; record each in `docs/system/decisions/log.md` (D-jump → long-list-jump-termination.json; D-86/D-87 in log.md)
- [x] 6.5 Confirm `hierarchy-full-traversal` (the 85.7% case) converges — root-cause the storage-page self-transition per design Open Question — RESOLVED: fixture storage_to_internal/external self-transition (D-87), not storage self-transitions per se; fixed → 0 missed

## 7. Negative-test validation

- [x] 7.1 Temporarily remove one `WithScrollablePage` → assert `element_coverage:completeness` FAILs with the missing scroll IDs in `missed` (then revert) — permanent: `ExpectedBehaviorElementCoverageTests.Exact_MissingRequired_FailsEnumeratingMissed`
- [x] 7.2 Inject a phantom tap → assert `extra` flagged (then revert) — permanent: `Exact_PhantomTap_FailsEnumeratingExtra` + `Exact_SubstringDoesNotCountAsMatch_FailsWithMissed`
- [x] 7.3 Inject a post-target tap in a TargetFound scenario → assert subset guard FAILs (then revert) — permanent: `Subset_PostTargetTap_FailsOverTraversal`

## 8. Cleanup & docs sync

- [x] 8.1 Remove `LegacyRatio` transitional path + `RequiredRatio` field/DTO once all JSON migrated and suite green — **DEFERRED per D-86**: ratio *verify* path already dormant for all active scenarios (loophole closed); enum-member removal is tangled with the spec-required Mode auto-derive (uses LegacyRatio as "mode absent" placeholder); 4 legacy orphan fixtures still carry requiredRatio (unloaded). Functional goal achieved; code cleanup → follow-up.
- [x] 8.2 Update `docs/system/layers/simulation-baseline.md` — document exact/subset modes, derivation from fixture ∪ scroll universe, numeric_anchor downgrade
- [x] 8.3 Run `dotnet build` (0 errors) and `dotnet test` (all green, count ≥ prior) — 711 passing (703 prior + 8 new negative tests)
- [x] 8.4 Record decision in `docs/system/decisions/log.md` (completeness-proof baseline established; reference design doc + this change) — D-86 + D-87 recorded
