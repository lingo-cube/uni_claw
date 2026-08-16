# Spec: dsh-uniclaw-control-plane-protocol-baseline

> Spec-driven definition of the frozen DSH↔UniClaw control-plane protocol baseline.
> Every requirement is auditable against [source-evidence-matrix.md](../../source-evidence-matrix.md) and
> [integration-matrix.md](../../integration-matrix.md). Source is authoritative at
> `UNICLAW_DSH_COMPATIBILITY_BASELINE = 47f943859bef60e4160492346772ded9b24f765a` (0.1.0-rc.5).

## ADDED Requirements

### Requirement: DSH compatibility baseline pinned

The change MUST pin the exact DSH baseline the entire mapping is designed against.

#### Scenario: Baseline names commit, version, and branch

Given the pinned DSH checkout at commit `47f943859bef60e4160492346772ded9b24f765a`,
When the baseline is read,
Then it reports DSH version `0.1.0-rc.5`, branch `master`, remote `deepseek-ai/deepseek-harness`,
and the note that the pinned checkout has no git tags (pre-release, no compatibility promise).

#### Scenario: Source is authoritative over docs

Given a docs claim that conflicts with pinned source (recorded D1–D7),
When a mapping decision is made,
Then the decision follows the pinned source and the discrepancy is recorded in the source-evidence matrix.

### Requirement: Source evidence for every major mapping

Every major mapping decision (observability, control, cognition, UI, transport, process lifecycle) SHALL cite at least one source-evidence-matrix row verified against the pinned checkout.

#### Scenario: Mapping row cites verified source

Given the IntegrationMatrix row for "Human control Start Run",
When the row's DSH source evidence is inspected,
Then it cites the commands registry surface (S7) whose source file and handler-without-model semantics
were verified in the pinned checkout.

#### Scenario: Unverified claim is rejected

Given a mapping claim with no source citation,
When the change is validated,
Then the claim is rejected as unsupported (F2).

### Requirement: No parallel UniClaw↔DSH protocol invented

The change MUST NOT define a custom wire protocol or custom semantic envelope for the control plane
unless the audit proves a required semantic has no DSH-native seam. No such gap is found in this baseline.

#### Scenario: Session-event vocabulary is the native seam

Given a Kernel-fact or proposal artifact that must cross into DSH,
When the DSH-native surface is chosen,
Then it is the merge-extensible `SessionEventMap` extended by a plugin-declared event type
(source-evidence S1/S2/S3), NOT a parallel protocol.

#### Scenario: No custom envelopes frozen

Given the candidate custom envelopes (DecisionRequest / DecisionResponse / HealingRequest / DiagnosisRequest),
When the mapping is frozen,
Then none of them is defined; proposals ride plugin-declared session events and DriverHost admission.

### Requirement: Kernel authority preserved

The change MUST NOT grant DSH any execution/state authority. Kernel remains sole owner of run execution
state, Container state, Traversal state, bindings, grounding, authorization, physical dispatch,
post-action verification, recovery validity, GoalEvidence, and completion.

#### Scenario: DSH never creates GoalEvidence

Given a GoalEvidenceProduced event from the Kernel,
When DSH records it,
Then DSH records the Kernel-reported fact only (F6); no DSH component synthesizes or completes GoalEvidence.

#### Scenario: DSH never dispatches physically

Given a DSH cognition output that resembles an action,
When it is handled,
Then it is a proposal artifact only; the only physical dispatch path is the Kernel's (F5).

#### Scenario: DSH completion opinion is not Kernel completion

Given a RunCompleted/RunFailed event,
When DSH appends it,
Then DSH records the Kernel-reported completion fact; DSH's own opinion never declares Kernel completion.

### Requirement: No second mutable Runtime state owner

All DSH-side copies of Kernel state MUST be read-only projections or fold-derived caches; DSH MUST NOT
own a mutable mirror of Kernel runtime state.

#### Scenario: RunSnapshot copies stay read-only

Given a RunSnapshot read,
When it crosses into DSH,
Then it arrives via a read-only service, a projection unit, a command result, or a UI data source —
never into a writable DSH-owned Runtime state (F7).

