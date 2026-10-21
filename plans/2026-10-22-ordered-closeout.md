# 旧任务有序推进（2026-10-22）

用户授权：有序推进；Leader承接，不覆盖并行工作。所有者随后追加“完成后提交推送”：并行任务1/2交回、整合验收及修改归属核对完成后执行；不把未完成/无关改动混入提交。各Change仍是各自WHAT/Acceptance唯一真相，本计划不创建第二套状态系统。

1. [x] 文档与证据（ARCH-DOC-024/025、Settings004已完成）：修正ARCH-DOC-024主题/证据链接；ARCH-DOC-025明确历史审查与后续buyer复核的替代关系；独立Review、元数据/链接核验后按文档范围收口。核对SETTINGS-COVERAGE-004输出工件与旧阻塞。
2. [x] 工作台（原会话已完成整合/实际DSH167项及浏览器验收，Leader只读复核）：先等待正在活动的UI会话交接，比较已验证layout补丁与当前源，保存入口指纹后最小整合；构建、测试、浏览器与实际profile挂载验证，禁止覆盖并行实现。
3. [ ] 轨迹与停滞：按TRAJECTORY-001、AGT-019、RUN-006原Acceptance核对现有证据；复验3081、视觉服务及注册设备环境；缺环境如实标记，不用模拟替代真实送达。
4. [ ] Settings覆盖：承接World补证和覆盖恢复后进行有界验收；完整前沿/剩余项需有证据处置，单点恢复不得替代全树。
5. [ ] JEV与Plan：图像消费先证实，再补配对实验；Plan泛化收益按原门槛复核，不能因局部通道成功关闭。
6. [ ] 第二轮结构优化：沿真实变更点局部整理Driver/HTTP/目录；历史路径迁移先解决兼容，不新增想象中的平台。

入口：HEAD bce0ec3a；共享树状态与920文件指纹、文档快照见evidence/ordered-closeout-20261022/。工作台会话01a15ee5-94b3-72a1-9a81-8d2f965165b2仍active，当前UI范围仅只读检查。

- 2026-10-22：ARCH-DOC-024/025双轴评审、元数据4项、链接/主题锚点核验完成；按文档范围closed，未实施功能不受此状态影响。设备已按注册启动器READY，3081已起，正在验证正式鉴权链路；工作台仍由原活动会话处理。

- 2026-10-22 Owner已将GLM并行任务1/2交出：GLM1独占docs/analysis/README.md、docs/design/README.md及evidence/glm-doc-index-20261022/；GLM2仅写evidence/glm-runtime-http-review-20261022/、RuntimeHttpServer源码只读。Leader不再写这两个索引或这些证据目录；任务3/4未派发。

- 2026-10-22：工作台原Owner已关闭LAYOUT/INTEGRATION两任务，实际DSH167项及浏览器/安装client校验由原会话提供，3082预览保留；Leader未重复合补丁。
- 2026-10-22：设备首轮3receipt/2verified、3真实咨询，page-identity-observation-exhausted停止，未出现自然Heartbeat/CasePacket，不通过H7/全树。暴露归属记账缺陷已在SETTINGS-COVERAGE-007按公开Runner RED→GREEN修复并关闭；Fix connectivity被当导航的分类问题独立保留，未继续点击或临时放宽。
- 2026-10-22：GLM任务1/2已交付并由既有并行提交86c1295f/a23fe67d推送；工作台最终c612e350已关闭STEPS。Leader核对来源后仅发布已完成范围。

本轮具体修改/验收/剩余问题见evidence/ordered-closeout-20261022/report.md；下一次动态树执行之前先收敛navigation-classification-gap.md中的反例，当前不以原始tap成功冒充安全导航。

- 2026-10-22 发布验收：隔离候选8项目2027/2027（首次全套七项目+补齐协议JSON后Kernel1037/1037分开记录）、插件51/51、场景认证28份0违规、协议生成与图审计通过。原始现场运行记录保留本地，不进入发布；导航分类、H7和全树等7个任务继续开放。
