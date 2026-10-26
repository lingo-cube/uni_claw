# 任务资产盘点、保护与显式退役

`tools/test-assets` 新增两个入口：`plan-delete-task CHANGE [--out FILE]`只读盘点，`delete-task-assets CHANGE --plan FILE`执行仍有效的计划。编排在`tools/test_asset_task_cleanup.py`，引用投影在`tools/test_asset_task_references.py`。各Run按自己的旧binding组合父根和分类路径，当前配置改变不影响旧资产定位。

原先只有归档工作副本释放，无法显式退役任务所属资产。这次把盘点、外部引用/基线保护、同任务依赖传播、删除前复核及逐文件回执接通。state/index和原Run记录保留；任务资产开始退役后禁止新建或复活，保留的Run仍可读取。部分失败保留未知文件并记pending，新计划可继续，删除计数不重复。

## 验证四元组

| method | expected | actual | evidence |
|---|---|---|---|
| 既有资产/入口契约加新增退役契约 | 原行为保持，保护/漂移/重试/锁及旧绑定可判定 | 95项通过；源码稳定、消费者完成 | regression-v4-result.json及其Run的logs/tests.txt、results/source-binding.json |
| 真实子进程CLI，创建隔离自有任务后盘点再删除 | 删除允许项，基线/外部引用/未知材料保留，拒绝退役读取 | 删除5文件、3,644逻辑字节；7个保护文件SHA不变；保留读取成功、退役读取拒绝；原Run事实不变 | actual-cli-v4-result.json及其Run的logs/plan.stdout.json、delete.stdout.json、retained-read.stdout.json、retired-read.stderr.txt |
| 原清单前缀比较、逐Run无恢复校验 | 原Run/归档/baseline记录不重写，必要证据可读 | LAYOUT+AUTOMATION共14Run、246登记文件、4归档可用 | verification.json、verify_final.py |
| 独立双轴审查固定候选 | 范围/规范无剩余问题 | Spec 0，Standards 0；11项源SHA一致 | candidate-v3.json/.patch、review-spec-v3.md、review-standards-v3.md |

## 评审修复及证据说明

- v1 Spec的计划阶段未知文件finding已撤回；不同的执行中新增未知文件缺口以公开回归修复，保持pending而非误报完整退役。当前任务的保留计划/交接也参与引用检查，repo-owned旧材料支持跨文档相对引用。见adjudication-v1.md、review-red-v1.txt和review-green-v1.txt。
- 回执补齐external/baselines，runBindings继续绑定保留的原记录。诊断说明中嵌入的本机路径也投影到逻辑标识。见diagnostic-portability-red.txt/green.txt与v3复审。
- regression-v1是验证器导入路径错误；actual-cli-v1是隔离验证器缺少scenario_certify副本，均保留失败说明与完成回执，不冒充产品失败或成功。v2及以后均使用新Run、新证据文件，未覆盖旧记录。
- before/仅保存入口时实际改动文件；候选patch、紧凑RED/GREEN日志、原始结果和失败诊断保留为本次必要证据。未追认既有未知临时目录的归属。受控CLI每轮消费结束后删除10份临时工具副本，隔离测试自身临时目录由上下文清理。

本轮未选择任何真实历史任务删除；自动GC、批量存量迁移、产品数据和聊天删除不属于此次范围。证据等级为本地CONTRACT/DETERMINISTIC/SCENARIO，无设备或模型实测声明。先前产品存储及World/Coverage工作已在2703244f统一发布，不重复开启。