#### Scenario: Projection cache is a fold shortcut, not truth

Given the `session_projcache` checkpoint,
When it is read,
Then it is treated as a fold shortcut with explicit watermark (`ver`, `seq`), never as Kernel truth.

### Requirement: Human deterministic control does not invoke models

Human control operations (Start/Pause/Resume/Stop/Abort/Inspect/Retry/Recovery/Requirement/Goal submission) SHALL run through the DSH commands registry whose handlers execute without a model turn (F8).

#### Scenario: Command executes without model turn

Given a user invokes `/uniclaw start`,
When the command handler runs,
Then it executes against the receiving agent without sending the command to the model (source-verified S7),
consuming zero model tokens.

#### Scenario: Control audit trail is durable

Given a command execution,
When it settles,
Then `command/run` and `command/done` durable session events record the lifecycle (S8).

### Requirement: Observability durability is buyer-gated

A RuntimeEventKind SHALL be copied into durable DSH session state ONLY when a concrete buyer is named; otherwise the kind MUST stay live-only or read-model-only.

#### Scenario: TrapRaised durable with buyer

Given a TrapRaised event,
When durability is decided,
Then it is durable + live because the buyer (human inspection, pause/resume context, post-run audit) is concrete.

#### Scenario: High-volume telemetry stays live-only

Given ObservationProduced / ContainerReconciled / NavigationDecision / ViewportExplorationDecision events,
When durability is decided,
Then they are live/read-model/UI-only with no durable copy, because no buyer exists (I-15, token economy).

#### Scenario: Graduated semantics not weakened

Given the graduated RuntimeEvent/RunSnapshot/EvidenceRef contracts,
When this change is applied,
Then none of them is modified; DSH consumes them as-is (F15).

### Requirement: Transport decision deferred without source pressure

The change MUST NOT select a DSH↔DriverHost transport without source pressure (F9).

#### Scenario: Transport is TRANSPORT_DEFERRED

Given the audit of DSH native seams (in-process, browser, inbound automation),
When the transport decision is made,
Then it is `TRANSPORT_DEFERRED`; the carrier is selected at the adapter boundary in the plugin
implementation change against DriverHost's existing read surface.

#### Scenario: SDK/ACP not repurposed as control-plane carrier

Given the SDK (JSON-RPC) and ACP seams,
When they are classified,
Then they are recorded as inbound automation seams (S21/S22), not control-plane carriers.

### Requirement: Process lifecycle decision explicit

The change MUST state who owns the DriverHost/Kernel process lifecycle.

#### Scenario: DriverHost owns its process

Given the DriverHost process,
When lifecycle responsibility is decided,
Then DriverHost/Kernel process durability is UniClaw-side; dsh-plugin-uniclaw connects to it and never
launches or supervises it.

### Requirement: Shadow insertion point frozen, not implemented

The change MUST record the exact DSH-native hook where a later Shadow cognition change inserts, and MUST
NOT begin Shadow implementation (F10).

#### Scenario: Insertion point is a plugin live-event hook

Given Kernel read-only facts arriving at dsh-plugin-uniclaw,
When the Shadow insertion point is identified,
Then it is the plugin live-event hook (S6/S33) feeding a later DSH cognition agent that records
DSH-side artifacts (S1/S17); Kernel execution is unchanged and Kernel consumption of DSH output is zero.

### Requirement: Advisory boundary metadata seam confirmed

The change MUST confirm DSH native surfaces can carry the basis/provenance metadata an Advisory flow
needs, without designing the Advisory protocol.

#### Scenario: Session events carry basis and provenance

Given a future DSH candidate for Kernel admission,
When the metadata carrier is identified,
Then session events (S1/S2) and structured tool/command payloads (S7/S9) carry basis refs and provenance;
DriverHost admission and Kernel fresh-state revalidation remain Kernel-side.

### Requirement: Token economy preserved

The change MUST preserve I-15 (Deterministic Information Acquisition Priority) and MUST NOT grant the
model default access to full Runtime traces, RunSnapshot history, perception output, screenshots,
EvidenceRefs, or conversation.

#### Scenario: Structured read precedes model cognition

