# Slice 1 SPEC review — candidate-v4

固定比较：`evidence/slice1/entry-source` → pure immutable `candidate-v4` / `candidate-v4.patch`；未使用 HEAD 或可变工作区。`parallel-kernel-driver-v1.patch` 所属的 Act/failed-step 语义不归本片，本评审未复评。

## Result

0 actionable SPEC findings。

v2 的全量字符串编码问题已被 v4 消除：`AuxiliaryArtifacts.cs:57-76,94-135` 在 `IBufferWriter` 分配前拒绝超额 size hint，并在编码的 `GetMemory`/`Advance` 检查时间和取消；临时文件仅在完整写入并 flush 后改名。咨询与遍历观察分别经同一 per-run 辅助 writer（`HostRunner.cs:204,271-274`；`SettingsCoverageRunner.cs:148,332-334`），其独立失败、丢弃和未结算状态写入两 Host 的 facts。核心记录仍未走此可丢 writer。
