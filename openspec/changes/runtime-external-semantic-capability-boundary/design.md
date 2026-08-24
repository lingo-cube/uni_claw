## Context

See `proposal.md` for motivation. The frozen authority path remains Agent-owned
execution and completion through FSM, Traversal, fresh Observation, and
GoalEvidence. The current semantic provider port is evidence-only, but its V1
shape supports only Container Identity and uses free-string candidates. Runtime
also contains provider implementations, scenario classifiers, corpus
infrastructure, and direct consumers of Android/Settings labels.

Two perception paths currently converge without a sufficiently explicit source
tier. Vision produces the primary screenshot-derived observation. UIAutomator
hierarchy acquired through ADB is useful corroborating structure, but is not
universally available or reliable and must never be treated as an equivalent
primary perception channel.

## Goals / Non-Goals

**Goals:**

- Restore scenario-neutral Generic Runtime without weakening existing semantic
  capability or frozen Agent authority.
- Establish an external, evidence-only Scenario Knowledge Package and Semantic
  Capability Binding.
- Introduce a typed, versioned V2 evidence boundary owned by Runtime as buyer.
- Make source authority and provenance non-launderable through semantic
  interpretation.
- Demote ADB hierarchy to optional auxiliary evidence with explicit availability
  and fail-closed fusion behavior.
- Preserve OpenWorld DFS, SETTINGS-TREE-01, RuntimeAgent Phase 1-4, Strategy,
  FSM, Traversal, Recovery, and GoalEvidence behavior.

**Non-Goals:**

- Redesign Agent, DFS, Strategy, FSM, Traversal, Recovery, or Goal evaluation.
- Make RuntimeAgent a planner, action generator, lifecycle owner, or Multi-Run
  coordinator.
- Make ADB hierarchy mandatory or silently equivalent to Vision.
- Add routes, selectors, click plans, scenario strings, or completion flags to
  the semantic contract.
- Implement production code during this design gate.

## Phase 0 Ownership Map

| Layer | Owns | Must never own in this change |
|---|---|---|
| Environment | Raw capture, primitive parsing, source-local geometry mapping, source availability, freshness, frame/display association, and provenance | Page identity, widget role, navigation meaning, scenario completion, action authorization |
| External Semantic Capability | Scenario/platform vocabulary, classifiers, locale rules, page/container relations, affordance interpretation, scenario corpora, and read-only candidate evidence production | DeviceAction, target dispatch, Agent callback, FSM/Run command, Recovery command, GoalEvidence, completion |
| Generic Runtime | Versioned evidence contract, manifest/symbol admission, freshness/source-tier validation, fusion, contradiction handling, reconciliation, and WorldBelief proposals | Settings/Android labels, scenario routes, selectors, page heuristics, lifecycle or completion authority |
| Agent | Candidate authorization, action approval, same-Run continuation, recovery decision, fresh verification, GoalEvidence evaluation, RunState, and terminal outcome | Scenario interpretation implementation supplied by an external capability |
| FSM | Legal lifecycle transition protocol | Semantic interpretation or planning |
| Traversal | Concrete execution and step verification | Scenario classification, Goal completion, or lifecycle decisions |

This map preserves Architecture v1 and Contract I-1 through I-14. No owner is
added and no existing authority moves.

## Phase 1 Executable Source Inventory

The inventory is source-based and distinguishes generic mechanics from embedded
knowledge. Documentation-only examples are included because they currently make
the production contract appear scenario-owned even when the model shape is
otherwise generic.

### A — Generic Runtime mechanism retained

| Production area | Retained responsibility |
|---|---|
| `AdbUiHierarchySource` acquisition/parsing path | Optional dump acquisition, XML/primitive parsing, raw node facts, source availability, auxiliary provenance, and source-local mapping into the canonical full-frame coordinate contract |
| Runtime semantic admission/fusion/reconciliation | Closed evidence validation, source-tier preservation, contradiction handling, and WorldBelief proposal production |
| Agent semantic/OpenWorld loops | Existing bounded loops, uniqueness checks, authorization, DFS progress, recovery choice, fresh verification, GoalEvidence, and terminal decision |
| `SemanticActionLowerer` | Lowering an already Agent-authorized, freshly grounded typed control role into a non-dispatched execution proposal; Traversal remains executor |
| Semantic V1 models | Frozen existing Container Identity contract during migration; no new V1 kinds or free-string inference |

### B — Scenario/platform knowledge extracted or removed

