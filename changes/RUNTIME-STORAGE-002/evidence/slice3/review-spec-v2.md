# Spec review — slice3 candidate-v2

审查仅覆盖冻结 `candidate-v2` 的 15 个 manifest-owned 文件、完整文件与 `owned.patch`；未运行测试，故不把测试代码或成本 JSON 当作本轮执行通过声明。

## Result: no remaining actionable Spec defect found

- **Strict replay 与 data-only 查询的分界正确。** `ReadVersionRecords` 对 v4 全量读取仍展开到 head、验证连续 sequence/hash/offset/delta，并恢复了 Initialize 的 `Sources` 双空 guard（`src/UniClaw.Host/Runtime/FileRunResourceJournal.Versioned.cs:256-277`）。`Inspect`/`InspectRecovery` 走该完整读取后，仅对末帧 Sources 做与原 v3 相同的当前前缀比对（`FileRunResourceJournal.cs:147-179`）。这保持旧有语义；不应扩张为逐帧重算所有历史外源前缀。
- **最新/历史版本 API 仍只是数据资格。** `ReadLatestVersion`/`ReadVersion` 从 head、baseline 和相邻 frame 校验出记录、confirmed snapshot、identity 与读取成本（`FileRunResourceJournal.Versioned.cs:205-253`），未导出继续执行许可，也不替代完整 replay。forward/reverse 均核对 old/new snapshot hash、相邻 sequence、offset 与 recordingId，适配 A3/A4 的错基线、跨 recording 和未知版本边界。
- **重开追加与兼容边界正确。** 保留原五参数构造 ABI，options 以附加 overload 提供（`FileRunResourceJournal.cs:36-45`）；可写 journal 使用 `FileMode.CreateNew` 并明确没有 restart/append 路径（`:56-61`）。v2/v3 继续由原 `Load` 读取，未迁移或覆写；v4 head/snapshot 均由绝对 `_path` 派生（`:49-53`）。
- **范围未越界。** 未见新增 Agent/Effect、恢复追加或清理；v4 `Inspect` 与严格恢复未借用快速版本查询作为认证捷径。新增 public regression 覆盖 Initialize source guard、CreateNew 重开不改字节、提交失败和 accepted-cancel；其效果仍待 Leader 的精确隔离执行确认。

成本 `cost/comparison-v1.json` 的三组相同输入 hash 与逻辑输出足以作为 A10 的有限成本样本；它明确报告 writer 成本上升并拒绝“全部更快”结论。本评审未发现其与 v2 guard/constructor 修正存在新的 Spec 冲突。
