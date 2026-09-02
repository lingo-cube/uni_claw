# Container Runtime V2 Purchase Reconciliation Ledger

> Status: `ARCHITECTURE_RECONCILIATION / NON-NORMATIVE_LEDGER`
> Date: 2026-08-31
> Freeze decision: [`CONTAINER_GRAPH_PREVIOUS_PURCHASE_FROZEN`](../decisions/container-graph-previous-purchase-freeze.md)
> Candidate input: [`Container Runtime V2 Architecture Working Draft`](container-runtime-v2-architecture-working-draft.md)
> Evidence baseline: [`PHASE-2.6-FINAL-REPORT`](../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/PHASE-2.6-FINAL-REPORT.md)

本账本把 Working Draft 中的候选语义逐项放回购买链：

```text
Phase 2.6 Evidence
→ Architecture Pressure / Buyer
→ Candidate Semantic
→ Benefit / Purpose / Necessity
→ Alternatives
→ Falsifier
→ Purchase Decision
→ Interface Contract
→ Implementation Symbol
→ Test
→ Real-runtime Validation
```

它不替代 Approved OpenSpec。`Purchase Decision` 为 `PURCHASE_FOR_SPEC` 时，表示 Sol 已允许写入新的 OpenSpec contract；只有该 change 进入受控 Apply 后，才允许 Runtime 实现消费。

## 1. Audit boundary and repository state

扫描范围：`openspec/changes/`（active + archive）、`docs/analysis/`、`docs/decisions/`、`src/`、`tests/`、相关 Guard。工作树包含用户已有的未提交 Runtime、Semantic、Perception、DriverHost 和 evidence 改动；本账本不把它们当作 committed baseline，也不清理或覆盖。

发现的关键生命周期事实：

1. `runtime-active-container-context-and-transition-semantics` 仍是 active change。
2. 其 README 仍写 `Apply: NOT_AUTHORIZED / Implementation: NOT_STARTED`，但当前 `tasks.md` 已把 1.1–5.2 勾选完成，且对应 production/tests/DriverHost 文件已存在。
3. 6.1–6.4 验证、Human verification packet、archive/graduation 均未完成。
4. 因此当前事实是 `IMPLEMENTATION_PRESENT_BUT_NOT_VERIFIED_OR_GRADUATED`，不能用旧 README 或 tasks 单独推断生命周期。
5. Runtime 中没有生产 `ContainerGraph` / `GraphNode` / `GraphEdge` world-model 实现；`RunExecutionGraph` 是 DriverHost execution read model，不是候选 ContainerGraph。

## 2. Old purchase evidence table