| Production area | Embedded knowledge | Destination |
|---|---|---|
| `World/InteractionAffordanceAnalyzer.cs` | Settings Preference-row, Android class/resource-id, and search action-bar classification | External platform/scenario binding; Runtime keeps only a reducer over admitted typed affordance candidates |
| `Agent/Agent.SemanticRun.cs` | Settings-derived transition timing commentary/assumptions, title-summary row-band calibration, and raw widget-label continuity interpretation | External evidence/policy descriptor where bought; Agent retains bounded settle, authorization, and verification mechanics |
| `Agent/Agent.OpenWorld.cs` | English Android toolbar `Navigate up` and Settings parent-return interpretation | External typed affordance/relation evidence; Agent retains uniqueness, authorization, DFS, and post-return verification |
| `Adapters/Device/AdbUiHierarchySource.cs` | Settings toolbar/page-title/resource-role interpretation such as `collapsing_toolbar` | External binding; adapter retains raw auxiliary facts and mechanical geometry mapping |
| `Adapters/Perception/Vision/ImageSwitchStateProvider.cs` | Android Settings-specific appearance calibration | External platform capability/calibration package; primary Vision source contract remains generic |
| `PhysicalHostComposition.cs`, `PhysicalHostOptions.cs`, `Program.cs` | Settings defaults, application identities, launch intents, page anchors, device mutations, corpus setup, and scenario timing/baselines | Isolated validation Harness plus explicit external package/binding composition |
| Runtime semantic Fast/Corpus/Evaluation/Benchmark implementation | Provider implementation, vector candidate/index, corpus infrastructure and scenario evaluation co-located with Runtime execution | External semantic capability/evaluation projects; Runtime retains the buyer contract only |
| Runtime Model/Startup and adapter XML comments/examples | Settings, Wi-Fi, DeveloperOptions, Android resource/id, and recursive Settings-child examples | Remove from generic production documentation or relocate to external package/tests without renaming the scenario |
| `StructuredElementEvidence` raw role-shaped fields and `SemanticActionLowerer` free `PerceptionType` use | Android-shaped title/summary/resource conventions and free provider label consumption | Source-qualified observation facts plus admitted typed control-role binding; no selector or target authority |

### C — Authority violation

`NONE_FOUND` in the audited semantic producer/analyzer/corpus paths.

`Agent.OpenWorld` and `Agent.SemanticRun` legitimately authorize actions, invoke
Traversal, evaluate GoalEvidence, and transition the Run because Agent already
owns those decisions. Those paths are preserved and MUST NOT be moved into the
external capability. `SemanticActionLowerer` may construct a proposal only
after Agent authorization and does not dispatch it; this is retained mechanism,
not capability authority.

## Human Apply Gate

The inventory proves that extraction can preserve authority and frozen
invariants, but V1 is insufficient for the required typed source-qualified
identity, affordance, and relation candidates. Therefore:

```text
AuthorityDelta = NONE
ArchitectureInvariantDelta = NONE
ContractDelta = SEMANTIC_EVIDENCE_PROTOCOL_V2_REQUIRED
Apply = BLOCKED_UNTIL_EXPLICIT_HUMAN_APPROVAL
```

Design and strict validation may complete before approval. Production code,
tests that reshape contract behavior, task completion, and package migration
MUST NOT begin until the gate is approved.

## Decisions

### 1. Runtime owns the consumer contract; external packages own knowledge

Runtime defines the closed V2 envelope and payload kinds because it is the buyer
and validator. External capability assemblies depend inward on that contract;
Runtime does not reference a Settings or Android knowledge assembly. This keeps
the existing Runtime project-reference isolation intact.

A package supplies a versioned manifest of semantic symbols and read-only
recognition knowledge. A binding is a pure interpreter over source-qualified
Observation facts and bounded verified history. Package and binding may ship
together, but their authority remains evidence production only.

Alternative considered: let each package define arbitrary payloads. Rejected
because Runtime could not mechanically reject hidden authority or preserve a
stable wire contract.

### 2. Add Semantic Evidence Protocol V2 beside V1

V1 remains frozen for bounded migration and receives no new evidence kinds. V2
uses a source-qualified envelope containing protocol version, evidence identity,
capability/package manifest references, observation reference, scope, freshness,
typed provenance, and one closed candidate payload.

Initial candidate payloads are:

- `ContainerIdentityCandidateEvidence`
- `ElementAffordanceCandidateEvidence`
- `ContainerRelationCandidateEvidence`

