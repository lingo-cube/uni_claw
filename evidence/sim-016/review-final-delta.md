# SIM-016 最后增量静态复核

## 结论

**本次两处新增均无阻断项。** 复核仅覆盖 capture 文件名绑定校验和未来语义修复时的历史证据生命周期说明；不构成对其他代码或前次评审范围的重新审查。

## 核验

1. `RecordedPopupEpisodes.Extract` 先验证 trace index 与记录的 captureId 一致，再分别要求 XML、PNG 文件名（去扩展名）等于该 captureId；不符即以 `popup-capture-file-binding` 拒绝提取（`tests/UniClaw.Simulation.Tests/PopupReplay/RecordedPopupEpisodes.cs:45-53`）。这补强了资产与帧的绑定，不改变来源内容、不改分类语义，也没有把路径名当作来源真实性的替代证明。与既有 SHA 校验共同使用，职责与范围相称。

2. `component-contracts.md` 新增说明要求未来致因 Change 保留原 trace 分类标签作为历史记录；调整旧实现复现断言以报告新旧差异；不回写独立正常页/弹窗 oracle；只有新证据与新实现通过后才更新 knownMismatch（文件末段）。该生命周期约束避免把旧错误输出改写成新真值，并保留 SIM-017 尚未实施的分类决定权。它没有要求 SIM-016 当前修分类或冻结 SIM-017 候选设计。

## 阻断项

无。按用户裁决，本轮完成提取/注入并保留原判定；SIM-017 仍仅为设计。未构建、未运行测试、未修改代码。
