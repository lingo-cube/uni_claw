# TEST-ASSET-LAYOUT-001 — 开发测试资产布局配置与真实路径接线
lifecycle_state: closed · disposition: none · depth: standard · base: a05176f3c66e0a3206f49dde463f51df1335975c

## Intent（WHAT/WHY）
将开发测试产物从分散硬编码路径接到可配置且按仓库/Change/Run隔离的布局；父路径与分类路径按同一规则组合，写端、读取、报告引用使用同一解析结果，旧Run不因配置改变失联。

## Scope / Out of Scope
首个buyer为verify-change，完成本地配置选择/解析、resolved查询、独立Run目录、真实TRX/log/result/coverage/信号读写及旧布局定位。接续任务为local/01a1730a-957c-7ab2-8d46-2574de5a93e9（存储验收后的测试资产治理）。
不承担产品核心/恢复/必交付来源，不实现自动删除、归档压缩、存量搬迁、完整test-assets.v1治理；这些归TEST-ASSET-AUTOMATION-001或后续明确范围。不操作.git，不提交推送，不调用设备/产品模型。

## Decisions
- CLI --asset-config > UNICLAW_TEST_ASSET_CONFIG > 明确本地profile默认，选择一份完整配置；非法配置报错，默认布局可查询，不静默回退仓库大目录。
- 配置parentRoot绝对、repositoryKey稳定，workPath/archivePath/categoryPaths为相对路径。workRoot=parentRoot/workPath/repositoryKey；runRoot=workRoot/changeId/runs/runId；分类=runRoot/categoryPaths[kind]；归档根=parentRoot/archivePath/repositoryKey/changeId。
- 分类不得用绝对值绕过父根，拒绝上跳、链接越界和路径冲突；支持raw/captures等合法嵌套分类值。路径分类不等于保留策略。
- 改父根或分类只改变后续新Run布局，不隐式搬迁旧数据；旧Run保存当时配置digest与可定位绑定。共享索引用逻辑根和相对路径，不用主机绝对路径充当身份。
- 产品profile不导入Harness开发资产配置；正式baseline沿既有manifest独立引用，不成为测试工作根的可删子树。

## Acceptance
| ID | method / expected |
|---|---|
| A1 | 配置入口证明CLI/env/profile优先级、非法失败与只读resolved查询；查询不启动测试 |
| A2 | 改父根、改分类、合法多级分类的组合正确；跨仓库/两Run隔离；绝对分类、上跳、链接逃逸与冲突拒绝 |
| A3 | 真实verify-change首个运行的写入、TRX/coverage/失败信号读取及结果引用一致，输出在配置落点；不能只建配置/目录便称完成 |
| A4 | 新配置下新运行使用新布局，旧Run按原绑定仍可定位；无覆写旧结果或隐式存量迁移 |
| A5 | 与产品数据/正式baseline隔离，原兼容入口可用；交付稳定解析/绑定契约供AUTOMATION消费 |

## Dependencies / Owner
与RUNTIME-STORAGE-002并行，产品保护契约不可削弱。AUTOMATION依赖本Change的布局解析与绑定；同一接续Leader可纵向分片，不能把未验收接口当已完成。

## Constraints
保留共享dirty和并行修改；首片从verify-change及tools测试开始。C#诊断路径接线先列清调用者归属/改动文件，不能扩大为产品保留Owner。必要验证与紧凑证据，避免反复整树拷贝/无关重测。

## Verification
level: CONTRACT + DETERMINISTIC + SCENARIO
method: 公开CLI/路径解析契约及真实verify-change运行、旧布局读取、独立评审。
expected: A1–A5；完整写读链路与隔离可核验。
actual: A1–A5布局首片已验收。配置/隔离/旧绑定/并发及原入口定向26例通过；self-test23/23。真实默认/嵌套类别两次quick各39文件校验有效，后次sourceStable=true；Kernel43/43、Host26/26，Simulation1失败及认证stale如实保留，不作为产品全绿。原coverage工具读取外部TRX，quick子集明确无法证明全库覆盖。
evidence: ../TEST-ASSET-AUTOMATION-001/evidence/slice1/README.md；其candidate.json/verify.py及双轴v3报告；assets.json；evidence/real-quick-final.txt；evidence/real-coverage-consumer.json；evidence/self-test-final.txt。

## Status log
- 2026-10-25 UNDERSTAND→RESOLVE→PERSIST→PLAN：用户要求分Change并合理分配；由接续任务负责开发布局，原产品Owner继续RUNTIME-STORAGE-002；完整资产治理另立AUTOMATION，不重开已closed的规范Change。

- 2026-10-25 PLAN→IMPLEMENT→REVIEW→VERIFY→CLOSED：完成tools/test_assets.py布局/immutable binding与verify-change实际路径接线、test-assets读取；并发半写/profile漂移RED→GREEN，真实SIGTERM输出保全及消费者错误分域通过。证据复用AUTOMATION slice1，不复制原材料。目录配置与新Run首个buyer闭合；归档/清理/其他入口不属于本Change。旧Run和正式baseline未迁移删除，临时支持文件处置见support-finalization.json。
