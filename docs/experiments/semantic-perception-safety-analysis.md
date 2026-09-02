# Semantic Perception Safety Analysis — Pipeline View

> Gate: `PROJECT_LEADER_SEMANTIC_PERCEPTION_PIPELINE_BOUNDARY_AND_SAFETY_REVIEW`
> Date: 2026-08-30 · ANALYSIS/DESIGN REVIEW ONLY
> Input evidence: `PROJECT_LEADER_BGE_SMALL_HELD_OUT_VALIDATION_RESULT`
> (frozen profile + ContainerIdentity-heldout-v1 + committed reports), source audit
> of `src/UniClaw.Semantic.Infrastructure/`, similarity/margin data at
> `semantic-assets/heldout/reports/similarity-separation-analysis.json`.
> Decision record: `docs/decisions/semantic-perception-pipeline-boundary-review.md`.

## 1. 概念模型修正（frozen terminology）

| 术语 | 职责 | 实例 |
|---|---|---|
| Embedding Model | 把 feature text → vector | BGE-small / BGE-base / future model / VLM-derived |
| Model Runtime | 执行 embedding 的基础设施 | fastembed / ONNX Runtime / HuggingFace / Torch |
| Vector Retrieval Backend | vector → nearest candidates | InMemory / FAISS / Qdrant / Milvus |
| Feature Extraction | Observation → feature representation | `v1-text-plus-type` |
| Candidate Policy | 判断 similarity 是否足以形成 evidence | conflict rejection · margin · evidence sufficiency · thresholds |
| Semantic Perception Provider | 组装整条 pipeline → SemanticEvidence / ABSTAIN | `FastSemanticContainerIdentityProvider` |
| Profile | 一次 qualification 的对象（非单模型） | `SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2`（§8） |

**BGE-small ≠ Vector Backend；BGE-small = Embedding Model。** qualification 的对象是
完整 pipeline profile，不是模型。

## 2. 失败层归属（pipeline 视角）

`FAST_SEMANTIC_CONTAINER_IDENTITY_PIPELINE_PROFILE_V1` =
FeatureExtraction `v1-text-plus-type` + BGE-small embedding + canonical
prototypes v1 + cosine retrieval + structural compatibility + previous-identity
conflict rejection + per-identity threshold + minimum-evidence rule。

held-out：Top1 = 0.7500 · FalseRecovery = 0.4167 · HardNegativeRejection = 0.5833。

**结论：PIPELINE PROFILE V1 未通过 safety qualification。** 不是"BGE-small 不安全"，
而是该组合的 acceptance 边界（per-identity threshold 0.30/0.65）在 held-out 分布上
无法把必须 abstain 的内容挡在门外；另有 1 例 near-empty 表示缺陷、2 例排序缺陷。

失败分类（12 case）：

| 层 | 分类 | 数量 | 归属 |
|---|---|---|---|
| Configuration | CONFIGURATION_GENERALIZATION_FAILURE（threshold 带外） | 9 | per-identity threshold 0.30/0.65 无法覆盖 held-out 高相似带 |
| Feature Representation | FEATURE_REPRESENTATION_FAILURE | 1 | ho-net-F3 near-empty（0 text token → net 0.659） |
| Embedding/Retrieval | EMBEDDING_SEPARATION_FAILURE | 2 | ho-wifi-A2/B2（net magnet 排序错误） |
| Prototype | PROTOTYPE_FAILURE（magnet 根因之一） | — | NetworkAndInternet magnet（见 §4） |
| Candidate Policy | CANDIDATE_POLICY_FAILURE | 9 | acceptance 只看 abs-sim，无 margin/evidence 维度 |
| Evidence Sufficiency | EVIDENCE_SUFFICIENCY_FAILURE | 3 | ho-dev-D1 / ho-net-D1 / ho-net-F3 拒绝依据不足 |

## 3. Embedding Separation Analysis（BGE-small, heldout-v1, 47 embedded）

来源：`similarity-separation-analysis.json`（每 case 记录对 4 prototype 的完整 cosine 向量）。

### 3.1 absolute similarity 分布（max over 4 prototypes）

