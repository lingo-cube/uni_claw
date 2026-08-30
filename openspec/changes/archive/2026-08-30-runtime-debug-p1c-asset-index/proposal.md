## Why

Foundation 要求 AssetRef 一等公民："截图、frame、crop、stage image 不得作为旁边附件，必须经 AssetRef 正式进入 Evidence/Trace/Debug IR 查询链"。README 冻结了 AssetRef schema，但前方没有可执行面把 capture bundle 里的资产变成可查询的 refs。P1c 增加第二个源适配器（Harness capture bundle），把 bundle 资产（manifest Artifacts + checksums 校验）投影为 AssetRef 索引，并暴露 `assets` / `asset-show` / `asset-related` 三个只读命令 —— 与 packet 源共用同一个 Query Core。

## What Changes

- 新源适配器 `tools/runtime_debug/sources/bundle.py`：fail-closed 读 `capture-manifest.json` / `records.json` / `checksums.sha256` / `artifacts/*.bin`（路径不入错误消息，避免 envelope 绝对路径禁令），校验 manifest Artifacts 唯一性与 checksums 覆盖；产出 Foundation schema 的 AssetRef（assetId/assetType=stored ContentType 或 `capture.artifact`/traceId/path 相对/ sha256/ parentAssetRef/ metadata{fileName,frameId,byteCount} + 经 records `FrameId↔SequenceNumber` 关联的 observationSeq）。
- 语义纪律：assetType 不猜测（截图/crop/overlay 等语义标签由生产者写入，索引层只投影 stored ContentType）；runId/spanId/occurrenceId 无 stored 值 → 显式 null。
- Query Core 新增 `assets` / `asset_show` / `asset_related`（parent/child 沿 DerivedFromArtifactId）。
- CLI 新增 `assets <bundle-dir>` / `asset-show <bundle-dir> --asset-id` / `asset-related <bundle-dir> --asset-id`（envelope `source` 只用 bundleId/traceId/scenarioId，不泄路径）。
- 全部 READ_ONLY / DETERMINISTIC / fail-closed；不读取 artifact 文件内容；不写回；零新依赖。

## Capabilities

### New Capabilities

- `runtime-debug-asset-index`: capture bundle → AssetRef 索引的只读确定性投影（checksum 校验、FrameId→observationSeq 关联、父子关系、语义标签不猜测）。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/sources/bundle.py`（新）+ `query.py` 三个纯函数 + `cli.py` 三个薄命令 + README。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` 新增 4 项契约测试（构造临时 bundle：assets 列表与 frame 关联、show/related 父子、checksum 不匹配 fail-closed、bundle 缺失 EVIDENCE_UNAVAILABLE）。
- 无 Runtime/Harness/wire/Trace 变更；harness 侧 bundle 格式保持现状（只读消费）。