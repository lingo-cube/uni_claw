# Semantic Perception Model Evaluation Summary

> Date: 2026-08-19
> Scope: Offline/experimental evaluation of Fast Semantic and candidate models
> (InMemory, BGE-small, BGE-base, Qwen2.5-VL-3B-UI-R1).
> Constraint: No Runtime / Provider / Contract change.

## 1. Model inventory

| Alias | Model | Type | Status |
|---|---|---|---|
| InMemory | InMemoryVectorSemanticIndex | Deterministic read-only index | Production default |
| bge-small-en-v1.5 | BAAI/bge-small-en-v1.5 | Text embedding (ONNX) | Tested |
| bge-base-en-v1.5 | BAAI/bge-base-en-v1.5 | Text embedding (ONNX) | Tested |
| bge-m3 | BAAI/bge-m3 | Text embedding | Not tested (fastembed unsupported) |
| qwen2.5-vl-3b-ui-r1 | Qwen2.5-VL-3B-UI-R1 | Vision-Language / UI reasoning | Experimental |

## 2. Fast Semantic baseline (InMemory)

DeveloperOptions-v1 corpus:

| Metric | Value |
|---|---|
| Top1 Accuracy | 1.0000 |
| Top3 Recall | 1.0000 |
| Top5 Recall | 1.0000 |
| False Recovery | 0.0000 |
| False Positive | 0.0000 |
| P50 / P95 / P99 | 0.0044 / 1.0405 / 1.2451 ms |

## 3. BGE raw prototype matching (Round 1)

DeveloperOptions-v1 6 cases, global threshold 0.30.

| Backend | Top1 | FalseRecovery | FalsePositive |
|---|---|---|---|
| bge-small-en-v1.5 | 0.6667 | 1.0000 | 1.0000 |
| bge-base-en-v1.5 | 0.6667 | 1.0000 | 1.0000 |

Problem: both BGE models false-recover on wrong/similar pages.

## 4. BGE threshold scan

| Threshold | bge-small Top1 | bge-small FalseRec | bge-base Top1 | bge-base FalseRec |
|---|---|---|---|---|
| 0.30 | 0.667 | 1.000 | 0.667 | 1.000 |
| 0.60 | 0.833 | 0.500 | 0.833 | 0.500 |
| 0.85 | 0.667 | 0.000 | 0.667 | 0.000 |

Conclusion: global threshold 0.85 removes false recovery but lowers Top1 to 0.667.

## 5. BGE with rules + per-identity thresholds (Round 2)

24 cases across DeveloperOptions / WifiSettings / NetworkAndInternet / SettingsRoot.
Rules:

1. PreviousVerifiedIdentity conflict rejection
2. Structural type compatibility
3. Per-identity thresholds

| Backend | Top1 | Top3 | Top5 | FalseRecovery | FalsePositive |
|---|---|---|---|---|---|
| bge-small-en-v1.5 | 1.0000 | 1.0000 | 1.0000 | 0.0000 | 0.0000 |
| bge-base-en-v1.5 | 0.9583 | 0.9583 | 0.9583 | 0.0000 | 0.0000 |

Latency:

- bge-small: P50=6.40ms, P95=8.00ms, P99=9.44ms
- bge-base: P50=24.16ms, P95=28.55ms, P99=32.42ms

Conclusion: BGE-small becomes viable for perception-layer semantic retrieval when combined with safety rules.

## 6. Qwen2.5-VL-3B-UI-R1 local test

| Scenario | Qwen output | Evaluation |
|---|---|---|
| DeveloperOptions scroll | `2. Developer` | ✅ Points to DeveloperOptions |
| WifiSettings | `container name is "Wi-Fi"` | ✅ Points to WifiSettings |
| Wrong page | `Data usage` | ✅ No false claim to DeveloperOptions |
| Similar page | `No.` | ✅ Rejects same-container claim |
| Element meaning | `Activate.` | ⚠️ Not stable as deterministic evidence |

Conclusion: qwen is a promising experimental UI semantic assistant, not a
production evidence source yet.

## 7. Configuration hardening

- `SemanticOptions.VectorBackend` defaults to `SemanticVectorBackend.InMemory`.
- `SemanticVectorIndexRegistry` manages backend creation.
- InMemory is the registered production default.
- Future BGE/FAISS/Qdrant/Milvus can be added via `ISemanticVectorIndexFactory`.

Tests: 32/32 PASS.

## 8. Final conclusion

| Layer | Recommended choice |
|---|---|
| Production default | InMemory + rules |
| Real embedding candidate | BGE-small-en-v1.5 (with conflict rejection + per-identity threshold) |
| LLM/VLM experimental | Qwen2.5-VL-3B-UI-R1 (do not wire into Runtime) |
| Framework | No LangChain / HuggingFace formal dependency required yet |

Next step suggestion:

- Larger held-out corpus validation for BGE-small
- Then implement `BgeVectorSemanticIndex : IVectorSemanticIndex` behind the registry
- Keep InMemory as safety fallback
---

## 9. 2026-08-30 — Terminology & Boundary Correction（supersedes §1/§2/§8 layer naming）

> 依据：`PROJECT_LEADER_SEMANTIC_PERCEPTION_PIPELINE_BOUNDARY_AND_SAFETY_REVIEW`
> （`docs/decisions/semantic-perception-pipeline-boundary-review.md` +
> `docs/experiments/semantic-perception-safety-analysis.md`）。历史表格保持原样；
> 本节的职责分类冻结为当前正确模型。

- **BGE-small / BGE-base = Embedding Model**（把 feature text → vector），
  **不是 Vector Backend / IVectorSemanticIndex implementation**。
- **fastembed / ONNX Runtime / HuggingFace / Torch = Model Runtime**
  （Model Execution Infrastructure）。
- **InMemory / FAISS / Qdrant / Milvus = Vector Retrieval Backend**。
- 当前 `InMemoryVectorSemanticIndex` 经代码审计属
  **DETERMINISTIC_REFERENCE_MATCHER**（无 vector/距离，overlap 打分 + 内嵌
  threshold），并作为 profile 判为 `PRODUCTION_SEMANTIC_PROFILE_NOT_QUALIFIED`
  （held-out FR 0.9583）；生产 Runtime 当前未启用该路径（NoOp default）。
- **PIPELINE PROFILE V1**（v1-text-plus-type + BGE-small + prototypes v1 + cosine +
  R1-R4 policy + round-2 thresholds）held-out：
  Top1 0.7500 · FalseRecovery 0.4167 · HardNegativeRejection 0.5833 →
  **未通过 safety qualification**（`BGE_SMALL_SAFETY_NOT_QUALIFIED`；
  主买家 = Candidate Policy，非单模型问题）。
- 未来 qualification 对象 = 完整 `SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2`
  （绑定 FeatureExtractionVersion · EmbeddingModelId/Revision · ModelRuntime ·
  PrototypeVersion · VectorBackend · SimilarityMetric · CandidatePolicyVersion ·
  ThresholdProfile · CorpusVersion），不是单模型。
- 上节 "Next step"（`BgeVectorSemanticIndex` / "InMemory as safety fallback"）
  按本修正重读：前者实为 embedding provider + retriever 组合（须先过
  Responsibility Separation Gate）；后者在 profile 合格前不得标注 "safe fallback"。
