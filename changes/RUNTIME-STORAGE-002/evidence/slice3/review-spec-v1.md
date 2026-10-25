# Spec review — slice3 candidate-v1

审查范围仅为 `candidate-v1/manifest.json` 与 `owned.patch`（entry-to-owned），未运行测试、模型、设备或子代理；因此下述是冻结候选的静态缺陷，不把测试源视为通过证据。

## Requirement defects

### S1 — v4 严格恢复/Inspect 丢失全历史 source 核对（高）

触发：写入至少两个 v4 frame；保留最后一帧的 `Sources` 与当前 consultation/execution 前缀一致，但篡改较早 frame 的 `Sources`（并重算该帧 hash、后继 `Previous`/hash 与 head），随后走 `ReadRecords`、`Inspect` 或依赖其的严格恢复。候选的 `ReadVersionRecords` 只从 frame、baseline 与 delta 复原（`owned.patch:356-375`），未比较任何 `Sources`；v4 `Inspect` 也仅比较末帧 `last.Sources`（`owned.patch:85-92`）。于是全历史 source 串改可作为严格恢复/Inspect 的正常历史通过。

这违反冻结契约“`ReadRecords/Inspect/InspectRecovery` 保留全历史严格验证与来源核对”（`resource-version-contract.md:7`），并会破坏 A9 的原恢复消费者语义（`state.md:50`）。快速 `ReadLatestVersion` 可以只作数据资格，但严格读取必须逐帧复用原 source-prefix 验证；同时加入早期 source 被替换而末帧未变的拒绝用例。

### S2 — 新 writer 没有防重开追加的有效状态/测试（高）

触发：同一 v4 journal 正常关闭后，以可写构造器再次打开并从 sequence 1 开始写入。候选对所有可写实例强制 `_versioned = true`（`owned.patch:42`），却将 `_versionHead` 留空；序号检查因而接受 1（`owned.patch:64`），`AppendVersionedAsync` 以空 previous/head 追加新的 Initialize（`owned.patch:153-188`）。这既不是排他 `CreateNew`，也不显式拒绝 restart；后续 head 指向第二个 sequence 1，完整读取会遇到重复序号/断链，或者在实际调用方改用续号时仍以空链写入。

冻结契约明确要求“新writer仍排他CreateNew”及“不添加恢复追加”（`resource-version-contract.md:9`），用户边界也要求 no append-on-restart。构造阶段应在目标 journal/head 已存在时 fail-closed，并新增“正常关闭后可写重开不能改动任一文件”的物理断言；现有测试仅覆盖只读 reopen（`owned.patch:646-958`），不能证明该约束。

## Scope/standards judgment

未发现新增 Agent/Effect、删除或恢复授权越界；版本 API 仅返回数据，符合契约。成本 probe 明确限定其统计范围，未作“全部更快”声明；但尚未看到冻结的 `cost/*.json`，故不把 A10 视为已证明。
