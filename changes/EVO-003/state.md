# EVO-003 — 运行产物导出不可变case与真实组件重放
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: a05176f3

## Intent（WHAT/WHY）
用户2026-10-24“下一步”承接EVO-002交付建议，授权实现runDir→不可变case导出，让已有live失败信号能够进入可复现取证。首个真实买方为SIM-016已验证的Settings XML弹窗分类；复用原始capture/动作回执/独立oracle，不扩为任意领域或完整Runtime重放。

## Scope / Out of Scope
范围：Harness导出/校验/重放CLI、case机械schema、独立工具测试载体、公开CLI回归与文档。支持既有facts/settings-trace/coverage-steps/XML关联格式；从显式独立oracle选取布尔标签，导出自包含原始输入和来源hash，生成可执行重放命令与真实TRX。可引用已匹配的live信号与诊断包，不复制诊断正文。
范围外：产品src、旧语料/期望/scenarios、分类修复、窗口语义裁决、模型或设备调用、完整Run状态恢复、自动创建Change/修复/合并/晋升、提交推送。其他领域或缺材料仍明确不可导出，不伪造输入或复现命令。

## Decisions
- D1 仅支持`settings-popup.v1`适配：从指定capture唯一关联到原trace和DeliveryCompleted回执，保存完整原始关联资料及相邻观察XML。相邻观察不等同已证明的独立pre-action捕获；未录制的清障/时间戳不补造。
- D2 oracle须显式传入、在run之外，含独立来源描述、匹配目标与布尔标签。标签不是原始窗口栈事实；不得从facts/trace popup或本次actual派生expected。机械校验不能认证Oracle的认识来源，独立review与gate仍必需。
- D3 新目录排他创建，`case.json`及其sha256封存最后写入；原输入不修改。输入和Oracle字节复制进bundle，重放不读取原run。缺失/错capture/重复关联/损坏/不支持/输出已存在均拒绝；不重用或覆盖旧case。
- D4 验证载体放tools独立test project，经`SettingsPopupClassifier.Classify`公开缝调用；真实TRX记录结果。载体只在显式replay运行，不进入solution默认回归；不增加产品组件/Capability。
- D5 锁定载体、脚本与分类配置源码hash；产品分类器本身是候选修改面，baseline只记录不作为immutable输入执法。模型/设备不进入本片。
- D6 导出只表示该组件输入可重放，不保证一定RED、根因已确定或修复已获准。独立标签不符可RED，正对照可GREEN；SIM-017窗口/分类语义gate保持required，不采纳EVO-001候选。
- D7 本期CLI出口与消费缝即验收缝：export/verify/replay，以及已有公开分类器。用户“下一步”已授权这一导出和复现验证范围，不另建私有测试接口。

## Acceptance
| ID | method | expected | actual | evidence |
|---|---|---|---|---|
| A1 | 真实Default apps run导出、schema/sha验证 | 输入/Oracle/回执/capture可追溯，封存后不可覆盖；不依赖原run继续存在 | PASS：六类原始输入及Oracle字节/来源hash一致；封存verify通过；临时副本原run移除后仍可verify | evidence/cases/default-apps/case.json；evidence/tools-suite.txt |
| A2 | 损坏、错关联、非独立Oracle、输出冲突公开CLI反例 | 明确拒绝且旧run/已锁case不变，不产生虚假replay-ready | PASS：错/重复关联、失败回执、缺XML、run内Oracle、篡改输入、已存在输出均拒绝 | evidence/tools-suite.txt（test_evolution_case 8项）；evidence/verification.json |
| A3 | 导出case交公开分类器实际重放两次 | Default apps独立标签反例重复RED且真实TRX原因一致；真实SIM弹窗正对照GREEN；同manifest/inputs不变 | PASS：Default apps两次真实TRX Failed，均为同一独立标签不符，manifest及inputs相同；SIM dialog实际TRX Passed；分类器未修复 | evidence/replays/default-apps-1/；evidence/replays/default-apps-2/；evidence/replays/sim-dialog-1/；evidence/smoke-verification.json |
| A4 | signal/diagnosis引用与realization hash检查 | 仅匹配来源可引用；工具/配置漂移拒绝，分类器候选面不被错误锁死 | PASS：真实live信号同run引用；诊断受控fixture仅path+sha引用，错run/hash拒绝；临时实现副本验证配置漂移拒绝而产品函数修改不被锁死 | evidence/tools-suite.txt；evidence/cases/default-apps/case.json |
| A5 | 双轴review、针对回归、保护hash与diff | 0遗留阻断，原dirty/旧资料不变，明确组件复现与live/整Run边界 | PASS：Standards/Spec均0问题；tools 44/44、Host RecordedPopup 8/8；27份保护hash与11份评审输入均一致；registry/diff通过 | evidence/review-standards.md；evidence/review-spec.md；evidence/verification.json |

## Constraints / Owner-Authority impact
Uniflow拥有生命周期；case只是证据工件。原Runtime/Evidence/World/Assurance/Effect Owner不变。导出不使canonical结果变成新观察，不把receiptCommand再执行，不把夹具标签当当前权限。

## Assumptions / Alternatives / Residual risks
首片不提供通用adapter注册或case数据库。被拒：从失败描述推expected、用运行产物替代独立Oracle、拷贝Runtime实现、回写锁定manifest、把全run资料存在等同可重放。只有此组件已具确定性输入和公开消费缝；其他领域仍需真实buyer证明。旧legacy case不迁移。

## Verification
level: SCENARIO（历史原始输入+离线公开组件；无ENVIRONMENT）
method: export/verify/replay公开CLI红绿、实际TRX与hash、独立Standards/Spec评审
expected: A1–A5
actual: PASS，A1–A5均证明；44项工具测试和8项既有Host回归通过，两次同case预期RED及正对照GREEN。无产品src改动、设备/模型调用或提交推送；SIM-017语义gate保持required。
evidence: evidence/verification.json

## Status log
- 2026-10-24 UNDERSTAND→RESOLVE→PERSIST→PLAN：复验EVO-002已关闭/未提交现场；原23份tracked修改及关键新增工件存保护hash。已有XML/receipt/capture与独立Oracle足够支持有界组件买方，缺清障/窗口事实明确保留。计划见plans/EVO-003.md。

- 2026-10-24 PLAN→IMPLEMENT：公开export/verify红绿已通过；原始六类输入、独立Oracle、case封存与realization pins落地。新增tools独立xUnit载体，经既有公开分类器运行真实TRX，replay接线验证中；无产品src/旧材料修改。
- 2026-10-24 IMPLEMENT→REVIEW：实现与文档固定于evidence/review-input.json；独立Standards/Spec评审均0问题，未产生修订实现。
- 2026-10-24 REVIEW→VERIFY：工具44/44、Host RecordedPopup 8/8及registry/diff通过；Default apps同manifest连续两次实际TRX标签不符，SIM dialog正对照Passed；27份保护hash及11份评审输入复核一致，原始输入与封存复制件一致。NuGet漏洞缓存访问产生NU1900警告，构建及测试正常完成。
- 2026-10-24 VERIFY→CLOSED：A1–A5四元组完整；结论只涵盖导出与组件重放，不包含分类修复或原始窗口事实。无阻塞Human Decision；下一段先补SIM-017窗口事实和分类语义，再决定候选修复。交付见report.md。