| Evidence cluster | Artifacts / symbols | Claim previously carried | Current evidence status | V2 reconciliation |
|---|---|---|---|---|
| Active context OpenSpec | `openspec/changes/runtime-active-container-context-and-transition-semantics/{proposal,design,tasks,README}.md` + 4 specs | observed location 与 active execution obligation 可分离；ordered ancestor path；typed transition；atomic commit | active、部分实现、verification 未完成 | `FREEZE`; normative status 不继承；逐项 MOVE/DELETE/KEEP |
| Active context implementation | `Agent/ActiveContainerContext.cs`; `Agent.cs`; `Agent.OpenWorld.cs`; `Agent.PlanRun.cs`; `Agent.Recovery.cs` | 单一 Agent slot 替代 `_activeContainer` + local parents + ancestry | production symbols present, uncommitted | `MOVE`: obligation/path evidence；最终删除 “active=current” 命名和并行 current truth |
| Transition implementation | `Model/ContainerTransition.cs`; `Agent.ContainerReconciliation.cs`; `DecisionRecord.ContainerTransition`; `Container.AcceptPreparedObservation` | 7 closed kinds + 4 dispositions；validation-before-commit；immutable history | implementation present；focused tests present | `KEEP` immutable occurrence/ref/atomic commit；`REFACTOR` kind/disposition semantics；不得把 expectation 当 occurrence |
| Context read projection | DriverHost `AgentStateSnapshot`, `RunSnapshot`, `RunSnapshotProjector`, `EvidenceCatalog` | observed/execution/path/latest transition refs 的只读投影 | implementation + tests present | `KEEP` read-only/evidence linkage；`MOVE` fields to V2 CurrentContainer/TransitionOccurrence projection |
| Old tests | `ActiveContainerContextTests`; Stage A/B/C replay; classifier/reconciliation tests; DriverHost read-model tests | state replacement、r5 preserved execution、atomic rollback、normal-path equivalence | tests exist；full verification task open | `KEEP` evidence mechanics tests；`REPLACE` assertions that equate active execution with current location or forbid same-node multi-entry |
| Old Guards | `ActiveContainerContextArchitectureGuardTests`; `ContainerReconciliationArchitectureGuardTests` | one owner / no old field / no side-effect in commit / no copied state | source-shape guards exist | `KEEP` no-parallel-truth/atomicity intent；`MOVE` symbol assertions to V2 boundaries |
| Fast Semantic archived purchase | archived `fast-semantic-container-identity-baseline`; decisions `fast-semantic-container-identity-*` | bounded candidate evidence、read-only retrieval、Runtime-owned validation、no Slow | archived graduation applies only to candidate provider boundary | `KEEP` candidate-only/fail-closed seam；`EXTEND` behind Fast Resolver contract；V2 Fast Trust remains ungraduated |
| Semantic evidence fusion | archived `semantic-evidence-fusion-baseline`; current semantic contract docs | Observation → candidate evidence → Runtime validation；confidence != truth | graduated evidence contract | `KEEP`; Graph/Fast/Slow may consume evidence but cannot obtain authority |
| StableKey container-domain correction | Phase 2.6 `PROJECT-LEADER-ROW-IDENTITY-TRANSITION-*`; `RowIdentityContext*` | container-scoped row correlation；Tap heuristic rejected；transition seam missing | unit/matrix green；fresh child buyer missing；not graduated | `KEEP` lifecycle-scoped correlation；`DEFER` harness consumption until post-observation V2 occurrence exists |
| Phase 2.6 acceptance | final report + evidence index + raw asset refs | 19 fresh real runs、0 Completed、8 deterministic repairs、remaining pressure/unknowns | strongest current real-runtime evidence；not graduation | primary buyer/falsifier source；do not reinterpret r5 cause beyond `I.UNKNOWN` |
| Existing Container local state | `Container.CurrentObservation`, `ViewportExplorationObservations`, local progress, `IsLocalComplete` | page-local fresh/accepted evidence and local completion owner | heavily tested and real-run exercised | `KEEP`; reinterpret as CurrentSlice + node-lifecycle LocalModel inputs; no duplicate owner |
| Agent obligation/progress | `_branchProgress`, `BranchProgressEvidence`, `CompileExplorationLedgerView`, GoalEvidence | traversal obligation、boundary/completion evidence、Agent projection | graduated/frozen invariants exist | `KEEP` Agent/UniAgent decision evidence；must not move into Graph action authority |
| Identity safety state | Run-local `visited` + active path `ContainsSemanticIdentity` | reject duplicate semantic page and ancestry cycle | protects old tree traversal but assumes identity uniqueness across entries | `MOVE/REPLACE`: Graph permits same destination via distinct relations；loop prevention stays Agent obligation policy, not node existence truth |
| Completeness mechanisms | `ContainerInventoryCompletenessEvidence`, discovery epoch, viewport exhaustion, post-completeness validator | bounded accepted evidence proves local traversal exhaustion | deterministic + Phase 2.6 evidence | `KEEP` evidence conditions；`REFACTOR` result to separate coverage complete from semantic resolution/subtree completion |
| Historical anti-Graph claims | sibling-branch/open-world proposals and prior Container architecture gate | NavigationGraph/PageGraph/route model not purchased | still authoritative as non-goal pressure | `KEEP`: V2 ContainerGraph is evidence world model only, not planner/route/action authority |

### Status/graduation truth

| Claim | Allowed interpretation after freeze |
|---|---|
| Old tasks `[x]` | implementation activity evidence only |
| Old focused tests green | behavior evidence only；不证明 V2 purchase 或 full readiness |
| Fast Semantic baseline `GRADUATED` | candidate-provider boundary graduated；不包含 Fast Resolver/Trust、Slow、Graph、CurrentContainer |
| Phase 2.6 deterministic repairs | KEEP as verified mechanisms；0/19 means full traversal not accepted |
| `PREMATURE_RETURN_TO_ACTIVE_PARENT` test green | proves old observed/execution split can be represented；does not prove V2 current-location semantics |
| `NavigationGraph: NOT_INTRODUCED` | remains true；V2 Graph must separately prove it is not a planner |

