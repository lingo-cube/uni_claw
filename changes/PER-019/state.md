# PER-019 — UniPerception 组件化：默认异步流水 + 组合注册

lifecycle_state: closed · disposition: none · depth: standard · base: 84a3abc2

## Intent（WHAT/WHY）

所有者裁决（2026-10-06，两轮）：

1. **命名**：组合能力更名 Text Semantic Perception → **UniPerception**（id `uni.perception`）——语义识别只是其能力之一，它同时提供 UI 元素识别（双协议）；本仓自定义组件。
2. **组件行为**：默认**异步**——Fast（YOLO+OCR 布局/元素）+ XML/hierarchy **先行**入世界模型；Slow 文本模型语义判读**晚到**补语义结果（带 basis 关联与 provenance）；不阻塞感知周期。
3. **组合注册**：composition root 建 Product Capability Registry，注册 `uni.perception`（组合，依赖 fast.yolo/fast.ocr/slow.text）与 `slow.visual`（独立）。

## Scope

- Agent.Dsh 桥 Fetch/Project 分离（Fetch 零 kernel 副作用可后台执行；Project 单线程驱动线程执行）——已落地。
- Host 新增 `UniPerceptionPipeline`（异步流水：发射即返回、周期 Poll 晚到投影 IsLate、路由变更诚实丢弃 Unaligned、预算发射时计、超时零投影）。
- Feed 集成：周期头 Poll（落成/丢弃），触发时 Dispatch（非阻塞）；trace 令牌 Dispatched/Landed/Dropped/TimedOut。
- 组合根：`PerceptionCapabilityComposition`（Host）注册双能力，Program 落 capability-facts 到 run dir。
- 消费 semanticDisposition（B 项）：Landed 令牌携带。
- 投影 lineage 带 fast basis 关联（A 项最小面）：`basis:<captureId>`。

## Out of scope

- C 项（真实 EvidenceIds 上下文投影）独立后续；PerceptionAssessment 完整类型后续；不改 Kernel internals/授权面。

## Acceptance

| ID | 行为 | 证据 |
|---|---|---|
| A1 | 异步默认 | 确定性：发射即时返回（周期不等）、晚到 IsLate 投影、路由变更丢弃、超时零投影、预算发射计 |
| A2 | 组件注册 | 注册表测试 + run dir capability-facts |
| A3 | 真实生效 | 真机回合 trace 含 Dispatched→（Landed 或 Dropped/TimedOut），周期无阻塞；disposition 入 trace |

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST → PLAN → IMPLEMENT · 命名两轮裁决落定（Unified→UniPerception）；桥 Fetch/Project 分离完成（8/8 绿）；流水实现中。
- 2026-10-06 · IMPLEMENT → REVIEW → VERIFY → CLOSED · 组件异步化落地：UniPerceptionPipeline（发射即返回/晚到 IsLate 投影/路由变更诚实丢弃/超时零投影/预算发射计，6/6 测试）；feed 集成 + traversal KernelProvider 补齐；组合根注册 uni.perception+slow.visual（facts 落 run dir）；B 项 disposition 入 trace、A 项 basis 关联入 lineage。真机（glm）：Dispatched→Dropped(unaligned) 异步语义闭环、周期零阻塞；首轮真机抓出两个真缺陷（basis 相关性漏对齐、facts 落盘位置 NRE）并修复。全量回归绿（165/140/798）。另按所有者指令切默认模型 deepseek-flash：配置生效但真机两轮全部 no-submit-decision（与 2026-10-02 deepseek 行为不稳同款，确定性不兼容）——链路诚实 fail-closed，默认值保持指令值待裁决。证据 evidence/per-019/verification-2026-10-06.md。
