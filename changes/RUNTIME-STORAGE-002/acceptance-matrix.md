# 父范围总验收汇总

2026-10-26当前汇总。Host身份/启动、完整版本投影、World周期基线和Coverage可靠交付均完成；Coverage初审三处缺口已修复并通过独立复审。精确联合候选Host411/411、Dsh42/42；Kernel166例的已验代码与最终候选核对，资产/报告Python与工具编译见evidence/publish/verification.json。各片RED、真实Kill、旧源和性能取舍保留原证明边界，不合成全产品或设备通过声明。用户已授权验收后联合提交推送。

| 项 | method / expected | actual | evidence / 判定 |
|---|---|---|---|
| A1 | 核对两Host、九源、身份/Run投影、Coverage及附件的Owner、consumer、生命周期、限额、保留/重建声明 | 主清单和Host逐字段责任已核定。Coverage原report/steps为唯一必交付输入；显式附件与配置capture子树按用途保留并登记摘要；facts/events保持原事实，Trace沿原可选/缺口语义。单Run及单文件限额已声明，跨Run/全盘期限未配置，不据此启用破坏性清理 | storage-inventory.md；host-projection-contract.md；evidence/coverage-delivery/source-uses.md。**职责与配置边界齐备** |
| A2 | 相同正式场景下，观察异常/拥塞/超时不改变P25结算、资源或Effect | 首片两臂及Host异常/限额已证；当前Kernel观察/原提交屏障52/52，前片Host辅助隔离及新日志回归保持。普通Host新装配有代码评审和公开文件adapter证据，不声称真实设备整条运行 | slice1 README；slice5 host-final/dsh-final；acceptance/kernel-current.json。**已证声明的确定性范围** |
| A3 | frame/flush/head及原Owner确认一致；取消、部分写、ack丢失、损坏不可伪最新 | 资源v4、Control/Plan v2的latest/正反版本、原提交屏障及严格恢复已有定向证据；Control/Plan最终159/159含真实Kill。Host身份另发现关闭后静默跳写、先改内存的具体缺口，公开4例RED→GREEN，原8例幂等/重启保持；该修正不把旧JSON行身份存储升级为九源hash/head格式保证 | slice3/4、evidence/core-versions/README.md、evidence/host-identity。**按各声明范围局部通过，不扩大为全Host耐久性** |
| A4 | 适用核心可变状态可正反查询，latest避免展开全部版本 | 资源、Control累计State、Plan累计Versions已实现可逆增量/独立周期基线及数据查询，原事实完整。Task/Session保留创建/一次绑定事实。Host逐字段核定为核心用途，新增确认摘要及不可变可逆revision；完整非空上下文/终局artifact更新/旧源保留起点/坏链/long上界已证，latest不展开旧前缀；数据历史无执行权限 | resource-version-contract.md；evidence/core-versions/README.md；host-projection-contract.md；evidence/host-projection/verification.json。**声明核心家族及联合Host定向回归通过** |
| A5 | 辅助增量可信还原、缺口明确，基线/链长按真实读取职责验证 | 咨询NDJSON完整/撕裂/丢弃/全丢及旧格式已证。World新写v3默认每32修订完整基线，latest/中间读取最近基线及后续delta；独立查询固定来源位置，完整原恢复验证仍保留，旧v1/v2原字节不改。查询明确FullWorldHistoryValidated=false，不授权截链或执行 | slice5；evidence/world-baselines/README.md、verification.json、world-final-v2-result.json。**World组件154/154及联合Host定向回归通过** |
| A6 | 已接受必交付要求、必要来源、状态可重开，重试只生成报告 | run-report与Coverage均接六态交付。Coverage任务工作前接受/绑定，固定原report/steps与附件；四个真实Kill窗口、来源丢失/篡改、仅报告重试、旧报告关闭/取消/lease、Host/Dsh/HTTP下载已证。初审三处缺口均有RED及修正，最终Host233、Dsh19、原脚本9通过，双轴v2各0 | slice2/5；evidence/coverage-delivery/README.md、verification.json。**两类既有报告均闭合** |
| A7 | 原Owner不转移；领域接口/提交/查询/辅助/交付分工、async与关闭明确 | 九源原生async/同步桥及恢复缝保持；codec、版本读模型、报告介质及有界进程留Host，Kernel不依赖文件或Harness资产JSON。新World基线由真实数据reader消费，原完整恢复仍验证；报告交付与Runtime Outcome分开，关闭/取消及未确认状态可见 | RUNTIME-STORAGE-001；core-versions、world-baselines、coverage-delivery契约/评审；publish集成证据。**声明边界通过** |
| A8 | 未配置不破坏性删除；保护活动/身份/未决/结果/来源/基线 | 产品缺省不启用历史GC/轮转；新基线不授权删旧log，身份/结果及固定输入无自动回收。待交付来源和显式任务留存可按用途查询保护；覆盖缺失/摘要错、路径与大小写歧义。开发资产释放独立Owner，四片已验收工作一并发布，任务级联删除/自动GC/存量迁移不在本轮产品收尾中追加 | storage-contract.md保留表；evidence/coverage-delivery/source-uses.md及保护用例；资产integration-manifest.json。**缺省不删除和来源保护通过，未宣称产品GC能力** |
| A9 | 声明旧版本/报告消费者兼容，原历史不改写、不授续跑 | 资源v2/v3、Run v1/v2、Control/Plan v1、World真实v1/v2及Host旧snapshot/events保持读取；原严格恢复、逐条顺序/值与错误断言保留。World周期基线与新查询不改旧源SHA；旧报告golden字节、NDJSON输入兼容，数据读取不授续跑 | slice3/4/5；core-versions、world-baselines真实旧源证明；host-projection；coverage-delivery兼容与最终联合测试。**声明版本兼容通过** |
| A10 | 同逻辑场景的空间、调用、读取/分配等实际成本，新增成本和未测项单列 | 资源16/64/256、核心writer保留内存、诊断累计写入和Control/Plan同事实成本已测；同Plan多版本增加的反向信息成本明确单列。World新v3同257操作/128修订为1,236,533 bytes，v2同源1,059,133，增加16.7%，换取latest/中间最多32次解码（v2为128/64）；实际仍验证258物理frame。间隔8/64的字节与解码对照已测。Host保留最新小摘要并增加版本文件，声明可靠性与查询收益，未冒称压缩或吞吐改善 | evidence/core-versions/cost-comparison.json；evidence/world-baselines/README.md、cost-result.json；evidence/host-projection/README.md；slice3–5成本记录。**声明成本/取舍组件证据齐备，未测峰值及设备指标不外推** |

