# Semantic Safety Hardening — Apply (Profile V2)

> Status: REGRESSION_SAFETY_RECOVERED | Decision: `SEMANTIC_SAFETY_REGRESSION_RECOVERED` | Date: 2026-08-30
> Gate: `PROJECT_LEADER_SEMANTIC_SAFETY_HARDENING_APPLY`
> Basis: `PROJECT_LEADER_SEMANTIC_PIPELINE_RESPONSIBILITY_SEPARATION_RESULT`
> (`SEMANTIC_PIPELINE_RESPONSIBILITIES_SEPARATED`; Profile V1 was SAFETY_NOT_QUALIFIED)
> Scope: first-round MINIMAL safety hardening on the separated pipeline. NO
> Runtime / Agent / SemanticEvidence / ISemanticProvider / fusion change; NO
> embedding / retrieval / prototype change; NO new model / backend / Ray /
> HuggingFace / VLM / Slow Semantic; NO case-id special cases.

## Decision

```
PROJECT_LEADER_SEMANTIC_SAFETY_HARDENING_APPLY_RESULT

Decision: SEMANTIC_SAFETY_REGRESSION_RECOVERED

NEXT_GATE: PROJECT_LEADER_SEMANTIC_PROFILE_V2_HELD_OUT_QUALIFICATION
（下一 Gate 必须创建全新 ContainerIdentity-heldout-v2，
  Profile V2 对 heldout-v2 只运行资格验证，严禁看到 failure 再调参）
```

**这不是 production qualification。** Profile V2 消除了 former-heldout-v1 上的已知
safety regression（FR 0.4167 → 0.0000），且没有发生 unacceptable recall collapse
（CorrectRecovery 0.7917，Abstention 0.6042 — 非 reject-all）；Runtime-facing
contract 零变更。

## 1. Safety Objective（冻结执行）

| 优先级 | Objective | Profile V1 | Profile V2 |
|---|---|---|---|
| 1 | False Recovery → 0 | 0.4167 | **0.0000** |
| 2 | Insufficient Evidence Admission → 0 | 3/7 | **0/7** |
| 3 | Hard Negative Rejection | 0.5833 | **1.0000** |
| 4 | Correct Recovery (可接受) | 0.9167 | 0.7917（≥ guard floor 0.70） |
| 5 | Latency (Fast Semantic band) | P95 6.80ms | P95 6.71ms（无 regression） |

ABSTAIN / UNKNOWN 作为正常成功路径。

## 2. 购买的两个 PRIMARY mechanism

**A. Margin-based Abstention（进入 Candidate Policy）**
`CandidatePolicyOptions.MinimumTop1Top2Margin`；政策在 eligible top1–top2 上计算
margin，不足即 ABSTAIN。single-eligible 无歧义时不适用。配置化、无 magic number。

**B. Evidence Sufficiency Hardening（generic vs identity-discriminative）**
`EvidenceSufficiencyEvaluator` + `EvidenceSufficiencyProfiles.V1`：区分
GENERIC tokens（text/toggle/button/settings/system/network/…）与 exclusive
per-identity anchors（从 tuning corpora + 真实 trace 词表提取的 Identity 语义知识）。
规则：near-empty（0 text）· generic-only · 无 discriminative signal（anchors +
switch-state signals）· evidence score 不足 → ABSTAIN。通用 token 与 anchor 重合时
（如 "settings"）不作为独立 proof。无 case-id 特判。

**未购买（无证据/避免过拟合）**：multi-prototype、embedding replacement、
discriminative weighting、anchor learning、新 structural model、VLM/LLM。

## 3. Margin 参数选择（safety-first scan, former-heldout-v1 = regression/adversarial）

`validation/semantic/bge-held-out/scan_margin.py` →
`semantic-assets/heldout/reports/margin-scan-profile-v2.json`（全部 10 个 V1 FR 的
margin ≤ 0.053；net-F3 额外被 sufficiency 拒绝）：

| margin | FR | HNR | IEadm | CorrectRecovery | Top1 | Abstention |
|---|---|---|---|---|---|---|
| 0.000 (V1) | 0.208 | 0.792 | 1 | 0.917 | 0.854 | 0.438 |
| 0.040 | 0.042 | 0.958 | 0 | 0.833 | 0.896 | 0.562 |
| 0.045 | 0.042 | 0.958 | 0 | 0.792 | 0.875 | 0.583 |
| **0.050 (selected)** | **0.000** | **1.000** | **0** | **0.792** | **0.896** | **0.604** |
| 0.055 | 0.000 | 1.000 | 0 | 0.750 | 0.875 | 0.625 |
| 0.060 | 0.000 | 1.000 | 0 | 0.667 | 0.833 | 0.667 |
| 0.100 | 0.000 | 1.000 | 0 | 0.458 | 0.729 | 0.771 |

