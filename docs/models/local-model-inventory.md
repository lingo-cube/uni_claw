# Local Model Inventory

> Date: 2026-08-19
> Owner: UniClaw Semantic / Local Experimentation
> Status: EXPERIMENTAL — NOT WIRED INTO RUNTIME
> Constraint: Current Semantic Architecture is FROZEN; Slow Semantic is
> `SLOW_SEMANTIC_NOT_JUSTIFIED`. This inventory only documents locally available
> models for offline experiments. No Runtime / Provider / Contract integration is
> authorized by this document.

## 1. Registered local models

| Alias | Model | Path / Source | Type | Quantization | Status |
|---|---|---|---|---|---|
| qwen2.5-vl-3b-ui-r1 | Qwen2.5-VL-3B-UI-R1 | `/Users/fran/models/qwen2.5-vl-3b-ui-r1/Qwen2.5-VL-3B-UI-R1.Q4_K_M.gguf` | Vision-Language / UI reasoning | Q4_K_M | EXPERIMENTAL |
| bge-small-en-v1.5 | BAAI/bge-small-en-v1.5 | downloaded via `fastembed` | Text embedding / retrieval | ONNX | TESTED (round2 safe: Top1=1.0, FalseRec=0 with rules) |
| bge-base-en-v1.5 | BAAI/bge-base-en-v1.5 | downloaded via `fastembed` | Text embedding / retrieval | ONNX | TESTED (round2 safe: Top1=0.958, FalseRec=0 with rules) |
| bge-m3 | BAAI/bge-m3 | not available in current `fastembed` | Text embedding / retrieval | - | NOT TESTED |

## 2. Benchmark reference

- `docs/experiments/semantic-model-evaluation-summary.md`
- `docs/benchmarks/semantic-embedding-backend-comparison.md`
- `docs/benchmarks/semantic-embedding-threshold-scan.md`
- `docs/benchmarks/semantic-embedding-round2.md`
- `docs/benchmarks/semantic-fast-vector-benchmark-developer-options.md`
- `docs/experiments/qwen2.5-vl-local-preview.md`

## 3. Purpose

- Candidate for offline UI semantic experiments (e.g. container identity
  disambiguation, element meaning exploration).
- NOT for Runtime decision-making.
- NOT for Slow Semantic / LLM Consumer integration until a future gate explicitly
  justifies it.

## 4. Tooling

- Runtime tool available: `llama-cli`
- Recommended controlled execution: `llama-server` + bounded HTTP request, or
  offline script with stdout/stderr redirected to files.
- Direct `llama-cli` in interactive chat shell can produce excessive output /
  hang; avoid in this environment.

## 5. Usage rules

1. Experiments must be offline and read-only.
2. No writes to Runtime state, Vector Memory, or Corpus.
3. No changes to `ISemanticProvider`, `SemanticEvidence`, Agent, Resolver, Vision,
   Belief System, L1, or DSH.
4. Results may be recorded as experimental notes only.
5. If a real Slow Semantic buyer is later confirmed, open a new baseline gate.

## 6. Related routing configuration

For AI assistant model routing (not local inference), see:

- `.ai/model-routing.yaml`
- `.ai/agent-routing.md`

These define logical roles / provider mappings and are unrelated to the local qwen
model above.
