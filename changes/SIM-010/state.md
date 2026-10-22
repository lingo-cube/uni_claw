# SIM-010 — 录制 XML 完整操作链的失败与恢复回放
lifecycle_state: closed · disposition: none · depth: standard · base: 9bc15b8974f5fb5746074e373e6c132bc4df0089

## Intent（WHAT/WHY）
用户要求继续补场景/模块/错误回放，遇到 gap 可讨论或启动前置任务并等待。SIM-009 新增合成语义输入回放已关闭；本切片使用既有真实录制 XML 经产品 parser/projector/ledger/World/grounding/Effect/Assurance，证明缺失后观察的等待恢复、错误重放的独立分歧，以及旧证据不足时的正确拒绝。

## Scope / Out of Scope
- 新增 `RecordedOperationReplayTests.cs`、独立录制 manifest/期望、SCN-REPLAY-004/005。
- 原 XML 经独立 Host 测试比对公开 parser/projector/Host occurrence adapter 的冻结导入结果；Simulation 只消费 immutable typed observation，工程仅引用 Kernel+Agent。新增 `RecordedHierarchyImportTests.cs` 与六份导入资产；不改产品/内部访问权限/闭包断言。
- 新增本 Change、evidence。SIM-009 文件/既有 World 测试不改；不改共享INDEX或模型路由，不操作设备或真实模型，不提交推送。
- 另立前置会话修 `verify-change` 漏工程与 `scenario-coverage` 参数化名字漏映射；本线不并行编辑这些工具，等待其完成并验收。

## Decisions
- 输入为既有录制原文，hash/sequence/frame 关联明确；expected 为独立固定契约值，禁止由 actual 反写。
- 失败注入只改变外部后观察的可用性或选择；缺后观察保持等待，不重复投递；错误/未变化后观察的独立业务期望不得被掩盖；旧 checked=false 的 Unknown 拒绝保留。
- 回放不直接写 World 或建关系，输出同时保留图/receipt/verification/实际 Slice 和 first divergence。对 source 输入正常通过/失败的判断都以独立期望核对。
- 录制只是历史外部输入；不声称当前设备、模型质量、任意B2后果或语义目的地符合性。
- World-UNCERTAINTY-001 当前 resolving，Q1-Q9已接受但Q10及具体接口等仍未决。其新未知清单、预算恢复和评分字段不是本线可擅自实施的契约；依赖单列。

## Acceptance（修订依据见 decisions.md，初版失败记录保留）
1. Host 原 XML→typed observation/非 canonical 候选的冻结映射由真实 parser 比对；Simulation 消费哈希钉扎的 immutable 输入，经测试侧候选 adapter，走 projector→Ledger→World→grounding→Effect→Assurance；两个 Host 保持独立组合。
2. 缺 post 先实测等待且零验证、效果不重复；送入对应 post 后同 Run 恢复。网络验证并建导航边；旧开关因 Unknown 拒绝，并带 VerificationFailed 再咨询及 no-checked-claim 原因。
3. 未变化网络 post 的单步 verified 不能满足独立导航期望；开关未变化拒绝。expectedBehavior 与 desiredOutcome 分开固定，旧录制不能证明 off 的 gap 必须保留。
4. 原文/导入资产篡改拒绝；验证结果变异（false 或 false→true）均被独立行为期望检出；SCN-REPLAY-004/005 经 schema/Trait/TRX 真值链发现。
5. 当前源码 full 八工程且 Simulation 仅一次；独立Review与Verify分开，原有工作树保护，失败保留；等待两个前置任务完成后收口。模块矩阵区分新增/既有/仍缺期望。

## Dependencies / Gaps
- VERIFY-ENTRY-001（独立前置会话，ID见 entry/ownership.json）：修复八工程枚举与参数化场景发现，完成后再用其真实入口验收。
- REPLAY-STATE-EVIDENCE-001（会话 01a163b7-c102-70f0-950d-137d03110b71）：用户选择保留旧录制 Unknown，补采与精确证明设计前置；本切片不声称该正例已完成。
- 默认 tap 仅验证目标存在而非目的地符合性；独立导航期望负责揭示旧行为，不擅改产品协议。
- World 新未知类型/存续/消费与S4输入还原/评分契约：仍由World线设计，本线只使用当前已接受公开缝；不把该设计中的候选当已实现。

## Verification
level: SCENARIO
method: 录制原文哈希、专项错误回放/恢复、独立期望、相关回归、新鲜场景覆盖、前置修复后的full、保护hash
expected: Acceptance1-5
actual: 专项Simulation6/6、Host2/2；本轮当前可执行输入零漂移的隔离full八工程2046/2046，Simulation一次；33/33场景（20certified+13descriptive）；独立Standards/Spec无阻塞；两个前置结果已等待并复核；保护5310项的外线文档/授权工具漂移保留。
evidence: evidence/sim-010/

## Status log
- 2026-10-22 · UNDERSTAND→RESOLVE→PERSIST→PLAN · 复验SIM-009 CLOSED与当前现场，读取World线设计状态，启动独立验证工具前置任务，先实现不依赖其新语义的录制操作回放。写前快照见entry/。

- 2026-10-22 · VERIFY→RESOLVE · 录制首次实跑暴露最终状态、checked=false和composition root三项假设不成立；原失败保留，用户选择Unknown拒绝＋补采设计。decisions.md逐项修订验收，不降业务期望。
- 2026-10-22 · RESOLVE→PERSIST→PLAN→IMPLEMENT · 导入/仿真拆开，固定行为期望与业务期望；等待前置设计，验证入口前置已CLOSED并独立复核。
- 2026-10-22 · IMPLEMENT→REVIEW · Simulation 专项6/6、Host导入2/2通过；当前源码隔离full运行中。导入观测不含canonical ID/关系，测试adapter等待整capture入账再释放候选，由World铸ID并绑定本轮已接纳basis；不声称现场Host adapter已在Simulation直接执行。
- 2026-10-22 · REVIEW→VERIFY→CLOSED · 独立两轴评审无阻塞；八工程full/33场景、同Kernel DLL hash、导入一致性、旧输入和保护检查完成。用户选择后续受控仿真正反例、不启设备；另起SIM-011继续，不把该未来正例算本切片完成。
