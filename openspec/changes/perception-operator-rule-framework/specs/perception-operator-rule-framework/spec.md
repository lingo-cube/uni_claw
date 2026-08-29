## Purpose

Defines the perception Operator & Cascading-Rule framework: generic, authority-classed,
deterministic, fail-closed composition operators whose parameters are bound by CSS-like
specificity-cascade rules keyed on five context dimensions plus supplementary tags, with a
validation-side learned-parameter lifecycle and governance-gated promotion. This framework
generalizes the row-composition class of perception problems (IR-G0) instead of loosening
individual thresholds. It changes perception infrastructure only: Runtime semantics,
Strategy Contract, GoalEvidence, and SourceIdentity are untouched.

## ADDED Requirements

### Requirement: Operator contract

Every composition operator SHALL declare: a stable id and version; an authority class ∈
{GENERATOR, VALIDATOR, ADVISOR}; a typed input contract (which evidence tiers it may
read); a typed output contract carrying provenance; a bounded parameter schema (typed
min/max/enum with safe direction for validators); deterministic pure-function semantics
over (inputs, resolved parameters); and an explicit fail-closed contract for insufficient
inputs. Operators SHALL emit a trace (input fingerprint, resolved parameters with rule
provenance, each decision or fail-closed reason) sufficient for offline replay.

#### Scenario: Operators are deterministic and replayable

- **WHEN** the same frame inputs and the same rule-set hash are resolved and executed
- **THEN** the operator produces byte-identical outputs and traces

#### Scenario: Insufficient inputs fail closed

- **WHEN** an operator's declared preconditions are unmet (e.g., fewer confirmed anchors
  than the resolved `minAnchors`)
- **THEN** it emits an explicit Unresolved/NoOp outcome with reason and produces no
  identity candidate — never a guess

### Requirement: Authority classes constrain generation

Navigation/menu identity SHALL be generatable only by GENERATOR operators (visual row
grouping / relation head). Text-semantics operators, structured-hierarchy (XML)
operators, and VLM operators SHALL NOT be GENERATORS: text semantics may only veto or
downgrade confidence; XML is auxiliary corroboration only; VLM is offline annotation or
low-frequency advisory only and SHALL NOT enter the authorization path.

#### Scenario: Text semantics cannot create a menu row

- **WHEN** a text-relation operator finds a title/caption pair
- **THEN** it may mark conflict or lower confidence but never emits a navigation candidate

### Requirement: Pipeline topology is code-owned; rules parameterize only

The operator execution DAG (which operators run, in what order, and that every GENERATOR
output passes every applicable VALIDATOR) SHALL be declared in the operator registry and
MAY NOT be reconfigured by rules. Rules SHALL bind parameter values only, namespaced per
operator (`operatorId.paramName`; cross-operator sharing requires explicit alias).

#### Scenario: Validators cannot be bypassed by configuration

- **WHEN** any rule attempts to disable a VALIDATOR or move a validator parameter in its
  declared unsafe direction
- **THEN** the rule set fails validation at load time

### Requirement: Selector dimensions and canonical values

The selector dimensions SHALL be exactly: `system`, `systemVersion`, `app`, `appVersion`,
`device`, and supplementary `tags` (a `key=value` set). Canonical representations SHALL be
fixed (`systemVersion` as `api-<N>`; `app` as package name; `device` as hardware model;
serial and modes such as `display=triple-screen`, `locale`, `density`, `model`,
`scenario` live in tags). A dimension whose value is absent in the frame context SHALL
resolve to `default`; rules MAY pin `default` explicitly to match value-absent contexts.
Context SHALL be supplied by the caller (Adapter layer) with the analysis request; the
perception service SHALL consume it without querying the world.

#### Scenario: Version-less app matches a default pin

- **WHEN** `com.android.settings` reports no `appVersion` (context `appVersion=default`)
  and a rule pins `appVersion=default`
- **THEN** the rule is eligible to match that context, exactly as a concrete value would

### Requirement: Specificity cascade with intersection-scoped conflict detection

Rule matching SHALL be: a rule matches a context iff every pinned dimension equals the
context value (or `default`) and the rule's tags are a subset of the context tags.
Specificity SHALL equal the number of pinned dimensions (each tag entry counts 1). The
effective value of a parameter SHALL come from the matching rule with the highest
specificity. A conflict exists ONLY between two rules that (a) have equal specificity,
(b) define the same parameter with different values, and (c) have selectors with a
non-empty reachable intersection — i.e. some context can match both — while (d) no
higher-specificity rule matching that intersection defines the same parameter (which
would resolve the ambiguity). A conflict so defined SHALL be rejected at load time with a
diagnostic identifying the intersecting selectors and suggesting an explicit
higher-specificity rule over the intersection. Rules whose selectors are MUTUALLY
EXCLUSIVE (e.g. pinning different concrete values of the same dimension, including a
concrete value versus `default`) SHALL NOT be reported as conflicts. Rule file order
SHALL NOT affect semantics. Conflict detection MAY be conservative — when it cannot
prove that a same-specificity pair's intersection is empty or covered, it SHALL reject
(fail-closed direction). Every resolved value SHALL carry provenance (rule id, pins,
specificity).

#### Scenario: Version override beats system default

- **WHEN** a rule pinning `{system, systemVersion}` and a rule pinning `{system}` define
  the same parameter and both match
- **THEN** the two-pin rule's value wins, with provenance naming it

#### Scenario: Mutually exclusive rules are not a conflict

- **WHEN** one rule pins `{app: com.android.settings}` and another pins
  `{app: com.example.other}`, both defining the same parameter
- **THEN** no conflict is reported (no context can match both selectors)

#### Scenario: Uncovered equal-specificity clash over a shared intersection is a load error

