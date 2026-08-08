# Task Contract Template

> Platform: Codex + Claude | Source: .ai/development-protocol.md
> This is the shared input interface for all AI Coding Agent tasks.
> Claude agents receive equivalent dispatch via their agent prompt; Codex consumes this contract inline.

---

## Contract Fields

```markdown
# Task Contract

Task ID:          <unique task identifier>
Role:             <portable role from .ai/model-routing.yaml>
Tier:             <leader | expert | standard | fast>
Scenario:         <SC-Px-xxx scenario name + Evidence Required IDs>
Goal:             <what must be achieved>
Required Semantic:<why this task exists — the capability gap it fills>
Approved Production Purchase: <Scenario Receipt reference or NONE if test-only>
Assertions:       <verifiable acceptance criteria>
Allowed Scope:    <files / directories / types allowed to modify>
Forbidden Scope:  <files / directories / types forbidden to touch>
Deferred Boundary:<capabilities explicitly NOT to implement>
Architecture Constraints: <invariants / frozen decisions / HG references>
Verification:     <build / test / guard / consistency commands>
Execution Preference: <optional: preferred_worker or preferred_route>
Return Contract:  <see .ai/result-contract.md>
```

---

## Execution Preference

Optional field. Expresses a preferred execution route.

```
preferred_worker: deepseek-flash-worker
```
or
```
preferred_route: runtime-coder
```

**Rules** (from `.ai/model-routing.yaml` workers section):
1. Task semantics must already be approved
2. Scope must be narrow
3. Task must be low architectural risk
4. Worker permissions must cover requested operation

If any condition is violated → ignore preference, escalate to runtime-coder or expert review.

Execution Preference is a **routing hint**, not an authority override.

---

## Role-Specific Behavior

The `Role` field in the Task Contract determines agent behavior.

### runtime-coder (standard)
Before editing: reload repository truth, read development-protocol.md, identify Task ID / Scenario / Required Semantic / Allowed Scope / Forbidden Scope / Deferred Boundary.

**Allowed**: edit within allowed scope, add required tests, local implementation refactor, fix implementation-caused compile/test failures.

**Forbidden**: invent new semantic, change Scenario, change approved OpenSpec SHALL, create unapproved architecture component, change ownership/authority/invariant, expand current Phase, implement deferred capabilities.

### runtime-validator (standard)
Fresh repository truth reload. Do not trust coder result. Do not edit production by default. Re-run scenario evidence, inspect production delta, inspect Scenario Receipts, inspect ownership/authority, inspect deferred boundary. Run build/tests/guards/consistency.

**Return**: PASS | CONDITIONAL_PASS | FAIL

**Independence**: if current session cannot provide adequate independence, report `VALIDATION_INDEPENDENCE_LIMIT` and request fresh session.

### phase-evolution-controller (standard)
Read current Phase truth. Select exactly ONE approved next task. Produce complete Task Contract. Choose route per model-routing.yaml. Receive Task Result. Classify result. Decide: next approved task / reroute / semantic review / architecture review / Human Gate / validation / slice done.

**Forbidden**: implementing production code, inventing Scenario, silently modifying OpenSpec, bypassing blocked status.

### scenario-architect (expert)
Define Scenario Contract (Given/When/Then/Evidence). Design deterministic Fake World. Derive minimum Vocabulary. Verify Architecture Invariants. Prevent coding agents from leaking semantic truth into production. Do not implement production code directly.

### openspec-researcher (fast)
Read-only retrieval, log parsing, symbol lookup, file search, summarization.

**Forbidden**: writing files, making architecture decisions, implementing code.

### project-leader (leader)
Top-level coordination, phase scope approval, major semantic review adjudication, architecture escalation resolution, Human Gate preparation, next Phase approval after validation.

---

## Complexity Purchase Check

Before introducing any new production artifact (type / field / enum / interface / component / mutable state / architecture dependency), the agent MUST verify:

> Is this explicitly purchased by the Task Contract or an existing Scenario Receipt?

- **YES** → continue
- **NO** → STOP. Return `BLOCKED_FOR_SEMANTIC_REVIEW`. Do not silently expand the model.

---

## Failure Classification

All agents MUST use exactly these statuses. Never silently reinterpret one category as another.

| Status | When |
|--------|------|
| `DONE` | Task completed per contract, all assertions verified |
| `BLOCKED_FOR_SPEC` | Approved normative contract is missing / contradictory / insufficient |
| `BLOCKED_FOR_SEMANTIC_REVIEW` | Current model cannot express required behavior; new semantic / field / component appears necessary |
| `BLOCKED_FOR_ARCHITECTURE_REVIEW` | Ownership / authority / dependency direction / architecture boundary requires reconsideration |
| `BLOCKED_FOR_HUMAN` | Invariant must change / frozen decision must change / Scenario and Spec cannot be reconciled / Human Gate condition reached |
| `ROUTING_UNAVAILABLE` | Required minimum tier cannot be satisfied by current execution route |

For blocked statuses, do NOT propose speculative implementation unless explicitly useful for reviewer context.

---

## Codex-Specific Notes

- Codex does NOT have Claude's custom agent spawning. When a Task Contract specifies a portable role, Codex executes that role **inline** in the current session.
- If Codex host supports per-subagent model selection, use it per model-routing.yaml. If not, use current task model + reasoning level. Record `ROUTING_CAPABILITY_LIMIT` when subagent spawning is unavailable.
- Codex reads AGENTS.md → .ai/development-protocol.md → .ai/model-routing.yaml → this Task Contract. The same shared protocol governs both platforms.
