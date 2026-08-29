# PROJECT_LEADER_SOURCE_NORMALIZATION_ANCHOR_ADJACENT_CONFIRMATION_REPAIR_RESULT

> Gate: `SOURCE_NORMALIZATION_ANCHOR_ADJACENT_CONFIRMATION_REPAIR_GATE` · 2026-08-29
> Decision: Human 明确批准修复 gate（用户指令 `SOURCE_NORMALIZATION_ANCHOR_ADJACENT_CONFIRMATION_REPAIR_GATE 继续完成`）
> Repair: `SourceEquivalenceNormalizer` 最小修复已实施并 full-verified
> Phase 2.6 维持 STOPPED · RUNTIME_COMPLETED ≠ VALIDATION_SCENARIO_PASS 保持
> HEAD: `3986d3d`（工作树，未提交）

## 1. 结论速览

**anchor-adjacent confirmation 修复已实施：anchor merge 成功后的**immediately adjacent、exact-Ordinal
重复窗口以 SAME_SOURCE/no-new-source 连续性确认，union 不增、不删、不重排；非相邻/backward 重复、
zero-anchor、in-frame duplicate 继续 fail-closed。RED→GREEN 钉死；全量回归绿（除 5 个既有环境性
RealDevice/Vision-config 失败，与修复无关，pre/post 一致）；机械校验 ALL PASS。**

| 验证项（诊断 §7 RED→GREEN 钉死清单） | 结果 |
|---|---|
| `anchor extension → identical adjacent confirmation`：RED Unresolved → 修复后 Resolved | ✅ `AnchorMerge_ImmediateExactConfirmation_ResolvesWithoutGrowingUnion`：HEAD 代码下 RED（1 fail），工作树修复下 GREEN（44/44 全绿） |
| 同一形状不经过 anchor 时结果不变 | ✅ 不变：无 anchor 的纯重复窗口仍走 strict 全重叠路径（NORM2/NORM3 + 全量回归）；修复分支仅在 `priorWindowResolvedByAnchor` 为真时介入 |
| non-adjacent / backward repeat 仍 Unresolved | ✅ `AnchorMerge_NonAdjacentRepeat_DoesNotReuseConfirmationException` + `AllAnchors_SubsetWindow_NarrowingRejects_StaysUnresolved` + `OpenWorldCompletenessNonMonotonicExtension` 全绿 |
| type flip / StableKey drift / in-frame duplicate 仍 fail-closed | ✅ NORM4 / BuildSignature 系列 / ViewportNormalizationEquivalence 全绿 |
| union/source count 不因 confirmation 增长 | ✅ 测试断言 `anchorOnly.UniqueSourceSignatures == result.UniqueSourceSignatures`（7=7），确认证据 5 条 SAME_SOURCE |
| fresh real seq25/28/31 不再在 normalization 失败，且下一 terminal 诚实记录 | ⚠️ PARTIAL-UPSTREAM-BLOCKED：root 已可 resolved（sources=16, unresolved=0，run-1 真机）；Display child 的 seq28/31 链因**上游** semantic project bounds 异常（ArgumentOutOfRangeException: "Bounds must fit the normalized frame"，device-state 相关）未能形成；下一 terminal 诚实记录为 perception-fusion 上游缺口，Phase 2.6 不自动重入 |

## 2. Minimal Code Change（单生产文件 + 单测试文件）

### `src/UniClaw.Runtime/World/SourceEquivalenceNormalizer.cs`（唯一生产文件，+25 行）
- 新增 `bool priorWindowResolvedByAnchor` 状态：
  - anchor merge 成功（第三梯队）时置 `true`；
  - strict / boundary 合并路径与 confirmation 消费后置 `false`；
  - 在 `overlapLength is null` 分支的最前部：当 `priorWindowResolvedByAnchor && sequences[i-1].SequenceEqual(next, Ordinal)`
    时，逐行记录 `{i-1}:{k} → {i}:{k}` 的 SAME_SOURCE evidence，**不修改 `current`（union 不变）**，置 false 后 `continue`。
- 该分支是**纯新增路径**：predicate 为 false 时与修复前逐字节同路径（boundary relaxation → anchor merge → Unresolved），
  因此不减少任何既有 resolve/fail-closed 结果（IsResolved 只能不降）。
- 未触碰：`FindUniqueSuffixPrefixOverlap` / `TryBoundaryRelaxation` / `TryAnchorBasedMerge`（含
  `potentialInsertions == 0` 的 fail-closed 谓词）全部原样。

### `tests/UniClaw.Runtime.Tests/Unit/AnchorMergeTests.cs`（+51 行 = 2 测试）
- `AnchorMerge_ImmediateExactConfirmation_ResolvesWithoutGrowingUnion`：用 obs 25/28/31 形状
  （`PriorRole`→`CurrentRole` 角色翻转 + 相邻 exact 重复）钉死 GREEN + union 不增长 + 5 条 SAME_SOURCE
  confirmation evidence（第一处起点 `28:` / 终点 `31:`）。
