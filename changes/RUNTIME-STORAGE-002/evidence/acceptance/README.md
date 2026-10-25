# 总验收及World完整reader补正

结论以父目录acceptance-matrix.md为准：**父Change尚未通过总体验收**。本轮补事实/成本证据并修正实际reader重复开销，未把开放的核心最新/变化、World基线策略或Coverage独立交付写成完成。

入口两文件见entry.json、entry/，最终精确diff见world-reader-final.patch。原适用性报告applicability-review.md是补测前的入口审查，后续World成本/实现处置以本文件和矩阵为准。inventory-audit.md与更新后的storage-inventory.md列出现状及未配置项。

| method | expected | actual | evidence |
|---|---|---|---|
| 128真实World观测生成v2、由同批原操作生成v1，公开reader还原 | 257操作/128修订、JSON逐条相同，成本实测 | 文件7,951,934→1,107,547 bytes；入口native读取分配159,316,504、legacy115,236,592 bytes | world-current.json、world-cost.json及其外置raw来源 |
| 同公开场景native读取128MiB预算 | 去掉重复往返/字符串复制，完整校验不变 | RED159,315,416；中间137,120,000仍未过；最终130,457,392 bytes通过。没有放宽预算/原断言 | world-reader-red.json、world-reader-green.json（实际52/53，名字不作为成功依据）、world-reader-final.json、world-cost-after.json |
| World旧格式、delta故障、原异步确认及Kill、writer内存、跨来源恢复 | 已有保护保持 | 最终53/53、零跳过 | world-reader-final.json |
| 当前Kernel诊断/提交屏障 | 原业务/Owner确认边界保持 | 52/52、零跳过；后续仅Host只读实现变化，Kernel源未变 | kernel-current.json |
| 两轴原补丁及流式读取增量复核 | 无剩余可操作问题 | Spec/Standards均0 | review-reader-spec.md、review-reader-standards.md及各delta.md |

耗时为同进程固定先native后legacy的单次观察，缓存/GC/机器负载未控制，不据此推导稳定速度。最终native仍分配130,457,392，legacy为58,839,280 bytes；空间减少不意味着全部指标更好。当前线程分配不是峰值内存。未测真实filesystem read/syscall；logicalOperations/Revisions是逻辑输出数，不冒充实际decoder调用次数。before/after是同参数独立运行，记录身份由运行铸造；每轮内部v1/v2共用同一批原操作。

所有Run由现有AssetRun完成逐文件bytes/SHA256及收尾回执；实际两轮World raw保留供同内容成本/reader复核。各测试自有临时目录已finally清理，没有整树/构建复制、没有删除旧产品源或旧证据。必要RED、中间未过及最终结果继续保留，deletedBytes=0。最终运行时唯一全仓源码漂移是其他Owner的tools/tests/test_asset_archives.py；本片两源及编译输入未变，不声称纯隔离/全仓源码稳定。没有因此重跑无关全量或撤销并行工作。

独立基线/有界区间读取及Coverage交付仍未实现，不因成本优化获得自动续跑、历史删除或新的Current World权威。未提交推送，未调用设备/产品模型。
