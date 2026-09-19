# CORE-008 Attempt/Trace 与跨领域 Slice 修订记录

## 已采纳的语义修订

1. Attempt 的产品语义与其产生方式、存储方式分开：它表达某个 Effect 的一次有产品含义
   的执行尝试，不是权限、不证明外部结果，也不要求预先新增独立数据库表。
2. Attempt 可以直接保存，也可以由满足完整性、历史留存和恢复要求的执行源记录生成；普通
   诊断 Trace 不自动具备这些保证。发送前的请求、目标绑定和尝试关联必须能可靠登记，
   不能依赖事后 Trace 或 Receipt 推断已经丢失的内容。
3. Receipt 是来源回执，需通过稳定关联挂到 Attempt；Receipt 不替代 Attempt，也不自动
   证明 World 结果。需要进入 World 时仍经 Evidence/Claim 语义路径。
4. Slice 是跨领域的局部观察或派生表示。UI 页面、机器人局部地图、文件子树、API 分页
   结果和数据库查询窗口均可使用 Slice；来源、对象、范围、观察时间和处理版本分开表达。
5. 重新处理旧数据形成新 Slice 版本，不等于重新观察；历史用途不能用当前重查结果替代。

## 当前未验证的仓库问题

需要单独审计现有执行记录：关闭普通诊断 Trace 后，系统是否仍能知道准备或尝试过哪些操作、
采用了哪些请求与绑定，以及哪些结果仍未确定。如果不能，缺口是产品恢复所需的执行记录
契约，而不是再增加一个同义模型。

## 仓库审计结果

- `EffectBoundary` 维护 append-only 的 `BindingLog` 和 `ReceiptLog`，但 `Receipt` 只在
  driver 返回后追加；当前没有看到发送前独立登记请求快照、执行端和尝试关联的持久记录。
- `ExportAttemptEvidence` 与 `ExportTransitionContext` 都以 `EffectReceipt` 为输入，不能
  在没有 Receipt 时恢复准备阶段或无回执的未知尝试。
- `RunTraceArtifact` 明确是非权威诊断投影；Trace 可以显式禁用，异步 writer 队列满时会
  丢弃记录并追加诊断，因此不能作为恢复所需的唯一执行源。
- 结论：当前仓库不能证明“关闭普通诊断 Trace 后仍可恢复准备/尝试、请求、绑定和未知
  结果”。第一缺口是发送前可靠登记与发送过程关联的执行记录契约，不是新增一个同义
  Attempt 类。

本 Change 未修改 Runtime/Trace 实现，也未声称该验证通过。

CORE-009 的契约评审结论：架构方向通过，契约为 `CHANGES_REQUIRED`。缺口集中在可靠提交
判定、发送时内容一致性和重试/补偿层次；不要求新增 Attempt 类、独立存储或 Trace 权威。
