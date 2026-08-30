# Runtime Debug P1c — AssetRef Index (capture bundle) — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-P1C-ASSET-INDEX` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-p1c-asset-index/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain 只读确定性查询/投影面（Debug Query Core 的增量能力）。

第二个源适配器：Harness capture bundle → Foundation AssetRef 索引（checksum 全覆盖校验、FrameId→observationSeq join、父子链、assetType 不猜测）；`assets/asset-show/asset-related` 命令。AssetRef 正式进入查询链。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow 套件（uv pytest）：**199 passed + 13 subtests**（本片测试在列）。
- 契约测试覆盖：构造 bundle fixture 全过：assets 列表+frame 关联+无绝对路径、show/related 父子、checksum 不匹配 SCHEMA_VIOLATION、缺失 bundle EVIDENCE_UNAVAILABLE；错误消息零绝对路径。
- `openspec validate runtime-debug-p1c-asset-index` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

不读 artifact 内容；语义标签（screenshot/crop/…）由生产者写入、索引层不推断；runId/spanId/occurrenceId 无 stored 值→显式 null。

## 4. Deferred scope

occurrence→crop 指派、stage-image 标注、bundle→Debug IR/packet（P1d）。

## 5. Final conclusion

**GRADUATED.** 本切片对应能力已验证并归档；deferred 项需各自独立 gate。
