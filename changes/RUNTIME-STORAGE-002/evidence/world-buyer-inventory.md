# World buyer 盘点（只读）

## 结论

`FileWorldJournal` 的 v2 增量已满足当前已接线的追加写入与完整历史读取语义：每个完成的外层操作提交操作记录及本次修订差异；读取端按初始空状态顺序解码为完整原操作/修订。没有找到生产读取方只需要“最新 World”的调用，也没有找到周期性 World 基线的读取方或测量数据。因此目前不能据此认定周期快照是有 buyer 的缺口，也不能把缺少快照说成当前恢复功能失效。可确认的缺口较窄：长历史仍须逐帧验证并完整展开，增量写入仍扫描字段/集合；若目标是降低读取或比较成本，尚缺具体运行调用、规模/成本证据和明确的最新状态语义。

## 实际写入方与链路

- `src/UniClaw.Host/HostRunner.cs` 的正式路径创建 `FileWorldJournal.CreateAsync(...world.journal.jsonl)`，随后以 `WorldModel.CreateAsync(... history: new(worldJournal, runStateJournal.RecordingId, evidenceJournal.RecordingId))` 注入 World Owner。
- `src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs` 对 Settings Coverage 执行同样的创建和注入。它消费当前 World 投影供覆盖流程使用；未发现其从 journal 读取 latest 或周期基线。
- `src/UniClaw.Kernel/World/WorldModel.HistoryRecording.cs` 的 `RecordCompletedOperationAsync` 生成一个完整外层 operation row 与新增 revisions，在 `IAsyncWorldJournal.AppendOperationAsync` 确认后才发布 candidate Owner 状态。正式文件 adapter 的写入落在 `FileWorldJournal.AppendOperationAsync` → `CommitAsync`：`WorldRevisionDeltaWriter.Encode` 按修订序号编码字段差异，写 v2 frame、flush、原子替换 `.head`，之后确认。
- `FileWorldJournal` 默认每次创建新的 `RecordingId`/新文件，不支持重启后接续追加。其初始 frame 为空状态，之后 v2 frames 连续保存增量；默认单 operation frame 上限 16 MiB。`WorldRevisionDeltaWriter` 的 `base` 连续计数及 `WorldRevisionDeltaReader.Decode` 的形状/顺序校验约束解码。

## 实际读取方及完整历史需求

- `FileWorldJournal.ReadRecords()` 对只写实例会重新打开只读实例；只读构造器读取完整 JSONL，验证每帧 schema、recording ID、连续 sequence、前链 hash、hash，以及 `.head` 与末位一致。v2 每条修订被 `WorldRevisionDeltaReader.Decode` 还原，`ReadRecords()` 返回完整 `WorldHistoryRecord` 列表。当前没有 `ReadLatest` 或按序号读取入口。
- 唯一生产恢复读取链是 `FileRunRecoveryArchive` 提供 `IRunRecoverySources.WorldHistory`，再到 `RunRecoveryReader.InspectWorldRecovery` → `WorldHistoryRead.ReadOriginal`。后者读取全部 records 并逐项验证操作序号、输入、数组字段、processed evidence 引用及原 Run/Evidence 身份关联；`RunWorldRecoveryRead` 比对原始历史末位。它是停 writer 后的只读恢复/一致性检查，不还原 live `WorldModel`、不授予 current/execution authority，也不续跑。该 reader 需要完整前缀及 hash/head 末位，不能被截断的 latest 文件替代。
- `tools/RunRecoveryProbe/Program.cs` 的 probe 可调用 `InspectWorldRecovery` 或 `WorldHistoryRead.ReadOriginal`，属于显式检查工具，同样走完整历史链；它不构成日常 latest buyer。
- World Owner 初始化及创建校验会调用 `ReadRecords()` 检查新 source 是否为空（`WorldModel.HistoryRecording.CreateAsync`）。这是初始化校验，不是周期基线需求。

## 增量充分性与剩余缺口

- 对当前合同（完整 World 演变作为默认辅助历史；受保护的恢复检查必须保留并校验完整源）而言，增量格式足够：它保持原 operation payload 和完整修订逻辑结果，减少反复序列化不变字段；读取端能还原旧逻辑输出，且完整 hash/head 链不变。受保护的恢复/报告来源若引用特定 World 历史，仍须保留对应来源及引用闭包。
- 对“快速取得最新值”或“大历史定期压缩”而言，当前格式没有提供该能力：没有快照、独立最新读取、增量前缀重置/链截断、逆向 codec 或历史回收策略；每次历史读取仍验证并解码整个 journal。注意这只是能力缺项，不等于已证实有调用方受影响。增量 writer 比较 map/array 时也仍需扫描相应集合。
- 普通辅助用途（诊断、检查、完整演变查看）可在后续明确保留范围后按策略降低旧细节；一旦 World 文件承担受保护恢复核验或已被交付/显式引用的证明来源，就不能按普通辅助日志轮转。现有 reader 仍要求全历史，因此未先改变/分流 buyer 前清理前缀会破坏恢复读取。

## 最小下一步

当前证据不支持直接增加接口或周期快照。最小有效动作是先记录一个真实 latest/周期读取调用（调用点、所需 World 字段/身份、允许的一致性位置），并用该调用的历史长度、全量 reader耗时/解码量说明成本；若没有这种调用，则现有增量格式已足够当前 buyer。若未来确认 latest buyer，才以其所需字段/位置设计独立基线，并明确与 `InspectWorldRecovery` 完整历史链、引用保护及保留范围的关系；不得让快照替代或削弱原恢复 reader。
