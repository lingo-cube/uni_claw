# Semantic Profile V4 — Development

> Status: READY_FOR_QUALIFICATION | Decision: `SEMANTIC_PROFILE_V4_READY_FOR_QUALIFICATION` | Date: 2026-08-30
> Gate: `PROJECT_LEADER_SEMANTIC_PROFILE_V4_DEVELOPMENT`
> Basis: `SEMANTIC_PROFILE_V3_HELD_OUT_QUALIFICATION_RESULT` (UTILITY_INSUFFICIENT —
> surface-form dependent representation; buyer = FEATURE_REPRESENTATION_HARDENING)
> Scope: upgrade surface-form dependent representation → semantic-concept-oriented
> representation. Safety semantics / prototypes / embedding / policy / Runtime
> contracts UNCHANGED.

## Decision

```
PROJECT_LEADER_SEMANTIC_PROFILE_V4_DEVELOPMENT_RESULT

Decision: SEMANTIC_PROFILE_V4_READY_FOR_QUALIFICATION

NEXT_GATE: PROJECT_LEADER_SEMANTIC_PROFILE_V4_HELD_OUT_QUALIFICATION
（必须创建全新 ContainerIdentity-heldout-v4，仅 PASS/FAIL）
```

## 1. Mechanisms purchased（按 §6 顺序；B 未购买）

- **Stage A — Terminology / Semantic Normalization**
  `SEMANTIC_TERMINOLOGY_PROFILE_V1`：surface → `[semantic-concept]` 注解
  （phrase-first · 保留原词面 · 每元素去重；query 与 prototype 同一函数）。
  概念是多 surface 的领域语义（wireless-network 覆盖 Wi‑Fi/WLAN/wireless；…），
  绝不输出 Container Identity（T3）；无单 case 词表（每个 concept ≥ 2 surfaces，T5）。
- **Stage C — Semantic Anchor Generalization**（v3 EVIDENCE 失败证明的 buyer）
  `EVIDENCE_SUFFICIENCY_PROFILE_V3`：anchor 提升为概念级
  （identity → anchorConcepts），规则更严：**≥2 个不同 anchor 概念，或 1 概念 +
  switch**；exact-spelling anchors 保留。near-empty / generic-only 依旧
  fail-closed（T7/T8）。
- Stage A 首版曾造成 data-usage 概念碰撞 FR → **billing-data 概念拆分**（
  data-usage 页不再被 mobile-network 概念抬升）→ 修复，safety 恢复 0。
- **Stage B（generic down-weighting）未购买**：A+C 已达标（§6 discipline）。

## 2. Frozen（改变仅限 FeatureRepresentation / Normalization / Anchor concepts）

V3 multi-prototype（**hash `dbd11e08…` 未变**）· identity-max aggregation ·
cosine/exact · **Policy V2（margin 0.05 / conflict / structural / min-evidence）** ·
BGE-small（384/fp32/fastembed）· ISemanticProvider / SemanticEvidence / Fusion。
Profile V4 仅新增绑定：`feature_representation_version=FEATURE_REPRESENTATION_V2` ·
`normalization_profile=SEMANTIC_TERMINOLOGY_PROFILE_V1` ·
`semantic_anchor_profile=SEMANTIC_ANCHOR_CONCEPTS_V1`。

## 3. Development evidence（tuning + former-heldout-v1/v2/v3 = development knowledge）

| Metric | V3 (v3-corpus) | **V4 (v3-corpus)** | V4 (v2-corpus) | V4 (v1-corpus) |
|---|---|---|---|---|
| FR / FPR / IE / HNR | 0/0/0/1.0 | **0/0/0/1.0** | **0/0/0/1.0** | **0/0/0/1.0** |
| CorrectRecovery | 0.500 | **0.8125** | 0.8250 | 0.8333 |
| AbstentionRate | 0.700 | 0.512 | 0.484 | 0.583 |
| Top1 | 0.700 | 0.888 | 0.891 | 0.917 |

**Combined（112 positives）：CorrectRecovery 0.821（92/112）≥ 0.80 ✅**

## 4. Exit targets（§23/§34）

| Target | Result |
|---|---|
| Safety 全 0（三 corpus） | ✅ FR/IE/HNR/conflict/structural = 0/0/1.0/0/0 |
| former-heldout-v3 CorrectRecovery ≥ 0.75 | ✅ **0.8125** |
| Combined ≥ 0.80 | ✅ **0.821** |
| Per identity ≥ 0.65 | ✅ dev 0.964 · wifi 0.714 · net 0.714 · **root 0.893** |
| SettingsRoot ≥ 0.65（或 structural buyer） | ✅ **0.893**（starvation 解决；无需 structural buyer） |
| Positive margin 右移（§24） | ✅ v3-corpus median **0.065 → 0.097**；<0.05 比例 21/48 → 8/48 |
| Fresh vocabulary / alternate wording / novel composition recovery | ✅ APN / Calling / Digital wellbeing / Safety & emergency / NFC / Nearby Share / Simulate secondary displays 等恢复（39/48）；SSID 类仍 margin 受限（残余） |
| Prototype hash 不变 | ✅ `dbd11e08…`（T16） |
| Policy V2 / margin 0.05 不变 | ✅（T17） |
| Embedding 冻结 | ✅（T18） |
| 非 dictionary memorization | ✅ 概念为多 surface 领域语义；原词面保留；无 case 特判（T21） |

## 5. Residual failures（v3-corpus 9 misses —— 不修，记录）

MARGIN_AMBIGUITY ×6（SSID 类 wifi-P2/P8；net-P1/P7/P11；root-P9）·
EVIDENCE_SUFFICIENCY ×2（net-P6 单行、root-P6 单 title——sparse-only 设计保留）·
EMBEDDING_SEPARATION ×1（wifi-P11）。均不阻断目标；无系统性 rank-order →
EMBEDDING_MODEL_BUYER 不确认。

## 6. Tests

T1–T22 全绿（normalization 语义 · anchor 概念化 · fail-closed 保留 · 三 corpus
safety 回归 · fresh-vocab 恢复 · margin 右移 · SettingsRoot 恢复 · 冻结/可复现/
契约不变）。历史 qualification FAIL 记录保持 RED：V2 Q8/Q10 · V3 Q9/Q11/Q12。
全套 **130 PASS / 5 RED（历史记录）**。

## 7. Verification

- `dotnet build src/UniClaw.Runtime.sln` — 0 errors
- `dotnet test tests/Semantic/Semantic.Tests.csproj` — 130 PASS / 5 RED（历史）
- `openspec validate --changes --strict --no-interactive` — run in-gate
- `scripts/check-consistency.sh` — run in-gate

## 8. Qualification discipline

立即冻结 Profile V4（FeatureRep V2 · SemanticTerminology V1 · AnchorConcepts V1）→
创建全新 `ContainerIdentity-heldout-v4`（不得参与任何设计）→ 只允许 PASS/FAIL。

## Deliverables

- `semantic-assets/profiles/SEMANTIC_TERMINOLOGY_PROFILE_V1.json` ·
  `SEMANTIC_CONTAINER_IDENTITY_PROFILE_V4.json`（SSOT）
- V4 报告：`reports/container-identity-heldout-v1|-v2|-v3-bge-small-profile-v4.json`
- Python runner：`--profile v4`（normalization + anchor concepts）
- `SemanticProfileV4DevelopmentTests.cs`（T1–T22）