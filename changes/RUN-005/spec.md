# RUN-005 — L2 Policy Protocol & Runtime

> Step 1 设计稿：`plans/2026-09-24-run-005-l2-policy-runtime.md`（待 Review/Grill）。

## Intent（WHAT/WHY）

在 RUN-004 冻结的多轮决策 Runtime 上新增 **L2 Policy 能力**：UniAgent 提交
bounded typed Policy（有限、类型化、条件式策略），Kernel 在**不获得规划权**
的前提下根据 fresh world 逐轮展开。一句话差异：L1 未来 steps 已知（Agent
预先展开），L2 下一 application 依赖后续 fresh observation（Agent 给规则，
Kernel 机械展开）。

**不变量**：UniAgent creates Policy · Kernel executes Policy · Kernel never
invents/repairs/extends（AGT-001 FROZEN §5 继承）；Policy = bounded
contingent advisory decision package ≠ workflow/program/script/effect batch/
driver macro（baseline §24.2 继承）。

## Scope（Step 1：只设计）

- 最小 .NET authoritative records（PolicyProposal/Scope/Predicate/Guard/
  Template/Bounds/Fallback + AgentDecision.Policy 第四员）
- Closed vocabulary：4 谓词 + 1 守卫 + 1 模板，逐项 18 场景反推（A2/A4/B2/
  B6/C1 为消费者；A1/B4 的 visited/属性过滤 buyer 未到 → 登记 DEFER）
- PolicyState：driver-private ephemeral（依据 baseline「Run Model 不存
  内容」+ AGT-001 GQ2 DEFER——不新建 owner、不持久、restart 丢弃重咨询）
- 单轮展开算法：PolicyExpand phase → fresh observe → scope/termination/
  guard/bounds/match → derive ONE step → **复用既有 StepAct→StepVerify 全链**
  （零新执行器）→ counters → repeat
- 失败/重咨询映射：PolicyInvalidated（唯一新相位，六 typed reason）+
  复用 StepRejected/VerificationFailed/StepVerified/E1
- 两层预算（Contract > Policy local；V6c 机械执法不得扩大合同）
- Termination ≠ Goal Completion（P12 执法）
- V6 校验规则集（8 条，全 fail-closed 不修复）
- P1-P12 deterministic 验收矩阵（ScriptedUniAgent，禁 DSH）
- Authority Matrix Delta（谓词求值 = 封闭 AST 机械消费，非规划）

## Out of Scope

实现代码 · DSH/DeepSeek 接入 · AGT-001 修改 · consultation 协议重设计 ·
第二 Kernel loop · 通用 workflow engine · general-purpose DSL · Memory ·
Recovery/Agent Continuation · cross-process Policy resume · DSH UI · prompt ·
schema generation/DSH bridge（AGT-002 消费本 change 产出的 authoritative
records）。

## 上游对齐

RUN-004 spec/state（冻结协议 + E1/E4/D5/预算链）· AGT-001 FROZEN 设计 §5
（closed typed；五要素）· baseline §24.2/§24.3/不变量 43/45 ·
consultation-protocol-v0.1（T5 预留/V6 预留/Progress.PolicyState? 预留）·
granularity 18 场景 · 代码基线（KernelRunDriver L240-534 / ExecutionContract
/ ConsultationTypes / AgentDecision）。

## Acceptance（本 Step）

1. 设计覆盖 Q1-Q17 全部议题（见设计稿目录映射）；
2. 每个 primitive 有场景消费者；无消费者的登记 DEFER；
3. 展开链逐项证明 fresh（Q7 清单）；无新执行器（Q11）；
4. Authority delta 无 Kernel 规划权增量；
5. state = ready-for-grill（不 CLOSED）。