## 3. Phase 2.6 pressure → candidate semantic → purchase decision

| Evidence | Architecture pressure / buyer | Candidate semantic | Classification | Purchase decision | Falsifier / graduation evidence |
|---|---|---|---|---|---|
| r5 fresh observation showed Settings Root while old expectation/execution remained Display；trigger cause remains `I.UNKNOWN` | Runtime must record what was freshly observed without forcing it to match expected transition | `ACTION_EXPECTATION != WORLD_TRUTH`; thin `CurrentContainer`; immutable TransitionOccurrence; pending obligation separate | `EVIDENCE_BACKED_PURCHASE` | `PURCHASE_FOR_SPEC` | replay must accept fresh current location while preserving unresolved obligation and issuing no action/recovery/completion |
| Z4 cross-container StableKey contamination + run-global known-row leakage | current occurrence/correlation needs Container lifecycle scope | `CurrentSlice`; node-owned lifecycle `LocalModel`; stale model bounds forbidden for dispatch | `EVIDENCE_BACKED_PURCHASE` | `PURCHASE_FOR_SPEC` | same labels/bounds across Containers must not merge; current fresh occurrence must be required for action grounding |
| unresolved child first frame + missing transition observability seam | physical transition can occur before semantic identity is trusted | working Container entity may exist at `INITIALIZED`; transition completion independent of identity trust | direct pressure; exact Graph-node solution not unique | `ARCHITECTURE_HYPOTHESIS` | `PURCHASE_FOR_CONTROLLED_EXPERIMENT` | compare against deferred-node alternative; must reduce blocking without unacceptable false node retention |
| return-control/title-off repairs + multi-entry Settings counterexample | current return depends on this entry path, not a node parent | `EntryContext`; no canonical parent; return expectation verified by fresh observation | repeated pressure; Phase 2.6 did not exercise every multi-entry pair | `ARCHITECTURE_HYPOTHESIS` | `PURCHASE_FOR_SPEC` because bounded/reversible and required to avoid parent authority | deterministic Desktop/Search → same Settings node → distinct verified returns; no reversed-edge truth |
| V/X deep Unknown after traversal reached deep pages | coverage and semantic resolution are independent | coverage-complete may retain Unknown; `ContainerComplete != SubtreeComplete` | `EVIDENCE_BACKED_PURCHASE` | `PURCHASE_FOR_SPEC` | Unknown must not alone block proven frontier exhaustion; Goal/semantic obligation remains unresolved separately |
| 19 runs, 0 Completed; blocker migration across perception/normalization/transition/semantics | case-by-case repair margin is declining | bounded ContainerGraph world model; Fast/Slow semantic verification; correction boundary | `ARCHITECTURE_HYPOTHESIS` | split below; no blanket graduation | compare Fast-only/perception-only alternatives on blocker migration, false trust, latency, cost and completion/depth |
| BGE held-out results | cheap semantic retrieval can rank candidates | Graph/trigger/destination candidate prior for Fast | `ARCHITECTURE_HYPOTHESIS` with existing graduated provider seam | `PURCHASE_FOR_CONTROLLED_EXPERIMENT` | vector similarity must not directly commit identity/action; measure false trust and latency |
| UI-TARS shadow role false promotion ~36.4% while showing useful semantic capability | stronger model may correct ambiguity but is unsafe as authority | async `ISlow...Advisor`-like provider returning revision-bound assessment; Disabled/Shadow/Advisory modes | `ARCHITECTURE_HYPOTHESIS` | `PURCHASE_INTERFACE_AND_SHADOW_ONLY` | graduate only if valid correction reduces wrong-branch/deep-Unknown/repeated repair and false correction/cost stay bounded |
| wrong-child/off-path examples in draft; no production Slow correction result yet | correction needs a consumer without giving Slow action authority | revision-bound semantic correction fact → UniAgent obligation recalculation boundary | `ARCHITECTURE_HYPOTHESIS` | `PURCHASE_CONTRACT_ONLY`; implementation after core occurrence/trust seam | stale result ignored; traversal and directed-entry scenarios must show obligation repair; Slow never dispatches recovery |
| deep traversal makes restart expensive | bounded recovery may benefit from last trusted path point | checkpoint as derived execution-path projection | `ARCHITECTURE_HYPOTHESIS` | `DEFER_IMPLEMENTATION_UNTIL_CORRECTION_PROVEN` | if restart cost is low or path confirmation unreliable, do not graduate checkpoint |

