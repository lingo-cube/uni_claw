# AGT-017 — Slow 感知 live 接线与深度遍历（异步保证）

lifecycle_state: closed · disposition: none · depth: standard · base: b93a009c

## Intent（WHAT/WHY）

所有者指令（2026-10-06）："加，然后继续跑更深度的测试，然后确保是异步的"。

现状事实：DSH peer 已有真实 slow 通道（`ExecuteSlowAsync` → `/api/uniclaw-agent/slow`，服务端要求 attached 会话、单飞行）；但产品 Host 的 slow 编排默认绑确定性回放桩（`SlowOrchestration` 硬类型 `DeterministicSlowRealization`），profile 无 slow 段——真机链路 slow 从未生效。直接开配置会产出假 slow（回放桩冒充模型）。

目标：把真实模型 slow 咨询接入产品 Settings 链（公开缝，不动 Kernel internals 与冻结 InternalsVisibleTo 纪律）；开启 profile slow 段与更深遍历预算；真实跑深度遍历并验证异步保证（有界等待不阻塞周期、超时零投影、诚实状态）。

## Scope / Out of scope

### 范围

- Agent.Dsh 新增 `DshSlowConsult` 桥：公开缝（`SlowConsultationRequest/Outcome` + `kernel.Process` P2）→ peer `ExecuteSlowAsync`；有界等待强制；错误/超时诚实映射（Rejected/TimedOut 零投影）。
- Host：`HostOptions.SlowConsult` 注入缝 + feed 构造参数（默认回放桩不变——测试与旧行为零影响）。
- Host.Dsh：组合桥与决策通道共享同一 attached peer；slow 模型 = 会话模型（adapter 层解析，遵守模型路由纪律）。
- profile：slow 段（enabled/boundedWaitMs=15000/maxRequestsPerRun=6/text-only）+ 深度预算（secondLevelPages 2→8、maxSteps/maxConsultRounds 24→48、maxScrolls 4→6）。
- 确定性测试（fake transport）+ 真实深度遍历回合 + 证据。

### 不在范围

- 不改 Kernel internals/InternalsVisibleTo（冻结测试缝）；不引入 visual slow 模型绑定（桥留接口，profile visualEnabled=false）。
- 不实现晚到结果跨周期投影（IsLate 机制留给异步感知运行时；本桥超时即 TimedOut 零投影——如实的有界异步）。
- 不动决策通道语义与授权面。

## Acceptance

| ID | 行为 | 证据 |
|---|---|---|
| A1 | 桥单元行为 | fake transport 测试：成功解析投影、错误→Rejected 带诊断、超时→TimedOut 零投影、坏 JSON→诚实失败 |
| A2 | 真实 slow 生效 | 深度遍历 trace 含 slow 条目（trigger\|status\|projected=N），真实模型往返 |
| A3 | 异步保证 | 有界等待被强制（周期不超 bound）、超时零投影周期继续、所有状态诚实入 trace |
| A4 | 深度遍历 | secondLevelPages=8 预算下的真实覆盖结果（无论终局，四元组如实） |

## Verification（2026-10-06 回填）

| level | method | actual |
|---|---|---|
| DETERMINISTIC | DshSlowConsultTests 6/6 + Host 159/159 + Kernel 798/798 | PASS（有界等待强制/错误诚实/解析投影全绿） |
| ENVIRONMENT | 深度遍历真实回合（48/8 预算） | PASS：CoverageComplete 100%、second-level 8/8、steps=19、divergence NONE；**live slow 首次生效**（cycle 17 SemanticUnclear→真模型往返→投影 1 条，全链证据见 evidence/agt-017/verification-2026-10-06.md）；异步保证：budget 6 用 1、周期未超界 |

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST → PLAN → IMPLEMENT · 事实链：peer 通道在、服务端契约（attached/单飞行）已读、Kernel 编排硬绑回放桩、InternalsVisibleTo 冻结——设计定公开缝桥；实现中。
- 2026-10-06 · IMPLEMENT → REVIEW → VERIFY → CLOSED · 桥（Agent.Dsh 公开缝）+ Host/Host.Dsh 注入 + profile（slow 段 + 深度预算 48/8）落地；确定性 6/6+159+798 全绿；真实深度遍历 CoverageComplete 100%（7/7 一级、8/8 二级路由、19 步）；**live slow 感知首次真实生效**：SemanticUnclear 触发 → 真模型 glm-5.3-flash 经 /slow 往返（DSH 会话日志留痕）→ 桥解析 → kernel.Process 投影 → trace 记录；模型对模糊路由诚实答 Unknown 未编造。A1–A4 全部有证据。
