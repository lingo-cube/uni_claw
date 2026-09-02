# Semantic Profile V3 — Held-out Qualification

> Status: UTILITY_INSUFFICIENT | Decision: `SEMANTIC_PROFILE_V3_UTILITY_INSUFFICIENT` | Date: 2026-08-30
> Gate: `PROJECT_LEADER_SEMANTIC_PROFILE_V3_HELD_OUT_QUALIFICATION`
> Basis: `SEMANTIC_PROFILE_V3_READY_FOR_QUALIFICATION`
> Scope: QUALIFICATION ONLY. Profile V3 frozen (profile + prototype hashes pinned
> in receipt BEFORE the corpus run); zero tuning / modification; heldout-v3 is a
> fresh corpus that never participated in any design.

## Decision

```
PROJECT_LEADER_SEMANTIC_PROFILE_V3_HELD_OUT_QUALIFICATION_RESULT

Decision: SEMANTIC_PROFILE_V3_UTILITY_INSUFFICIENT
```

**Safety 全部硬闸通过（FR=0 · IE=0 · HNR=1.0 · conflict=0 · structural=0，32 个全新
negatives 无一错误恢复）——multi-prototype 未扩大 false-positive surface。但
Utility 未达标：CorrectRecovery 0.500 < 0.70，SettingsRoot 0.333 < 0.50
（再次饿死）。不得进入 Runtime wiring。不得当场修复。**

heldout-v3 是"考试"：它被设计为强调**新的 wording / 新的元素组合 / 新的 scroll
组合 / 新的 sibling ambiguity / 新的 generic combos**（§5）。答案明确：

**Profile V3 记住了 development 词表，但没有学到一般化的 Container Identity
Representation。** 正例失败集中在 development 词表之外的真实 Settings 行词汇
（Access Point Names · Calling · Digital wellbeing · Safety & emergency · NFC ·
Nearby Share · Parental controls · System update · MAC address · SSID 列表）。

## 1. Qualification receipt（freeze 于 run 前）

`semantic-assets/heldout/reports/profile-v3-qualification-receipt.json`：

| Identity | Value |
|---|---|
| ProfileId | `SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3` |
| Profile sha256 | `dbd11e08470d5d0437383bb5fe66806588af4b3aab2e9dd6ab096218decd9324`（run 前后一致） |
| Prototype content sha256（raw identity_prototypes） | `3223eca713003a3de2212dc556db665e3cd23ec8c980071d7e628c083f45469c`（run 前后一致） |
| Corpus | `ContainerIdentity-heldout-v3` · sha `b8d920b3…` |
| Policy / Sufficiency / Retrieval | CONTAINER_IDENTITY_POLICY_V2 · EVIDENCE_SUFFICIENCY_PROFILE_V2 · cosine identity-max |
| margin | 0.05（未变） |

## 2. Corpus（80 = 48 positives + 32 negatives）

4 identities × 12 positives（Normal ×2 / title-offscren scroll ×2 / scroll-bottom /
partial / sparse / alternate wording / mixed controls / novel composition ×3——大量
development 词表之外的真实行）+ 32 negatives（wrong page ×4 / generic+misleading ×4 /
near-empty ×4 / sibling ×4 / text-overlap ×4 / structural-similar ×4 /
high-semantic-similarity ×4 / prev-conflict ×4）。Source：Manual 48 / Synthetic 32
（真实 trace 数量受限，未伪造 RealTrace——如实记录）。
**Isolation（Q1/Q16）**：与 tuning + former-heldout-v1/v2 的 element fingerprint
完全互斥；无 case-id / 旧实例引用。

## 3. Qualification gates

| Gate | Criterion | Result |
|---|---|---|
| Hard Gate 1–4 | FR / IE / conflict / structural = 0 | **PASS（0 / 0 / 0 / 0）** |
| Hard-negative rejection | = 1.0 | **PASS（32/32）** |
| CorrectRecovery | ≥ 0.70 | **FAIL（0.500）** |
| AbstentionRate | < 0.90 | PASS（0.700，非 reject-all） |
| Per-identity CorrectRecovery | ≥ 0.50 | **FAIL：dev 0.583 · wifi 0.500 · net 0.583 · root 0.333** |
| No identity starvation | — | **FAIL（SettingsRoot 4/12）** |
| Top1 / positive margin median | 报告 | 0.700 / 0.065（fresh vocabulary 回到低 margin 带） |
| P50 / P95 / P99 | 报告 | 4.51 / 6.38 / — ms（无 latency regression） |