### Architecture-hypothesis purchase cases

#### Evidence-only ContainerGraph and working unproven node

- Repeated problem: r5 demonstrates that a fresh observed destination can disagree with expectation, Z4 demonstrates that relation/correlation evidence leaks when Container scope is unclear, and unresolved first-child frames expose a period in which physical location exists before identity trust.
- Structural benefit: one Run-local append-only evidence model can preserve distinct occurrences and multi-entry relations while keeping current truth in `CurrentContainer`. An `INITIALIZED` node lets the Runtime continue collecting local evidence without fabricating identity.
- Alternative: keep only per-transition records and defer every node until identity is trusted. This has lower model complexity, but repeats candidate matching in transition, return, traversal and correction paths and preserves the unresolved-first-frame blocking seam.
- Long-term cost if not purchased: each consumer must reconstruct entry history and identity candidates independently; special cases continue to accumulate around expectation/observation disagreement.
- Added complexity: opaque refs, relation correlation, unresolved-node reconciliation, false-node retention and an additional evidence aggregate.
- Boundedness/reversibility: Run-local only; no persistence, route API, action authority, canonical parent or production Agent integration in the first experiment. The immutable model can be removed without migrating external data.
- Falsifier/validation: reject graduation if a transition-only alternative resolves multi-entry and correction with less complexity, or if working nodes frequently retain false identities. Validate r5 fresh-over-history, Desktop/Search multi-entry, fold/bind/reject rates and zero action-authority leakage.

#### Fast Container Resolver and derived Fast Trust

- Repeated problem: Phase 2.6 blockers migrated across weak text, semantic ambiguity, transition classification and deep Unknown despite multiple deterministic repairs. BGE held-out evidence shows cheap semantic ranking is useful but not truth.
- Structural benefit: one revision-bound resolver combines action prior, fresh Slice, existing candidate evidence and non-authoritative Graph candidates. This localizes optimistic working interpretation and prevents each Runtime path from inventing its own semantic threshold.
- Alternatives: (a) keep adding deterministic/perception special cases; (b) block until a stronger semantic result; (c) let the existing provider candidate directly determine identity. (a) has declining marginal return, (b) increases main-path latency, and (c) violates the graduated candidate-only boundary.
- Long-term cost if not purchased: duplicated conflict/threshold logic, continued blocker migration and no explicit place to compare Fast-only against stronger asynchronous correction.
- Added complexity: action-prior vocabulary, candidate ranking, hard-conflict rules, latency measurement and false-trust diagnostics.
- Boundedness/reversibility: synchronous pure interface and derived view only; no mutable trust slot, provider/backend replacement, action authorization, completion or memory publication.
- Falsifier/validation: do not graduate if Fast fails to resolve most relevant ambiguity, increases false identity/branch rate, exceeds latency budget, or performs no better than current deterministic fusion. Measure SAME/NEW/TRANSIENT/AMBIGUOUS, abstention, hard-conflict precedence, false trust and latency.

#### Slow Semantic Advisor in Disabled/Shadow/Async Advisory modes

- Repeated problem: advertisements, overlays, unrelated pages, wrong children, low-information scenes and deep Unknown require richer scene/relationship interpretation; UI-TARS shadow evidence shows useful semantic capability but unsafe role promotion.
- Structural benefit: an independent asynchronous assessment can challenge Fast and provide correction evidence without stalling the main path or receiving action authority. It also creates an explicit experiment boundary for stronger models instead of embedding provider calls throughout Runtime logic.
- Alternatives: Fast-only; synchronous strong model in the main path; continued scenario-specific repairs. Fast-only remains the control arm, synchronous use couples latency/availability to execution, and special cases repeat the Phase 2.6 marginal-return pattern.
- Long-term cost if not purchased: no controlled way to measure whether stronger semantics reduces wrong-branch/deep-Unknown repairs; provider experiments would leak directly into Runtime behavior.
- Added complexity: async lifecycle, stale-result rejection, cost/latency telemetry, false-correction visibility, provider-neutral result vocabulary and test fakes.
- Boundedness/reversibility: first consumption is Disabled or Shadow; immutable results bind exact evidence revisions and cannot mutate CurrentContainer/Graph, authorize actions, recover, plan or complete Goals. No concrete backend or mandatory capability is purchased.
- Falsifier/validation: do not graduate Slow as mandatory if it rarely produces valid correction, fails to reduce wrong branch/unresolved/repeated repair, creates material false correction, or costs more latency/money than the measured benefit. Compare Fast-only with Fast+Slow Shadow/Advisory on the same fixtures and fresh runs.

