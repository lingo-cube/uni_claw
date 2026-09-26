# PER-011 — Visual + Hierarchy Fusion Architecture

## 前置与 Intent

前置条件：`PER-010 design_status = FROZEN`。本 change 不重新定义 XML / hierarchy semantics，而定义 hierarchy、screenshot、OCR 等 observation source 如何在时间、覆盖率和字段 authority 约束下形成可追溯的 World belief 输入。

Fusion 是 Capability Plane 的 association/derivation 能力，不是新的 authority owner，不拥有 WorldModel、identity、absence、control、grounding 或 assurance。

## Scope / Out of Scope

范围：

- Source Authority Matrix；
- temporal alignment、capture correlation、freshness boundary；
- hierarchy occurrence、visual occurrence、OCR token 的 association/fusion；
- Supported / Conflicted / Unknown / Unsupported / Unaligned 语义；
- coverage/absence 语义；
- provenance、conflict disposition、有界 escalation；
- fusion scenarios、failure matrix 与 implementation slices。

排除：

- 不改 PER-010 的 `UiHierarchyObservation v1`；
- 不新增 Product public `Ixxx` interface，不改 WorldModel/AGT/RUN/Grounding baseline；
- 不让 confidence voting 取代字段 authority；不把整张截图和整棵 XML 直接交给 Agent/大模型；
- 不把 fused result 直接当 target、effect proof 或 canonical identity；
- 不实现 Product code、VLM、跨 revision temporal stitching 或无限升档。

## Fusion boundary

```text
Hierarchy raw / typed observation ─┐
Screenshot raw / typed observation ├→ source adapter → P2 Evidence Ledger
OCR raw / typed observation ───────┘                         ↓
                         immutable admitted source Evidence
                                      ↓
                    bounded association + field fusion capability
                                      ↓
                  derived ObservationProposal + provenance refs → P2
                                      ↓
                             WorldModel reconciliation
                                      ↓
                       Control / Grounding / Assurance consumers
```

Source evidence 和 derived fusion proposal 都必须经过 P2 admission；fusion 只读取
immutable admitted source evidence，不能直接写 WorldModel。WorldModel 是唯一
canonical WorldBelief / identity / conflict reconciliation authority。融合过程可以
产生 association evidence，但不得铸造 persistent identity。

## Source Authority Matrix

下表的“结构 authority”是**条件 authority**：只有 source 声明对应 capability、
field 为 `Observed`、occurrence association 足够明确、capture 可时间对齐且属性
valid 时才成立。否则结果退化为 `Unknown` / `Unsupported` / `Unaligned`，不能由
另一个 source 静默接管。

| 字段/claim | 可观察 source | 结构 authority | 辅助 source | 冲突处置 |
|---|---|---|---|---|
| `checked` / semantic toggle state | hierarchy、视觉 | hierarchy；要求 checked capability + `checkable=true` validity guard | visual/OCR 只能说明 rendered appearance | hierarchy 条件满足时保留 hierarchy claim，记录 visual overruled；条件不满足则 `Conflicted` 或 `Unknown`，不按 confidence 投票 |
| `enabled` | hierarchy、视觉 | hierarchy `enabled` capability | visual appearance | 同上；过期/不唯一时不赋 hierarchy authority |
| `selected` | hierarchy、视觉 | hierarchy selected capability | visual highlight | 保留双方 evidence；无有效 authority 时 `Conflicted` |
| `focused` | hierarchy、视觉 | hierarchy focused capability（注明 accessibility focus 与 input focus 可能不同） | visual focus ring | 不把视觉焦点 ring 强行映射成同一 claim |
| semantic `text` | hierarchy、Accessibility/Compose semantics | hierarchy semantic-text capability | OCR/视觉仅作关联和完整性线索 | semantic text 与 rendered text 分轴；不以像素文字覆盖语义文本 |
| rendered text | screenshot、OCR | screenshot/OCR 在 declared visual coverage 内 | hierarchy semantic text | OCR 与画面不一致保留 `Conflicted`；不得改写 semantic text |
| `bounds` / geometry | hierarchy、screenshot | 各自 frame 内的 measured geometry；不是 world identity | OCR token box、视觉 detector box | 先 frame normalization 和 association；无法对齐则 `Unaligned`，不能取平均伪造 bounds |
| `visibility` | hierarchy visible-to-user、screenshot | hierarchy 仅在 capability + same capture window 下；视觉可证明 rendered visibility | OCR/other detector | clip/overlay/partial coverage 使结果 `Unknown` 或 `Conflicted`，不把未看到当 absent |
| icon / rendered visual state | screenshot、OCR/视觉 detector | visual source 在 declared pixel coverage 内 | hierarchy role/state | hierarchy semantic state 与 visual appearance 分轴；冲突保留 |
| role / semantic role | hierarchy、Compose semantics、视觉 | hierarchy/semantics role capability | visual detector | 无 semantics capability 时视觉只能辅助，不铸造 role truth |
| `resource-id` | hierarchy | hierarchy source metadata authority | 无 | 只作为 association/grounding evidence，永不成为 canonical identity |

