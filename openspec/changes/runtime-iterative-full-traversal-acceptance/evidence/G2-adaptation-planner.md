# G2. Evidence-Informed Adaptation Planner — Acceptance Evidence

## Leader's independent verification

- Build 0 errors; `SettingsAdaptationPlannerTests` re-run by leader → **20/20**.
- `HarnessSourceShapeGuardTests` 5/5 (token/mutation/scenario guards scan the new files).
- `git diff -- src/UniClaw.Runtime/` empty (zero Runtime changes).
- Only new files under `SettingsCampaign/Adaptation/` + the test file.

## Worker WorkResult (module-worker-g2) — accepted summary

- `RoundKnowledgeExtractor`: 4 typed conservative extraction rules (Completed→KnownContainer;
  normalization-unresolved→KnownUnresolved; depth-boundary→KnownRecordOnly; launch/foreground
  →KnownUnresolved@settings-entry); deterministic RunId-derived evidence locators
  (universe + candidates share format constants → resolution-by-construction); every
  candidate through `KnowledgeAdmission.TryAdmit` — rejected returned, never forced.
- `AdaptationPlannerRules`: closed data-driven first-match table; safety-first (unresolved
  root blocks depth increase; prohibitions only grow; depth rises only on FRESH
  root-exhaustion knowledge, capped at 3; stale knowledge never drives a delta — only the
  mutating/external safety rule persists across rounds, per spec).
- `SettingsAdaptationPlanner`: composes extract→admit→rules→PlanningRound→PlanDeltaValidator;
  Rejected validation = internal error (throws — no silent illegal record); termination
  BoundedScopeExhaustion on budget or mature plan.
- Tests: ≥3 legal adaptation rounds through the real IterativeCampaignRunner (4 Accepted
  PlanningRounds incl. NO_OP_WITH_REASON cases), negative controls (unknown refs), forbidden
  sources, provenance gate, stale-never-drives-delta, round independence, no
  action/coordinate/selector content.

DEVIATIONS: none material. BLOCKED: none — no CONTRACT_GAP (eight freedoms sufficed).
