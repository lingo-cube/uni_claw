# 当前Change分配与接口

2026-10-25用户要求：分Change，按合理方式分配，本聊天保留适合自己做的，需交接的交给已有任务。本分配替代首次“全部交给目标聊天”的安排，不改变已经验收/封存的范围。

| Change | 负责聊天 | 范围 | 当前下一动作 |
|---|---|---|---|
| RUNTIME-STORAGE-002 | 原存储Leader（本聊天） | 当前Control/Plan版本存储及父A1–A10总体验收 | 本片通过后按continuation-handoff.md转交World及Coverage两个后续片；不重做已封存家族 |
| TEST-ASSET-LAYOUT-001 | 存储验收后的测试资产治理（local/01a1730a-957c-7ab2-8d46-2574de5a93e9） | 本地布局配置、父根/相对分类组合、稳定Run绑定、verify-change真实路径接线与旧路径定位 | 从verify-change首个buyer配置→写读→结果引用落地 |
| TEST-ASSET-AUTOMATION-001 | 同一资产治理聊天 | test-assets.v1登记/校验、批次/retryOf、消费者完成后的收尾、可验证归档/恢复及自有清理 | 消费LAYOUT已验证接口，先首个verify-change纵向片，再其他入口 |

## 依赖与文件责任
- LAYOUT可与产品存储并行，AUTOMATION按LAYOUT接口分片接续；不再把产品父Change全部关闭作为配置/新开发产物治理的等待条件。具体删除/迁移仍需归属、消费者和有效引用保护验收。
- 产品Kernel/Host领域journal与相关产品测试、tools/RunRecoveryProbe及ResourceStorageProbe归原存储Leader。开发资产tools/verify-change、资产配置/路径/登记工具和tools/tests及两个资产Change归目标聊天。
- 诊断C# adapter路径接线如确需改动，由资产聊天在自己的计划列出具体文件/调用者归属并负责该接线；不改产品结果/恢复/报告源Owner。共享README/INDEX/state更新只做各自小范围追加/生成，不整文件覆盖别人的进度。
- 目标已派发的storage inventory只读任务可以完成，将结果写至RUNTIME-STORAGE-002/evidence/remaining-core-inventory.md供原Leader复用；不派它继续产品实现，原Leader不重复同一盘点。
- 资产聊天已有AUTOMATION立项/入口指纹或路径配置代码全部保留；配置部分转归LAYOUT，原证据按原位置引用，不为拆Change复制整树或重新测试。目标负责修订两份state的实际进度，不把规划当已实现。

## 路径组合接口
详见各state和next-handoff.md：本地parentRoot绝对，workPath/archivePath/categoryPaths相对，按repositoryKey/changeId/runId组合；分类不能越过父根。一份解析贯通写、读、引用、恢复和收尾。改配置只改变新运行布局，不自动移动旧数据；旧Run保留当时绑定。测试分类不等于产品归属或保留策略，正式baseline独立引用。

产品不读取Harness开发资产配置，核心结果/恢复/待交付来源不进入测试GC。存量批量迁移与.git维护不在这两项首片范围；不因closed/时间/重复hash直接删除。

本次完成Change拆分、范围和执行分配，不宣称新增配置/清理已经验收。不提交推送，不运行设备/产品模型。

## 用户本轮续行分配
本聊天完成Control/Plan版本存储；另外两项转交同一已有资产聊天，依次World独立基线/重建链策略、Coverage独立交付与附件保护。派发前以continuation-handoff.md记录前置实际结果和具体文件所有权，未验收不解除依赖。目标对各片执行独立双轴评审与验收后自动接续下一片，不另加人类确认。产品父Change的最终A1–A10汇总仍归原Leader。
