# ARCH-DOC-023 — ARCH-ALIGN-001 收口补注：ADR-0047 引用链、评审档状态终态化与裸层号残留清理
lifecycle_state: closed · disposition: none · depth: standard · base: 14987fe

## Intent（WHAT/WHY）

ARCH-ALIGN-001（14987fe，2026-10-12）收口审核发现三类收尾缺口：协议基线
P2/P3/P7/P8 四处内容按第十轮裁决修改但无 ADR-0047 引用（ADR 成为孤儿）；
头部 amendment 注只列 P1/P24/P25，读者看不出 P2/P3/P7/P8/P23 也在修订范围；
评审总档头部仍是 `REVIEW_IN_PROGRESS`（与其父 Change 已收口不符）；另有四处
裸层号残留在权威文档内（产品基线 §23 "L4 详细设计"、simulation-baseline
"六个 L2"/"L2 typed seam"、UWorld §2.1 映射表无 T* 对应、CONTEXT
ContainerGraph 词条）。本 Change 只做文档对齐补注，零语义变更。

## Scope / Out of Scope

In：
- `docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md`：
  头部 amendment 注补全修订范围（P2/P3/P7/P8/P23 + ADR-0047）；P2/P3/P7/P8
  Status 行补 ADR-0047 引用。
- `docs/analysis/uniclaw-architecture-protocol-overall-review-2026-10-11.md`：
  头部状态 `REVIEW_IN_PROGRESS / ARCHITECTURE_DIRECTION_ACCEPTED` →
  `COMPLETE / DELIVERED-BY-ARCH-ALIGN-001`；说明行改为矩阵已交付、开放项
  以 §13.3/§15.1 为准。
- `docs/architecture/product-architecture-baseline-l0-l3.md` §23：裸
  "L4 详细设计" → "UIWorld L4 域协议详细设计"（带域命名空间注）。
- `docs/architecture/simulation-baseline-v0.1.md`："六个 L2"→"六个
  Product T2 责任域"、"L2 typed seam"→"Product T2 typed seam"；头部加
  术语对齐 provenance 行（约束 C1–C9 原文不动）。
- `docs/architecture/uworld-protocol-baseline-l4.md` §2.1：表前加历史层号
  ↔ Product T2/T3 对应注（ADR-0042 §4 迁移映射要求）。
- `CONTEXT.md`：ContainerGraph 词条的 "L4" 加 UIWorld 域命名空间限定。
- src/ 现行 README 产品层号残留清理（ARCH-ALIGN-001 命名迁移注声称已改、
  实际漏改的部分）：Agent.Dsh（L1/L3）、Agent（L3/L2）、Core ×2、Host、
  Kernel 根 §Core 缝（跨 L2）、Kernel/Runtime ×3（标题 L1 本体 + 正文 +
  组装顺序）、六责任域 README 标题（L2·基线 §N）与 Control 表格行。
  Capability 阶梯 L0–L3（Capability/Perception README、capability-component
  skill）是独立命名空间，不动；历史 ADR/Change/design 中的旧层号按
  ADR-0042 §4 不改写。

Out：不改协议语义/状态值（只加引用）；不改 ADR 正文；不做协议基线文件名
迁移（ADR-0042 显式 defer 给后续协议 Change）；不改产品代码；不处置评审
总档 §13.1/§14.2/§15 的开放实现项（属后续实现 Change）。

## Decisions

- D1：只做加法补注与状态行引用，不重写任何冻结正文；simulation/UWorld 两
  份 FROZEN 文档的改动采取"内联最小替换 + 头部/节前 dated 注"双留痕。
- D2：评审总档新状态词避免 analysis 目录禁词（裸 FROZEN/CLOSED 词边界），
  用 `COMPLETE / DELIVERED-BY-ARCH-ALIGN-001`。
- D3：ADR-0047 引用补在协议基线四处 Status/语义行 + 头部 amendment 注，
  使"为什么 P2/P3/P7/P8 语义变了"可从 canonical 协议文档直接溯源。

## Acceptance

| # | 判据 |
|---|---|
| A1 | `git grep -n "ADR-0047"` 至少命中协议基线 5 处（头部 + P2/P3/P7/P8），孤儿状态消除 |
| A2 | 评审总档头部状态为 COMPLETE 族（合法 analysis 状态词），且不含裸禁词 |
| A3 | 产品基线/simulation/UWorld/CONTEXT 四文件的裸层号残留均带命名空间限定或对应注 |
| A4 | src/ 现行 README 的产品层号残留清零（Capability 阶梯与显式历史引用除外） |
| A5 | DocsMetadataTests + SourceReadmeOwnershipTests 全绿（执法面不回归） |

