## Why

P4 收尾：Foundation §13 的 minimize 雏形。基于 P4b 的机械失败判定（firstMechanicallyFailedStep），实现确定性贪心最小化——在保持同一非 OK 结果的前提下尽量删步，产出"机械最小失败保留切片"（READ/repair→GREEN 循环的 falsifier 前提）。只读、不仿真、语义充分性显式 out-of-scope。

## What Changes

- `replay.py` 增加 `minimize_fixture(fixture)`：校验+投影前置；无机械失败 → no-op（hadFailure=false）；有失败 → 保留失败步、丢弃其后步骤，自失败步向前贪心试删（每删一步重投影判定 firstMechanicallyFailedStep 不变，不变则保留删除），输出 minimalSteps / removedOrders / iterations / note（mechanical-only）。
- CLI `minimize <fixture.json>`（复用 replay 读/校验路径）。
- 契约测试 4 项：Rejected fixture 机械最小 = [Rejected]（含 removed 1/2/4）、无失败 no-op、缺失 fail-closed、只读（输入不变）。

## Capabilities

### New Capabilities

- `runtime-debug-minimize`: replay fixture 的确定性机械最小失败保留切片（只读、贪心、不仿真）。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/replay.py` +1 纯函数；`cli.py` +1 命令；README。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` +4 契约测试。
- 无 Runtime/Harness/wire/Trace 变更；无新依赖；不写盘。
