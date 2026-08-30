# docs/analysis/ — Non-Normative Analysis

> DocumentType: `KNOWLEDGE_ROUTING_RULE`
> Authority: `NONE`

本目录是新建或显式迁移后的 landscape、gap analysis、research result 与尚未成为 Architecture
Decision / Runtime Contract / OpenSpec Spec 的 contract draft。Analysis 可以引用权威源，
但不能建立或修改 authority、lifecycle、owner、Runtime behavior 或 implementation
authorization。

## Governance

- Analysis 不登记到 `docs/decisions/index.md`。
- 既有历史 Analysis 不因本目录建立而批量迁移；必须有逐项 migration manifest。
- Human 明确冻结的 Architecture Decision 仍进入 `docs/decisions/`；批准的 OpenSpec
  contract 仍进入对应 `openspec/changes/<change>/specs/`。
- Analysis 中的 Roadmap Draft 不是正式 Roadmap；没有单独 Human authorization 时，
  不创建 `docs/roadmaps/` 或正式 roadmap 文件。
- 文件必须声明 `DocumentType`、`Status`、`Authority`、scope 与禁止边界。
- 大体积运行证据保留在原 artifact/capture 位置；Analysis 只保存 `EvidenceRef`。

## Migration Manifest

| Gate | Source | Target | Preserved facts | Governance correction |
|---|---|---|---|---|
| `PROJECT_LEADER_RUNTIME_DEBUGGING_CAPABILITY_P0_CONTRACT_GATE` | `docs/decisions/runtime-debugging-capability-landscape.md` | `docs/analysis/runtime-debugging-capability-landscape.md` | capability inventory、buyer evidence、Gap Matrix、Roadmap Draft 均保留 | 从 Decision Registry 移除；Analysis 保持 `Authority: NONE`，Roadmap 仍未授权 |

本 manifest 只覆盖上表中的单文件迁移，不授权移动其他 Analysis、Decision、Result、
Casebook 或 active work。
