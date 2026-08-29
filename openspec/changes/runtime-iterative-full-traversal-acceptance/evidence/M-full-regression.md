# M. Full Regression — Evidence (2026-08-27, session-final)

All numbers from THIS session's runs (no reused numbers).

## Addendum (later same day): current-tree state after the perception-repair candidate work

The working tree NOW carries the `perception-navigation-row-composition-repair`
change's uncommitted candidate diffs, including TWO files inside this change's
baseline-manifest scope:

- `src/UniClaw.Runtime/World/SourceEquivalenceNormalizer.cs` — **Runtime production**:
  `ExtractNavigationSignatures` now skips auxiliary-tier canonical occurrences when an
  explicitly correlated primary Vision source exists (primary-only signature sequence).
- `src/UniClaw.Semantic.Settings/SettingsSemanticCapability.cs` — production capability:
  duplicate same-text overlapping primary renderings disposed NonInteractive; visual
  search-hint recognition.

Facts recorded per Human Gate #2 ruling (2026-08-27, disposition
`RETAIN_AS_RUNTIME_OWNED_CONTRACT_CONFORMANCE_REPAIR`):

1. **Scope of this change's `0/216` claim (corrected per ruling)**: it means ONLY that
   the Phase 2.6 campaign itself made zero edits to the 216 manifest files (verified at
   session close). It is NOT a claim that the working tree contains no Runtime changes.
2. **Normalizer diff ownership (reclassified per ruling)**:
   `src/UniClaw.Runtime/World/SourceEquivalenceNormalizer.cs` is a **Runtime-owned
   contract-conformance repair** — Owner `RuntimeAgent / World normalization`,
   `AuthorityDelta: NONE`, **`RuntimeBehaviorDelta: PRESENT`**, `ArchitectureDelta:
   NONE`. It blocks non-authorization-eligible auxiliary occurrences from the
   completeness identity sequence when an explicit Primary Vision source exists; it
   does NOT transfer authority and must NOT be cited as tolerating duplicate visual
   menu items. Runtime precision-overlap, uniqueness, and fail-closed rules are
   unchanged. Targeted tests re-verified 7/7 PASS.
3. `src/UniClaw.Semantic.Settings/SettingsSemanticCapability.cs` diff is owned by the
   perception repair change (semantic capability layer).
4. Current-tree verification (this addendum's run): build 0 errors; Phase 2.6 harness
   area 154/154; full suite 2215/2220 — failures: 2 known environmental RealDevice
   tests + 3 Vision factory artifact-replacement tests (order-dependent, matching the
   repair's own report) + 1 coordinator exclusivity test that PASSES in isolation
   (order pollution). Strict validation + consistency + `git diff --check` all green.


| Check | Command | Result |
|---|---|---|
| Build | `dotnet build src/UniClaw.Runtime.sln` | 0 errors |
| Runtime deterministic full suite | `dotnet test src/UniClaw.Runtime.sln` | **2213 / 2215 passed**; 2 failures = `[Collection("RealDevice")]` environmental tests (`Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete`, `ExternalBoundary_RealDevice`) requiring the `com.uniclaw.fixture` capstone app/setup on the emulator — pre-existing throughout the session (same 2 failures observed before any campaign ran), unrelated to this change (physical/real-app fixtures, not touched) |
| Semantic suite | `dotnet test tests/Semantic` | 32/32 passed |
| Architecture guards | included in full suite (ArchitectureGuardTests + HarnessSourceShapeGuardTests) | green (source-shape guards scan ALL new harness files incl. Campaign/Knowledge/PlanDelta/SettingsBinding/SettingsCampaign) |
| New capability tests | CampaignRunner 8/8 · ScenarioKnowledgeFixture 35/35 · ScenarioKnowledgeStore 9/9 · PlanDeltaRecorder 19/19 · SettingsStrategyBinding 6/6 · SettingsAdaptationPlanner 20/20 | **97/97** |
| Consistency | `scripts/check-consistency.sh` | ALL PASS (C1–C12) |
| Whitespace | `git diff --check` | CLEAN |
| Runtime production byte-identity | `shasum -c evidence/runtime-baseline-manifest.sha256` (216 files) | **0 deviations** |
| OpenSpec strict validation | `openspec validate --changes` | 18 passed, 0 failed |
| Agent workflow validators | `tools/agent_profile_validator.py validate` | AGENT_WORKFLOW_VALIDATION_PASS |
| AgentWorkflow python tests | `unittest discover tests/AgentWorkflow` | 29 failures/errors — **PRE-EXISTING at HEAD e6c6f4b** (verified via clean stash: same failures without any session change; "source revision drift pinned e2d8dd4 ≠ current e6c6f4b" family) — unrelated to this change |

## Runtime byte-identity statement

- All 216 Runtime production files (`src/UniClaw.Runtime/**`, Adapters, DriverHost, Harness,
  Semantic.*) byte-identical to the session-start manifest.
- The only tracked-file edits this session: `TierBProgram.cs` (+settingscampaign route,
  harness project), `UniClaw.Runtime.ValidationHarness.csproj` (+1 ProjectReference to
  UniClaw.Semantic.Settings — harness composition, design D6), `tasks.md` (progress),
  `.dsh/profile-adapter/state/events.jsonl` (session telemetry).
- All new code: harness (`Campaign/`, `Knowledge/`, `PlanDelta/`, `SettingsBinding/`,
  `SettingsCampaign/`) + tests — validation tooling only.
- Archived Phase 2/2.5 bundles and the Phase 3 draft: untouched.
