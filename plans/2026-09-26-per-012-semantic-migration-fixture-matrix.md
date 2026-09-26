# PER-012 Semantic Migration Fixture Matrix

版本：v0.1.1；decision-only；Human Gate 已接受；focused grill PASS；FROZEN。

本矩阵只定义迁移 contract 的 fixture/scenario evidence 形状，不实现 adapter、
consumer、Evidence Ledger、WorldModel 或 Product code。PER-009 不重开；PER-011
implementation 在本 contract 与矩阵 FROZEN 前保持 `BLOCKED`；当前 migration entry
gate 已满足，但实现仍需另立 change。

## Matrix

| ID | Fixture / scenario | Setup | Expected result | Gate evidence |
|---|---|---|---|---|
| M-01 | Exact checked false | two-state contract + `checkedBooleanExact` + Exact metadata + contract/fixture evidence；legacy XML `checked=false` | typed `semantic.checked=Unchecked` | contract id、capability metadata、fixture trace 三者齐全 |
| M-02 | Collapsed checked false | tri-state-capable/unknown domain，acquirer 只能表达 boolean，缺少 Exact proof | `Unknown(reason=partial-unrepresentable)`；不得输出 `Unchecked` | absence of Exact proof and resulting typed claim |
| M-03 | Tri-state rich source | source directly expresses Checked/Unchecked/Partial | values map losslessly to typed semantic domain | source capability declaration + three-value fixtures |
| M-04 | Egress-only legacy projection | typed Checked/Unchecked with a lossless legacy representation | `typed → legacy → old consumer`; no legacy input admitted to typed/P2/P3/Fusion/WorldModel | routing trace shows one-way edge |
| M-05 | No-loss projection refusal | typed Unknown/Partial/Conflicted cannot be represented as ON/OFF | legacy output is `unavailable` or `degraded`; no forced ON/OFF | projection result + degraded marker |
| M-06 | Single-consumer cutover | one consumer assigned to legacy or typed route for a migration stage | consumer reads exactly one route; no local dual-read arbitration | consumer routing config + read trace |
| M-07 | Cutover order | observer/read-only, ordinary non-effect-critical, then Control/verification/effect-critical consumers | later class cannot cut over before earlier class acceptance | ordered rollout record |
| M-08 | Routing rollback | typed route temporarily fails acceptance for one consumer | route rolls back to legacy only; consumer/run marked `legacy/degraded`; PER-010/011 contract unchanged | routing rollback record + degraded marker |
| M-09 | Legacy consumer creation guard | attempted registration of a new `*.state` reader | registration rejected; no new legacy reader | guard result |
| M-10 | Legacy deletion gate | candidate `*.state` surface | deletion allowed only when zero production readers + migration fixture PASS + shadow/cutover acceptance PASS + rollback observation window PASS | four gate records |
| M-11 | Valid shared-leaf dedup | A,B→F1; F1,C→F2 with shared transitive leaf references | valid admission; transitive basis `{A,B,C}`; no duplicate corroboration | lineage trace and basis set |
| M-12 | Malformed lineage | self-reference, cycle, missing parent, or declared basis mismatch | `MalformedLineage`; fail-closed; zero new belief contribution | rejection reason and no-admission trace |
| M-13 | Aligned with disagreement | Screenshot t1; async UI change; hierarchy t2; no Product mutation marker; classifier says `Aligned` | eligible for joint fusion; field disagreement may remain `Conflicted`; no automatic source selection | temporal classification + conflict result |
| M-14 | PER-009 boundary | any migration fixture attempts to alter PER-009 lifecycle/design | operation rejected/recorded as out of scope; PER-009 remains unchanged | path audit and lifecycle snapshot |

## Matrix status and entry rule

- M-01–M-14 are required fixture/scenario definitions for the focused grill; the
  definitions passed focused grill. This file does not claim runtime execution PASS.
- Focused grill may challenge ambiguity, authority leakage, lossy projection, routing
  order, rollback labeling, deletion evidence, lineage deduplication, and temporal
  conflict behavior.
- PER-011 implementation entry requires this matrix and the PER-012 contract to be
  explicitly `FROZEN`, followed by the recorded migration evidence. That migration
  entry gate is now satisfied; implementation remains outside this change.
