# AGT-018 — UniAgent 维度 README（大脑定位纠正 + tools + realization 可替换）+ Memory System 组件声明
lifecycle_state: closed · disposition: none · depth: standard · base: d5af89bb

## Intent（WHAT/WHY）

ARCH-DOC-020 补齐 Kernel 维度后，L1 的 UniAgent（`src/UniClaw.Agent/`）
仍无 README；Memory System（L1 四组件中唯一零代码）按所有者裁决放
占位声明。所有者对定义草案的修正（2026-10-07）：

1. **UniAgent 不是「面向用户的完整智能主体」——它是整个系统的大脑**
   （概念纠正）；
2. 它可以调用**自己定义的 tools**；
3. 必须描述清楚 **realization 可替换**：现在用 DSH 实现，可在别处用
   别的方式实现；宿主（DSH）既有能力经 adapter **复用**映射进来，
   而不必自研；
4. Memory System = 需要**声明出来**的记忆组件；可由 DSH 组件映射过来；
   能力 = 给 UniAgent 提供记忆，管理系统常用规则/常用知识。

## Scope / Out of Scope

In：`src/UniClaw.Agent/README.md`（新）；`docs/design/
memory-system-component-declaration-v0.1.md`（新，CANDIDATE/NONE）；
`docs/design/README.md` 索引同步（新增 memory 行 + 修复 CAP-010 遗留的
capability 协议指南状态行孤儿）。
Out：不改基线 §3.1 措辞（L0 重开有条件，见 D2 偏差记录）；不改任何
代码；Memory System 不预造实现。

## Decisions

- D1：README 定位以所有者裁决（"系统的大脑"）为准；sole owner 权威
  （§3.1/§6）与四不边界不变——纠正的是定位表述，不是 L0 结构。
- D2：基线 §3.1「面向用户的完整智能主体」与所有者裁决存在表述偏差，
  在 README 与本 change 如实记录为待办（基线措辞修订需独立 change；
  L0-L3 语义重开条件见基线 §23）。
- D3：tools 表述挂靠既有权威：tool 词汇归 UniAgent，经 realization
  投影执行（ADR-0038：DSH Tool 是 Binding/Exposure 非能力类型）；
  realization 可替换挂 ADR-0022（dual full realizations 先例）。
- D4：Memory System 占位放 `docs/design/`（CANDIDATE / Authority: NONE，
  符合 docs 分类学：未裁决候选），升格条件 = 真实 buyer + 实现 change。

## Acceptance

| # | 判据 |
|---|---|
| A1 | README 定位=大脑（含偏差注记）、含 tools 与 realization 可替换两节、8 文件归属表、Agent.Dsh 不进归属表 |
| A2 | Memory 声明落地 design/（CANDIDATE/NONE + buyer=UniAgent + 零落地状态）并入 design 索引 |
| A3 | design 索引 capability 行状态修正（CAP-010 孤儿清理） |
| A4 | DocsMetadataTests 绿；Agent 8 文件覆盖核对通过 |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "dotnet test Kernel.Tests --filter DocsMetadata；Agent 文件覆盖核对"
  expected: "全绿；8/8 覆盖"
  actual: "DocsMetadata 4/4 绿；Agent 覆盖 8/8；design 索引同步（新增 memory 行 + capability 行状态修正）"
  evidence: "evidence/agt-018/"
```

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 所有者四点修正落定，
  开立 AGT-018。
- 2026-10-07 · PERSIST → IMPLEMENT → VERIFY → CLOSED · README（大脑定位
  + 偏差注记 + tools + realization 可替换）与 Memory 声明（design 占位）
  落地；design 索引同步含 CAP-010 孤儿清理；8/8 覆盖、DocsMetadata 4/4
  绿；INDEX 已再生。
