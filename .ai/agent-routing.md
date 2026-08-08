# AI Coding Agent Routing

> **Human-readable explanation only.** The canonical executable routing truth is `.ai/model-routing.yaml`.
> This file must not contradict the YAML. If they differ, the YAML wins.
>
> Shared routing map for Codex and Claude Code.
> Keep project rules in `AGENTS.md`; keep executor-specific mechanics in `.claude/` or Codex session behavior.
> Model aliases and provider/fallback chains are configured in `.ai/model-routing.yaml`.

## Principles

1. Roles are portable; tools are adapters.
2. `AGENTS.md` is the project entrypoint for both Codex and Claude.
3. Claude custom agents live in `.claude/agents/*.md`; Codex uses the same role names as an execution stance, or a subagent when Codex multi-agent tooling is available.
4. A role may run on a stronger tier than configured, but must not silently downgrade below its tier.
5. Runtime implementation only starts from an approved Scenario Contract / OpenSpec SHALL / task. Ambiguous semantics route to design first.

## Model Tiers

| Tier | Responsibility | Typical work | Avoid |
|------|----------------|--------------|-------|
| `leader` | Top-level coordination and final responsibility | Scope control, OpenSpec lifecycle, task dispatch, merge judgment, user-facing decisions | Leaf coding and bulk file reading |
| `expert` | Decision-dense investigation | Cross-module refactor, deep failure diagnosis, architecture tradeoff | Mechanical coding that has no design decision |
| `standard` | Contract-driven production work | Runtime coding, scenario design, validation, focused tests | Owning top-level phase decisions alone |
| `fast` | Read-only retrieval and compression | File search, log extraction, symbol lookup, artifact summarization | Writing files or making architecture decisions |

## Portable Role Map

| Portable role | Tier | Claude Code adapter | Codex adapter | Main output |
|---------------|------|---------------------|---------------|-------------|
| `project-leader` | `leader` | Main Claude session in Fable orchestration mode | Current Codex task session | Plan, dispatch, final decision |
| `phase-evolution-controller` | `standard` | `.claude/agents/runtime-evolution-agent.md` | Inline planner; use task plan/checklist, then execute next action in main task | Next Action |
| `scenario-architect` | `expert` | `.claude/agents/scenario-architect.md` | Inline role or Codex subagent if available | Scenario Contract, Fake World, vocabulary, invariant check |
| `runtime-coder` | `standard` | `.claude/agents/runtime-coder.md` | Inline contract-driven implementation role | Code/test changes for one approved task |
| `runtime-validator` | `standard` | `.claude/agents/runtime-validator.md` | Code-review/validation stance in Codex; run guards/tests directly | PASS / CONDITIONAL_PASS / FAIL |
| `openspec-researcher` | `fast` | `.claude/agents/openspec-researcher.md` | Lightweight read-only search/MCP pass | Structured facts with file/line evidence |
| `openspec-coder` | `standard` | `.claude/agents/openspec-coder.md` | Inline implementation role for non-Runtime OpenSpec tasks | Code/test changes for one scoped task |
| `openspec-refactorer` | `expert` | `.claude/agents/openspec-refactorer.md` | High-reasoning investigation/refactor stance | Root cause, options, targeted change |

## Dispatch Rules

| Request shape | Route |
|---------------|-------|
| `openspec propose ...` | `project-leader` loads `.claude/skills/openspec-propose/SKILL.md` as playbook, then uses `scenario-architect` / `openspec-researcher` as needed. |
| `openspec explore ...` | Read-only exploration. Prefer `openspec-researcher` for broad retrieval; do not implement production code. |
| `openspec apply ...` | `project-leader` reads change artifacts, dispatches `runtime-coder` / `openspec-coder` for approved tasks, then `runtime-validator`. |
| Runtime scenario or semantic design | `scenario-architect` before coding. |
| Approved Runtime task | `runtime-coder`; keep one dispatch scoped to one task. |
| Phase or vertical slice completion claim | `runtime-validator`; validation is independent of coder claims. |
| Cross-module design failure or deep diagnosis | `openspec-refactorer` on `expert`. |
| Bulk read-only context gathering | `openspec-researcher` on `fast`. |

## Codex Execution Notes

Codex does not require Claude slash commands or Claude custom-agent frontmatter.
When a named Codex subagent tool is unavailable, run the mapped role inline:

1. State the role being used.
2. Load the same required repository documents the Claude agent would load.
3. Respect the role's write boundary, especially read-only validator/researcher boundaries.
4. Use MCP-first C# symbol lookup when a C# definition/reference/diagnostic is needed.
5. Report that the role was executed inline, not as a separate Claude agent.

## Claude Execution Notes

Claude keeps its existing custom agents and slash commands.
Agent frontmatter `model` values remain Claude platform enums (`opus`, `sonnet`, `haiku`); the backing provider/fallback chain is read from `.ai/model-routing.yaml` via `.claude/model-routing.md`.

## Change Control

- Add or rename a role here first.
- Update `.ai/model-routing.yaml` with its tier binding.
- Update `.claude/agents/*.md` only when Claude needs a new concrete adapter.
- Update `AGENTS.md` only to change the shared entrypoint map.
