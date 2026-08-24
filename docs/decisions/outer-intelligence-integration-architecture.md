# 外层智能体接入架构（Outer Intelligence Integration）— 设计文档

> **Alignment note (2026-08-19):** This is a **deferred design document**,
> subordinate to [UniAgent Architecture v1](../architecture/uniagent-architecture-v1-core-development-guide.md).
> Per v1 invariant 15, **DSH is the implementation framework, not an
> architecture concept.** Where this document frames "DSH 是宿主" / "DSH 是
> host", that describes DSH as the *implementation* of the v1 host roles
> (Composition Host / AgentHost / Capability hosting), NOT DSH as architecture.
> The abstractions defined here (`IIntelligenceProvider`, `TaskSpec`,
> `AgentProfile`, `IntelligenceSeam`) are **Reserved Extensions** under v1
> invariant 19 — not active architecture, not implemented, and must not be
> introduced until a real buyer authorizes them via a fresh gate.

> Status: DESIGN_DISCUSSION_CONVERGED (pending OpenSpec propose) — DEFERRED
> Date: 2026-08-16
> 母本: `docs/decisions/semantic-agent-runtime-target-architecture-review.md`（Phase 5/6）
> 现状基线: `docs/decisions/semantic-agent-runtime-current-state-review.md`（2026-08-16 update）
> 约束: 14 Architecture Invariants (I-1..I-14) 为不可违反边界；OBS-F9 GoalEvidence 语言冻结；I-2/I-3 不破。
> 用途: 后续 OpenSpec change（`dsh-outer-intelligence` / `kernel-intelligence-seam`）的母本。

---

## 1. 核心定位：观测层与智能层是独立抽象，DSH 是宿主

观测和智能是**问题域的两个抽象**，定义在 UniClaw 侧，与宿主无关。DSH 恰好是能承载它们的 harness——它提供 LLM/VLM/记忆/UI/编排，但不拥有抽象。

```
┌─ UniClaw 问题域（Kernel 侧契约，与宿主无关）────────────────┐
│  IObservationSource（观测抽象）                              │
│    只读面: snapshot / events / trap / evidence              │
│    真实实现: DriverHost 投影（Kernel 公开面）                 │
│                                                            │
│  IIntelligenceProvider（智能抽象）                           │
│    定义: TaskSpec / Capability+SemanticObject 目录          │
│    能力: LLM / VLM / 记忆 / 分析 / 异常诊断                  │
│    真实实现: DSH（宿主提供）或未来其它宿主                    │
└──────────────┬──────────────────────┬──────────────────────┘
               │ 契约（wire/接口）       │ 适配器注入
┌──────────────▼──────┐   ┌────────────▼─────────────────────┐
│ Kernel Agent        │   │ DSH（宿主/harness）               │
│ · IntelligenceSeam  │   │ · 承载 IIntelligenceProvider 实现 │
│   = IIntelligence-  │   │ · 控制平面 UI（已建 V1 骨架）      │
│   Provider 注入点   │   │ · 命令编排 / 会话 / 权限 / 模型路由 │
│ · 黑盒执行          │   │ · 观测消费者（读 IObservationSource）│
└────────────────────┘   └──────────────────────────────────┘
```

**性质**：
- 抽象归属 UniClaw；DSH 是宿主实现之一，未来可替换
- Kernel 依赖抽象接口（`IIntelligenceProvider`），不依赖 DSH
- DSH 只做宿主 + UI + 编排，不参与 Kernel 内部调度决策

---

## 2. 与 Target Architecture 的对齐：DSH 接入点 = Phase 5/6 接缝

### 2.1 既有文档已预见的接缝

`semantic-agent-runtime-target-architecture-review.md` §3.6（Adjudication Path）已预留：

```
Agent Semantic Decision:
  3. May invoke slow intelligence (VLM, future)   ← 这就是 IntelligenceSeam 的锚点
```

§6.1 Phase 6（Intent Compilation）：