#### Semantic correction boundary and checkpoint proposal

- Repeated problem: a stronger assessment is useless unless the intended obligation can remain pending while the actually visited child is recorded; deep restart cost creates checkpoint pressure.
- Structural benefit: immutable correction facts let Runtime repair meaning while UniAgent retains sole authority to recompute work. A checkpoint stays a derived proposal over the corrected execution path.
- Alternative: encode `REVISIT/REENTER/RESET/RECOVER` into Slow or Graph. This is rejected because it creates a second planning/recovery authority and a hidden FSM.
- Added complexity: exact evidence binding, stale rejection, obligation-consumer mapping and path-confirmation criteria.
- Boundedness/reversibility: contract-only until an upper-layer gate; no Agent/Goal mutation and no stored checkpoint lifecycle.
- Falsifier/validation: if correction facts cannot deterministically repair traversal/directed-entry obligations without changing frozen Agent authority, stop at a Human Gate. Do not implement checkpoint if recovery-cost reduction is not demonstrated.

## 4. P1–P12 purchase ledger

| Priority | Purchased semantic / hypothesis | Decision now | Interface implication | Current implementation relation |
|---|---|---|---|---|
| P1 Graph | evidence-backed Container world model；not planner/action/current truth/persistent topology；node may be unproven | hypothesis bought for bounded Run-local experiment | separate read and record responsibilities; no planner API | no production equivalent；do not reuse DriverHost `RunExecutionGraph` |
| P2 CurrentContainer | `NodeRef + CurrentSlice + EntryContext`; current physical working location | evidence-backed purchase | one Agent-owned current slot/read contract | replaces location meaning currently split across belief + ActiveExecutionContainer; obligation remains separate |
| P3 Relation | Source + TriggerOccurrence/Affordance + Destination + append-only evidence；same destination does not merge relations | follows Graph hypothesis | recorder accepts only observed occurrence evidence; assessment derived | no equivalent; BranchProgress is not Relation |
| P4 Entry/Return | no canonical parent；return target derives from current entry context；expectation != truth | bounded architecture purchase | EntryContext projection + return verification inputs | ActiveAncestorPath values reusable; its parent/tree authority is not |
| P5 Transition occurrence | actual source/trigger/fresh destination/outcome；many occurrences may support one relation；off-path need not create normal edge | evidence-backed purchase | immutable occurrence tracker/read seam | reuse TransitionRef/EvidenceRef/atomic commit; replace closed old classifications as authority |
| P6 Action prior + Fast resolution | action supplies prior only；Fast combines action + fresh observation + Graph prior | controlled hypothesis | resolver contract accepts immutable snapshots and returns assessment | extend existing semantic candidate/fusion; no new action authority |
| P7 Fast Trust | working interpretation derived from evidence; never action/completion/memory truth | controlled hypothesis | trust projection is derived, no mutable truth owner | no existing Fast Trust contract; current provider remains evidence-only |
| P8 Slow Advisor | stronger async assessment, revision-bound, default Disabled/Shadow/Advisory | interface + Shadow hypothesis | provider-neutral advisor/result contract; no mutation/action methods | absent; must not alter external provider/backend in first slice |
| P9 Slow correction | corrects meaning; UniAgent decides next obligation/action | contract-only hypothesis | corrected semantic fact port at UniAgent boundary | no production consumer; requires separate upper-boundary gate before behavior change |
| P10 Slice/LocalModel | fresh visible window + node-lifecycle accepted knowledge；no cross-run stable item identity；stale bounds cannot dispatch | evidence-backed purchase | reducer/reader seam only if existing Container methods cannot hold responsibility | reuse `CurrentObservation`/viewport accepted history/normalizer before creating types |
| P11 Completeness | frontier exhaustion proof；coverage != semantics；Container != subtree | evidence-backed purchase | analyzer produces evidence, not FSM/action | extend existing completeness evidence; no new completeness FSM |
| P12 Checkpoint | last sufficiently confirmed node on correct execution path, derived only | hypothesis, implementation deferred | read-only projection after trust/correction exists | no new Graph object/FSM/state slot |

