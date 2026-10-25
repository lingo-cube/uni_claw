# Spec review applicability supplement — slice3 candidate-v1

本补充按完整冻结候选文件复判，保留旧 `review-spec-v1.md` 原文不改；未运行测试。

## Reversed findings

- 原 S2 不适用。完整候选在 `FileRunResourceJournal.cs:49-54` 明确说明无 restart/append 路径，并以可写 `FileMode.CreateNew` 原子创建 journal。已存在文件的可写重开会在构造阶段抛 `IOException`，所以 `_versionHead` 为空不会导致追加。该行为符合“新 writer 仍排他 CreateNew”“不添加恢复追加”（`resource-version-contract.md:9`）。
- 原 S1 关于“严格读取必须逐帧比较当前外源前缀”过度要求。入口 v3 `Load` 本身只校验每帧 hash、顺序及 Initialize 的空前缀（候选 `FileRunResourceJournal.cs:210-224`）；`Inspect` 只用末帧 Sources 对照当前前缀。恶意重算整条 hash 链不属于本片故障范围，不能以此新增逐帧外源平台承诺。

## Retained, narrowed defect

### S1a — v4 full replay 漏掉原 Initialize source-prefix-empty guard（高）

触发：构造一个 hash/head/baseline 均自洽、但 sequence 1 / Initialize frame 的 `Sources.Consultation` 或 `Sources.Execution` 非空的 v4 文件，再走 `ReadRecords`、`Inspect` 或严格恢复消费者。v4 `ReadVersionRecords` 只验证 Initialize 的 sequence/phase/snapshot、recording、previous、delta 与 snapshot hash（`FileRunResourceJournal.Versioned.cs:256-275`），没有检查 `first.Sources` 两个长度；入口 reader 则明确在 `Load` 拒绝相同情况（`FileRunResourceJournal.cs:217-219`）。

这是一项真实的旧 reader 语义退化，而非要求新平台：冻结契约要求 `ReadRecords/Inspect/InspectRecovery` 保留全历史严格验证与来源核对（`resource-version-contract.md:7`），A9 要求恢复消费者兼容且原历史不改写（`state.md:50`）。应在 v4 完整 replay 的 Initialize 校验中恢复两长度为零的 guard，并加入 public regression；不要求新增对每帧当前外源的逐帧比较。

## Cost applicability

`cost/comparison-v1.json` 已满足本片“同一输入”成本样本前提：三个 count 均有 input SHA 与相同逻辑输出（`:17-116`），并明确限制测量范围（`:2-8`）。其结论也正确保持边界：256 时存储字节和最新读取扫描/分配下降、writer 时间和分配上升，未宣称所有指标变快（`:118`）。
