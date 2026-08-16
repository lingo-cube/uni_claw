# dsh-shadow-cognition Specification

## Purpose
TBD - created by archiving change dsh-shadow-cognition. Update Purpose after archive.
## Requirements
### Requirement: Shadow Cognition authority boundary

The DSH Shadow Cognition capability SHALL observe Kernel-produced evidence and
run state, optionally invoke cognitive/model reasoning, and produce DSH-side
interpretation artifacts for human inspection, while Kernel consumes ZERO
shadow outputs. The capability SHALL have no execution authority, no
authorization authority, no GoalEvidence authority, no Container authority, no
StateBelief authority, no Binding authority, and no Runtime state-transition
authority. The boundary SHALL be enforced architecturally, not only
documented: the frozen wire contract contains zero mutation methods, the plugin
owns no physical capability, and `src/UniClaw.Runtime` is never modified.

#### Scenario: Shadow has no execution path to Kernel

Given a Shadow analysis artifact that recommends an action such as "tap X",
When the artifact is produced and presented,
Then the Kernel performs nothing — the frozen wire contract exposes no
mutation method the plugin could call, and no Kernel-side consumer of shadow
outputs exists.

#### Scenario: Shadow cannot create GoalEvidence

Given a `ShadowAnalysis` artifact,
When every code path from the artifact to Kernel state is inspected,
Then there is no path that can create GoalEvidence — no wire method exists and
no file under `src/UniClaw.Runtime` is modified.

#### Scenario: Shadow cannot mutate Container, Binding, or StateBelief

Given a `ShadowAnalysis` artifact,
When every code path from the artifact to Kernel state is inspected,
Then there is no path that can mutate Container, Binding, or StateBelief
state — the read-only wire surface is the only DriverHost contact and it
carries no mutation method.

### Requirement: First buyer is post-hoc run interpretation

The first Shadow buyer SHALL be post-hoc / near-live run interpretation: a
human inspecting a UniClaw run through DSH asks what appears to be happening,
why the latest visible action was chosen, what evidence may explain a Trap,
what seems to block progress, whether observed behavior is consistent with the
Goal, and what to investigate next. Shadow SHALL produce hypotheses, diagnostic
summaries, candidate explanations, and human-facing recommendations — never
executable authority.

#### Scenario: Buyer questions map to artifact sections

Given a human requests interpretation of a run,
When the `ShadowAnalysis` artifact is produced,
Then its HumanSummary, ObservedFacts, Hypotheses, Uncertainties, and
Recommendations answer the buyer's interpretation questions, and none of its
outputs carry executable authority.

### Requirement: Minimal trigger model — V1 human.request only, auto triggers deferred

Shadow SHALL NOT invoke a model on every RuntimeEvent. In V1 the trigger model
SHALL be exactly: **human-requested analysis only** (mandatory, always
available) via `uniclaw-shadow-analyze <runId> [--focus …] [--reason …]`.
Non-terminal events such as `TrapRaised` and `RecoveryStarted` SHALL NOT
auto-trigger a model call in this baseline; trap interpretation SHALL be
human-requested. Terminal run-state triggers `run.failed` and `run.completed`
SHALL be DEFERRED, not built, until a consumer exists
(`AutoTriggersDeferredUntilConsumerExists = YES`): without a durable or
currently consumed native live Shadow surface, automatically generating
ephemeral analyses has no clear human buyer. The config key
`shadow.autoTriggers` SHALL remain reserved and SHALL be validated as empty
(`[]`) in V1; a non-empty value is a configuration error naming the deferral.
Human requests SHALL always produce a new analysis.

#### Scenario: TrapRaised leads to analysis on human request

Given a run that raised a Trap with bounded evidence,
When a human requests `uniclaw-shadow-analyze <runId> --focus trap`,
Then a Shadow analysis is produced within the bounded causal window and cites
the TrapRaised RuntimeEvent id (SHADOW-F1).

#### Scenario: Failed run analysis cites relevant RuntimeEvents

Given a run that failed,
When analysis is requested for it,
Then the artifact's ObservedFacts and Hypotheses cite RuntimeEvent ids from the
bounded window, including the RunFailed event (SHADOW-F2).

#### Scenario: No automatic invocation on any event in V1

Given a run producing a stream of RuntimeEvents,
When the plugin drains them,
Then no model call is triggered by any RuntimeEvent — terminal auto triggers
are deferred and unbuilt in V1, `shadow.autoTriggers` is reserved and must be
empty, and a model is invoked only when a human requests an analysis.

### Requirement: Read-only graduated source inputs and truthful unavailable data