Semantic symbols are typed manifest references, not unvalidated scenario
strings. Element occurrences reference a current source observation occurrence;
they are never selectors or direct action targets. Agent must re-ground and
authorize against fresh evidence.

Alternative considered: extend V1's `Candidate` string and reserve enum. Rejected
because it cannot prevent scenario strings or executable semantics from being
smuggled through fields that Runtime treats generically.

### 3. Coverage is a requirement descriptor, not SemanticEvidence

`CoverageRequirementDescriptor` belongs to versioned capability admission or a
Strategy criterion binding. It describes finite required evidence categories and
scope but has no satisfaction or completion field. Runtime accumulates generic
inventory evidence; Agent alone evaluates GoalEvidence and terminal completion.

Alternative considered: `CoverageRequirementEvidence`. Rejected because a
requirement is not an observation and combining them creates shadow completion
authority.

### 4. Perception sources have non-interchangeable tiers

Vision/screenshot perception is primary. ADB UI hierarchy is an optional
auxiliary contributor. Its batch carries source id, availability, capture time,
observation/frame association, display geometry, and raw structural facts.

Semantic processing and fusion preserve the lowest source tier supporting a
claim. ADB-only derived semantics remain auxiliary. Missing ADB never invalidates
an otherwise sufficient visual observation; missing required Vision cannot be
repaired by silently promoting ADB.

Alternative considered: treat both as equal `StructuredElementEvidence`
producers. Rejected because consumers cannot distinguish reliability,
availability, or provenance and may accidentally rely on ADB as completion or
action authority.

### 5. Coordinate normalization follows the producer, not one service

The Observation contract owns the canonical full-screenshot `[0,1] x [0,1]`
frame. Vision maps its model/crop coordinates; the ADB adapter maps UIAutomator
pixel bounds. Both use the same conformance tests and retain frame metadata.

ADB normalization is a mechanical acquisition transform only. Page-title,
Preference-row, toolbar, parent-return, and widget-role interpretation moves to
external semantic bindings.

Alternative considered: send ADB bounds through Vision only for normalization.
Rejected because it makes Vision own a non-visual source and couples ADB
availability to the Vision service without improving evidence authority.

### 6. Existing production components split by mechanism and knowledge

- `InteractionAffordanceAnalyzer`: retain fail-closed generic reduction; replace
  raw Settings/Android rules with admitted V2 affordance candidates.
- `Agent.SemanticRun`: retain bounded closed-loop and fresh verification; remove
  Settings timing/row/type calibration from semantic decisions.
- `Agent.OpenWorld`: retain DFS, uniqueness, authorization, and post-return
  verification; consume admitted relation/return candidates instead of matching
  Settings-derived labels.
- `AdbUiHierarchySource`: retain dump acquisition, primitive parsing, source-local
  geometry mapping, and availability; remove semantic roles and emit auxiliary
  provenance.
- `PhysicalHostComposition`: retain generic composition; replace Settings defaults
  and arbitrary semantic resolver callbacks with capability discovery. Physical
  baselines move to Harness.
- `SemanticActionLowerer`: retain authorized lowering and safety checks; consume
  a typed, freshly grounded control role rather than a free provider label.
- `SemanticCandidate`, vector implementation, Corpus, benchmark, and evaluator
  infrastructure move with external semantic/evaluation packages. Runtime sees
  only admitted V2 evidence.

### 7. Authority is protected structurally and behaviorally

Semantic packages and bindings receive no Agent, Traversal, FSM, GoalEvidence,
Recovery, or Run-start dependency. Their outputs cannot represent actions,
commands, completion, or callbacks. Runtime admission/fusion cannot dispatch.
Agent independently authorizes every candidate-derived action and verifies the
fresh post-action world before GoalEvidence or lifecycle change.

ADB-only authority is prohibited both before and after semantic interpretation.
A semantic provider cannot launder an auxiliary source into primary evidence.

### 8. Source grounding is canonical, source-neutral, and Vision-first

Runtime owns a pure `SourceGroundingNormalizer` that projects the current
immutable `Observation` into immutable `CanonicalObservationOccurrence`
records. The normalizer performs only source/capture correlation and primitive
geometry/occurrence normalization. It does not classify Settings, infer an
affordance, authorize a target, or mutate WorldBelief.

Each canonical occurrence carries:

- observation sequence and common frame reference;
- canonical observation-scoped occurrence identity;
- primary source-local occurrence reference when supported by Vision;
- zero or more auxiliary source-local corroboration references;
- current canonical full-frame bounds when available;
- source tier and complete provenance;
- an explicit `HasPrimarySupport` / authorization-eligibility result derived
  structurally from source support, never from confidence or scenario labels.

