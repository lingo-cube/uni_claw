# Claude → Codex migration map

`AGENTS.md` is now the repository's Codex-native, always-read instruction file.
It consolidates the durable project constraints and replaces the behavioural role
of the Claude pre-edit hook with an explicit context-routing table.

The OpenSpec command protocol is available as the project-local Codex skill:
`.agents/skills/openspec-lifecycle/SKILL.md`. Its trigger is OpenSpec
exploration, proposal, implementation, status checking, and archival. It maps
Claude commands as follows:

| Claude entry point | Codex equivalent |
| --- | --- |
| `/opsx:explore` | `$openspec-lifecycle` — Explore |
| `/opsx:propose` | `$openspec-lifecycle` — Propose |
| `/opsx:apply` | `$openspec-lifecycle` — Apply |
| `/opsx:archive` | `$openspec-lifecycle` — Archive |

The existing `.claude/` assets are retained as historical/reference material;
they are not deleted or rewritten. Claude-only configuration is intentionally
not copied verbatim:

- `settings.json` permissions and hooks are Claude runtime features. Codex uses
  its own approval and sandbox system.
- Fable/Opus/Sonnet/Haiku routing cannot be configured by repository files in
  Codex. `AGENTS.md` preserves the process intent: bounded investigation,
  bounded coding, verification, and explicit user approval for charter changes.
- `cwm-roslyn-navigator` and `csharper-mcp` are optional external services.
  Codex must use Roslyn MCP for C# semantic navigation when the tools are
  exposed. `rg` fallback is only for docs, config, logs, exact-string audits, or
  a clearly stated emergency path when no Roslyn MCP service is callable.
- Legacy Python-oriented workflows, tracing scripts, test-contract scripts, and
  validation-report skills are not promoted to automatic Codex rules because
  they reference paths absent from the current C# branch. Keep them as source
  material until their commands are verified and intentionally migrated.

Before migrating another Claude skill, verify that its referenced paths and
commands exist on this branch, then create a focused `.agents/skills/<name>/`
skill rather than copying Claude metadata or model routing settings.
