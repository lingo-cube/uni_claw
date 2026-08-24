# Capstone Revisit Evidence Analysis

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Phase: **1 — Evidence Collection First** (of
> PROJECT_LEADER_RUNTIME_BEHAVIOR_DEBUGGING_AND_RECOVERY_FIX)
>
> Evidence source: real-emulator run of
> `CapstoneSingleAgentRunTests.Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete`,
> evidence dump `/tmp/capstone_evidence.txt` (runtime trace, observation
> timeline, affordance dump, action history, branch progress, revisit coverage
> ledger, terminal reason). No production code was modified before this
> analysis.

---

## Collected Evidence Inventory

| artifact | location in dump | content |
|----------|------------------|---------|
| runtime trace | `TRACE ...` | full run: exploration → 8-child inventory → 7 verified returns → 5 adaptive reverses → coverage INCOMPLETE terminal |
| observation timeline | `OBS_TEXT[seq]` | 54 observations; every frame's OCR text |
| branch coverage evidence | `PROGRESS Fixture Root` | 7 completed siblings (06,05,07,08,04,03,02) |
| revisit coverage ledger | `REVISIT_COVERAGE Fixture Root` | freshly-exposed=[06,08,04,02,07,05,03] — **Child 01 absent** |
| action history | `ACTIONS` | LaunchApp, 5×ScrollForward, 8×Tap, 5×ScrollBackward, 6×Tap (7 dispatches + 7 returns) |
| grounding result | `TRACE` (no "grounding rejected"/"fresh grounding mismatch" lines) | grounding chain never failed for any branch |
| failure terminal reason | `TRACE Failed ...` | `bounded revisit coverage INCOMPLETE: discovered=8, resolved=7, unresolved=[Child 01] (bounded budget exhausted; these branches were never given a re-grounding opportunity); zero dispatch。` |

## Required Answers

### 1. Failure 发生在哪个生命周期阶段？

**OpenWorld DFS 的 Root 容器 bounded revisit/recovery 子阶段**——具体是
`RunOpenWorldAsync` 的 dispatch 循环内、`if (revisitBudget <= 0)` 的
budget-exhausted 决策点（上一任务的 container-coverage-completion gate）。
失败发生在 discovery epoch FROZEN 之后、7 个 child 全部 verified-return 之后，
恰好在最后一次反向滚动（step=0.20, seq=48）恢复 Child 02 并将预算归零时。

### 2. 最后一个正确状态是什么？

**Child 02 的 verified parent return（seq=51）→ post-completeness consistency
PASS（seq=54）→ Root inventory 重新接受（8 children）**。此时 Agent 位于
Root dispatch 循环，pending={Child 01}，revisitBudget=0。此前每一步都是正确的：

- 8/8 child discovery（epoch `seq=[2,3,4,5,6,7]`, sources=8, unresolved=0）
- 7 个 verified parent return（06, 05, 07, 08, 04, 03, 02）——每次 return 后
  post-completeness consistency 均 PASS
- 零 grounding failure / 零 authorization rejection / 零 settle failure
  （trace 中无任何 "grounding rejected"、"fresh grounding mismatch"、
  "authorization rejected"、"did not settle" 行）
- 自适应 reverse 每一步都正确：方向向上（向列表顶部）、步进 0.40→0.20→0.10
  砍半按预期执行、每次 reverse 都揭示了新的一行（04→03→02）

### 3. 哪个 invariant 被触发？

**Container-Coverage-Completion fail-closed gate（Agent 自有）**：一个已发现的
pending branch（Child 01）在冻结预算内从未获得 re-grounding opportunity →
Runtime **不得**宣告 "verified bounded traversal completion"，必须带
unresolved-branch 证据 fail-closed。该 invariant 正确触发（这正是否决"过早完成"
的保护——若没有它，run 会以误导性的 "Verified bounded traversal completion but
fresh GoalEvidence remains unsatisfied" 结束，掩盖 coverage gap）。

### 4. Failure 类型

**E — recovery/revisit failure（预算数量不足）**。

| 类型 | 判定 | 证据 |
|------|------|------|
| A discovery failure | 否 | 8/8 发现，epoch sources=8 unresolved=0 |
| B grounding failure | 否 | 7 个 branch 的 grounding 链全部通过；无 grounding 拒绝 trace |
| C authorization failure | 否 | 无授权拒绝 trace；7/7 成功 dispatch |
| D execution failure | 否 | 7/7 verified parent return，无 settle/执行失败 |
| **E recovery/revisit failure** | **是** | reverse 方向/步进正确但冻结预算（5）比"从探索终点走回顶部"所需（6）少 1 步；预算在恢复 Child 02 时恰好耗尽，Child 01 差 1 次反向 |
| F environment failure | 否 | emulator/vision/fixture 均健康；帧证据完整且自洽（每次 reverse 帧内容与预期一致） |

---

## Phase 2 — Evidence Based Root Cause Classification

**C — Insufficient bounded policy。**

- 不是 **A (Runtime logic defect)**：dispatch/grounding/authorization/recovery
  逻辑全部按设计工作（7/7 恢复、零失败 trace、coverage gate 正确 fail-closed）。
- 不是 **B (Test fixture/environment mismatch)**：fixture 行为一致（8 行列表、
  Child 01 在顶部、OCR 在 launch 帧干净检出 "Child 01"）；reverse swipe 物理
  行为一致（每次约 1 行）。
- **是 C**：bounded policy 的**预算数量**（`discoveryObservations − 1`）小于
  coverage 完成所需的反向距离。策略形状正确（方向、步进、砍半、终止语义），
  只是冻结数量不足。
- 不是 **D (Observation/evidence missing)**：证据完整——Child 01 的 grounding
  （signature `Child 01|row`）在冻结 normalization 中，仅在 discovery 帧可见，
  reverse 帧从未到达其位置；这不是"缺观测"，是"没走到"。

## Phase 3 — Architecture Ownership Check

**Owner: Agent —— revisit budget 推导（+ coverage tracking）。**

| 可能范围 | 判定 | 说明 |
|----------|------|------|
| **Agent: exploration policy / revisit budget / coverage tracking** | **是（owner）** | budget 在 epoch FROZEN 时由 Agent 冻结（`RevisitBudget: discoveryObservations.Length - 1`）；coverage ledger/gate 也是 Agent 自有。修复只动这一处推导。 |
| Traversal: action execution | 否 | 无 lowering/验证改动 |
| Environment: observation acquisition | 否 | 观测获取正常（54 帧自洽） |
| Semantic Capability: evidence production | 否 | fixture role classifier 正常（8 sources 全部被承认） |

**禁止项遵守**：不修改 DFS ownership、不修改 GoalEvidence authority、
不修改 Semantic authority、不引入场景知识（修复是通用预算推导/终止判据，
零 Settings/child-index/list-size 假设）。
