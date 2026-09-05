---
status: accepted
date: 2026-09-05
---

# Adopt the upstream agent-skills ecosystem (`.agents/skills/`) instead of a private skill format

The V2 harness conforms to the open AI-coding ecosystem before inventing anything: skills are vendored verbatim from upstream repos (mattpocock/skills ×8, humanlayer/skills `show-me`) via the vercel-labs `skills` CLI into `.agents/skills/`, with provenance carried by `skills-lock.json`. A previously drafted set of seven self-authored SKILL.md files under `skills/` was deleted — hand-rewritten approximations of upstream skills are forbidden.

## Considered Options

- Self-authored canonical skills — rejected: chat-rewritten upstream equivalents with no provenance, drifting from the ecosystem.
- Claude Code plugin bundle — rejected: managed read-only subscription; this harness needs repo-owned files, Codex-first.
- `npx skills` installer (copy mode) — chosen: editable repo-owned files at the standard `.agents/skills/` location with lockfile provenance.

## Consequences

- Repo-level constraints (skills never own lifecycle/routing/task systems) live in `AGENTS.md`; upstream SKILL.md bodies stay untouched.
- Updates flow through `npx skills update`; `skills-lock.json` hash changes are upstream upgrades.
- UniClaw-specific debugging semantics (E0–E4 / FDP / Owner) attach later as local extensions or references, never as upstream edits.
