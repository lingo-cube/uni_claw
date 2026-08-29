# C. ScenarioKnowledgeFixture Contract — Acceptance Evidence

## Leader's independent verification

- Build: `dotnet build src/UniClaw.Runtime.sln` → 0 errors.
- Tests (re-run by leader, not trusted from worker): `ScenarioKnowledgeFixtureTests` → **35/35 passed**.
- Purity: `git status` — only new files under `src/UniClaw.Runtime.ValidationHarness/Knowledge/`
  and `tests/UniClaw.Runtime.Tests/ValidationHarness/ScenarioKnowledgeFixtureTests.cs`.
  Zero edits to Runtime production paths (verified against the baseline manifest; the only
  working-tree diffs there are the two pre-existing ones recorded in
  `A-human-gate-and-baseline.md`).

## Worker WorkResult (module-worker-c, verbatim summary)

FILES_CREATED:
- `Knowledge/KnowledgeType.cs` — closed enum, exactly the seven graduated types, no 8th word.
- `Knowledge/KnowledgeStatus.cs` — Active/Stale/Contradicted/Superseded/Invalidated.
- `Knowledge/KnowledgeScope.cs` — value record (ScenarioId, ApplicationPackage,
  SemanticCapabilityId, SemanticCapabilityVersion, AndroidAssumptions, Locale,
  CreatedFromRunIds); `Matches()` = scenario+app+cap id+version+locale+android; run set
  excluded from match.
- `Knowledge/KnowledgeAdmissionSource.cs` — ObservedResult + 7 forbidden markers
  (Guesswork/HardcodedTextAsTruth/Coordinates/FixedPath/SelectorScript/ProbeByExecution/
  RuntimeInternalAssumption).
- `Knowledge/ScenarioKnowledgeRecord.cs` — full field contract + AdmissionOrdinal
  (deterministic, no DateTime); RecordId = SHA-256 over canonical content EXCLUDING
  lifecycle-only fields and the ordinal → identity stable under downgrade; `WithStatus`.
- `Knowledge/KnowledgeAdmission.cs` — Admitted/Rejected + `TryAdmit` gate: provenance
  (SourceRunId + ≥1 EvidenceRefs), source==ObservedResult, anchor, confidence ∈ [0,1],
  complete scope, defined vocabularies, Version ≥ 1.
- `Knowledge/FreshEvidenceOutcome.cs` — Contradicts/Supersedes/Invalidates/Stales factories.
- `Knowledge/ScenarioKnowledgeFixture.cs` — scope-bound store: admission gate re-run +
  scope binding + duplicate-content rejection; `ActiveKnowledge(scope)`; immutable
  `ApplyFreshEvidence` (new instance, history intact); `LifecycleStatistics()`; NO
  force-apply / re-activation API (absence is the guarantee; reflection test bakes it in).

DESIGN_NOTES (worker):
1. RecordId excludes lifecycle fields so downgrades keep identity → diffable freezing;
   Supersedes/SupersededBy pairs always resolvable.
2. Fresh-wins enforced at two layers: stateless gate + store-level duplicate rejection.
3. AdmissionOrdinal replaces CreatedAtUtc; zero DateTime in record identity.
4. XML docs cite spec requirement names + design D2/D3/D4.

DEVIATIONS: none. BLOCKED: none.

## Spec scenario coverage

| Spec scenario | Test evidence |
|---|---|
| Provenance-gated admission | reject-no-run-id / reject-no-evidence-refs cases |
| Forbidden knowledge sources rejected | 7 marker classes each rejected with echo |
| Human-readable persisted asset | (D-group freeze renders these records) |
| fresh-evidence-wins conflict | 4 outcome classes; old never force-applied |
| scope isolation | per-field mismatch cases |
