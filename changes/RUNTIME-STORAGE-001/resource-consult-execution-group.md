# B组：资源账目、P25咨询与可靠执行（Slice 5）
状态：COMPLETE（E1–E5）。承接组A，父change仍implementing。使用已获授权的领域journal、EffectBoundary、Kernel/Driver和正式Host公开缝；无需重复征求常规测试许可。

## Decisions / Scope
- 核心继续依赖各领域抽象。资源initialize/begin/confirm、咨询reserve/complete/reject、执行prepare/submission/receipt及原反馈/关联操作沿原Owner真实await，旧同步入口显式桥接。
- 外部调用前原记录确认后才咨询/投递；接受后取消先结算、后续外部操作停止。准备提交三值与实际Receipt保留，失败锁存、不盲重试。同Owner一条在途，零收集窗口，不造线程/后台队列。
- 保留资源v2/v3/hash/head/源前缀、P25原JSONL及多态codec、执行原长度帧和重放限定。三种格式不强塞入组A的同格式文件模块。
- 实际批量buyer：delivery已经返回后，submission＋receipt两条原记录可共同写入/flush；prepare保持独立提交。最多2条及默认16MiB整批字节上限，不跨Owner或外部屏障。
- 身份归档的异步初始化和其他全域收尾留C组；本组不改变恢复游标、续跑、设备或模型行为。

## Acceptance（E1–E5）
| ID | method | expected |
|---|---|---|
| E1 | 公开Driver延迟资源initialize/begin/confirm及前/后提交故障 | 调用返回未完成Task；确认前零新咨询/投递、不发布binding/新序列；拒绝Drive重入，故障后不重试 |
| E2 | 延迟P25 reserve/complete/reject，取消/前后提交故障与旧幂等回放 | reserve未确认零咨询；结果未确认零采纳/投递；保持pending、次数和fingerprint语义，故障锁存 |
| E3 | 公开EffectBoundary→Kernel/Driver延迟prepare及结果确认、三值/取消/追加故障 | prepare未成功零delivery；Unknown/Failure不发送；结果失败仍返回实际Receipt、不盲补发；同Owner拒绝重入 |
| E4 | 三真实文件适配器原生async/关闭/字节边界、旧读回；真实Driver已提交未ack各Kill＋新reader | 原记录/身份/源SHA保持，未确认不推进；执行同场景原3帧，结果2→1批次、总flush3→2；正常关闭等待接受写入 |
| E5 | 固定候选/入口源码、前片342封存、最终相关回归、双轴review、四元组 | 新行为全部通过，无新增回归，明确旧失败及源归属；父change不误关闭 |

## Plan
按资源→P25→执行逐条RED/GREEN贯通，随后统一原生文件、Host关闭、真实Kill与计数。全组三份最终相关回归、一次双轴review，修订只复核增量。入口660构建输入、20预期候选路径及前片342封存已固定在evidence/slice5/entry.json；不覆盖并发代码或旧证据。

## Verification
level: DETERMINISTIC + SCENARIO
method: 上表E1–E5、三份最终TRX、真实Driver四窗口Kill/八新reader、managed计数、独立双轴review及verify。
expected: E1–E5。
actual: 新Kernel35/35、Host13/13；最终Kernel1284/1287、Host343/344、DSH24/24，仅原3+1失败。Driver准备等待取消RED为delivery1，贯通原token后GREEN为0。原三执行帧/1delivery/1receipt/1492字节保持，真实write/flush/批次3→2；最多2条/16MiB、窗口0。四实际Kill/八新reader原数据/SHA保持；20候选、663实际输入/26程序集绑定，646保护输入及前片342封存不变；Standards/Spec各0，verify exit0。身份归档计入总路径为8/9，父implementing，下一C组。
evidence: evidence/slice5/README.md、test-summary.json、review-summary.json、verification.json。
