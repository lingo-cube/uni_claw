# World身份修复与语义消费交付

日期：2026-10-21。状态：本范围verified，源码已冻结；共同验收由JEV-SEMANTIC-CAP-001主会话收尾。本报告只说明本会话增量，进入时已有World滚动/导航等dirty实现均保留。

## 做了什么

修复当前页面身份的迟到回退、冲突推进和重访签名不稳定，并让正式语义能力的观察结果经现有P2/World链路进入已识别容器的Slice。保留World、Assurance、Effect原所有权，没有新图、新公开载荷或Jev专属World入口。

## 原来的问题

```text
A(t1) → B(t3) → 迟到A(t2)       当前身份从B回退到A
同scope观察A、再观察矛盾B        记录Conflict，但仍New(B)
同入口occ-1、重访occ-2           页面签名不同，存在身份碎片风险
```

这些行为已经由WORLD-SEMANTICS-001构造探针证明；本轮转成正式回归测试。新语义subject还必须处理“第一条结果已经晚于切页”的情况，不能只看该subject是否已有值。

## 这次修改

| 文件 | 增量与现在的行为 |
|---|---|
| `src/UniClaw.Kernel/World/WorldModel.cs` | Owner用当前`ui.screen`原capture时间判断历史适用性；Reaffirm也推进时间标记。历史记录仍进入basis，但不替换当前页面claim/身份/occurrences或追加关系；当前页面claim本次冲突则阻断关联；New候选要求全部support合法且无显式反证 |
| `src/UniClaw.Kernel/World/WorldModel.Semantics.cs` | 复用唯一`SemanticPageClaim`，核对当前身份证据与exact capture；有效语义进入`<containerId>.semantic.page`，由既有Slice输出。类别不mint/merge身份，任何关联插件对该辅助subject的New/Matched都被Owner挡住，原签名不变；未知/未声明字段不写当前事实 |
| `src/UniClaw.Host/SettingsPageIdentity.cs` | 稳定入口使用accepted且唯一的元素描述，不再使用每轮新铸的occurrence id；缺描述/重复名称保持无法判别。同页滚动/已观察返回逻辑保留 |
| `src/UniClaw.Host/SettingsTraversalLiveFeed.cs` | 对现有签名函数调用补一项描述唯一性，从前页accepted occurrences计算；未增加语义业务接线或修改上游能力调用 |
| `tests/UniClaw.Kernel.Tests/World/{WorldIdentityReconciliationTests,WorldSemanticEvidenceTests,SemanticCapabilityWorldIntegrationTests}.cs` | 正式缺陷回归、当前capture/错帧/session/冲突/类别与身份分离、真实注册/L1/projector/P2/World/Slice调用链 |
| 既有关联与Host测试fixture | 不同frame使用不同观察scope；专门的冲突题显式同scope。原签名测试改用真实opaque occurrence形态与独立accepted描述，保留同名不同上下文判据 |
| World README | 登记本Owner partial与边界，无共享协议/CONTEXT/索引修改 |

代码阅读主线：`WorldModel.Reconcile → 当前适用性/claim冲突 → Associate owner gate → PublishRevision`；辅助语义为`SemanticContainer → scoped claim → DeriveSlice`；Host为`lastReceipt的accepted目标描述 → 唯一性 → SettingsPageIdentity.Derive`。

语义链路已经使用主会话落盘的真实公开接口与官方投影：

```text
CapabilityRegistry.Resolve("uni.perception") → ISemanticPerception.ObserveSemanticsAsync
→ SemanticObservationProjector.CreateProposals → UniKernel.Process / Ledger
→ WorldModel.Reconcile → 原容器Slice.ScopedClaims
```

两个供货实现复用同一消费代码；“实际观察details、意图other”仍输出details含义并单列Mismatch，不把预期写成当前页身份。模型confidence不参与身份权威。

辅助语义当前需要明确引用当前身份观察的accepted EvidenceId，并匹配其capture id与原观察时间；typed来源再核对session/cycle。仅类别相同或时间邻近均不够。缺该依据时记录仍可留作accepted历史依据，World在AssociationLog标不足，不给当前页贴语义。

## 验证结果

