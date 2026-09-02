# Semantic Profile V4 — Held-out Qualification

> Status: **HELD_OUT_QUALIFIED** | Decision: `SEMANTIC_PROFILE_V4_HELD_OUT_QUALIFIED` | Date: 2026-08-30
> Gate: `PROJECT_LEADER_SEMANTIC_PROFILE_V4_HELD_OUT_QUALIFICATION`
> Basis: `SEMANTIC_PROFILE_V4_READY_FOR_QUALIFICATION`
> Scope: QUALIFICATION ONLY. Profile V4 fully frozen (profile/terminology/anchor/
> prototype hashes pinned in the receipt BEFORE the corpus run); zero tuning;
> heldout-v4 is a fresh corpus that never participated in any design.

## Decision

```
PROJECT_LEADER_SEMANTIC_PROFILE_V4_HELD_OUT_QUALIFICATION_RESULT

Decision: SEMANTIC_PROFILE_V4_HELD_OUT_QUALIFIED
```

Profile V4 通过 Independent Offline Semantic Perception Qualification：
**Safety 全硬闸 0**（FR 0 / IE 0 / HNR 1.0 / conflict 0 / structural 0），
**Utility ≥ floor**（CorrectRecovery 0.750 ≥ 0.70；abstention 0.5625；per-identity
全 ≥ 0.50），**词汇泛化门通过**（LexicallyNovelPositiveRecovery 0.75 ≥ 0.65），
**概念碰撞安全门通过**（CC negative FR 0 / HNR 1.0）。仍不等于 physical-device
production proven。
**NEXT_GATE: `PROJECT_LEADER_SEMANTIC_RUNTIME_INTEGRATION`** —— 允许实现真实
BgeSmallEmbeddingProvider、SemanticOptions→PipelineFactory→ISemanticProvider→
Fusion 接线（feature flag · NoOp fallback · fail-closed · rollback）。

## 1. Qualification receipt（freeze 于 corpus run 前）

`reports/profile-v4-qualification-receipt.json`：

| Identity | Value |
|---|---|
| Profile sha | `09d9e058…`（run 前后一致） |
| FeatureRepresentationVersion | FEATURE_REPRESENTATION_V2 |
| TerminologyProfileHash | `110a2944…` |
| SemanticAnchorProfileHash | `c7e18eec…` |
| PrototypeProfileHash | `dbd11e08…`（= Profile V3，未变） |
| Corpus | `ContainerIdentity-heldout-v4` · 96 cases · sha `…` |
| Policy / Sufficiency / Retrieval | CONTAINER_IDENTITY_POLICY_V2 · EVIDENCE_SUFFICIENCY_PROFILE_V3 · cosine identity-max |
| margin | 0.05 |

## 2. Corpus（考试 = 全新）

96 = 4 identities × 14 positives + 40 negatives。**LEXICALLY_NOVEL_POSITIVE ×8**
（Window animation scale · Force activities to be resizable · Scanning always
available · Wi‑Fi charging · Preferred network type · SIM card lock · Quick tap ·
Now Playing —— 语义属于已知 concept family，词面从未出现在 prototype /
terminology / 任一 development corpus）+ **CONCEPT_COLLISION_NEGATIVE ×8**
（跨 container 共享概念的压力样本）。Sources：RealTrace 4（truth.json 未用
子集）· Manual 52 · Synthetic 40（真实 trace 受限如实记录）。Isolation：与
tuning+v1+v2+v3 的 fingerprint 完全互斥（初版 6 处实例复用被隔离屏当场捕获并
替换——隔离检查正常工作是本 Gate 的设计意图）。

## 3. Gates（全部 HARD）

