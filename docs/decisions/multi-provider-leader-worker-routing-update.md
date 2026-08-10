# MULTI_PROVIDER_LEADER_WORKER_ROUTING_UPDATE_RESULT

> Generated: 2026-08-09
> Role: Project Leader / Development Protocol Maintainer
> Change: Provider-neutral role-based model routing across OpenAI and Claude
> Type: PROCESS / ROUTING — no Runtime, architecture, or semantic changes

---

## Routing Model

**ROLE_BASED_MULTI_PROVIDER**

```text
LEADER DECIDES. WORKER EXECUTES.
```

模型供应商不决定职责。职责先定义，再映射到具体 provider/model。

---

## Canonical Roles

### PROJECT_LEADER_MODEL

- **Tier:** `leader` (HIGH_REASONING)
- **Authority:** Canonical decisions
- **Owns:** Lane selection, semantic commitment, worker dispatch, auto-continue, Architecture Fit, Gate judgment, scope, ownership, authority, canonical state transitions
- **Provider mapping:**
  - OpenAI: `GPT-5.6 Sol`
  - Claude: `Claude Opus`

### EXECUTION_WORKER_MODEL

- **Tier:** `fast` / `standard` (LIGHTWEIGHT)
- **Authority:** Bounded execution
- **Performs:** Implementation, testing, diagnosis, repair, evidence collection, docs reconciliation
- **Must NOT commit:** New CP, new RM, semantic/architecture/ownership/authority/invariant/safety/scope expansion
- **Provider mapping:**
  - OpenAI: `GPT-5.6 Luna`
  - Claude: `Claude Haiku`

---

## Provider Mappings

| Provider | PROJECT_LEADER_MODEL | EXECUTION_WORKER_MODEL | Notes |
|---|---|---|---|
| **OpenAI** | GPT-5.6 Sol | GPT-5.6 Luna | Sol ≈ Opus, Luna ≈ Haiku (role equivalence, not technical identity) |
| **Claude** (Anthropic) | Claude Opus | Claude Haiku | Fable for main-session leader; Opus for subagent leader |
| **Codex** | GPT-5.6 Sol | GPT-5.6 Luna | Shares OpenAI model identifiers |

---

## Decision Authority

**PROJECT_LEADER_ONLY.** Workers may detect or recommend semantic/architecture/ownership/authority changes. They must return evidence + exact escalation reason to PROJECT_LEADER_MODEL. A worker escalation request is not a Hard Gate decision. Only PROJECT_LEADER_MODEL commits canonical state.

## Worker Authority

**BOUNDED_EXECUTION.** Worker completion ≠ canonical task completion. Worker output is evidence/input for PROJECT_LEADER_MODEL.

---

## Mixed-Provider Support

**SUPPORTED** (subject to current Harness capabilities). The logical role model permits:

- Claude Opus Leader → GPT-5.6 Luna Worker
- GPT-5.6 Sol Leader → Claude Haiku Worker

provided role authority remains unchanged and worker capability is sufficient for the bounded task. Provider identity does not imply authority — authority derives from role, not model name.

---

## Routing Priority

```text
1. Determine required ROLE
2. Resolve configured provider
3. Resolve concrete model
4. Execute

Never: model name → infer authority
Always: authority role → model mapping
```

---

## Fast Lane

**AUTO_CONTINUE.** Default behavior for CAPABILITY_DELIVERY_FAST lane unchanged. Ordinary implementation/test failures auto-continue.

## Hard Gates

**UNCHANGED.** All canonical Hard Gates preserved:

- `HG-SEMANTIC`
- `HG-ARCHITECTURE`
- `HG-SAFETY`
- `HG-HUMAN`
- `HG-VALIDATION`
- `HG-SCOPE`

## Human Gates

**UNCHANGED.** Human interaction required only for real semantic commitments, architecture/ownership/authority changes, safety-semantic changes, and explicitly reserved governance decisions.

---

## Updated Files

| File | Change |
|---|---|
| `.ai/model-routing.yaml` | v2 → v3: added canonical provider-agnostic roles, OpenAI provider mapping, mixed-provider support, provider mapping section |
| `.ai/agent-routing.md` | Added `PROJECT_LEADER_MODEL` and `EXECUTION_WORKER_MODEL` to Portable Role Map with OpenAI adapter column. Added Development Lane Routing section. |
| `.ai/development-protocol.md` | Added §3 Provider-Neutral Model Routing with canonical roles, provider mapping, worker limits, mixed-provider support, routing priority |
| `.ai/task-contract.md` | Added `DevelopmentLane`, `AcceptedSemanticEnvelope`, `AllowedProductionScope`, `HardGatePolicy`, `SuccessCriteria`, `AutoContinue` fields |
| `.ai/result-contract.md` | Added Fast Lane Worker Results table, `FAST_LOOP_RESULT` format, provider-neutral consistency rules |
| `.ai/auto-continue-contract.md` | Updated Routing section to reference provider-neutral `PROJECT_LEADER_MODEL` / `EXECUTION_WORKER_MODEL` |
| `docs/decisions/multi-provider-leader-worker-routing-update.md` | This artifact (NEW) |

---

## Backward Compatibility

- All existing Human Gate semantics preserved
- All existing Hard Gate semantics preserved
- All existing Architecture Invariants preserved (I-1..I-14)
- Two-Lane Development Model preserved
- Fast Capability Delivery behavior preserved
- H4-3 auto-continue behavior preserved
- Task/result contract schemas backward-compatible (new fields are optional additions)
- Claude frontmatter `model` values (`opus`, `sonnet`, `haiku`) unchanged — platform enums remain

---

## Runtime Changes

**NONE**

## Architecture Changes

**NONE**

## Semantic Changes

**PROCESS_ONLY** — development protocol and routing configuration updated. No CP, RM, capability, or Runtime semantics changed.

---

## Validation

| Check | Result |
|---|---|
| PROJECT_LEADER_MODEL always high-reasoning | PASS — tier `leader`, `min_reasoning: highest_available` |
| EXECUTION_WORKER_MODEL bounded execution | PASS — tier `fast`/`standard`, explicit forbidden list |
| OpenAI mapping: Leader = GPT-5.6 Sol, Worker = GPT-5.6 Luna | PASS — configured in `.ai/model-routing.yaml` providers.openai |
| Claude mapping: Leader = Claude Opus, Worker = Claude Haiku | PASS — configured in providers.anthropic |
| Worker cannot acquire canonical semantic/architecture authority | PASS — `must_not_commit` list enforced in canonical_roles |
| Changing provider does not change development semantics | PASS — provider is execution policy, not project semantics |
| Fast Lane remains AUTO_CONTINUE | PASS — unchanged |
| Hard Gates unchanged | PASS — all 6 preserved |
| Human Gate behavior unchanged | PASS — unchanged |
| Runtime code unchanged | PASS — no src/ modifications |
| Architecture unchanged | PASS — no architecture changes |
| `.ai/model-routing.yaml` is canonical source | PASS — all other docs reference it |
| No hardcoded provider-specific model names in protocol docs | PASS — logical roles used throughout |

**All checks PASS.**

---

## Recommended Next Task

**CP12_MINIMUM_VERTICAL_SLICE_FAST_LOOP** — apply the Fast Lane protocol (with provider-neutral routing) to CP-12 / RM-10 / GC-03 capability delivery.

STOP.
