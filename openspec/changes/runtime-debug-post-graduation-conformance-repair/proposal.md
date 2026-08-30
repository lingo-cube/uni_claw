## Why

P1/P2 归档后的独立复验发现：bundle adapter 的字段迁移未完整收敛，P1d 生成物虽然声明为冻结的 `runtime-debug-evidence-packet.v0`，却不满足 P0 Schema；P2c reader/projection 还会接受悬空 EvidenceRef、畸形 chain 与非确定阶段顺序。现有毕业结论因此缺少可复现的合同一致性证据，必须先纠偏再复验。

## What Changes

- **BREAKING**：`packet-generate` 不再生成“缺字段但沿用 P0 version”的结构骨架，而是生成字段完整、显式表达 absence、可由冻结 P0 Schema 验证的 Evidence Packet；不推断 FDP、Owner 或 repair。
- Evidence Packet reader 对 P0 required shape、closed vocabulary、所有内部 EvidenceRef 与 repair gate 一致性 fail closed。
- bundle adapter 统一采用 Harness 实际持久化的 camelCase wire shape，并流式校验 artifact bytes、byteCount、digest、路径与关系完整性；只读且不复制 artifact。
- terminal-chain 固定七阶段顺序，严格区分 absent 与 malformed，缺失的 optional diagnosis/divergence 字段不伪造成 `null`。
- 补充跨 P1/P2 的回归 falsifier、Schema validator 验证、只读/确定性验证和毕业后复验收据。
- 修正文档中 `checksum-verified`、P0 compatibility、当前生命周期和 spec Purpose 的不准确表述；不改写历史 Decision，只追加纠偏结论。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `runtime-debug-read-only-projection`: reader 必须完整验证冻结 P0 packet/Debug IR 合同、closed vocabulary 与全部内部引用。
- `runtime-debug-causal-diff-projection`: causal/evidence projection 必须采用固定七阶段顺序；缺失 required chain 是 Schema violation，而非可投影的结构包。
- `runtime-debug-asset-index`: bundle 读取必须对实际 artifact bytes、byteCount、digest、路径和父子关系 fail closed，并与 Harness camelCase wire shape 一致。
- `runtime-debug-packet-generator`: 生成物必须是完整且 Schema-valid 的 P0 Evidence Packet，未知诊断必须用 explicit absence 与 blockers 表达。
- `runtime-debug-terminal-chain`: terminal chain 必须采用固定阶段顺序，malformed 输入 fail closed，absent optional 字段保持 absent。
- `runtime-debug-trace-diff`: generated P0 packet 的 explicit-MISSING chain 可机械比较；阶段顺序固定，缺失 required chain 在 reader 边界 fail closed。

## Impact

- 修改 `tools/runtime_debug/` 的只读 reader、bundle source adapter 与 projection。
- 修改 `tests/AgentWorkflow/test_runtime_debug_cli.py`，增加毕业后反向验收用例。
- 同步上述四个主规格、README、analysis/decision/current-state 投影。
- 无 Runtime、Trace model、Harness wire/API、设备/网络、生产依赖或 repair authority 变更。