`XML 永远优先`、`Visual 永远优先`、`confidence 高者胜` 都不是 fusion rule。
字段 authority 是 claim type、capability、validity、association、temporal alignment
的合取结果。

## Temporal Alignment Diagram

每个 source observation 带 PER-010 `CaptureId`、`ObservationCycleId`（若有）、
`CaptureTimestamp`、`SessionCorrelation`、source sequence 和 declared coverage。
Fusion 产生如下 alignment disposition：

```text
Aligned
AlignedWithCoverageLimit
Unaligned（不同 cycle、已知 mutation、或跨 revision）
TemporalUnknown（缺 correlation/time）
```

```text
capture cycle C
  ├─ screenshot t1
  ├─ hierarchy  t2
  └─ OCR        t3
       ↓
  可对齐 = same correlation/cycle
          + Δ 在调用方 bounded window 内
          + 没有 mutation marker
          + 坐标 frame 可转换
```

- screenshot t1 + hierarchy t2 只有在同一 correlation/cycle、时间差在 bounded
  window 内且中间没有 mutation marker 时才可融合。
- 任一 source 在 dispatch 或 UI mutation 之后取得，必须进入新的 cycle；不得与
  mutation 前的 source 拼成 current state。
- 缺 correlation 不能用相邻 wall-clock 猜相同页面；结果为 `TemporalUnknown`。
- `CaptureTimestamp` 是 provenance；不直接构成 freshness。对某个 action 是否够新
  由 Assurance 根据 consumption requirement 裁决。
- 不跨 WorldBelief revision 拼接 occurrence 或 claim；旧 evidence 可作为历史 basis，
  不自动成为 current belief。

## Association and fusion granularity

Fusion 的输入粒度是：

```text
HierarchyOccurrence ↔ VisualOccurrence(region/frame) ↔ OcrToken
```

association 结果至少区分：

```text
Unique
ManyToOne        多个 hierarchy occurrence 对同一 rendered region
OneToMany        一个 hierarchy occurrence 对多个 OCR/token/visual region
Ambiguous
Unassociated
```

association 只产生 candidate links、空间/文本/semantic features 和 disposition；
不把 link 直接升格为 ContainerIdentity、LogicalItem 或 target handle。Compose
merged/unmerged、scroll/recycled list、overlay、virtualized list 都可能造成
ManyToOne、OneToMany 或 Ambiguous；无法唯一关联时 fail-closed。

Fusion result 的设计形状（非 Product public interface）为：

```text
FusedObservation
├ claim / field kind
├ association disposition
├ field disposition: Supported | Conflicted | Unknown | Unsupported | Unaligned
├ source EvidenceId[] / artifact refs
├ CaptureId[] / correlation / coverage
├ fusion rule + version
├ conflict disposition（none / overruled-source / unresolved）
└ limitations / escalation history
```

## Conflict Matrix

| 情况 | fusion disposition | WorldModel 输入 | 控制含义 |
|---|---|---|---|
| authority 条件满足且双方一致 | `Supported` | derived proposal + 双方 provenance | 可由后续 assurance 判断能否消费 |
| authority 条件满足但辅助 source 相反 | `Supported` + `overruled-source` 记录 | authority claim 和 conflict ledger 都保留 | 不静默删除辅助证据；required property 的最终 freshness/assurance 仍独立判断 |
| 两方相反且没有有效 authority | `Conflicted` | 两边证据都保留 | required property fail-closed，不 dispatch |
| source 能力不存在 | `Unsupported` | 不产生伪造 claim | 不能把 Unsupported 当 Unknown/false |
| source 理论支持但本次无法判定 | `Unknown` | 可请求有限 re-observe | 不产生 false/absent |
| 时间、frame 或 mutation 不可对齐 | `Unaligned` | 各自 evidence 保留，不融合 | 不把跨时刻 claim 组合为 current truth |

Conflict 永远不由 producer confidence、模型分数或多数投票静默消除。无法合理
消解的冲突保持显式状态，进入 WorldModel 后由其 Reconciliation 记录 belief
Conflict；fusion 不先行覆盖。

## Absence and coverage

每个 fused result 带 coverage：

```text
CompleteWithinDeclaredSurface
Partial
Unknown
SourceUnavailable
```

规则：

- hierarchy 没看到 + screenshot 没看到 ≠ element 不存在；
- offscreen、virtualized、clip、overlay、scroll、窗口未覆盖、source unavailable
  都只能导致 `Unknown` / coverage limitation；
- `Empty` capture 是空采集，不是页面 absence；
- 只有 source 明确提供 negative evidence 且 coverage/authority 条件成立时，才可
  形成 property-level `Observed(false)`；element absence 不由 omission 推导；
- `Partial` 与 `CompleteWithinDeclaredSurface` 不能混用；fusion 不“补齐”缺失字段。

## Bounded escalation

