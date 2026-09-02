# R0-R2 Core Model Stage Result

Date: 2026-08-31

## STATUS

`CONTAINER_RUNTIME_V2_CORE_PURCHASE_IN_PROGRESS`

The previous ContainerGraph purchase is frozen and the first reversible Apply slice passed the Leader Gate. This result does not authorize Agent integration, a provider/backend, lifecycle graduation, archive, or production authority.

## PURCHASED

- `CurrentContainer = NodeRef + CurrentSliceRef + EntryContext` as a thin physical working-location projection.
- Immutable `TransitionOccurrence` as evidence of what physically occurred, separate from expectation, relation, and trust.
- `INITIALIZED` working nodes without proven semantic identity.
- Source/entry-affordance/destination relation evidence where same destination does not collapse distinct relations.
- Explicit normal-relation eligibility; abnormal/off-path occurrence evidence does not automatically become a reusable relation.
- Monotonic fresh evidence revision and validate-before-replace atomicity.

## HYPOTHESIS

- A Run-local evidence-only ContainerGraph remains an architecture hypothesis until multi-entry reuse and fresh-conflict validation demonstrate benefit.
- Fast derived trust and Slow Shadow semantic assessment remain bounded hypotheses and have no production authority in this stage.

## IMPLEMENTED

- `src/UniClaw.Runtime/Model/ContainerRuntimeV2.cs`: immutable refs, node/relation/Slice/EntryContext/CurrentContainer/occurrence/Graph/state contracts and pure reducer.
- `tests/UniClaw.Runtime.Tests/Unit/ContainerRuntimeV2CoreModelTests.cs`: deterministic semantic and atomic rejection coverage.
- `tests/UniClaw.Runtime.Tests/Architecture/ContainerRuntimeV2CoreArchitectureGuardTests.cs`: thin-current, evidence-only Graph, immutable surface, pure reducer, and authority-boundary guards.
- `NEW_SYMBOL_JUSTIFICATION`: existing `Container` requires known `SemanticPageName`; old `ContainerTransition` mixes disposition-shaped compatibility semantics; DriverHost `RunExecutionGraph` is an authority-free execution projection. None can own the V2 evidence aggregate without breaking responsibility.

## VALIDATED

- Leader inspected production and test code and rejected the first Worker result for three semantic gaps: per-occurrence trigger as relation identity, incomplete transition current replacement, and a weak immutable-collection guard. The same Worker repaired the original bounded scope.
- `dotnet test tests/UniClaw.Runtime.Tests/UniClaw.Runtime.Tests.csproj --filter 'FullyQualifiedName~ContainerRuntimeV2Core|FullyQualifiedName~ContainerTransition' --no-restore`: 46 passed, 0 failed.
- `dotnet build src/UniClaw.Runtime/UniClaw.Runtime.csproj --no-restore -t:Rebuild -v:minimal`: 0 errors; no warning originates from `ContainerRuntimeV2.cs`. Existing repository warnings remain outside this stage.
- `git diff --check`: passed.
- Worker write scope remained the three authorized files; no Agent, Container, DriverHost, provider, or behavior integration changed.
- `AuthorityDelta = NONE`; `BehaviorDelta = NONE`; `NET_NEW_MUTABLE_TRUTH = 0`.

## DEFERRED

- Agent-owned mutable slot and compatibility migration from `ActiveContainerContext`.
- Graph consumer interfaces or a stateful service; the immutable snapshot/reducer must first be tested as the minimum seam.
- Slice/LocalModel integration, coverage projection, Fast resolver, Slow Advisor, semantic correction consumption, and real-device validation.
- Relation assessment maturity, checkpoint lifecycle, cross-Run graph memory, planner, recovery FSM, and concrete semantic backend.

## RISKS

- Existing `ActiveExecutionContainer` can still be misread as the physical current Container until the migration stage.
- Existing run-global semantic `visited` behavior still conflicts with legal same-node/different-relation entry.
- The repository remains dirty and contains unrelated changes and pre-existing warnings; later stages must repeat overlap checks.
- Public relation values can describe candidate support, but only the reducer currently enforces structural admission. A later writer must not create a second mutable Graph owner.

## NEXT_WORKITEM

Reconcile the immutable snapshot/reducer against the requested Graph read/record responsibilities. Prefer the existing value/reducer contracts when they already provide the seam; create a new interface only if an independent consumer or replacement-test buyer cannot be served without it. Add derived non-authoritative relation assessment and focused multi-entry/fresh-conflict evidence without integrating Agent behavior.

## Documentation synchronization

| Target | Result | Reason |
|---|---|---|
| Canonical active OpenSpec | UPDATE | Tasks 2.1-2.5 now have implementation and verification evidence. |
| Runtime layer/pattern docs | NO_CHANGE | This slice is a passive Model contract with no production consumer or behavior change. |
| Current-state projections | UPDATE | The active change was already regenerated into current gates/snapshot before Apply. |
| Decision receipt | NO_CHANGE | The previous-purchase freeze decision already records the supersession boundary. |
| Main specs / archive | DEFER | Active change is not graduated or archived. |
