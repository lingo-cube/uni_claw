# Source Coverage Map

本文件用于证明文档拆分没有遗漏原始《UniClaw Agent Runtime — Greenfield 构建主提示词》的主要内容。

| 原始章节 | 对应文档 |
|---|---|
| 开场假设 / 技术栈 | `constitution/agent-runtime-charter.md` |
| 1 系统目标 | `constitution/agent-runtime-charter.md` |
| 2 核心控制闭环 | `constitution/agent-runtime-charter.md` |
| 3 External World 不可信 | `constitution/agent-runtime-charter.md`, `constitution/runtime-architecture-contract.md` |
| 4 核心架构 | `constitution/agent-runtime-charter.md` |
| 5 Agent | `layers/agent-runtime.md` |
| 6 Container | `layers/container-runtime.md` |
| 7 Traversal | `layers/traversal-runtime.md` |
| 8 Environment | `layers/environment-runtime.md` |
| 9 Observation | `patterns/state-and-belief-model.md` |
| 10 World Belief | `patterns/state-and-belief-model.md` |
| 11 Runtime State | `patterns/state-and-belief-model.md` |
| 12 Memory | `patterns/state-and-belief-model.md` |
| 13 Plan | `patterns/state-and-belief-model.md` |
| 14 Graph | `patterns/state-and-belief-model.md`, `layers/container-runtime.md` |
| 15 Dynamic Grounding | `layers/container-runtime.md` |
| 16 Semantic Identity | `layers/container-runtime.md`, `constitution/runtime-architecture-contract.md` |
| 17 FSM | `patterns/fsm-protocol-pattern.md` |
| 18 Global Lifecycle | `layers/agent-runtime.md`, `patterns/fsm-protocol-pattern.md` |
| 19 Startup | `layers/agent-runtime.md` |
| 20 Recovery Anchor | `layers/agent-runtime.md` |
| 21 Trap | `patterns/trap-recovery-pattern.md` |
| 22 Trap Scope | `patterns/trap-recovery-pattern.md` |
| 23 Recovery | `patterns/trap-recovery-pattern.md` |
| 24 Recovery Mechanism | `patterns/trap-recovery-pattern.md` |
| 25 Action Safety | `patterns/action-safety-and-concurrency.md` |
| 26 AI / LLM / VLM | `patterns/ai-semantic-capability.md` |
| 27 AI 异步能力 | `patterns/ai-semantic-capability.md` |
| 28 Observability | `patterns/observability-and-results.md` |
| 29 Mutable State Owner | `constitution/runtime-architecture-contract.md` |
| 30 Decision Authority | `constitution/runtime-architecture-contract.md` |
| 31 Dependency Direction | `constitution/runtime-architecture-contract.md` |
| 32 Interfaces Before Implementations | `layers/environment-runtime.md`, `engineering/greenfield-development-guide.md` |
| 33 Simulation First | `layers/environment-runtime.md` |
| 34 Normal Scenario | `scenarios/01-normal-wifi.md` |
| 35 Recovery Scenario | `scenarios/02-agent-recovery.md` |
| 36 Scroll Scenario | `scenarios/03-scroll-identity.md` |
| 37 Uncertain Action | `scenarios/04-uncertain-action.md` |
| 38 Popup Scenario | `scenarios/05-popup-local-recovery.md` |
| 39 建设顺序 | `engineering/greenfield-development-guide.md` |
| 40 项目结构 | `engineering/greenfield-development-guide.md` |
| 41 Architecture Tests | `engineering/greenfield-development-guide.md` |
| 42 Scenario Tests | `engineering/greenfield-development-guide.md` |
| 43 Completion Evidence | `patterns/observability-and-results.md`, `layers/agent-runtime.md` |
| 44 Error / Trap / Failure | `patterns/trap-recovery-pattern.md` |
| 45 Result 类型 | `patterns/observability-and-results.md` |
| 46 Cancellation / Pause / Shutdown | `layers/agent-runtime.md`, `patterns/action-safety-and-concurrency.md` |
| 47 Concurrency | `layers/agent-runtime.md`, `patterns/action-safety-and-concurrency.md` |
| 48 核心类约束 | `engineering/greenfield-development-guide.md` |
| 49 接口价值 | `engineering/greenfield-development-guide.md` |
| 50 架构味道 | `engineering/greenfield-development-guide.md` |
| 51 编码原则 | `engineering/greenfield-development-guide.md` |
| 52 文档原则 | `engineering/greenfield-development-guide.md` |
| 53 ADR | `engineering/greenfield-development-guide.md` |
| 54 AI Coding 工作方式 | `engineering/greenfield-development-guide.md` |
| 55 Greenfield 优势 | `constitution/agent-runtime-charter.md` |
| 56 Architecture Spine | `constitution/agent-runtime-charter.md` |
| 57 第一阶段完成标准 | `engineering/greenfield-development-guide.md` |
| 58 设计自由度 | `engineering/greenfield-development-guide.md` |
| 59 最终架构原则 | `constitution/agent-runtime-charter.md`, `constitution/runtime-architecture-contract.md` |
| 60 第一项工作 | `engineering/greenfield-development-guide.md` |