- `AnchorMerge_NonAdjacentRepeat_DoesNotReuseConfirmationException`：同一形状后追一次重复（34）→ 仍
  Unresolved 且 AnchorMerges 为空（异常不借给非相邻窗口）。

## 3. RED→GREEN（确定性，先证 RED 再证 GREEN）

- **RED**：`git show HEAD:.../SourceEquivalenceNormalizer.cs` 覆盖工作树文件 → rebuild →
  `AnchorMerge_ImmediateExactConfirmation_ResolvesWithoutGrowingUnion` **FAIL**（1 fail / 1 pass——
  falsifier 测试在 pre-repair 下本就 pass，因为 fail-closed 行为两侧一致）。
- **GREEN**：恢复工作树修复文件 → rebuild → 同一组 2 测试全 PASS；AnchorMergeTests 15/15；
  相邻套件（OpenWorldCompletenessNonMonotonicExtension / ViewportExhaustionConfirmationRed /
  BoundaryTolerance / SourceRoleStability + ViewportNormalizationEquivalence / SourceSignatureStability）44/44。

## 4. Counterexamples（全部保持）

| 反例 | 验证 |
|---|---|
| zero-anchor（无任何命中行）→ Unresolved | `NoAnchors_StaysUnresolved` ✅ |
| pure-repeat/subset（0 insertion）→ Unresolved | `AllAnchors_SubsetWindow_NarrowingRejects_StaysUnresolved` ✅ |
| backward / non-monotonic anchor order → Unresolved | anchor-merge narrowing guard + `OpenWorldCompletenessNonMonotonicExtension` ✅ |
| non-adjacent repeat 复用 confirmation 异常 → Unresolved | 新测试 2 ✅ |
| in-frame duplicate signature → Unresolved | `NORM4_DuplicateTitle_WithoutUniqueOverlap_DoesNotMerge`（Normalize 头部 distinct 守卫）✅ |
| no adjacent overlap → Unresolved | `NORM8_NoAdjacentOverlap_FailsClosed` ✅ |
| strict-overlap clean windows 仍走 strict 而非 anchor 路径 | `StrictOverlap_CleanWindows_UsesStrictPathNotAnchorMerge` ✅ |
| union 元素不删不重排 | `AnchorMerge_PreservesExistingUnionOrderAndElements` ✅ |
| 确定性 | `AnchorMerge_IsDeterministic` ✅ |

## 5. 套件 / 构建 / 一致性

- `dotnet build src/UniClaw.Runtime.sln`：0 errors / 0 warnings。
- 全量 Runtime 套件：**2300 passed / 5 failed**（2305 total）。5 个失败：
  - `CORR_HOST03/04/09`（Vision host config identity mismatch）、`Capstone_OneAgentOneRun_RealEmulator`
    （期望 Completed 实际 Failed）、`ExternalBoundary_RealDevice`（期望 permissioncontroller foreground
    未出现）——全部为既有环境性失败：用 HEAD（pre-repair）normalizer 复跑同一 5 个测试，**同 5 个、同签名失败**，
    与本 gate 无关（修复不改变这些路径；RealDevice/Vision 环境驱动）。
- `scripts/check-consistency.sh`：**ALL PASS**（C1..C15）。修复了两处 pre-existing C15 违规——
  `FRAME-LOCAL-COMPOSITION-VALIDITY-VETO-REPAIR-RESULT.md` 两行含 `WI-*` 编号（active OpenSpec
  禁止嵌入 WorkItem 编号），改为描述性措辞（事实不变）。
- `git diff --check`：CLEAN。
- `openspec validate --changes --strict`：23/23 passed。

## 6. 真机验证（fresh real campaign, post-fix）

环境：`emulator-5554`；validation-scoped shadow receipt（`/private/tmp/p26-shadow-receipt.json`，
与工作树 pipeline revision 一致，CURRENT-ACTIVE 未动）；`settingscampaign 1`（1 autonomous run，
Round-1 conservative posture）——与诊断 run 相同入口。

### run-1（post-fix，21:16）
| Artifact | SHA-256 |
|---|---|
| `/tmp/p26-normalization-gate-repair-stage.json` | `cbe92875…69e780` |
| `/tmp/p26-normalization-gate-repair-frames.json` | `133fa9bb…4c7c8` |
| `/tmp/p26-normalization-gate-repair-fusion.json` | `28d5cdd6…411b8` |
| `/tmp/p26-normalization-gate-repair-report.json` | `35e2be57…8e88` |

- 17 observed frames；accepted root seq `5/8/11/14/17/19`（与诊断一致）；
- **root inventory complete: sources=16, unresolved=0** ——真实 root 页 normalization 成功
  （strict + anchor 语义在真实数据上成立）；