```text
fast screenshot/OCR
        ↓（required field 未定案或 coverage 不足）
hierarchy acquisition
        ↓（仍 Conflicted / Unknown，且目标属于本次 buyer）
focused rescan（同一 source/frame，有限次数）
        ↓（仍悬案且有明确不可逆/高价值消费理由）
deep/VLM（后置、有限预算）
```

- 升档触发是当前证据体系无法定案，不是 confidence 低。
- 每个 target/cycle 的 focused rescan 最多一次；deep/VLM 最多一次，均受调用方
  time/attempt budget 限制；预算耗尽即 `Unknown`/`Conflicted` fail-closed。
- 无 required buyer、无 declared budget 或 source capability 不存在时不升级。
- 升档结果仍是 claim，必须进入同一 P2→WorldModel 路径；deep 不拥有最终 truth。
- PER-009 已冻结的 Focused 语义可作为第一实现 slice；Deep 仍是后置 seam，不在
  本 change 实现。

## Provenance

任何 fused result 至少可回溯：

```text
Hierarchy EvidenceId / artifact id
Visual frame id / artifact id
OCR evidence id（如有）
CaptureId + ObservationCycleId + correlation
Fusion rule/version
Association disposition
Field disposition
Conflict disposition
Coverage / limitations
Escalation history
```

Provenance 丢失、source id 不可解析、rule/version 不匹配时，derived result 不得
进入 WorldModel current path；应返回 `Malformed` 或 `Unknown` 诊断。

## Fusion scenarios

1. **同周期 checked**：hierarchy `checked=Checked`、`checkable=true`，visual switch
   看起来 on；结果 `Supported`，保留两个 EvidenceId，后续 freshness 仍由 Assurance
   判断。
2. **权威域冲突**：hierarchy `checked=Unchecked`、visual 看起来 on；结果为
   hierarchy `Supported + overruled-source(visual)`，不做 confidence voting，冲突
   记录保留。
3. **stale hierarchy + fresh screenshot**：不同 cycle 或 mutation marker；结果
   `Unaligned`，不融合成当前 checked，允许 bounded hierarchy re-observe。
4. **fresh hierarchy + stale screenshot**：semantic state 由 hierarchy 支持；旧
   visual 只作为历史证据，不能覆盖当前 belief。
5. **双源都没看到元素**：coverage 非完整或存在 scroll/virtualization 时结果
   `Unknown`，不输出 element absent。
6. **Compose merged/unmerged**：一个 visual card 对多个 semantics occurrence，
   产生 ManyToOne/OneToMany；不铸造 stable element identity。
7. **OCR 多 token**：一个 hierarchy text occurrence 对多个 OCR token，保留
   OneToMany association；文本 claim 按 semantic/rendered 两轴处理。
8. **multi-window/overlay**：window metadata 缺失或 frame 不同，`TemporalUnknown`
   或 `Unaligned`；不把不同 window 的相同 bounds 误合并。
9. **provenance 缺失**：任一 source id、capture correlation 或 rule version 无法回
   溯，derived result fail-closed，不进入 current WorldModel path。
10. **有界升档**：FastScreen 与 hierarchy 冲突，单次 focused rescan 仍未知，预算
    用尽后保留 `Conflicted/Unknown`，不循环调用 VLM。

## Failure matrix

| failure | expected fusion result | owner |
|---|---|---|
| source unavailable | no source claim；保留诊断 | acquisition adapter |
| malformed source evidence | no partial fusion | adapter/Evidence admission |
| capture correlation missing | TemporalUnknown | fusion capability |
| known UI mutation between captures | Unaligned | fusion capability |
| frame conversion unavailable | Unaligned | association layer |
| coverage partial/offscreen | Unknown + coverage | source/fusion |
| ambiguous association | Ambiguous / no fused current claim | fusion capability |
| authority conflict unresolved | Conflicted | WorldModel reconciliation after P2 |
| provenance incomplete | fail-closed derived proposal | Evidence Ledger ingress |
| escalation budget exhausted | Unknown/Conflicted, bounded stop | Control/Observation policy |

## Grill findings disposition

按 PER-011 checklist 攻击了第二 WorldModel、confidence voting、stale/fresh 混合、
absence、Compose/virtual list association、多对一/一对多、fusion→target 直连、
provenance 丢失、conflict fail-closed、bounded escalation 和 Agent 绕过 WorldModel。
所有项已在本 spec 的 boundary、matrix、disposition、coverage、provenance 与 budget
规则中收敛。Focused re-grill 结果 `PASS`；没有需要修改 Product baseline、
WorldModel authority、Grounding authority 或 AGT/RUN frozen boundary 的项。
设计状态 `FROZEN`。

## References

- `changes/PER-010/spec.md`（FROZEN compatibility contract）
- `changes/PER-009/state.md`
- `changes/PER-009/mechanism.md`
- `docs/architecture/product-architecture-baseline-l0-l3.md`
- `docs/architecture/uworld-protocol-baseline-l4.md`
- `docs/architecture/perception-provider-baseline-v0.1.md`
