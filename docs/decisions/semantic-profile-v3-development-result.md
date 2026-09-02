# Semantic Profile V3 — Development

> Status: READY_FOR_QUALIFICATION | Decision: `SEMANTIC_PROFILE_V3_READY_FOR_QUALIFICATION` | Date: 2026-08-30
> Gate: `PROJECT_LEADER_SEMANTIC_PROFILE_V3_DEVELOPMENT`
> Basis: `PROJECT_LEADER_SEMANTIC_PROFILE_V2_HELD_OUT_QUALIFICATION_RESULT`
> (UTILITY_INSUFFICIENT: CorrectRecovery 0.525, SettingsRoot 0.30 — root cause:
> correct identity lacked advantage in representation space)
> Scope: representation hardening ONLY. Safety Policy semantics (margin 0.05 /
> conflict / structural / min-evidence) UNCHANGED; embedding / retrieval frozen;
> zero policy relaxation; zero case-id special rules.

## Decision

```
PROJECT_LEADER_SEMANTIC_PROFILE_V3_DEVELOPMENT_RESULT

Decision: SEMANTIC_PROFILE_V3_READY_FOR_QUALIFICATION

NEXT_GATE: PROJECT_LEADER_SEMANTIC_PROFILE_V3_HELD_OUT_QUALIFICATION
（下一 Gate 必须创建全新 ContainerIdentity-heldout-v3，只运行 qualification）
```

## 1. Mechanism purchased（Stage A: Prototype Hardening — only stage needed）

- **Multi-prototype identity state representation**（`identity_prototypes` in V3 JSON）：
  DeveloperOptions ×3 / WifiSettings ×4 / NetworkAndInternet ×4 / SettingsRoot ×5 —
  每个 prototype 回答 "哪个普遍存在的 Identity 状态"（canonical · alternate wording ·
  connection/data/network controls · scrolled · category-overview · info-region），
  非 heldout-case 记忆（one semantic state → one reusable prototype）。
- **Identity-max aggregation**（retrieval）：per identity 取其 state prototypes 的最大
  similarity 得到 identity-level candidate（`DeterministicSemanticMatcher` 与 BGE
  runner 均实现；single-prototype 时与 V1/V2 逐字节等价，兼容性证明沿用）。
- **Anchor vocabulary 扩展**（`EVIDENCE_SUFFICIENCY_PROFILE_V2`）：按 state 词汇
  补充 identity 语义 anchors（WLAN / mobile networks / USB debugging / Wallpaper &
  style 等）——仅增加合法 identity evidence，不降低 sufficiency 要求（near-empty /
  generic-only 依然 fail-closed）。
- **Safety-first trim**：扩展面引起的 2 个 regression FR（net data-usage 页、root
  System 页）通过**原型内容修剪**消除（从 net data-network 移除 "Data usage"、从
  root 两个原型移除 "About phone"）——不以任何方式触碰 policy/margin。

Strategy 纪律：Stage A 达标 → **不进入 Stage B（Feature Representation）**（§19）。

## 2. Frozen / unchanged

Embedding BAAI/bge-small-en-v1.5（revision/dim/runtime/precision 冻结）·Retrieval
cosine·exact ✓ · Similarity metric ✓ · **Candidate Policy V2 safety semantics
（margin 0.05、conflict、structural、min-evidence 原则）✓** · ISemanticProvider /
SemanticEvidence / Runtime Fusion ✓ · Profile V2 immutable（hash
`92a06b05…` 未变）· 无新 vector backend / Ray / HF / VLM / Slow Semantic / online
memory。

## 3. Development evidence（tuning + former-heldout-v1/v2 = 全部为 development knowledge）

报告：`reports/container-identity-heldout-v1-bge-small-profile-v3.json` 与
`reports/container-identity-heldout-v2-bge-small-profile-v3.json`。

| Metric | Profile V2 (v2-corpus) | **Profile V3 (v2-corpus)** | Profile V3 (v1-corpus) |
|---|---|---|---|
| FalseRecovery / FPR | 0 / 0 | **0 / 0** | **0 / 0** |
| InsufficientEvidenceAdmitted | 0 | **0** | **0** |
| HardNegativeRejection | 1.0 | **1.0** | **1.0** |
| CorrectRecovery | 0.525 | **0.850** | 0.792 |
| AbstentionRate | 0.672 | **0.469** | 0.604 |
| Top1 | 0.703 | **0.906** | 0.896 |
| P50 / P95 (ms) | 5.39 / 12.91 | 3.99 / 5.87 | 3.74 / 6.56 |