Shadow SHALL consume only already-graduated read surfaces: `RunSnapshot`
(classification-preserving), `RuntimeEvent` pages (run-scoped cursors, stable
EventId), and `EvidenceRef` (logical locator). Shadow SHALL NOT require any new
Runtime emitter. Where data is unavailable or partial (for example full
GoalEvidence freshness source, CurrentObservationSequence,
CurrentContainerSummary, BindingsSummary, StateBeliefsSummary, or C-class
decision/authorization events), Shadow SHALL express explicit uncertainty and
SHALL NOT present fabricated inference as Kernel fact.

#### Scenario: Missing snapshot field yields uncertainty, not fabrication

Given a RunSnapshot that lacks field X,
When a Shadow analysis is produced from it,
Then `uncertainties` contains an entry with reason `missing-data` naming X, and
no `observedFacts` entry asserts X (SHADOW-F3).

#### Scenario: Unavailable EvidenceRef keeps the analysis truthful

Given an `EvidenceRef` that cannot be resolved (`evidence.get` fails or is
unavailable),
When a Shadow analysis is produced,
Then `uncertainties` contains an entry with reason `unresolved-evidence-ref`
and no claim asserts the unresolved content (SHADOW-F4).

### Requirement: Evidence hierarchy never collapses

Shadow reasoning SHALL distinguish KERNEL FACT, DERIVED READ MODEL, EVIDENCE
REF, SHADOW INFERENCE, and SHADOW RECOMMENDATION, and SHALL never collapse
these categories. Each `ObservedFact` SHALL carry classification
`kernel-fact` or `derived-read-model` with a reference; each `Hypothesis`
SHALL carry classification `shadow-inference`; each `Recommendation` SHALL be
human-facing only. A Kernel fact such as `ActionDispatched(Tap)` and a Shadow
inference such as "the action may have targeted the Wi-Fi toggle" SHALL remain
distinguishable.

#### Scenario: Facts and hypotheses remain distinguishable

Given any `ShadowAnalysis` artifact,
When its content is inspected,
Then every observedFact carries `kernel-fact` or `derived-read-model`, every
hypothesis carries `shadow-inference`, and no hypothesis is presented as a
Kernel fact (SHADOW-F13).

### Requirement: ShadowAnalysis output schema is bounded

Shadow SHALL produce one bounded `ShadowAnalysis` artifact with only
current-buyer fields: `analysisId`, `runId`, `sessionId`, `trigger`, `focus`,
`requestedAt`, `completedAt`, `classification` (always `COGNITIVE_INFERENCE`),
`evidenceRefs`, `observedFacts`, `hypotheses`, `uncertainties`,
`recommendations`, `humanSummary`, `model`, and `modelCall` accounting.
Shadow SHALL NOT introduce a confidence scoring framework, severity ontology,
memory system, planner state, execution proposal, approval status, or action
authorization.

#### Scenario: Artifact carries only bounded fields

Given the `ShadowAnalysis` artifact,
When its schema is validated,
Then every field is from the bounded set, and no confidence framework,
severity ontology, memory, planner, execution-proposal, approval, or
authorization field exists.

#### Scenario: Classification survives caching and presentation

Given an artifact held in the bounded process-local cache and rendered for a
human,
When its `classification` field is read,
Then it is `COGNITIVE_INFERENCE` — never `WORLD_TRUTH`, `KERNEL_FACT`,
`ACTION_AUTHORIZATION`, or `GOAL_EVIDENCE`. (V1 has no durable session event,
so no persistence survives; the classification is a mandatory constant field
of every artifact.)

### Requirement: Bounded context assembly with deterministic retrieval first

Before any model call, Shadow SHALL deterministically retrieve: the latest
`RunSnapshot`, a bounded recent `RuntimeEvent` window (capped by
`shadow.maxEvents`, default 200), trap detail when focus is trap, and
EvidenceRefs only lazily. The assembled model context SHALL be bounded
(`maxEvents`, `maxContextChars` default 80 000, exactly one snapshot,
`maxEvidenceRefs` default 8, bounded bytes per ref) and SHALL NOT accumulate an
unbounded transcript. Visual evidence SHALL be lazy: fetched only when the
analysis buyer requires it and `shadow.visual.enabled` is true (default false).

#### Scenario: Bounded causal window is enforced

Given a run with more than `shadow.maxEvents` recorded events,
When a Shadow analysis is produced,
Then the model input contains at most `maxEvents` events and at most
`maxContextChars` characters (SHADOW-F11).

#### Scenario: Visual evidence is lazy

Given an analysis focus without a visual need, or `shadow.visual.enabled=false`,
When the analysis is produced,
Then zero image or screenshot content is fetched; only a buyer-requiring focus
with the enabled flag fetches it (SHADOW-F12).

#### Scenario: Deterministic retrieval precedes the model

Given a Shadow analysis request,
When the model call happens,
Then the snapshot, bounded events, and lazily resolved EvidenceRefs were
retrieved deterministically before the call, and the model is not asked to
reconstruct facts already available.

