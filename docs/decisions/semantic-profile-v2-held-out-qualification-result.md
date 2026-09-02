# Semantic Profile V2 — Held-out Qualification

> Status: UTILITY_INSUFFICIENT | Decision: `SEMANTIC_PROFILE_V2_UTILITY_INSUFFICIENT` | Date: 2026-08-30
> Gate: `PROJECT_LEADER_SEMANTIC_PROFILE_V2_HELD_OUT_QUALIFICATION`
> Basis: `PROJECT_LEADER_SEMANTIC_SAFETY_HARDENING_APPLY_RESULT` (Profile V2, REGRESSION_RECOVERED)
> Scope: QUALIFICATION ONLY. Profile V2 frozen before the corpus run; zero tuning;
> zero modification of feature/embedding/prototype/retrieval/policy/margin/sufficiency.

## Decision

```
PROJECT_LEADER_SEMANTIC_PROFILE_V2_HELD_OUT_QUALIFICATION_RESULT

Decision: SEMANTIC_PROFILE_V2_UTILITY_INSUFFICIENT
```

**Safety gates fully PASS on a true held-out corpus (FR=0, IE=0, HNR=1.0, conflict=0,
structural=0)，但 utility 未达标：CorrectRecovery 0.525 < 0.70，SettingsRoot 0.30
（identity starvation）。不得进入 Runtime wiring。不得降低 safety threshold 来换取
recall。** heldout-v2 从此降级为 former-heldout-v2 + regression/adversarial evidence
source；未来资格验证必须创建全新的 ContainerIdentity-heldout-v3。

## 1. Qualification Receipt（Profile 冻结证据）

`semantic-assets/heldout/reports/profile-v2-qualification-receipt.json`
（在 corpus run 之前写入）：

| Identity | Value |
|---|---|
| ProfileId | `SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2` |
| **Profile sha256（冻结）** | `92a06b05c0d9f74b0c81b28396b7d185349f7aa985f43fc585ad93d31f43dbf7`（= manifest profileHashAtCreation = run 后仍一致，Q2/Q12 证明） |
| Embedding Model | BAAI/bge-small-en-v1.5 · revision pinned-by-fastembed · 384 · fastembed+onnxruntime · fp32 |
| PrototypeProfile | v1-canonical-signatures（未变） |
| CandidatePolicyProfile | CONTAINER_IDENTITY_POLICY_V2 · margin 0.05 |
| EvidenceSufficiencyProfile | EVIDENCE_SUFFICIENCY_PROFILE_V1 |
| Corpus | ContainerIdentity-heldout-v2 · sha256 `a3837f022dbb158619d024ad8be11350da330de4addc06e1299c9739c53ec6a1` |
| Runner / Test rev | run_held_out.py --profile v2 --corpus v2（qualification mode）· SemanticProfileV2QualificationTests |

## 2. ContainerIdentity-heldout-v2（真独立 corpus）

`semantic-assets/heldout/ContainerIdentity-heldout-v2.json` + `manifest-heldout-v2.json`：

- **64 cases** = 4 identities × 16（10 positive states + 6 negative/hard-negative）
- 每 identity 正例覆盖：Normal/title-visible ×2、title-offscren scroll ×2、scroll-bottom、
  partial ×2、scroll+switch、reworded wording、full-combo（新 wording / 新组合 / 新可见状态）
- 24 negatives：wrong page · text-overlap · generic settings page · near-empty ·
  structurally-similar wrong identity · semantically-similar sibling / conflicting prev
- Source：RealTrace 4（truth.json root-scrolled 新子集）· Manual 36 · Synthetic 24
- Difficulty：Easy 12 · Medium 28 · Hard 24
- **Isolation 证明（Q1）**：与 tuning corpora + former-heldout-v1 的 element fingerprint
  互斥。初版曾含 1 个被隔离检查捕获的实例复用（ho2-dev-N4 空元素元组与 v1 net-F3
  相同）——在 corpus run 前修复；最终 corpus 经 Q1 完全独立。
- **Profile 冻结声明**：corpus 创建于 Profile V2 freeze 之后；未参与任何设计/hardening。

## 3. 资格 Gates 结果（Profile V2 on heldout-v2, BGE pipeline）

| Gate | Criterion | Result |
|---|---|---|
| Hard Gate 1 | False Recovery = 0 | **PASS（0.0000）** |
| Hard Gate 2 | Insufficient Evidence Admission = 0 | **PASS（0/8）** |
| Hard Gate 3 | PreviousIdentity conflict violation = 0 | **PASS（0）** |
| Hard Gate 4 | Structural incompatibility admission = 0 | **PASS（0）** |
| Hard-negative rejection | ≥ 1.0（required） | **PASS（1.0000，24/24）** |
| Correct Recovery | ≥ 0.70 | **FAIL（0.5250）** |
| AbstentionRate | < 0.90 | PASS（0.6719，非 reject-all） |
| Identity starvation | 每 identity ≥ 0.50 | **FAIL（SettingsRoot 0.30）** |
| Top1 / Top3 / Top5 | 报告 | 0.7031 |
| P50 / P95 / P99 | 报告 | 5.39 / 12.91 / — ms（embedding 主导，无安全策略 regression；P95 冷启动抖动，代表值 ~6.7ms） |

