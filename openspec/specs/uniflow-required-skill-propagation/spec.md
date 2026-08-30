# uniflow-required-skill-propagation Specification

## Purpose

让 UniFlow 把 Leader 选中的项目 Skill 可靠地传递给 Codex 与 DSH Worker，并要求 UI/Runtime 问题先建立人类可理解的界面操作假设，再进入必要代码证据链。

## Requirements

### Requirement: WorkItem carries ordered required Skill names

UniFlow SHALL allow a WorkItem to carry an ordered, duplicate-free `required_skills`
array of project Skill names. The field SHALL be additive and backward compatible:
an omitted field is interpreted as an empty list, while every newly built WorkItem
SHALL emit the field explicitly.

#### Scenario: New WorkItem preserves selected Skills

- **WHEN** Leader builds a WorkItem with two selected Skills
- **THEN** serialization preserves both names in Leader-defined order without merging or fanout

#### Scenario: Legacy WorkItem remains readable

- **WHEN** a pre-existing WorkItem omits `required_skills`
- **THEN** validation treats it as no selected Skill without mutating the stored record

### Requirement: Required Skills resolve from trusted repository sources

The WorkItem validator SHALL accept only valid Skill names and SHALL resolve each
name from the repository-owned `.ai/skills` root. Missing, duplicated, malformed,
unreadable, frontmatter-mismatched, or non-Skill entries MUST fail before
Worker execution. Caller-supplied absolute paths, traversal paths, and `.agents`
adapter paths MUST NOT become Skill truth sources.

#### Scenario: Valid project Skill resolves

- **WHEN** `required_skills` names a unique Skill with a readable `SKILL.md`
- **THEN** ModuleContext contains the canonical repository-relative `SKILL.md` path

#### Scenario: Invalid Skill fails closed

- **WHEN** a required Skill is missing, malformed, duplicated, unreadable, or its frontmatter name mismatches
- **THEN** dispatch is rejected before Worker file modification with concrete validation evidence

### Requirement: Codex and DSH Workers receive the same Skill context

All UniFlow Worker execution profiles SHALL receive the resolved required Skill
paths in ModuleContext. Worker adapters MUST fully read every selected `SKILL.md`
before diagnosis, implementation, test authoring, verification, or semantic analysis.
A Skill remains `Authority: NONE` and MUST NOT expand WorkItem scope, permissions,
contracts, or lifecycle authority. Skill content SHALL participate in the context
digest so a Skill change invalidates stale Worker context.

#### Scenario: Codex Worker loads selected Skill

- **WHEN** a Codex WorkItem selects `evidence-driven-debugging`
- **THEN** the Worker instructions require the canonical Skill body to be read before action

#### Scenario: DSH envelope and manifest preserve selected Skill

- **WHEN** DSH dispatches a WorkItem with `required_skills`
- **THEN** the WorkItem remains unchanged in the envelope and the Worker payload carries the ordered canonical paths, complete Skill bodies, and fail-closed loading directive

#### Scenario: Delayed DSH dispatch remains self-contained

- **WHEN** DSH CLI records a dispatch for session-side Worker spawn
- **THEN** the dispatch record preserves the complete validated Worker payload rather than only Skill names or a context digest

#### Scenario: DSH rejects incomplete Skill payload before spawn

- **WHEN** a selected Skill is absent from the Worker payload or its path/order differs from ModuleContext
- **THEN** DSH rejects the spawn as `REQUIRED_SKILL_UNAVAILABLE` before Worker action

### Requirement: Bug routing selects the minimum debugging Skill set

For a Bug or failure-investigation WorkItem, Leader MUST select
`evidence-driven-debugging`. For Runtime, FSM, Traversal, Recovery, asynchronous,
real-device, flaky, or nondeterministic behavior, Leader MUST additionally select
`runtime-behavior-debugging`. The selection is an execution method and MUST NOT
be inferred as architecture or modification authority.

#### Scenario: Runtime UI failure receives both debugging Skills

- **WHEN** Leader dispatches a Runtime UI behavior failure
- **THEN** the WorkItem orders `evidence-driven-debugging` before `runtime-behavior-debugging`

### Requirement: Leader performs a bounded Reality Preflight

Before semantic attribution, architecture judgment, or deep code analysis, the
Leader MUST establish a concise, falsifiable working view from the user-visible
goal, current observable state, shortest human-feasible path, expected visible
transition, observed gap or unknowns, and nearest falsifier / First Divergence.
The working view MUST guide evidence entry and Owner routing without becoming a
Fact, contract, Runtime belief, fixed interaction script, or modification authority.
When direct UI evidence is unavailable, the Leader MUST mark it unknown rather
than inventing visible state.

#### Scenario: Leader resists premature semantic or code depth

- **WHEN** a UI or Runtime behavior request could trigger broad semantic attribution or a long code-chain investigation
- **THEN** the Leader first records the bounded Reality Preflight and enters only the minimum owning seam indicated by evidence

#### Scenario: Non-UI work does not invent a screen state

- **WHEN** the task has no relevant visible interface evidence
- **THEN** the Leader marks UI state as unavailable and uses the nearest repository, result, or trace falsifier without manufacturing a UI path

### Requirement: UI-first reasoning precedes long code-chain analysis

The debugging and UniAgent evolution Skills SHALL first describe the user-visible
goal, current UI state, and shortest plausible human interaction path as a falsifiable
hypothesis. They SHALL compare that hypothesis with observed evidence and enter code
analysis at the First Divergence Point rather than following an unbounded call chain.
They MUST NOT convert coordinates, fixed click sequences, incidental labels, timing,
or one observed UI path into Runtime authority or scenario knowledge.

#### Scenario: UI failure is framed from human-visible behavior

- **WHEN** a button-driven UI flow fails
- **THEN** the Worker states the visible goal, current screen, plausible shortest human path, observed divergence, and evidence before proposing a code owner

#### Scenario: UI hypothesis does not micromanage Runtime

- **WHEN** the human interaction hypothesis contains a concrete observed path
- **THEN** the Worker uses it as falsifiable evidence and does not hard-code coordinates or a fixed action script