### Requirement: EvidenceRef traceability

Every non-trivial Shadow claim SHALL be traceable to RuntimeEvent ids,
RunSnapshot field classifications, EvidenceRef logical locators, or exact
DSH-side source artifact references. Shadow SHALL prefer references over
copied bulky content, and SHALL resolve EvidenceRef content lazily only when
the buyer requires it.

#### Scenario: Claims carry references

Given an artifact containing ObservedFacts and Hypotheses,
When their references are inspected,
Then each non-trivial claim cites an evidence reference (RuntimeEvent id,
snapshot field, EvidenceRef locator, or source artifact), and bulky content is
not copied into the artifact unless the buyer requires it.

### Requirement: DSH-native model invocation via ctx.llm

Shadow SHALL invoke models through the existing DSH-native seam `ctx.llm`
(`LlmRuntime.stream(GenerateOptions)`), with provider and model selected by DSH
configuration (`shadow.model.provider`, `shadow.model.model`). Shadow SHALL NOT
introduce a new provider framework, custom WebSocket, parallel session model,
or custom agent runtime. Shadow SHALL make one-shot calls: no agent loop, no
tool loop, no loop-only markers, `purpose` left unset. Shadow SHALL register no
model-facing tools in this baseline. If no model is configured, Shadow SHALL
produce a deterministic read-only digest with `uncertainty: model-unavailable`.

#### Scenario: One-shot call through the pinned LLM seam

Given Shadow analysis is configured with provider and model,
When an analysis is produced,
Then exactly one `ctx.llm` stream call with a one-shot `GenerateOptions`
(provider, model, system, single user message, signal) is made, and no agent
loop or tool loop is started.

#### Scenario: No model configured degrades deterministically

Given Shadow analysis without a configured model,
When an analysis is requested,
Then the artifact is a deterministic read-only digest (facts plus cited
events) carrying `uncertainty: model-unavailable`, still classified
`COGNITIVE_INFERENCE`.

#### Scenario: No model-facing tools

Given the Shadow model invocation path,
When its tool configuration is inspected,
Then no tools are exposed to the model, so no mutation-capable tool surface
exists.

### Requirement: Ephemeral process-local durability — zero custom session events

ShadowAnalysis SHALL be **ephemeral and process-local** in V1
(`EPHEMERAL_PROCESS_LOCAL`). Shadow SHALL append **ZERO custom session
events**: no `shadow/analysis` session event exists, no other custom event
type is appended, and no `ignorable` envelope marker is required because no
custom event is appended at all (`DurableEventType = NONE`,
`UnknownCustomSessionEventsWritten = NONE`,
`UnknownNonIgnorableSessionEventsWritten = NONE`). The producing DSH session
log and its reload behavior SHALL be completely untouched by Shadow. A
completed ShadowAnalysis MAY live in a bounded process-local cache
(`Map<runId, bounded recent ShadowAnalysis>`, size-bounded, process-local,
non-authoritative, disposable, convenience only) that is explicitly NOT a
Memory, Knowledge Store, or History Database. Kernel truth SHALL NOT depend on
Shadow persistence (trivially: no Shadow persistence exists in V1). On DSH
restart, ephemeral ShadowAnalysis SHALL be lost truthfully: the cache is
empty, Kernel state is unaffected, lost analyses SHALL NOT be reconstructed or
fabricated as though persisted, and a fresh analysis SHALL be recomputable on
demand from the current legitimate Kernel read surfaces.

#### Scenario: Restart leaves the session log untouched

Given a Shadow analysis completes and DSH restarts,
When the pinned session is reloaded,
Then the reload is unaffected — no `shadow/analysis` event exists in the log,
zero custom session events were written, the Kernel run remains unaffected,
the lost ephemeral analysis is not reconstructed or fabricated, and a new
human request may recompute analysis from current legitimate read surfaces
(SHADOW-F15 rebaselined).

#### Scenario: Zero custom session events guard

Given the complete Shadow V1 implementation,
When its session-write surface is inspected,
Then zero custom session events are appended: no `shadow/analysis` event type
exists anywhere in the plugin source, and no direct or dual write to
`sessionPersistence` reproduces the sequence-aligned dual-write workaround
(design.md §9.3). An implementation that appends an unknown non-ignorable
event fails its own tests, because such an event would make the session log
refuse to load.

#### Scenario: Restart does not reset Kernel truth — truthful loss

Given DSH restarts during or after an analysis,
When the session resumes,
Then the Kernel run continues unaffected; the ephemeral ShadowAnalysis is lost
truthfully (no fake Shadow history is reconstructed); run facts are re-fetched
from DriverHost on demand; and a fresh analysis can be recomputed from current
legitimate read surfaces (SHADOW-F7 rebaselined — truthful loss is the PASS
criterion).

