# Tier B S1 Normalization Remediation — Result

DocumentType: `IMPLEMENTATION_RESULT`
Decision: `PROJECT_LEADER_UNIAGENT_EMULATOR_TIER_B_S1_NORMALIZATION_REMEDIATION_RESULT`
Change: `openspec/changes/uniagent-emulator-validation-harness/`（S1 normalization 对齐修复，Human Decision 2026-08-26）
Date: 2026-08-26
Authority: Runtime Architecture Contract I-1..I-14 不变；本结果不新增架构权威。

---

## 1. Human-readable Reality Analysis

Expected：同一逻辑节点跨 viewport 帧重复出现时，harness 应识别为 equivalent source，不产生
假阳性 unresolved，最终允许真实 8/8 验收。Observed（修复前）：探索推进至 Child 06 后 root
completeness 在 normalization 阶段 unresolved。修复后：**Real Emulator（scroll-test AVD，emulator-5554）上达到 Visited 8/8 CAPSTONE
COMPLETE，Scenario Acceptance = PASS**（51 事件，零微操）。Gap 的本质是考官（harness）两处
偏离毕业 capstone 语义，而非 Runtime 缺陷。

## 2. Previous First Divergence Point

Harness binding 的多帧 viewport accumulation → SourceEquivalenceNormalizer → duplicate
reconciliation（Owner: Validation Harness）——方向正确，但根因有两层（见 §4）。

## 3. Capstone vs Harness Semantic Diff（§3 要求的逐项对照）

| 维度 | 毕业 capstone | 修复前 harness | 偏离 |
|---|---|---|---|
| composition | **vision-only**（PhysicalEnvironment ctor 无 structuredUiSource） | 前轮误加 ADB structured 通道 | **deviation D1** |
| auxiliary evidence | 不存在（无通道） | auxiliary fallback 分类把 LinearLayout 行也标 NavigationCandidate | D1 的后果：dual-tier nav |
| occurrence identity | Text\|PerceptionType（primary） | 同 | 无 |
| viewport union | cross-frame first-occurrence 累计 | 已实现（前轮） | 无 |
| 页面上下文分类 | **child 页的 "Child NN" 标题 = NonInteractive**；truncated "Child" = NonInteractive | child 页标题也分类 NavigationCandidate；无 truncated guard | **deviation D2** |
| signature stability | 由上述分类保证 | 被 D2 破坏（popup 子页 dialog 标题+按钮同帧双 nav 签名） | D2 的后果 |

## 4. Exact Root Cause（两层，均证据定位）

**D1（结构通道偏离）**：v3 轮我为"修 normalization"加入了 ADB structured 通道——对照毕业
管线后发现 capstone ctor **本就 vision-only**；我加通道反而引入 auxiliary fallback 的
dual-tier NavigationCandidate，使 normalizer 的 overlap 消歧正确地 fail-closed。
证据：CapstoneSingleAgentRunTests.cs:203-207（ctor 无 structured 参数）；
InteractionAffordanceAnalyzer.Fallback（LinearLayout 行 → NavigationCandidate）。

**D2（页面上下文分类缺失）**：evidence dump（seq 26-29）实证 popup 子页每帧含
`NavigationCandidate(Child 06), NavigationCandidate(Child 06)` —— dialog 标题与行标题同帧
双 nav 签名 → 单帧 duplicate → normalizer 按契约拒绝（这正是我自己的 targeted 测试断言
不可弱化的行为）。毕业 capstone 分类器（:104-160）在 child 页把 "Child NN" 归
NonInteractive 并带 truncated-guard；我的分类器缺这两条上下文规则。

**首次错误步骤**：D1 在 normalizer 输入层（帧序列含 dual-tier 签名）；D2 在分类层
（同帧重复签名）。二者叠加 → root completeness unresolved。

## 5. Minimal Harness Change（仅两处，均在 harness）

1. **移除 D1**：TierBProgram 组合回到 vision-only（与毕业 ctor 逐字一致），注释记录该
   偏离-回归决策。
2. **补齐 D2**：ContextAwareRoleClassifier 增加 (a) child 页上下文（单一 Child 标题页上
   "Child NN" → NonInteractive）；(b) truncated "Child" guard（非 `^Child \d{2}$` →
   NonInteractive）——逐字镜像毕业语义，无 fixture 特判、无 text/bounds/index 去重。

