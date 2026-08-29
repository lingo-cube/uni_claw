# I (partial). Fixture v1 Freeze — Real Campaign Knowledge Asset

## What

Frozen `validation/knowledge/settings/settings-bounded-traversal/v1/` from the archived
Stage-A+B campaign evidence (`G-stage-a/stageAB-adaptive-campaign.json`):

- 3 × KnownUnresolved `settings-root-inventory` records (runs 1–3), ACTIVE, conf 0.7,
  disposition "record-only; requires upper-agent replan".
- Scope: scenario `settings-bounded-traversal` · app `com.android.settings` · capability
  `uni-claw.settings.semantic` v1 · android `android-35/p26_pixel/arm64-v8a/emulator` ·
  locale `en-US` · createdFromRuns run-1..run-4.
- Artifacts: `records.json` (canonical 15-field order) + `manifest.json` (content SHA) +
  `FIXTURE.md` (human digest + lifecycle statistics).

## How (tool: `fixturefreeze`, harness-side)

- Reads the ARCHIVED campaign outcome JSON (evidence artifact, not a re-run).
- Reconstructs records with the extractor's own constants (ValidityAssumption "stable
  across frames", Version 1, ordinal — mirrored from `RoundKnowledgeExtractor` source;
  the campaign report serializes content fields only).
- Re-validates EVERY record through the real `KnowledgeAdmission` gate
  (ObservedResult source): admitted 3/3, rejected 0.
- Freezes via the graduated `ScenarioKnowledgeStore` (design D2).

## Verification

- Determinism: second freeze to a temp root → **byte-identical** (records.json,
  manifest.json, FIXTURE.md).
- Content: human-readable, scope-explicit, provenance-bearing (per-record SourceRunId +
  EvidenceRefs; manifest createdFromRuns).
- Freeze half of task I.1 complete; the clean-emulator reuse campaign remains blocked
  by IR-G0 (see STOP report).

## Forward value for re-entry

When the perception repair lands and a re-entry campaign observes root normalization
RESOLVING, the fresh evidence will CONTRADICT these frozen ACTIVE records — exercising
I.2's fresh-evidence-wins requirement on REAL data (the engineered conflict case is
pre-seeded by reality, not fabricated).
