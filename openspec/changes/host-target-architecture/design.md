## Context

`src/UniClaw.Host/` was built as a single `locate_one_item` vertical slice. To ship fast it bypassed Core's locked seams and re-implemented capabilities that belong in Core. Four conflicts were found:

- **C1** — Host casts `IScreenStateProvider` back to concrete `AdbScreenStateProvider` (`HostCommands.cs:504`) to reach `RefreshAsync`, which returns the Device-only `AdbScreenStateResult`. Host depends on concrete classes, not the seam.
- **C2** — Host defines `DeterministicSettingsModelProvider` (`HostCommands.cs:668`) instead of using Core's `MockModelProvider`; the Core mock lacks vision replay, the Host mock is Settings-specific and not reusable. Two mocks, neither right.
- **C3** — `EntryPolicyExecutor` is `new`-ed (`IncrementalScenarioRunner.cs:521`) with a Host-wrapped driver instead of injecting `IEntryPolicyExecutor`.
- **C4** — Two observation paths produce structurally different `PageAnalysis`: the AI path fills `Level1Menus`/`Level2Menus`/`Items`; the UIAutomator path fills only `Items`/`CurrentPath`/`HasScroll`/`IsEndOfList` and hardcodes `Direction.Left` (`ScenarioObservation.cs:181-186`). Same record, different shape.

Root cause: Host assembles AI capabilities by grabbing low-level components (`IModelProvider`, raw `IPageAnalyzer`) instead of the `IUniBrain` facade, and re-implements missing capabilities locally rather than lifting them into Core. This is why the layer resists control and why the active `deliver-safe-android-settings-test-loop` change's tasks 8/9 (E2E tests, stability gates) cannot be honest until the seams are fixed.

Constraints:
- The locked `IScreenStateProvider` 4-method lock (guarded by `ArchitectureGuardTests.cs:818`) is untouched — new seams are extension interfaces.
- The 24 spec requirements of `deliver-safe-android-settings-test-loop` are not changed; this is a structural redesign of assembly seams.
- Existing 930+ tests preserved; each migration step independently verifiable.
- No locked enum changes; no reverse-dependency violations (Core gains extension interfaces; the spec forbids only reverse refs).

Stakeholders: the active `deliver-safe-android-settings-test-loop` change consumes the fixed seams; `enumerate_first_level` (task 8) and stability gates (task 9) depend on C1/C2/C4 being resolved.

## Goals / Non-Goals

**Goals:**
- Make Host a composition root: configure + assemble Core components, never implement providers.
- Lift missing capabilities into Core (`IObservableScreenStateProvider` + `ScreenStateResult`, `MockModelProvider` vision replay, `UniBrainFactory`, `PageAnalysis` shape contract).
- Assemble AI capabilities through `IUniBrain` config-driven via `UniBrainFactory`, not by hand-`new`-ing sub-interfaces.
- Resolve C1–C4 and record the D6 runner/engine decision.
- Add a structural guard preventing regression to the bypass pattern.
- Keep the locked 4-method guard green at every step.

**Non-Goals:**
- `enumerate_first_level` runner (task 8) — separate OpenSpec change; this design only fixes seams so it can plug in.
- Spec defects D1/D2/D3 — parallel spec amendment under the active change, not here. D4 is supported by the shape contract; D6 is recorded here.
- Phase 3 behavior (G4), advanced scroll metrics (G5) — deferred.
- Re-specifying the 24 active-change requirements — preserved, not in scope.

## Decisions

