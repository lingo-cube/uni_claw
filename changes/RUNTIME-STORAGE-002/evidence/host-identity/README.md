# Host任务/会话身份确认顺序修正

## 原问题与实际修改
原TaskInstanceStore和ProductSessionStore的Create/Bind先改两个内存索引，再调用`_writer?.WriteLine`；Dispose把writer置null。四个公开场景均真实返回新身份/绑定，但文件不变，新reader无法找到同一值。原重复请求还可能从内存误认已经保存；问题不属于Kernel或World。

当前两Store在原_sync内先检查关闭/故障状态，成功Append（原AutoFlush）后才发布双索引；编码/写异常锁死后续变更。Dispose使用同一_sync，等已接受的同步调用结算后关闭；Get仍能读取最后确认的内存值。原API、JSON行格式、身份/幂等键、1:1及同值绑定不变；未新增跨Store事务或异步桥。故障后的调用方须重开检查原来源，不自动续跑或盲重试。

## 验证四元组
- method：原公开Store构造/Create/Get/Bind/Dispose/重开文件缝；Luna先写四个关闭后Create/Bind回归，Leader统一RED→GREEN与静态双轴评审。
- expected：关闭后写入抛ObjectDisposedException，内存/文件/重开来源均无新身份或绑定；正常幂等、1:1、绑定和重启恢复保持。
- actual：4/4 RED（初轮无逐例标准输出，补ITestOutputHelper后仍4/4同根因RED，原资料保留）；修正后4个新回归＋8个原测试=12/12，零跳过；Spec和Standards各0。所有执行期源码/输入稳定。
- evidence：identity-red.json、identity-red-observed.json、red-observations.json、diagnosis.packet.json、identity-green.json、candidate.patch/candidate.json、review-spec.md/review-standards.md。verify.py核对22项证据/绑定/资产/诊断schema，不重跑产品。

仅证明上述公开同步确认/关闭行为；写异常锁死与锁范围经代码评审，未通过新增介质接口注入磁盘满等异常。原Load容错、进程/OS崩溃、跨进程仲裁、RuntimeRunStore事件与snapshot一致性或完整历史均未由本片证明。父A1–A10保持open，不回改RUNTIME-IDENTITY-001旧结论。

## 配置与资产
身份文件继续由产品构造路径注入。正式DSH装配为RuntimeHttpServer(runsRoot)→Path.Combine(runsRoot,".identity")→task-instances.jsonl/product-sessions.jsonl；本片不改变目录、旧文件定位或Host配置，不读取Harness资产JSON。

开发验证复用现有AssetRun及固定配置，parentRoot/workPath/repositoryKey/changeId/runId/categoryPaths单一解析贯通日志与TRX引用。三轮各独立Run，消费者已完成后登记；新测试临时目录独占且自动清理，原8例仅补独占目录Dispose、原断言不变。不创建整树/临时编译副本、不删除旧证据/他人或产品源，必要RED/最终证据task保留。未提交推送、未调用设备或模型。

## 当前后续
既有接续会话已进入World基线公开RED/实现，按原顺序在World验收后做Coverage交付与附件保护。本聊天继续核定RuntimeRunStore混合字段的来源和历史职责，再据实际证据汇总父验收；不能把Projection名称或PayloadAvailable=false当作无需保存的理由，也不为填接口预造通用数据库。
