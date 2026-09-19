# CORE-012 Human Review Docket — 产品侧可靠执行源接入（实现前复审）

> 日期: 2026-09-19
> Change: `changes/CORE-012/state.md`（planned；implementation_forbidden）
> 上游: CORE-010 计划 §4（实现前 Human Gate 四问）· CORE-011（仿真证明已
> closed：S1–S12 全绿，`evidence/2026-09-19-core-011-reliable-execution-source-fixture.md`）
> 契约: `docs/design/core-execution-record-contract-v0.1.md` +
> 仿真契约 `docs/design/core-execution-record-simulation-contract-v0.1.md`
> 本文件只提供裁决材料，不做实现、不冻结接口名。每个 `DECIDE:` 是需要
> 用户显式回答的决策槽。

---

## Q1 执行源 Owner 是否仍由 EffectBoundary / Runtime 承担？

**仓库事实**

- `src/UniClaw.Kernel/Effects/EffectBoundary.cs` 自述：sole Canonical
  Binding Authority + sole Effect Delivery Authority（Target §16，不变量
  23），唯一拥有 binding 认定、Effect Gate、Dispatch；显式**不拥有**
  strategy、replan、recovery、admissibility judgment、Effect Verification、
  Outcome Proof。
- `KernelRunDriver` 自述：只拥有 lifecycle 编排，「不拥有任何 canonical
  domain truth 或 judgment authority」；且是 internal 非 frozen seam。
- `UniKernel`：composition/lifecycle 协调（activation latch 已上移至此）。
- 仿真契约 §2 Owner 表已预分配：可靠执行记录行为 → Runtime /
  EffectBoundary（发送前固定、提交、发送关联、反馈追加）。

**选项**

| 选项 | 形状 | 评价 |
|---|---|---|
| A（建议） | EffectBoundary 经构造注入执行源（同 `IEffectDriver` 模式），Dispatch 内 gate 通过后、`_driver.Deliver` 前接入 | 与 sole Delivery Authority 一致；提交点物理上在唯一能越过外部影响边界的代码里，不产生第二投递路径 |
| B | KernelRunDriver 侧组件 | driver 是 internal 编排器、显式非 canonical；会把 delivery truth 拆到两个 owner——拒绝 |
| C | UniKernel 持有 | 组合根不应拥有 delivery 记录；与 ADR-0009 目标序（Bind→Judge→Gate 集中在 boundary）冲突——拒绝 |

**注意**：A 必然修改 `EffectBoundary` 构造函数（新增可选依赖）。CORE-010
期间该修改被明确禁止；产品轮授权必须显式点名放开这一处。

`DECIDE-Q1`：Owner = EffectBoundary（选项 A）？

---

## Q2 复用现有记录能否提供可靠提交与 pending discovery？

**仓库事实**（CORE-010 计划 §2 表 + 本轮代码复核）

- `BindingLog`：内存 `List<BindingDecision>`，append-only 但无请求快照、
  无提交确认语义、进程死亡即失。
- `ReceiptLog`：只在 driver 返回**之后**存在；发送前与未知窗口内无可发现
  记录。
- `KernelRunDriver` phase：运行内存状态；计划 §3 明文「不得直接冒充持久
  执行源」。
- `RunTraceArtifact` / `AsyncFileTraceWriter`：诊断面，可关闭可丢弃；
  「不能作为唯一执行源」（不变量级约束，CORE-009 契约）。

**结论**：现有记录无法提供 (i) driver 前可靠提交点，(ii) 三值提交结果，
(iii) 无 Attempt ID 的未决发现。CORE-010 spec 的复用前提（「必须先证明
可靠提交、故障范围内读取和无 Attempt ID 的未决发现」）不成立 → 按 spec
 fallback「另行设计满足契约的 Runtime 执行源」。现有 Log 保持 owner 观察
 面/诊断用途不变。CORE-011 已在测试侧证明该执行源的**语义**可行；产品轮
 需要真实现。

`DECIDE-Q2`：确认不复用（新最小执行源组件 + 现有 Log 用途不变）？

---

