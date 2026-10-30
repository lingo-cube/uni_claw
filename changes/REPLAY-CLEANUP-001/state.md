# REPLAY-CLEANUP-001 — 保留正式基线，清理旧弹窗回放过程数据
lifecycle_state: closed · disposition: none · depth: standard

## Intent / Scope
用户明确选择“只保留正式测试基线，历史过程证据也可删除”。范围限本轮讨论的旧弹窗回放：SIM-016过程输出、本轮REGRESSION-DIAG-001过程数据、SIM-024 current-replay候选组合及重复载体、由本轮诊断创建的私有临时副本。保留正式fixture/Oracle/原始录制及其传递依赖，保留简短历史结论和清理记录。

## Decisions / Constraints
- 用户本次数据保留裁决优先于此前“失败尝试保留”的流程约定；不请求重复确认。
- 允许删除过程数据，不刷新正式manifest pins、不更改Oracle、不修改产品代码，不清理其他活动Change/设备/服务。退役候选的六个重复测试，不将测试减少报告为修复通过。
- 删除前逐文件登记path/size/SHA及用途；记录只留元数据，不另复制待删内容。所有正式fixture、场景、产品源码和由fixture引用的现存文件先保存SHA，删除范围必须与保护集合无交集。
- 原历史验证结论按当时执行记录理解；过程文件删除后不再声称其可在当前目录重验证。相关状态与报告追加明确退役说明，避免旧路径被当作现存证据。

## Acceptance
A1 正式fixture/Oracle/场景及其现存依赖逐字节不变；本次不修改产品源码，共享树并行漂移单独记录且不回滚。
A2 清单中非基线过程数据实际删除，候选载体没有遗留可执行引用；仅保留轻量退役说明、原结论和本次清理元数据。
A3 当前候选六例不再发现，正式六例仍存在；定向构建/回归结果如实记录，旧pin拒绝保持原状，不声明失败已修复。

## Plan
识别正式fixture依赖与候选/过程输出→冻结保护哈希与删除清单→删除并补退役记录→私有副本编译、测试发现和相关buyer回归→核对保护与删除结果。

## Verification
level: CONTRACT + DETERMINISTIC
method: 保护SHA/删除清单交集与落盘结果检查；私有副本Host测试构建、定向正式回放与窗口buyer回归；diff检查
expected: A1–A3；正式旧pin拒绝不变
actual: 删除305份旧过程/候选文件47,691,874字节，退役6个重复用例；旧诊断副本318,024,946字节及本次验证副本109,301,105字节均移除。150份正式fixture/场景/载体及引用文件SHA保持；272份产品文件中271未变，1份并行修改单独保留，本次无产品源码写入。私有构建通过；清理后24例16通过/8失败，对照恢复候选执行输入后30例16通过/14失败，扣除6重复候选后剩余失败相同（仅临时目录GUID归一化）。正式六例仍为原pin拒绝；另两例为测试临时目录清理异常，本轮不修。verify.py和diff检查通过，清理范围完成，不宣称全套绿。
evidence: report.md；evidence/deletion-manifest.json；evidence/verification.json；evidence/temporary-cleanup.json；evidence/concurrent-drift.json

## Status log
- 2026-10-25 UNDERSTAND→RESOLVE：用户授权非基线及历史过程数据清理，异步澄清明确只保留正式测试基线。核对正式PopupReplay三文件与SIM-024候选两文件；产品/正式fixture无拟删过程目录依赖。
- 2026-10-25 PERSIST→PLAN→IMPLEMENT：建立有界删除清单，保留正式依赖，候选重复载体与过程输出一并退役。无产品修复、提交或推送。
- 2026-10-25 IMPLEMENT→REVIEW：逐项核对删除范围与150份正式基线/载体保护集无交集；退役载体只属候选，正式六例及来源仍存在。旧报告/状态标明过程证据删除，不保存数据副本，不将测试数量下降当修复。
- 2026-10-25 REVIEW→VERIFY：私有构建与定向测试完成；额外两例在测试finally删除临时目录时报错，恢复候选执行输入作对照仍相同，不扩展修复范围。首次对照准备缺少非执行provenance，不作为有效对照；随后只恢复4份真实执行输入，结果明示限度。并行Reports源码漂移单列保留。
- 2026-10-25 VERIFY→CLOSED：verify.py核验保护/删除/用例发现与对照，diff检查通过；两个本会话临时副本已删。仅关闭清理范围，基线pin认证和测试目录异常未修复。
