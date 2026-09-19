# ADR-0023：可靠执行源由 Effect Boundary 经注入拥有，落文件 append-only journal

## Context

CORE-009 契约要求 Effect 的执行尝试具备可恢复的可靠记录：发送前可靠
登记、三值提交结果、无 Attempt ID 的未决发现、迟到反馈关联与重试/补偿
区分。CORE-010 规划确认现有 `BindingLog`/`ReceiptLog`、`KernelRunDriver`
phase、RunTrace 都无法承担（无 driver 前提交点、无三值结果、无可发现
未决；Trace 是可关闭可丢弃的诊断面）。CORE-011 以测试侧 fixture 证明
契约语义（S1–S12 仿真全绿）。CORE-012 复审四问经 Human 裁决
（Q1=A/Q2=不复用/Q3=B+提交边界限定/Q4=最小面+五约束），CORE-013 完成
受控产品实现（567/567 测试，544 基线零回归）。本 ADR 把该组稳定决策
升格为架构决策。

## Decision

1. **可靠执行源 Owner = Effect Boundary**：经构造函数可选注入
   （`EffectBoundary(IEffectDriver, IReliableExecutionSource? = null)`），
   接入点在 Dispatch 的 gate 通过、`_driver.Deliver` 之前——提交点物理
   上位于唯一能越过外部影响边界的代码内，不产生第二投递路径。
   `KernelRunDriver` 与 `UniKernel` 不成为第二 delivery truth owner
   （CORE-013 实现中两者零改动，commit 阻断 reason 经既有 GateRejected
   路径透传）。
2. **不复用现有 Log**：`BindingLog`/`ReceiptLog` 保留 owner 观察面与
   Receipt 关联用途；可靠提交、三值结果与未决发现由新最小执行源承担。
3. **v1 默认实现 = 文件 append-only journal，提交边界有操作定义**：
   `CommitResult=success` ⇔ 帧完整写入并 Flush（托管缓冲 → OS 文件
   系统），未 Dispose 的进程死亡后同机重开可完整读取；页缓存在单机
   单进程故障范围达标；torn 尾帧 = 从未提交；提交路径禁异步/批量缓冲。
   fsync 调优、OS 崩溃/断电、磁盘损坏恢复、跨节点为显式 non-goal。
4. **最小接口面 + 五条冻结约束**：Prepare（三值 CommitResult）/
   DiscoverPending（无 ID）/ GetAttempt（只读投影，不重铸
   CanonicalBinding）/ AppendSubmission / AppendReceipt（允许显式无
   Receipt）/ AppendLateFeedback（含待关联）/ LinkRetry / LinkCompensation。
   约束：只存 correlation 与不可变记录；append-only；不重判
   admissibility；与 `IRunTrace` 零依赖零供给；Discovery 只读，消费
   决策归 Control/Agent 流。接口名不是冻结公共 API 词汇。
5. **失败语义**：提交非 success 在 Effect Gate 以
   `execution-commit-failed/unknown` reason fail-closed（循
   `delivery-closed` 先例：边界机械前置条件，非 authorization 判定），
   零 driver 调用；driver 已调用后的追加失败不吞 delivery——记录停留
   pending-unknown，receipt 照常返回（恢复路径可查的安全方向）。

## Considered Options

- **KernelRunDriver 侧组件 / UniKernel 持有执行源**：拒绝。driver 是
  internal 非 canonical 编排器；UniKernel 是组合根——两者都会把
  delivery truth 拆出第二个 owner。
- **复用 BindingLog/ReceiptLog 加固**：拒绝。内存 List 无提交点可
  升格；把观察面改成存储会破坏其 owner-view 语义。
- **内存默认实现 / 只冻结接口**：拒绝。不满足 Q3 声明的单进程崩溃/
  重启故障范围；内存 fixture 通过不能替代 journal 恢复验证。
- **扩展 Dispatch 返回面（第三元组/新公共记录）表达 commit 结果**：
  拒绝。无 buyer 的公共面 churn；`delivery-closed` 先例已给出 gate
  reason 表达边界机械前置条件的既定词汇位。
- **Trace/AsyncFileTraceWriter 承担恢复存储**：拒绝（CORE-009 契约
  不变量级约束：诊断面可关闭可丢弃，永不作为恢复唯一依据）。

## Consequences

- 正面：S1–S12 在产品 seam 有确定性证明（含真实 journal 跨模拟进程
  死亡恢复、Trace 两臂独立性、Host B 协调动作持久）；「无可发送
  Attempt」「重启不重复投递」由未决集合的结构定义保证，非流程约定。
- 代价与遗留：自动重试/补偿编排未实现（接口面已备，Control/Agent
  buyer 待定——恢复消费编排是 CORE-014 docket 的议题）；journal
  路径策略（创建/retention/清理、产品 Host 默认注入与否）未定；
  真实进程 spawn-kill-restart 证明超出 v1 声明范围。
- 本 ADR 不主张：OS 崩溃/断电、磁盘损坏、跨节点恢复已验证；执行源
  已成为 Core 顶层事实模型（它只存 correlation，Attempt/Effect 语义
  仍归 Core 契约）。