```
BusinessIntent → Capability selection (Agent)
Runtime receives "开启 Wi‑Fi" 并自主推导执行策略
Plan = Capability sequence, not pre-compiled action tokens
```

### 2.2 结论：DSH 任务入口对接 Phase 6 目标形态

DSH 的 `run.start(TaskSpec)` 传**意图级**规格（goal + acceptance + device + safety），
不是预编译步骤。Kernel 侧在 Phase 6 落地后自主编译 Capability 序列。

```
DSH TaskSpec ──→ run.start ──→ [Phase 6 入口] Agent Intent Compilation
                                    ↓ (目标态)
                              Agent 自主选 Capability → SemanticAction
                                    ↓
                              Traversal 执行（黑盒，I-3 不变）
```

**风险控制**：不绕过 Phase 6 直接传 Plan（那会固化 target review 明确要 DEPRECATE 的
caller 预编译模式）。过渡期若需传 Plan，显式标记为 compatibility mode，Phase 6 落地后移除。

---

## 3. IntelligenceSeam：只在裁决点的慢智能接缝

### 3.1 形态

```csharp
// Kernel 侧抽象（UniClaw 问题域）
public interface IIntelligenceProvider
{
    Task<IntelligenceAdvice> ConsultAsync(AdjudicationContext context, CancellationToken ct);
}

// 调用点：Agent 语义裁决时（belief UNRESOLVED / CONTRADICTED）
// 对齐 target review §3.6 "May invoke slow intelligence (VLM, future)"
```

- **接缝面**：只在 Agent 语义裁决点（精确范围，用户已确认）
- **消费边界**：建议制——`IntelligenceAdvice` 是候选建议，最终动作由 Kernel 确定性裁决（I-3 不破）
- **注入**：构造注入（如现有 Startup/Recovery 模式），组合根挂 DSH 适配器
- **Guard 安全**：Kernel 只依赖抽象接口，零 LLM/VLM 引用（Guard 2 不触发）

### 3.2 调用×消费矩阵（场景正交，不绑死）

| 场景 | 调用方式 | 消费边界 |
|---|---|---|
| 感知分类（VLM 识别控件/页面/状态） | 同步 | 分域授权（低风险可验证） |
| 场景规划（任务发起前目标→步骤拆解） | 同步/预计算 | 建议制 + 人工确认 |
| 异常诊断（运行中模糊情况） | 异步 | 建议制（Kernel 裁决） |
| 事后分析（Shadow/报告） | 异步 | 零消费（现状，Kernel 不读） |

**统一原则**：
- 调用方式 ← 由「Kernel 下一步是否需要立即知道」决定（需要→同步，不需要→异步）
- 消费边界 ← 由「输出进入状态变更路径的深度」决定（感知→分域授权，决策→建议制，事后→零消费）

---

## 4. DSH 对 Agent 的定义边界

### 4.1 两层定义，都是契约，不是实现

**① 任务契约（每任务一份）——定义"做什么"**
```
TaskSpec {
  scenarioId, goal（语义目标）, device,
  acceptance（验收条件/GoalEvidence 期望）, safety（安全约束）
}
→ run.start(TaskSpec) → Kernel 黑盒执行
```

**② 能力配置（每部署一份）——定义"能用什么外置能力"**
```
AgentProfile {
  intelligence: {
    perceptionDomains: [...]   ← 分域授权：可直采 DSH 输出的感知域
    consultPoints: [...]       ← 建议制：Kernel 请求 DSH 但自己裁决的点
    brain: { sync: [...], async: [...] }
  },
  memory: { enabled, scope }
}
→ Kernel 在接缝处按此配置消费 DSH 能力
```

### 4.2 与 Phase 2 的对齐：AgentProfile 收敛为 Capability/Object 目录提供方

Target review Phase 2：SemanticObject + Capability 是**不可变声明知识**（caller/configuration
提供 catalog）。DSH 的"定义"应收敛为：

