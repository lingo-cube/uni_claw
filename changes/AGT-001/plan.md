# AGT-001 — Plan

> 前置：Step 1 设计稿完成（plans/2026-09-24-agt-001-uniagent-runtime-architecture.md）。
> 本 plan 只到「Grill → 修订定稿」；实现拆分在 Grill 后另立 plan。

## 步骤（垂直切片，不按层拆）

1. **Grill（独立会话）**：以设计稿为靶，攻击面 = 设计稿 §11 十问 +
   §12 Open Questions + Failure Model 完备性 + Policy schema 与 baseline
   §24.2 语义一致性。产出：finding 清单（F-major / M-medium / S-self-refuted）。
2. **修订**：按 grill findings 修订设计稿（revision 留痕）；上游冻结面
   冲突项 → 上抛 owner 裁决（不就地改 baseline）。
3. **定稿裁决**：verdict = DESIGN_FROZEN（进入实现拆分）或需二次 grill。
4. **实现拆分（Grill 后，另行立项或本 change 续档）**：
   - 切片 A：`UniClaw.Agent.Dsh` adapter + fake sidecar 协议测试
     （CONTRACT 级：信封/握手/错误码——先于真 sidecar）；
   - 切片 B：DSH 侧 uniclaw-agent profile（单工具 + concludesTurn）+
     mock provider 冒烟；
   - 切片 C：Product Host 配置门控接入 + failure matrix 测试；
   - 切片 D：conformance C1-C11 场景落地（fake/mock provider）；
   - RUN-005（Kernel 侧 Policy 展开）与本 change 的 AgentDecision+Policy
     成员联动由 RUN-005 spec 评审裁定时序。

## 依赖与时序约束

- RUN-004 协议冻结（✓ 前提已满足）；RUN-005 未启动——Policy schema 在
  本稿只定义 Agent 侧产出契约，Kernel 展开语义不得提前冻结。
- SIM-003 已闭（场景库 execution 绑定体系可为 realization 标注扩展复用）。
- Open Questions Q-ctx（context 尺寸预算）须在切片 A 前裁决；
  Q-transport 信封设计在切片 A 内完成。

## 验证策略（Grill 阶段）

- 每条裁决可指回 FROZEN 上游条文或 DSH 机制证据（file:line）；
- 十问预答无「靠提示词/靠自觉」类软约束；
- Failure Matrix 覆盖指令 12 项且 owner 列无「DSH 自愈」；
- 修订后设计稿与 changes/AGT-001/spec.md 裁决表一致。
