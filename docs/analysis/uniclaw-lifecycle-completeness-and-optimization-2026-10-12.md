# UniClaw 组件、协议与生命周期完备性审查（2026-10-13）

> Status: COMPLETE / SUPERSEDED-HISTORICAL
> Authority: NONE

本报告保留2026-10-13的历史推理快照。字段层结论由[2026-10-14最小buyer复核](uniclaw-completeness-check-2026-10-14.md)修正，尤其身份/P24/P25不再按旧文认定为当前阻塞；数学初始前提见[历史公理推导](uniclaw-mathematical-axioms-philosophical-derivation-2026-10-14.md)。2026-10-22仅核对审查交付与替代关系，不重新认证当前产品生命周期。工程化主题独立见[组件分级收口](uniclaw-component-engineering-closeout-2026-10-22.md)。

## 这份审查要回答什么

题目不是“工程管理做得好不好”，而是四个语义问题：

1. 当前组件和协议是否足以表达产品要做的事情；
2. 生命周期中的正常、拒绝、等待、恢复、终止、迟到和重复分支是否都有合法走法；
3. 用既有场景库反推时，公理是否真的能落到具体世界事件；
4. 用数学和哲学检查后，哪些结论是成立的，哪些只是条件成立，哪些仍是缺口。

工程文件、测试命令和生成器只作为证据，不是本审查的主题。

## 一、组件和协议是否表达了同一个产品

当前产品拓扑是：

```text
Product T0：UniClaw、User、Host、Device/Environment
Product T1：UniAgent、Uni Kernel、Capability Plane、Memory System
Product T2：Evidence Ledger、World Model、Run Model、Control Loop、Assurance、Effect Boundary
Product T3：责任域内部模型、协议实现和 Provider/Host realization
```

这套分级与语义生命周期是两条不同的轴：

```text
Product Session → Primary Goal → Execution Contract → Primary Run
→ Runtime Outcome → Goal Evaluation
```

证据和控制又形成第三条流：

```text
Observation → Evidence Record → WorldBelief → Control Intent
→ Binding → Effect → Verification
```

因此，当前模型没有把“组件”“事实”“判断”“结果”混为一类：

- UniAgent 拥有 Goal、Contract authoring 和 Goal Evaluation；
- Run Model 拥有 canonical Run State；
- Evidence Ledger 接纳 Evidence；
- World Model 维护 WorldBelief；
- Control Loop 产生 Intent；
- Assurance 产生 action judgment 和 Outcome Proof；
- Effect Boundary 拥有 Binding 与现实 Effect delivery；
- Uni Kernel 只负责组合、生命周期协调和 Runtime Outcome emission。

P1–P26 表达主运行链，外围 O 边表达 Task、Launch、Host、Workspace、Capability、Profile 和 Harness 的接入。就语义分工而言，组件与协议是相互匹配的；没有发现必须把 Uni Kernel 改成全局状态 Owner 才能解释的核心需求。

## 二、生命周期的每个分支是否有合法走法

把一次执行写成状态转换：

```text
Session → Goal → Contract(admitted) → legal activation
→ observe / decide / act / verify 循环
→ waiting 或 recovering（必要时）
→ Completion / Failure / SafeStop / Escalation
→ Runtime Outcome → Goal Evaluation
```

逐类判断如下：

