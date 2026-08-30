# Runtime Debug P2d — Execution Tree — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-P2D-EXECUTION-TREE` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-p2d-execution-tree/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain 执行树剪枝查询面 + 端到端验收。

capture bundle `observability-trace.json` 的 EXECUTION 树（Run→Span→ChildSpan）多维剪枝投影：layer/component/name 绝对切除子树，--only-errors/时间窗过滤 + 因果脊柱保留；projection-only（文件字节不变）；malformed trace fail-closed SCHEMA_VIOLATION；无 trace EVIDENCE_UNAVAILABLE。并实现 Foundation benchmark 可执行版：good/bad bundle 对 → assets → packet-generate → run-compare → execution-tree → terminal-chain 组诊断素材。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow（uv pytest）：**233 passed + 1083 subtests**（执行树 5 项 + E2E 全链 1 项在列）。
- AgentWorkflow 233 passed + 1083 subtests；执行树 5 项测试（全树/子树切除+字节不变/only-errors 脊柱/时间窗/无 trace）+ E2E 全链 1 项；bundle 读取器对齐严格 Harness 契约（camelCase/schemaVersion/records 内嵌/字节级 artifact 校验）由并行 conformance-repair 推进并在此适配。
- `openspec validate runtime-debug-p2d-execution-tree` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

剪枝绝不修改 trace/bundle（字节断言）；生成包的语义面为诚实 MISSING/UNKNOWN（EVIDENCE_COLLECTION/UNASSESSED/UNRESOLVED），不伪造诊断；Tracker 语义由 run-compare/execution-tree 结构事实承担。

## 4. Deferred scope

span 内 Event 子节点投影、跨 bundle 执行树对齐、语义 FirstBad 判定（Agent 层）。

## 5. Final conclusion

**GRADUATED.** P2d 已验证并归档。
