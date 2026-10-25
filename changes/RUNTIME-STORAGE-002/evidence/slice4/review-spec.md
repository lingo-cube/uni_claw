# Slice 4 Spec review

做了什么：仅以 `evidence/slice4/entry/FileCommittedHistory.cs`、`FileRunStateJournal.cs` 与 `candidate.patch` 比对 `writer-retention-contract.md`；未运行构建或测试。

原问题：原 writer 在成功确认后仍以 `_payloads` 长期保存完整序列化 payload。

本次修改：候选以 `_written`/`_hash` 保存确认端点；writer `ReadRecords` 在写门内用严格只读 reader 重读，并核对 RecordingId、条数与 hash。序号推进仍在 frame flush 与 head 成功之后；readonly 分支、批量/取消/故障锁死和格式路径未改。

验证结果：Spec 无发现（候选 `candidate.patch:17-32,41-63,83-115`；契约“writer 读回绑定自身端点”“格式、确认/批量/取消/严格 readonly 保持”）。

新发现：无。

下一步：由 Leader 按既定验证执行；父 Change 仍为 open，本结论不宣称 A1–A10 或父范围完成。
