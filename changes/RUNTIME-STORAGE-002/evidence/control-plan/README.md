# Control/Plan实际增长与运行中latest读取

本片完成三模式×16/64/128的实际Owner测量，并修正一条已确认latest查询热路径。父RUNTIME-STORAGE-002继续open；物理版本增量/周期基线尚未在Control/Plan实现，不能把运行内缓存当作冷启动恢复或执行游标。

## 实际问题与修改

`KernelRunDriver.BeginRuntimePlanReplacementAsync`和`CommitRuntimePlanAdoptionAsync`原来调用公开`RuntimePlanHistory`，全量反序列化/验证所有原行，只为取最新采纳及pending被接替序号。现在仅在原Append成功后更新两个私有序号；失败/丢ack不推进，未完成接替仍阻断。公开完整History与文件源/格式/严格恢复保持。新增增长载体只走实际Driver、原文件journal和公开恢复，不直接伪造原始行；没有Effect或模型/语言handler调用。

此前盘点漏掉这条运行热读取，本片已在remaining-core-inventory.md追加更正，保留当时入口结论，不再把它解释为“Plan没有latest需求”。

## 增长结论

| 实际128规模场景 | 原行数 / 物理journal bytes | 累计逻辑状态JSON bytes / 末状态 bytes | 处置 |
|---|---|---|---|
| 单plan完成128个只读候选 | Control391行 / 10,392,380 | State 2,762,840 / 11,178 | State累计重复显著，原Input也有重复的观测列表；下一物理增量须保留原输入/结果及来源关系 |
| 128个不同Plan ID checkpoint采纳/替换 | Plan256行 / 824,323 | Versions 359,323 / 2,837 | Versions表随着ID数增长，可作为后续状态增量对象；Graph仍保留原采纳事实 |
| 同Plan ID递增128版本 | Plan256行 / 312,166 | Versions 7,181 / 29 | 单行表大小基本不增长，不能套用“不同ID”的体积收益；原Graph事实与版本关联保持 |

完整16/64/128见growth-baseline-summary.json及9个baseline样本。State/Versions统计是逻辑JSON字段量，不能视作物理journal的精确分摊。正式文件还含frame、hash、转义、输入及结果。SourceSha256Preserved和完整Run/Control/Plan只读核验均成立，样本完成既定计数后返回预期`TerminalNotProven / agent-blocked:growth-complete`，不冒称任务目标已完成。

## 实测与验证

| method | expected | actual | evidence |
|---|---|---|---|
| 不同Plan ID 128次采纳/127次接替，初始化＋Drive分配256MiB预算 | 避免为latest序号展开历史，原事实不变 | RED464,566,664 bytes；修正后预算GREEN。最终独立样本40,188,360 bytes | plan-latest-red.json、plan-latest-final.json、final-plan-distinct-128.json |
| 同参数128规模前后比较 | 原行/字段长度与完整恢复保持 | distinct初始化＋Drive约464.6→40.4MB；versioned约231.1→31.0MB；Control无接替场景约228.1→227.1MB，不把小差异当收益。物理文件字节和各逻辑字段量不变 | comparison-128.json；after-*样本 |
| 既有文件源/批量/确认丢失/重开/Kill与新公开预算用例 | 原确认、替换关联和只读恢复不退化 | Host28/28，零跳过 | plan-latest-final.json |
| 原Plan/Traversal及提交屏障 | failed/lost ack、pending、版本、Control异常和来源保持 | Kernel167/167，零跳过；最终元数据更名未改Kernel源 | plan-kernel-final.json |
| 独立两轴审查 | 无剩余可行动问题 | Spec0；Standards初评1项指标命名问题，改名及增量复核后0 | review-spec.md、review-standards.md、review-standards-delta.md |

原探针`Driver.ManagedAllocatedBytes`实际覆盖初始化/装配至Drive结束，Standards指出归因不清；最终改名`CompositionAndDrive`，窗口和预算保持。旧baseline/after报告仍按此窗口解读，不能称纯Driver内部方法分配。停止reader单独测构造与ReadRecords：最终distinct128分别分配12,309,184 / 2,795,520 bytes，读取Control1/Plan256行；仍是完整读取，不是latest-only文件查询。

各样本是同参数的独立进程，Run/recording等身份重新铸造，不声明before/after输入SHA相同。部分样本并行，wall time及机器负载未控制，不推导稳定速度。managed分配不是峰值/保留内存；没有实际filesystem syscall/read-byte或掉电证明。

## 资产与恢复

入口5文件在entry.json/entry，最终4文件精确diff在candidate-final.patch；Kernel Runtime README仅追加私有位置缓存语义。既有封存/并行源保持。每轮使用配置父根/work/仓库/Change/Run及raw/logs/results分类，assets.json登记输入/源码/配置和文件SHA及实际回执。原始journal/head/资源基线在外置raw/source，growth.json只保留汇总、末状态、逻辑记录摘要及来源索引，避免再复制全记录数组。

测试自有子进程目录均finally清理，必要baseline/RED/最终证据仍被后续版本化设计消费，因此保留；未新建整树/独占构建副本，无产品或历史删除，deletedBytes=0。原构建1个nullable观测字段warning已修，其他项目既有warning不算本片行为失败。不提交推送，不运行设备/模型。

下一步以Control累计State和原输入关联为首个持久化增量/周期基线切片；Plan不同ID的Versions表可复用已验证机制时再接入，原不可变Graph/事实不能丢。新格式需独立证明旧读取、正反还原、head/基线绑定、部分写/批量确认与实际净成本，不由当前缓存优化跳过。
