# PNL-001 / PNL-002 / PNL-003 收尾证据（2026-10-03）

## 范围

本记录只证明 UniClaw Workspace/Workbench v0.1 的当前只读交付可以关闭；它不
宣称 Trace 采集平台、完整 OTel 归一化、Project/TestSet 写管理或诊断 Skill
注册已经完成。后续缺口见
[`docs/reports/PNL-003-observability-project-management-gap-report.md`](../docs/reports/PNL-003-observability-project-management-gap-report.md)。

## Closure matrix

| Change | 处置 | 关闭依据 |
|---|---|---|
| PNL-001 | `CLOSED / superseded` | 基础 Task protocol、repository、instantiate 和兼容 capability 已保留；旧独立面板/preset Gate 由 PNL-002/003 Workspace 承接，不再产生独立实现义务 |
| PNL-002 | `CLOSED` | Product Workspace vertical slice 的项目→任务实例→session→对话/Trace/Evidence/Metadata 只读链路在合同测试和专用 3083 真实任务上通过 |
| PNL-003 | `CLOSED` | Host-neutral shared frontend、DSH adapter/browser bridge、双入口回归、部署 drift 和真实 Android Settings 场景均通过 |

## Reproducible verification

| method | expected | actual | evidence |
|---|---|---|---|
| `cd web/uniclaw-workspace && npm test` | shared frontend tests green | **66/66 passed** | command output 2026-10-03 |
| `cd dsh/uniclaw-task-workbench && npm test` | DSH host/client/bridge tests green | **50/50 passed** | command output 2026-10-03 |
| `npm run build:browser` | shared modules generated into DSH browser bundle | `generated 7 shared modules` | command output 2026-10-03 |
| `./dsh/deploy.sh --check-only --port 3083` | deployed profile has no drift | `DRIFT CHECK: clean (29 files compared)` | deploy output 2026-10-03 |
| `node --check` + `git diff --check` | syntax and whitespace checks clean | passed for shared/browser entrypoints and final docs diff | command output 2026-10-03 |
| real DSH `3083` browser scenario | 真实任务可读请求→决策→提交、来源切换、明细与返回 | Android API 35 `emulator-5556`：3 rounds（2 act + 1 noAction）、DSH 50、UniClaw 200、combined 250；Evidence 5 files；Metadata device/runtime；modal close-back | `evidence/PNL-003-WI-PNL003-016.md`；`evidence/pnl003-real-task-android-settings-20261003/` |

## Closure boundary

- PNL-001 的 preset 收窄机制不在当前 Host 平台上继续扩张；需要时另立 Change。
- Trace 字段完整性、Metadata provenance、Project/TestSet catalog 和诊断 Skill
  管理是下一轮输入，不是当前 UI vertical slice 的失败。
- DSH 继续拥有 Host session、原生事件和 lifecycle；UniClaw 继续拥有 Product
  Session/Run 语义；Workspace 只读组合与展示。