```
DSH 可定义（与 Phase 2 对齐）:
  · Capability 目录（ToggleWiFi = name + target + effect + satisfaction criteria）
  · SemanticObject 目录（Wi‑Fi Switch = identity + category + capabilities）
  · TaskSpec（goal + acceptance + device + safety）

DSH 不可定义（Kernel 权威）:
  · Agent 的 Capability 选择决策（I-3）
  · SemanticAction 授权
  · binding/belief 状态（I-2）
  · GoalEvidence 写入（OBS-F9 冻结）
```

### 4.3 权威源

- **AgentProfile / Capability 目录**：Kernel 侧权威（Agent 构造注入），DriverHost 只投影
- **TaskSpec**：DSH 发起，Kernel 侧校验（格式/范围/授权域）
- **DSH 能力**：`IIntelligenceProvider` 抽象注入，DSH 是宿主实现

---

## 5. 控制平面数据模型（五层）

| 层 | 数据 | 对应既有概念 | 现状 |
|---|---|---|---|
| ① 任务列表 | runId/场景/设备/状态/时间 | run.list + snapshot | ✅ fixture 有 |
| ② 实时事件流 | RuntimeEvent 时间线 | RuntimeEventProjector | ⚠️ 命令层未暴露 `run.events.after` |
| ③ 智能层记录 | ShadowAnalysis + **Adjudication 轨迹** | Shadow + Agent 裁决记录 | ⚠️ Shadow 有，裁决轨迹未投影 |
| ④ 证据层 | Trap / EvidenceRef / SemanticEvidence 链 | run.trap.get / evidence.get | ✅ 元数据有 |
| ⑤ 场景/设备库 | Capability/Object 目录 + 设备投影 | Phase 2 目录 | ❌ 未投影 |

**新增洞察**：控制平面应展示 **Adjudication 轨迹**（Agent 何时调了慢智能、建议是什么、
裁决结果）——这是外层智能体监控最重要的数据，DSH 需要知道"Agent 为什么咨询、采纳了什么"。
既有文档未明确投影此项，是控制平面数据模型的新增项。

### 5.1 数据优先顺序

1. 打通 ② 实时事件流（`run.events.after` 命令层暴露）——实时监控是控制平面的灵魂
2. 补 ⑤ 场景/设备库——"启动任务"要有可点的东西
3. 上 ③ Adjudication 轨迹——等 IntelligenceSeam 落地

---

## 6. 需要的新协议方法族（wire 层，后续 OpenSpec change 细化）

| 方法 | 用途 | 方向 |
|---|---|---|
| `run.start {TaskSpec}` | 任务发起（Phase 6 入口） | DSH → Kernel |
| `profile.get` | 读取 AgentProfile/Capability 目录 | DSH → Kernel（只读投影） |
| `perception.ask` | 同步 VLM 感知（分域授权） | Kernel → DSH（经接缝） |
| `intelligence.consult` | 同步/异步智能咨询（建议制） | Kernel → DSH（经接缝） |
| `escalation.notify` / `escalation.resolve` | 异常升级与结果回传 | 双向 |
| `run.events.after` | 实时事件流（命令层暴露，wire 已有） | DSH → Kernel（只读） |

---

## 7. 未决问题（OpenSpec propose 时细化）

1. TaskSpec 的 schema（字段/校验规则/与 Goal 的关系）
2. AgentProfile 的存储形态（Kernel 侧配置文件 vs 构造参数 vs 注册表）
3. IntelligenceAdvice 的 schema（建议类型/置信度/引用证据）
4. Adjudication 轨迹的投影契约（事件种类/载荷）
5. 分域授权的声明表格式与 Kernel 侧校验
6. 过渡期 compatibility Plan 模式的边界

---

## 8. 变更范围声明

本设计文档是**架构讨论的固化**，不包含任何代码变更。后续实施必须：
- 走 OpenSpec 流程（`dsh-outer-intelligence` / `kernel-intelligence-seam` 两个 change）
- 不触碰已冻结的 8 个只读 wire 方法语义
- 不违反 14 Invariants / OBS-F9 / I-2 / I-3
- Kernel 生产代码变更（IntelligenceSeam）需独立 OpenSpec change 并通过 Architecture Guard