## 本轮落实的修正

FileWorldJournal只读构造器逐行校验并缓存已解码不可变记录；ReadRecords返回独立数组，去掉完整row的Serialize→Deserialize往返。读取前后检查空文件/末行换行，原schema、序号、hash、head与delta校验保留。真实128MiB分配预算RED约159MB；中间候选约137MB未通过并保留52/53；最终约130MB，53/53。测试只扩展已有同内容成本场景，原值/顺序断言不变，未降低预算。

## 原收尾顺序的最终处置

1. Host身份/启动确认与混合投影逐字段核定、最新/正反历史已完成。创建/一次绑定事实保持原追加语义。
2. World独立周期基线、真实读取及链长/空间取舍已完成，原完整链认证保持。
3. Coverage两项接续均由目标聊天交接完成，必要来源缺失明确不可生成，重试不重跑任务。

上述原范围义务已落实；最终整体候选的测试、源绑定、依赖选择、评审及资产收尾以evidence/publish/为准。用户已授权验收后联合提交推送；不运行设备/模型，不自动删历史，不追加新产品任务。

## Control/Plan后续补测
实际三模式16/64/128已完成；Control累计State和不同Plan ID的Versions表有重复增长，同ID升版的表单行量基本固定。Plan接替热查询已从全历史展开改为确认后两个序号缓存，128次采纳初始化＋Drive分配约465→40MB，Host28/Kernel167及原pending/丢ack边界通过。物理文件/原事实不变，因此尚不能把它作为A4冷启动版本增量已完成；下一步优先Control持久化可逆增量/基线。详见control-plan-contract.md。

## Control/Plan版本存储补齐（最新）
原第一项现已验收：FileControlTraversalJournal/FileRuntimePlanJournal v2对累计State/Versions可逆增量+周期基线，原事实及v1严格兼容；数据latest/正反版本与完整原始恢复分开。公开故障/真实Kill/原batch确认及资源回归最终159/159，两轴0；额外旧封存v4 513记录摘要保持。A3/A4/A7/A9在本声明核心范围得到新增实证；A10有同一原记录/ID净成本，保存减少与CPU/同ID小表开销都披露。详见evidence/core-versions/README.md、verification.json及cost-comparison.json。
旧“下一步先测Control/Plan”是历史记录，已由本次实现/验收替代。当前剩余顺序为World独立基线/链策略→Coverage独立交付/必要附件保护，交给既有聊天按continuation-handoff.md接续；父范围继续open，不以本片局部通过代替总体关闭。

## Host身份确认补正（2026-10-26）
TaskInstance/ProductSession关闭后静默跳写及先发布内存的原缺口已通过四公开用例RED→GREEN修正；原8例幂等/重启/1:1/绑定保持，最终12/12、两轴0，22项证据核对。原格式/构造路径不变，未声称新hash/head或多Store事务。RuntimeRunStore仍按主表保留混合字段资格缺口；不得将这次局部修正当作整个Host历史重建完成。

## World独立基线/重建链补齐（最新，2026-10-26）
A5 World分支已验收：新v3按修订写独立基线，真实原完整reader消费；只读版本查询全物理链校验，解码基线+有界tail，非完整World语义认证或执行许可。A9真实原v1/v2/Evidence及源SHA保持；A10同源128修订/257 operation的额外存储、解码/分配/耗时及基线8/32/64取舍披露。154/154公开用例、双轴0、四元组与来源见world-baselines-contract.md和evidence/world-baselines/README.md/verification.json；没有截链/历史删除。旧A5未完成文字是此前状态，World分支现由本节替代；Coverage A6/附件保护仍由接续聊天推进，父总体汇总仍归原Leader。

## Coverage独立交付/用途保护补齐（2026-10-26）
A6 Coverage分支及A8对应来源用途保护已有公开证据：method=真实确定性Runner/Host/Dsh/HTTP与四个子进程Kill窗口；expected=先接受、原报告字节/结果保持、固定输入跨重启仅报告重试、必要附件缺失损坏显式失败、用途保护可查；actual=Host233/233、Dsh19/19、原报告Python9/9、独立两轴v2各0，RunId/附件歧义/任意生成器缺口均闭合，任务领域结果与咨询次数保持；evidence=coverage-delivery-contract.md、evidence/coverage-delivery/README.md、coverage-host-final-v2-result.json、coverage-dsh-final-v1-result.json、review-*-v2.md、verification.json、finalization.json。来源保护并非历史删除实施；不改Ledger Owner或总父验收。
