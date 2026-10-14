# SETTINGS-COVERAGE-003 执行计划

1. 证据校正：清理上轮报告中把 Host guard 拒绝写成 bridge no-response 的错误表述，确认真实代码路径。
2. 动态前沿：在 SettingsCoverageLedger 增加跨观察/滚动周期的安全入口 frontier 与 checkpoint，静态 targetPages 仅作兼容的已知 seed。
3. 计划驱动：让 Director/Leader 每次先处理当前 frontier；对不可唯一、危险或无验证证据的入口持久化 skip/blocked reason。
4. 能力链：保持真实 perception/vision 作为观察源，接通语言检测与 Slow 结果的 capture/route 绑定。
5. 真实运行：重启注册模拟器与专用 DSH，逐轮增加 steps/depth，最多 8 轮修复；每轮保存精简 manifest，不提交大体量原始资产。
6. 审核收口：跑 focused tests、全量 tests/build、协议状态机/invariant/certification，检查 git diff 和 evidence provenance，再提交并推送。

## 实际执行结果

- 已完成 1 次 DSH 适配器 characterization + 8 次 Leader-file 有界运行。
- 两轮有限 coverage set 在真实 emulator 上达到 100% verified；深度运行暴露 Host policy、route fallback、identity-only grounding 三类问题并完成前两类修复。
- 第八轮仍在 identity-only 无 XML/scroll grounding 时 safe-stop；Change 保持 `VERIFY/BLOCKED`，不宣称完整 Settings 树闭包。