Given a DSH cognition need,
When context is assembled,
Then the deterministic read path (service/command/tool) supplies minimum sufficient context first;
model cognition only runs with an explicit buyer.

#### Scenario: No model-facing uniclaw tool pre-approved

Given the candidate `uniclaw.*` tools,
When the baseline evaluates them,
Then each is recorded with buyer/purpose/freshness/kernel-validation/authority/cost but NONE is approved
without a concrete buyer; hard-forbidden behaviors (raw ADB, raw coordinates, Container mutation,
StateBelief mutation, GoalEvidence creation, RunCompleted mutation) are rejected.

### Requirement: EvidenceRef logical identity preserved

EvidenceRef MUST be treated as a logical identity (metadata-first, raw evidence lazy/on-demand); the
change MUST NOT claim persistent EvidenceRef resolution is complete (F14).

#### Scenario: Logical identity, metadata first

Given an EvidenceRef,
When DSH handles it,
Then it is carried as a logical id in session events and structured payloads; metadata is returned first
and raw evidence only on-demand; no screenshots/trace bundles enter model context by default.

#### Scenario: Persistent resolution stays deferred

Given the graduated EvidenceRef contract,
When the baseline reports it,
Then persistent EvidenceRef resolution is recorded as DEFERRED, never claimed complete.

### Requirement: UI state is projection, not Kernel truth

Control-plane UI MUST render projection values with derived-where-marked labels; unavailable fields stay
absent (F13).

#### Scenario: UI renders projection labels

Given the Control Plane UI panel,
When it displays RunSnapshot-derived values,
Then derived fields are visibly flagged and absent fields stay absent; the UI never presents itself as
Kernel truth.

### Requirement: Roles frozen

DriverHost and dsh-plugin-uniclaw roles MUST be frozen exactly as specified in design.md §2, including
their MUST-NOT boundaries.

#### Scenario: DriverHost is not a cognitive framework

Given the DriverHost role definition,
When it is read,
Then it is the UniClaw-side integration boundary (read projections now; bounded Kernel control ops,
proposal admission, freshness validation, protocol adaptation later) and MUST NOT become a cognitive
brain, agent replacement, Container/Traversal owner, generic workflow engine, generic AI provider
registry, second mutable WorldState, or generic plugin platform (F12).

#### Scenario: Plugin is not a state owner

Given the dsh-plugin-uniclaw role definition,
When it is read,
Then it owns DSH-facing integration only (plugin lifecycle, commands, tools, services, events, client
modules, config, DriverHost translation) and MUST NOT own Kernel state, Container state, physical device
state, GoalEvidence, or Runtime completion.

### Requirement: OpenSpec artifacts complete and validated

The change MUST ship proposal.md, design.md, specs/dsh-uniclaw-control-plane-protocol-baseline/spec.md,
tasks.md, source-evidence-matrix.md, and integration-matrix.md, and MUST pass strict validation and
consistency checks.

#### Scenario: Strict validation passes

Given the completed change,
When `openspec validate dsh-uniclaw-control-plane-protocol-baseline --strict --no-interactive` runs,
Then it passes.

#### Scenario: Repo consistency passes

Given the completed change,
When `scripts/check-consistency.sh` runs,
Then it reports ALL PASS and no Runtime build/test is required for this OpenSpec-only change.

## MODIFIED Requirements

None — the graduated `dsh-kernel-read-only-observability` contracts (RuntimeEvent, RunSnapshot,
EvidenceRef) are unchanged; this change only consumes them.

## REMOVED Requirements

None.

## Naming and Metadata

- Change id: `dsh-uniclaw-control-plane-protocol-baseline`
- Target maturity: `DSH_UNICLAW_CONTROL_PLANE_PROTOCOL_BASELINE_FROZEN`
- Next gate: `PROJECT_LEADER_DSH_UNICLAW_CONTROL_PLANE_PROTOCOL_BASELINE_REVIEW`
- Future sequence: 2. this change → 3. `dsh-uniclaw-control-plane-plugin-implementation` →
  4. `dsh-shadow-cognition` → 5. `dsh-advisory-cognition` → 6. bounded blocking seams only if later justified.
