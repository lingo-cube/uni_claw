# SIM-016 Spec Review

## 结论

按当前 `state.md`、component contract、SIM-017 设计和实现检查，范围与用户裁决总体一致：旧 classifier 规则保留，Default apps 跨包误判明确作为未解决已知差异；SIM-017 是窗口事实/未知生命周期的候选设计，不冻结协议或字段。Runtime 验证使用公开 `SettingsCoverageRunner.Run` 与真实 Host/Kernel 处理链，外部 Agent、交付输入及 replay 时钟受控替代均有声明。

## Findings

- **P1 — SIM-017 的 Default apps 证据措辞超出已审计来源。** `changes/SIM-017/design.md` 将录制反例描述为“受控身份标题为 Default apps”，并在 acceptance/反例处当作正常页真值；但 `evidence/sim-017/source-audit.md` 说明素材是 recorded layout 与 controlled identity substitute，且录制不能证明原设备窗口关系。SIM-016 独立 oracle 明确将其作为正常页面的语义标签，因此可用于本回放的独立 oracle；不能写成该标题来自原始设备录制或它证明实际无覆盖。建议将 SIM-017 中来源表述统一为“独立 oracle/受控身份将该回放场景标为正常页面；录制只证明 XML/route 与包名差异”。
- **P2 — SCN-REPLAY-011 场景安全说明与测试证据对不上。** `scenarios/SCN-REPLAY-011.json` 的 `security.reviewNote` 称窗口与控制事实“仅按声明来源使用”，但 Runtime 测试明确合成了 post-CANCEL envelope，并以受控 route/identity/replay clock 与 Agent/driver 代替真实原 Run；component contract 已准确披露。建议该 note 改为明确“录制弹窗布局 + 受控 Agent/交付/身份/时钟及 post-CANCEL 拼接”，避免安全说明被读成原始控制事实。

## 已核对且无发现

- `RecordedPopupEpisodes.Extract` 对源 trace/steps/XML/PNG 校验 SHA、captureId 与 traceIndex 顺序、PNG viewport，并校验 trigger postCapture 关联；v1 对缺失 CANCEL receipt/post frame 拒绝冒称完整 clearance。录制只作为前缀，source cycle/context/traceIndex 被保留。
- Oracle 与提取测试分别保留旧分类输出和独立语义判定；Default apps 仍被记录为 known mismatch，分类器未改。此误判按 owner 决定留待 SIM-017 后续授权处理，不作为本轮缺陷。
- 三个 Runtime 场景分别验证成功、持续弹窗、交付失败的状态/派发/咨询数及终止行为；使用公开 Runner、FeedNext、CurrentCaptureId、IEffectDriver，分类/解析/Director/Kernel 是真实实现。成功关闭后的布局是显式合成，未宣称原录制有 CANCEL 闭环。
- held 分支通过 `SelectFrame` 将原弹窗帧替换为正常候选帧的实际供给，并记录两者来源；drop-once 注入缺 XML 后恢复供给。禁用选择器变异有记录，确认该分支不是单纯附加日志。失败 delivery receipt 由真实 EffectBoundary 对受控 driver 结果生成。
- SCN-REPLAY-011/012/013 分别登记，expectations 对齐各自 oracle 的状态、effects、agent consultations；SIM-017 保持候选字段/阈值不冻结，未把旧分类修复混入本轮。

本评审为静态 spec/source review；按 WorkItem 未构建或运行测试。
