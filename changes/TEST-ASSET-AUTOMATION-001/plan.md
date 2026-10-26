# 第二片：重放与诊断开发出口
前置清理核对见evidence/slice2/cleanup-precheck.json：旧限定40文件和上轮3临时文件已不存在，恢复的4源文件SHA保持；78个验收/失败文件保留。不重新全仓盘点或删除。

1. 新tools/test_asset_operations.py经既有test-assets CLI建立/结束独立Run；重放调用原evolution-case.py及--out-dir，不修改封存REALIZATION或case。记录实际TRX/重放JSON/来源，成功失败取消先消费后收尾；全保留所需证据。
2. 诊断共享接口仅负责Harness工具出口：src/UniClaw.Host.Dsh/RuntimeToolHost.cs中保留原InvokeProcedureAsync ABI，抽内部可注入输出的core；新增RuntimeToolHost.DevelopmentAssets.cs处理显式开发Change/配置经Python解析得到的Run路径及finally回执。RuntimeHttpServer.cs仅增加可选developmentAssets请求字段/路由，原默认产品模式不依赖开发配置。tests/UniClaw.Host.Dsh.Tests/RuntimeToolHostProcedureTests.cs仅标partial供新测试文件复用既有fixture。上述文件只做诊断接线，不改产品journal、结果、恢复、必交付源Owner。
3. Python先消费重放TRX/replay.json，C#先完成packet/信号，然后finish；清理错误不覆盖原结果。诊断模型只用现有fake transport测试，不调用真实模型/设备/服务。显式取消等所有出口记录状态，未完成消费者pending可见。
4. 先公开缝RED/GREEN，后精确定向回归与独立双轴审查，保存必要diff/输入摘要，不整树复制，不重跑全量。归档/清理仍为后片，不顺手迁移存量。

此前尚未实施的存量盘点/迁移/归档/自动清理没有取消；它们等待新入口/消费者/保护证明，不因旧限定清理完成而冒称已完成。旧EVO-003 case当前realization漂移，第二片保留原件并单独导出当前case-input，不重写旧hash。

## 第三片：Run归档与恢复读取

1. tools/test_asset_archives.py实现单Run的archive/verify/restore；ZIP保留逐文件原路径/字节/摘要、原Run来源与配置绑定，索引只追加archive记录，不改历史结果或文件摘要。归档根沿原Run绑定解析，改当前配置不漂移。
2. 归档先实际解包，完整校验每文件，再调用原scenario-coverage TRX reader、evolution-signal reader及packet/schema/跨文件引用校验；未知格式只声明字节完整，不冒称消费者证明。拒绝路径上跳、重复成员、链接、缺失/新增未登记产物、错Run/错摘要。
3. restore恢复原Run根：只补缺失文件，已有不同内容或未知链接拒绝，不覆盖。path/path-ref在确属已登记归档且目标缺失时可完整恢复后返回；validate仍只读，显式区分工作目录与归档中的字节可用性，不用归档掩盖工作文件损坏。
4. 本片不提供通用删源/任务删除/自动GC。已有94文件继续保留；真实恢复证明另建本任务自有验证副本，完成归档解包/原消费者验证后，Leader按精确清单核验并移除该可丢弃副本，再通过归档恢复并复验原引用。保护原始输入、正式baseline、封存case和其他任务；没有全仓迁移、C#构建或模型/设备/服务调用。
5. 先公开接口RED/GREEN，定向回归和独立双轴审查，保存紧凑证据；A5只验收Run归档/恢复部分，受保护清理/任务级联删除仍open。

## 第四片：明确登记的归档工作副本释放

1. 仅新verification Run创建时可显式登记workReleasePolicy及继续通过逻辑引用读取的理由；既有Run不补填授权，诊断/重放暂不开放。release-work仅释放该Run外部工作根的登记文件，保留原索引、结果、证据和归档，不删除任务或产品来源。原100文件保留。
2. 删除前复验最终收尾、源稳定性、v2归档的实际解包/原消费者、精确工作清单，以及仓库内计划/WorkItem/文档/其他索引的引用。仅明确有效的test-assets逻辑文件引用可继续；物理路径、未知Run提及、baseline或外部来源依赖阻止释放。没有把closed/日期/hash当归属证明。
3. 归档、恢复、释放共享Run互斥；删前复核内容/时间/文件身份并拒绝链接或多硬链接。回执在索引追加，区分实际deleted、alreadyAbsent和pending，不改原summary/finalization或业务结论。中途失败可重试，不清除原失败记录。
4. 使用新自有副本通过真实CLI验证释放→只读validate→path-ref恢复→原消费者可读；原100不动。先接口RED，再定向回归/双轴审查。任务级联删除、全局GC和存量迁移仍留后片，不以工作副本释放冒充完成。

## 第五片（最终）：显式任务资产删除
按task-delete-contract.md完成一次闭环：公开CLI只读计划RED→最小盘点；跨任务/基线/未知保护与同任务依赖；显式执行与退役读取/外层锁；实际删除、漂移/部分失败回执与重试；原工具回归、独立双轴、资产收尾。自动GC/存量迁移不追加，不改变已关闭产品存储。所有真实删除仅本轮自有验证目录。
