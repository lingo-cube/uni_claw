# BGE-small Held-out Validation Runner

Gate: `PROJECT_LEADER_BGE_SMALL_HELD_OUT_VALIDATION`

Runs the FROZEN profile `BGE_SMALL_CONTAINER_IDENTITY_PROFILE_V1`
(`semantic-assets/profiles/`) over the held-out corpus
`ContainerIdentity-heldout-v1` (`semantic-assets/heldout/`) and writes the
committed report to
`semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v1.json`.

## Run

```bash
uv run --with fastembed python validation/semantic/bge-held-out/run_held_out.py
```

Requires network on first run (fastembed + BAAI/bge-small-en-v1.5 download,
cached in `/tmp/bge-cache`).

## Constraints (enforced by design, not by will)

- The script reads every threshold / prototype / feature-extraction / rule from
  the frozen profile JSON. It contains no tuning code and no held-out feedback.
- Running it never mutates the profile or the corpus.
- Metric formulas are identical to the C# side
  (`tests/Semantic/BenchmarkTests/HeldOutValidationTests`): top1 accuracy,
  top3/top5 recall (admitted ranking), false recovery == any accepted claim on
  an expected-None case, false positive == any emitted claim on an expected-None
  case, hard-negative rejection rate, abstention correctness, P50/P95/P99 of
  per-case decision latency.

## Pipeline (frozen order)

1. Feature extraction `v1-text-plus-type` (per-element `text (type)`).
2. R4: no text + no types + no structural features -> ABSTAIN.
3. Embed query + the 4 frozen prototype texts (cosine).
4. R1: structural type overlap filter.
5. R2: PreviousVerifiedIdentity conflict rejection (fail-closed).
6. R3: per-identity threshold acceptance.

## Failure classification

Deterministic rules map every miss to the gate taxonomy
(EMBEDDING_SEPARATION_FAILURE / FEATURE_REPRESENTATION_FAILURE /
THRESHOLD_GENERALIZATION_FAILURE / STRUCTURAL_RULE_FAILURE /
IDENTITY_PROTOTYPE_FAILURE / CORPUS_DEFECT / UNKNOWN) — see `classify_failure`
and the benchmark report.