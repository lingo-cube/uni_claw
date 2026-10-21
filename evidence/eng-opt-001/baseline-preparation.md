# 集成基线准备（隔离候选，2026-10-21）

- 工作区：`/private/tmp/uniclaw-eng-opt-001-review`；入口源码/测试快照上准备候选修复，未覆盖主工作区产品代码，也未包含World会话随后追加的high接线。
- 原治理RED：两份源码归属README漏项、一份分析文档缺状态/权威头部、DSH codec consumer放入Simulation导致额外Agent.Dsh依赖。
- 候选修复：补登记/元数据；协议消费测试移至Host.Dsh.Tests，由它单向引用Simulation.Tests，后者仅通过测试程序集friend提供现有fixture；Simulation保持仅Kernel/Agent产品依赖。独立审查确认无环，修正一处当前文档链接。
- 扩展检查发现：wire测试固定旧prompt hash导致握手拒绝；default绑定测试仍断言旧deepseekFlash但所有者已切commandcodeFlash。只修改测试fixture和当前声明的断言，不修改产品默认值/握手校验；两份涉及测试的候选变更待当前会话追加测试完成后以局部patch合入，不能用旧文件覆盖。
- 快照缺样本/包还原曾失败，原输出保留；按原始文件补齐，未合成或改写输入以凑通过。场景源哈希因当前源码变化失效，隔离快照经唯一工具重认证20/28块，未改场景预期，未写主工作区scenarios。

## Review与Verify
独立Standards核对测试归属、依赖方向、发现数量，无阻塞；文档失链已修正。追加fixture修复已由独立Standards/Spec复核，均0阻塞：仅正向wire fixture跟随合法prompt更新，实际工件漂移负向执法仍保留；实际默认绑定的明确断言未放宽。最新局部patch为baseline-local-only.patch，保留World新增high测试。

- 治理检查8/8、DSH计划本地确定性消费2/2、适配/工件检查26/26。
- 完整solution：8项目2005/2005（1026+406+252+206+56+36+14+9）。运行命令及每项目TRX见baseline-full-final.txt、baseline-full-final/results.json。
- 这只证明冻结候选快照的离线.NET范围；3个真实模型消费测试默认条件跳过其内部执行，绝不以Passed外壳宣称真实模型复验。没有设备、模型、插件JS或Workspace验证结论。
- 候选源码指纹：baseline-candidate-source-hashes.json；过程RED/补样本/独立Review输出均保留。

## 下一步
待WORLD-OBSERVATION-001结束共享写入后，按当前源码逐段合入上述最小修复，保留其新增high请求/断言；再以稳定当前源重认证和验证。隔离结果不冒充最新工作区全绿。
