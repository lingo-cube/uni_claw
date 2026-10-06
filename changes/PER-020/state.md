# PER-020 — Perception 维度代码归属 README + 冻结基线 L0 组成图补齐（双协议为纲）
lifecycle_state: closed · disposition: none · depth: standard · base: 1d031cb7

## Intent（WHAT/WHY）

Perception 维度抽样检查（CAP-010/011 之后的第二个维度）发现管理文档缺口：
28+ 文件无代码归属 README；冻结基线 §1 L0 组成图只列 6 文件（2026-09-12
时点），Fast/Slow 家族、Fusion/、UiHierarchy/ 漂在文档外。

所有者内容裁决（2026-10-07，对预列清单的审核意见）：

1. **能力身份 = 两个协议接口**：`ISemanticPerception`（语义感知）+
   `IUiElementPerception`（UI 元素感知）——这两个接口要说清楚；
2. **UniPerception 是基于双协议的 union 组件**（同时实现两者）；
3. **快慢（Fast/Slow）是隐性概念，降级但保留**：不是能力协议，是 union
   内部的异步实现策略（所有者裁决 2026-10-06：行为默认异步，Fast+XML
   先行入世界模型、Slow 晚到补语义）——概念必须说清，不能弄错；
4. 文档要表达**要做的方向**（协议负载词汇冻结、slow.visual 实例化等）。

## Scope / Out of Scope

In：新增 `src/UniClaw.Kernel/Perception/README.md`；冻结基线 §1 组成图补
12 行缺失模块 + 双协议/快慢概念澄清 + 头部升 v0.2（文件名保留 v0.1 字样
维持历史引用，版本以头部为准）。
Out：不改任何代码；不改 §2 数据流/§3 契约/§4-§8；不 supersede
ADR-0020/0021（§3 语义数组零变化）；不做其余 11 个维度的 README（后续
系统性 change）。

## Decisions

- D1：README 以双协议接口为纲（§1 身份 → §2 union 组件 → §3 快慢降级为
  异步策略 → §4-§5 采集传输与子目录归属 → §6 方向），而非以 Fast/Slow
  分组为纲——快慢只在 §3 作为实现策略出现。
- D2：冻结基线只做加法：12 行文件归属 + 一段概念澄清（能力身份=双协议、
  UniPerception=union、快慢=内部异步策略）；头部 v0.1→v0.2 + 修订注记。
- D3：文件名不改（5 处历史引用保持有效），版本语义以头部 Status 为准。

## Acceptance

| # | 判据 |
|---|---|
| A1 | README 存在且以双协议为纲，快慢明确降级为异步策略并保留概念，含方向节 |
| A2 | 基线 §1 图覆盖目录全部 31 文件（6 既有 + 12 新行 + 2 子目录行） |
| A3 | 基线头部 v0.2 + 修订注记；§3 契约字节不变（diff 核对） |
| A4 | DocsMetadataTests 绿（architecture 目录仍含 FROZEN/COMPONENT-BASELINE 声明） |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "dotnet test tests/UniClaw.Kernel.Tests --filter 'FullyQualifiedName~DocsMetadata' ; git diff 核对 §3 零变化"
  expected: "DocsMetadata 绿；基线契约节零 diff；README/图与目录实况一致"
  actual: "DocsMetadata 4/4 绿；基线 diff 仅 4 hunk（头部/注记/§1 图/概念段），§2-§8 零变化；README↔目录 31/31 全覆盖（coverage-check）"
  evidence: "evidence/per-020/"
```

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 所有者四点内容裁决落定，
  预列清单按修正框架开立 PER-020。
- 2026-10-07 · PERSIST → IMPLEMENT → REVIEW → VERIFY → CLOSED · README
  落地（双协议为纲、快慢降级为异步策略、含方向节）；基线升 v0.2（§1 图
  +14 行 + 概念澄清，§3 契约零变化）；Review 自查发现 UiHierarchy 表漏
  点名 5 文件，当轮补齐；31/31 覆盖核对与 DocsMetadata 4/4 绿。
