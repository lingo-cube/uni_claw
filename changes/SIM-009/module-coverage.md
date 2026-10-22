# SIM-009 模块覆盖矩阵

下表记录可定位的契约入口、已有测试入口及本轮回放交叉覆盖。测试文件存在只说明入口存在；执行结果见末尾与本线 evidence；文件存在不代表所有契约完备。

| 模块 | 公开契约 / 导览 | 现有测试入口 | 新回放交叉覆盖 | 未验证边界 |
|---|---|---|---|---|
| Evidence | `src/UniClaw.Kernel/Evidence/README.md`：证据账本与 provenance | `tests/UniClaw.Kernel.Tests/Evidence/EvidenceOwnershipTests.cs`、`tests/UniClaw.Kernel.Tests/Runtime/PageObservationExecutionTests.cs` | `PageIdentityReplayTests.cs` 经真实账本回放 identity 输入；`OperationReplayRegressionTests.cs` 检查 owner graph 输入 | 账本持久化、并发写入及跨进程恢复 |
| World | `src/UniClaw.Kernel/World/README.md`：World belief 与投影 | `tests/UniClaw.Kernel.Tests/World/PageObservationRecoveryTests.cs`、`tests/UniClaw.Kernel.Tests/World/WorldIdentityReconciliationTests.cs` | 两条回放分别覆盖 identity 恢复与操作后 World 投影 | 多设备及真实页面变化 |
| Run | `src/UniClaw.Kernel/Run/README.md`：Run 状态与归属 | `tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverTests.cs` | 两条回放通过 KernelRunDriver 观察运行状态、效果及恢复 | 长时间运行、进程崩溃后的续跑 |
| Control | `src/UniClaw.Kernel/Control/README.md`：控制决策边界 | `tests/UniClaw.Kernel.Tests/Runtime/ObservationRecoveryControlTests.cs` | 身份回放覆盖暂停后输入及续行；操作回放覆盖有限串行动作路径 | 复杂策略组合与真实 Agent 决策质量 |
| Assurance | `src/UniClaw.Kernel/Assurance/README.md`：效果验证与授权 | `tests/UniClaw.Kernel.Tests/Runtime/PageObservationExecutionTests.cs` | identity 回放核对错误补证不产生后验验证、正确补证后恢复 | 真实设备 assurance 判定 |
| Effect | `src/UniClaw.Kernel/Effects/README.md`：效果边界与投递 | `tests/UniClaw.Kernel.Tests/EffectBoundaryExecutionSourceTests.cs` | 两回放核对效果/receipt 与不重复 dispatch | 外部副作用系统及投递故障恢复 |
| Runtime | `src/UniClaw.Kernel/Runtime/README.md`：KernelRunDriver 运行期 | `tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverTests.cs` | 两回放使用真实产品运行链 | 真实时钟、取消竞争及设备时序 |
| Perception | `src/UniClaw.Kernel/Perception/README.md`：感知契约 | `tests/UniClaw.Kernel.Tests/Perception/TextFastBasisTests.cs`、`tests/UniClaw.Kernel.Tests/Perception/SlowReplayRealizationTests.cs` | 回放消费 fixture capture / identity，不验证模型输出 | 真模型识别质量及复杂页面泛化 |
| Agent | `src/UniClaw.Agent/README.md`：Agent/Profile 契约 | `tests/UniClaw.Agent.Tests/GoalEvaluationTests.cs` | 回放采用确定性决策替身并观察咨询/continuation 时序 | 真实模型规划质量、供应商与提示词影响 |
| Host | `src/UniClaw.Host/README.md`、`src/UniClaw.Host.Dsh/README.md`：Host 装配入口 | `tests/UniClaw.Kernel.Tests/ProductHostClosureTests.cs` | 无直接 Host 交叉覆盖 | 生产 Host、设备配置及服务联调 |
| Core | `src/UniClaw.Core/README.md`：跨模块语义投影 | `tests/UniClaw.Core.Tests/CoreObjectRelationTests.cs` | 回放未直接验证 Core 投影 | Kernel 到 Core 的端到端语义投影 |
| Trace / Diagnostics | `src/UniClaw.Kernel/Trace/README.md`、`src/UniClaw.Kernel/Diagnostics/README.md`：非权威轨迹与诊断 | `tests/UniClaw.Kernel.Tests/RunTraceBulletTests.cs` | 回放检查点可输出诊断材料；不作为权威状态来源 | 诊断完整性、生产遥测与长期保留 |

## 执行结果

Leader 本轮已在独占源码快照执行八工程，共 2038/2038 通过（Simulation 263、Host 411、Host.Dsh 56、Kernel 1037、Agent 36、Agent.Dsh 212、Core 14、FileSystem 9）。详见 `evidence/sim-009/module-results.json` 及其逐项日志/TRX。

其中新增回放回归 11 项，既有 Host 录制/重建/隔离相关 20 项均通过，包含既有录制 XML 经操作链回放。31 条场景经新鲜 TRX 完成 schema/映射/状态核对；20 certified + 11 descriptive，新增三条均属于后者。

现有 `tools/verify-change --scope full` 实码只枚举 7 工程，漏 Host.Dsh，与 tests/README.md 的 8 工程说明不符。本线 `reproduce.sh` 显式补跑该项目；未改共享工具。原 full 首轮失败日志保留：Host 9 项因临时快照漏带 25 个既有 XML；新 SCN-REPLAY-003 Theory 带空格参数名不被既有覆盖工具识别，已改为无参数 Fact 内逐一断言三子例。补齐输入后分别复验，未改产品或旧期望。

以上证明本次实际执行范围内通过，不声称每个模块契约已穷尽。B1 真模型决策质量、B2 任意分叉接棒、C 设备保真、多容器/新未知清单语义继续独立立项；本线不代替 World 线决定语义。