### D1 — Extension interface, not lock mutation (resolves C1)
**Decision:** Add `IObservableScreenStateProvider : IScreenStateProvider` in Core with one new method `RefreshAsync`; lift `ScreenStateResult` into Core (replaces Device-only `AdbScreenStateResult`). `AdbScreenStateProvider` implements the new interface; its 4 locked methods unchanged. Host programs against `IObservableScreenStateProvider`; the cast is deleted.
**Result-type mapping (resolved 2026-07-30, option 1 — replace):** `AdbScreenStateProvider.RefreshAsync` SHALL change its return type from `AdbScreenStateResult` (Device) to Core `ScreenStateResult`. `ScreenStateResult` is a Core sealed record with fields `Succeeded` / `Status` / `HierarchyXml` / `HierarchyFingerprint` / `HasScroll` / `IsEndOfList` / `Failure` — **no `Progress`** (Progress is owned by the locked `IScreenStateProvider.GetScrollProgress()` method; duplicating it in the result breaks the lock's single-source semantics). `AdbScreenStateResult` is deleted from Device (full replacement, not coexistence); `AdbScreenStateProvider.LastResult` and all consumers switch to `ScreenStateResult`. This is a breaking change to Device's public surface, accepted because M1/M3 land together: M1 switches the return type + provider, M3 switches the Host consumers (`HostRunServices`, `ScenarioObservation`, deletes the cast). No C# signature conflict (unlike option 2, where two `RefreshAsync` overloads differing only by return type are illegal).
**Rationale:** Principle 8 — locks are extended, never broken. The 4-method lock stays; the observable refresh capability is an additive extension. Lifting the result type into Core removes Host's only reason to reach the concrete Device type.
**Alternatives considered:**
- *Add `RefreshAsync` to `IScreenStateProvider` directly* — rejected: breaks the locked 4-method guard and the charter invariant.
- *Keep `AdbScreenStateResult` in Device and have Host depend on it* — rejected: perpetuates the concrete-dependency bypass; the whole point is to sever Host→Device concretions.
- *Option 2 — coexistence (new interface returns Core type, old `RefreshAsync` stays returning Device type)* — rejected: C# forbids same-name/same-param methods differing only by return type; would force renaming the interface method or hiding the old one, leaving dual types and violating PRD "replaces."
- *Option 3 — copy all fields including Progress into Core `ScreenStateResult`* — rejected: `Progress` already belongs to the locked `GetScrollProgress()` method; duplicating it in the result type breaks the lock's single-source semantics and contradicts PRD §4.1's field set.

### D2 — Config-driven `UniBrainFactory` in Core (resolves C2 assembly seam)
**Decision:** Core provides a `UniBrainFactory`/builder that turns `UniBrainConfig` + credentials into an assembled `UniBrainService`. Host hands config + credentials to the factory and receives `IUniBrain`; Host never hand-`new`s `PageAnalyzer`/`IModelProvider`. `UniBrainConfig` (already present: `DefaultProvider` + `CapabilityRouting`) is the single config; credentials flow through a separate channel (the existing invariant that `UniBrainConfig` holds no credentials is preserved).
**Rationale:** Principles 2 & 3 — AI capabilities aggregate into UniBrain; assembly is config-driven. `UniBrainConfig` already exists as the vehicle; extend it, don't reinvent. This makes mock/replay vs real vs real-device the same Host producing different link shapes from different config (Principle 7), not separate runners.
**Alternatives considered:**
- *Let Host keep assembling sub-interfaces but via a local helper* — rejected: leaves the bypass pattern structurally possible; the M7 guard could not enforce "assemble IUniBrain" cleanly.
- *Put credentials inside `UniBrainConfig`* — rejected: violates the existing credential-free invariant and the spec requirement on `UniBrainConfig`.

### D3 — Vision replay in Core `MockModelProvider` (resolves C2 capability gap)
**Decision:** Extend `MockModelFixture` so its capability→entry map satisfies `CompleteVisionAsync`/`CompleteMultimodalAsync` (mode-agnostic entries; the consuming method sets `Mode`). The existing spec clause mandating `NotImplementedException` for vision/multimodal on `MockModelProvider` is modified to symmetric replay-or-fail-fast behavior.
**Rationale:** Principle 4 — missing capabilities are added in Core, not Host. The replay link shape becomes a config selection inside UniBrain (`DefaultProvider="mock"`), not a Host-owned provider. This is what lets Host delete `DeterministicSettingsModelProvider` and still get a deterministic Settings replay.
**Alternatives considered:**
- *Keep a Host-owned replay provider* — rejected: that is exactly the C2 anti-pattern.
- *Separate vision fixture dictionary* — rejected: mode-agnostic entries keyed by capability are simpler and match the existing `MockModelFixture` shape.

### D4 — `PageAnalysis` shape contract as test, not prose (resolves C4)
**Decision:** A Core-defined contract (enforced by tests) that both the AI and UIAutomator observation paths satisfy: `Level1Menus`/`Level2Menus`/`Items`/`CurrentPath`/`HasScroll`/`IsEndOfList` filled to a common rule. UIAutomator path fills `Level1Menus`/`Level2Menus` and derives `Direction` from layout (no `Direction.Left` hardcode). A dual-path same-fixture equivalence test asserts structural equivalence on the fields the runner and safety gate consume. "Mock green" implies "real-path-shape green."
**Direction rule (resolved 2026-07-30):** UIAutomator path SHALL align with the AI path's fallback — `Level1Dir`/`Level2Dir` default to `Direction.Left` (the AI path also falls back to `Left` when the DTO omits direction, `PageAnalyzer.cs:141-142`). The shape contract SHALL assert equivalence on the menu-list fields (`Level1Menus`/`Level2Menus`/`Items`/`CurrentPath`/`HasScroll`/`IsEndOfList`); it SHALL NOT assert on `Direction` values beyond "both paths use the same fallback rule." The UIAutomator path's hardcode of `Direction.Left` (lines 181-182) is replaced by an explicit fallback derivation (no longer an ungoverned guess); the substantive C4 fix is filling `Level1Menus`/`Level2Menus`, which were previously empty. This deliberately couples direction fallback across paths rather than inventing a layout-classifier, keeping M4 minimal and the two paths structurally aligned.
**Rationale:** Principle 4 + spec alignment §6 — failure must be observable, not maskable. A test-enforced contract is the only way to prevent the two paths from silently drifting again. Supports spec defect D4 directly. Aligning the direction fallback (rather than deriving from container type) was chosen over a layout-classifier to keep M4 minimal and avoid inventing classification rules for UIAutomator containers — the C4 harm was the empty menu lists and the *ungoverned* hardcode, not the Left value itself.
**Alternatives considered:**
- *Document the expected shape in prose only* — rejected: prose contracts drift; C4 happened precisely because there was no test.
- *Merge the two paths into one* — rejected: the paths legitimately differ in how they produce `PageAnalysis` (AI vision vs UIAutomator dump); the contract is on the *output shape*, not the production mechanism.

### D5 — Inject `IEntryPolicyExecutor`; record D6 (resolves C3 + D6)
**Decision:** Host injects `IEntryPolicyExecutor` (no `new EntryPolicyExecutor`); construction lives in the composition factory. D6 recorded: in V1 the scenario runner owns the observe→plan→gate→execute→verify loop and does not depend on `TraversalEngine`/`TraversalFSM`. `HostRunServices.CreateTraversalEngine` is retained but marked unused with a note `// Does not use TraversalEngine; see host-target-architecture-design §3`; a future change routing the runner through `TraversalEngine` updates both this spec and the `traversal-engine` canonical spec.
**Rationale:** C3 is a direct injection violation. D6 is recorded (not silently decided) because it diverges from the engine path — recording it makes the divergence auditable and reversible via a spec-coordinated future change.
**Alternatives considered:**
- *Route the runner through `TraversalEngine` now* — rejected: out of scope for a seams fix; would expand the change into runner re-architecture.

### D6 — One decorated `IActionExecutor`, guard-enforced (spec alignment §6.2)
**Decision:** There is exactly one `IActionExecutor` in `HostRunServices` — the `SafeActionExecutor`-decorated one. All action consumers (traversal, recovery, popup, entry) receive that instance; no second un-decorated `IActionExecutor`. The M7 guard asserts Host holds no second un-decorated `IActionExecutor`, so recovery/popup paths cannot bypass the safety gate.
**Rationale:** `deterministic-action-safety` §1 requires recovery/popup paths to pass the same gate. The architecture makes this enforceable structurally rather than by convention.
**Alternatives considered:**
- *Rely on convention (always pass the decorated instance)* — rejected: convention is what C1–C4 eroded; structural enforcement is the lesson.

### D7 — Probes on `ITraceRecorder` (Host)
**Decision:** `doctor`/`analyze` route diagnostics through `ITraceRecorder` and the run-asset pipeline; no parallel diagnostic output format. New probes added the same way.
**Rationale:** Principle 6 — probes are Host conveniences built on existing trace, not a parallel diagnostic system. Keeps observability unified.
**Alternatives considered:**
- *A dedicated probe output format* — rejected: duplicates observability infrastructure and diverges from the run-artifact-reporting spec.

## Risks / Trade-offs

- **[Risk] `ScreenStateResult` lift moves a type from Device to Core, touching `AdbScreenStateProvider` consumers.** → Mitigation: `AdbScreenStateProvider` still implements the interface; the Device-only `AdbScreenStateResult` is superseded but the migration step M1 keeps the 4 locked methods and adds the interface implementation, verified by the guard test and a new interface contract test before any Host consumer switches.
- **[Risk] `UniBrainFactory` is a new Core coupling point — if its config surface is wrong, every link shape suffers.** → Mitigation: it consumes the *existing* `UniBrainConfig` shape (no new config vocabulary); credentials flow separately so the config invariant is preserved. M2 adds a factory-builds-facade-from-config test before Host depends on it (M3).
- **[Risk] Modifying the `MockModelProvider` vision clause is a spec MODIFIED on a previously-locked behavior.** → Mitigation: the modified requirement keeps the full updated content (archive-safe); the change is additive capability (replay) on methods that previously threw, so no consumer relied on the throw.
- **[Risk] The `PageAnalysis` shape contract test could be too loose (passes while paths still drift) or too tight (blocks legitimate path differences).** → Mitigation: the contract is on the fields the runner and safety gate *consume*, not on every field; the dual-path same-fixture test is the concrete verification.
- **[Risk] Deleting `DeterministicSettingsModelProvider` is a Host BREAKING change.** → Mitigation: M3 lands only after M2 (Core mock vision replay) so the deterministic Settings analysis exists as a `MockModelFixture` preset before the Host provider is removed; Host composition tests verify the replay link shape first.
- **[Trade-off] Keeping `CreateTraversalEngine` unused is dead code until a future change.** → Accepted: it is marked and noted, and D6 records why; removing it would force a coupled change and lose the migration hook.

## Migration Plan

Order respects dependencies (seam before use; Core gap before Host use of it). Each step independently verifiable and keeps the locked 4-method guard green.

| Step | What | Resolves | Verify |
|------|------|----------|--------|
| **M1** | Core: add `IObservableScreenStateProvider` + `ScreenStateResult`; `AdbScreenStateProvider` implements it; locked 4 methods untouched | C1 | guard test green (4 methods); new interface contract test |
| **M2** | Core/UniBrain: add `MockModelProvider` vision replay (extend `MockModelFixture`); add `UniBrainFactory`/builder consuming `UniBrainConfig` | C2, G1 | fixture-driven vision test (zero API cost); factory builds facade from config |
| **M3** | Host: delete `DeterministicSettingsModelProvider`; `CreateProvider` mock branch → assemble via `UniBrainFactory` with `MockModelProvider`; delete `(AdbScreenStateProvider)` cast → program `IObservableScreenStateProvider` | C1, C2 | Host composition tests green; no cast; no Host-owned provider |
| **M4** | Core: define + test `PageAnalysis` shape contract; UIAutomator path fills `Level1Menus`/`Level2Menus`, drops `Direction.Left` hardcode | C4 | dual-path same-fixture equivalence test |
| **M5** | Host: inject `IEntryPolicyExecutor` (no `new`); record D6 decision; mark `CreateTraversalEngine` unused | C3, D6 | entry injection test; doc note |
| **M6** | Host: route `doctor`/`analyze` diagnostics through `ITraceRecorder`; add missing probes on same path | probes | doctor output trace-correlated |
| **M7** | Add architecture guard: Host must assemble `IUniBrain`, not directly `new` `IPageAnalyzer`/`IModelProvider`; Host holds exactly one decorated `IActionExecutor` | structural | guard test prevents regression to the bypass pattern |

Dependencies: M1→M3 (interface before use); M2→M3 (Core mock before Host drops its own). M4, M5 independent and parallelizable. M6 last (convenience layer). M7 after M2/M3 (guard the new pattern once it exists).

Rollback: each step is a discrete commit; a failing step is reverted without affecting prior steps because seams are added before consumers switch. M1/M2 are pure Core additions (no behavior change until M3 consumes them), so they can land independently and roll back cleanly.

## Open Questions

- **`UniBrainFactory` exact API shape** — does it take a `UniBrainConfig` + a separate `CredentialProvider`/credentials object, or a single composite options record? The spec pins the invariant (credentials separate from `UniBrainConfig`); the exact parameter shape is an implementation detail for M2. Recommend a small `UniBrainAssemblyOptions` holding `UniBrainConfig` + credentials, decided at M2.
- **`PageAnalysis` shape contract enforcement location** — contract test in Core (alongside `PageAnalyzer`) vs a dedicated `PageAnalysisShapeContractTests` class. Recommend the dedicated class so the dual-path equivalence is visible and not buried in provider tests. Decide at M4.
- **Whether `AdbScreenStateResult` is deleted or kept as an internal Device alias** — M1 can keep it temporarily; full removal is a cleanup follow-up after all consumers use `ScreenStateResult`. Recommend deletion in a follow-up hygiene change, not this one, to keep M1 minimal.