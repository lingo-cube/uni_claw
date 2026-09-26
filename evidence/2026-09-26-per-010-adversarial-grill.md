# PER-010 正式 Adversarial Grill 报告

> 日期：2026-09-26 · 对象：`changes/PER-010/spec.md` v0.1.1（FROZEN）及其执法载体
> （PER-013 slice plan，当时未落盘）· 裁决：**PASS_WITH_FINDINGS**
> 取证：PER-010/011/012 spec · architecture baseline L0-L3 ·
> `src/UniClaw.Host/UiAutomatorDump.cs` · `src/UniClaw.Kernel/Runtime/{AgentDecision,ConsultationTypes}.cs`
> · compatibility inventory · 既有 grill alignment evidence。
> Owner 处置（2026-09-26）：findings 随 PER-013 实现指令携带（「这些 grill 过来吧」）。

## Verdict

```text
PASS_WITH_FINDINGS

REOPEN 触发项逐项检查：
  第二 truth owner          → 未发现（Evidence Ledger 唯一 Admission Authority，
                              baseline L180-181/L926；WorldModel 唯一 belief owner，L909）
  直接 effect bypass        → 未发现（bounds→Grounding→Assurance 链 spec L192-198；
                              AgentActionProposal 机械无坐标）
  Agent direct XML authority→ 未发现（AgentDecisionContext typed bounded，无 raw XML 字段）
  无法定义 freshness/admission→ 可定义（provenance 字段齐全，owner 明确：
                              admission=Ledger、freshness=Assurance）

条件：17 项 findings 中 F-E2（密码字段，M-H）与 F-C1（torn capture，M-H）
必须在对应 slice 开始前处置；其余随 slice gate 带走。PER-010 保持 FROZEN。
```

## 1. Authority findings

Face 1 五问：XML≠World truth 已写清（spec Out of Scope）；admission owner =
Evidence Ledger（P2，唯一 Admission Authority，判据=record integrity/provenance/
lineage/canonicalization，baseline L180-181/L926/L948-949）；最终 belief owner =
WorldModel Reconciliation（L909）；XML 不绕过 Ledger/WorldModel（PER-011 L58-61、
PER-012 §2 禁 legacy 反流）；Agent 拿不到 raw XML（spec L192 + 现状 schema）。

- **F-A1 (M)**：PER-010 未在自身文本交叉引用 admission 判据；PER-013 Slice 0/C
  inventory 必须显式列出，防「单 Ledger、判据私设」旁路。
- **F-A2 (M，记录性)**：PER-012 mismatch 表不完备——`UiAutomatorDump.cs` 还有
  `Bool()` 缺失→false（L314-315）、`ParseBounds` 失败→"0,0,0,0"（L317-323）、
  `Descendants("node")` 层级展平（L155）三项违规未录入。

## 2. Missing / Unknown semantics

确认：`ObservedValue<T>` 三态不可折叠；`字段缺失≠Observed(false)`、
`source unavailable≠element missing`、`无能力≠未判定`（spec L108-113）；
Empty ≠ absence；现状 `ElementEpistemic.Partial` 已是诚实降级方向。

- **F-B1 (M)**：attribute-absent 与 attribute-empty-string 未区分
  （bool 空串=missing 还是 malformed 未定）。落点 Slice C fixture。
- **F-B2 (L)**：`Empty` 判定缺「source 自认成功」佐证（防 service 退化误判合法空树）。

## 3. Freshness / correlation

确认：CaptureMetadata 必含 timestamp/correlation（ObservationCycleId 可选）；
「provenance 输入，非 freshness authority；freshness 由 Assurance 裁决」=
有 owner 的 deferral；PER-011 mutation→new cycle + baseline supersession（L888）
拒绝 `Effect t1 / XML<t1 / verify t2`。

- **F-C1 (M-H)**：torn capture / dump latency 无语义——单一 CaptureTimestamp
  无 start/end 语义、无 atomicity 声明；capture 窗口内 mutation 产生撕裂树却被当
  原子快照；PER-011 mutation marker 只覆盖 capture 之间。要求：timestamp 语义
  （=完成时刻）+ 可选 duration + 「mutation 落在 capture 窗口内 → Unaligned」。
- **F-C2 (L-M)**：ObservationCycleId optional → X6 对齐依赖；Slice F 实测缺失行为。
- **F-C3 (M)**：device/app switch 无 failure matrix 行（detector = window/package
  与预期 surface 不符 → Unknown/Unaligned，不是 Empty）。

## 4. Node identity

确认（八面最强）：occurrence-only 明文；index/XPath/path/bounds/resource-id/
Compose key 全部只做 association feature；PER-011 association 五态 + 歧义 fail-closed。

- **F-D1 (M)**：claim subject 命名通道——现状 `ui.node.{LocalId}`（index/
  resource-id 尾段）把 occurrence 特征固化为跨 revision key，WorldModel 会当同一
  实体 reconcile。typed path 的 claim subject 必须 capture/occurrence-qualified。

## 5. Parser / normalization（归档）

