# S3 — 资源账目真实消费者与约束（只读盘点）

范围：沿现有 Run resource journal 的生产装配、Runtime 语义写入、历史读取和公开测试盘点。没有修改实现/测试，也没有运行测试或设备/模型。

## 生产写入链与语义权威

- 两个正式生产入口分别是 `UniClaw.Host/HostRunner.cs` 与 `UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs`：新 Run 目录排他创建 `resource.journal.jsonl`，并把 `FileRunResourceJournal` 作为 `RunDriverInputs.ResourceJournal` 注入同一个 `KernelRunDriver`（HostRunner 约 126、264 行；SettingsCoverageRunner 约 306、320 行）。两者同时给 FileRunResourceJournal 绑定本地 consultation 与 execution journal 来源路径。
- 语义生产者是 `KernelRunDriver.Resources.cs`：首次有效驱动时要求 journal 空且可初始化，捕获 accepted Run/View 与策略身份，写 `Initialize`；咨询、dispatch、verify 前写 `Begin`，对应操作结算后写同源 `Confirm`；安全边界的 Run/Evidence/World 位置变化用 `checkpoint` 配对确认。`WaitingForInput` 的 verify 保留同一个 Begin。失败锁存并阻止后续驱动/副作用。若 journal 实现 `IAsyncRunResourceJournal`，`AppendResourceAsync` 等待原生 AppendAsync 确认，否则走保留兼容性的同步 Append。
- 记录的资源含义由 Kernel Runtime 的 `RunResourceAccounting.Inspect` 验证与解释：消耗/授予、receipt、续额复核、资助进展依据以及未决/终态投影。Host adapter 只负责介质与来源证明；Run、Evidence、World、咨询、执行事实仍归各原 Owner。账目不是 Runtime Outcome 或 continuation 权限，`ContinuationSupported` 恒 false。
- `FileRunResourceJournal` 是 Host 写入/读取 adapter：单 writer、新写 v3、每条 frame 载荷带前链摘要及 consultation/execution 精确长度+SHA 前缀，初始化要求两来源为空。异步写 frame、flush 后原子替换 head 才发布提交；写失败锁存。同步 `Append` 是兼容入口，非生产路径推荐的异步确认语义。head 确认是 managed flush/替换边界，既有说明未声称掉电耐久。

## 读取、提交末位与来源绑定

- `FileRunResourceJournal.Inspect(path, binding, consultationPath, executionPath)` 是资源账目读取入口。无 `binding` 仍可读到分项，但不能声称 `AccountingComplete`；完整余额需原 Driver 在原 Run/View 初始化时签发的不可序列化 `RunResourceBinding`，身份必须匹配，两个原来源文件完整字节前缀必须与资源帧末次封存 stamp 相同。只比较整文件当前 SHA/长度，不重放、不修复来源 journal。
- `FileRunRecoveryArchive` 组装器负责读取原身份归档并调用 Host 严格 reader。`RunResourceAccounting.RestoreBinding` 从原归档校验身份/录制 ID 后产生只读历史绑定；这不是新 Driver 为旧记录补发的授权。`RunRecoveryReader.InspectRecovery` 另要求 `CommittedEndpointVerified`，即 `.head` 单行完整，schema、RecordingId、sequence、hash 精确匹配资源流末位；缺 head、额外/不完整行或不匹配均不能认证提交终点。
- 更高层 `RunResourceAccounting.InspectRecovery` / `InspectEvidenceRecovery` / `InspectWorldRecovery` 分别比较 Run/资源、Run/资源/Evidence、再加 World 原 Owner 末位及关联。各源分别可读不等于共同历史点；缺口保留可读分项但 Point 不成立，不回退旧前缀。必须停止原 writer；没有活跃并发快照保证。历史读取器不构造可执行 Kernel、不追加、不调用 Agent/Effect。
- 必须冻结的身份和提交绑定：RunId、TaskInstanceId、ProductSessionId、accepted ContractViewRef/Version/Generation、PolicyFingerprint 与初始配额；资源 RecordingId；原咨询/执行来源完整前缀；原归档关联的 Run/Evidence/World/Control/Plan recording IDs（归档版本覆盖范围不同）；各记录携带的 Run/Evidence/World HistoryPosition；frame 连续 Sequence/Previous/Hash；最后一条 head endpoint。不能把“有资源余额”泛化为单一全局原子提交。