## 5. Existing symbol KEEP / MOVE / DELETE / DEFER reconciliation

| Existing symbol / responsibility | Decision | Target meaning / migration condition |
|---|---|---|
| `Agent._belief` / `WorldBelief.SemanticPage` | `KEEP` | fresh accepted observation evidence; must not be copied as a second current truth |
| `ActiveContainerContext` | `MOVE_THEN_DELETE` | split current physical location into V2 `CurrentContainer`; move unresolved execution obligation/path evidence to existing Agent progress/obligation projection |
| `ActiveExecutionContainer` | `SUPERSEDE_NAME_AND_AUTHORITY` | it may temporarily identify pending execution obligation, never current world location |
| `ActiveAncestorPath` values | `MOVE` | execution-entry/path projection and verified return evidence; no canonical parent/topology/action authority |
| `ContainerTransition` immutable record/ref/evidence | `KEEP_AND_REFACTOR` | V2 TransitionOccurrence; preserve append-only trace and evidence linkage |
| old `ContainerTransitionKind` / disposition | `DEFER_COMPAT_DELETE` | compatibility mapping only until V2 occurrence/assessment consumers migrate; old expectation-shaped kinds do not become Graph truth |
| `ContainerTransitionClassifier` | `REFACTOR` | separate occurrence recording from Fast boundary/relation assessment; classifier never authorizes action |
| `Agent.ContainerReconciliation` validation-before-commit | `KEEP` | atomic current/evidence update seam; adapt inputs to V2 CurrentContainer and occurrence |
| DriverHost context/read-model projection | `KEEP_AND_MOVE` | project CurrentContainer, entry context, occurrence and derived trust; remain authority-free |
| `Container` existing class | `REUSE_FIRST` | candidate node-local working entity/LocalModel owner; do not create parallel `GraphNode` until a unique buyer proves existing class cannot serve |
| `Container.CurrentObservation` | `KEEP` | source for `CurrentSlice`; never history/action authority |
| `Container.ViewportExplorationObservations` | `KEEP_AND_CONSTRAIN` | current-node lifecycle accepted slice accumulation; no cross-run identity |
| `Container.IsLocalComplete` | `KEEP_AND_REFINE` | local execution evidence; coverage projection must not imply semantic/subtree completion |
| `ContainerInventoryCompletenessEvidence` + discovery epoch | `KEEP_AND_EXTEND` | frontier/coverage evidence; Unknown tracked separately |
| `SourceEquivalenceNormalizer` | `KEEP` | local correlation mechanism; bounds/text/ordinal/StableKey remain evidence only |
| run-local `visited` semantic identities | `DELETE_AFTER_REPLACEMENT` | replace global duplicate-node rejection with relation/obligation evidence so same node from different Source remains legal |
| `ContainsSemanticIdentity` cycle guard | `MOVE` | Agent traversal loop-prevention policy; cannot deny Graph node existence or establish canonical parent |
| `_branchProgress`, `BranchProgressEvidence`, `GoalEvidence` | `KEEP` | Agent/UniAgent obligation and completion evidence; Graph/Slow cannot mutate or authorize it |
| `CompileExplorationLedgerView` | `KEEP` | authority-free obligation/evidence projection; not ContainerGraph |
| Fast semantic candidate provider/policies/vector index | `KEEP_AND_EXTEND` | evidence source behind Fast Resolver; no direct CurrentContainer/Graph mutation |
| RowIdentityContext container domain | `KEEP` | current-lifecycle occurrence correlation; transition consumption deferred until post-observation seam exists |
| `RunExecutionGraph` | `KEEP_SEPARATE` | observability execution hierarchy only; explicitly not ContainerGraph |
| new long-term/cross-run graph memory | `DEFER` | requires Environment Memory buyer and separate persistence/versioning gate |
| Transition FSM | `DEFER` | do not create until actual pending async lifecycle cannot be expressed by occurrence + correlation refs |

## 6. Mutable truth budget

The V2 target is replacement, not addition:

