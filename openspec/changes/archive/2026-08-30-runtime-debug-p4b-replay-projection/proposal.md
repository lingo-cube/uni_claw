## Why

P4a 交付 fixture 提取与校验；P4b 让 fixture 可被"确定性干跑投影"消费：按 stored records 序列机械重放轨迹（动作/观测计数、最后观测 seq、首个机械非 OK 步骤），为 P4 minimize（RED→repair→GREEN）提供可断言的基础轨迹 —— 不仿真状态、不接触设备。

## What Changes

- `replay.py` 增加 `project_replay_run(fixture)`（校验前置、纯机械）：trajectory（order/kind/seq/actionId/actionKind/targetIndex/targetState/resultOutcome/frameId）、counts（steps/observations/actions/lastObservationSeq）、`firstMechanicallyFailedStep`（首个 resultOutcome ∉ {Dispatched,Succeeded} 的 order，机械 only）。
- CLI `replay-run <fixture.json>`（与 `replay` 共用读/校验路径，仅结果形态不同）。
- 契约测试 3 项：轨迹+计数+机械首失败（Rejected 步骤）、干净 fixture 无失败、缺失 fail-closed。

## Capabilities

### New Capabilities

- `runtime-debug-replay-projection`: replay fixture 的确定性干跑投影（只读、不仿真、机械首失败定位）。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/replay.py` +1 纯函数；`cli.py` +1 命令；README。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` +3 契约测试。
- 无 Runtime/Harness/wire/Trace 变更；无新依赖；不写盘。
