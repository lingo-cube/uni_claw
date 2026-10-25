# Slice 1 — 正式执行与辅助观察/产物隔离

本片实现并验证观察者异常/等待与Host辅助产物写入的隔离；不是RUNTIME-STORAGE-002全部完成。核心最新＋正反增量、可靠报告交付、必要来源保护及历史清理仍待后续切片。没有真机/模型、自动续跑、掉电或跨节点证明。

## 原问题与实际修改
- 原`KernelRunDriver.ConsultAgentV2Async`把正式Agent调用和咨询导出放在同一try，导出IOException进入正式P25 Reject；遍历恢复观察者也直接等待回调。
- 新`DiagnosticObservations`为一个Driver提供有界队列/单消费者和独立错误、丢弃、超时、未决关闭状态；正式Drive仅入队，不等待观察回调。Host正常关闭时显式flush/异步释放Driver，未完成回调继续受持有/异常观察，不冒充成功关闭。
- 两正式Host共用`AuxiliaryArtifacts`，咨询/遍历回调和收尾可选产物共用字节/总量/等待配额。先受限UTF-8编码，再异步写临时文件并发布；失败/超限写入facts.diagnostics，不污染原任务结果。关键facts/coverage结果不使用可丢writer。
- 已有同步观察消费者改成显式等待独立观察关闭；白名单和对应维度README登记新公开形状/文件。用户已确认的Owner/确认屏障/Effect语义保持。
- 拥塞补例用同一有效Defer/Blocked序列进行两臂比较，Capacity=1时允许诊断丢弃，但三条正式P25均存在、Completed且消耗/顺序一致；测试会释放并关闭观察者。

## 证据与四元组
| 条目 | method | expected | actual / evidence |
|---|---|---|---|
| 咨询原入口RED | 相同公开Driver测试，独立入口副本禁用/抛错observer两臂 | 有效咨询均Completed；诊断不能Reject | 原入口抛错臂为Rejected，候选同测试通过；`luna-red/baseline-red-v2.trx`、`candidate-green-v2.trx`，固定RED测试在`red-source/` |
| Host原入口RED | 离线Coverage Host中创建trace.json同名目录 | 正式结果与基线相同，可选输出独立失败 | 原入口收尾抛UnauthorizedAccessException；`host-red/host-red-20261025-01.trx`；候选失败与超限两例通过 |
| 异常、停滞与关闭 | 公开Driver/正式P25 journal，TCS固定观察者未结束窗口 | Drive先返回，P25不变；关闭报告失败/Unsettled | `AuxiliaryObservationIsolationTests`及纯候选TRX；不是仅编译或私有helper测试 |
| 产物失败/超限 | 同一Host公开入口，目录冲突或极低字节限额 | 结果/覆盖逻辑/投递保持，核心facts/report存在，exports记录失败或Dropped | `AuxiliaryArtifactIsolationTests`及纯候选Host TRX |
| 相关原行为 | 资源初始化/关闭/取消、导航等待验证及公开面守卫 | 原提交屏障和零重复投递不变，新公开类型正确登记 | 纯候选Kernel/Host定向验证，命令和指纹见`pure-verification/verification.md`；最终绑定见后续版本manifest |

## 评审与失败尝试
- Standards v1和Spec v1指出运行中回调绕过字节额度：改为同一受限writer。
- Spec v2指出先建全量JSON再限制字节：改用先拒绝超额sizeHint的有限IBufferWriter，避免额外无界JSON string。
- Spec v3发现其他任务并发加入Act-scope/failed-step业务hunks：未撤销共享文件，将其单列`parallel-kernel-driver-v1.patch`，从纯候选中排除。Standards/Spec v4生产均0；拥塞test-only增量v5补强条数断言后，v6两轴均无剩余项。本片不评判或取得这些并发业务改动的所有权。
- 第一轮咨询红例日志被worker后续重跑覆盖，`diagnosis-unverified-v1.json`与原日志不作有效RED；隔离原入口重新取得`baseline-red-v2`并更新有效`diagnosis.packet.json`。编译/白名单排序等失败尝试均保留，不当作行为回归或通过。

## 扩大回归的边界
- 共享混合工作区一次Kernel全量1300/1303；三个失败在独立原入口的相同三项过滤器全部复现，分别是ClaimEvolution顺序、RunTrace空值和四项旧README登记（`baseline-regression/kernel-entry-failures-v1.trx`）。
- 共享混合工作区一次DSH全量86/87；原入口与候选同一13-case过滤器均12/13，唯一partial覆盖验证断言一致（`baseline-regression/overlay-entry-v1.trx`、`overlay-candidate-v1.trx`）。不能复用上片24/24代表当前DSH。
- 共享混合工作区一次Host全量915通过/13失败/1跳过：12项先被旧fixture的Ledger摘要门拒绝，另1项是WorldOperationReplay随机OwnerId整份JSON比较。Ledger当前SHA与本change立案入口相同，未由本片修改；这是入口指纹/fixture门检查，不声称已独立运行复现这13项。没有改fixture门或旧证据让其通过。
- 共享源存在并发Act语义；纯候选的独立构建/定向结果才用于本片验收，不将混合工作区全量结果冒充纯候选全量通过。

## 恢复与后续
- 最终`candidate-v6.json/patch`及副本固定13个本片文件；Kernel纯候选25/25，Host纯候选12/12（生产/Host测试输入自v4未变，复用原Host结果）；见`verification-v6.json`与`pure-verification/`。新增6例，不声称父范围全通过。
- `pure-build-inputs.tar.gz`和`build-binding.json`保存v4完整构建输入；`build-binding-v6.json`只增加最终测试overlay，985输入/26程序集绑定，临时目录失效不影响输入留存，不再次复制整个源归档。
- `python3 changes/RUNTIME-STORAGE-002/evidence/slice1/verify.py`只读校验候选、输入归档/overlay、TRX与封存摘要；`reproduce.py <全新目录>`从封存输入恢复并重复公开定向场景。verify已实际运行，reproduce仅做语法检查，不冒充已再次执行。
- 所有slice1实现均未提交/推送。下一片建立必交付报告要求/来源/状态的可靠记录及公开查询/幂等重试，再推进核心版本快照与辅助历史优化。
