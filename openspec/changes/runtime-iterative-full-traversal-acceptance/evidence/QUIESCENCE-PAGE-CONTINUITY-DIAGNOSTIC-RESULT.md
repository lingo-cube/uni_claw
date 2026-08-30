# PROJECT_LEADER_QUIESCENCE_PAGE_CONTINUITY_DIAGNOSTIC_RESULT

> Gate: `PROJECT_LEADER_QUIESCENCE_PAGE_CONTINUITY_DIAGNOSTIC_GATE` · **DIAGNOSIS ONLY（零代码修改）** · Phase 2.6 STOPPED
> 主证据（冻结现场，未重跑）：r5 fresh real（`/tmp/p26-pattern5-repair-r5-{stage,frames,run}.json/log`）
> terminal = `quiescence admission budget exhausted (last seq=28, attempts=2, classification=left container)`。

## 0. 一句话

seq28 被判 `left container` **是因为真实屏幕确实离开了 Display 页、回到了 Settings 根页**
（structured 实时 UI dump 显示 `search_action_bar` 根标记 + 'Search settings'/'Network & internet'/
'Apps'… 完整根列表）—— **A. REAL_CONTAINER_EXIT，运行时判定正确，fail-closed 正确**。
运行时无 false negative、无连续性谓词缺陷；真正的未知是"**为什么一记 ScrollForward（Display 页最底部）之后、无任何动作间隔，应用原地回到了根页**"——触因在设备/系统层，缺屏幕录制证据，属
**EVIDENCE_COLLECTION**（不是 runtime 修复、不是 quiescence authority 变更）。

## 1. Freeze exact failure（时间线，r5 trace）

```
… scroll stability CONFIRMED (seq=25, attempt 2)                    ← GOOD：Display child 稳定确认
Step-8/Action-8 = ScrollForward()                                   ← child 探索继续滚动
viewport exploration continue: source-seq=25 … scroll more
scroll stability pending (seq=27, attempt 1, occurrences=13, dup=False,
    drift=0.0003, reason=multiplicity mismatch)                     ← 新行出现（页码底部），pending 正确
scroll stability frame left the container (page 'Settings'; attempt 2)← BAD：seq28 解析为根页
quiescence budget exhausted (last seq=28, attempts=2, classification=left container) → terminal
```

- GOOD = seq25（最后一个 accepted Display viewport）；BAD = seq28（attempt 2，left-container）。
- 帧证据：seq27 仍为 Display 底部（'Auto-rotate screen'/'Screen saver' 新行 + 工具栏 'Display'
  mi 仍在 idx0）；**seq28：vision 空帧（0 candidates）+ structured 为完整根页**。

## 2. Human Reality Reconstruction（先于运行时分类）

| 帧 | 人看到的屏幕 |
|---|---|
| seq25 GOOD | Display 子页（工具栏 'Display' + Brightness…Color contrast/Other display controls 可见行）|
| seq27 attempt1 | **Display 子页向下滚动后的底窗**（Lock display…Auto-rotate/Screen saver；工具栏仍在顶部 idx0）|
| seq28 attempt2 | **Settings 根页**（Search settings 搜索栏 + 'Network & internet'/'Connected devices'/'Apps'/'Notifications'… 根行）—— 与 Display 子页完全不同的 UI |

**HUMAN_REALITY(seq28) = LEFT_CONTAINER**（根页真实可见：structured 实时 pull 完整、资源 id 明确
`search_action_bar`；非过渡态——行列表完整稳定、非 ghost/clip/动画帧；非 dialog——是完整根页）。
HUMAN_REALITY(seq27) = SAME_CONTAINER（Display 底窗，过渡滚动中）。

## 3. AssetRef（historical campaigns 未存截图 → 明确登记 MISSING）

| 目标 | assetType | 关联 | status |
|---|---|---|---|
| GOOD seq25 | frame (fusedCandidates/stage) | run-1 · obsSeq 25 · container SettingsSubpage(Display) · occurrence row_011 'Display' mi | **MISSING_ASSET: screenshot**（历史 campaign 未持久化 image crop）—— 以 stage fusedCandidates + structured 文本证据代替 |
| BAD seq27 | frame | run-1 · obsSeq 27 | MISSING_ASSET: screenshot |
| BAD seq28 | frame | run-1 · obsSeq 28 · occurrence=structured `search_action_bar`（ResourceLeaf 根标记）| MISSING_ASSET: screenshot；**structured 证据完整（根行 + 根标记）** |
| 证据坐标 | stage/frames JSON | `p26-pattern5-repair-r5-*`（seq25/27/28 全量 elements+structured）| 存在 |

不伪造 AssetRef；屏幕录制/截图为缺失项（`MISSING_ASSET: screenshot/recording`）。

## 4. Good/Bad Evidence Packet + Continuity Predicate Matrix