- **F-E1 (L)**：unknown attribute 规则未文字化（不猜值/记 diagnostic）。
- **F-E2 (M-H)**：password/敏感字段零策略——corpus 每节点带 `password` 属性，
  契约无 sensitive-field/data-minimization 规则；bounded projection 内容策略空洞。
  落点：类型层显式排除或脱敏 + provenance 记 redaction。
- **F-E3 (M)**：hierarchy 尺寸无界——无 size/depth bound；截断必须 = Partial +
  coverage limitation，不能静默丢节点。
- **F-E4 (L)**：field policy（Unknown vs 整 capture Malformed）未要求声明化。

## 6. Acquisition / failure

确认：五 outcome 不可折叠（SourceUnavailable 与 Empty 永不合并）；timeout/
cancellation 有上界、调用方 budget、清理；现状 3s timeout + Kill(entireProcessTree)
+ 原子 dump/cat/rm + ProbeStateMachine ≤3/run 已示范。

- **F-F1 (M)**：failure matrix 缺 owner/retry/escalation/effect-permission 列；
  对照要求缺 3 行（stale XML、post-effect old dump、device/app switch）。
  PER-013 须产出合并五列矩阵作为 acceptance artifact。

## 7. Grounding boundary

确认：「bounds、resource-id 只能作为 grounding evidence，必须经过 World belief →
Control Intent → Grounding → Assurance → Effect；禁止 XML bounds → direct click」
（spec L192-198）；`AgentActionProposal` 机械无坐标（「Kernel 仍须 fresh Grounding」）。

- **F-G1 (L，Slice E 必测)**：现状 `MapTargetStateClaim` IoU 直产 `{role}.state`；
  Slice E closure test 必须显式覆盖该函数反向不可达。

## 8. Agent context boundary

确认：AgentDecisionContext 为 typed bounded record（ClaimSummary/ElementSummary/
ScreenSummary），无 raw XML 字段；channel 只序列化此 context。X10 现状成立。

- **F-H1 (M)**：ElementSummary 携带 Bounds（「occurrence × XML 增强投影」）但不带
  capture/cycle 引用——Agent 无法区分 bounds 的 belief freshness；应加 cycle/capture
  ref 或 freshness 标记，并写明 bounds 是 decision evidence 非 grounding 输出。
- **F-H2 (L)**：`ElementEpistemic.Partial`（视觉单源降级）与 checked 三态 `Partial`
  撞名，Agent 提示层需消歧。

## 9. Required changes before freeze（实现前处置清单）

| # | Finding | 级别 | 落点 |
|---|---|---|---|
| 1 | F-E2 password/敏感字段内容策略 | M-H | PER-010 narrow amendment 或 Slice A 类型层执法 |
| 2 | F-C1 torn capture timestamp 语义/duration | M-H | PER-010 CaptureMetadata + PER-011 Unaligned 条件 |
| 3 | F-B1 empty-string vs absent 规则 | M | Slice C adapter contract + fixture |
| 4 | F-C3 device/app switch failure 行 | M | 合并 failure matrix + Slice F |
| 5 | F-D1 claim subject occurrence-qualified | M | Slice A/C exit |
| 6 | F-F1 合并五列 failure matrix | M | Slice D/E acceptance artifact |
| 7 | F-A1 admission 判据交叉引用 | M | Slice 0/C inventory |
| 8 | F-A2 mismatch 表补录三项 | M | PER-012 记录性补丁或 Slice 0 发现 |
| 9 | F-H1 ElementSummary cycle/freshness 引用 | M | Slice D |
| 10 | F-E3 hierarchy size bound / 截断=Partial | M | Slice A/C |
| 11 | X5/X6 测试场景无 slice 归属 | M | Slice D 纪律断言 + Final Grill G9 |
| 12 | F-E1 unknown attribute 规则文字化 | L | Slice C contract |
| 13 | F-E4 field policy 声明化 | L | Slice C |
| 14 | F-B2 Empty 需 source 成功佐证 | L | Slice C |
| 15 | F-C2 cycle id 缺失行为实测 | L | Slice F |
| 16 | F-G1 MapTargetStateClaim 反向 closure test | L | Slice E 必测项 |
| 17 | F-H2 Partial 撞名消歧 | L | 协议文档 |

PER-013 slice plan（当时草案）对照：X1–X4、X8–X10 有 gate 覆盖；X5/X6 无归属；
X7 半覆盖（scroll01 成对夹具未列入 Slice C exit）。

## 10. Owner questions（事后处置记录）

Q1 F-E2 层级 → 推荐类型层执法（Slice A 携带 Password 字段供脱敏执法 + Slice C
redaction policy）；Q2 F-C1 → 字段语义归 PER-010 侧（CaptureMetadata 增可选
duration，语义=完成时刻）、Unaligned 条件归 PER-011；Q3 X5/X6 → Slice D 只做
cycle 纪律断言 + Final Grill 增 G9；Q4 F-A2 → Slice 0 发现记录；Q5 → 本文件
persist + findings 并入 PER-013。Owner 指令（2026-09-26）：findings 全量随
PER-013 实现携带，按最终版 slice plan 落位。