| 分支 | 当前结论 | 说明 |
|---|---|---|
| 正常完成 | 成立 | 观察、接纳、形成 belief、绑定、授权、动作、动作后验证、Completion 可连续走通 |
| 目标已满足 | 成立 | NoAction 是合法分支，零 Effect 仍可完成 |
| 感知缺失、错误、冲突、迟到 | 成立 | 不把 confidence 或 provider 宣称升级为事实；走 Insufficient、Ambiguous、Conflict 或新 revision |
| 绑定无候选、多候选、目标失效 | 成立 | Candidate 不等于 Canonical；不能 forced pick；必须重新观察或安全停止 |
| 未授权、越权、终态后动作 | 成立 | Gate 拒绝，拒绝原因可观察，Effect delivery 关闭后不能复活 |
| UnknownOutcome | 成立 | 唯一合法后继是 re-observe，禁止 blind redispatch |
| 等待外部输入 | 条件成立 | 等待本身合法，但宿主失联或永不再次进入时缺少 watchdog/超时边 |
| 预算耗尽 | 条件成立 | T6 要求有限测度；当前部分测度仍依赖契约/策略默认值存在 |
| 重启与恢复 | 部分成立 | Journal 的 commit boundary、torn frame 和未决 attempt 语义成立；跨 Run 的恢复编排仍登记为缺口 |
| 用户中途取消、澄清、交互升级 | 未闭合 | 取消有 driver 形状，但 User→Runtime 的中途协议边没有完整登记；澄清和改 Contract 不能在当前图中自然到达 |
| admitted 但未 activation | 未闭合 | Contract View 存在悬挂点，缺少失效、过期或 GC 语义 |
| Observation acquisition | 未闭合 | P2 锁定观察回流，却没有正式登记 Control→Capability 的观察指令边 |
| Memory | 有意占位 | P20/P21 只允许 prior/context/history，不能成为 current truth；真实 buyer 前不应预造实现 |

所以，生命周期不是“完全缺失”，而是：**安全闭环和终局闭环成立；活性、用户中途控制、激活前悬挂、观察致动和跨 Run 恢复仍未完整闭合。**

## 三、用 150 场景反推

既有场景推理文档使用 7 个压力轴：环境行为、感知可靠性、绑定歧义、权限压力、生命周期位置、时间、历史。150 个场景被压缩为 13 个等价类 C1–C13；这比逐条写故事更能检验转移结构。

反推结果：

- 47/47 条编号不变量都被至少一个场景压中；
- T1–T6 每条都被多个等价类实例化；
- 150 个场景的分布为 `✅122 · ◐14 · D13 · NB1`；
- C1 正常流、C2 感知异常、C3 绑定歧义、C4 权限、C5 时间异常、C6 终局证明、C7 身份连续性、C8 证据演化、C10 激活基数均能走到对应的合法分支；
- C9 遍历、C13 交叉组合和 C5 跨 Run 迟到反馈暴露了预算、恢复编排和用户/宿主交互边界；
- C11 Memory 的“未实现”是明确的 D，不是偷偷当作通过；C7 的第二运行时域是 NO_REAL_BUYER，不应凭想象扩展。

几个最能反推设计正确性的场景是：

1. **SR-064：receipt 成功但世界未变**。证明 Receipt 不能直接推出 Verified Effect。
2. **SR-068：证据不足**。证明“不知道”不是 Failure，也不是 Completion。
3. **SR-069：并发终局提案**。证明 terminal 必须 single-winner、exact-prior。
4. **SR-087：signature 变化即新身份**。暴露身份归一化仍需 buyer 约束。
5. **SR-099/102/103：大世界、小预算、自适应遍历**。暴露 T6 的边界，而不是证明核心安全公理错误。
6. **SR-108：断点续跑**、**SR-144：Provider 崩溃后恢复**。证明恢复编排尚未由协议图完全表达。
7. **SR-150：一切正常但 Goal 语义不可验证**。证明最终交付应允许 Undetermined/NeedsFollowUp，不能为了“结束”伪造成功。

仓库中的可执行场景库是这些推理场景的一部分验证载体；它能证明已执行的具体路径，不能替代 150 场景的等价类覆盖，也不能把未实现的 D/◐ 自动变成 PASS。

## 四、数学完备性检查

以状态机 `M = (S, E, δ, s0, T)` 表示系统，检查七个性质：

### 1. Safety：坏事不可发生

成立。权限、绑定新鲜度、串行验证屏障、terminal delivery closure、UnknownOutcome 禁止盲重发等规则形成了拒绝分支。当前没有发现由场景反推出的 Safety 违约。

### 2. Consistency：重复请求不制造第二个事实