## Constraints

- 零语义变更：协议边 Producer/Consumer/Payload/Status 结论一字不改，
  只允许追加引用与括注。
- 旧层号在历史 ADR / Change State / 证据中不改写（ADR-0042 §4）。

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "git grep 探针（ADR-0047 引用数、残留裸层号清点）+ DocsMetadataTests 四规则逐字复刻脚本（evidence/arch-doc-023/replica-checks.ps1）+ git diff --check"
  expected: "ADR-0047 在协议基线命中 ≥5；目标残留清零；DocsMetadata 四规则全过；README .cs 登记零回归；无空白错误"
  actual: "ADR-0047 命中 5 处（协议基线头部/P2/P3/P7/P8）；docs/architecture 与 src README 产品层号残留清零（剩余命中均为 Capability 阶梯命名空间或显式历史引用）；复刻脚本 Rule1-4 ALL PASS（Rule1 0 violations）；diff 中含 .cs 的删除行 2 条、stem 全部保留；git diff --check clean。xunit 本体因环境无法执行：HTTPS 被沙箱 schannel 拦截（SEC_E_NO_CREDENTIALS；HTTP 301 通）、nuget restore 结构性不可达。补偿验证 2/2 全绿：①DocsMetadataTests 四规则逐字复刻（replica-checks.ps1）ALL PASS；②SourceReadmeOwnershipTests 逐字复刻（replica-source-readme.ps1，同 29 条归属映射/双执法线/obj-bin 豁免/>50 下限）167 文件受检 0 违规。Owner 指示收口；xunit 复跑降级为事后核对项（非阻塞）"
  evidence: "evidence/arch-doc-023/replica-checks.ps1 · replica-checks-output.txt · replica-source-readme.ps1 · replica-source-readme-output.txt（均 PASS）"
```

收口备注（Owner 指示，2026-10-12）：两项执法测试在本环境以逐字复刻脚本替代执行并全绿；网络恢复后建议补跑一次 xunit 作事后核对（非阻塞）：
`dotnet test tests\UniClaw.Kernel.Tests\UniClaw.Kernel.Tests.csproj --filter "FullyQualifiedName~DocsMetadata|FullyQualifiedName~SourceReadmeOwnership"`

## Status log

- 2026-10-12 · UNDERSTAND → RESOLVE → PERSIST · 审核结论确认三类收尾缺口
  属实；开立 ARCH-DOC-023，范围锁定为纯文档补注。
- 2026-10-12 · IMPLEMENT → REVIEW · 七文件编辑完成（协议基线 5 处、评审
  总档头部、产品基线 §23、simulation ×3、UWorld §2.1、CONTEXT ×1）；
  自查 old_string 逐处唯一命中。
- 2026-10-12 · REVIEW → VERIFY · 待跑执法测试与 grep 探针。
- 2026-10-12 · VERIFY（补偿验证完成，xunit 待环境恢复） · grep 探针 A1/A3/A4
  全过；dotnet test 两次后台运行均卡在 nuget restore（nuget.org 经代理隧道
  失败、沙箱 TLS schannel 报 SEC_E_NO_CREDENTIALS、本机与 D:\space-x 无离线
  包缓存），判环境失败；以逐字复刻 DocsMetadataTests 四规则的确定性脚本
  （evidence/arch-doc-023/）替代执行，Rule1-4 ALL PASS；README stem 零回归。
  按 UniFlow A7 留在 VERIFY；恢复条件与收口命令已记录于 Verification。期间
  新发现并修复：① ARCH-ALIGN-001 命名迁移实际漏改 10 个 src README（本
  change 补齐）；② PS5.1 以 ANSI 读无 BOM UTF-8 会吞行界（复刻脚本首轮
  假红根因，脚本已改 ASCII 并用 .NET UTF-8 读文件）。
- 2026-10-12 · VERIFY → CLOSED · 网络终判：HTTP 出网通（301）、HTTPS 被沙箱
  schannel 拦截（SEC_E_NO_CREDENTIALS）——nuget restore 在本环境结构性不可达。
  补齐第二项补偿验证：SourceReadmeOwnershipTests 逐字复刻（同映射/同豁免/同
  下限）167 文件 0 违规 PASS；两项执法面均有等价确定性证据。Owner 指示完成
  收口；INDEX 再生；xunit 复跑降级为事后核对项。
