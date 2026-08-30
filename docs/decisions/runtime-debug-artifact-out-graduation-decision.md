# Runtime Debug — Artifact Out (--out) — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-ARTIFACT-OUT` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-artifact-out/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain 生成物落盘面（UX 摩擦消除）。

packet-generate/replay-extract 可选 --out 落盘：原子写（temp+rename）、禁入 bundle 目录、不覆盖已存在文件（append-only）；无 --out 行为不变。消除端到端 shell 提取摩擦。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow（uv pytest）：**256 passed + 1083 subtests**。
- AgentWorkflow 256 passed + 1083 subtests；packet/fixture --out 读回闭环 ×2、bundle 内路径拒绝、覆盖拒绝。
- `openspec validate runtime-debug-artifact-out` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

输出策略受限（INVALID_INPUT/SCHEMA_VIOLATION）；不改输入；原子写保证无半成品；无 --out 零行为变化。

## 4. Deferred scope

覆盖/更新语义、多文件输出、路径白名单扩展。

## 5. Final conclusion

**GRADUATED.** 本切片已验证并归档。