| 谓词 | GOOD seq25 (accepted Display) | BAD seq28 (left container) | Δ |
|---|---|---|---|
| title identity | 子页工具栏 'Display'（row_011 mi/tb）| 无子页标题 —— 根页 'Search settings' | CHANGED |
| title/page role | child collapsing_toolbar title | `search_action_bar`（ResourceLeaf）根标记 | CHANGED |
| foreground identity | com.android.settings | com.android.settings | UNCHANGED |
| known container identity | SettingsSubpage(Display) | resolver → 'Settings'(root) | CHANGED |
| parent relation | back control 存在 | 根页无 back | CHANGED |
| shared logical sources | Display 行（row_011..row_035）| 根行（Network & internet/Apps/Notifications…）| CHANGED |
| spatial continuity | — | — | CHANGED（行集全换）|
| scroll direction | forward |（无新动作）| UNCHANGED |
| observation freshness | fresh | fresh（attempt2 新观测 seq28）| UNCHANGED |
| stability | CONFIRMED(attempt2) | 单帧即 left 判定（未走 stability）| — |
| page signature | child | root marker | CHANGED |
| external-boundary signal | 无 | 根页 = "回到父容器"（真实退出 child）| CHANGED |

FIRST_SEMANTICALLY_RELEVANT_CHANGE：**seq27→seq28 之间真实屏幕由 Display 底窗变为 Settings 根页**（
运行时输出之前，世界先行发生变化）。

## 5. Trace Causal Chain / LAST_GOOD / FIRST_BAD / FDP

```
Scroll dispatch (Action-8)
 → post-scroll obs seq27（Display 底窗；new rows）→ pending（multiplicity mismatch —— 正确）
 → re-observe seq28（fresh）→ resolveSemanticPage=Settings（ST structured root marker）
 → page('Settings') != container('SettingsSubpage(Display)') → left container → budget exhausted → terminal
```
- LAST_GOOD = seq27（Display 底窗，attempt1 pending 合理——确有新行出现）。
- FIRST_BAD = **真实世界的返回根页事件（seq27 与 seq28 之间，无运行时动作介入）**——运行时首次
  感知该事件的时间点 = seq28 解析为 'Settings'。
- **FDP 定位**：运行时侧**没有错误的 evidence transformation 或 decision**（seq28 判定基于
  structured 实时根标记，证据充分且正确）。"budget exhausted" 是 terminal symptom（真实退出后，
  任何重试都不会回到 Display——budget 不是过小，§7.6 证明）。

## 6. A–H Classification

| 类 | 判定 |
|---|---|
| **A. REAL_CONTAINER_EXIT** | ✅（structured 实时根标记 + 完整根行 = 真实退出）|
| B. PAGE_CONTINUITY_FALSE_NEGATIVE | ❌（判定与实际一致）|
| C. PERCEPTION_IDENTITY_DRIFT | 部分成分（seq28 vision 空帧——截图未检出内容），但**未驱动判定**（判定走 structured）|
| D. QUIESCENCE_SETTLE_FAILURE | ❌（页面稳定；重试无法改变根页事实）|
| E. OBSERVATION_FRAGMENTATION | 非主因（事实在同一 observation 内完整：structured 全量）|
| F. STALE_OR_WRONG_REFERENCE | ❌（seq28 为 fresh 观测，非历史证据）|
| G. TRUE_AMBIGUITY | ❌（不是"证据不足"，是明确相反证据——根页）|
| **H. MIXED** | 主因=A；附注=C(空 vision 帧作为观察质量观察项，非判定输入) |

## 7. 既有机制检查

1. `left container` 正式 predicate：`page != null && page != container.SemanticPageName`（
   `Agent.OpenWorld.cs` L2407-2418；foreground 同应用为前提）—— **单页名不匹配即可判定**。
2. 决策 Owner：Agent quiescence settle（post-scroll 稳定性/容器连续性循环，`ConfirmScrollStabilityAsync` 族）。
3. fresh primary 输入：`current.ForegroundApplication` + `_resolveSemanticPage(current)`（当前观测）。
4. structured auxiliary 输入：**决定性输入** —— `SearchActionBarResourceLeaf(search_action_bar)`
   存在 → resolver 直接返回 RootIdentity（`SettingsStrategyBinding.ResolveSemanticPage` L163-166）。
5. historical evidence 进入 authority：**无**（仅当前观测；frozen invariant `HISTORICAL_EVIDENCE !=
   CURRENT_CONTAINER_TRUTH` 保持）。
6. 单一字段翻转：**有**—— structured 层 `search_action_bar` 出现即 page='Settings' → left。
   （这是**合法根标记**，非误报源；本 case 中它与真实根页一致。）
7. quiescence retry 重新采集 fresh evidence：**是**（attempt2 = 新观测 seq28，非重放）。

