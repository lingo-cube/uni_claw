# ARCH-DOC-020 — Kernel 维度 README 补齐：按 L0-L3 架构粒度重划（非目录一概而论）
lifecycle_state: closed · disposition: none · depth: standard · base: a649a706

## Intent（WHAT/WHY）

Perception 抽样（PER-020）后，Kernel 尚余目录无代码归属 README。所有者
裁决（2026-10-07）：**不按 11 个物理目录一概而论，按基线 L0-L3 已做的
架构划分重划维度**——六个 L2 责任域各自是核心组件维度；Trace、
Diagnostics 各自独立；Runtime（L1 Uni Kernel 驱动面）独立；Outcome 并入
Run 维度；Core 归 Kernel 根级总索引。一个 change 全做，UniAgent 不在本批。

## Scope / Out of Scope

In（10 个 README，全部新增，零代码改动）：
- 六 L2：`Evidence/` `World/` `Run/`（含 `Outcome/` 归属说明）`Control/`
  `Assurance/` `Effects/`（含 `ExecutionSource/`）；
- 观测面：`Trace/`、`Diagnostics/`；
- `Runtime/`（Uni Kernel 驱动面，独立维度）；
- `src/UniClaw.Kernel/README.md`（根总索引：装配定位 + 全维度导览 +
  `Core/` 投影缝一节）。

Out：不改任何代码；不改基线/ADR；UniAgent（`src/UniClaw.Agent/`）与
Memory System（不在 Kernel 树）后续另立；不动既有 Capability/Perception
README。

## Decisions

- D1（所有者裁决落地）：维度划分 = 基线 §10 六 L2 + Trace + Diagnostics +
  Runtime 驱动面 + 根索引；物理目录≠维度（Outcome→Run、Core→根）。
- D2：README 是**代码归属索引**，不复述基线权威——每篇以指向方式引用
  基线相应 §（canonical ownership / sole authority / 不拥有）与 ADR，
  只做「目录文件 ↔ 架构归属」映射与变更规则，避免第二真相源。
- D3：统一骨架（定位→代码归属表→子目录→变更规则→指向），小维度
  （Diagnostics 单文件）相应缩短。

## Acceptance

| # | 判据 |
|---|---|
| A1 | 10 个 README 落地，维度划分与基线 §10/§7 对应（非目录平铺） |
| A2 | 每篇引用对应基线章节与 ADR，无权威内容复制（抽查 diff 核对） |
| A3 | 覆盖核对：Kernel 全部 13 目录的 .cs 文件在对应 README 中点名 |
| A4 | DocsMetadataTests 绿；全量 Kernel.Tests 无回归 |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "覆盖核对脚本（目录↔README 点名）+ dotnet test Kernel.Tests"
  expected: "13 目录全覆盖；测试全绿"
  actual: "覆盖 72/72（含并行会话期间新增的 5 文件，当轮补入复核）；Kernel.Tests 841/841 绿（含 DocsMetadata 4/4）"
  evidence: "evidence/arch-doc-020/"
```

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 所有三点裁决经问询确认
  （Runtime 独立/Outcome→Run+Core→根/一个 change 全做）；开立 ARCH-DOC-020。
- 2026-10-07 · PERSIST → IMPLEMENT → REVIEW → VERIFY → CLOSED · 10 个
  README 落地；Review 自查发现并行会话新增 Run/Control/Assurance 5 文件
  漏点名，按头注释补入后 72/72 复核；Kernel 841/841 绿；evidence 与
  INDEX 已再生。
