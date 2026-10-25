# 第三片：资源账目版本读取与增量编码

真实buyer：两正式Runner的资源账目写入与既有Inspect/只读恢复；累计Receipt/Renewal/Review/Progress/FundedItems增长导致重复全量快照。只优化Host物理格式与只读版本查询，Runtime仍提供原RunResourceRecord，原Owner/计费/提交顺序不变。

- 新文件使用v4：完整Initialize事实、每条原元数据/来源摘要/序号、确认快照相对上次确认的正反结构增量。Begin保持上一确认快照，不为null快照重复保存整份旧列表。
- checkpoint是读取基线；按最大尾部记录数与累计增量字节触发。head原子发布同一确认的log长度/摘要/位置与已确认checkpoint引用。未确认尾部或错head不得作为最新返回；坏基线不静默退回旧版本。
- 新公开FileRunResourceJournal.ReadVersion(sequence,direction)与ReadLatestVersion返回原记录、该版本最新已确认快照、identity与读取成本；只还原数据，不返回执行许可。原ReadRecords/Inspect/InspectRecovery保留全历史严格验证与来源核对，不借快查询认证未展开历史。
- forward由不晚于目标的checkpoint推进；reverse由不早于目标的checkpoint（或最新状态）逆推，核对old/new与相邻序号/摘要。跨recording基线、错版本、坏增量明确拒绝。
- 原v2/v3只读兼容、不迁移不覆写。新writer仍排他CreateNew，原生async确认，接受后取消先结算；不跨Begin/Confirm因果屏障凑批，不添加恢复追加或清理。
- 公共测试缝沿父state已授权原journal/Driver/Host：先一个增长资源快照场景RED，再恢复/反向/顺序/空值/删除/错源/尾部/Kill与成本。指标分别记录日志/快照/head、managed写/flush、最新/历史扫描与读取，不承诺所有指标都降。