## 6. Targeted Evidence（§6 A–D，5/5 绿）

`ViewportNormalizationEquivalenceTests`（复用毕业 SourceProvenanceContractTests 的
admitted-frame 配方）：
- **A** 同源跨帧：[01,02,03]→[02,03,04] overlap 等价 resolved + SAME_SOURCE 证据；
- **B** 相近文本（Child 01 / Child 011）不合并：2 occurrences 签名互异，union=2；
- **C** OCR 变体（Child 0AF）= 新 distinct source，3 签名，无误合并；
- **D** 完整滚动序列（4 帧覆盖 8 行）resolved，UniqueSourceSignatures=8；
- **E** 单帧重复保持 fail-closed（禁止字符串去重的负向断言）。

## 7. Tier A Result

ValidationHarness 56/56（含新 targeted 5）全绿；Tier A fixture world 语义无回归。

## 8. Tier B Real Emulator S1 Result

**PASS @ 真实 8/8**（`docs/work/active/tierb-s1-8of8-PASS.json` + runlog + 截图）：
- Runtime terminal：`Completed`，GoalEvidence reason = "Fixture external state shows Visited
  **8/8 CAPSTONE COMPLETE** (full required coverage)"；
- Scenario Acceptance：observedCoverage **8/8**，scenarioPass **true**（harness 独立读
  fixture 外部态确认 "Visited 8/8  CAPSTONE COMPLETE"）；
- 51 events（滚动探索 union + 8 子页含 popup 子页处置 + GoalEvidenceProduced→RunCompleted）；
- 恰 1 次 start；零 Emulator 微操；Runtime 生产源零修改。

## 9. S1/S2/S3 Final Matrix（修复后在 Real Emulator 上复验；Physical Device 层为 Tier C，WAIVED_BY_HUMAN，未在本矩阵内）

| 场景 | 结果 | 证据 |
|---|---|---|
| S1 | **PASS @ 8/8** | tierb-s1-8of8-PASS.json |
| S2 | **PASS_BOUNDED_FAIL_CLOSED**（异常真实注入 force-stop；Runtime 自主 Failed，理由明确 "viewport exploration did not prove positive exhaustion…fail closed"；零介入零 redispatch；anomalous run 的 coverage 如实 unavailable） | tierb-s2-postS1remediation.json |
| S3 | **PASS**（run-1/run-2 distinct，双 Completed，恰 2 start，adaptation fact `evh3-51-events` 仅入 Run 2 strategyId） | tierb-s3-postS1remediation.json |

## 10. Full Regression

harness 56/56 · 确定性 **2109/2109** + Semantic 32/32 · 架构守护 61/61 · consistency ALL
PASS · strict PASS · `git diff --check` PASS · **Runtime 生产源零修改**（工作树 Runtime diff
为会话前既有 Phase-2 在途状态，未触碰）。

## 11. Updated Tier B Decision

**TIER_B_PASS** —— S1 PASS @ 真实 8/8 + S2 PASS_BOUNDED_FAIL_CLOSED + S3 PASS + 全量回归绿。
成功条件 §7 十项逐条满足（1 抽象 Strategy 准入；2 零微操；3 多 viewport 累计正确；
4 重复 occurrence 正确等价；5 distinct 不合并；6 无 harness 假阳性 unresolved；7 fixture
外部态 8/8；8 terminal 有真实 GoalEvidence；9 Scenario Acceptance PASS；10 Runtime 零修改）。

## 12. AuthorityDelta

`NONE`。零 Runtime/API/wire/SourceIdentity/normalization-contract 改动。

## 13. ArchitectureDelta

`NONE_RUNTIME / VALIDATION_HARNESS_ONLY`（§5 两处 + targeted 测试文件）。

## 14. Tier C Recommendation

**READY_PENDING_HUMAN_GATE** —— Tier B 全矩阵通过后，Tier C（物理设备）只剩 Human 授权
（同一生产管线，换物理 serial）。

## 15. Remaining Human Gates

1. Tier C 执行授权（当前 HOLD，本结果不改变）。
2. Phase 2.5 lifecycle 结论 / graduation / archive（Human-owned）。
3. Phase 3 Memory：REMAIN_PAUSED（未动）。
