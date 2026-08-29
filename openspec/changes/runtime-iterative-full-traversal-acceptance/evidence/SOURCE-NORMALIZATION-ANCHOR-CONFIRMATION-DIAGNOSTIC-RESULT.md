# PROJECT_LEADER_SOURCE_NORMALIZATION_ANCHOR_CONFIRMATION_DIAGNOSTIC_RESULT

> Gate: post-`FRAME_LOCAL_COMPOSITION_VALIDITY_VETO_REPAIR_GATE` downstream diagnosis
> Date: 2026-08-29
> HEAD: `3986d3d`（工作树）
> Disposition: **DIAGNOSIS ONLY / PRODUCTION REPAIR NOT AUTHORIZED / Phase 2.6 STOPPED**

## 1. 结论

fresh real accepted viewport 已稳定复现 `Source normalization is unresolved`。当前 blocker **不是**此前报告推测的
“anchor 多行插入顺序反转”；该修复及回归测试已经存在。精确链路是：

```text
seq25 accepted（16 个 primary NavigationCandidate）
→ seq28 strict / boundary 全部不匹配
→ anchor merge 成功：13 anchors + 4 insertions，union 16→20
→ anchor merge 后的 union 不以 seq28 的 17-signature window 为连续 suffix
→ seq31 与 seq28 的 production signature 序列逐项完全相同
→ strict / skip-first / skip-last / skip-both 仍全部不匹配
→ anchor 17 个、顺序单调，但 potentialInsertions == 0
→ `TryAnchorBasedMerge` fail-closed
→ Normalize = Unresolved
→ completeness truthful Failed
```

因此 exact terminal failed predicate 是：

```csharp
if (potentialInsertions == 0)
    return null;
```

FDP 在其上一窗口已经形成：**成功的 anchor merge 不保证 latest accepted window 仍是 accumulated union 的 suffix**；
紧随其后的同帧稳定确认因此无法走原本可接受的 strict overlap，只能落入“zero insertion = revisit”拒绝分支。

## 2. Fresh real evidence

运行：真实 `emulator-5554` Android Settings；1 个 autonomous campaign run；CURRENT-ACTIVE 未改；使用与当前工作树
pipeline revision 一致的 validation-scoped shadow receipt。第一次 probe 因设备遗留在 Display 子页而在 Startup 边界诚实
失败；恢复到标准 `android.settings.SETTINGS` 根入口后重新执行，得到以下有效 run。

| Artifact | SHA-256 |
|---|---|
| `/tmp/p26-normalization-gate-stage.json` | `147d33ab7ac253c73afaf6a83f32be686aa25c1b2f7c1c0543fd2c35a1b72cba` |
| `/tmp/p26-normalization-gate-frames.json` | `f818e259956e368a354e01e8f1478166a61761f0e9c95f2939c7934456359ee1` |
| `/tmp/p26-normalization-gate-fusion.json` | `8fe0c58048233eb004bd18dcd8daee65c39a2fe7f0374ef39d134a85c2d2baa7` |
| `/private/tmp/p26-shadow-receipt.json` | `47818935e131b3ebed20100c608188432ed57665c52517f7a9b2b40b56e8acec` |

有效 run：21 observed frames；accepted root seq `5/8/11/14/17/19`；accepted Display child seq `25/28/31`；
终态 `Failed — Source normalization is unresolved; completeness cannot be proven.`；autonomy、四项 invariant 和
validation gates 均 PASS。

## 3. Blocker timeline

| seq | accepted reason | primary NavigationCandidate | frame-local事实 | normalizer outcome |
|---|---|---:|---|---|
| 25 | `scroll stability CONFIRMED (attempt 2)` | 16 | 12 menu_item；Brightness/Lock screen/Screen timeout/Display size 各有同 StableKey text_block 副本也被 admission 为 NavigationCandidate | first union |
| 28 | `scroll stability CONFIRMED (attempt 2)` | 17 | 13 menu_item；新增 Auto-rotate/Screen saver；Color contrast StableKey `row_035→row_036` | strict/boundary noop；anchor matched，13 anchors + 4 insertions；union=20 |
| 31 | `scroll stability CONFIRMED (attempt 2)` | 17 | production signature 序列与 seq28 完全相同 | strict/boundary noop；17 ordered anchors；`potentialInsertions=0` → rejected |

Runtime 随后记录：

```text
viewport exploration exhausted: source-seq=31; no new admitted navigation occurrence; viewport exhausted
Source normalization is unresolved; completeness cannot be proven.
```

这证明 seq31 是已经 settled 的相邻稳定确认，不是画面仍变化，也没有使用未来帧修正过去帧。

## 4. Operator-by-operator normalization trace

### seq25 → seq28

| operator/predicate | result | evidence |
|---|---|---|
| in-frame non-empty | pass | 16 / 17 signatures |
| in-frame exact duplicate | pass | StableKey 相同但 `text_block` 与 `menu_item` 的 signature type 不同，故不是 exact duplicate |
| strict suffix(union)-prefix(window) | noop | no unique overlap |
| boundary skip-first | noop | no unique overlap |
| boundary skip-last | noop | no unique overlap |
| boundary skip-both | noop | no unique overlap |
| anchor count | pass | 13 |
| anchor order | pass | strictly increasing |
| insertion requirement | pass | 4 new signatures：`row_036|text_block`、`row_036|menu_item`、`row_039|menu_item`、`row_040|menu_item` |
| anchor merge | matched | union 16→20；但 `union.tail(17) != seq28` |

