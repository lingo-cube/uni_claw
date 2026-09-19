# ADR-0022：Codex 与 DSH 是两个完整 UniAgent Realization，并承担不同角色

## Context

UniAgent 需要借助现成 Harness Runtime 落地，但“把 Host 当模型/推理 Adapter”与
“把 Host 作为完整 UniAgent realization”会产生完全不同的 lifecycle、state、
failure 与 conformance 边界。UAR-001 已证明 Codex 与 DeepSeek Harness（DSH）
都能承载完整 realization；两者的可定制性与产品定位不同。Human 于 2026-09-12
授权把稳定取舍升格为产品架构决策。

## Decision

1. **Codex-backed UniAgent 与 DSH-backed UniAgent 都是完整的 UniAgent
   Realization**：各自在具体 Host Runtime 上承担 UniAgent 的完整产品职责与
   lifecycle，并接受同一 Host-neutral Conformance Surface 约束。
2. **Codex-backed UniAgent = Simulation Realization**：用于产品语义模拟、
   场景验证、差分 conformance 与回归；“模拟”不降低核心产品语义要求，也不产生
   production-ready 声明。
3. **DSH-backed UniAgent = Product Realization**：作为目标产品实现方向，通过
   后续定制化与 hardening 逐步达到产品级；该角色选择不等于当前已具备生产就绪
   证据。
4. Codex/DSH 的 session、turn、step、tool、event、transcript、模型与 transport
   只属于 realization 内部实现，不得取得 Product Session、Primary Goal、
   Execution Contract、Primary Run、Goal Evaluation 或任何 Kernel/L2 Authority。
5. Product Runtime 与 Development Harness 即使复用同一 Host，也必须隔离 state、
   instruction/tool set、lifecycle 与 completion semantics。

## Considered Options

- **把 Codex/DSH 当作 UniAgent 内部 reasoning/model Adapter**：拒绝。它把完整
  UniAgent lifecycle 压缩成 provider 调用，无法容纳 Product Session、Goal、
  Contract、Outcome 与 Goal Evaluation 的产品语义。
- **只建设 DSH Product Realization**：拒绝。缺少第二个真实 realization 会让
  Host-neutral seam 退化为单实现假设，并失去差分 conformance 的真实 buyer。
- **Codex 与 DSH 各自定义一套产品契约**：拒绝。它会复制 Product Authority，
  让 Host vocabulary 泄漏到共享架构。
- **两个完整 realization、角色分化、共享一个 Conformance Surface**：接受。
  两个真实 realization 证明 seam 存在，同时允许模拟与产品化采用不同内部实现。

## Consequences

- 稳定契约由 `docs/architecture/uniagent-realization-baseline-v0.1.md` 冻结；
  UAR-001 长文只保留为非权威设计历史。
- ADR-0019 继续独立拥有 post-activation Kernel self-drive 决策；本 ADR 不定义
  legal Primary Run activation 或 lifecycle command 协议。
- 未来 R1、Tracer Bullet 或实现 change 必须同时满足 Target Product Architecture、
  Inter-Component Protocol 与 UniAgent Realization Baseline；Host 能力不能改写
  Owner/Authority。
- 本 ADR 不授权 R1、实现、具体 SDK/Profile/plugin、transport 或部署方案。
