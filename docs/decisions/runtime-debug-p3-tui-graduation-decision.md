# Runtime Debug P3 — TUI Shell — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-P3-TUI` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-p3-tui/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain TUI 面（同一 Core 的渲染壳）。

同一 Query Core 之上的薄 TUI 壳（textual 仅限 app.py、视图模型 stdlib 纯函数、框架延迟导入）：EXECUTION/CAUSAL 树切换、errors-only、AssetRef 面板、诊断面板；技术栈调查后选型（textual 8.2.8，经 uv --with 可选运行依赖，Core 包保持零框架）。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow（uv pytest）：**238 passed + 1083 subtests**。
- AgentWorkflow 238 passed + 1083 subtests；5 项视图模型/框架隔离测试；模块导入不依赖 textual。
- `openspec validate runtime-debug-p3-tui` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

TUI 零本地分析逻辑（数据全部经 view_models→query 单跳派生）；框架隔离有结构保证；诊断面板 FAILED spans 与 execution-tree --only-errors 同一 Core 参数。

## 4. Deferred scope

图片渲染（terminal 协议差异）、多 bundle 标签、完整 diff 交互、causal 树对 bundle 的语义链（需 packet 层）。

## 5. Final conclusion

**GRADUATED.** P3 已验证并归档。
