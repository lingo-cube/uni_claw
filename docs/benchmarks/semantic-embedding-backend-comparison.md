# Semantic Embedding Backend Comparison

> Date: 2026-08-19
> Scope: Experimental offline comparison of candidate embedding models for
> Fast Semantic UI Container Identity retrieval.
> Constraint: Read-only evaluation only. No Runtime / Provider / Contract wiring.

## Method

- Corpus: DeveloperOptions-v1 style A–E + adversarial similar-page case (6 cases).
- Query: text + element type summary of visible elements.
- Prototypes: DeveloperOptions, WifiSettings, NetworkAndInternet, SettingsRoot.
- Similarity: cosine similarity.
- Candidate selection: best prototype if similarity >= 0.30.
- Runtime tool: `fastembed` + ONNX.

## Results

| Backend | Top1 | Top3 | Top5 | FalseRecovery | FalsePositive | MeanConf | CalErr | P50 | P95 | P99 |
|---|---|---|---|---|---|---|---|---|---|---|
| InMemoryVectorSemanticIndex (baseline) | 1.0000 | 1.0000 | 1.0000 | 0.0000 | 0.0000 | 0.2667 | 0.7333 | 0.0044ms | 1.0405ms | 1.2451ms |
| BAAI/bge-small-en-v1.5 | 0.6667 | 0.6667 | 0.6667 | 1.0000 | 1.0000 | 0.8085 | 0.1418 | 7.5533ms | 7.9210ms | 7.9210ms |
| BAAI/bge-base-en-v1.5 | 0.6667 | 0.6667 | 0.6667 | 1.0000 | 1.0000 | 0.7909 | 0.1242 | 23.6046ms | 25.3535ms | 25.3535ms |
| BAAI/bge-m3 | NOT TESTED | - | - | - | - | - | - | - | - | - |

## Observations

- BGE models retrieve positive DeveloperOptions cases correctly (A/B/C/E).
- Both BGE models falsely recover on negative cases (`dev-D`, `adv-sim`) at the
  current 0.30 threshold, producing FalseRecovery=1.0000.
- BGE requires threshold calibration / negative-case tuning; it is not
  plug-and-play with the current simple prototype matching.
- BGE-M3 is not supported by the installed `fastembed` version in this
  environment and was not tested.

## Conclusion

- InMemory baseline remains the current controlled benchmark baseline.
- BGE-small / BGE-base are promising but need calibration and a better decision
  policy before they can replace InMemory for safety-sensitive recovery.
- Future work: threshold search, negative examples, possibly classifier/reranker.