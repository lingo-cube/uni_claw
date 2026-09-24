# RUN-005 — L2 Policy Protocol & Runtime

> 设计稿：`plans/2026-09-24-run-005-l2-policy-runtime.md`
>（**v0.3.1 FROZEN**：owner 终裁 PASS_WITH_ONE_NARROW_AMENDMENT——semantic
> lease 规则冻结（§4）+ Owner 决策 1（PolicyInvalidated 统一承载，§7）+
> Owner 决策 2（独立最小 PolicyEvaluationView，§4.1）+ GuardCursor warm-up
> 澄清（§8）；Further Grill NOT REQUIRED；v0.2 其余裁决全部维持。
> **v0.3.1 窄幅 amendment（2026-09-24 owner 授权，不重开设计/不再 Grill）**：
> 删除 `ElementExists`→DEFER（buyer = coverage-aware traversal /
> termination；P12 随之 DEFER，v1 矩阵 P1-P11 聚焦 A2/A4/C1）+ Slice B
> 两条实现约束（lease exact-equality / PolicyId validated-vs-adopted））。
> 状态：`lifecycle_state: implementing · design_state: frozen`（plan =
> Slice A→B→C）。

## Dual-Grill 处置记录（2026-09-24）

- **Owner 预审**：PASS_WITH_FINDINGS（4 SUBSTANTIVE + 4 MEDIUM + lease 点）。
- **独立盲审**（fresh subagent，零泄漏委托）：REOPEN（1 BLOCKER +
  4 SUBSTANTIVE + 3 MEDIUM + 2 MINOR）；核心方向确认存活。
- **交叉对表**：预算域/认识论三态/FailClosed/Scope-lease 四根问题双命中
  （高置信）；target 表达与 ClaimInSet 过冲为 owner 独有命中；逐元素记忆/
  E4 楔死/ScriptedUniAgent 重做/预算门为盲审独有命中。
- **v0.2 全部吸收**（设计稿 §16 修订日志逐项）。

## Intent（WHAT/WHY）

在 RUN-004 冻结的多轮决策 Runtime 上新增 **L2 Policy 能力**：UniAgent 提交
bounded typed Policy，Kernel 在**不获得规划权**的前提下根据 fresh world
逐轮展开。一句话差异：L1 未来 steps 已知（Agent 预先展开），L2 下一
application 依赖后续 fresh observation（Agent 给规则，Kernel 机械展开）。

**不变量**：UniAgent creates · Kernel executes · never invents/repairs/
extends（AGT-001 FROZEN §5）；Policy ≠ workflow/program/script/effect
batch/driver macro（baseline §24.2）；**一切 Policy 内结局 fail closed 回
Agent decision boundary**（基线原文；v0.2 删除 FailClosed 终局分支）。

## Scope（Step 1：只设计）

最小 records（v0.3.1：两谓词 ClaimEquals/ClaimInSet——ElementExists 删除→
DEFER + 单守卫 ObservationUnchanged + **自带语义目标的模板**（AgentActionStep
同形）+ 单界 MaxApplications）· PolicyTruth 三态逐 primitive 推导表 ·
PolicyExpand 良基单轮循环（复用 StepAct→StepVerify 全链；E4 映射）·
PolicyInvalidated（唯一新相位，八 typed reason；invalidation 带预算门）·
两层预算（V6c 对 StepsRemaining；展开轮零咨询）· ephemeral PolicyState（含
GuardCursor、_pendingPolicyOutcome 与 adopted lease ref）· P1-P11（v0.3.1：
P12 随 ElementExists DEFER；触发修正：conflicted-claim 可产化）· authority
delta（无规划权；模板自带目标消灭「猜 target」路径）。

DEFER 登记：B2/B6/A1/B4 消费者（逐元素记忆/coverage/内容稳定 scoping/
多模板）；semantic lease 深语义专项；**coverage-aware 元素存在/终止
（v0.3.1：原 ElementExists——需 coverage/completeness semantics）**。

## Out of Scope

实现代码 · DSH/DeepSeek · AGT-001 修改 · consultation 重设计 · 第二 Kernel
loop · workflow engine · general DSL · Memory · Recovery/Agent Continuation ·
cross-process resume · DSH UI · prompt · schema generation（AGT-002）。

## 上游对齐

RUN-004 · AGT-001 FROZEN · baseline §24.2/§24.3/不变量 43/45 ·
consultation-protocol-v0.1 · granularity 18 场景（§5-2 执行态条款）·
代码基线（KernelRunDriver L240-683 / ExecutionContract / ConsultationTypes）。

## Acceptance（本 Step）

1. v0.2 覆盖双审全部 findings（§16 逐项）；
2. 每个 v1 primitive 有可产消费者；DEFER 项逐一登记 buyer；
3. 展开链逐项 fresh；无新执行器；无 Kernel 规划权增量；
4. state = awaiting-owner-readjudication（不 CLOSED）。

