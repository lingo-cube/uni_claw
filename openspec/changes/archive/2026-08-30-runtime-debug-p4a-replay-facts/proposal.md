## Why

Foundation §13 为 P4 replay/minimization 预留契约。本轮落地其最小可执行切片：从 capture bundle 机械提取 replay fixture（failure evidence → replay fixture），并提供 fixture 校验/摘要命令；minimize 明确列为后续 gate（本轮不实现）。

## What Changes

- `query` 之外新增 `replay.py`（Core 纯函数）：`build_replay_fixture(bundle, case_id)` 产出 `runtime-debug-replay.v0`（steps=records 有序投影、assets=AssetRefs、trace 摘要、deterministicInputDigest 按 P0 约定）；`validate_replay_fixture` fail-closed 校验（schema/replayId/caseId/steps order 唯一/asset 结构）+ 摘要。
- CLI：`replay-extract <bundle> --case-id X`（stdout 完整 fixture）、`replay <fixture.json>`（校验+摘要）；缺文件 `EVIDENCE_UNAVAILABLE`、损坏 `SCHEMA_VIOLATION`。
- 契约测试 4 项：extract→validate 闭环（step/asset/span 数一致）、确定性（两次 extract 字节相同）、损坏 fixture fail-closed、缺文件 fail-closed。
- 不实现 replay 回放/minimize（RED→repair→GREEN 是 P4b+ 契约；本轮只产 fixture 事实）。

## Capabilities

### New Capabilities

- `runtime-debug-replay-facts`: capture bundle → replay fixture 的机械提取与 fail-closed 校验/摘要（deterministic、零回放执行）。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/replay.py` + `cli.py` 两个命令 + README。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` +4 契约测试。
- 无 Runtime/Harness/wire/Trace 变更；无新依赖；不写盘。