| Gate | Criterion | Result |
|---|---|---|
| FR / IE / HNR / conflict / structural | 0/0/1.0/0/0 | **PASS（0/0/1.0/0/0）** |
| CorrectRecovery | ≥ 0.70 | **PASS（0.7500）** |
| AbstentionRate | < 0.90 | PASS（0.5625，非 reject-all） |
| Per identity | ≥ 0.50 | **PASS**：dev 0.929 · wifi 0.857 · net 0.643 · root 0.571 |
| No starvation | 每 identity ≥ 7/14 | PASS（root 8/14，未饿死） |
| **LexicallyNovelPositiveRecovery** | ≥ 0.65 | **PASS（0.75，6/8）** —— concept generalization 而非 dictionary |
| **ConceptCollisionNegative FR / HNR** | 0 / 1.0 | **PASS（0 / 1.0）** —— normalization 未压爆容器边界 |
| Top1 · margin median | 报告 | 0.8542 · **0.102**（fresh corpus 上仍维持右移；<0.05 仅 9/56） |
| P50 / P95 ms | 报告 | 4.98 / 6.93（无 latency regression） |

## 4. 核心结论

考试问题的答案是 **PASS**：当"从没见过的词"出现、但语义属于同一 domain concept
时，Pipeline **仍然认得出来**（LNP 6/8；SSID 类与 margin 极限个案除外）。Concept
collision 被完整 evidence + ranking + margin + policy 挡回（CC negative 全 abstain）
—— normalization 没有把不同 container 压得过近。SettingsRoot 0.571 在 floor 之上
（margin 主导的残余，未触发 starvation → structural buyer 未重开）。

## 5. Residual（14 positive misses —— 只分类不修）

EVIDENCE_SUFFICIENCY ×7（单行 sparse 设计 + 无 title 且需 ≥2 概念的行组合）·
MARGIN_AMBIGUITY ×7（fresh 词面仍部分挤入低 margin 带）。无系统性 rank-order →
EMBEDDING_MODEL_BUYER 不确认；无概念充分但需拓扑的明确证据 → structural buyer
未确认（仅记录未来观察）。

## 6. 生命周期

Profile V1 NOT_QUALIFIED · Profile V2 UTILITY_INSUFFICIENT（Q8/Q10 RED 保留）·
Profile V3 UTILITY_INSUFFICIENT（Q9/Q11/Q12 RED 保留）· heldout-v4 = **本次
QUALIFIED corpus（不再复用为未来 qualification）** · 未来任何 Profile 变更 →
Profile V5 + 全新 heldout-v5。

## 7. Tests / Verification

**158 总计：153 PASS / 5 RED（5 = 历史 qualification FAIL 记录：V2 Q8/Q10 + V3
Q9/Q11/Q12 —— evidence，不清理）。** V4 Q1–Q22 全绿。
`dotnet build src/UniClaw.Runtime.sln` 0 errors · `openspec validate --changes
--strict --no-interactive` 11/11 · `scripts/check-consistency.sh` ALL PASS。

## NEXT_GATE

`PROJECT_LEADER_SEMANTIC_RUNTIME_INTEGRATION` —— qualification receipt 成为唯一
合法 Semantic baseline。该 Gate 才允许：实现真实 BgeSmallEmbeddingProvider ·
接入真实 model runtime · SemanticOptions→FastSemanticPipelineFactory→
FastSemanticContainerIdentityProvider→ISemanticProvider→SemanticEvidenceFusionPipeline
接线 · feature flag / NoOp fallback / fail-closed / rollbackable · 真实
Observation→SemanticEvidence→Runtime Fusion。

## Deliverables

- `semantic-assets/heldout/ContainerIdentity-heldout-v4.json` +
  `manifest-heldout-v4.json`（96 cases，含 LNP/CC 分布与 isolation 记录）
- `reports/profile-v4-qualification-receipt.json`（result=QUALIFIED）·
  `reports/container-identity-heldout-v4-bge-small-profile-v4.json`
- `tests/Semantic/.../HeldOutContainerIdentityCorpusV4.cs` ·
  `SemanticProfileV4QualificationTests.cs`（Q1–Q22）
- 决策记录 + registry（245）