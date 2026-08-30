## Why

Foundation §7 差分分析要求 Good/Bad pair 支撑 FIRST_SEMANTICALLY_RELEVANT 定位的机械前置。P2a 做了 bundle 级结构差分；P2b 在 packet 层做 EvidenceChain 逐节差分：两个带链的 packet 对齐 stage，输出 UNCHANGED/CHANGED/ADDED/REMOVED 轴与"首个机械变化 stage"，并把双方 stored LastGood/FirstBad 原样投影 —— 让"FirstDivergence 定位"在 packet 面可执行，语义判断仍归 Agent。

## What Changes

- `query.diff_packets(good, bad)`：链节对齐（good 链序 + bad 独有 stage），每节 present/statusAxis/refsAxis（input/decision/output refs 集合相等判定），`firstMechanicallyChangedStage`（首个状态或 refs 变化节，显式"机械 only"）；refs goodOnly/badOnly；双包 stored LastGood/FirstBad 投影；任一双缺 EvidenceChain → `INSUFFICIENT_TRACE_COVERAGE`。
- `runtime-debug trace-diff <good-packet> <bad-packet>`（envelope source 带双 packetId）。
- 契约测试：checkbox vs fusion-noop（raw 为机械首变，canonical UNCHANGED，stored LastGood/FirstBad 投影）；生成包（无链）→ `INSUFFICIENT_TRACE_COVERAGE`；缺失包 fail-closed。

## Capabilities

### New Capabilities

- `runtime-debug-trace-diff`: packet-vs-packet EvidenceChain 机械差分（确定性、fail-closed、不推断语义首变）。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/query.py` +1 纯函数；`cli.py` +1 命令；README 接口行。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` +3 契约测试。
- 无 Runtime/Harness/wire/Trace 变更；无新依赖；不写盘。
