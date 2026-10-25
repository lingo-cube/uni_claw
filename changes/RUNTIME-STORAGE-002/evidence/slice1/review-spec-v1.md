# Slice 1 SPEC review — candidate-v1

固定比较：`evidence/slice1/entry-source` → `candidate-v1` / `candidate-v1.patch`；未使用 HEAD 大 diff。父 change 的快照、历史与可靠报告交付均不在本片判缺。

## Findings

### P1 — `consultations.json` 导出没有资源字节上界

`storage-contract.md:46,52,66,74` 要求辅助采集/导出在异常、停滞、拥塞时隔离，并声明数量、**字节**与等待限额。`HostRunner.cs:268-273` 将每次咨询后的整个 `consultations` 列表 `TryJson` 后直接 `File.WriteAllTextAsync`；这条路径没有使用 `AuxiliaryArtifactLimits`（后者仅从 `HostRunner.cs:369` 起用于其它产物），而 `DiagnosticObservationOptions` 只限制队列条目数与时间（`DiagnosticObservations.cs:6-18`）。

实际触发：大量咨询或单条 context/decision 很大时，队列中一项回调仍会同步构造无上限的全量 JSON，随后接受任意大小的写入；容量 32 不限制该 payload。导出虽不再把 P25 Reject，却可无界占用内存/磁盘，并在关闭超时后继续作为未结算后台工作，违背本片“有界关闭/资源额度”的辅助输出隔离。应将该导出纳入明确的字节策略（或以有界分段/摘要导出）并在状态中报告因限额产生的缺口。