## 4. Residual failure distribution（24 positive misses —— 只诊断不修）

| Class | Count | Buyer hypothesis（Profile V4 cycle 起点） |
|---|---|---|
| MARGIN_AMBIGUITY_FAILURE | 14 | 新行词汇落入稠密相似带，正例 margin 中位 0.065（≈V2-era 密度）→ Feature Representation（generic 降权 + **术语/语义归一化**，即 V3-dev 中被延后的 §13 机制）为主 buyer |
| EVIDENCE_SUFFICIENCY_FAILURE | 6 | anchors 是 exact-spelling identity 词表：新行名（APN/Calling/Digital wellbeing/Safety & emergency/NFC…）无 anchor → 无 discriminative signal → abstain。anchor 泛化（semantic concept 级，而非逐词拼写）属 Feature Representation / 词表工程 |
| EMBEDDING_SEPARATION_FAILURE | 4 | 少量 sibling rank-order（仅 4/24）→ 非系统性 → **EMBEDDING_MODEL_BUYER 不确认** |

Negative failure：**0**（32/32 abstain，含 high-sim/数据使用页/系统页等压力样本）→
`PROTOTYPE_MAGNET_FAILURE / EVIDENCE_OVER_ADMISSION / CONFLICT / STRUCTURAL` 均
未出现：多原型未扩大 FP 面（§14/§15 通过）。

## 5. 核心结论（gate 的中心问题）

"V3 是记住了已知 wording/state，还是学会了更一般的 Identity Representation？"
→ **记住了 development 词表；未学到一般化表示。** 正面证据保留：safety 完全免疫
新表面；负面证据决定资格：fresh vocabulary 上的恢复率 0.50 / root 0.333。

## 6. 生命周期

- Profile V3（本 Gate 前 READY_FOR_QUALIFICATION；现 UTILITY_INSUFFICIENT 记录）
- **`ContainerIdentity-heldout-v3` → FORMER_HELDOUT_V3 + regression/adversarial；
  失去未来 qualification 身份；未来资格验证必须创建 `ContainerIdentity-heldout-v4`**
- 历史保持：Profile V1 NOT_QUALIFIED · Profile V2 Q8/Q10 RED · v1/v2 corpus 原样

## 7. Tests（Q1–Q16）

GREEN（109）：Q1 isolation · Q2 profile freeze · Q3 prototype assets freeze ·
Q4–Q8 safety · Q10 abstention<0.90 · Q13 receipt reproducible · Q14 profile
unchanged after run · Q15 prototype assets unchanged · Q16 no leakage。
RED（honest FAIL，不修）：**Q9（CorrectRecovery 0.50 < 0.70）· Q11（root 0.333 <
0.50）· Q12（root starvation）** + 历史 V2 Q8/Q10（保持）。
全套 **109 PASS / 5 RED**（其中 3 个为本 Gate 的 V3 FAIL 记录，2 个为历史 V2）。

## 8. Verification

- `dotnet build src/UniClaw.Runtime.sln` — 0 errors
- `dotnet test tests/Semantic/Semantic.Tests.csproj` — 109 PASS / 5 RED（Q9/Q11/Q12 V3 + Q8/Q10 V2 历史）
- `openspec validate --changes --strict --no-interactive` — run in-gate
- `scripts/check-consistency.sh` — run in-gate

## NEXT_GATE

`PROJECT_LEADER_SEMANTIC_PROFILE_V4_DEVELOPMENT`
（Feature Representation Hardening：generic token 降权 + terminology/semantic
normalization + anchor 语义泛化 → Profile V4 → 全新 `ContainerIdentity-heldout-v4`
资格验证）。**Runtime Integration 保持禁止直到 offline qualification PASS。**

## Deliverables

- 决策记录 `docs/decisions/semantic-profile-v3-held-out-qualification-result.md`（registry 243）
- `semantic-assets/heldout/{ContainerIdentity-heldout-v3.json, manifest-heldout-v3.json}`
- `reports/profile-v3-qualification-receipt.json` · `reports/container-identity-heldout-v3-bge-small-profile-v3.json`
- `tests/Semantic/.../HeldOutContainerIdentityCorpusV3.cs` · `SemanticProfileV3QualificationTests.cs`（Q1–Q16）
- Runner：`--profile v3 --corpus v3`（身份保持冻结）