# 剩余存储与测试资产治理的联合交接

最新分工见[work-allocation.md](work-allocation.md)：产品余项由原聊天继续；目标只接两个开发资产Change。以下首次联合交接的Owner分配以最新表为准，路径与保护契约继续适用。

接续任务：存储验收后的测试资产治理（local / 01a1730a-957c-7ab2-8d46-2574de5a93e9）。本轮用户明确要求把下一步交给该任务，与资产整理一起做，明确配置化及父路径/分类路径结合；并允许Leader反驳不合理安排。

## 本次指令与职责
同一Leader联合推进，分范围验收：RUNTIME-STORAGE-002继续承载产品存储剩余工作；测试资产自动化按UniFlow建立实际实现Change，引用TEST-ASSET-LIFECYCLE-001已完成规范，不重开或冒认其closed范围。共用一份依赖计划和路径组合约定，不把测试资产管理变成产品Owner或第二生命周期。

旧交接的“存储整体验收前只读等待”不再阻止本次明确交接的剩余存储、配置接线及新产物治理实施。迁移/归档/自动删除仍须逐片证明来源保护、引用可读与实际消费者完成，不能把用户的“一起做”解释为批量删除历史的授权。

## 已完成与剩余
1. 辅助观察隔离：slice1封存；报告交付：slice2首个run-report buyer；核心增量：slice3首个资源家族。恢复入口为state.md、plans/runtime-storage-002.md及各slice README/verify.py。最后纯候选Host181/181、DSH50/50、额外真实Kill2/2；这些不是父A1–A10整体验收或当前共享全量通过。
2. 产品下一步：按真实buyer核对Run、Control遍历、RuntimePlan等适用可变族的已有增量/版本/latest成本，已有能力直接验收，缺口才改；不可变事实不强制逆向。然后World/咨询等辅助基线、增量、缺口与保留保护，最后逐A1–A10核验。原Owner/来源确认/禁止业务重执行保持。
3. 开发资产：接通配置→路径→实际写入→test-assets.v1清单→报告/读取/消费者→收尾。先verify-change新运行，再重放、诊断及仿真出口；新链路验收后再盘点存量。失败/取消/重试不得覆盖历史。

## 配置约定（目标接口，当前尚未实现）
开发测试资产采用一份本地布局配置；建议CLI `--asset-config <file>`优先，其次环境变量`UNICLAW_TEST_ASSET_CONFIG`，最后明确的本地profile默认配置。只选择一份配置，不逐入口自行拼凑不同优先级。配置存在但非法必须报错；默认布局如启用须可查询，不静默落回仓库内大目录。具体配置文件落点由实现Leader核对本地profile约定后确定，避免新增第二配置真相源。

建议冻结的字段形状：
```yaml
version: 1
parentRoot: /absolute/local-test-assets
repositoryKey: uni-claw
workPath: work
archivePath: archives
categoryPaths:
  raw: raw
  logs: logs
  results: results
  scratch: scratch
```
字段名可在首片按真实buyer收敛；行为约束不能省略：
- parentRoot是本机绝对父根，开发工作/归档根在仓库外；repositoryKey是稳定命名空间，不只凭同名仓库目录猜唯一身份。
- workPath、archivePath、categoryPaths各值均为相对路径。分类路径不能用绝对值绕开父根；拒绝上跳/越界和符号链接逃逸，不允许工作/归档落点相互覆盖。Change/Run键也作为受校验的路径段。
- workRoot = parentRoot / workPath / repositoryKey；runRoot = workRoot / changeId / runs / runId；分类目录 = runRoot / categoryPaths[kind]。例如 raw=raw/captures 时，它仍在该次run内部。
- archiveRoot = parentRoot / archivePath / repositoryKey / changeId；具体归档另有独立标识，不把runId/目录名当产品Run身份。
- 只改parentRoot应统一改变新运行的布局基准，不隐式迁移既有数据；只改一个categoryPath应只影响新运行的相应分类。嵌套分类或冲突路径须明确归属与冲突行为，不能靠目录递归删除推断寿命。
- 同一解析结果贯通写端、读端、报告/UI引用、归档恢复和清理；禁止部分入口继续硬编码仓库evidence/TestResults。提供可查询的resolved配置，清单记录其digest及实际命名空间。
- assets.json使用逻辑根、Run相对文件路径、kind、摘要和保留策略，不把主机绝对路径当资产身份。历史Run保留当时布局版本/根绑定；后来改配置不能让旧引用漂移或指向新目录。不能读取/迁移的旧路径明确保留。
- kind分类与run/task保留策略分开；目录分类不构成删除许可。正式baseline沿既有manifest独立引用，不随Change/工作根删除。

产品路径配置仍由产品Host/profile负责，不导入Harness测试资产YAML，也不把核心结果/恢复源/必交付来源放入测试清理域。既有Host --runs、product/tasks/tool-runtime.yaml(base/subdir)、重放--out-dir须通过兼容接线统一其各自实际路径，不再造跨层通用存储平台。

## 首片具体动作与验收
- 先读现有live材料：changes/TEST-ASSET-LIFECYCLE-001/handoff.md、docs/agents/test-asset-retention.md；其中历史失败、回归修复/恢复教训只是当时事实，勿冒称已重跑。核对当前dirty与Owner，保留并行修改。
- 选verify-change作为开发资产首个真实buyer，落本地配置loader/路径解析与独立Run工作根、紧凑索引；同步识别产品剩余存储依赖，分片推进，不再纯等待父Change自动关闭。
- 验证父根改变/分类改变/嵌套分类组合、跨仓库与两次Run隔离、非法绝对分类/越界拒绝、写读与报告引用一致、旧布局仍可定位。配置digest不同开新验证批次，retryOf不覆盖旧Run。
- 所有成功/失败/取消出口先完成TRX/coverage/信号/packet消费，再保护必要证据并收尾。清理失败保留pending、不改变测试/产品结果；check-only入口不暗中变全仓删除器。
- 归档实际解包核验并验证原消费者可读，才按明确自有清单删源。活动/未知归属/未决/证明/待交付来源及被保留的基线均不得盲删；归属核对包含计划和WorkItem。存量迁移去重与.git维护分开，不操作.git。
- 简单盘点/机械测试交Luna；存储提交边界、路径归属和删除判断由Leader负责。必要验证一次到位，保存紧凑结果与有效失败，不重复复制整套源码/构建输出，不为封存反复重跑已通过的无关范围。

本次仅交接，不实现配置/迁移/清理，不提交推送、不调用设备/产品模型。接续Leader实际落地后按各范围验收，不把本文件当实现证明。
