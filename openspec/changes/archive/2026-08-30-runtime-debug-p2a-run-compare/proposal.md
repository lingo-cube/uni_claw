## Why

Umbrella CLI 契约包含 `run compare <a> <b>`，P1c/P1d 使两个 capture bundle 都可被同源适配器读取。P2a 实现配对 bundle 的结构事实差分：Good/Bad 对比无需语义引擎也能先给出 terminal / records / assets 三个轴的 UNCHANGED/CHANGED 判定与资产增删改清单 —— 为后续 FIRST_SEMANTICALLY_RELEVANT 定位提供机械前置（结构事实先行，语义留给 Agent）。

## What Changes

- `query.compare_bundles(good_bundle, bad_bundle)`：好/坏 bundle 的 terminal（stored FinalState/RuntimeSucceeded/RuntimeOutcome）、records（总数/观测数/动作数/最后观测 seq）、assets（按 ArtifactId 对齐：同 id 同 hash=UNCHANGED、同 id 异 hash=CHANGED、单侧存在=added/removed）三轴；输出两 run 的 deterministicInputDigest；显式注明"不推断 FIRST_SEMANTICALLY_RELEVANT"。
- `runtime-debug run-compare <good-bundle> <bad-bundle>`（stdout canonical envelope；任一 bundle 读取失败沿用 `EVIDENCE_UNAVAILABLE`/`SCHEMA_VIOLATION`）。
- 契约测试：结构性差异（terminal/metadata 轴、added=[x-extra]、changedOrSame 含 CHANGED）、全同 bundle 全轴 UNCHANGED、缺失 bundle fail-closed。

## Capabilities

### New Capabilities

- `runtime-debug-run-compare`: 双 capture bundle 的结构事实差分（deterministic、fail-closed、不推断语义变化点）。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/query.py` +1 纯函数；`cli.py` +1 命令；README 接口行。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` +3 契约测试。
- 无 Runtime/Harness/wire/Trace 变更；无新依赖；不写盘；不读 artifact 内容。
