# Slice 1 Baseline Snapshot — physical-wifi-off-to-on-minimum-semantic-loop

- **Change**: `openspec/changes/physical-wifi-off-to-on-minimum-semantic-loop/`
- **Date**: 2026-08-12（实施开始前）
- **Purpose**: tasks 1.2 回归基准，供 3.3 对比（async seam 化不得改变 Fake 语义）

## 基线状态（实施前）

| 维度 | 基线 | 证据来源 |
|---|---|---|
| 确定性套件（SC-P1-001..005 + frozen 13 capability） | 全绿 | `docs/decisions/s0-baseline-ready-capstone-authorization-review.md`（411/411，tasks 1.1–4.1） |
| ArchitectureGuardTests（Guard 1–7） | 全绿 | S0 capstone 同批验收 |
| Perception 套件（Perception/ + Vision/ + fixture 一致性） | 全绿 | 上轮门禁验收（perception phase3/phase4 closure receipts 后全绿） |
| `dotnet build src/UniClaw.Runtime.sln` | 0 错误（仅存量警告 CS1591/CS8794/xUnit2017，不属本 change） | 实施前最后构建 |
| Traversal 语义（Select→Check→Execute→Observe→Verify→Branch、journal、retry、authority） | frozen | Phase 1 change closeout（SC-P1-001/SC-P1-005） |

## 对照方式（3.3）

- 实施后重跑完整 `dotnet test src/UniClaw.Runtime.sln`（Tier 0），以本文档为基准对照；
- 判据：Fake 确定性套件 + Guard + Perception 全绿 = async seam 未改变 Fake 语义（环境同步完成 → 行为等价）。

## 备注

- 已知 2 项测试基础设施偶发（`PF01_ProcessRunner_TimeoutKillsShortLivedChildWithoutShellInterpolation`、`Vision.CORR_HOST04_RestartReverifiesRealChild`）归类为 infra 非确定性，不计入回归判定。
- 本次实施变更点（Traversal async seam / Startup.AttachAsync / Agent await / 新宿主项目）均不触及基线断言语义。