选择：safety-first 先最小化 FR（0），再最小化 IE-admission（0），随后取 recall 更高者
→ **margin = 0.05**（绝不优化 Top1；0.05 为 FR=0 区间的最高 CorrectRecovery）。

## 4. Profile V2（独立建立，V1 保留）

`SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2`
（`semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2.json`，SSOT）：
FeatureExtraction `v1-text-plus-type` · **EmbeddingModelIdentity 冻结**
（BAAI/bge-small-en-v1.5, v1.5, 384, fastembed+onnxruntime, fp32）· PrototypeProfile
`v1-canonical-signatures`（冻结）· RetrievalBackend cosine/exact（冻结）·
SimilarityMetric cosine · **CandidatePolicyProfile `CONTAINER_IDENTITY_POLICY_V2`**
（margin 0.05）· **EvidenceSufficiencyProfile `EVIDENCE_SUFFICIENCY_PROFILE_V1`**。

Profile **V1 保留且未改**（`SEMANTIC_CONTAINER_IDENTITY_PROFILE_V1` 保持
SAFETY_NOT_QUALIFIED；历史失败记录 pinned：T15 + v1 report 原样）。

回滚：配置 `Policy.ProfileVersion` v2→v1 即回滚（`FastSemanticPipelineFactory.CreateFromOptions`
绑定；无需改 Runtime）。

## 5. former-heldout-v1 重新运行（Profile V1 vs V2, BGE pipeline）

报告：`semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v2.json`
（V1 = `...-profile-v1.json`）。

| Metric | V1 | V2 |
|---|---|---|
| Top1 / Top3 / Top5 | 0.7500 | **0.8958** |
| FalseRecovery / FalsePositive | 0.4167 | **0.0000** |
| HardNegativeRejection | 0.5833 | **1.0000** |
| InsufficientEvidenceAdmitted | 3/7 | **0/7** |
| CorrectRecoveryRate | 0.9167 | 0.7917 |
| AbstentionRate | 0.4375 | 0.6042 |
| P50 / P95 / P99 (ms) | 3.80 / 6.80 / 7.40 | 3.85 / **6.71** / 7.11 |

历史 Safety Failure（10 FR + 3 IE 错误 admission）**全部消除**。即使全部转绿，也不
得宣布 HELD_OUT_QUALIFIED（heldout-v1 已是 former-heldout）。

## 6. Breakdown（Profile V2，required dimensions）

| Identity | 表现 |
|---|---|
| DeveloperOptions | 6/6 正例正确；0 FP |
| WifiSettings | 4/6 正例（A2/B2 由 net magnet 排序缺陷 abstain → EMBEDDING_SEPARATION）；0 FP |
| NetworkAndInternet | 4/6 正例（A2/B2 margin 0.041/0.009 abstain → MARGIN_AMBIGUITY）；0 FP |
| SettingsRoot | 5/6 正例（B2 margin 0.029 abstain → MARGIN_AMBIGUITY）；0 FP |
| None (24 negatives) | 24/24 ABSTAIN（HNR 1.0） |

无 identity 被 policy 完全饿死（各 identity 均保留多数正例 recovery）。

## 7. Failure Classification（residual, 5 case — 全部为 abstain 而非错误 claim）

| Class | Count | Cases | Next buyer |
|---|---|---|---|
| MARGIN_AMBIGUITY_FAILURE | 3 | ho-net-A2, ho-net-B2, ho-root-B2 | 已接受为 safety-first 代价（margin 与 recall 的权衡已文档化）；multi-state prototype / anchor 加权为未来 buyer |
| EMBEDDING_SEPARATION_FAILURE | 2 | ho-wifi-A2, ho-wifi-B2 | **PROTOTYPE_MAGNET_BUYER_OBSERVED**：NetworkAndInternet 高相似磁吸导致 wifi 页面排序错误；sufficiency+conflict 使其 fail-closed abstain（无错误 claim）；§11 严格判据（high-margin + 充分 evidence 的错误吸附）未满足 → 未确认，仅记录 |
| EVIDENCE_SUFFICIENCY / STRUCTURAL / THRESHOLD / UNKNOWN | 0 | — | — |

## 8. T4/T6/T8 生命周期（REGRESSION_SAFETY_RECOVERED）

