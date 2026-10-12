# ADR-0048：Observation Origin 的封闭值域与机械推导判定权

状态：accepted（2026-10-12，协议修复 grill——记录见 `docs/design/protocol-completeness-formal-audit-v0.1.md` 附录 C；实现经 PROTOCOL-FIX）

ADR-0047 冻结了 observation origin 的语义（外部观察 vs post-action effect-flow 自产观察）并自记"当前类型缺独立字段"。形式审计（同上附录 A）确认该缺口是 MaterialEffect 判定、期望渗透缓解（D6）与前提显式化（E5）的共同根源。本 ADR 决定值域与判定权。

## Decision

### 1. 封闭二值值域

`Origin ∈ { External, PostActionSelf }`。扩值需协议 + 代码双改（`IngressKind` 先例）；
不设 `Unknown`——推导不出时保守归 `External`（误归外部 = 拒绝满足 MaterialEffect，
fail-closed 安全方向；误归 post-action = 假阳性，危险方向，必须避免）。

### 2. 判定权：显式 attempt 关联的机械推导

- 存在**精确 attempt correlation**（观察与具体 dispatch attempt 的显式关联键）→ 判
  `PostActionSelf`；
- 无关联键 → 判 `External`；
- **禁止以时间邻近作推导依据**（协议通则 8：时间相邻 ≠ 因果）；
- producer 自报 origin 仅作诊断字段，**不参与判定**（⑦ 自认自报不可核验）。

### 3. 落点

`ObservationProposal` / `EvidenceRecord` / `EffectReceipt` 回流侧统一携带；EvidenceId
canonical 输入含 origin（语义变化不复用同一 id，ING-006 D5 同律）。

## Consequences

- MaterialEffect 判定获得机械可复算的 origin 来源；D6（期望渗透）的判定输入显式化。
- E5 升格落地：conditional soundness 的信任面从"producer 全体诚实"收窄为
  "correlation key 不可伪造"——更小、更可执法的前提。
- Context（External | PostActionEffectFlow）与 Origin（谁发起的流程 vs 谁生产的
  观察）保持正交，不再互相代偿。

## Rejected alternatives

- **producer 自报为准**：⑦ 无核验手段，等于把健全性押在善意上。
- **时间启发式推导**：外部观察可能落在动作后时间窗内，误判即假阳性（危险方向）。
- **三值含 Unknown**：保守归 External 已覆盖其语义，多一个值多一条歧义路径。

## Known gaps（实现前如实记录）

- `ObservationProposal` 等类型尚无 origin 字段；attempt correlation 键的形状随
  PROTOCOL-FIX 切片定义（receipt↔observation 的显式关联）。
