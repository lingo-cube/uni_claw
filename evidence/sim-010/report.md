# SIM-010 录制导入与错误回放

## 做了什么
新增两个录制操作回放场景，把旧 Network 导航和 Stay awake 开关的原文导入、缺帧等待、错误重放和恢复接到实际 Kernel 所有者链。当前八工程全量 2046/2046、场景 33/33；本切片不等于所有仿真、模型和真件缺口已完成。

## 原来的问题
初版假设“未变post最终应是VerificationFailed”和“checked=false证明Unchecked”均被实跑推翻：通用tap仅校验目标存在；最终状态经NoAction转为TerminalNotProven；旧布尔XML无法排除Partial，正确行为是Unknown→no-checked-claim拒绝。最初专项3失败/3通过及编译失败日志均保留。

直接把Host引用加到Simulation也被工程闭包拒绝。最终保留独立composition root，没有修改闭包断言或产品源码。

## 这次修改
- `RecordedHierarchyImportTests.cs`：Host侧逐一校验原XML与独立manifest哈希，真实parser/projector/Host occurrence adapter导出结果与六份冻结资产完全一致。
- `Fixtures/Replay/Imported`：只含typed observation、未铸canonical ID的occurrence候选及最后字段subject，不含World/Slice/关系/receipt/expected。明确虚拟capture时钟和page context，不冒充真实采集元数据。
- `RecordedOperationReplayTests.cs`：Simulation仅引用Kernel+Agent；测试adapter等整capture字段均被Ledger接纳后释放候选并带当次basis；产品World铸ID/建关系，再走真实grounding→Effect→Assurance。Agent和外部delivery是确定性替身；不是Simulation现场运行Host adapter。
- manifest将expectedBehavior与desiredOutcome分开固定：旧开关必须安全拒绝，业务关闭证明仍缺；网络错误重放的单步verified不会遮掉缺导航边。
- 场景004/005为专有载体、描述性期望，不冒充golden认证。原有期望、认证和产品源码未改。

## 验证结果

| method | expected | actual | evidence |
|---|---|---|---|
| 原版录制链专项 | 暴露错误假设而不改实际输入追绿 | 最初3失败/3通过；Unknown及tap缺口明确定位 | recorded-behavior.trx、recorded-diagnostic.trx、decisions.md |
| 修订后专项 | 等待不重投递，同Run续行；导航独立分歧；Unknown拒绝；篡改/验证结果变异能检出 | 6/6 PASS | recorded-import-v3.trx、output/final |
| 独立Host导入一致性 | 两序列的三种输入均与原XML经产品adapter结果完全一致 | 2/2 PASS（内部逐帧校验6份） | import-host.trx |
| 当前源码独占快照full | 八工程一次、Simulation一次，闭包及旧回归保持 | 2046/2046；Simulation269、Host413、Host.Dsh56、Kernel1037、Agent36、Agent.Dsh212、Core14、FileSystem9 | full.txt、module-results.json、final-snapshot.json、simulation-full.trx |
| schema/认证/二进制Trait/TRX | 新条目真实被发现且全部通过 | 33/33；20 certified＋13 descriptive | full.txt、resolved-config.json |
| 独立Review | Standards/Spec分开，只读审查，不以测试替代 | 两项独立评审无阻塞，限定范围见报告；闭包与DLL另验 | standards-review.md、spec-review.md、runtime-artifact-check.json |
| 来源及保护检查 | 被验证可执行输入与当前一致，已有文件变动不接管 | 可执行输入零漂移；Change文字/status随收口变化；保护5310项，发现4项外线文档变化及2项已授权工具前置变化，均保留 | snapshot-source-comparison.json、protected-files-check.json |

full数字不重复计入前面的architecture子集。原始输入hash可核对，但历史录制不证明真实性签名或当前设备状态。最初diagnostic JSON同名覆盖的证据限制见design-prerequisite-review.md；原失败TRX/日志与原XML均保留，最终输出独立目录。

## 新发现
补采前置设计已完成并返回24条独立期望（未实跑）：proof内容校验、域范围、checkable消费guard和XML三态适配均需最小前置切片。旧录制继续Unknown拒绝，不能为完成关闭目标伪造ExactProof。

通用tap目的地符合性、World新未知/多容器、B1真实模型输入还原与质量、B2任意动作分叉、C设备保真仍未完成，详见changes/SIM-010/dependencies.md。新模块断言见module-coverage.md；其余模块只作本轮回归，不宣称穷尽。

## 下一步
用户已选择先做受控仿真正反例、不启设备；下一切片SIM-011继续推进；真实Stay awake证明须先解决限定域proof与checkable guard。当前不启设备、真实模型，不提交推送。两个前置会话均已等待到完成，工具前置复核通过，补采设计作为待裁决输入。
