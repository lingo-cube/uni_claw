## 1. TDD 失败基线 (failing coverage tests)

- [ ] 1.1 Add multi-branch coverage test: hub page with two navigation buttons (`to_A`→listA, `to_B`→listB), each scrollable; assert BOTH listA and listB items visited (currently listB=0). Confirm it FAILS.
- [ ] 1.2 Add deep-navigation coverage test: root→page1→page2 chain with scrollable content at each level; assert page1 AND page2 items visited + PressBack restores each parent page.
- [ ] 1.3 Add non-scrollable multi-branch control test (hub→listA2/listB2 with static items) asserting both branches covered (isolates navigation from scroll).

## 2. 检测导航子节点 (detect navigation children at generation)

- [ ] 2.1 In `DynamicMatcher`/`TemplateInstantiator`, propagate the matched `MenuItem.ExpectedAction` / `ExpectsPageChange` onto the generated child node — mark navigation children (no new enum).
- [ ] 2.2 Add a derived flag/accessor on the generated `TraversalNode` (or its ChildrenStrategy/Meta) indicating "navigation subpage entry".
- [ ] 2.3 Unit-test: generated children for `ExpectedAction.Navigate` items are flagged; non-navigate items are not.

## 3. 推子页帧 + 子页元素归子帧 (push sub-page frame)

- [ ] 3.1 In `StepOrchestrator` (Step 8/9 area), when a navigation child is executed (tap) and the page fingerprint changes, push a DynamicMatch **sub-page frame** attributed to that navigation child (not root).
- [ ] 3.2 Make `DynamicChildManager.Generate` key the sub-page's children off the **current frame's NodeId** (the navigation child), so listA items become children of `to_A`'s frame, not root.
- [ ] 3.3 Guard false-navigation: if a flagged navigation child's execution does NOT change the page, treat it as an ordinary leaf (do not push a sub-page frame).
- [ ] 3.4 Unit-test: after executing `to_A`, listA's generated children have parent NodeId = the `to_A` sub-page frame, not root.

## 4. PressBack 还原 (restore parent page via existing PressBack + Pop)

- [ ] 4.1 Verify sub-page frame exhaustion (depth ≥ 2) triggers the existing Step 9 `PressBack + Pop` (no new termination logic); confirm the parent page is restored so remaining siblings regenerate.
- [ ] 4.2 Confirm the parent (root) does NOT complete until ALL sibling navigation children are entered (to_B regenerated and visited after to_A's sub-page pops).
- [ ] 4.3 Integration-test: full hub→listA→(back)→listB→(back)→done action sequence; assert PressBack fires between branches and `to_B` is tapped.

## 5. 去重 + all_visited 校验 (VisitedNodes dedup + all_visited correctness)

- [ ] 5.1 Verify navigation children appear in `VisitedNodes` exactly once across frame push/pop + parent regeneration.
- [ ] 5.2 Verify `all_visited` is false while a sibling navigation branch remains unentered, and true only after all siblings' sub-pages are traversed.
- [ ] 5.3 Unit-test: parent with to_A traversed but to_B not traversed is NOT all_visited; after to_B traversed it IS.

## 6. 回归 + 基线重标 (regression + baseline recalibration)

- [ ] 6.1 Run full suite; confirm the 3 new coverage tests pass and no existing test regresses (661 baseline).
- [ ] 6.2 Recalibrate hierarchy + any multi-branch baseline JSON `numericAnchor` to reflect now-complete coverage (empirical, per D-67); confirm `allPassed=true` and scroll metrics sane.
- [ ] 6.3 `dotnet build` clean (0 errors, 0 functional warnings); `openspec validate navigation-subpage-frames`.
- [ ] 6.4 Append decision-log entry **D-74** (DynamicMatch 多分支导航覆盖 —— 导航子节点推子页帧;append-only;引用 refactor 文档) + update `docs/system/layers/traversal.md` §2 多分支导航说明。