| 桶 | n | min | median | p95 | max |
|---|---|---|---|---|---|
| positives | 24 | 0.750 | 0.889 | 0.976 | 1.000 |
| negatives | 23 | 0.659 | 0.789 | 0.919 | 0.922 |
| hard-negatives | 23 | 0.659 | 0.789 | 0.919 | 0.922 |
| pos title-visible | 8 | 0.877 | 0.908 | 0.993 | 1.000 |
| pos title-offscreen | 8 | 0.790 | 0.845 | 0.945 | 0.951 |
| pos partial | 8 | 0.750 | 0.861 | 0.914 | 0.919 |

negative 与 positive 的重叠带 **0.75–0.92 完全重叠**：绝对相似度单独无法作为
acceptance 依据（这正是"PIPELINE V1 threshold-only acceptance"的死穴）。
pos title-visible 全部 ≥ 0.877 但 negatives 也有 0.92 的（wifi-F1 0.918、wifi-F3 0.919）。

### 3.2 Top1–Top2 margin 分布

| 桶 | n | margin min | margin median | margin p95 | margin<0.05 |
|---|---|---|---|---|---|
| positives | 24 | 0.009 | 0.078 | 0.225 | 4/24 |
| negatives | 23 | 0.004 | 0.044 | 0.162 | 13/23 |
| title-visible positives | 8 | 0.041 | 0.075 | 0.211 | 1/8 |
| offscreen positives | 8 | 0.009 | 0.062 | 0.177 | 3/8 |
| partial positives | 8 | 0.053 | 0.108 | 0.224 | 0/8 |

margin 部分可判别：negatives 57%（13/23）margin < 0.05，positives 仅 17%（4/24）。
但 4 个正例 margin < 0.05（ho-wifi-B2 0.039、ho-net-A2 0.041、ho-net-B2 0.009、
ho-root-B2 0.029）→ 单纯 margin 门槛会误伤（recall 代价集中于 net 磁吸区）。
**margin 是有价值的 policy 维度，但不是免费午餐。**

### 3.3 raw ranking（pre-policy top1）

- positives raw-top1 正确率：**22/24**（partial 8/8, offscreen 7/8, title-visible 7/8；
  2 个 miss 均为 WifiSettings → NetworkAndInternet）。
- 结论：**BGE-small 对强证据（多行/结构明确）已提供可用相对 separation。**

### 3.4 Semantic Magnet 检测（negative top1 吸引）

| 被吸引 Identity | negative 中出现次数 | 说明 |
|---|---|---|
| NetworkAndInternet | 12/23 | **主磁吸体**：网络词表（network/cellular/sim/Wi-Fi/data）被压缩到相似带 |
| DeveloperOptions | 6/23 | 次磁吸体：type-word 主导的弱内容（如单 "System (menu_item)"）倾向 dev |
| SettingsRoot | 4/23 | root 行词表宽，弱证据易吸向 root |
| WifiSettings | 1/23 | 最低（但 wifi 页面也高相似 0.84–0.92） |

另：2 个 positive miss 也全部流向 NetworkAndInternet（confusion WifiSettings→Net ×2）。
正的相似带（0.66–0.92）中心化在网络/设置词表 → **prototype 表示 + 词表分布共同造成磁吸**。

### 3.5 判定

```
存在可用相对 separation（22/24 正例排序正确 + margin 部分可判别）
→ 非 EMBEDDING_MODEL_BUYER_FOUND
acceptance policy 错误（threshold-only 无视 margin/evidence；10 FR 全部 margin≤0.053）
→ CANDIDATE_POLICY_BUYER_FOUND（primary）
＋ FEATURE_REPRESENTATION_BUYER（near-empty 表示缺陷）
＋ PROTOTYPE_BUYER（net magnet；multi-state prototype，见 §5）
```

## 4. Feature Representation Analysis（v1-text-plus-type）

query_text = per-element `text (type)`。type 词（text / menu_item / text_block /
switch）与高频 settings 词（settings / system / network / connected / display /
options）是 **GENERIC_UI_SIMILARITY tokens**，在短 query 中占比高：单行
`"System (menu_item)"` 的 token 面 = 60% generic → 相似带扁平（dev 0.776 /
net 0.767 / root 0.755，margin 0.010）。near-empty（0 text token）时 embedding
退化为纯 generic 偏差向量 → ho-net-F3 用空内容产出 net 0.659。

