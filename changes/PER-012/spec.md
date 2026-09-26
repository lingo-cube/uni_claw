# PER-012 — PER-009 → PER-010/011 Semantic Migration Decision

版本：v0.1（decision-only；未冻结）

## Intent（WHAT/WHY）

在不重开 PER-009、PER-010 或 PER-011 的前提下，裁决现有 PER-009 realization
如何过渡到 PER-010/011 的前向 observation/fusion contract。该 change 只决定
语义映射、兼容边界、切换闸门和验收；不实现 adapter、Evidence Ledger、WorldModel
或 Product code。

## Status and relationship

- PER-009：保持现状，**不重开**。
- PER-010 v0.1.1：保持 `FROZEN/closed`。
- PER-011 v0.1.1：保持 `FROZEN/closed`，但 implementation 依赖本 change 的
  dedicated migration decision 通过。
- 本 change 当前为 `DRAFT_HUMAN_GATE`，不是新的 authority owner，也不改变
  Product baseline、WorldModel authority 或 Grounding authority。

## Recorded semantic mismatch

| 现状 | 前向 contract | 迁移决策必须解决的边界 |
|---|---|---|
| legacy XML `checked=false` 可被旧路径压成 `*.state=off` | capability-aware checked；lossy boolean 为 `Unknown(partial-unrepresentable)` | 何时允许 `Unchecked`，以及旧输出如何被标记为 legacy projection |
| PER-009 将 semantic checked 与视觉 appearance 放在共享 `*.state` 冲突路径 | PER-011 将 semantic claim 与 rendered appearance 分轴 | 旧 consumer 如何兼容，新 consumer 不得把两轴静默合并 |
| Evidence Ledger 目前不验证 parent EvidenceId、closure 或 basis 一致性 | derived proposal 必须有 immediate parents、transitive leaf basis 和 fail-closed admission | lineage 验收先于 derived proposal implementation |
| 旧 temporal path 可能把相邻 capture 当作同一 current claim | `Aligned` 只表示 eligible for joint fusion | 迁移不得把 Aligned 升格为 same-world-instant |

## Recommended transition contract（待 Human Gate）

### 1. Checked mapping

Legacy XML 的 `checked=false` 只有同时满足以下条件才可进入 semantic
`Unchecked`：

1. 当前 claim domain 有明确 two-state contract；
2. adapter capability metadata 明确声明 `Exact`；
3. contract/fixture evidence 可追溯并支持该声明。

否则统一进入 `checkedBooleanCollapsed`，输出
`Unknown(reason=partial-unrepresentable)`。Android API level、attribute presence、
class/role name、历史样本和经验样本都不能单独提供 Exact authority。

### 2. Shared `*.state` compatibility

PER-009 的 shared `*.state` 保留为 legacy compatibility surface，直到迁移验收
完成；它不被回写为 PER-010/011 的新 semantic contract。迁移期间：

- legacy output 必须带 source-contract/provenance 标记，不能伪装成 v0.1.1
  capability-aware claim；
- semantic checked 与 rendered appearance 不在迁移边界内自动合并；
- PER-011 新路径只消费 typed observation/proposal，不把 legacy projection 当作
  第二独立 source 或新的 authority owner；
- 任何旧 consumer 的切换必须在独立 rollout decision 中明确，不能由 adapter
  静默改变旧 `*.state` 的含义。

### 3. Lineage admission

迁移后的 derived evidence 只有在 admission 层完成 parent reference、cycle、
closure 和 declared basis 校验后才可进入 current path：

- 合法 shared-leaf duplication：leaf 去重，保持 valid，不增加 corroboration；
- self-reference、cycle、missing parent、basis mismatch：`MalformedLineage`，
  fail-closed，零新 belief contribution；
- 原始/direct source evidence 保留；不得将 malformed proposal 降级为独立证据。

### 4. Temporal compatibility

迁移不得改变 PER-011 的时间语义：`Aligned` 只授予联合融合资格。没有 mutation
marker 不等于证明 same-world-instant；Aligned 后的 field disagreement 仍可为
`Conflicted`。

## Out of Scope

- 不修改 `changes/PER-009/**`。
- 不修改 `src/**`、Product baseline、WorldModel、Grounding 或 AGT/RUN boundary。
- 不创建第二 WorldModel、第二 authority owner 或 confidence voting 路径。
- 不在本 change 内实现 adapter、lineage validator、dual-read、cutover 或回滚代码。

## Human Gate

需要确认以下三项后，才能把本 change 从 `DRAFT_HUMAN_GATE` 推进到 resolved：

1. legacy `*.state` compatibility surface 的保留范围和终止条件；
2. old consumer 到 typed observation/proposal 的切换顺序与回滚条件；
3. 下列 acceptance fixture 是否足以作为 PER-011 implementation 的 entry gate。

## Acceptance（decision change）

- A1 形成 checked mapping table，明确 Exact / Collapsed / tri-state 三条路径。
- A2 明确 legacy `*.state` 只作 compatibility surface，不成为新 authority。
- A3 明确 semantic checked 与 rendered appearance 的迁移边界。
- A4 明确 lineage valid dedup 与 `MalformedLineage` 的 admission 结果。
- A5 明确 Aligned、Conflicted、Unknown 的 temporal compatibility 语义。
- A6 明确切换顺序、回滚条件、fixture evidence 和 implementation entry gate。
- A7 PER-009 未重开；PER-010/011 继续 `FROZEN/closed`；无 Product code 改动。

## Implementation entry gate

PER-011 implementation 只有在 A1–A7 被接受、migration mapping 有独立 evidence、
且 rollout/rollback boundary 已记录后才可开始。未通过前，允许继续做文档和 fixture
设计，不允许把现有 PER-009 runtime path 宣称为已符合 PER-010/011。

