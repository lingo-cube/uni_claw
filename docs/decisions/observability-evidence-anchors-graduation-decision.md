# Observability — Evidence Anchors (trace → frame/asset refs) — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_OBSERVABILITY-EVIDENCE-ANCHORS` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-observability-evidence-anchors/`

## 1. Buyer and exact claim boundary

**Buyer:** trace → 帧/截图/AssetRef 的一键定位（FDP span 落地到证据）。

Observe/Execute 边界在 span tag 上携带 observation.seq/frame 与 action.kind（tag 通道，零 schema v1 变更，fail-open）；execution-tree 消费锚点并按 observation.seq 直连 AssetRef（span→截图/帧引用），view 透传。引用为候选关联，非 world truth。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- C# Observability 88/88、AgentWorkflow 258+1083；emission 断言 + anchors 透传 + AssetRef join（有/无资产）；validate/consistency/diff 全过。
- `openspec validate observability-evidence-anchors` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

只打两处边界 tag（一致性命中 review gate）；join 无匹配为空、不推断（INFERRED deferred）；未改 schema/wire/authority。

## 4. Deferred scope

无锚 span 时间窗 INFERRED 推断、正式 Ref 字段（schema v2 需求）。

## 5. Final conclusion

**GRADUATED.** 本切片已验证并归档。
