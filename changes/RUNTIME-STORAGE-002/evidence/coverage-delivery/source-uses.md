# Coverage来源与消费者

| 来源 | 实际生产/消费者与用途 | 本片处置 |
|---|---|---|
| `SettingsCoverageLedger.Report` | Runner在原实时Ledger上计算覆盖结论；事件字段不足以重建完整报告 | 原JSON冻结到`coverage-source.json`，绑定Run及三种原recording身份；不重建Ledger，不用事件补造 |
| `CoverageStepJournal.Artifacts` | Runner输出既有`coverage-steps.json`；`tools/evolution-case.py`第61/77行将其作为case输入与来源清单 | 同一唯一源保存原步骤JSON，生成器逐字节复制到第二个既有输出；任务保留，不覆盖失败 |
| 原PNG | `SettingsTraversalLiveFeed`第193/449行按显式PersistScreenshots保存当前/比较像素 | 已配置的真实capture子树声明任务留存；实际文件固定到附件包并登记SHA，不从扩展名推可删 |
| 原XML | `SettingsTraversalLiveFeed`第253行按PersistHierarchies保存；原evolution case固定XML输入用于重放 | 配置留存与显式必要附件分开，指定为必需时缺失/摘要错即不可交付，不能重跑任务补料 |
| windows/displays txt、比较json | 同一LiveFeed按捕获设置保存窗口/显示上下文、比较来源 | 作为配置的capture子树现有内容保留/打包，不假称独立Oracle或当前执行许可 |
| `facts.json`、`coverage-events.jsonl` | 既有业务结果/coverage事件/heartbeat审计投影 | 原值不因报告交付失败改写；只报告重试不写回。它们不能替代唯一report/steps输入 |
| `trace.json`、`settings-trace.json`、其它诊断镜像 | 原辅助模块及诊断/定位消费 | 沿原AuxiliaryArtifacts可选/缺口语义；不会把可选trace缺失补成完整，未整体提升为新必需输入 |
| pinned `inputs/coverage-source.json`、`inputs/coverage-attachments.zip`及generator | `RequiredReportDelivery`固定且带摘要的生成来源 | 重启仅从这些固定输入生成；原目录/原生成器丢失仍可读取。固定副本坏则显式失败，不更改FP或任务结果 |

报告要求在执行前接受并固定附件的路径、用途、必要性与已知摘要。显式文件和配置留存目录均经同一产品相对路径解析；未交付的输入和任务留存来源可由既有报告端点查询。只有类型化声明和实际消费者证明用途，PNG/XML后缀本身不授予清理或证明权限。本片不启用历史GC/任务删除。
