# 仿真与回放发布记录

用户于2026-10-23授权完成后提交推送。本包含SIM-009至017的模块/组件回放、来源和注入、CHECKED-GUARD与VERIFY-ENTRY必要前置，以及checked补采的候选设计；后者仍保留resolving，不暗示真实补采已完成。

只含待发布文件的独立副本验收：八工程2109/2109，41/41场景，工具18/18。证据为publication-full.txt、publication-tool-tests.txt、publication-simulation-full.trx、publication-host-full.trx与publication.json。两组构成70/156 pins均匹配，不依赖被排除的并行工作树修改。

来源清单列55份测试必需资产，其中25份已在base，其余30份随本包保存；不是整周录制或完整run目录。保留核心报告/四元组、分类红例、四项变异结果、最终TRX与选定actual；完整写前保护、重复输出、临时构建快照、私有配置/ignored resolved-config保留本地。历史报告中的其他本地路径是当时审计索引；重新运行以本包代码、固定fixture、原始资产和当前测试入口为准。复现方式见report.md。

范围审计对guard与旧场景的排除建议附有依赖例外；已用CHECKED-GUARD的精确30项ownership及构成pins确认它们是必要前置，因此纳入。原始来源只按实际测试依赖与hash选取，不机械加入整目录。可识别凭证文本规则对选中文件未发现凭证值；不提交运行配置。

changes/INDEX.md按发布副本的Change状态生成；共享工作区原INDEX内容和其他任务文件均保留，提交时仅登记发布版本。当前分类误判、原CANCEL证据缺失及unknown实现gap见report.md和SIM-017设计，均未被本次发布改称修复。
