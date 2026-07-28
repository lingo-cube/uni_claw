---
name: openspec-lifecycle
description: Manage UniClaw's OpenSpec spec-driven lifecycle with command-specific agent, model, and C# semantic-navigation routing. Use when asked to explore requirements, propose a change, implement or check an active OpenSpec change, or archive a completed change in this repository.
---

# OpenSpec lifecycle

Use the installed `openspec` CLI from the repository root. Treat its JSON output
as the authority for schema, artifact order, task state, and archive behavior.
Use the matching `.claude/skills/openspec-*/SKILL.md` as project playbook context,
but do not copy stale Claude-only tool calls or slash-command mechanics.

## Shared guardrails

1. Read `AGENTS.md` and the smallest context set it routes for the affected
   layers before changing code or OpenSpec artifacts.
2. Read only the playbook for the requested action:

   | Action | Project playbook |
   | --- | --- |
   | Explore | `.claude/skills/openspec-explore/SKILL.md` |
   | Propose | `.claude/skills/openspec-propose/SKILL.md` |
   | Apply | `.claude/skills/openspec-apply-change/SKILL.md` |
   | Archive | `.claude/skills/openspec-archive-change/SKILL.md` |

3. Inspect active changes before selecting one:

   ```bash
   openspec list --json
   openspec status --change "<change>" --json
   ```

   Ask the user to select when several plausible changes exist. Do not invent a
   change for work that is outside OpenSpec unless the user asks to propose one.
4. Follow `openspec instructions ... --json` for the current artifact or apply
   state. Re-check status after each artifact and verified task.
5. Never alter artifacts or task state merely to make a change appear complete.
   Run the verification required by the task/spec and report failures honestly.
6. Keep the primary agent responsible for scope, architecture decisions,
   artifact consistency, shared-file edits, integration, and final verification.
   Escalate enum, charter, and layer-topology decisions to the user.

## Agent and model routing

Delegate only when work is bounded and independent. Do not spawn agents for a
small edit, a single semantic query, sequential tracing, a decision that needs
the full conversation, or work that would overlap the same files.

| Role | Model and effort | Allowed work |
| --- | --- | --- |
| Researcher | `gpt-5.6-terra`, low or medium | Read-only artifact/doc/log audits and independent evidence gathering |
| Coder | `gpt-5.6-terra`, high | One well-specified implementation task with relevant tests |
| Refactorer | `gpt-5.6-sol`, high or xhigh | Cross-module impact analysis, complex diagnosis, or one isolated difficult implementation |

Apply these delegation rules:

- Use no more than three child agents concurrently. Fan out only independent
  tasks whose results can be integrated without conflicting edits.
- Give each child one concrete deliverable, explicit file ownership, required
  context paths, constraints, and verification. Treat children as leaf agents;
  instruct them not to delegate again.
- Keep `tasks.md`, shared OpenSpec artifacts, architecture decisions, and final
  full-suite validation owned by the primary agent.
- When applying an explicit model override, pass bounded context rather than a
  full-history fork and include all necessary task context in the prompt.
- If a requested model is unavailable, use the closest available model at the
  same or stronger reasoning tier and report the fallback. Do not create a new
  user-owned Codex task merely to obtain another model.
- Review every child result and the shared working-tree diff before accepting
  it. Child completion is evidence, not task completion.

## Code-query routing

For C# definitions, references, implementations, callers, hierarchy, code
actions, and diagnostics, use a configured Roslyn semantic-navigation MCP
service before textual search.

1. If Roslyn tools are not already exposed, use tool discovery once to look for
   C# or Roslyn semantic navigation.
2. Use semantic operations such as `find_symbol`, `find_references`,
   `find_implementations`, `find_callers`, `get_type_hierarchy`, and
   `get_diagnostics`, then read only the returned file range needed.
3. Before editing a `partial` type, locate every declaration semantically.
4. Keep a single, lead-driven C# query in the primary conversation. Delegate a
   read-only C# impact audit only when it is an independent batch task; require
   the researcher to use Roslyn MCP too.
5. Use `rg` for OpenSpec artifacts, documentation, logs, config, exact-string
   audits, and other non-C# bulk retrieval.
