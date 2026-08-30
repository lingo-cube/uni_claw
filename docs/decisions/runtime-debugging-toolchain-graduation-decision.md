# Runtime Debugging Toolchain — Umbrella (contracts home) — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUGGING-TOOLCHAIN` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debugging-toolchain/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain（P5 面 / umbrella 契约）收尾。

P0–P5 全部实现的契约 home：统一 Ref/Data Model（AssetRef 一等公民）、Query Core（六族/双树/剪枝）、CLI+TUI 单 Core 契约、Analysis+Skill 路由契约；14 个实现分片皆经独立 gate 归档；本 umbrella 收官将四个 capability 契约合入主 specs。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow（uv pytest）：**252 passed + 1083 subtests**。
- 14 个实现分片全部 GRADUATED 归档（2026-08-30 批次）；AgentWorkflow 252 passed + 1083 subtests；toolchain-routing 已接线。
- `openspec validate runtime-debugging-toolchain` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

契约不与实现重复（引用 frozen P0 文件）；全程 READ_ONLY/DETERMINISTIC/NO_AUTHORITY；无 Runtime/Architecture delta。

## 4. Deferred scope

P6+（自动修复、生命周期 wiring）、真实设备端到端基准。

## 5. Final conclusion

**GRADUATED.** 本切片/契约已收尾归档。