## Q3 第一版持久性范围是否仅为单进程崩溃/重启？

**契约事实**：仿真契约 §1 范围 = 单执行路径、单进程崩溃后重组 Host；
不覆盖磁盘损坏、跨节点、真实设备、OS 级证明。产品 v1 应闭合「仿真 ↔
真实进程重启」的差距，否则验收退化回仿真层。

**存储介质选项**

| 选项 | 形状 | 评价 |
|---|---|---|
| a | 可注入接口 + 内存默认实现 | 组合/测试最简；但真实进程死亡即失——不满足已声明模型，只能算语义切片 |
| B（建议） | 可注入接口 + 文件 append-only journal 默认（单机、普通写入；v1 不做 fsync 调优/损坏恢复）+ 测试内存实现 | 真正满足「进程崩溃/重启」；仓库已有 `AsyncFileTraceWriter` 的临时目录文件型 append-only 先例与测试模式可循 |
| c | 只冻结接口、存储 deferred | 最小，但 v1 验收仍停留在仿真层，等于重走 CORE-011 |

**建议边界**（无论选哪个）：journal 只存执行记录事实（准备集合、提交
结果、追加条目），不存 Trace、不复制 Core 对象语义；损坏恢复与跨节点
继续列为显式 non-goal。

`DECIDE-Q3`：v1 持久性 = 选项 B（文件 journal，单进程崩溃/重启范围）？

---

## Q4 `Prepare/CommitResult/DiscoverPending` 最小接口是否足够且不成为第二事实权威？

**充分性证据**（CORE-011 S1–S12 → 接口成员映射，全部已被确定性测试跑过）

| 接口成员（规划词） | 覆盖场景 |
|---|---|
| Prepare / CommitResult（三值） | S1 成功放行 / S2 失败零调用 / S3 未知零调用可查询 |
| DiscoverPending（无 ID） | S4 无可发送 / S5 未决发现 |
| GetAttempt（完整还原） | S5 准备集合还原 / S7 Receipt 还原 |
| AppendSubmission / AppendReceipt（允许无 Receipt 显式未决） | S6 未知保持、不伪造失败 |
| AppendLateFeedback（含待关联） | S8 追加不覆盖 |
| LinkRetry / LinkCompensation | S9/S10/S11（注：S9 的 RetryCommit 在产品接口中可折叠进提交重试语义） |
| 结构性独立于 Trace | S12 两臂对照 |

未出现需要更多面的场景 → 最小面判定：**足够**。

**不成为第二事实权威的约束**（实现轮必须遵守）

1. 只存 correlation 与不可变记录；不重生成 CanonicalBinding、不重判
   admissibility（判定权留在 Assurance/Gate）。
2. Append-only；不覆盖 Core 对象语义（Attempt/Effect 语义归 Core 契约）。
3. 与 `IRunTrace` 零依赖、零供给（双向）。
4. Discovery 只读；消费决策（重试/补偿/重观察）仍归 Control/Agent 流。
5. 产品接口命名不直接照搬 fixture 名（契约声明 fixture 名不是已批准
   API；命名在实现轮评审时定）。

`DECIDE-Q4`：最小接口面 + 上述五条约束作为实现轮验收的一部分？

---

## 授权边界（实现授权必须显式点名的内容）

实现授权（另行给出，当前 NONE）至少要覆盖：

1. 修改 `src/UniClaw.Kernel/Effects/EffectBoundary.cs`：构造函数新增
   可选执行源依赖 + Dispatch 内接入点（gate 后、driver 前）。
2. 新增执行源接口与默认实现（形状取决于 DECIDE-Q3）。
3. 组合根接线（`UniKernel`/`SimulationHost` 侧注入）。
4. 产品级验收测试：S1–S12 改写为产品 seam 版 + 真实进程重启语义测试
   （Host A/B 模式对真实现跑）。

未点名者维持禁止：Trace 权威、Core 顶层对象、真实外部操作、数据库、
跨节点。

## 建议裁决摘要

Q1=A · Q2=不复用 · Q3=B · Q4=批准最小面+五约束。