**IDENTITY_DISCRIMINATIVE**：`AndroidWifi`、`Wi-Fi`、`Cellular`、`SIM cards`、
`Developer options`、`Enable demo mode`、`Automatic system updates`、`Settings`
title、`Search settings`、非高频行名（`Mobile data`、`Emergency alerts`、
`Dark theme, font size, brightness` 等长描述行弱判别）。

**GENERIC_UI_SIMILARITY**：`System`、`Connected`、`Display`、`Security`、
`Privacy`、type 词、`Settings` 作为通用标题词的泛化、`Network & internet` 作为
root/net 共享行。

未来 buyer（本 Gate 只登记，不实现）：
stable anchors / anchor combinations / structural signatures（type+switch 分布 /
行序）/ hierarchy（父容器行 vs 子页面）/ viewport state（title-visible vs
offscreen vs partial 应使用不同表示）/ discriminative feature weighting（generic
token 降权）/ discrim-anchor count（§6）。

## 5. Prototype Analysis（v1-canonical-signatures × 4）

单 Identity 单 canonical prototype 在 title-visible（median margin 0.075）成立，
在 scroll-middle / partial / low-information 状态**不成立**：
- offscreen 正例 margin 中位从 0.075 → 0.062，4 个低 margin 正例 3 个来自
  offscreen/滚动态；
- net prototype（3 个 menu_item 行）把整个网络词表磁吸到 0.65–0.92；
- 同页面不同 viewport 状态 query 与同一 prototype 的距离分布差异大。

目标：**Multi-Prototype Container Representation**（title-visible /
scroll-middle / scroll-bottom / partial / low-information 每状态独立 prototype 或
状态加权）。约束：prototype 必须描述可泛化页面状态，**不得**针对个别 heldout
case 制造 prototype。→ 登记 PROTOTYPE_BUYER（未来）。

## 6. Failure → Buyer → Minimal Mechanism 映射（heldout-v1, 12 failures）

目标优先级：**先降 False Recovery，不牺牲 Safety 换 Recall。**

| Case | failure 层 | Buyer | Minimal mechanism（只登记 buyer） |
|---|---|---|---|
| ho-dev-D1（"System" 单行 → dev 0.776, margin 0.010） | Policy + Evidence | CANDIDATE_POLICY + EVIDENCE_SUFFICIENCY | 最低 discriminative-anchor 数 + margin ≥ min 才能 delist；低证据一律 abstain |
| ho-dev-F2（accessibility 行 → dev 0.777, margin 0.039） | Policy + Prototype | CANDIDATE_POLICY + PROTOTYPE | discriminative-anchor absence 检测（accessibility 页无 dev anchor） |
| ho-wifi-E1（hotspot 页 → net 0.870, margin 0.048） | Policy + Prototype | CANDIDATE_POLICY + PROTOTYPE | net magnet 抑制：margin 门槛 + 相对 prototype 距离语义检查 |
| ho-wifi-E2（Ethernet/VPN 页 → net 0.886, margin 0.039） | Policy + Prototype | CANDIDATE_POLICY + PROTOTYPE | 同上 |
| ho-net-D1（"Airplane mode" 单行 → net 0.784, margin 0.004） | Policy + Evidence | CANDIDATE_POLICY + EVIDENCE_SUFFICIENCY | 单行非 anchor → abstain；margin < 宽容带 → abstain |
| ho-net-E1（root 行子集 → net 0.921, margin 0.028） | Policy | CANDIDATE_POLICY | margin 门槛（0.028）；root 与 net 需要跨 prototype 竞争，不能只看 abs |
| ho-net-F1（data-usage 页 → net 0.785, margin 0.007） | Policy + Evidence | CANDIDATE_POLICY + EVIDENCE_SUFFICIENCY | 低 margin + 无 net anchor → abstain |
| ho-net-F3（near-empty → net 0.659） | Representation + Evidence | FEATURE_REPRESENTATION + EVIDENCE_SUFFICIENCY | text token=0 → 硬 abstain（不进 embedding） |
| ho-root-E1（System 页 → root 0.789, margin 0.018） | Policy + Evidence | CANDIDATE_POLICY + EVIDENCE_SUFFICIENCY | 低 margin + 无 root anchor → abstain |
| ho-root-F2（search-soup → root 0.843, margin 0.035） | Policy + Evidence | CANDIDATE_POLICY + EVIDENCE_SUFFICIENCY | label-soup 无 structural corroboration → abstain |
| ho-wifi-A2（wifi 页 → raw top1 net） | Embedding/Retrieval | RETRIEVAL_RANKING（net magnet） | 排序层 buyer：wifi anchor 加强 / net prototype 去磁 |
| ho-wifi-B2（wifi 页 → raw top1 net） | Embedding/Retrieval | RETRIEVAL_RANKING（net magnet） | 同上 |