`TEMPORARY_VIEWPORT_DIFFERENCE != CONTAINER_EXIT` 保持：17 行完整根列表 + 根标记 ≠ 临时差异。
`QUIESCENCE_TIMEOUT != PROOF_OF_EXIT` 保持：退出由**主动证据**（根标记）证明，非超时。

## 8. Reality States

- seq27：B. 没看清楚（滚动中新行出现、multiplicity mismatch）→ **pending/reobserve —— 运行时行为正确**。
- seq28：**看清楚且确实离开**（A；根页稳定完整）→ left-container fail-closed —— 正确。
- 禁止路径未发生："没看清楚"从未直接变成 "left container"（seq27 是 pending，非 left；left 仅由
  确定性根页证据触发）。

## 9. Debug IR

```
ExpectedReality: child 探索中滚动到 Display 页底部后，下一页仍应显示 Display 行 → 正常 exhaustion。
ObservedReality: seq27（Display 底窗,pending）→ seq28（真实根页）→ left container。
TerminalState: Failed — viewport exploration did not prove positive exhaustion; quiescence budget exhausted (attempts=2).
TargetObservation: seq28（attempt2 观测）。
GoodComparison/BadComparison: 见 §4 矩阵（title/rows/signature 全 CHANGED；foreground UNCHANGED）。
EvidenceChain: Scroll(Action-8) → seq27(Display,new rows) → pending → seq28(ST: search_action_bar→root) → left.
LastGood: seq27。FirstBad: 真实返回根页事件（seq27↔seq28 之间，无 runtime 动作）。
GapKind: ENVIRONMENT/DEVICE-EVENT（真实退出，触因未定）—— 非 runtime 连续性缺陷。
Owner: 设备/系统层事件（触因待证）；运行时判定路径正确（Agent quiescence + SettingsStrategyBinding resolver）。
TraceRefs: /tmp/p26-pattern5-repair-r5-stage.json（runtimeTrace 全链）。
EvidenceRefs: /tmp/p26-pattern5-repair-r5-frames.json（seq25/27/28 elements+structured）。
AssetRefs: MISSING_ASSET: screenshot/recording（seq25/27/28 均无图像）；证据坐标 = stage/frames JSON。
LogRefs: /tmp/p26-pattern5-repair-r5-run.log（Action-8=ScrollForward()；lastAction 确认）。
MissingEvidence: ① seq27→seq28 期间 screen recording / screenshot；② 该时间窗 emulator logcat
  （ANR/activity recreate/系统手势事件）。
Confidence: 运行时判定正确性 = 高（structured 完整根证据）；回退触因 = 低（未捕获）。
Disposition: **EVIDENCE_COLLECTION**（缺关键截图/录制 → 无法识别回退触发机制；无 runtime 修复点；
  无 quiescence/container-identity authority 变更需求）。
```

## 10. Debug Toolchain Buyer Gaps（本 gate 手工步骤）

- time-window logs（seq27→seq28 前后）：需手工 `jq`/grep 拼装（r5 stage runtimeTrace 全链一次可得，
  但**动作-观测交错对**需手工对齐）——无 CLI 时间窗工具。
- causal subtree：手工由 trace 剪枝（GOOD→BAD 5 步链，§5）。
- AssetRef association：历史 campaign **未持久化截图/录制**（`MISSING_ASSET`），需新增 capture 来满足未来
  gates——工具链缺口：**run 事件→观测 seq→图像/录制的关联存储**。
- Evidence Packet：本次手工组装（§4 矩阵）——无结构化 evidence-packet 工具。
均作为 Runtime Debugging Toolchain buyer 记录；本 gate 未实现 CLI/TUI（gate 指令）。

## 11. 结论 / Next Human Gate / Phase 2.6

- **运行时行为正确，无 runtime 修复点**；fail-closed 正确（真实退出时 child 完整性不可证）。
- 下一候选：
  1. **EVIDENCE_COLLECTION（首选）**：为 r5 同型 child 探索配 **screen recording + logcat 时间窗**，
     识别返回根页触发（ADB keyevent 909(back)？系统 ANR/activity recreate？手势？）；
  2. 若确认系统/手势环境因素 → **ENVIRONMENT_GATE**（如 ScrollForward 手势参数/设备配置）；
  3. 若确认 Settings 子页底窗滚动行为本身会触发返回（真实 app 语义）→ 由 Leader 决定
     "子页探索到底后的下一滚动应视为 exhaustion 候选而非继续滚动"类上限策略（**需新 gate**，本 gate 不实施）。
- **Phase 2.6 维持 STOPPED**；本 gate 零代码修改，完成后停止，等待 Human。

## 12. Boundary Declaration

零代码修改；未放宽 `left container` predicate；未增加 quiescence budget/retry；未延长 sleep；
未用 title text 或 StableKey 数量单独证明 same-container；未改 completeness / Pattern-5 /
Fusion publication repair / Not set / Will never / ICON/OCR/Safety。