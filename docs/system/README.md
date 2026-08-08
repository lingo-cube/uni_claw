# UniClaw Agent Runtime — 文档索引

本目录将 Agent Runtime Greenfield 的架构知识拆分为可长期维护、可按需加载的文档。

目标不是减少原始内容，而是让不同职责拥有各自的权威文档，便于人类与 Coding Agent 按任务范围加载最小必要上下文。

## 推荐阅读顺序

### 所有 Runtime 任务必读

1. `constitution/runtime-architecture-contract.md` — **14 条不可违反边界契约**（硬约束，Guard Test 验证）
2. `greenfield-runtime-charter.md` — 完整行为指导（规范参考，13 Part）

### 按任务类型继续读取

| 任务 | 必读文档 |
|---|---|
| 理解系统使命与核心原则 | `constitution/agent-runtime-charter.md` |
| Agent / Run lifecycle / Startup | `layers/agent-runtime.md` |
| Container / Semantic Identity / Grounding | `layers/container-runtime.md` |
| Traversal / 单步执行 | `layers/traversal-runtime.md` |
| Vision / Device / 外部能力 | `layers/environment-runtime.md` |
| Observation / World Belief / Runtime State / Memory / Plan | `patterns/state-and-belief-model.md` |
| FSM | `patterns/fsm-protocol-pattern.md` |
| Trap / Recovery | `patterns/trap-recovery-pattern.md` |
| Action timeout / 幂等 / Pause / 并发 | `patterns/action-safety-and-concurrency.md` |
| LLM / VLM / 异步语义分析 | `patterns/ai-semantic-capability.md` |
| Trace / Result 类型 / Completion Evidence | `patterns/observability-and-results.md` |
| Greenfield 建设 / 测试 / 目录 / Coding Agent 工作方式 | `engineering/greenfield-development-guide.md` |
| 场景实现 | `scenarios/*.md` |

## 文档权威关系

- `constitution/runtime-architecture-contract.md`：**硬约束**，跨阶段稳定，机械 Guard 验证。最高优先级。
- `constitution/agent-runtime-charter.md`：系统使命、核心闭环、Architecture Spine 与总体原则。
- `greenfield-runtime-charter.md`：完整行为指导的规范参考。拆分文档均从此衍生。
- `layers/*`：描述各运行层的职责和依赖边界。
- `patterns/*`：描述跨层复用的设计规则。
- `scenarios/*`：用可执行场景锁定架构行为。
- `engineering/*`：定义 Greenfield 建设顺序、测试与 AI Coding 行为。

## 原始章节覆盖

本文档集完整覆盖原始 Greenfield 主提示词第 1–60 节。详见：

`engineering/source-coverage-map.md`
