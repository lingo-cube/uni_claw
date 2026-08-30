## Context

See `proposal.md`. UniFlow already has one portable WorkItem schema and shared
Profile/ModuleContext logic. Codex custom agents and the DSH adapter both consume
that contract, but Skill selection currently exists only in Leader/session context.
Existing WorkItems and recorded DSH envelopes must remain readable.

## Goals / Non-Goals

**Goals:**

- Make selected Skill names portable, deterministic, and fail-closed.
- Keep `.ai/skills` as the only canonical body root and `.agents/skills` /
  `.dsh/skills` as discovery adapters only.
- Include Skill bodies in the Worker context digest and expose resolved paths to
  both Codex and DSH.
- Make UI-first reality reasoning actionable without granting Runtime control.

**Non-Goals:**

- No automatic architecture decision, code-fix selection, Worker fanout, MCP,
  plugin, or new Runtime protocol.
- No semantic classifier that guesses Bug type from arbitrary Chinese text.
- No migration rewrite of historical WorkItems or DSH receipts.

## Decisions

### D1 — WorkItem stores ordered names, not paths

`required_skills` is an ordered array of lowercase kebab-case names. Names are
portable across Codex and DSH; paths are derived by the validator. This avoids
absolute-path leakage and keeps caller-controlled paths out of trusted context.
An array is required because general and Runtime-specific debugging methods may
compose; this does not create Worker fanout.

Alternative rejected: one `required_skill` string cannot represent the approved
general-plus-specialized composition. Arbitrary path objects enlarge the trust
surface and duplicate canonical location rules.

### D2 — Additive compatibility, explicit new output

The schema allows the field to be absent for recorded WorkItems. Validation
normalizes absence to an empty list in memory, while `build_work_item` emits an
explicit list for every new WorkItem. No historical evidence file is rewritten.

Alternative rejected: making the field immediately required would invalidate
active and recorded WorkItems owned by other workstreams.

### D3 — Unique resolution from canonical roots

The validator checks the name format, then resolves exactly
`.ai/skills/<name>/SKILL.md`. The file must exist and its frontmatter `name` must
match. Missing, unreadable, or mismatched entries fail before dispatch. `.agents`
and `.dsh` links are discovery adapters, never truth sources.

### D4 — ModuleContext owns resolved execution context

`build_context_manifest` accepts the selected names, resolves them, publishes
`context_sources.required_skills`, and includes their content in `RuleDigest`.
The manifest also carries ordered canonical Skill documents and a concise loading
directive so a DSH Host does not need a second resolver or an implicit repository
read. The CLI accepts `--work-item` so Codex adapters can build context from the
exact validated WorkItem. DSH `ModuleContextStore` passes the same names into the
same upstream builder; the envelope deep-copies the WorkItem, and delayed CLI
dispatch records persist the complete Worker payload for the session-side spawn.

### D5 — Worker adapters fail closed before action

All four Codex Worker adapters require full reads of resolved Skills before their
profile-specific action. An unresolved or unreadable required Skill returns
`BLOCKED_FOR_SPEC` with reason `REQUIRED_SKILL_UNAVAILABLE`; Skill instructions
remain below Contract/Invariant and cannot expand scope. DSH relies on the shared
dispatch validator and manifest rather than introducing a second Skill loader.
Its Host payload contains the same ordered documents and directive; missing or
inconsistent Skill payload is rejected before spawn. This proves adapter delivery,
not that a model followed the instruction; actual Host execution still needs a
Host receipt and integration evidence.

### D5.1 — Leader performs a bounded Reality Preflight

Before semantic attribution, architecture judgment, or deep code traversal, the
Leader records a short working view: user-visible goal, current observable state,
shortest human-feasible path, expected visible transition, observed gap/unknowns,
and the nearest falsifier or First Divergence. This is a navigation hypothesis,
not a Fact, contract, Runtime belief, or permission grant. The Leader stops widening
the evidence chain once the minimum owning seam is identified, then selects the
ModuleProfile, ExecutionProfile, and required Skills.

If UI evidence is unavailable, the Leader marks it unknown and uses the closest
available screen/trace/result evidence rather than inventing a screen state or
forcing every non-UI task through a UI script.

### D6 — UI-first is a falsifiable interaction hypothesis

The three relevant Skills gain concise, role-specific guidance: identify the
visible goal, current screen, shortest plausible human path, and expected visible
transition; compare with screen/trace evidence; enter code at First Divergence.
This is a reasoning aid, not a click script. Coordinates, fixed sequences, labels,
timing, and one scenario path remain forbidden as Runtime knowledge.

## Risks / Trade-offs

- **[Risk] Leader forgets to select a debugging Skill** → UniFlow documents a
  mandatory Bug routing rule and tests its canonical mapping/example; semantic
  free-text classification remains a Leader responsibility.
- **[Risk] Skill rename breaks active WorkItems** → resolution fails closed with
  the missing name; rename must update the WorkItem before dispatch.
- **[Risk] Skill changes under a reused Worker context** → Skill paths and bytes
  participate in `RuleDigest`, invalidating the ProfileContextKey.
- **[Risk] UI-first language becomes scenario scripting** → Skill text and tests
  preserve the explicit no-coordinate/no-fixed-sequence/Runtime-authority boundary.

## Migration Plan

1. Add schema/validator support with omission interpreted as empty.
2. Emit `required_skills` from the builder and examples.
3. Propagate ordered Skill documents and loading policy through Codex and DSH
   ModuleContext, Host payload, and delayed dispatch records.
4. Add the bounded Leader Reality Preflight to the shared RoleProfile/workflow.
5. Update Skill methods and focused tests.
6. Run validator, AgentWorkflow, Skill validation, consistency, and strict
   OpenSpec validation. Rollback removes the additive field handling and adapter
   instructions; historical WorkItems remain unchanged throughout.