### seq28 → seq31

| operator/predicate | result | evidence |
|---|---|---|
| exact adjacent-window equality | **true** | seq28 signatures == seq31 signatures，17/17 Ordinal equal |
| strict suffix(union)-prefix(window) | noop | prior anchor-merged union 不保留 seq28 为 suffix |
| boundary skip-first / last / both | noop | 三者均无 unique overlap |
| anchor count | pass | 17 |
| anchor order | pass | strictly increasing |
| insertion requirement | **failed** | `potentialInsertions == 0` |
| final | Unresolved | generic adjacent-overlap failure returned to completeness seam |

## 5. First Divergence / classification

### Terminal FDP

- **First Divergence**：seq28 anchor merge 成功后，normalizer 只保留 unique union，没有保留“latest accepted window”作为相邻连续性比较基准；`tail_eq_latest=false`。
- **Exact failed predicate**：seq31 `TryAnchorBasedMerge` 的 `potentialInsertions == 0`。
- **Owner**：Runtime / World normalization — `SourceEquivalenceNormalizer`。
- **GapKind**：`ANCHOR_MERGED_LATEST_WINDOW_CONFIRMATION_DISCONTINUITY`。
- **Classification**：frame 已 settled；不是 perception settle 问题。它是 anchor merge 与紧邻 identical confirmation 的 normalization consistency gap。

### 独立上游 gaps（不是本 terminal FDP 的充分解释）

fresh trace 同时推翻了旧报告中的一项事实：多个同 StableKey `text_block` 副本仍被 admission 为
`NavigationCandidate`，并非 `NonInteractive`。源码原因是 Pattern 5 在单个 Fact 上同时要求 text/provider/bounds，
而 production projector 把它们拆为 Text Fact 与 Geometry Fact；Pattern 5 未命中后，structured corroboration 让副本落入
preference-row admission。GapKind：`SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION`。

该 gap 会污染 source inventory，但**不是当前 terminal failure 的必要条件**：只保留 menu_item 后重放，seq28 anchor merge
仍因 top-row disappearance + `Color contrast row_035→row_036` 产生非 suffix union，seq31 仍在 zero-insertion predicate 失败。
因此不得用修 Pattern 5 冒充 normalization blocker 已完成。

另有 `Color contrast` StableKey 漂移与 child title `Display` 被保留为 navigation source 的事实，分别归属
evidence-quality / fusion-semantic role 后续清单；本 gate 不修改它们。

## 6. Translation falsifier

- seq28→31：viewport 内容、production signature 数量与顺序完全一致；仍失败。
- 同一完整 row evidence 在 translation 后要么保持 role/identity，要么一致 fail-closed；本 run 的最终失败不是 frame-local
  role 翻转，而是 prior anchor merge 使 identical adjacent confirmation 失去 strict 可达性。
- 结论：**FDP 稳定复现，证据充分；不再是 `INSUFFICIENT_EVIDENCE`。**

## 7. 最小候选修复（未实施）

候选 gate：`SOURCE_NORMALIZATION_ANCHOR_ADJACENT_CONFIRMATION_REPAIR_GATE`。

最小语义不是“接受任意 pure repeat”，而是使当前已经存在的 identical-adjacent-window 语义不依赖 prior merge tier：

1. normalizer 在维护 unique union 的同时，保留上一 accepted window 的 exact signature sequence 仅用于相邻连续性判定；
2. 当 `next` 与 **immediately previous accepted window** exact-Ordinal 相同，记录 SAME_SOURCE/no-new-source continuity，
   union 不增、不删、不重排；
3. backward/non-monotonic revisit、非相邻旧窗口、zero-anchor、duplicate-in-frame 继续 fail-closed；
4. confirmation 不产生 dispatch/grounding/GoalEvidence/authority；不改 acceptance、settle、scroll、Unknown/completeness。

RED→GREEN 必须钉死：

- `anchor extension → identical adjacent confirmation`：当前 RED Unresolved，修复后 Resolved；
- 同一形状不经过 anchor 时结果不变；
- non-adjacent/backward repeat 仍 Unresolved；
- type flip、StableKey drift、in-frame duplicate 仍 fail-closed；
- union/source count 不因 confirmation 增长；
- fresh real seq25/28/31 不再在 normalization 失败，且下一 terminal 必须诚实记录，不自动宣称 Phase 2.6 PASS。

## 8. Authority / lifecycle

当前 OpenSpec change 明确声明 production Runtime byte-identity，并要求任何 Runtime-owner FDP 以
`STOPPED_AT_RUNTIME_OR_CONTRACT_GAP` 返回 Human Gate。因此本次：

- `AuthorityDelta: NONE`
- `ArchitectureDelta: NONE`
- `RuntimeBehaviorDelta: NONE`
- production/test behavior **零修改**
- Phase 2.6 **维持 STOPPED**，不自动重入
- 下一步必须由 Human 明确批准 `SOURCE_NORMALIZATION_ANCHOR_ADJACENT_CONFIRMATION_REPAIR_GATE`