**Combined regression evidence（64 positives）CorrectRecovery = (19+34)/64 = 0.828。**

## 4. Exit targets（§二十一 / §二十七）

| Target | Result |
|---|---|
| Safety 全部为 0（两 corpus） | ✅ FR 0 / IE 0 / HNR 1.0 / conflict 0 / structural 0 |
| Combined CorrectRecovery ≥ 0.75 | ✅ **0.828**（w/safety preserved） |
| Per identity ≥ 0.60（combined） | ✅ dev 0.938 · wifi 0.688 · net 0.875 · root 0.812 |
| SettingsRoot ≥ 0.60（starvation resolved） | ✅ **0.812**（v2 0.30 → v3 0.812：main-settings /
  category-overview / info-region prototypes 恢复了 root 的多状态覆盖） |
| Positive margin distribution 右移 | ✅ v2-on-v2 median **0.053** → v3-on-v2 median **0.104**（2×）；
  recall 提升来自 representation（margin 右移），非规则放松 |
| 未降低 Safety Policy | ✅ margin 0.05 / sufficiency 规则 / thresholds 全未变 |
| Embedding 冻结 | ✅ BGE-small 未换 |
| Runtime-facing contract 不变 | ✅ |
| Profile V2 immutable | ✅（T12） |
| Profile V3 可复现 | ✅（T13: JSON SSOT ↔ C# binding） |

## 5. Residual failures（结果达标，记录 buyer，不再本 Gate 修）

v2-corpus 6 misses：dev-P8 / wifi-P9 / wifi-P10 / root-P6（margin 界内 →
PROTOTYPE_COVERAGE/INHERENT-AMBIGUITY 级）；wifi-P1 / root-P7（rank-order →
EMBEDDING_RANK_ORDER_FAILURE，残余窄 margin 混淆）。v1-corpus 5 misses：
wifi-A2/B2（rank-order）、net-A2/B2、root-B1（margin 界内）。
无系统性 rank-order collapse → **EMBEDDING_MODEL_BUYER 未确认**（原型/表示已充分，
仅残余少量 sibling 窄 margin）。

## 6. Tests

GREEN：T1 identity-max 聚合确定性 · T2 store 拥有 prototype semantics · T3 加
prototype 不改 policy contract · T4 v1 safety recovered · T5 v2 FR=0 · T6 v2 IE=0 ·
T7 v2 HNR=1.0 · T8 combined CorrectRecovery ≥ 0.75 · T9 SettingsRoot ≥ 0.60 ·
T10 positive margin 分布右移 · T11 无 case-id 特判 · T12 V2 immutable · T13 V3
可复现 · T14 Runtime contract 不变。
保持 RED（历史 FAIL 记录，不修）：V2 qualification Q8/Q10。
全套：**95 PASS / 2 RED（Q8/Q10 = V2 qualification FAIL 记录）**。

## 7. Verification

- `dotnet build src/UniClaw.Runtime.sln` — 0 errors
- `dotnet test tests/Semantic/Semantic.Tests.csproj` — 95 PASS / 2 RED（Q8/Q10）
- `openspec validate --changes --strict --no-interactive` — run in-gate
- `scripts/check-consistency.sh` — run in-gate

## 8. Next discipline

`READY_FOR_QUALIFICATION` 取得后：**冻结 Profile V3**（feature/embedding/prototype/
policy/config 全冻结）→ 创建全新 `ContainerIdentity-heldout-v3`（不得参与任何设计）→
只运行 PASS/FAIL。禁止：看到 heldout-v3 failure → 调参 → 再跑 → 宣布 qualified。

## Deliverables

- `semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3.json`（SSOT：
  multi-state prototypes + extended anchors + frozen policy）
- `reports/container-identity-heldout-v1|-v2-bge-small-profile-v3.json`（V3 报告）
- Python runner：`--profile v1|v2|v3` + identity-max 聚合
- C#：`DeterministicSemanticMatcher` identity-level 聚合（compat-safe）·
  `SemanticPerceptionProfiles.V3` · `SemanticProfileV3DevelopmentTests.cs`（T1–T14）