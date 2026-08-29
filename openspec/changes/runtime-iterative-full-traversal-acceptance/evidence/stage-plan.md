# Stage G/H/I Campaign Plan (leader-authored, upper-agent role)

The upper agent in this campaign is the validation harness's planning layer: the leader
(human-gated orchestrator) authors each round's `StrategyDirective` — informed ONLY by
prior rounds' frozen read-only evidence and the ScenarioKnowledgeFixture — and the
`IterativeCampaignRunner` executes each round as an independent Runtime Run. PlanDelta
records are validated by the closed-vocabulary validator.

## Composition (Stage G entry)

- Host: TierAHost over a REAL Settings RunGraphFactory:
  `PhysicalHostComposition.CreateAndroidRunGraphFactory` shape, but the RunGraph must be
  composed with the harness-local Settings binding environment:
  `PhysicalEnvironment(AdbScreenshotSource(emulator-5554), LocalVisionPerceptionSource(vision.SocketPath), AdbDispatchTarget(...), "com.android.settings", 1080, 1920)`
  wrapped by `SettingsBindingComposition.Wrap` (production SettingsSemanticCapability),
  `resolveSemanticPage = SettingsStrategyBinding.ResolveSemanticPage`,
  `launchIntentAction = "android.settings.SETTINGS"`.
- Strategy compiler: `StrategyContractCompiler([new SettingsStrategyBinding()])`.
- Executor: campaign executor over the real host (Tier-B style, same seam as
  `CampaignRunExecutors.TierA`); read surface = `WireReadSurface(host.BoundPort)`.
- Vision: `CanonicalVisionHostFactory.Create(platforms/perception/governance/artifacts/current-active-identity.json, python: .venv-local-vision/bin/python)`.
- Scenario scope id: `settings-emulator-35-v1` (KnowledgeScope: scenarioId
  `settings-bounded-traversal`, app `com.android.settings`, capability
  `uni-claw.settings.semantic` v1, android `api-35/scroll-test/arm64-v8a`, locale `en-US`).

## Round strategy ladder (conservative → mature)

- **R1 (Stage A, conservative)**: depth 1, ExploreScope, constraints = NavigableContainer
  only, prohibited = {StateMutation, ExternalBoundaryCrossing}, completion =
  ExhaustiveCoverageWithinScope. Posture UNPROVEN_SAFE → record-only. Expected: root
  inventory + record-only descent of one level; knowledge = root container rows typed.
- **R2+ (Stage B, ≥3 adaptations)**: each round's PlanDelta must be causally tied to a
  knowledge record from the previous round's evidence, e.g.:
  - KnownRecordOnly for observed leaf classes → dispatch-surface exclusion evidence
    (subcategory constraints remain fixed at NavigableContainer; the visible lever is
    scope root / depth / objective kind);
  - KnownLocalControl for toggle-shaped rows → constraint evidence (not navigable);
  - KnownExternalBoundary (e.g. rows that leave Settings) → boundary knowledge;
  - depth increase (1 → 2 → 3) once child-container knowledge is ACTIVE;
  - scope root change to a discovered sub-container once its page-identity knowledge is ACTIVE.
- **Stage C (reuse)**: freeze fixture v1 → clean emulator (restart) → fresh campaign
  loads v1 → initial plan depth 3 from round 1 with all known-danger classes excluded
  from the start; verify stale/contradicted handling with one engineered conflict.

## Per-round evidence → knowledge flow

Result (ValidationResult + call log + report) → Interpretation (leader, citing
EvidenceRefs from the round's own result/evidence sections) → KnowledgeAdmission.TryAdmit
(ObservedResult source only) → fixture.Admit. Conflicts → ApplyFreshEvidence.

## RestartRequiredAdvisoryCase capture

Any round that terminates fail-closed/terminal where post-hoc evidence analysis shows a
single constrained UniAgent advisory checkpoint (e.g. "is this unknown row a settings
subpage?") could have continued the run without restart → record the case with
SourceRunId, EvidenceRefs, terminal reason, uncertainty type, hypothetical question,
allowable answer type, why Runtime could not decide alone, whether restart was required.