A Vision occurrence is independently canonical and groundable. ADB absence
does not remove or invalidate it. An auxiliary-only occurrence may be retained
for diagnostics and reconciliation, but it is never eligible for Agent action,
identity, coverage, parent-return, or completion proof.

Cross-source corroboration may attach auxiliary metadata to a primary
occurrence only when the current-frame correlation is deterministic and
unambiguous. Ambiguous correlations remain separate. Corroboration never
changes the primary occurrence identity or source authority.

The Semantic Capability returns typed evidence referencing the primary
source occurrence when primary support exists. It MUST NOT return an auxiliary
occurrence with primary provenance. Runtime admission rejects provenance and
occurrence-source mismatch.

Alternative considered: create synthetic Vision elements from structured
fixtures. Rejected because it launders auxiliary acquisition into primary
evidence and prevents tests from detecting a mandatory ADB dependency.

### 9. DFS consumes canonical evidence without changing authority

`InteractionAffordanceAnalyzer`, source-equivalence normalization, source
grounding, and parent-return resolution consume canonical occurrences rather
than indexing `Observation.StructuredElements` directly.

The change is representational:

- DFS still discovers candidates and controls execution order.
- Agent still validates freshness, uniqueness, current bounds, continuity, and
  authorization.
- Traversal still performs the concrete action and verification protocol.
- Semantic Capability still provides advisory typed interpretation only.

Parent-return is valid only from a fresh, unique canonical occurrence with
primary Vision support and admitted typed relation/affordance evidence. ADB may
corroborate that occurrence, but ADB-only parent-return evidence fails closed.

Source equivalence remains an Agent-run-local generic mechanism. It may use an
opaque primitive signature from the canonical primary occurrence, but it MUST
NOT branch on scenario vocabulary or use auxiliary-only evidence to create a
logical source.

## Risks / Trade-offs

- [Typed V2 becomes an over-general framework] → Initially admit only identity,
  affordance, and relation candidates bought by existing scenarios.
- [Scenario strings hide inside diagnostics] → Diagnostics are non-authoritative
  trace metadata and Runtime never branches on them; symbols must resolve through
  a registered manifest.
- [ADB evidence is accidentally promoted during fusion] → Carry source tier in
  immutable provenance and test derived-evidence tier preservation.
- [Removing ADB semantics reduces current scenario recall] → Preserve
  SETTINGS-TREE-01 as a package-level characterization test and fail closed while
  external bindings are migrated.
- [Vision outage is masked by ADB] → Require explicit primary-source readiness for
  buyers that need visual evidence; ADB cannot satisfy that readiness.
- [PhysicalHost setup bypasses Agent] → Move device baseline preparation to an
  isolated validation Harness and guard it from production Run entrypoints.
- [Parallel V1/V2 causes ambiguity] → V1 receives no new kinds; each admitted
  binding declares exactly one protocol major per response.

## Migration Plan

1. Freeze this OpenSpec design and add characterization and forbidden-edge
   guards before changing behavior.
2. Add V2 contracts, capability manifests, source-qualified evidence batches,
   admission, and fail-closed tests without wiring new actions.
3. Add primary/auxiliary source metadata; make ADB unavailability non-fatal to a
   sufficient visual observation and prove ADB-only non-authority.
4. Extract platform and Settings knowledge into external packages and move Fast
   semantic/corpus/evaluation implementations out of Runtime execution assembly.
5. Rewire World and Agent consumers to admitted typed evidence while preserving
   all existing decision and lifecycle owners.
6. Introduce canonical source occurrences, migrate generic DFS grounding from
   structured indices to canonical primary-supported references, and remove all
   synthetic-Vision fixture projection.
7. Replace PhysicalHost Settings defaults with capability discovery and move
   physical scenario preparation to Harness.
8. Run independent Vision-only, Vision-plus-ADB, and ADB-only rejection proofs;
   then run SETTINGS-TREE-01 with the external package plus a Generic Fake World with
   no scenario package, followed by all authority, Strategy, RuntimeAgent,
   OpenWorld, consistency, and OpenSpec validations.
9. Have Sol independently verify the boundary before Strategy or Semantic
   graduation resumes.

Rollback is staged: V2 wiring remains additive until a consumer is migrated;
each migrated slice can temporarily return to its frozen V1 path without
changing Agent/FSM/GoalEvidence authority. Embedded scenario behavior is removed
only after its external-package characterization is green.