机制候选排序（全部 buyer，不实现）：① text-token 最低数 + 硬 abstain（堵 near-empty，
1/10 FR）；② top1–top2 margin 门槛（载 10/10 FR，代价 4 正例 margin<0.05 → 需要与
anchor count 组合）；③ discriminative-anchor count（跨 9/10 FR 有证据）；④
per-identity threshold 带外重标定（属于 CONFIGURATION 层，必须在新 tune corpus 上
重拟合，不能在本 gate 做）。禁止一次性购买全部（§17 纪律）。

## 7. InMemory 重新分类（真实代码依据）

- `InMemoryVectorSemanticIndex.Score()`：text fragments + element types +
  structural features 的 overlap 比值打分——**无 vector，无 embedding，无 L2/cosine**；
  `Retrieve()` 内置 `_matchThreshold` 决定是否返回 candidate → **Policy 在 index 内部**。
- `InMemoryVectorIndexOptions`：`Patterns`（prototype 数据）+ `MatchThreshold`（policy）
  ——retrieval options 同时携带 prototype 与 policy。
- held-out：Top1 0.4167 · FalseRecovery 0.9583（含空文本 "type:text" 观测置信度 1.0）。

**分类：B. DETERMINISTIC_REFERENCE_MATCHER**（不是 exact vector retrieval，
也不是 test-only fixture）；作为 profile：**D. PRODUCTION_SEMANTIC_PROFILE_NOT_QUALIFIED**
（held-out safety 未通过）。但生产 Runtime 当前 **未启用** 该路径（
`SemanticEvidenceFusionPipeline` 默认 `NoOpSemanticProvider`，且该 seam 未接入
Agent 决策逻辑；`SemanticOptions.FastSemanticProviderEnabled=true` 是无消费者的
inert 默认）→ **今日无 ACTIVE production safety risk**；任何将来启用必须先进
safety gate（boundary guard，本 gate 不改生产行为）。

## 8. Pipeline Profile Identity（future V2 schema, frozen）

以后 qualification 对象 = `SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2`，绑定：
FeatureExtractionVersion · EmbeddingModelId · EmbeddingRevision · ModelRuntime ·
PrototypeVersion · VectorBackend · SimilarityMetric · CandidatePolicyVersion ·
ThresholdProfile · CorpusVersion。PIPELINE V1 即该 schema 的实例
（FeatureExtraction `v1-text-plus-type` + BGE-small + prototypes v1 + cosine +
InMemory(expt backend) + policy v1 + thresholds round2 + heldout-v1）。V2 修复后
**不得**用 heldout-v1 宣布 production qualified——最终 qualification 必须使用
`ContainerIdentity-heldout-v2`（未参与任何设计/调参）。

## 9. Safety Objective（freeze）

1. False Runtime Identity Recovery → 0（或尽可能接近 0）
2. Insufficient Evidence Admission → 0
3. Hard Negative Rejection
4. Correct Recovery
5. Latency

**ABSTAIN / UNKNOWN 是正常成功路径**；不得为提高 Top1 牺牲 FalseRecovery。
不得因 BGE 存在提前接入 HuggingFace；不得因未来 Perception Service 提前接入 Ray。

## 10. Asset Lifecycle（heldout-v1）

`ContainerIdentity-heldout-v1` 已履行一次真 held-out qualification，failure 已公开
分析 → 重新登记为 **former-heldout + regression/adversarial evidence source**：
允许 safety hardening / regression / adversarial benchmark / debugging / profile
comparison；**禁止**作为未来最终 qualification dataset。未来创建
`ContainerIdentity-heldout-v2`（见 `semantic-assets/heldout/MANIFEST.md`）。