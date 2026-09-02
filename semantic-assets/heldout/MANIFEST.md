# heldout/ — ContainerIdentity-heldout-v1

## Lifecycle status: FORMER_HELDOUT → regression/adversarial evidence source

`ContainerIdentity-heldout-v1` 已完成一次真正的 held-out qualification
（PIPELINE PROFILE V1 → `BGE_SMALL_SAFETY_NOT_QUALIFIED`，2026-08-30），且 failure
已被公开分析（`docs/experiments/semantic-perception-safety-analysis.md`）。

因此登记为：

- **former-heldout**：不得再作为未来最终 held-out qualification dataset；
- **regression / adversarial evidence source**：允许 safety hardening、regression
  benchmark、adversarial benchmark、debugging、profile comparison。

即使未来 Profile V2 在 heldout-v1 上 100% PASS，**也不得**据此宣布 production
qualified。最终 qualification 必须使用新的 **`ContainerIdentity-heldout-v2`**
（未参与 feature design / model selection / prototype design / policy design /
threshold design / hardening）。

（原始登记信息保留如下，作为历史证据。）

## Held-out corpus record (original, 2026-08-30)

| Field | Value |
|---|---|
| Corpus id | `ContainerIdentity-heldout-v1` |
| Corpus version | 1 |
| Case count | 48 |
| Identities | DeveloperOptions (12), WifiSettings (12), NetworkAndInternet (12), SettingsRoot (12) |
| Expected negatives | 24 (D/E/F dimensions) |
| Sources | RealTrace 10, Manual 27, Synthetic 7, Regression 4 |
| Difficulty | Easy 8, Medium 16, Hard 24 |
| Viewport | TitleVisible 9, TitleOffscreen 11, Partial 9, WrongPage 16, Unknown 3 |
| Ambiguity | 0 → 22, 1 → 3, 2 → 19, 3 → 4 |
| Canonical sha256 | `fd415a52511db7190fa26859eb1b11d805fba54f57414b4f60f9389449ed63c5` |

## Coverage contract (per identity × 12)

A x2 Normal (title visible) · B x2 Title-offscreen / scroll · C x2 Partial ·
D x1 Low-information (expect abstain) · E x2 Similar-page interference
(expect abstain) · F x3 Hard negative:
- F1 wrong container (Regression family)
- F2 visually/text-overlap similar container (adversarial)
- F3 insufficient evidence / empty or near-empty query

> 2026-08-30 lifecycle note: above case table is the original v1 record. The
> corpus now serves as regression/adversarial evidence (see top of this file);
> it is NOT a future held-out qualification dataset.

## Assets

| Asset | Path |
|---|---|
| Canonical corpus JSON | `ContainerIdentity-heldout-v1.json` |
| BGE-small Profile V1 report (historical failure record) | `reports/container-identity-heldout-v1-bge-small-profile-v1.json` |
| BGE-small **Profile V2** report (SEMANTIC_SAFETY_HARDENING_APPLY: FR 0.4167→0, IE 3/7→0, HNR 1.0 — REGRESSION_SAFETY_RECOVERED, NOT qualified) | `reports/container-identity-heldout-v1-bge-small-profile-v2.json` |
| Margin parameter scan (safety-first selection, margin=0.05) | `reports/margin-scan-profile-v2.json` |
| Similarity / margin / magnet separation analysis | `reports/similarity-separation-analysis.json` |
| InMemory production-default report (committed run) | `reports/container-identity-heldout-v1-inmemory-profile-v1.json` |

## Grounding

- RealTrace cases are verbatim or contiguous-subset observations from committed
  capture evidence: `openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/artifacts/uitars-bench/truth.json`
  (root-top / root-scrolled / accessibility / display-child) and the Wi-Fi /
  DeveloperOptions / wrong-page traces recorded in
  `docs/experiments/qwen2.5-vl-local-preview.md`.
- Manual cases are independently authored from real Android Settings
  vocabulary (optionally derived from a real trace with edits).
- Synthetic cases are adversarial constructions; Regression cases are new
  variants of documented historical failure families (never verbatim tuning
  cases).

## Tuning exclusion

This corpus is excluded from every tuning/benchmark helper selection
(category `Experimental` in the C# corpus model) and is only ever consumed by
validation tooling. Proof: `HeldOutValidationTests.T1` (ids + fingerprints),
`T0_TuningCorpusShapeIsStable`.