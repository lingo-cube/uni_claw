## 1. Safety and Specification

- [x] 1.1 Create and verify a timestamped rollback archive before configuration writes.
- [x] 1.2 Record the user-authorized portable-core, adapter, deletion, history, and rollback boundaries in proposal, design, and spec.

## 2. Portable Skills and Tooling

- [x] 2.1 Move the five reusable Skill bodies from `.claude/skills` to `.ai/skills`, normalize Host-specific language, and validate each bundle.
- [x] 2.2 Move the C# MCP query guide into `.ai/tooling` and update current references.
- [x] 2.3 Make `.agents/skills` and `.dsh/skills` controlled relative-link adapters over `.ai/skills` only.

## 3. Protocol and Host Adapters

- [x] 3.1 Update AGENTS, UniFlow, Profile, contract, DSH, OpenSpec, guide, and active-change references to the single portable core.
- [x] 3.2 Update Validator and tests so required Skills resolve only from `.ai/skills` and still propagate identically to Codex and DSH.
- [x] 3.3 Replace Claude-specific consistency checks with portable-core and Host-adapter guards, including a bounded current-reference scan.

## 4. Claude Configuration Retirement

- [x] 4.1 Reduce root `CLAUDE.md` to a stateless `AGENTS.md` compatibility pointer.
- [x] 4.2 Delete `.claude/` only after all current dependencies and adapters have migrated.

## 5. Documentation and Verification

- [x] 5.1 Synchronize current OpenSpec membership projections and record documentation sync decisions.
- [x] 5.2 Run migrated Skill validation, Agent profile/DSH focused regressions, strict OpenSpec validation, consistency checks, reference scans, and `git diff --check`.
- [x] 5.3 Record the verified rollback path, removed material, remaining Host/pin boundary, and final evidence.

Verification evidence: [`evidence/checkpoint.md`](evidence/checkpoint.md).

## Design Docs

| Module | Design Doc |
|--------|------------|
| engineering-governance | `openspec/changes/universal-ai-coder-protocol-migration/design.md` |