```text
BEFORE (current working tree)
  WorldBelief.SemanticPage                  # observed semantic evidence
  ActiveContainerContext.ActiveExecution    # execution obligation, often treated as active/current
  ActiveAncestorPath                        # active recursive path
  Container.CurrentObservation              # node-local accepted observation

AFTER target
  CurrentContainer                          # sole Agent-owned current physical working location
    NodeRef
    CurrentSliceRef
    EntryContext
  Container/Node local accepted evidence    # existing owner, not copied
  Agent obligation/path projection          # existing progress/evidence owner, not current location
  Graph evidence + assessments              # append-only evidence / derived views, not parallel current truth

NET_NEW_MUTABLE_TRUTH = 0
```

Graph records and assessment records may be append-only evidence. `TrustView`, `RelationAssessment`, checkpoint and completeness views must be derived. No mutable `Graph.Current`, `ActiveContainer`, `_latestTransition`, `_latestTrust` or canonical parent slot may be added.

## 7. First continuous purchase boundary

The first Apply slice should not create all interfaces from the Working Draft. It should purchase and implement only the minimum continuous seam:

```text
TransitionOccurrence
→ CurrentContainer projection
→ EntryContext
→ evidence-only Graph record/read boundary
```

It must reuse the existing `Container`, immutable transition refs/history, Agent reconciliation commit, DriverHost projection and focused tests. Fast/Slow providers remain outside this first implementation slice, except that interfaces must leave a revision-bound evidence consumer seam.

Required deterministic scenarios:

1. same node reached from Desktop and Search produces two relations and two EntryContexts, not two canonical parents;
2. Back expectation is derived from current EntryContext and verified by fresh observation;
3. r5-style unexpected accepted destination advances CurrentContainer while unresolved execution obligation remains pending;
4. transition completed with an `INITIALIZED` working entity before identity trust;
5. off-path occurrence is retained but does not create a normal relation/action authorization;
6. stale LocalModel bounds cannot authorize dispatch;
7. no second current/active mutable truth is introduced.

## 8. Current stage result

```text
STATUS: CONTAINER_RUNTIME_V2_READY_FOR_AGENT_INTEGRATION_HUMAN_GATE
PURCHASED_FOR_SPEC:
  CurrentContainer
  TransitionOccurrence
  EntryContext / path-relative return
  Slice / lifecycle LocalModel constraints
  coverage != semantic resolution
HYPOTHESIS_PURCHASED_FOR_CONTROLLED_EXPERIMENT:
  evidence-only ContainerGraph + first-class relations
  working unproven node
  Fast Resolver / derived Fast Trust
  Slow Advisor Disabled / Shadow / AsyncAdvisory seam
HYPOTHESIS_CONTRACT_ONLY:
  semantic correction -> UniAgent obligation boundary
  derived checkpoint proposal
DEFERRED:
  Agent obligation consumer pending Human Gate
  production checkpoint state / recovery behavior
  cross-run graph memory
  transition FSM
  provider/backend purchase
VALIDATED:
  repository inventory and Phase 2.6 evidence mapping
  immutable core / Graph / Fast / Slow / correction deterministic tests
  stateful async stale Slow and Fast/Slow conflict tests
  Architecture Guards / solution build / OpenSpec / consistency
NOT_VALIDATED:
  actual Agent obligation correction behavior
  full-suite green (classified unrelated/environmental failures remain)
  fresh real-device Phase 2.6 acceptance
```
## R6 implementation status and upper-boundary gate

As of 2026-09-01, the reversible Runtime-side implementation includes the
immutable V2 core, evidence-only Graph read/record projection, Fast resolver,
Slow Disabled/Shadow/AsyncAdvisory seam, revision-bound semantic correction
facts, read-only Agent obligation reevaluation input, and derived checkpoint
proposal.  Detailed evidence is recorded in
`openspec/changes/container-runtime-v2-core-semantics/evidence/R6-SEMANTIC-CORRECTION-BOUNDARY-RESULT.md`.

The next consumer would modify Agent-owned obligation/progress semantics.  It is
therefore stopped at `REQUIRED_HUMAN_GATE_UPPER_AGENT_AUTHORITY`; the existing
Agent, GoalEvidence, action, recovery, and completion authorities remain
unchanged.  `NET_NEW_MUTABLE_TRUTH = 0` for all implemented R0-R6 slices.
