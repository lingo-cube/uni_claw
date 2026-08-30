## Why

Runtime Debugging P0 已冻结并验证 Debug IR、Evidence Packet、occurrence correlation 与只读 tooling contract，但定位人员仍需逐个打开 packet、人工汇总终态与缺失证据，并手工筛选 occurrence。P1a 需要把这两类重复机械动作变成确定性、fail-closed 的离线投影，同时保持 Runtime、Trace 与 authority 完全不变。

## What Changes

- 新增本地 `runtime-debug` 只读命令面，首个垂直切片仅实现 `summarize` 与 `occurrence`。
- `summarize` 只投影显式指定的 `runtime-debug-evidence-packet.v0`，输出 source identity、terminal state、evidence availability、target scope 与 unresolved blockers；不推断 root cause、Owner 或 repair authorization。
- `occurrence` 在显式 packet scope 内接受且只接受一个 typed selector，输出稳定排序的 occurrence candidates、correlation status/proof 与 linked EvidenceRefs；不通过 text、bounds、index 或 StableKey 单独证明 identity。
- 所有成功与失败结果使用确定性 canonical JSON 和 closed command status；unsupported input、missing evidence、identity mismatch 与 ambiguity 均 fail closed。
- 使用 P0 五个真实 fixture 建立 CLI contract tests，并验证工具不改写输入 artifact 或 repository state。
- P1a 不实现 `trace-diff`、`terminal-chain`、packet generator、replay/minimization，也不新增 Trace 字段、Runtime wire/API、Runtime service 或 authority。

## Capabilities

### New Capabilities

- `runtime-debug-read-only-projection`: 定义显式 P0 Evidence Packet 上 `summarize` 与 typed `occurrence` 查询的只读、确定性、fail-closed 行为。

### Modified Capabilities

无。

## Impact

- 新增独立、Python standard-library-only 的 `tools/runtime_debug/` 实现与 `tools/runtime-debug` 命令入口。
- 新增 `tests/AgentWorkflow/` 下的离线 CLI contract tests；复用现有五个 P0 packet fixtures，不复制大体积 artifact。
- 依赖 P0 非权威工作契约与 schema 作为输入格式；不修改 Runtime、Harness、DriverHost、PhysicalHost、Trace model、wire contract 或 production dependency graph。
- 工具无设备、网络、Runtime process、隐式 `latest`、写回、修复、owner 选择或 lifecycle authority。