| method | expected | actual | evidence |
|---|---|---|---|
| 独立缺陷RED/GREEN | 旧页不回退、冲突不推进、临时id不拆页、缺/重复描述不猜 | 初始回归逐项RED；最终对应行为GREEN | identity-late/conflict/new-contradiction/revisit/entry-missing与host-entry-ambiguity等TRX；test-summary.json |
| 最终World专项 | P2→World辅助消费、错帧/迟到/冲突/typed session/cycle隔离、正式插件替换、原导航/滚动/P23/Slice与状态演进保持 | 100/100通过；意图Mismatch不改观察；同语义类别不同实体不合并；旧capture首结果不贴当前页 | evidence/world-reviewed-final.trx |
| Host全量 | 签名修复及现有操作重放等不退化 | 400/400通过 | evidence/host-final.trx |
| 相关Simulation | World重建/双跑重放及同步/异步遍历行为保持 | 70/70通过 | evidence/simulation-world-regression.trx |
| 完整Kernel（没有临时排除） | 功能与架构回归，所有失败明确归因 | 1009/1012；仅剩下面3个共享工作区检查失败 | evidence/kernel-reviewed-final.trx |
| 自审、保护与冻结 | 只改负责增量，保留他人内容，无意图/权威越界 | 12个负责源码/测试文件已冻结；852个保护文件中836未变，8个本会话负责文件、8个并行文件变化已归因；无清理/重置 | review.md；evidence/source-freeze.json、preservation-check.json、owned-incremental.patch |

测试命令（从仓库根目录）：

```sh
dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --filter 'FullyQualifiedName~WorldIdentityReconciliationTests|FullyQualifiedName~WorldSemanticEvidenceTests|FullyQualifiedName~SemanticCapabilityWorldIntegrationTests|FullyQualifiedName~UiRealizationBoundaryTests|FullyQualifiedName~WorldObservedNavigationTests|FullyQualifiedName~WorldViewportTransitionTests|FullyQualifiedName~KernelRunDriverNavigationGraphTests|FullyQualifiedName~ClaimEvolutionTests|FullyQualifiedName~UIWorld' --logger 'trx;LogFileName=world-reviewed-final.trx' --results-directory changes/WORLD-SEMANTICS-002/evidence
dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore --logger 'trx;LogFileName=host-final.trx' --results-directory changes/WORLD-SEMANTICS-002/evidence
dotnet test tests/UniClaw.Simulation.Tests/UniClaw.Simulation.Tests.csproj --no-restore --filter 'FullyQualifiedName~WorldReconstructionTests|FullyQualifiedName~WorldScenarioReplayTests|FullyQualifiedName~TraversalScenarioTests' --logger 'trx;LogFileName=simulation-world-regression.trx' --results-directory changes/WORLD-SEMANTICS-002/evidence
dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --logger 'trx;LogFileName=kernel-reviewed-final.trx' --results-directory changes/WORLD-SEMANTICS-002/evidence
```

Host与Simulation回归在严格JSON读取的最终小改动前已通过；最后小改动只影响`ui.semantic.page`未知字段读取，最终100项专项及完整Kernel在该改动后重跑。没有把不同时间的结果假称同一次全仓运行。

## 新发现与处置

- 初次保护覆盖所有状态claim过宽，会破坏既有矛盾留痕和合法occurrence派生；回归已发现并修正为当前`ui.screen`专用保护，普通状态权威裁决不变。中间失败保留，未修改断言隐藏问题。
- 稳定描述可能不唯一，已补现有feed唯一性输入与反例。路径/标题/类别仍不是无条件身份定义。
- 默认JSON读取会忽略额外字段，已由真实RED证明夹带expectedPageKey可以透传进Slice，改为按上游唯一record严格读取后GREEN；没有给P22添加预期字段。

## 当前剩余项与下一步

当前完整Kernel的3个失败：

1. `DocsMetadataTests.AnalysisAndDesign_DeclareStatusAndNoneAuthority_NoFrozenWords`：旧analysis文件缺Status/Authority。
2. `ProductHostClosureTests.Solution_SimulationTestProjectIsSeparateCompositionRoot`：Simulation测试项目既有Agent.Dsh额外引用与两引用断言冲突，本次没有改该csproj。
3. `SourceReadmeOwnershipTests.SourceFiles_AreRegistered_InDimensionReadme`：剩SettingsNavigationScope.cs和ProfileIdentityRenderer.cs登记，均不是本次World新增。WorldModel.Semantics.cs及主会话本轮语义文件已不在失败清单。

主会话继续共同文档、公开面与scenario认证，处理或准确记录这些共享检查；本会话已停止源码修改。相关实现不依赖Jev provider名称，未增加业务接线工程，也没有提交/推送或操作设备。

本实现只把有当前身份依据的语义结果附到既有容器；**不声称语义类别已能独立识别同route新实体、解决任意同名页面、创建包含/覆盖层生命周期或证明语义目的地Effect成功**。这些超出当前已接受载荷与本范围证据。真实Jev线路和模型误判结果由主会话单独报告，不能把本会话确定性测试当作生产准确率。

原始`ui.screen`时序保护依据accepted capture time的严格前后关系，没有从cycle字符串臆造先后顺序；同时间戳不同原始capture的通用排序不在本次证明范围。新语义路径另外要求exact当前身份证据与capture关联，因此不会仅因时间相等就接纳旧页语义。
