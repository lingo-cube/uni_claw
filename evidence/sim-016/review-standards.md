# SIM-016 独立 Standards / Authority 评审

## 结论

**未发现阻止本轮接入的 Standards / authority 问题。** Host 分类抽取仍由 Host 拥有，Runtime 重放经既有 Runner 输入缝运行；原始录制事实、测试 oracle 与合成控制输入有区分。分类误判继续作为已知未解决差异，不应在本轮要求修正。SIM-017 是 Owner 延后的候选设计，不形成冻结协议或本轮实施义务。

## 做了什么 / 原来的问题 / 这次修改

原 XML 分类逻辑位于 `SettingsTraversalLiveFeed`，离线测试若复制算法会形成第二份分类真相；同时早期回放叙述容易把录制 XML 的历史标签误写成窗口事实，或把受控 CANCEL 后拼接误写成原始关闭证据。

本次将检查限定在最终 Host 提取、录制提取/注入、Runtime Runner 接入、场景元数据和构成 pins。`SettingsPopupClassifier.Classify` 只解析 XML、host package 和 resource ID，LiveFeed 的 `DerivePopupState` 直接转发（`src/UniClaw.Host/SettingsPopupClassifier.cs:1-22`；`src/UniClaw.Host/SettingsTraversalLiveFeed.cs:563-569`）。无 XML/坏 XML 返回 null；原有跨包或白名单命中规则仍输出 present。没有新 capability、L1 协议、Runtime/产品 DTO 或可授权清障的分类输出。

提取器从有 hash 的 trace、steps、XML、PNG 读取数据，校验 capture 顺序/关联、尺寸和触发动作 postCapture；对缺少原始 CANCEL/receipt/关闭后帧的录制拒绝宣称完整 clearance（`tests/UniClaw.Simulation.Tests/PopupReplay/RecordedPopupEpisodes.cs:24-59`）。原序观察断言的是对既有分类输出的复现，并独立比较 curated oracle，把 Default apps 的 mismatch 标成未解决（`tests/UniClaw.Simulation.Tests/RecordedPopupReplayTests.cs:11-30`）。这没有把旧 `popup` 字段提升成语义真值。

三个 Runtime 场景经 `SettingsCoverageRunner.Run`、`FeedNext`、`CurrentCaptureId` 和 `IEffectDriver` 进入真实 Host/Kernel 处理；Agent、交付响应、route/identity/clock 与关闭后布局拼接均明确受控。成功路径使用录制的非弹窗布局构造新的 post-action envelope，并在帧记录标明“controlled post-CANCEL splice; not original clearance”；持续弹窗通过输入替换注入，交付失败由 driver 提供（`tests/UniClaw.Simulation.Tests/RecordedPopupRuntimeReplayTests.cs:20-68,80-114`）。场景说明也明确没有证明原始窗口栈、模型或完整原 Run（`scenarios/SCN-REPLAY-011.json`、`012.json`、`013.json`）。故障路径操作供给输入而非直接注入 canonical receipt/World 状态。

## Authority 与候选设计

SIM-017 设计保留分类规则，且明确字段、状态形态、来源、预算及阈值均候选；“窗口来源/World/Evidence/Director”映射尚待 Owner 裁决（`changes/SIM-017/design.md:1-8,47-68`）。SIM-016 的 Host 分类抽取没有越过这条边界。`review-disposition.json` 将 P1/P2 记录为文档措辞已解决，并把窗口证据及 current/stale/unknown 实现保留为延后产品 gap（`evidence/sim-016/review-disposition.json:2-24`）；该 gap 不应作为本轮必须修复项。

本轮核验的构成 manifest 包含 SIM-014 已冻结来源的复用路径及新增 SIM-016/场景路径；SIM-016 合同也明确要求复用 70 项并保持原 SIM-014 manifest 不变（`changes/SIM-016/component-contracts.md` 的“构成manifest”段；`tests/UniClaw.Simulation.Tests/Fixtures/PopupReplay/composition.json:1-4,748-772`）。未发现改写原来源的迹象。旧 37 条与 SIM-014 70 pins 是保持约束，不是要求本次重签或重分类。

## 验证结果

静态审阅实际源文件、测试、场景、变更设计及处置记录；未运行构建或测试（本 Work Item 禁止构建）。证据支持：真实来源只用于其可证明的录制帧/步骤；synthetic 输入在实现与场景声明中可辨；真实 Host/Runtime 调用链未绕过 Owner 缝；未知窗口证据与生命周期问题维持 deferred。

## 新发现

无新的 Standards / authority 阻断项。已知 Default apps 跨包误判仍由旧规则产生，且被独立 oracle 标记为 unresolved；这与 Owner 明确选择暂不修改分类一致。

## 下一步

由主线完成其余评审与获准验证。若后续要改变窗口判定或未知状态生命周期，应先由 Owner 在 SIM-017 后续 Change 裁决证据来源/职责，再冻结接口与语义；本评审不把候选设计转成实现要求。
