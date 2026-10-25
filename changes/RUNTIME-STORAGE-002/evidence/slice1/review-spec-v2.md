# Slice 1 SPEC review — candidate-v2

固定比较：`evidence/slice1/entry-source` → immutable `candidate-v2` / `candidate-v2.patch`；未使用可变工作区或 HEAD 大 diff。父 change 后续快照、历史、可靠报告交付不在本片判缺。

v1 P1 已消除：两正式 Host 的咨询/遍历诊断回调现共享同一 per-run `AuxiliaryArtifacts`，并将 `exports` 状态写入 facts（`HostRunner.cs:204,270-274,402-406`；`SettingsCoverageRunner.cs:148,332-334`）。

## Findings

### P1 — 字节上限在无界序列化之后才生效

`storage-contract.md:46,52,66,74` 要求辅助导出在资源额度下隔离；本片也承诺有界 Host 输出。`AuxiliaryArtifacts.cs:45-46` 先执行任意 `capture()`，把完整对象建成 `string payload`，之后才在 `47-53` 计算字节并拒绝超过 `MaxArtifactBytes`/`MaxTotalBytes` 的内容。两个新诊断回调分别把会增长的完整 `consultations` 列表和恢复 facts 作为该 capture（`HostRunner.cs:270-273`、`SettingsCoverageRunner.cs:332-334`）。

实际触发：咨询记录或单个 context/decision 足够大时，`TryJson(consultations)` 会先分配并编码整个 JSON；即使最终记录 `consultations.json:byte-limit`，进程已承担无上界的峰值内存/CPU，可能 OOM 或长时间停滞并影响正式流程。单写门、队列容量、写入取消令牌均不能取消这段同步序列化。应让该辅助导出使用可在产出期间强制上限的流式/有界 writer，或在受限的摘要/分段投影上序列化；否则不能把字节配置称为本片的资源有界隔离。
