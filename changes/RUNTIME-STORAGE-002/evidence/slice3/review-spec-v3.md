# Delta Spec review — slice3 candidate-v3

范围仅为 `candidate-v3/delta-from-v2.patch`，其余结论继承 v2；未执行测试。

## Result: guard is applicable; no new Spec defect

v2 的缺口是真实 A3/A4 问题：若在物理 log 末尾复制最终 frame，并把 head 的 `length`/`lastOffset` 改为副本位置，head 自身仍可看到相同的 hash 和 sequence；从较早 baseline forward 到 `target == head.Sequence` 会先到达原始最终 frame，因 sequence 已满足而停止。这会把非物理末位的同序号 frame 作为最新数据返回。

v3 在 `ReadVersionCore` 返回前，针对 `target == head.Sequence` 同时要求重建结果的 `FrameHash`、`LastOffset`、`NextOffset` 分别等于 head 的 `Hash`、`LastOffset`、`Length`（`delta-from-v2.patch:7-9`）。因此复制帧案例即使具有相同序号/hash，也会因 offset 不同抛出 `InvalidDataException`；正常的 latest 与明确 `ReadVersion(head.Sequence, Forward|Reverse)` 仍必须落在 head 所确认的实际末位。这正是“最新状态、增量和提交位置一致，部分写入/损坏不得发布伪最新状态”（A3，`state.md:44`）及未知/错基线/一致历史读取（A4，`:45`）所需的最小 guard。

新增 public regression 精确构造副本、更新 head 的 length/lastOffset，并断言 `ReadLatestVersion` 拒绝（`delta-from-v2.patch:19-50`）；这是针对已复现 RED 的最小回归。改动未扩大为逐帧外源验证、完整 replay 平台、恢复授权、Agent/Effect 或清理，因此没有越过 v2 已审范围。