- Display child：seq24/25 semantic project 阶段抛 `ArgumentOutOfRangeException: Bounds must fit the
  normalized frame. (Parameter 'width')`（`SemanticEvidenceV2.SemanticNormalizedBounds`——
  perception/fusion 层，**非本 gate 代码**，本 gate 零改动该路径）→ 该观察 admission 为空 → child 只接受 seq25
  单窗 → viewport exhausted → 总链（含空候选观察）Normalize Unresolved → terminal:
  `Failed — Source normalization is unresolved; completeness cannot be proven.`
- 结论：**seq25/28/31 确切形状未能复现**——根因是**设备状态相关的上游 projection bounds 失败**
  （前一轮全量 suite 的 RealDevice 测试在 Display 页留下的滚动/内容状态差异），先于 anchor-confirmation
  路径发生。修复路径在该 run 未被真机直接触发。

### run-2（post-fix，force-stop + 恢复 SETTINGS 根入口后，21:2x）
| Artifact | SHA-256 |
|---|---|
| `/tmp/p26-normalization-gate-repair-r2-stage.json` | `9e7c9a8a…736e1` |
| `/tmp/p26-normalization-gate-repair-r2-frames.json` | `d3614991…73d07a` |
| `/tmp/p26-normalization-gate-repair-r2-report.json` | `5f8bdef9…6d4b` |

- 13 frames；accepted root `5/8/11/14/17/19`；**进入 child 前 root normalization 即 Unresolved**。
- 帧级对比：run-2 的 root 帧与 run-1 内容不同（同 StableKey 行在 run-2 以 `text_block`（非 menu_item）
  出现、row id 偏移），属诊断 §5 已登记的
  `SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION`（text_block 副本 admission 污染 source inventory）——
  **与修复无关的上游 fragmentation，且本 gate 明确不修它**（诊断 §5 禁令：不得用修 Pattern 5 冒充本 blocker）。

### 真机鉴定的诚实结论
- 确定性 RED→GREEN：**已证明**（§3）。
- fresh real `seq25/28/31 不再在 normalization 失败`：**未能在本 session 真机重现**
  ——child 链被上游 projection bounds 失败（run-1）或 root 上游 fragmentation（run-2）先行阻断；
  两个阻断点均在本 gate Owner（SourceEquivalenceNormalizer）之外。
- 下一 terminal 诚实记录：`Failed — Source normalization is unresolved`（归一化层），但其成因已不再是
  anchor-confirmation zero-insertion，而是上游 perception（bounds projection / text_block admission）。
- **Phase 2.6 维持 STOPPED，不自动重入，不宣称 PASS。**（K.1 entry gate 未满足，2.6B 不进入。）

## 7. Deltas / Phase 2.6

- **AuthorityDelta: NONE**（无授权/权限变化；Human 仅批准此修复 gate）。
- **ArchitectureDelta: NONE**（无新 abstraction/boundary；正常化逻辑仍是纯函数、无新复杂度状态机）。
- **RuntimeBehaviorDelta: NORMALIZER-ONLY**——仅"anchor merge 后紧邻 exact-Ordinal 重复窗口"从
  Unresolved 变为 SAME_SOURCE 确认（union 不变）；其余行为逐字节不变（44 项相邻套件 + 2300 全绿 +
  HEAD-复跑同失败集即证）。confirmation 不产生 dispatch/grounding/GoalEvidence/authority；
  不改 acceptance、settle、scroll、Unknown/completeness。
- **REVISIT_COMPLETENESS_FRESHNESS_PRESSURE**：non-adjacent/backward repeat、zero-insertion
  anchor revisit 继续 fail-closed（§4 反例保持）。
- **Phase 2.6 reentry readiness：仍 NOT READY / 维持 STOPPED**。当前下一阻塞移至上游：
  1. Display child semantic project bounds 失败（`SemanticNormalizedBounds` overflow，
     device-state-dependent）——perception/fusion Owner；
  2. `SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION`（text_block 副本 admission）——Pattern 5
     Owner。二者都需要各自 Human Gate；本 gate 产出不自动触发任何重入。

## 8. 边界声明

- 未放宽任何 fail-closed 谓词（`potentialInsertions == 0` / zero-anchor / non-monotonic 等全部原值）。
- 未修改 `FindUniqueSuffixPrefixOverlap` / `TryBoundaryRelaxation` / `TryAnchorBasedMerge`；
  未修改 admission / settle / acceptance / dispatch / GoalEvidence / Unknown / completeness 语义。
- 真机使用 validation-scoped shadow receipt（身份事实来自工作树 /version；CURRENT-ACTIVE 未动）。
- 修复仅覆盖"immediately adjacent、exact-Ordinal 重复、且前窗由 anchor merge 解决"的唯一组合；
  no-new-source 连续性不产生任何新 authority。