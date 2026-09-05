---
status: accepted
date: 2026-09-06
---

# UniFlow ships as a LOCAL_UNIFLOW skill at the standard skills location

A root-level `uniflow.md` was a private placement the ecosystem has no
convention for. The agent-skills ecosystem does have one standard, discovered
on demand by both harnesses: `.agents/skills/<name>/SKILL.md`. UniFlow — the
single workflow control plane — now lives there as a local skill
(`source: LOCAL_UNIFLOW` in frontmatter), auto-discoverable by Codex and DSH
instead of relying on a pointer from AGENTS.md to an ad-hoc file.

## Consequences

- AGENTS.md keeps only pointers; the truth table now points to
  `.agents/skills/uniflow/SKILL.md`.
- Engineering skills still never own lifecycle/routing/task systems; `uniflow`
  is the control plane itself, packaged in the skill format — the sole
  exception to "skills are HOW, not WHEN".
- `model-routing.yaml` and `schemas/` remain configuration/contract files at
  repo root (they are neither workflow nor skill).
- Provenance for local skills is declared in SKILL.md frontmatter and indexed
  in the provenance inventory; skills-lock.json remains installer-managed and
  upstream-only.
