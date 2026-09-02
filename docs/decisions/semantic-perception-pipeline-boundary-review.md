# Semantic Perception Pipeline — Boundary & Safety Review

> Status: RESPONSIBILITY_MIX_FOUND | Decision: `SEMANTIC_PIPELINE_RESPONSIBILITY_MIX_FOUND` | Date: 2026-08-30
> Gate: `PROJECT_LEADER_SEMANTIC_PERCEPTION_PIPELINE_BOUNDARY_AND_SAFETY_REVIEW`
> Evidence: `docs/experiments/semantic-perception-safety-analysis.md` · held-out reports ·
> source audit of `src/UniClaw.Semantic.Infrastructure/` + `src/UniClaw.Runtime/Capabilities/Perception/Semantic/`
> Basis: `PROJECT_LEADER_BGE_SMALL_HELD_OUT_VALIDATION_RESULT`
> Type: ANALYSIS / DESIGN REVIEW ONLY — no production behavior change, no tuning,
> no held-out repair, no Runtime integration.

## Decision

```
PROJECT_LEADER_SEMANTIC_PERCEPTION_PIPELINE_BOUNDARY_AND_SAFETY_REVIEW_RESULT

Decision: SEMANTIC_PIPELINE_RESPONSIBILITY_MIX_FOUND

NEXT_GATE: PROJECT_LEADER_SEMANTIC_PIPELINE_RESPONSIBILITY_SEPARATION
（完成后 → PROJECT_LEADER_SEMANTIC_SAFETY_HARDENING_APPLY）
```

运行时面对的责任边界（`ISemanticProvider` → `SemanticEvidence`）基本正确，但
Embedding / Retrieval / Prototype / Policy 在**具体实现与配置模型**中存在真实
职责混合（下述每一项均有代码证据），且该混合已阻塞安全加固的表达（无法在配置中
声明 policy profile / embedding identity）。V1 的 held-out failure 主买家在
Candidate Policy 层（`CANDIDATE_POLICY_BUYER_FOUND`），不是单模型问题，也不在
Runtime 边界。

## 1. 概念模型修正（frozen）

BGE-small = **Embedding Model**（不是 Vector Backend）。fastembed/ONNX/HuggingFace/
Torch = Model Runtime。InMemory/FAISS/Qdrant/Milvus = Vector Retrieval Backend。
`FastSemanticContainerIdentityProvider` = Semantic Perception Provider。
`ISemanticProvider` = Runtime-facing Semantic Perception contract。
Qualification 对象 = 完整 pipeline **Profile**（`SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2`
schema，`docs/experiments/semantic-perception-safety-analysis.md §8`），不是单模型。

## 2. 审计结论（真实代码依据）

**Q-A. IVectorSemanticIndex 当前是否只负责 vector → nearest candidates？**

否。接口形状正确：`Retrieve(ContainerSemanticQuery) → SemanticCandidate?`
（只收已提取特征、只返回相似度候选）。但唯一实现
`InMemoryVectorSemanticIndex` 内部承担了多于 retrieval 的职责：
- 自建文本/类型/结构 overlap 打分（非 vector distance）——
  `Score()` 遍历 `query.TextFragments/ElementTypes/StructuralFeatures` 与
  `SemanticPattern` 的 overlap；
- 内置 acceptance：`Retrieve` 内 `best.SimilarityScore >= _matchThreshold ? best : null`
  ——**policy（threshold）在 index 内部**；
- `SemanticPattern`（identity 的 text/type/structural 签名）由调用方加载进 index，
  即 **prototype 数据储存在 retrieval 层**。
→ 接口层基本正确；实现层 Retrieval+Policy+Prototype 混合。

**Q-B. InMemoryVectorSemanticIndex 属于？**

**2. DETERMINISTIC_REFERENCE_MATCHER**：无 vector、无 embedding、无距离度量，
纯 deterministic overlap matcher + 内嵌 threshold。它既不是 exact vector retrieval
backend（答案 1），也不是"仅 fixture"（答案 3——被 `SemanticVectorIndexRegistry`
注册为默认 backend 且被 benchmark 当作生产 default 使用）。
作为 profile：`PRODUCTION_SEMANTIC_PROFILE_NOT_QUALIFIED`
（held-out FR 0.9583 / Top1 0.4167，且空文本 "type:text" 观测置信度 1.0）。

**Q-C. BGE 实验是否存在 Embedding + Index + Policy 组合成 "backend" 概念错误？**

是，至少三层证据：
- `SemanticVectorBackend.Bge = "BGE"` 与 `Faiss/Qdrant/Milvus` 混在同一 backend
  标识空间——embedding model 被命名成 backend；