## 格式与兼容边界

- 当前写入格式 `uniclaw.runtime.resource.v3`；reader 明确接受 v3 与旧 `uniclaw.runtime.resource.v2`，禁止单文件混合 schema。v3 把 RecordingId 纳入 frame hash，v2 hash 规则不含该字段。旧 v2 可继续读其帧账目；它没有 v3 RecordingId 绑定，不能自动补造原归档身份或由新 Driver 发新 binding。
- 两版均要求 UTF-8、完整换行结尾、连续序号、前链与 payload/source stamp 摘要一致；撕裂尾、坏 JSON/hash、序号断裂拒绝。严格恢复还核验资源 head 的 schema `uniclaw.runtime.resource-head.v1`、recording ID、sequence 与最终 hash。v2没有资源 head endpoint 的旧历史不能被静默提升为当前已确认末位。
- 不原地迁移/追加旧历史；旧文件只读。旧账目即使可分项读取，也不意味着可恢复 Driver、授权续跑或可认证缺失的 source-prefix/历史位置。

## 增长字段与最小真实接缝

每次 Confirm 都保留一份完整 `RunResourceSnapshot`：`ReceiptIds`、`Renewals`、`Reviews`、`UsedProgressFacts`、逐项 `FundedItems`，以及计数、pending/unresolved/terminal 标志；Initialize 也保存初始快照。列表/字典随 Run 生命周期累积，因此重复快照会随记录数增长，尤其收据及复核/资助进展。位置字段（Run/Evidence/World）和 identity 同样逐帧重复，但其大小较小且承担逐确认绑定。

最小切口应留在 Runtime 已有 `RunResourceSnapshot` 语义边界及 Host 的 `FileRunResourceJournal` 编码/reader：Runtime 继续提供可验证当前快照及 Begin/Confirm 语义；Host adapter 在实际新格式中只编码 buyer 证明需要的变化，并让公开 reader 还原完整原记录视图、严格验证 prefix/head/hash。不要抽出泛用存储平台，不改变 Owner、确认屏障或旧同步接口。是否值得做及快照/增量策略须以完整/历史读取 buyer 和字节测量裁定；本盘点不主张净收益。

## 已有测试覆盖（测试名/文件为范围证据；本任务未执行）

- `RunResourceAccountingTests`：正式 SettingsCoverage Runner 产生有效账目、确认咨询消耗、验证已投递 receipt 后的账目与余额；验证必须有原 binding/source、账目完整且 continuation false。
- `RunResourceRecoveryTests`、`RunRecoveryConsistencyTests`、`RunEvidenceRecoveryTests`、`RunWorldRecoveryConsistencyTests`、`RunEvidenceBatchStorageTests`：资源/原身份读取、Run 与 Evidence/World 共同末位、损坏/错源/提交后未 checkpoint 缺口、真实子进程窗口和严格来源一致性。`RunEvidenceRecoveryConsistencyTests`、`RunWorldRecoveryConsistencyTests` 还用 checkpoint 故障 wrapper 检查确认前后行为。
- `RunResourceRecoveryBoundaryTests`：归档初始化失败时资源账目边界；`RunEvidenceRecoveryTests` 的资源场景验证未决咨询/Effect 不可报完整余额。生产入口覆盖目前可由 SettingsCoverageRunner 公开 Runner 测例直接看到；HostRunner 的多项端到端恢复覆盖分散在上述恢复套件。
- 本盘点没有做测试覆盖率推断：具体 v2 fixture/head 缺失兼容测试需要在测试源中按用例核实；现有源码证据能确认 reader 接受 v2，但此处不把它写成已由当前测试证明。

## 提交语义参考

`docs/adr/0055-async-batch-storage-requires-confirmed-commit.md`：原生异步返回必须代表已确认提交，不能把 enqueue/受理当作成功；接受后的取消须先结算，失败不能发布为成功。资源账目是独立 Owner/格式，不与 P25、execution 或其他存储跨屏障合批。此处引用的是既有生产契约，不作扩展设计。
