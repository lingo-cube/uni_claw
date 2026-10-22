# SIM-009 — 场景、模块与错误回放回归

## 做了什么
把身份补证和操作空间关系接到数据驱动回放；新增三条专有场景，复用现有场景映射与八工程模块测试。输入驱动真实产品 owner，独立期望用于事后比较。没有修改 src、旧测试期望或 World 线负责测试。

## 原来的问题
现有 World 双跑能证明确定性，但不能独立证明语义。身份补证已有产品专项测试，场景库缺对应登记；WRC-003 操作链已存在，空间反例主要停留在 owner 单元测试。

本轮实现中，回放比较器最初只数导航边，不能检出数量正确但端点、方向或类型错误；独立评审定位后，以三个变异反例实跑 RED，再补检查并重放 GREEN。

## 这次修改
- `tests/UniClaw.Simulation.Tests/PageIdentityReplayTests.cs`：JSON 输入→KernelRunDriver→EvidenceLedger→World→Assurance；依次回放缺身份、错 capture、匹配补证。核对错 identity 虽通过完整性入账仍不能授权；恢复 traversal 时不重复 dispatch，旧 revision 不变。双跑仅作确定性旁证。
- `OperationReplayRegressionTests.cs`：固定语义 capture/剧本动作经实际运行链生成导航边、滚动关系与回执；重建只累计产品保存的偏移。独立期望包含两条导航的标题/方向、关系类型、revision/container 一致性、±0.2 位移、Alpha=.4/Beta=.6 内容坐标、效果和验证数量。
- `Fixtures/Replay/operations.json`：正反滚动、重排、嵌套滚动区、重复锚四组固定输入。后三组保留关系而偏移未知，输出内容为空。它们是合成输入，不是新设备录制。
- `Fixtures/Replay/page-identity.json`：输入与检查点期望分离；expected 不参与产品输入构造。
- `scenarios/SCN-REPLAY-001/002/003.json`：现有 schema 的 `execution.kind=none` 专有载体；详细期望由测试消费，不伪称 golden-bundle 认证。映射通过既有 Trait/coverage 发现。
- `changes/SIM-009/module-coverage.md`：逐模块入口、新回放交叉覆盖、执行结果与未验证边界。
- `reproduce.sh`：专项回放、既有录制 XML 回放、漏列的 Host.Dsh 工程、现有 full 验证入口。

## 验证结果

| method | expected | actual | evidence |
|---|---|---|---|
| 独立 oracle 初始 RED | 错位移/漏边/错内容/缺验证均被测试检出 | 初始检查器空实现时四变异断言 RED；另四失败定位为 fixture 补证配额不足 | operation-oracle-red.trx、operation-diagnostic.trx |
| 错配额的复现→修正后回放 | 同帧/动作/期望，仅修正 fixture 配额，返回边恢复 | 4 次补证配额→VerificationFailed/1 条导航；明确 8 次→2 条导航、2 条滚动、4 回执、4 后验验证；两分支均保留为回归 | ReplayFixture_InsufficientRecoveryBudget_ReproducesFailure_BeforeBudgetCorrection；output/operations-translation-budget4.json 与 operations-translation.json |
| 独立审查后导航内容 RED→GREEN | 数量相同的错端点、类型、方向也能检出 | 三变异先 3 RED/4 GREEN，修正比较器后七变异全通过；报告定位 navigation[0] | navigation-oracle-red.trx；simulation-reviewed.trx；output/oracle-navigation-*.json |
| 身份恢复回放 | 暂停→错 capture 仍暂停→匹配证据后恢复，效果仍 1 | 通过；账本接纳不等于身份授权；旧 revision 不变，深度 0→1 | output/page-identity-replay.json |
| 模块测试 | 八个 .NET 测试工程实际执行并通过 | 2038/2038，按最终有效执行汇总；包含新增 11 回放回归与既有 Host 20 项录制/重建相关测试 | module-results.json；full.txt；simulation-reviewed.trx；host-final.trx；host-dsh-final.trx |
| 场景认证/覆盖 | 新条目有实际载体，旧 golden 不变 | 31/31 状态/映射通过；20 certified + 11 descriptive，原 20 期望/认证未改 | coverage-reviewed.txt；full.txt 的 CERTIFICATION |
| 工作树隔离 | 不覆盖并行更改；测试快照与交付源码一致 | 负责路径源码逐一 hash 一致；外部文档变动单列，未回退 | frozen-source.json；protected-files-check.json；entry/ |

这些是本轮执行证据。初始 full 的失败不被改写为 PASS；完成状态由有效的局部复验与未变化模块的执行结果合并证明，不声称最后又完整重跑了整条脚本。

## 新发现与限制
1. `tools/verify-change` 的 full 实际只枚举七工程，遗漏 Host.Dsh；本线脚本显式补跑 56/56，未修改共享工具。建议其负责线修正列表与说明的一致性。
2. 临时快照最初排除了 evidence，漏带旧测试依赖的 25 个录制 XML，导致 Host 9 失败；只补拷原文件后 Host 411/411，未改算法或旧断言。输入路径清单见 copied-legacy-fixtures.json。
3. 覆盖工具的测试名正则不支持带空格参数的 Theory 展示名；本线用无参数场景入口内循环三子例适配，不扩写共享工具。
4. 修正的是本线回放 fixture 配额、剧本结束响应和检查器漏检；本轮没有产品代码修复。旧 WRC-003 产品修复只作为既有回归，未重开。
5. 未运行设备或真实模型；不证明任意长页复原、任意新决策的下游后果、视觉相似度或感知模型质量。没有把未知位移变成内容坐标。既有录制输入的回放不是当前设备事实。

## 下一步
在独占源码快照运行 `evidence/sim-009/reproduce.sh <快照路径>`；新出现的实际回放错误应先追加固定输入与独立检查点，保留 RED，再由对应产品负责线修复并重放同一用例。模块缺口按矩阵扩展，新多容器/未知清单语义由 World 线裁决。
