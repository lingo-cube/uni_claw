## Why

P1a 已交付 `summarize` / `occurrence` 只读投影并建立五案例测试基线。但 FDP 分析的另外两个主视图仍是手工操作：把 Debug IR 的 `EvidenceChain` 逐 stage 摊开（causal/evidence tree）与 Good/Bad 差分（`GoodComparison`/`BadComparison`/`LastGood`/`FirstBad`）。P1b 在同一个 Query Core 内补齐这两个只读投影与 evidence-chain 查询，保持 READ_ONLY / DETERMINISTIC / NO_RUNTIME_AUTHORITY / 零新依赖，且纯消费已冻结的 P0 packet —— 不引入新数据源、不计算 FDP/Owner/Disposition。

## What Changes

- `runtime-debug trace causal <packet> [--prune stage,...] [--only-decisions] [--only-evidence]`：将 packet 内 `EvidenceChain` 投影为因果/证据树（stage 有序：raw→normalized→fused→canonical→semanticAdmission→affordance→runtimeState），每 stage 含 status/summary/input/decision/output refs；`--prune` 只隐藏不删除（隐藏≠删除），`--only-decisions`/`--only-evidence` 为类型过滤。
- `runtime-debug evidence chain <packet> --evidence-ref <refId>`：输出该 ref 在各 stage 的角色（input/decision/output）、各 stage status，以及 ref 的 kind/uri/digest/mediaType/integrity/selector；未知 ref → `EVIDENCE_UNAVAILABLE`，stored `IDENTITY_MISMATCH` → 同名状态。
- `runtime-debug diff <packet>`：投影 packet 内 stored `GoodComparison`/`BadComparison`（status/label/summary/axes/evidenceRefs）与 `LastGood`/`FirstBad`（stored 事实，不计算）。
- 全部输出沿用 canonical envelope / closed statuses / exit-code 映射；CLI 仍是薄适配器，逻辑只进 Query Core。

## Capabilities

### New Capabilities

- `runtime-debug-causal-diff-projection`: P0 packet 上的 causal/evidence tree、evidence-chain 与 packet-scoped Good/Bad 差分只读投影（deterministic、fail-closed、prune-only）。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/query.py` 增加 `causal_tree` / `evidence_chain` / `compare` 三个纯函数；`cli.py` 增加 `trace` / `evidence` / `diff` 子命令（薄适配）。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` 增加 7 项契约测试（causal 顺序/prune/only-decisions、evidence chain 成功与未知 ref、diff 投影与 closed status）。
- 无 Runtime/Harness/DriverHost/Trace/wire 变更；无新依赖；无 FDP/Owner/Disposition 计算；输入 packet 字节不可变。