T4 / T6 / T8 已转为 **Profile V2 Safety Regression Tests** 并因通用 hardening **自然转
GREEN**（无 special case）：
- `T4_HardNegativeRejection_ProfileV2RegressionSafety` GREEN（HNR 1.0, FR 0）
- `T6_InsufficientEvidenceAbstains_ProfileV2RegressionSafety` GREEN（7/7 abstain）
- `T8_SameCorpusComparison_ProfileV2` GREEN（FR 0, FPR 0 ≤ InMemory 0.9583）
记录为 **REGRESSION_SAFETY_RECOVERED，不是 PRODUCTION_QUALIFIED**。
V1 历史失败记录由 `T15_V1ProfileFailureRecordPreserved` 原样 pin（FR 0.4167）。

## 9. Degenerate reject-all guard（§18）

`SafetyHardeningAssessment`：CorrectRecoveryRate ≥ 0.70 且 AbstentionRate < 0.90，
否则 `SAFETY_HARDENING_OVER_REJECTS`。Profile V2：CorrectRecovery 0.7917 ≥ 0.70 ✓，
Abstention 0.6042 < 0.90 ✓ → **非 reject-all，guard 通过**（T14）。安全指标不是通过
"全部 ABSTAIN" 获得。

## 10. Tests（新增/调整，§23 T1–T14 + T15）

全部 GREEN（70/70 Semantic suite）：
T1 margin 足够 → 继续 · T2 margin 不足 → ABSTAIN · T3 near-empty → ABSTAIN ·
T4 generic-only → ABSTAIN · T5 充分 discriminative evidence → 允许 · T6 conflict
fail-closed · T7 structural fail-closed · T8 Policy V1/V2 可经 profile 独立选择与回滚 ·
T9 Vector Index 不知道 margin policy · T10 Embedding Provider 不知道 safety policy ·
T11 SemanticEvidence contract 未变化（属性面 pin）· T12 V2 config identity 可复现
（JSON SSOT ↔ C# 绑定一致）· T13 former-heldout regression safety（FR 0 / IE 0 / HNR 1.0）·
T14 Degenerate reject-all guard · T15 V1 失败记录保留。
既有 55 测试（含分离 gate T1–T8、兼容性、latency）全部保持 GREEN。

## 11. Exit Conditions（12/12 ✅）

1 margin-based abstention 进入 Candidate Policy ✅ 2 evidence sufficiency 已强化 ✅
3 机制全部配置化并绑定 Policy/Profile identity（versioned/rollbackable）✅
4 Profile V1 保留（未覆盖，NOT_QUALIFIED 原样）✅ 5 Profile V2 独立建立 ✅
6 former-heldout-v1 仅作 regression/adversarial ✅ 7 False Recovery 0（相对 V1 显著下降）✅
8 Insufficient evidence admission 0 ✅ 9 非 reject-all（guard 通过）✅
10 Correct Recovery 可接受（0.7917）✅ 11 Runtime-facing contract 不变 ✅
12 Embedding/Retrieval/Prototype 职责仍分离 ✅

## 12. Verification

- `dotnet build src/UniClaw.Runtime.sln` — 0 errors
- `dotnet test tests/Semantic/Semantic.Tests.csproj` — **70/70 PASS**
- `openspec validate --changes --strict --no-interactive` — run in-gate
- `scripts/check-consistency.sh` — run in-gate

## 13. 下一步纪律（freeze & qualify）

`SEMANTIC_SAFETY_REGRESSION_RECOVERED` 取得后：**停止继续调 Profile V2**。冻结
Feature/Embedding/Prototype/Retrieval/Policy/Config → 创建全新
`ContainerIdentity-heldout-v2`（未参与任何设计/调参）→ Profile V2 只运行资格验证。
禁止：看到 heldout-v2 failure → 调参 → 再跑 → 宣布 qualified（heldout-v2 一旦用于
设计即失去 held-out 身份）。

## Deliverables

- `semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2.json`（SSOT）
- `semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v2.json`（V2 报告）
- `semantic-assets/heldout/reports/margin-scan-profile-v2.json`（参数扫描证据）
- `validation/semantic/bge-held-out/run_held_out.py`（--profile v1|v2）+ `scan_margin.py`
- C#：CandidatePolicyOptions(V2 fields) · ContainerIdentityCandidatePolicy（margin+sufficiency）·
  EvidenceSufficiency{Options,Assessment,Evaluator,Profiles} · CandidatePolicies.V2 ·
  SemanticPerceptionProfiles.V2 · FastSemanticPipelineFactory.CreateFromOptions · 扩展
  CandidateEvaluationContext（text/structural/element fields）
- `tests/Semantic/.../SemanticSafetyHardeningTests.cs`（T1–T15）+ HeldOutValidationTests
  T4/T6/T8 → Profile V2 regression safety