- `ISemanticVectorIndexFactory.Create(InMemoryVectorIndexOptions?)` 的契约是
  InMemory 形状（patterns + matchThreshold），对未来所有 backend 都传
  InMemory 专属 options——没有 embedding/provider/policy 维度；
- 实验 runner 在单脚本里组合 embed + prototype + retrieval + rules，文档持续以
  "BGE backend" 命名——概念模型把 pipeline 压缩成 "backend"。

## 3. 配置责任 map（SemanticOptions 现状 vs 目标）

现状（`SemanticOptions`：`FastSemanticProviderEnabled` / `VectorBackend` /
`InMemoryIndex` / `Benchmark` / `Evaluation`）——只有 retrieval backend + benchmark
身份；**无 embedding 身份、无 prototype profile、无 policy profile**；
`FastSemanticProviderEnabled = true` 默认无消费者（inert）。

目标身份（每个身份独立可声明/可 pin，不改实现）：

| 身份 | 现状 | 目标 |
|---|---|---|
| Provider | – | semantic.provider |
| Embedding Model identity | ❌ 无（BGE 挤在 backend 常量里） | embedding.provider/model/revision/runtime/device/precision |
| Vector Backend | ✅ `VectorBackend` / registry | retrieval.backend/metric/topK |
| Prototype Profile | ❌（prototype 数据在 InMemory options） | prototype.profile |
| Policy Profile | ❌（threshold 在 index 内部/options 里） | policy.profile |
| Pipeline Profile | ❌ | 上述聚合为一个 profile identity |

→ 目标：Model / Model Runtime / Vector Backend / Prototype Profile / Safety Policy /
Pipeline Profile 必须为**独立 configuration identity**（$E.A2 Responsibility
Separation Gate 购买；本 Gate 不实现）。

## 4. Embedding ↔ Vector Index 独立可组合（目标模型冻结）

`IEmbeddingProvider`（BgeSmall/BgeBase/Future/VLM-derived）× `IVectorSemanticIndex`
（ExactInMemory/FAISS/Qdrant/Milvus）必须可自由组合：BGE-small+InMemory、
BGE-small+FAISS、BGE-small+Qdrant、FutureEmbedding+InMemory…。Benchmark 必须分开回答
**Embedding Quality** 与 **Index Recall/Latency/Resource**，不得混成一个维度。
现状：C# 侧接口天然可分（index 只收 query、不产 embedding）；实验侧完全混合。
分离 Gate 的验收 = 同一 corpus 上拆分报告两个维度。

## 5. InMemory 正确角色 / 生产风险

- 分类：**DETERMINISTIC_REFERENCE_MATCHER**（职能）；作为当前 designated profile：
  **PRODUCTION_SEMANTIC_PROFILE_NOT_QUALIFIED**。
- **生产激活状态（实测）**：`SemanticEvidenceFusionPipeline` 默认
  `NoOpSemanticProvider`，其注释明确 "NOT wired into Agent decision logic"；
  `FastSemanticContainerIdentityProvider` 只存在于 `UniClaw.Semantic.Infrastructure`
  并被 tests/benchmarks 构造；`SemanticOptions` 无生产消费者。
  → **今日无 ACTIVE production safety risk（E 不适用为当前态）**；但配置默认描述
  未合格 profile——任何未来启用必须先过 Safety Gate（boundary guard，本 Gate 不改）。

## 6. Held-out Failure 重新解释（摘要，详见 analysis doc）

PIPELINE PROFILE V1 = v1-text-plus-type + BGE-small + prototypes v1 + cosine +
R1 structural + R2 conflict + R3 per-identity threshold + R4 min-evidence。
held-out：Top1 0.75 / FR 0.4167 / HNR 0.5833 → **PIPELINE PROFILE V1 未通过
safety qualification**。失败层：CONFIGURATION_GENERALIZATION（9，threshold 带外）、
FEATURE_REPRESENTATION（1，near-empty）、EMBEDDING_SEPARATION（2，net magnet
排序 miss）、EVIDENCE_SUFFICIENCY（3）。主买家 = **CANDIDATE_POLICY**（10 FR 全部
margin ≤ 0.053 且 abs-sim 0.66–0.92 与正例重叠带完全重合；absolute-sim 单独不可
判别）；次买家 = FEATURE_REPRESENTATION（generic token 主导/空 query 偏差）+
PROTOTYPE（net magnet；multi-state prototype）。**非 EMBEDDING_MODEL_BUYER**（22/24
正例 raw-top1 排序正确，separation 相对可用）。映射与 minimal mechanisms 见
`docs/experiments/semantic-perception-safety-analysis.md §6`。