- **WHEN** one rule pins `{system, systemVersion}` and another pins `{system, app}` with
  different values for `rowGrouping.minAnchors`, and their selectors intersect (a context
  exists matching both, e.g. that system+version+app), and no higher-specificity rule
  over that intersection defines the parameter
- **THEN** the rule set fails to load until an explicit intersection rule (e.g.
  `{system, systemVersion, app}`) or deduplication resolves the ambiguity

#### Scenario: Intersection covered by a higher-specificity rule is not a conflict

- **WHEN** the two rules above exist AND a third rule pinning
  `{system, systemVersion, app}` defines `rowGrouping.minAnchors`
- **THEN** the pair is accepted: every reachable intersection context resolves
  unambiguously through the higher-specificity rule

### Requirement: Tree is an organizational view

Rules MAY be organized and presented as a dimension-ordered directory tree for management
and review. The tree SHALL be presentation only: matching, override, and conflict
semantics SHALL be exclusively those of the specificity cascade above.

#### Scenario: Reorganizing the tree never changes resolution

- **WHEN** the same rule set is regrouped into a different directory-tree presentation
  (e.g., re-filed under `app/` vs `system/` view) without editing any rule's pins or
  params
- **THEN** every context resolves to identical effective values and provenance

### Requirement: Governed, diffable rule assets

The active rule set SHALL serialize to a deterministic, human-readable, diffable format
(stable key order; no timestamps or machine paths) with a schema version, and its content
hash SHALL be bound into the existing perception governance chain
(`configId → deploymentId → CURRENT-ACTIVE receipt`). Unpromoted rule sets SHALL NOT enter
runtime. A loader/linter SHALL reject: unknown operators/parameters, out-of-bounds values,
unsafe validator adjustments, dead rules (pinning values that cannot occur), and
complexity-budget overruns (per-operator active-rule count above the declared budget).

#### Scenario: Unpromoted candidate rules never run

- **WHEN** a candidate rule set exists only in the validation-side store
- **THEN** the perception service resolves against the receipt-bound active set only

### Requirement: Learned-parameter lifecycle and evidence thresholds

Learned parameters SHALL live in a validation-side store with records carrying
`{selector pins, operatorId, parameter, value, evidenceRefs, sourceCampaign/run, status
(ACTIVE/STALE/CONTRADICTED/SUPERSEDED/INVALIDATED), version, supersedes/supersededBy,
validityAssumptions}`. Admission SHALL be provenance-gated (citing real campaign or
offline-eval evidence, with the evidence set hashed). A supersession SHALL require a
minimum sample size and non-overlapping evidence intervals; otherwise the prior value is
marked STALE. The concrete threshold values and the proposal producer are DEFERRED design
inputs, to be fixed by a separate decision before any S5 authorization (S5 does not gate
Phase 2.6 re-entry). Conflicts resolve fresh-evidence-first; no API may re-activate downgraded
values or force-apply learned values at runtime. Promotion to production SHALL occur only
through a new governance config manifest and receipt switch with human approval;
rollback SHALL use the existing receipt mechanism, recording INVALIDATED on the learning
side.

#### Scenario: One observation cannot flip a parameter

- **WHEN** a single run proposes a value contradicting an ACTIVE learned value
- **THEN** no supersession occurs; the prior stays ACTIVE and the proposal is recorded
  below threshold (or marks the prior STALE if evidence weakens)

### Requirement: IR-G0 unblock slices with equivalence gates

S1 SHALL port the retained four-anchor row-grouping operator as `uniform-list-row-grouping`
with root-rule defaults equal to the current candidate values and SHALL be verified
behavior-identical (frame-level equivalence over the archived real-frame regression set,
including the v1n false-positive frames). S2 SHALL add a deterministic `row-relation-head` GENERATOR whose inputs are FROZEN to raw
visual regions (uncombined detector boxes and OCR text blocks) and their pairwise
geometric relation candidates (same-column, vertical adjacency/containment, overlap) —
it SHALL NOT consume already-established row groups (no identify-rows-to-identify-rows
circularity), and text semantics, XML, and VLM SHALL NOT be used to fabricate row
identity. Its output is a row-group PROPOSAL with head/satellite election that must pass
`spacing-verifier`, activating structured composition for low-anchor viewports without
text-role guessing; its acceptance SHALL include the v1n regression frames and
no-regression on four-anchor behavior. S3 (model-backed relation head) SHALL require a
separate Human Gate and SHALL NOT be entered automatically when S2 falls short — S2
shortfall STOPS at the fail-closed boundary and returns to the Human Gate. S4 SHALL wire
`text-relation-check` and `structured-corroboration` as validators that only
veto or downgrade confidence and SHALL NOT fabricate candidates. S5 (learning loop) is
DEFERRED behind a separate post-S2 decision and SHALL NOT block Phase 2.6 re-entry; its
minimum sample sizes, evidence intervals, and proposal producer are deferred design
inputs to be fixed before any S5 authorization. Phase 2.6 SHALL re-enter Stage A only
after S2 or a separately authorized S3 delivers exactly one navigation candidate per
visual row on the regression frames.

#### Scenario: S1 is behavior-identical to the retained candidate

- **WHEN** the S1 port resolves the archived real-frame regression set with the
  receipt-bound root defaults
- **THEN** its composed candidates are identical to the retained candidate's outputs on
  every frame, and the v1n misjudgment frames remain fail-closed

## Explicit Non-Claims

This change does not include: Runtime normalization changes; XML as identity source;
text semantics or VLM generating actionable menus; automatic production parameter
push (promotion is always human-approved); general UI understanding beyond each
operator's declared composition semantics; Phase 2.6 re-entry without the S2/S3 gate.
