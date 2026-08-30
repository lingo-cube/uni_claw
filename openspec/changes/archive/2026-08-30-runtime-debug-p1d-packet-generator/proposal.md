## Why

Foundation/umbrella 的 Analysis 契约要求 "base Evidence Packet 机器可生成"；此前 `summarize`/`occurrence`/`causal`/`diff` 只消费已存在的 packet，新的真实 run 要进分析链只能手工造 packet。P1d 给 Query Core 增加机械基础 packet 生成器：从 Harness capture bundle（manifest/records/artifacts/checksums）确定性产出 Debug IR 结构骨架 —— 只投影 stored 事实，**绝不推断语义**，产物必须能被既有 P1a/P1b reader 读回（闭环验证）。

## What Changes

- `query.generate_packet(bundle, case_id, target_seq?)`：构建 P0-schema 兼容 base packet：
  - `TerminalState`（从 `RuntimeSucceeded`/`RuntimeOutcome`/`FinalState`）、`TargetObservation`（显式 `--observation-seq` 或最终 Observation record；无则 `UNRESOLVED`）；
  - `evidenceIndex`：每个 bundle artifact 一个 `CAPTURE_ASSET` entry（AssetRef 字段：uri 相对路径 / digest `sha256:<ContentHash>` / mediaType / selector 含 `observationSeq`(FrameId→records join) 与 frameId）—— **AssetRef 以 EvidenceRef 身份进 packet**；
  - `TargetOccurrence`（CANDIDATE，无 occurrence identity，evidenceRefs=target 帧资产）、`MissingEvidence`（expected-reality / occurrence-identity / good-bad-comparison / evidence-chain-stages —— 枚举原始 bundle 无法提供的语义面）；
  - `repairGate{eligible:false, blockers:[MISSING_REQUIRED_EVIDENCE]}`、`generation.deterministicInputDigest`（P0 约定：排序 `refId:<sha256 hex>` 行做 sha256）；
  - **禁止字段**：ExpectedReality/ObservedReality/Good/Bad/LastGood/FirstBad/GapKind/Owner/Disposition/Confidence/EvidenceChain 一律不生成。
- `runtime-debug packet-generate <bundle> --case-id <name> [--observation-seq N]`（stdout canonical envelope；不写盘；未知 seq → `EVIDENCE_UNAVAILABLE`）。
- 契约测试：生成 → 存盘 → `summarize`/`occurrence`/`evidence` 读回闭环；确定性；零语义伪造；AssetRef 绑定 evidenceIndex；未知 seq fail-closed。

## Capabilities

### New Capabilities

- `runtime-debug-packet-generator`: capture bundle → 机械 Debug IR base packet 的确定性只读生成（stored-facts-only、schema 兼容、可被既有 reader 消费）。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/query.py` +1 生成函数（含 digest 约定）；`cli.py` +1 命令；README 接口更新。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` +5 项契约测试。
- 无 Runtime/Harness/wire/Trace 变更；无新依赖；不写盘（stdout 输出）；不读 artifact 内容。