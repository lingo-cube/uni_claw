# C组：身份归档、初始化与统一收尾（Slice 6）
状态：COMPLETE（F1–F5）；父A1–A4已证明。承接组B，沿原领域存储/Owner/Driver和正式Host公开缝测试，不重复索取已授权许可。入口与前片封存先固定。

## Scope / Decisions
- IAsyncRunRecoveryArchive确认原身份记录后才发布ResourceBinding或咨询；保留原文件/schema/hash/只读恢复规则。接受后取消结算，故障不重试。
- Evidence/World文件新建的原seq0 marker、World Owner原initialize走异步factory，正式Host显式等待；保留旧同步构造及历史只读读取，不新增后台线程。
- completion verifier/adjudicator和cancel的原证据回流走ProcessBatchAsync；裁决异步入口与Drive共用串行门，确认前不进入终局。Host普通产物及现有观察回调异步等待，等级保持；Trace内存Finalize不伪造I/O。
- 九条生产存储路径、正常关闭与失败回流逐项检查。未使用跨操作收集队列，收集/准入窗口0，同Owner一条在途；Evidence/Control/执行合法批次保持各自数量/整批字节限制。无数据库、跨Owner事务、续跑或路径优化。
- 仅正式Run存储与两Runner普通输出；P24激活/任务/会话等Host控制面存储不是本change九条路径，不趁机改造。

## Acceptance
| ID | method | expected |
|---|---|---|
| F1 | 延迟/前后提交故障/取消的公开Archive→Driver，以及真实文件Kill/两个新reader | 归档未确认零咨询/投递、无ResourceBinding；失败锁存、取消先结算；原身份/历史/SHA保持，只读不授执行许可 |
| F2 | 文件async factories/World Owner公开CreateAsync延迟与故障、取消/旧读取 | 初始化完成前不返回可用Owner/来源；原seq0/seq1及v1/v2格式保持，正式Host无同步初始化写入 |
| F3 | Driver completion/cancel/adjudication延迟Evidence/World/Run确认及重入/故障 | 未ack不发布终态或推进下一动作，原证据内容与判定保持，裁决不绕Drive门 |
| F4 | 两正式Runner离线产物/关闭回归；九路径native计数/等待/峰值与既有三合法批次对照 | 保存/关闭都await，真实write/flush和记录数可核对；清楚区分marker、语义记录、native调用和观察时间，不承诺物理磁盘/掉电/吞吐提升 |
| F5 | 固定候选/输入、前片封存、最终相关回归、独立双轴review、父A1–A4映射及verify | 新行为全通过、无新增回归；同源旧失败如实保留；只有九路径/收尾与父验收全部证明后关闭 |

## Plan
归档RED/GREEN→初始化factory→回流/裁决→两Host输出等待→九路径计数与实际Kill→统一回归/双轴review/核验。原封存不覆盖；测试或核验失败保留，不因中间绿或路径计数9/9就宣称父change完成。

## Verification
level: DETERMINISTIC + SCENARIO
method: F1–F5、最终TRX/九源计数与原文件、归档实际Kill/两个新reader、两Host暂停消息循环回归、双轴review及独立verify。
expected: F1–F5及父A1–A4。
actual: 新Kernel13/13、Host8/8；Kernel1297/1300、Host356/357、DSH24/24，同源旧3+1失败。九路径23语义记录＋2 marker，frame write/flush各24、head各19；三合法批次减I/O、World增量收益保持。归档ack前真实Kill/两个新reader原身份/历史/SHA保持。同步桥上下文阻塞RED→GREEN；补另一Host的原启动失败/输出/关闭回归后Spec M1关闭。v3仅新增Host用例，Kernel/DSH同源码和程序集复用v2；Host全相关选择重跑。23候选/667实际输入/26程序集绑定，644保护输入/3并行输入单列，1456原封存逐SHA保持；两轴0、verify exit0。
evidence: evidence/slice6/README.md、candidate-verification-binding-v3.json、test-summary-v3.json、review-summary.json、verification.json。