## 4. Diagnostics（qualification diagnosis only — 未用于设计）

**Margin 分布**：positives median 0.053（16/40 < 0.05）· negatives median 0.023
（16/24 < 0.05）· abstained-positives median 0.030（15/19 < 0.05，margin+sufficiency
吃掉了 19 个正例）。**第 2 次词汇/组合泛化的正例显著聚集在低 margin 区**：新 wording
下的语义近邻比 v1 更拥挤 → margin 0.05 在 heldout-v2 上的 recall 代价大于
former-heldout-v1。

**Identity 分解**：DeveloperOptions 0.60 · WifiSettings 0.60 · NetworkAndInternet 0.60 ·
SettingsRoot **0.30（饿死）** · None 1.0。root 的宽泛词表（settings/search/display/
battery 等高频词）+ 新组合被 generic/低 margin 双重压制。

**Magnet 专项（§16）**：未出现"高 margin + 充分 evidence 的错误吸附"：
7 个 EMBEDDING_SEPARATION 全部是**窄 margin 的 rank-order 混淆**（net→wifi ×2、
wifi→root、wifi→dev、root→dev、net→root、dev→wifi；top1–top2 差距 0.008–0.054），
并有 4 例被 sufficiency 先行阻断（margin=None）。**Policy V2 行为正常：低 margin
ambiguity → ABSTAIN；NetworkAndInternet 未再现单极性高可信磁吸**（v2 中混淆带更宽，
是多向稠密而非单向 magnet）→ PROTOTYPE_MAGNET_BUYER_CONFIRMED 不成立。

## 5. Residual Failure Distribution（19 positive abstains）

| Class | Count | Buyer hypothesis（本 Gate 不实现） |
|---|---|---|
| MARGIN_AMBIGUITY_FAILURE | 8 | 新 wording/组合落在 margin<0.05 区 → **Prototype Hardening**（multi-state / 词面更宽的 per-identity 表示）可抬高正例 margin |
| EMBEDDING_SEPARATION_FAILURE | 7 | 稠密相似带中 rank-order 混淆 → Feature Representation（generic token 降权 / 术语归一化）+ Prototype；Embedding Evaluation 视为后续选项 |
| EVIDENCE_SUFFICIENCY_FAILURE | 4 | 新词面（WLAN / Internet / Main settings / Display & sound）缺 anchor → **anchor 词汇覆盖（identity semantic knowledge 扩展）** + Feature Representation |
| STRUCTURAL / THRESHOLD / UNKNOWN | 0 | — |

**Profile V3 development cycle 起点**：Prototype Hardening（anchor 覆盖 + 表示）为主，
Feature Representation 为辅；不得以降低 safety threshold 或 margin 换取 recall。

## 6. 生命周期（旧资产身份不变）

- `ContainerIdentity-heldout-v1`：FORMER_HELDOUT + regression/adversarial（不变）
- Profile V1：SAFETY_NOT_QUALIFIED（不变）
- Profile V2（本 Gate 前）：REGRESSION_RECOVERED / NOT YET QUALIFIED（不变）
- **`ContainerIdentity-heldout-v2`（本 Gate 后）：former-heldout-v2 + regression/adversarial
  evidence source — FAIL 后失去未来 qualification 身份**（§23：未来资格验证必须新建
  `ContainerIdentity-heldout-v3`）

## 7. Tests（Q1–Q12）

GREEN：Q1 isolation manifest valid（含 fingerprint 互斥）· Q2 profile hash frozen ·
Q3 FR=0 · Q4 IE=0 · Q5 HNR=1.0 · Q6 conflict=0 · Q7 structural=0 · Q9 abstention<0.90 ·
Q11 receipt reproducible · Q12 no profile mutation。
RED（honest FAIL record，不修绿）：Q8 CorrectRecovery ≥ 0.70（0.525）· Q10 no
identity starvation（SettingsRoot 0.30）。

## 8. Verification

- `dotnet build src/UniClaw.Runtime.sln` — 0 errors
- `dotnet test tests/Semantic/Semantic.Tests.csproj` — **81 PASS / 2 RED（Q8/Q10）**
- `openspec validate --changes --strict --no-interactive` — run in-gate
- `scripts/check-consistency.sh` — run in-gate
- Profile V2 / Runtime / contract：零修改（freeze 全程成立）

## NEXT_GATE

`PROJECT_LEADER_SEMANTIC_PROFILE_V3_DEVELOPMENT`
（Prototype + Feature Representation 强化 → Profile V3 → 全新
`ContainerIdentity-heldout-v3` 资格验证）。Runtime Integration 保持禁止，直到
held-out qualification PASS。