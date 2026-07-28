## Context

`AGENTS.md` is now the shared project rule entry point for Claude Code, Codex, and other coding agents. Claude Code still has project-local slash commands and skills under `.claude/`, but Codex does not execute those slash commands directly. Without an explicit mapping, requests like `openspec apply <change>` rely on conversational memory instead of durable repo instructions.

The project already keeps OpenSpec artifacts in `openspec/changes/` and existing Claude playbooks in `.claude/skills/openspec-*`. This change connects those surfaces for Codex without copying skill content or changing runtime code.

## Goals / Non-Goals

**Goals:**
- Make Codex recognize natural-language OpenSpec lifecycle triggers.
- Preserve `openspec/changes/` as the source of truth for change progress.
- Reuse `.claude/skills/openspec-*` as project playbooks by reference.
- Keep Claude slash command behavior unchanged.

**Non-Goals:**
- Do not build a Codex plugin or native slash-command implementation.
- Do not migrate `.claude/skills/` into global Codex skills.
- Do not change C# runtime code, tests, or OpenSpec CLI schemas.

## Decisions

### D1: Encode Codex trigger rules in `AGENTS.md`

Codex already treats `AGENTS.md` as durable repository guidance. The trigger table belongs there because it is project-scoped, versioned with the repo, and visible before task execution.

Alternative rejected: keep the mapping only in conversation. That works for one session but disappears across new Codex sessions.

### D2: Use natural-language commands instead of slash commands

Codex users will invoke OpenSpec with prompts such as `openspec apply <change>` or `按 OpenSpec apply 执行 <change>`. Codex maps those phrases to the relevant playbook and artifacts.

Alternative rejected: pretend Claude slash commands run in Codex. That would create a false affordance and hide the actual execution model.

### D3: Reference Claude skills as playbooks, not duplicated sources

For each lifecycle action, Codex reads the matching `.claude/skills/openspec-*/SKILL.md` when it needs detailed workflow steps. The Claude skill directory remains the single project-local source until a real Codex plugin/skill migration is justified.

Alternative rejected: copy each Claude skill into a second Codex-specific location now. That creates drift before there is a proven need for native Codex discovery.

## Risks / Trade-offs

- Natural-language triggers are less discoverable than slash commands -> Mitigated by adding explicit examples in `AGENTS.md`.
- Referencing `.claude/skills/` from Codex is a soft integration -> Mitigated by documenting exact file paths and lifecycle mappings.
- The mapping can drift if Claude skill names change -> Mitigated by making `AGENTS.md` the shared protocol entry and keeping skill paths explicit.
