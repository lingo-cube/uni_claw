# SIM-016 — 原录制弹窗提取、注入与处理回归
lifecycle_state: closed · disposition: none · depth: standard · base: 9bc15b8974f5fb5746074e373e6c132bc4df0089

## Intent / Scope
用户2026-10-23授权“直接接，有什么gap需要讨论吗”。接通原始XML及动作/回执片段→不可变录制提取→按原序重放/显式合成故障→真实Host分类/Runtime处理→可定位结果。继承不启设备/真实模型、不提交推送；不复制Runtime/产品DTO、不把错误字符串或canonical World结果作为输入。

## Seams / Decisions
用户批准的接通范围落实到既有SettingsCoverageRunner.Run及Options.FeedNext/CurrentCaptureId/EffectDriver与真实Ledger/World。原始XML分类目前埋在需启动ADB/Vision的LiveFeed中，最小抽取为纯Host公开SettingsPopupClassifier.Classify，LiveFeed与离线buyer共用；不进Capability Registry，不新增产品L1协议。该缝仅拥有XML结构分类，不能授权清障动作。
录制提取器/故障adapter只在tests中；原录制和旧fixture不可变。源动作、回执、帧关联完整才声明原序列；缺失环节须标合成。旧分类规则先保持以复现真实错误，再决定修复。正常跨包页误报弹窗是独立语义gap，不用包名白名单静默绕过。

## Acceptance
1. 真实原XML与trace/步骤关联提取为可重载片段，校验hash/捕获关联/原先后关系，给出缺失字段与合成部分；至少正常跨包页和真实弹窗来源。
2. 同一公开Host分类在live/replay调用；原录制复现误判，故障注入覆盖漏帧/错capture/重复或关闭无效等实际可接入的分支，注入回执与原输入独立。
3. 经既有公开Runtime/Runner输入缝消费弹窗观察与受控交付，检查清障请求、回执、后续观察和恢复/未恢复、不盲重发；不冒充真实模型或设备运行。
4. 新场景可经数据发现接入，来源/协议/实现/adapter/oracle构成锁定；旧37条与SIM-014的70 pins保持；若修复分类涉及旧冻结语义，先提交具体红例与讨论。
5. 独立Standards/Spec评审、当前源码隔离八工程full、场景/TRX关联、来源保护与生命周期说明。

## Verification
level: SCENARIO
method: 公开Host分类RED/GREEN、原录制序列/派生故障、真实Runtime处理、隔离full、来源/构成检查
expected: Acceptance1-5
actual: PASS_SCOPE：原录制2份6帧/16资产、Host专项8/8、四项变异均检出；真实Runner成功/持续弹窗/交付失败三分支符合独立oracle；最终full八工程2109/2109，41/41场景；旧37场景与70 pins不变，新156 pins匹配，执行源码零漂移。分类误判仍为已知未修复；SIM-017设计已交付且仅设计关闭。
evidence: evidence/sim-016/

## Status log
- 2026-10-23 UNDERSTAND：核对SIM-014/015关闭证据与dirty ownership；用户授权接入。接入点已明确，先做可复现样例；分类规则修订待真实红例后提问。
- 2026-10-23 RESOLVE→PLAN：已运行原录制红例1/1失败（正常Default apps被判present）。用户明确选择保留原判定，SIM-017承载窗口证据/未知状态前置设计；父线只完成提取/注入与已知失败可重现，不声明分类bug修复。原失败测试保留classification-red.cs.txt，不回写独立期望。
- 2026-10-23 IMPLEMENT→REVIEW：两份原录制/6帧、公开Host复现与原序提取、6项专项通过；公开Runner三分支分别Completed/TerminalNotProven/UnconfirmedDelivery。禁用实际hold选择器、错误oracle、错trace关联各触发1项失败；Spec两项来源措辞问题已修，语义与oracle未改。前置设计已完成并收到源码复核，待Standards独立复核。
- 2026-10-23 REVIEW→VERIFY：Standards与最后capture文件绑定增量复核无阻断；四项变异（含交换XML资产）均被测试检出。SIM-017设计已交付并复核；当前精确源码隔离八工程full运行中。
- 2026-10-23 VERIFY→IMPLEMENT：初次full 2项失败已保存attempt1-*；保持冻结的两组合根边界，把6项新场景测试/输入移入Host.Tests。只共享测试侧内容门的源文件，不引用Simulation工程；跨根承载完整性转由既有coverage入口统一按二进制Trait/TRX执法，C#保持本程序集反向trait检查。C1–C9、旧场景/认证、独立oracle与分类规则不改；补README登记。
- 2026-10-23 IMPLEMENT→REVIEW→VERIFY：跨根工具和载体增量独立复核无阻断。第二次full全部行为/场景通过，但coverage成功结尾旧变量NameError，保留attempt2-*，修复并增加CLI成功回归；工具18/18。最终当前源码隔离full全部通过，双TRX完整名关联6项新场景测试；来源/保护核验见verification.json。
- 2026-10-23 CLOSED（接入范围）：本轮提取、注入、真实Host/Runtime消费及回归完成。SIM-017前置设计已等待完成；窗口事实采集、unknown/stale实现、分类误判修复留待后续裁决与实施。本轮未启设备/模型或提交推送。
- 2026-10-23 PUBLICATION：用户追加授权“完成后提交并推送”。发布包仅含SIM-009至017及CHECKED-GUARD/VERIFY-ENTRY必要前置、checked补采候选设计和测试必需资产；排除外线改动、私有运行配置及重复快照。HEAD归档加显式文件清单的独立发布副本full八工程2109/2109、41/41场景、工具18/18通过，见publication-full.txt及publication.json；原未提交/推送声明保留为历史阶段事实。

## 2026-10-25 数据保留更新
用户明确选择“只保留正式测试基线，历史过程证据也可删除”。`evidence/sim-016/`的215份旧过程输出（TRX、日志、导出actual、重复快照及脚本/评审过程材料）由REPLAY-CLEANUP-001列单删除，仅留退役说明；以上执行/发布结果保留为当时结论，不再声称这些旧路径现存可验证。正式PopupReplay fixture/Oracle/原始录制、场景和正式载体及其依赖逐字节保留。清理不修复旧source pin拒绝，不晋升新组合。
