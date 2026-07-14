## 1. TDD 失败基线 (failing coverage tests)

- [x] 1.1 Add multi-branch coverage test: hub page with two navigation buttons (`to_A`→listA, `to_B`→listB), each scrollable; assert BOTH listA and listB items visited (currently listB=0). Confirm it FAILS.
- [x] 1.2 Add deep-navigation coverage test: root→page1→page2 chain with scrollable content at each level; assert page1 AND page2 items visited + PressBack restores each parent page.
- [x] 1.3 Add non-scrollable multi-branch control test (hub→listA2/listB2 with static items) asserting both branches covered (isolates navigation from scroll).

## 2. 行为检测：指纹变化 → 导航子帧 (behavioral navigation detection)

- [x] 2.1 Remove fingerprint auto-invalidation from `DynamicChildManager.GetNextUnvisitedChild` (lines 480-489). Scroll is already handled by `TryHandleScroll`'s explicit `Invalidate`; the auto-invalidation was the root cause of sibling loss on navigation.
- [x] 2.2 In `StepOrchestrator`, after a non-scroll action executes (tap/click), capture pre-action page fingerprint, compare with post-action fingerprint. If fingerprint changed → navigation detected → push a DynamicMatch **sub-page frame** attributed to the executed child (not root). If fingerprint unchanged → normal leaf flow.
- [x] 2.3 Unit-test: tap that changes page fingerprint triggers sub-frame push; tap that does NOT change fingerprint is treated as ordinary leaf (no sub-frame).

## 3. 子页帧归属 + Generate key (sub-page frame attribution)

- [x] 3.1 In `DynamicChildManager.Generate`, the `_dynamicChildren` cache key is the current frame's NodeId. After a sub-page frame is pushed for a navigation child, `Generate` naturally keys sub-page children under that navigation child's frame. Verify this already works correctly (no code change needed — the key is `node.NodeId` from Step 3 call site).
- [x] 3.2 Unit-test: after executing `to_A`, listA's generated children have parent NodeId = the `to_A` sub-page frame, not root.

## 4. PressBack 还原 (restore parent page via existing PressBack + Pop)

- [x] 4.1 Verify sub-page frame exhaustion (depth ≥ 2) triggers the existing Step 9 `PressBack + Pop` (no new termination logic); confirm the parent page is restored so remaining siblings regenerate.
- [x] 4.2 Confirm the parent (root) does NOT complete until ALL sibling navigation children are entered (to_B regenerated and visited after to_A's sub-page pops).
- [x] 4.3 Integration-test: full hub→listA→(back)→listB→(back)→done action sequence; assert PressBack fires between branches and `to_B` is tapped.

## 5. 去重 + all_visited 校验 (VisitedNodes dedup + all_visited correctness)

- [x] 5.1 Verify navigation children appear in `VisitedNodes` exactly once across frame push/pop + parent regeneration.
- [x] 5.2 Verify `all_visited` is false while a sibling navigation branch remains unentered, and true only after all siblings' sub-pages are traversed.
- [x] 5.3 Unit-test: parent with to_A traversed but to_B not traversed is NOT all_visited; after to_B traversed it IS.

## 6. 回归 + 基线重标 (regression + baseline recalibration)

- [x] 6.1 Run full suite; confirm the 3 new coverage tests pass and no existing test regresses (665 baseline).
- [x] 6.2 Recalibrate hierarchy + any multi-branch baseline JSON `numericAnchor` to reflect now-complete coverage (empirical, per D-67); confirm `allPassed=true` and scroll metrics sane.
- [x] 6.3 `dotnet build` clean (0 errors, 0 functional warnings); `openspec validate navigation-subpage-frames`.
- [x] 6.4 Append decision-log entry **D-74** (DynamicMatch 多分支导航覆盖 —— 行为检测: tap 后指纹变化 → 推子页帧, 移除 GetNextUnvisitedChild 指纹自动作废; append-only; 引用 refactor 文档) + update `docs/system/layers/traversal.md` §2 多分支导航说明。
