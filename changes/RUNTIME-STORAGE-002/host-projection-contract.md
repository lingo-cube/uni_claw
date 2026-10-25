# Host查询摘要的保存责任与收尾边界

本文件完成原A1/A4中RuntimeRunStore字段资格核定，范围保持在已承诺Host身份/启动/状态/结果查询；不增加产品功能线。O5的authority为Runtime Host（protocol-map.generated.md），snapshot不能整体当可丢缓存。

| 字段 | 实际来源/职责 | 保存与还原判定 |
|---|---|---|
| RunId、TaskInstanceId、ProductSessionId、launch/idempotency/correlation、acceptedContractViewRef/generation | RuntimeHttpServer检查Task/Session与Activation关联后交RuntimeRunStore.Create；查回用于准入幂等 | 核心；原始创建值完整保留，坏源不能被忽略后新铸身份 |
| ProjectRef/TestSetRef/TaskRef/Environment、StartedAt、协议schema | 原接收请求/Host时间与协议版本；用于查询和报告上下文 | 随原创建保存，不代替Kernel接受Contract的authority |
| HostSessionRef | DSH attach真实返回后Host绑定 | 核心关联；版本还原应区分尚未绑定/已绑定，不据此取得执行许可 |
| Status/Phase/Outcome/Reason/EndedAt | Host admission/attach/执行收尾事实；admission失败时可能还没有Kernel来源 | 核心；完整保留原值和变化，不能一概声称能由RunModel重建 |
| Artifacts availability/detailEndpoint | 原报告/文件的展示投影，交付权威仍为RequiredReportDelivery及原来源 | 可变查询字段，原变化与对应版本保留；不反向授予报告已交付事实 |
| Revision/ObservedAt/LastEventSequence/Consistency | Host提交及查询位置 | 必须与同次已确认摘要/原event关联，不是全局原子快照或自动续跑游标 |

最小实现保留原来已有的最新完整小摘要，新增不可变版本记录保存原event与可逆JSON变化，首条保留完整基线；无需给小摘要再建一个周期快照平台。最新查询校验其版本/来源绑定，不展开全部旧版本；公开ReadProjectionVersion支持正反数据还原。原Get/List/幂等/ReadEvents及HTTP DTO/schema保持；不能从事件的PayloadAvailable=false推断可完整重建旧snapshot。

原v1 snapshot/events保持可读。旧活动Run普通更新后，以旧latest为新历史起点并明确EarliestRevision/CompleteHistory=false；之前不存在的数据版本显式不可用，原event前缀保留。新历史的部分写/坏来源/未知后缀必须拒绝伪最新，终局结果不被仅artifact更新改变。不批量迁移/覆盖封存历史，不重跑Agent/Effect、不自动恢复或删除。

同一Host准入链的ActivationRecordStore沿用已确认身份Store修正：原Reserve/Finalize/Fail先检查关闭/故障、原JSON行成功追加后才发布三个索引，Dispose共原锁。原键/冲突/终局语义不变。该同型确认缺口随本次Host完整收尾，不另立新功能范围。

产品父根/分类路径沿RuntimeRunStore构造root解析，snapshot与history分类由同一内部逻辑组合贯通写/读/重开/版本；产品不读Harness资产配置。原构造物理签名保留，必要字节限额经产品构造参数显式注入。测试沿原公开Host/file-adapter缝及本父已接受版本数据查询：坏摘要/重复创建真实RED、完整字段正反版本、legacy起点/事件分页、原Activation与RuntimeRunStore正常行为、部分/损坏来源拒绝、原结果零重执行。最终独立两轴与联合可提交树构建通过后关闭本Host范围。
