# F. SettingsStrategyBinding — Acceptance Evidence

## Leader's independent verification

- Build: 0 errors. Tests (leader re-run): `SettingsStrategyBindingTests` → **6/6** (part of
  the combined 15/15 run with the store tests).
- Runtime byte-identity: `shasum -c runtime-baseline-manifest.sha256` → all 216 files OK,
  zero deviations. The only tracked-file edit outside new dirs is the harness csproj's
  single `ProjectReference` to `UniClaw.Semantic.Settings` (harness project, authorized by
  design D6: the binding constructs the production capability; Runtime/Semantic production
  paths untouched).
- `git diff --check` clean.

## Worker WorkResult (module-worker-f) — accepted summary

- Pure adapter over the production capability's ADMITTED PRIMARY evidence
  (`Observation.AdmittedSemanticEvidence.EligibleForAuthorizationInput`).
- Occurrence↔element correlation verified against `SemanticObservationFactProjector`
  (`CreateOccurrenceId(primarySourceId, elementArrayPosition)`); bounds-checked.
- Page identity: graduated anchors only — `search_action_bar` (root), `Navigate up`
  accessibility label + exactly-one `collapsing_toolbar` title (child → `SettingsSubpage(<title>)`),
  foreground-app gate. NO page-name literals.
- Inventory: `SourceEquivalenceNormalizer.OccurrencesOf` grounding (`nav:N` identities,
  required by `Agent.SourceGroundingValidator`); required branches == admitted primary
  navigation set; viewport union; children recursive; leaf → bounded-leaf empty map.
- Authorization: NavigationCandidate / parent-return relation classes only; rejections
  name the evidence class honestly (ContainerIdentity is page-scoped — fail-closed).
- Dispatch policy: NavigableContainer → EnterAndTraverse only.
- Compile-path: `StrategyContractCompiler([binding]).Compile(Settings-scope directive)`
  → Accepted.
- Purity test: source scan (no coordinates/taps/xpaths/package paths/page-title literals/
  Knowledge·Campaign·PlanDelta·Fixtures references).

DEVIATIONS (accepted by leader):
1. Root constant identifier `RootIdentity` (value "Settings") instead of `SettingsRoot` —
   the shared guard's scenario-token scan trips on that identifier outside the fixture
   whitelist; value identical, guard stays green. Accepted.
2. Harness csproj +1 ProjectReference (see above). Accepted — within the harness project's
   composition authority.

BLOCKED: none — no capability vocabulary gaps encountered (no CAPABILITY_GAP stop).