### Requirement: Human inspection surface via DSH-native command

Shadow SHALL expose its minimum human surface through a DSH-native command
`uniclaw-shadow-analyze` on the existing command registry, following the
graduated `uniclaw-*` naming convention, dispatched deterministically without
sending the command to the model, and returning the `ShadowAnalysis` as
structured text. Shadow SHALL NOT build a new frontend.

#### Scenario: Command follows the uniclaw convention

Given the command registry of the graduated plugin,
When `uniclaw-shadow-analyze` is registered,
Then it uses the `uniclaw-` kebab-case prefix, is session-scoped, dispatches
without a model turn, and returns the analysis text (HumanSummary first).

### Requirement: Failure isolation is fail-open relative to Kernel

All Shadow failures SHALL be fail-open and contained: model not configured,
model timeout, model error, context-assembly failure, EvidenceRef resolution
failure, bounded-cache write failure, and DSH restart SHALL NOT stop a
Kernel run, change a Goal, create a Kernel Trap, change Agent state, or change
completion. Each failure SHALL surface as an explicit status or uncertainty in
the artifact.

#### Scenario: Model timeout leaves Kernel unaffected

Given a model call that exceeds `shadow.timeoutMs`,
When the analysis completes,
Then the artifact status is `timeout` with `model-timeout` uncertainty, and
the Kernel run is unaffected (SHADOW-F5).

#### Scenario: Model error leaves Kernel unaffected

Given a model call that fails,
When the analysis completes,
Then the artifact status is `error` with `model-error` uncertainty, and the
Kernel run is unaffected (SHADOW-F6).

#### Scenario: Cache write failure does not block the caller

Given a bounded process-local cache write failure for the analysis,
When the analysis completes,
Then the artifact is still returned to the command caller, the failure is
logged and contained, and Kernel behavior is unchanged. (V1 appends zero
custom session events, so no session append/persistence failure path exists.)

### Requirement: Session and Kernel Run identity are never conflated

The relationship SHALL be one DSH session to zero, one, or many inspected
Kernel RunIds, established explicitly per analysis request. Shadow SHALL NOT
assert `SessionId == RunId`, SHALL NOT derive a RunId from a SessionId (or vice
versa), and SHALL NOT create a Kernel run from session creation.

#### Scenario: Identity fields stay separate

Given the command flow and the `ShadowAnalysis` artifact,
When identities are read,
Then `runId` and `sessionId` are separate explicit fields, no code asserts
their equality, and no Kernel run is created from session creation (SHADOW-F14).

### Requirement: Shadow-only, no Advisory

This change SHALL implement Shadow only: Kernel ignores the cognitive result.
Advisory cognition (Kernel or Agent consuming a cognitive proposal while
remaining final authority) SHALL be explicitly out of scope. Forbidden in this
change: `DecisionProposed`, `DecisionAccepted`, and `ActionAuthorized`
RuntimeEvents, proposal ingestion into Agent, and semantic action suggestions
consumed by Kernel.

#### Scenario: No advisory machinery exists

Given the Shadow baseline implementation,
When its Kernel-facing surface is inspected,
Then no Decision/ActionAuthorized event, proposal ingestion path, or
Kernel-consumed suggestion exists; the only direction is Kernel → DSH.

### Requirement: Zero new Runtime semantic emitters

Shadow SHALL add zero new Runtime semantic emitters. No file under
`src/UniClaw.Runtime` SHALL be modified by this change, and no new
RuntimeEvent class or emitter SHALL be introduced. All Shadow inputs come from
the frozen, already-graduated read surfaces.

#### Scenario: Runtime delta is zero

Given the complete Shadow baseline change set,
When `git status` and a source scan under `src/UniClaw.Runtime` are inspected,
Then zero files are modified and zero new RuntimeEvent classes or emitters
exist (SHADOW-F16).

### Requirement: Model call accounting without zero-call requirement

Shadow MAY invoke models, so model-call accounting SHALL record per analysis:
trigger, input EvidenceRefs, input event count, and context size (cheaply
available boundedness), plus provider/model, status, and timestamps. The
deterministic read-only information collection before the model SHALL remain
zero-model, and all existing `uniclaw-*` commands SHALL remain zero-model.

#### Scenario: Each analysis records one model call

Given a Shadow analysis that invokes the model,
When the accounting record is read,
Then it contains trigger, input EvidenceRefs, `inputEventCount`,
`contextChars`, provider, model, status, and timestamps.

#### Scenario: Deterministic collection is zero-model

Given a Shadow analysis request,
When the pre-model retrieval phase runs,
Then it makes zero model calls, and the `uniclaw-*` command surface stays
zero-model.

