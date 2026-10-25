# A1 生产存储盘点复核

## 方法与结论

| method | expected | actual | evidence |
|---|---|---|---|
| 复用 `storage-inventory.md`、核心/World buyer盘点及slice3/5记录；只核对其引用的当前生产装配、Owner和新增来源变化，不重搜历史 | 两正式Host、九个原生存储源、Host身份/Run投影、Coverage正式输出、附件、咨询增量和报告固定来源均按当前实现准确列明；未配置项明确写“未配置” | 主体盘点仍适用；第五片新增咨询增量及其报告固定输入需补入；Coverage正式输出与辅助产物须区分。未发现统一保留、清理或总配额配置，不能推定期限或自行补参数 | `storage-inventory.md`；`HostRunner`、`SettingsCoverageRunner`、`RuntimeHttpServer`；`evidence/slice3/README.md`、`evidence/slice5/README.md`、`evidence/remaining-core-inventory.md`、`evidence/world-buyer-inventory.md` |

两条正式运行Host是 `HostRunner` 与 `SettingsCoverageRunner`，均装配九个原生源：Evidence、Run state、Control traversal、Runtime Plan、World、Execution、P25 consultation、Run resources、Run recovery archive。它们按Run创建并由Host作用域关闭；各自的帧/head、提交和校验语义详见既有清单。单条/批次限制只在各源构造器或Options配置处定义；没有总量配额、跨源保留期限或自动清理策略的项目均为**未配置**。

| 范围 | 当前事实 / A1处置 | 字段状态与证据 |
|---|---|---|
| 两Host与九源 | 生产装配仍由 `HostRunner`、`SettingsCoverageRunner` 承担；九源 Owner、消费者、Run生命周期和既有单条限制沿用旧盘点 | Owner、consumer、lifecycle已列；各源quota以对应构造器/Options为准；总体quota和保留未配置。依据 `HostRunner` 的 `File*Journal` 装配段及 `SettingsCoverageRunner` 对应装配段，见 `storage-inventory.md` 九源表 |
| Host身份 | TaskInstance、ProductSession、Activation分别写身份账目；必须在Run目录外保持来源身份，不能由Run投影或展示文件重建 | Owner/consumer/lifecycle已列；容量、期限、回收均未配置。`RuntimeHttpServer` 使用 `TaskInstanceStore`、`ProductSessionStore`，启动绑定身份；见旧盘点“Host身份”表 |
| Run投影与附件 | `RuntimeRunStore` 的snapshot/events供HTTP创建、幂等、查询、分页及附件引用；事件标记 `PayloadAvailable=false`，故投影/附件引用不能代替canonical Run history或完整产物来源 | Owner/consumer/lifecycle已列；总量/期限/清理未配置。附件引用的物理文件与其被报告、证明或恢复引用时的保护范围没有统一登记，属未完成项；依据 `RuntimeRunStore`、`RuntimeHttpServer.BuildArtifacts` 和 `storage-inventory.md` |
| Settings Coverage正式输出 | `SettingsCoverageLedger`参与实时Agent/屏障/overview判定；收尾正式写 `coverage-events.jsonl`、`coverage-steps.json`、`coverage-report.json`、`coverage-observed-elements.json`、`facts.json` 等。不可因文件名是report/facts就归为可丢诊断 | Owner/consumer/lifecycle已列；必需输出走直接写路径，辅助导出才走 `AuxiliaryArtifacts` 限额。全量/期限保留策略未配置，不能由当前写入成功推断跨重启交付闭环。证据：`SettingsCoverageRunner` 写出段及 `storage-inventory.md` |
| 咨询诊断增量 | 普通Host新增 `ConsultationArtifactLog`：有界待写队列、原Driver诊断单消费者Drain、`AuxiliaryArtifacts`共额度追加；关闭时生成一次受限兼容 `consultations.json`。`consultations.journal.jsonl`仍是正式P25事实源，两者不可互换 | 消费者/生命周期/限额Owner已列；`ConsultationArtifactLog`队列容量等有默认配置，增量文件受`AuxiliaryArtifactLimits`的单Run总字节数及单文件字节数约束。跨Run/全盘总量、保留期限和清理未配置。证据：`HostRunner`、`ConsultationArtifactLog`、`AuxiliaryArtifacts`、`evidence/slice5/README.md` |
| 报告固定来源与交付 | `RequiredReportDelivery`接受要求后固定生成器及实际输入清单/摘要；第五片将新增咨询log列入catalog，报告优先读取该增量log，旧JSON仅在log缺失时兼容；新log损坏不退回旧镜像。Prepare后删除原Run目录仍可从固定输入生成原咨询内容 | Host报告交付Owner、消费者、交付状态和来源固定方式已有契约；`ReportDeliveryLimits`覆盖其明确的来源/产物读取、记录、并发及生成期限。固定来源不自动清理；其他报告种类和统一保留策略未配置。证据：`report-delivery-contract.md`、`evidence/slice5/README.md`、`RequiredReportDelivery`、`gen-run-report.py` |
| 辅助诊断/观察 | `AuxiliaryArtifacts`与`DiagnosticObservations`有各自有限额度/队列和完成状态；失败、丢弃、未结算写入通过facts diagnostics可见，不改变正式Owner事实 | 组件Owner、调用消费者、Host Run关闭生命周期已列；`AuxiliaryArtifactLimits`配置单Run `MaxTotalBytes`及单文件 `MaxArtifactBytes`，增量文件追加同样计入额度。跨Run/全盘总量、期限与回收未配置。见 `storage-inventory.md` 对应表及slice1/5记录 |

## 判定

- A1盘点可作为当前源码导航；九源正式事实、Host身份、Run投影及Coverage输出必须按用途分别保护。`RuntimeRunStore`事件/附件引用、Coverage报告、辅助诊断不是同一类来源。
- 新增咨询增量和必交付报告固定来源是旧盘点之后的实现增量，字段与证据已在上表补记；报告输入固定可防止原目录消失后丢唯一输入，但不等于已建立所有产物的长期保留/自动回收策略。
- 缺Owner/consumer/lifecycle者需按具体附件/产物逐项补齐；现有盘点已给出的组件级Owner和Host关闭生命周期不代表每个被引用文件都有保留Owner。凡未见配置的quota/retention均写**未配置**，不新增阈值，不授权自动删除。
- 这是一项事实盘点，不证明A1–A10整体通过或父Change关闭；旧盘点指出的未测成本、未建立的统一保留与清理策略仍是开放缺口。
