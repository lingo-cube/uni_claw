# 第五片：咨询诊断逐条追加与报告兼容

原普通Host每次咨询都序列化并覆写整个ConcurrentQueue，且从不释放队列记录。现在HostRunner将原context/decision/guardedDecision/guard/duration送入ConsultationArtifactLog，待写数限于DiagnosticObservationOptions.Capacity，由原Driver诊断消费者逐条Drain，不另造worker。每个成功追加的consultations.ndjson行带schemaVersion、生产sequence及原record，后续写不改旧字节。

AuxiliaryArtifacts沿同一MaxArtifactBytes、MaxTotalBytes及WriteTimeout限制编码/写出；MaxArtifactBytes也限制整个增量文件，默认不超过16MiB，仍在必交付报告每源默认32MiB限制内。首写CreateNew，I/O失败/超时后停止该log后续追加，待写/丢弃/已写数可见。Driver关闭后只尝试一次旧consultations.json数组，流式读取原记录；此兼容副本仍受原额度约束，额度不足可缺失。核心P25及Outcome/Effect无改动，文件仅是可选诊断。

gen-run-report优先新log、缺失时读旧JSON；新log损坏不回旧镜像，保留完整前缀并标partial。序号缺口、facts诊断计数不符、关闭未决，以及全部丢弃导致文件根本不存在，均显式不完整。catalog新增可选consultationLog输入，必交付流程固定脚本与这个输入；原v1协议/输出保持，具体实现由脚本SHA区分。公开集成测试在Prepare后删除原输入目录，新store仍用固定的log生成原咨询内容并确认交付。

## 验证

| method | expected | actual | evidence |
|---|---|---|---|
| 真实报告CLI | 新log可读 | 原脚本忽略log，首例缺consultationCount；修改后通过 | report-red.json、report-green.json |
| 完整首行+撕裂尾行+过时旧JSON | 保前缀、partial、不回退 | 同例RED→GREEN | report-tail-red.json、report-tail-green.json |
| 已写log但有丢弃 | 明确partial | 5通过1失败→全部通过 | report-count-red.json、report-count-green.json |
| 全部丢弃且无文件 | partial、0条、明确异常 | 6通过1失败→最终通过 | report-missing-red.json、report-missing-green.json |
| 公开Host文件adapter/来源固定/原辅助隔离及交付 | 有界、前缀不改、预算、失败停写、取消/未决可见、输入不丢 | 最终32/32，零跳过，含既有交付Kill场景 | host-final.json |
| 原DSH报告工具/端点/关闭/孤儿进程 | 旧报告交付契约保持 | 24/24，零跳过 | dsh-final.json |
| 最终CLI/catalog | 新旧、优先级、缺口、输入目录兼容 | 9/9 | report-missing-green.json |
| 原PNL006真实旧输入golden | JSON/Markdown字节相同且输入未变 | CHECK-PASS；最终补齐实际输入及golden文件摘要绑定 | legacy-golden-final.json |

独立Spec/Standards均0项，见review-spec.md、review-standards.md；精确入口四文件和最终新增文件diff见entry.json、entry/、candidate.patch。src/UniClaw.Host/README.md仅新增模块说明，不归属其他并行README改动。父change保持open，不声明全量/真机/模型/产品最终验收。

32条相同合成记录，旧方式逐步全JSON实文件长度累计28,852 bytes，新NDJSON加一次兼容数组5,415 bytes。它是累计写入payload字节的比较，不能等同最终磁盘空间、syscall/flush或吞吐收益：新方式有行envelope和一次额外兼容写。预算计数含接受后失败的保守预留。未测峰值、设备或断电。

## 绑定与收尾

每次运行通过run-check.py调用现有开发资产adapter，按本机配置父根/work/uni-claw/Change/Run及logs/results分类落盘；assets.json固定Run及配置digest，不改产品根路径。每轮日志、TRX/CLI输出、源码/输入指纹、摘要和实际收尾回执已登记。最终Host/DSH/Python及golden执行期源码/输入均稳定；首个report-red的全仓源码标记存在并行漂移，保留原事实，不冒称隔离纯候选。该红例对应的生成器入口原字节已单独保存；后续三个RED及所有最终运行没有此漂移。

没有创建完整仓库/构建副本，测试自有tempdir已由各finally清理；必要RED/最终材料仍供审查保留，deletedBytes=0。legacy-golden首轮只作初步观察，最终轮额外绑定实际消费者输入/期望，是最终旧格式结论的依据。不修改旧片封存和历史输入，不提交推送，不运行真实模型/设备。

下一步进入父A1–A10逐项汇总：按现有记录族的实际用途列已证/缺口，复核最新/历史资格、World成本与来源保护/默认不删除。此片完成诊断buyer，不把没有新快照buyer当成全部父验收已完成。