## 7. 职责模型冻结（目标，非本次重构）

```
Semantic/Fast
├── Pipeline            FastSemanticContainerIdentityProvider
├── Features            IContainerSemanticFeatureExtractor
├── Embedding           IEmbeddingProvider · ModelIdentity
├── Retrieval           IVectorSemanticIndex · SemanticCandidate
├── Policy              ContainerIdentityCandidatePolicy
├── Prototype           ContainerIdentityPrototypeStore
└── Evidence            SemanticEvidenceBuilder
```

只在真实混淆阻塞后续（分级见 §3/§4/§8）时才购买重构——本 Gate 已判定配置模型
不可表达 policy/embedding identity，属于阻塞项（下一 Gate 购买 separation）。

## 8. 最终 10 问回答

1. **BGE-small 职责** → Embedding Model（把 feature text → vector）；不是 backend/index。
2. **IVectorSemanticIndex 职责** → Vector Retrieval（query 特征 → nearest candidates），
   接口已正确；实现层（InMemory）越界承担 matcher+threshold。
3. **当前 InMemory** → DETERMINISTIC_REFERENCE_MATCHER（无 vector/distance）+ 作为
   profile PRODUCTION_SEMANTIC_PROFILE_NOT_QUALIFIED（held-out FR 0.9583）；
   生产路径今日未启用（NoOp default）。
4. **Feature/Embedding/Prototype/Retrieval/Policy 是否分离** → 部分：Feature Extractor
   与 Provider 分离；但 Retrieval 实现内含 Policy(threshold)+Prototype(patterns)，
   config 无 Embedding/Prototype/Policy 身份，experiment 术语把 Embedding 当 backend。
5. **held-out failure 主 buyer 层** → Candidate Policy（threshold-only acceptance，
   无视 margin/evidence；10 FR 全是低 margin 高 abs）；次：Feature Representation
   + Prototype。
6. **需要 architecture refactor 吗** → 需要（有限）：Responsibility Separation Gate
   购买 = 把 policy/threshold 从 index 移出为 policy profile、prototype 独立 store、
   配置具备 embedding/prototype/policy 独立身份；ISemanticProvider 边界不动。
7. **需要换 embedding model 吗** → 不基于本数据（separation 相对可用 22/24；
   非 EMBEDDING_MODEL_BUYER）。是否换模型是 `SEMANTIC_EMBEDDING_MODEL_EVALUATION`
   类 gate 的独立问题，且必须先解决 policy/representation。
8. **需要修改 Semantic contract 吗** → 不需要（margin/evidence/anchor 等全部可在
   pipeline 内部表达；不触碰 SemanticEvidence / ISemanticProvider）。
9. **heldout-v1 后续角色** → former-heldout + regression/adversarial evidence
   source（hardening/regression/adversarial/debug/profile comparison 可用）；
   禁止作为未来最终 qualification；未来创建 heldout-v2（未参与任何设计/调参）。
10. **NEXT_GATE** → `PROJECT_LEADER_SEMANTIC_PIPELINE_RESPONSIBILITY_SEPARATION`
    （完成后 → `PROJECT_LEADER_SEMANTIC_SAFETY_HARDENING_APPLY`）；BGE-small 的
    Runtime integration 在 safety gate 全绿前保持禁止。

## 9. Deliverables（本 Gate）

- `docs/experiments/semantic-perception-safety-analysis.md`（分析 hub：separation
  统计 / magnet / feature / prototype / failure→buyer→mechanism 映射 / safety
  objective / asset lifecycle）
- `semantic-assets/heldout/reports/similarity-separation-analysis.json`（可复现分析数据）
- `validation/semantic/bge-held-out/analyze_separation.py`（只读分析脚本）
- `semantic-assets/heldout/MANIFEST.md` + `semantic-assets/README.md`（heldout-v1
  生命周期重新登记）
- `docs/experiments/semantic-model-evaluation-summary.md`（术语修正：Embedding Model
  vs Retrieval Backend vs Model Runtime）
- 本决策记录 + registry 登记

## 10. 验证

- `dotnet build src/UniClaw.Runtime.sln`：0 errors（无 src 变更）
- `dotnet test tests/Semantic/Semantic.Tests.csproj`：40 PASS / 3 RED
  （T4/T6/T8 保持真实 RED evidence，按要求不修绿）
- `openspec validate --changes --strict --no-interactive`：11/11 PASS
- `scripts/check-consistency.sh`：ALL PASS
- 生产行为 / 调参 / held-out repair / Runtime integration：**无**