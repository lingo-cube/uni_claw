# 受控状态回放与消费者修复

## 做了什么
把开关关闭/开启与9条拒绝路径接到公开产品运行链。回放实际找出一处产品缺陷，再由前置修复消费者，使用同一组11个状态输入/期望重放验证。父线最终八工程2064/2064、场景34/34，当前可执行输入与被验证快照零漂移。

## 原来的问题
checkable=false或缺失时，post unchecked已入账且时间合法，消费者却返回verified/StepVerified。首次11例为9 PASS/2 FAIL，首次分歧verification.isVerified。canonical post记录证明不是选错旧capture，也不是投影丢字段；resolver没有检查checkable。PER-009/010既有契约要求该属性为真才具有checked状态权威。

## 这次修改
- `ControlledCheckedReplayTests.cs`与`controlled-checked.json`：固定三态/未知/能力、时间和目标数量输入，独立固定verified/phase/rejection期望。真实KernelRunDriver→projector→Ledger→World→grounding→Effect→Assurance执行；测试adapter只提交非canonical候选，不读取Expected、不提前滤掉无效guard，World铸ID/绑basis。
- `SCN-REPLAY-006`：受控synthetic专有载体，最终TerminalNotProven、目标评估not-applicable；状态动作verified不冒充任务Completed。Source说明不声称人工逐项审阅、设备采集或校准。
- 产品前置`CHECKED-GUARD-001`：`SemanticCheckedResolver.cs`仅接受同capture+node的checkable=true；false/missing/malformed为Unknown，raw checked不抹除。Assurance只修说明。新增7个公开Kernel接口用例，补齐旧synthetic正例的显式guard，原断言及真实负例保留。
- 原20条golden经既有入口更新本产品修复的认证元数据；原场景body、输入、期望、两种digest均不变。旧录制不升级Exact，Stay awake仍no-checked-claim拒绝。

## 验证结果

| method | expected | actual | evidence |
|---|---|---|---|
| 原公开链固定11例 | 2正例通过、9负例拒绝，偏差可定位 | 修复前9/11；false/missing错误verified，路径明确 | controlled-first.trx、controlled-reproduced.trx、guard-diagnosis.json |
| 将修复前原始resolver字节放入隔离副本 | 可重复精确重现原错误 | 独立复现9 PASS/2 FAIL，失败正是两个guard负例 | reproduce-red.txt、before/SemanticCheckedResolver.cs.txt；`python3 evidence/sim-011/reproduce.py --red` |
| 原11输入/verified/phase期望对照 | 不通过改弱预期消除RED | 逐例完全相同；source文字纠正与拒绝原因收紧单独记录 | carrier-finalization.json |
| 修复后当前源码full | 同11例全绿，false/missing为VerificationFailed，raw仍保留 | 11/11；新增公开guard7/7；旧录制继续拒绝 | output/final、recorded-output/final、前置guard-public-red.trx/verification.json |
| 当前源码独占full八工程 | 所有工程一次，Simulation一次；不借旧快照结论 | 2064/2064，Simulation280、Kernel1044、Host413、Host.Dsh56、Agent36、Agent.Dsh212、Core14、FileSystem9 | full.txt、module-results.json、simulation-full.trx、final-snapshot.json |
| schema/认证/二进制Trait/新鲜TRX | 新场景与参数逐一真实发现 | 34/34，20 certified+14 descriptive；SCN006的11个完整展示名逐例Passed | controlled-discovery.json、resolved-config.json |
| 保护/固定输入/认证/构建artifact | 范围清楚，原输入不动，同一Runtime | 父线954项保护仅30项授权前置变动；固定载体/旧录制hash保持；20元数据-only；四份Kernel DLL hash一致 | protected-files-check.json、frozen-inputs.json、certification-parent-check.json、runtime-artifact-check.json |
| 独立Standards/Spec与Verify分开 | 无未处置阻塞，可执行输入与快照一致 | 两轴无阻塞；最终源码比较无可执行漂移 | standards-review.md、spec-review.md、review-disposition.md、snapshot-source-comparison.json |

full数字不重复计architecture子集，34场景没有混成34份golden认证。原始失败、旧fixture首轮失败及认证陈旧失败都保留在父线/前置evidence目录。Root的reproduce.py默认生成新独占副本跑当前full；--red只改一次性副本，不回退工作区。

## 新发现
产品guard曾让30个旧手写case暴露缺失输入，前置逐调用点补synthetic有效性事实并保留断言，理由见evidence/checked-guard-001/fixture-rationale.md。Unsupported源在本链末端仍表现为无checked claim，细分诊断未改善；本次严格证明拒绝。

旧XML仍无法证明真实关闭；proof内容/范围/版本信任与真实三态通道、设备补采、取消/恢复、B1真实Agent输入和质量、B2任意动作分叉仍未在本轮实现，见SIM010/dependencies.md与REPLAY-STATE-EVIDENCE-001/design.md。11个受控例不等于设计中24条已实跑。

## 下一步
本轮受控仿真范围完成。真实Stay awake补采按用户选择另行安排，先确定受限域证明与具体设备/权限/预算；World未知/Agent可见性继续由原设计线收敛。本轮未启设备/真实模型，未提交推送。三个前置会话均已等待交付；工具修复与guard修复完成，补采设计保留为后续评审输入。
