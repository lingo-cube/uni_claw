# RUNTIME-STORAGE-002实施计划

唯一需求/验收：`changes/RUNTIME-STORAGE-002/state.md`。用户已授权Leader实施，简单独立任务交Luna；具体模型只在本次Host派发指定，不进入共享契约。Codex目录尚无model bindings，不借用DSH供应商；用户明确指定Luna的委派直接依照本轮指令，不静默降级。

## 垂直切片与顺序
1. 正式流程/辅助观察隔离：在既有KernelRunDriver.DriveAsync、RunDriverInputs回调和两正式Host公开缝先复现咨询导出污染P25结算，再实现有界采集/独立故障及关闭；不改提交/Effect/恢复语义。Luna负责生产存储清单和独立公开回归，Leader负责生产实现/装配/定位证据/整体验证。
2. 必交付确定性报告：接受要求时可靠保存稳定身份、来源要求与必要材料，生成/校验/交付独立结算，重启查询与显式重试不重跑任务；使用现有run-report/coverage buyer。不新增模型或远程发送。
3. 核心最新＋版本化变化：以已有增长资源账目及状态查询为buyer，冻结可验证提交位置、正反历史读取及快照策略；保持不可变事实和旧reader，新增逆向信息不施加给普通辅助日志。
4. 辅助增量/快照与维护：以World、咨询历史和原报告消费者验证增量链/读取边界；配置保留保护/配额/有界关闭，仅策略明确时启用辅助回收，不改旧封存。
5. 所有切片独立双轴评审与四元组证据，最后逐A1–A10核查；未完成条目保持open，不能仅因首片通过关闭父change。

## 测试缝与证据
- 已授权公开测试缝：原领域journal/Owner/Driver、正式Host及原报告工具；按state A2–A10验收，不额外要求用户重复确认这些入口。
- RED→GREEN按行为切片逐个推进，避免先写虚构通用接口/成批结构断言。
- 共享工作区先保留入口SHA与候选diff。每切片只测相关回归；正式结束前选择受影响范围，既有RED要按实际候选辨认，不复用旧通过数冒充当前结果。
- 实施与证据放本change新目录，保存失败尝试；不改写RUNTIME-STORAGE-001/WORLD-HISTORY-002封存。禁止真机/模型调用。

## 首片范围
- Leader：Kernel运行时观察隔离实现、Host装配/产物错误与关闭、必要公开契约/README/白名单、Host公开回归、state与证据。
- Luna inventory：只写`changes/RUNTIME-STORAGE-002/storage-inventory.md`（WI-STORAGE002-INVENTORY）。
- Luna regression：只写`tests/UniClaw.Kernel.Tests/Runtime/AuxiliaryObservationIsolationTests.cs`（WI-STORAGE002-OBS-RED）。
- 不依赖整个git diff作为本片diff；当前大量未提交代码属于其他change。候选以入口文件副本/摘要和本片新增文件独立记录。

## 首片完成后的恢复入口
- 首片纯候选v6/双轴评审/验证入口：`changes/RUNTIME-STORAGE-002/evidence/slice1/README.md`；Kernel25/25、Host12/12，不关闭父change。
- 其他任务并发改了KernelRunDriver的Act scope/failed-step，保留根工作区；`parallel-kernel-driver-v1.patch`为非本片差异，后续操作须重新捕获实际源码，不能把pure候选复制覆盖共享文件。
- 后续先做本计划第2片。报告要求在接受时可靠记录，准备好来源版本和可恢复的必要材料；重启后查询/仅报告重试。现有`RuntimeHttpServer`收尾调用run-report后只写log，当前没有可靠交付状态；Settings覆盖材料包含不能由九源重建的部分，不能只存pending标记。
- 不重新讨论已确认取舍，不把当前全量旧失败当新功能授权；验证等级/边界按state与首片README解释。

## 第二片完成后的恢复入口
- `changes/RUNTIME-STORAGE-002/evidence/slice2/README.md`：冻结candidate-v2、1007输入/44程序集、纯Host23/DSH50/Python2、独立Spec/Standards及真实Kill/关闭证据。`verify.py`只读复验封存，不运行模型/设备。
- 启动显式requireReport先接受；实际任务先终态，报告失败/重试只影响交付域；默认普通报告及直接legacy工具入口兼容。已固定来源与脚本不自动清理，Delivered与当前可读性分开。
- 评审的错Run/Host-only kill/正常关闭三项已修；报告状态每次确认的单逻辑async适用ADR-0055因果屏障例外，不另造队列凑批，不声称批量收益。
- 本片只接首个run-report buyer，其他报告Owner、核心历史/快照及保留策略继续按父state逐项核对；下一片优先增长型资源账目，先明确实际读写消费者、版本末位及可逆值，再写RED。
- 根工作区并发README登记保留；核心代码与候选相符，未提交推送。不要用本片73个.NET定向测试替代全量/真实设备证明，也不要更改首片封存。

