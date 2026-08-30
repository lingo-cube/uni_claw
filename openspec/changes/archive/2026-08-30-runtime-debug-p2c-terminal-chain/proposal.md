## Why

Foundation §8 分析引擎要求 run summary 与 terminal causal chain；P1b 的 causal tree 已给链视图，但"终结因果链 + stored 诊断字段"仍是手工拼（GapKind/Owner/Disposition 要从 packet 里翻）。P2c 增加 `terminal-chain`：机械投影 TerminalState、链节、LastGood/FirstBad，并把 packet 中 stored 的诊断字段（GapKind/Owner/Disposition/Confidence）作为 **STORED 事实** 投影给 Agent —— 不计算、不推断。

## What Changes

- `query.terminal_chain(packet)`：stored TerminalState + 有序链节（stage/status/summary/refs）+ stored LastGood/FirstBad + `storedDiagnostics`（仅当 packet 存有 GapKind/Confidence/Disposition/Owner 时投影，Owner 只取 status/domain/seam/basis）；无链/无诊断的生成包 → chain=[] + storedDiagnostics={}（terminal 仍可用）——不伪造事实。
- `runtime-debug terminal-chain <packet>`。
- 契约测试：checkbox fixture 全字段（chain 7 节、firstBad= semanticAdmission、GapKind=CONTRACT_REGRESSION、Owner.domain 存在、note 标注 STORED）；生成包 → terminal-only + 空链 + 空 storedDiagnostics。

## Capabilities

### New Capabilities

- `runtime-debug-terminal-chain`: P0 packet 的机械终结因果链投影（stored-facts-only 诊断视图）。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/query.py` +1 纯函数；`cli.py` +1 命令；README。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` +2 契约测试。
- 无 Runtime/Harness/wire/Trace 变更；无新依赖。
