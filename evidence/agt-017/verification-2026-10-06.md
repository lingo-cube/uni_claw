# AGT-017 验证证据 — Slow 感知 live 接线 + 深度遍历（2026-10-06）

> 环境：emulator-5556（基准规则）+ 专用 3081（glm-5.3-flash，7890 代理）。
> 代码：`DshSlowConsult` 桥（Agent.Dsh，公开缝）+ Host/Host.Dsh 注入 + profile slow 段与深度预算。

## A1 桥确定性合同（6/6）

`tests/UniClaw.Agent.Dsh.Tests/DshSlowConsultTests`（fake transport，零网络）：成功解析投影（经
`kernel.Process` 公开 P2 缝，ledger 落 canonical record）；端点错误→Rejected 带原始诊断零投影；
**有界等待强制**（300ms 界对 30s 传输 → TimedOut，实测 elapsed < 5s——异步保证）；非 JSON→诚实
Rejected；prompt 携带 target/claim/触发词；visual 未配置→NotConfigured。

## A2+A3 真实深度遍历回合（rounds/deep-coverage/run-20261006-070551-321）

**覆盖结果**：`Completed (terminal-emitted)`，**CoverageComplete rate=100%**，steps=19，
firstDivergence=NONE：

- root-page ✓；first-level-all-visible **7/7**；scroll-discovered 1/1；
  **second-level-pages 8/8（distinct routes）**；back-navigation 9 次；repeated-entry 1/1。

**Live Slow 首次真实生效**（cycle 17）：

| 环节 | 证据 |
|---|---|
| 触发 | 根页 fallback 身份 `android.settings` → `SemanticUnclear`（settings-trace cycle 17） |
| 桥 prompt | "…Claim under question: **ui.screen.route** (field: route). Trigger reason: slow-trigger:SemanticUnclear. Fast perception context: yolo=[…]"（DSH 会话日志） |
| 真模型回答 | `{"status":"Succeeded","semanticDisposition":"Unknown","proposals":[{"subject":"ui.screen.route","value":"Unknown"}]}` |
| 投影 | 桥解析 → `kernel.Process`（P2 公开缝）→ trace：**`SemanticUnclear|Succeeded|projected=1`** |
| 服务端记录 | `~/.dsh/storages/session_projcache/sessions/session-043186a9-0f59-404d-92f0-252acfe114fd.json`（turnOutline turns[0] response） |

模型对"当前屏幕路由是什么"诚实回答 Unknown（fallback 页确实无标题可辨）——**slow 感知没有编造**。

**异步保证**：budget 6 次、实际用 1 次；cycle 17 fastLatency=0.69s / hierarchy=1.95s，slow 在
15s 有界内完成，周期未阻塞；超时路径由确定性测试承载（零投影 + 界内返回）。

## 回归

Host.Tests 159/159、Kernel.Tests 798/798、Agent.Dsh 桥测试 6/6。profile 钉值测试已按深度基线
（48/48/6/8）更新；ledger 语义测试改为独立有界配置（不再绑 profile 值）。

## 已知边界（如实）

- slow 投影内容未在 run dir 落盘（trace 只记 trigger|status|projected=N）——投影声明查证需读
  kernel 侧或 DSH 会话日志；后续可在桥/饲料层补投影明细落盘（候选改进，非本轮）。
- 触发词 `SemanticUnclear` 仅在 fallback 路由命中（rk1 身份的根页重访不触发）——本轮 20 周期
  仅 1 次触发属预期语义，不是缺陷。