## 第三片资源首家族完成后的恢复入口
- `changes/RUNTIME-STORAGE-002/evidence/slice3/README.md`与`candidate-v4/manifest.json`：15文件，1015输入/48程序集；纯Host181、DSH50与Kill2通过，三档同输入成本与v4双轴评审。
- 资源log新v4写法/periodic基线/head只在Host；Runtime仍提交原RunResourceRecord。新data-only版本查询不可代替旧strict全源恢复资格核验；原v2/v3只读和5参constructor保持，禁止重新开writer追加或自动清理。
- 下一步先按真实buyer清点Run/Control/RuntimePlan等适用可变族的已有delta/版本与latest成本，避免把资源codec扩成泛用平台；随后做World/咨询等辅助采集与快照/保留。父任务未关闭。
- 成本有明确代价：writer编码CPU/分配增加、snapshot新增IO，不能只报压缩率或把无源prefix-hash的离线样例等同完整生产延迟。

## 当前接续Owner
用户已把下一步交接给local任务01a1730a-957c-7ab2-8d46-2574de5a93e9（存储验收后的测试资产治理），与资产整理联合推进。按`changes/RUNTIME-STORAGE-002/next-handoff.md`执行配置/父根/分类路径约定；产品与开发资产分范围验收，本聊天不再平行改实现。

## 最新分配（替代上节整项转交）
产品余项仍由原聊天负责；资产聊天只接TEST-ASSET-LAYOUT-001和TEST-ASSET-AUTOMATION-001。执行按changes/RUNTIME-STORAGE-002/work-allocation.md；复用其只读storage inventory报告，避免重复探索，不同时改产品源。
第四片（2026-10-25继续）：先消除Control/RuntimePlan/Run文件writer已落盘序列化历史的重复长期缓存；契约见changes/RUNTIME-STORAGE-002/writer-retention-contract.md。每个行为先公开测试RED再改生产，Leader统一构建避免并发；随后独立双轴评审和定向恢复回归。World辅助读取与保留维护仍在后续范围。

第四片结果：公开内存RED→GREEN及资源失败锁定补正完成，最终定向209/209、两轴原范围及扩展均0。证据evidence/slice4/README.md；运行走配置父根/相对work/仓库键/Change/Run/分类路径。下一片按真实消费者处理Host诊断全量镜像的有界增量及报告兼容；World无新latest buyer不先造接口，成本/父范围资格仍要在总体验收明确。

第五片：consultation-log-contract.md固定普通Host诊断buyer。CLI先验证增量输入→有界公开文件adapter逐条追加→原Driver观察队列装配→一次旧镜像→报告固定输入和缺口→定向回归/两轴评审。Leader统一C#构建并与资产聊天协调输出；测试源和最小入口绑定沿现有外置资产根，不复制整树。

第五片已完成：Host32/DSH24/Python9、旧golden字节一致，两轴0；evidence/slice5/README.md为恢复入口。下一步先以A1–A10做范围汇总与缺口判断，重点核对核心族适用性、World成本和保留保护，避免无限追加无buyer机制。父change不因此关闭。

父总验收：独立A1事实核对及A3/A4/A5/A10适用性审查；Leader整理A2/A6/A7/A8/A9及证据源漂移。先同步旧inventory并在已有World同内容v1/v2用例补constructor+ReadRecords成本和逻辑等价观测；不把文件长度冒作实际I/O、不把GC后保留冒作峰值。结论必须区分局部通过/未测/真实缺口，未满足的验收不通过修改措辞自动变绿。

总验收补正：World完整reader真实159MB分配→移除重复往返及整文件字符串复制→130MB，原128MiB预算及JSON断言保持；最终World53/53、Kernel52/52，两轴0。父验收不关闭，acceptance-matrix.md列明三组剩余工作，下一步优先Control.State/Plan.Versions的真实增长与核心最新/变化资格；其后World基线、Coverage独立交付及附件保护。不能用局部读成本修正替代父A4/A5/A6。

Control/Plan增长片：实际Driver读操作场景分别测单plan多候选、distinct planId及同planId增版本；16/64/128规模记录Owner原记录、文件/元字段量、managed调用与完整reader成本/原身份绑定。先测量再固定需要版本增量/周期基线的范围，保留不可变Graph/输入/结果事实和严格恢复语义；不为接口形状预造统一查询层。

增长片完成：9个原样本+3个128前后对照+1个最终元数据样本；Plan已确认latest查询缓存修正，Host28/Kernel167，评审最终0，恢复入口evidence/control-plan/README.md。下一片先Control State及输入/结果关联的持久化可逆增量/周期基线，再按不同ID表增长接Plan Versions；物理格式与冷查询尚未由运行内序号缓存完成，不提前关闭A4。