成立。P1、P24、P25、P16/P17、Binding consumption 和 Runtime Outcome emission 都有幂等或 single-winner 语义。

### 3. Authority closure：每类事实只有一个写入者

成立。Observation、Evidence、WorldBelief、Intent、Judgment、Effect 和 Outcome 没有被同一组件越权合并。Capability、Trace、Workspace、Harness 都不能反向取得 Product authority。

### 4. Liveness：非终态最终能继续或诚实结束

条件成立。只要预算/终止测度存在且宿主持续再入，循环可以继续、耗尽或进入合法终局；但 P1 不强制终止测度、WaitingForInput 缺 watchdog，故不能证明无条件活性。

### 5. Reachability：需求中的状态确实可达

Completion、Failure、SafeStop、Escalation 均可达；但用户中途取消/澄清、admitted-never-activated 的处理和 Observation Request 仍有图上的缺号或悬挂。

### 6. Epistemic soundness：系统不把自己的假设当成现实

基本成立，但有一处循环论证风险：P22 的 transition prior 可能影响 association，再参与同一动作的 MaterialEffect 解释。当前 Conflict、Ambiguous、Insufficient 和 trust grade 规则能降低风险，但 MaterialEffect 的 prior-independent 佐证政策还需要明确。

### 7. Terminal completeness：所有终止都能被准确分类

成立。Completion、Failure、SafeStop、Escalation 被严格区分；证据不足保持非终态或 Undetermined，不被强行塞进四类终局。

## 五、哲学推导

- **认识论**：Evidence 只表示“系统依据什么”，不表示“现实必然如此”；这是把怀疑制度化，而不是把感知器当作真理机器。
- **证明论**：Outcome Proof 不能从自身宣称推出；因此“证据不足”必须能保持不确定，这避免了循环自证。
- **政治哲学**：Control 提案、Assurance 判断、Effect 执法三权分离，避免组件既制定规则又执行自己的判决。
- **时间哲学**：append-only 和 revision 保留“过去发生过什么”，迟到更正只能产生新解释，不能抹掉历史。
- **主权边界**：当前系统对环境和动作采取 fail-closed，但对用户的中途主权还不完整；用户不能自然地在 Run 中取消、澄清或要求升级，这是生命周期缺口。
- **诚实性**：SR-150 的正确答案不是“完成”，而是“无法验证，需要后续处理”；这是系统可靠性的终点，而不是失败的掩盖。

## 六、最终判定

| 维度 | 判定 |
|---|---|
| 组件分工 | 结构成立 |
| 主协议语义 | 主链成立，外围和元字段仍有缺口 |
| Safety | 成立 |
| Consistency / 幂等 | 成立 |
| Authority closure | 成立 |
| Liveness | 条件成立，不是无条件成立 |
| Reachability | 核心终局成立，中途用户控制和激活前悬挂未闭合 |
| Epistemic soundness | 成立，但有 prior 渗透的政策风险 |
| Terminal completeness | 成立 |
| 整体生命周期 | **部分完备，尚不能称为完全完备** |

最准确的产品结论是：**UniClaw 已经建立了一个安全、可审计、能诚实停下的核心执行模型；它还不是一个在用户中途控制、宿主失联、激活悬挂、跨 Run 恢复和观察调度上都具备完整闭合性的产品生命周期。**

## 七、建议的后续裁决顺序

1. 先裁决用户中途取消/澄清/升级、WaitingForInput watchdog、admitted Contract View 失效和 Observation Request 是否属于当前产品范围。
2. 再把这些裁决映射到协议边和状态转换，不先扩展实现类型。
3. 为 P1 补终止测度的来源和留痕规则，为 MaterialEffect 补 prior-independent 佐证规则。
4. 最后才实现跨 Run 恢复、真实 Host/Provider 编排和 Memory buyer；每个新增语义都必须有等价类场景和反例。

参考：`docs/design/scenario-reasoning-v0.1.md`、`docs/design/protocol-completeness-formal-audit-v0.1.md`、`docs/architecture/product-architecture-baseline-l0-l3.md`、`docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md`。