6. If no Roslyn service is configured or callable, state that the workflow is
   falling back, use `rg`, and manually check for partial declarations. Do not
   assume the old Claude MCP server names are available in Codex.

## Command dispatch

### Status

Keep status/list requests local to the primary agent. Run `openspec list --json`
and, for the selected change, `openspec status --change "<change>" --json`.
Do not spawn a child agent just to summarize this output.

### Explore

Use a read-only thinking stance for ambiguity reduction, impact analysis, and
option comparison. Do not edit product code or create artifacts unless the user
explicitly asks to capture the exploration.

1. Inspect active changes, relevant canonical specs, the affected layer docs,
   and `docs/system/decisions/log.md`.
2. Keep C# semantic tracing and trade-off decisions in the primary agent.
3. Optionally use Researcher agents for independent non-C# bulk retrieval,
   artifact digests, log analysis, or broad audits. Do not use Coder or
   Refactorer agents to implement during Explore.
4. State the problem, constraints, affected boundaries, options, risks, and
   unanswered decisions. End with a concise recommended proposal scope.
5. Do not create a change merely because exploration occurred.

### Propose

Use the primary agent as the proposal coordinator and artifact owner.

1. Derive or confirm a kebab-case change name. If that change already exists,
   ask whether to continue it or choose another name.
2. Create and inspect the artifact graph:

   ```bash
   openspec new change "<kebab-case-name>"
   openspec status --change "<kebab-case-name>" --json
   openspec instructions "<artifact>" --change "<kebab-case-name>" --json
   ```

3. For a broad proposal, optionally parallelize independent read-only
   investigations:
   - use Researcher for existing specs, decisions, and file inventories;
   - use Refactorer in analysis-only mode for a genuinely cross-module design
     or a second architecture review;
   - do not use Coder to implement product code during Propose.
4. Create artifacts in the CLI-reported dependency order. Keep proposal,
   design, specs, and tasks authored or integrated by the primary agent. Make
   requirements testable with SHALL/MUST statements.
5. Re-run status after every artifact. Stop when all `applyRequires` artifacts
   are done, then report readiness and unresolved decisions.

### Apply

Use the primary agent as implementation lead.

1. Select the change and obtain exact execution guidance:

   ```bash
   openspec status --change "<change>" --json
   openspec instructions apply --change "<change>" --json
   ```

2. Read every returned `contextFiles` path before implementation. Respect
   `blocked` and `all_done` states.
3. Build a dependency-ordered task plan. Delegate only when at least two tasks
   are independent and their file ownership does not overlap:
   - Researcher: read-only impact or semantic-reference audit;
   - Coder: one routine task and its focused tests;
   - Refactorer: one cross-module or high-risk task with explicit boundaries.
4. Do not let multiple agents edit the same file or task. The primary agent
   integrates results, resolves conflicts, runs cross-cutting checks, and
   updates each `tasks.md` checkbox only after its required verification passes.
5. Run task-specific tests and `dotnet build src/UniClaw.Core.sln`. Run
   `dotnet test src/UniClaw.Core.sln` for cross-cutting or riskier changes.
6. Complete the project playbook's Tier 1/2/3 documentation-sync checkpoint
   before declaring implementation complete. Leave canonical spec sync and
   Tier 4 decision extraction to Archive where the playbook assigns them.
7. Summarize completed tasks, verification, remaining work, and blockers.

### Archive

Keep archive authority and mutations in the primary agent.

1. Inspect and validate:

   ```bash
   openspec status --change "<change>" --json
   openspec validate "<change>"
   ```

2. Verify artifact/task completion, delta-spec sync needs, Tier 1/2/3 document
   state, and design decisions that belong in
   `docs/system/decisions/log.md`. Present any user decisions required by the
   archive playbook before mutating files.
3. Optionally use one Researcher for a read-only delta-spec or documentation
   audit. Do not delegate the archive command, decision approval, or shared
   documentation edits.
4. Archive only a validated, approved change through the CLI:

   ```bash
   openspec archive "<change>"
   ```

5. Confirm the archived location, canonical spec result, extracted decisions,
   documentation verification, and any approved warnings.
