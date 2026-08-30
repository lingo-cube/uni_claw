## Why

Umbrella Query Core 契约定义 EXECUTION tree（Run→Span→Event→ChildSpan）与多维剪枝，但 packet 层没有 span 数据；而 capture bundle 的 `observability-trace.json`（Harness TraceRun）就是现成执行树数据源。P2d 补上：bundle 适配器线程读取 trace，Query Core 输出可剪枝的执行树 —— 隐藏≠删除，剪枝只发生在投影。

## What Changes

- bundle 适配器新增可选 `observability-trace.json` 读取（camelCase TraceRun；schemal 校验 spanId 唯一、offset/duration 非负、字段类型；malformed fail-closed；无 trace → None）。
- `query.execution_tree(bundle, hide_layers, hide_components, hide_names, only_errors, time_from, time_to)`：
  - EXECUTION 树（parent 关系 → 嵌套 children，root 序确定性）；
  - **显式剪枝为绝对切除**（layer/component/name 隐藏命中 span 及其整棵子树）；
  - `--only-errors` / `--time-from/--time-to` 为过滤器：命中 span 隐藏，但其仍存活后代的祖先沿因果脊柱保留；
  - 输出 stats（total/shown/hidden）+ pruned 摘要 + "projection-only" 注记；无 trace → `EVIDENCE_UNAVAILABLE`。
- `runtime-debug execution-tree <bundle-dir> [--hide-layer L1,L2] [--hide-component C1,C2] [--hide-name N1,N2] [--only-errors] [--time-from NS] [--time-to NS]`。
- 契约测试 5 项（全树形状、hide-layer 剪整棵子树且 trace 文件字节不变、only-errors 保留 FAILED+祖先脊柱、时间窗重叠保留、无 trace fail-closed）。

## Capabilities

### New Capabilities

- `runtime-debug-execution-tree`: capture bundle 执行树的多维只读剪枝投影（deterministic、fail-closed、projection-only）。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/sources/bundle.py` +trace 读取；`query.py` +1 纯函数；`cli.py` +1 命令；README。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` +5 契约测试。
- 无 Runtime/Harness/wire/Trace 变更；无新依赖；不写盘。
