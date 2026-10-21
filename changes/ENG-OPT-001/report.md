# ENG-OPT-001 实施与验收报告

## 做了什么

按“前置任务收口→整合基线→所有权修复→验证判断归位→局部内聚和导航”完成本轮有界优化。保留其他会话的未提交工作，没有提交或推送。模块与组件的自顶向下清单、边界依据见[设计审查](../../docs/analysis/uniclaw-module-boundary-design-review-2026-10-21.md)，目录取舍见[组织审查](../../docs/analysis/uniclaw-directory-organization-review-2026-10-21.md)。

- WORLD-OBSERVATION-001由原Leader完成XML/无XML的实际high补证、World确认、Assurance验证及Traversal恢复后关闭；本会话等待其idle并核对72份最终指纹无漂移才编辑重叠源码。
- WRC-003补独立Standards/Spec与当前19+5项针对性验证后关闭。
- RUNTIME-IDENTITY-001补Store原A1–A4的独立审查及8项当前测试后关闭；不把这当完整HTTP/设备生命周期验收。
- 原现场4项整合失败得到修正；最新工作区8个.NET测试项目2024/2024通过。

## 原来的问题

1. `RunModel.AdmitContract`和`EvidenceLedger.Admit`保留调用方可变集合：接纳后改输入，会改变canonical权限、来源链或祖先依据，而RunId/EvidenceId保持原值。
2. `WorldModel.RevisionHistory`等六个读取面直接返回内部List；外部可Clear历史，从而改变Current。
3. `UniKernel`自行决定页面/时序/typed checked是否验证成功，Assurance只接收部分现成结论。判断职责分散，Kernel还调用ADB驱动的分类工具。
4. Settings覆盖流程用英文提示词反推动作及目标：`Volume > media`被截断，`Scroll settings`被误判为滚动；新语义事实与展示文字混合。
5. DSH协议消费测试放在Simulation中，引入不应有的Agent.Dsh依赖；源码归属、文档元数据、模型/工件测试断言落后于实际代码。

## 这次修改

| 调用链/文件 | 实际改动及原因 |
|---|---|
| [RunModel](../../src/UniClaw.Kernel/Run/RunModel.cs) | 接纳时拥有独立集合和证明条件快照；保持HashSet比较器，保留不可变set语义，SortedSet按原比较器复制。版本、预算幂等和RunId算法不变。 |
| [EvidenceLedger](../../src/UniClaw.Kernel/Evidence/EvidenceLedger.cs) | 新canonical record复制provenance lineage、直接父证据和叶依据集合；重复接纳仍返回原canonical对象，原admission/ID规则保留。 |
| [WorldModel](../../src/UniClaw.Kernel/World/WorldModel.cs) | 六个公开历史/日志读取面缓存只读包装；Owner追加仍对既有读取引用可见，外部写入被拒绝。 |
| [UniKernel](../../src/UniClaw.Kernel/UniKernel.cs)→[Assurance.PostAction](../../src/UniClaw.Kernel/Assurance/RuntimeAssurance.PostAction.cs) | Kernel投影最小只读事实、提供期望并编排顺序；页面身份/时序/typed checked/视觉谓词由同一Assurance Owner定案。保留Unknown/Unsupported/Partial、异常与既有日志分支；Kernel不再依赖ADB分类函数。 |
| [CoverageDirective](../../src/UniClaw.Host/SettingsCoverage/CoverageDirective.cs)→Director→journal→Ledger | 当前链携带Kind/Target/Text，验证和进度不解析展示文字；公开旧tuple入口与旧text-only记录集中走Legacy兼容读取。步骤JSON追加directiveKind/directiveTargetDescriptor，避免产物再次丢失语义。 |
| [Ledger.Directives](../../src/UniClaw.Host/SettingsCoverage/SettingsCoverageLedger.Directives.cs) | 将指令选择归为同一Ledger的内聚局部文件，不新建第二账本/Owner。Assurance的动作后逻辑也按行为归为同一partial Owner。 |
| [PlanGenerationKernelConsumerTests](../../tests/UniClaw.Host.Dsh.Tests/PlanGenerationKernelConsumerTests.cs) | 5项DSH codec消费测试从Simulation移至Host.Dsh.Tests；后者单向复用Simulation测试fixture，Simulation仅依赖Kernel/Agent产品程序集。整体测试发现数不减少。 |
| 默认绑定/握手fixture | 断言跟随所有者已选择的CommandCode和当前合法prompt工件；保留工件漂移/缺失的负向拒绝，不改产品默认模型或安全校验。 |
| [根导航](../../README.md)、src/tools/tests/schemas导航 | 补全入口、程序集与逻辑职责的区别、源码/发布副本、测试/证据分层及真实验证范围；复用现有基线，不复制第二份架构权威。 |

## 验证结果

### 修复前后行为

- Run：调用方清空/改写权限与证明条件，View不变；原忽略大小写禁止规则保留。
- Ledger：生产者改来源链/父集合，不改canonical记录；下一层派生证据仍沿正确叶依据接纳。
- World：外部Clear不再改变Owner历史/Current；Owner正常追加仍可见。
- Coverage：两个合法标签在旧冻结实现均失败；当前正式Host消费链均成功，Report无偏离，保存的JSON有明确种类/目标。
- typed验证：保持成功、Unknown/Unsupported/Partial、过期、缺dispatch、页面补证等待与不重复点击行为，判断集中于Assurance。

### 最终离线.NET整合

| 测试项目 | 通过/总数 |
|---|---:|
| Kernel | 1037/1037 |
| Host | 408/408 |
| Simulation | 252/252 |
| Agent.Dsh | 212/212 |
| Host.Dsh | 56/56 |
| Agent | 36/36 |
| Core | 14/14 |
| FileSystemRealization | 9/9 |
| **合计** | **2024/2024** |

method：`dotnet test UniClaw.Kernel.slnx --no-restore --logger 'trx;LogFilePrefix=eng-opt-final' --results-directory evidence/eng-opt-001/final-tests`；expected：8项目无失败；actual：2024/2024；evidence：[最终结果](../../evidence/eng-opt-001/final-tests/results.json)、[完整输出](../../evidence/eng-opt-001/final-tests.txt)及同目录逐项目TRX。

- 场景：28/28执行映射通过，20个认证场景+8个描述场景；当前Simulation TRX驱动覆盖结论，认证检查0违规。20个认证块经唯一工具更新源码哈希，排除认证元数据后28份场景与HEAD语义完全一致，未修改预期凑通过。
- 协议：7 machines / 37 transitions / 45 edges / 27 steps / 12 cards，生成物新鲜及图审计通过。
- 导航：97个局部链接，0失效；共享树差异格式检查通过。
- 审查：baseline、所有权、Assurance、Coverage分别做Standards/Spec独立审查，最终均0阻塞；比较器漏项曾被Spec指出，修复后双轴补审关闭。
- 保护：World交接文件只有声明范围的重叠变更；164份历史运行资料路径/字节无变化。入口快照、各切片patch/hash及RED/失败输出全部保留。

证据入口：[验收四元组](../../evidence/eng-opt-001/verification.json)、[双轴评审及处置](../../evidence/eng-opt-001/review.md)、[完整目录](../../evidence/eng-opt-001/)。本会话没有新设备/模型运行；原World实机结论引用其独立验收，不冒充本轮重跑。真实模型消费的3个opt-in测试未开启内部执行，Passed外壳不能当真实模型验收。既有编译警告仍在；本结论不是仓库所有语言、插件或外部环境全部通过。

## 新发现与保留范围

- 审查发现非HashSet不可变/排序集合比较器会被改变，已补公开反例及保留策略，未增加反射探测或收紧admission协议。
- `runs-d9`虽没有产品/测试硬编码读取，原路径被旧Change与冻结证据清单钉住。纯搬迁且不留别名会破坏旧命令/清单的直接解析。本轮保留164份原路径/字节，补[清单](../../evidence/eng-opt-001/historical-runtime-paths.json)、历史标识和新运行显式输出根说明。
- Driver大状态簇、RuntimeHttpServer用例拆分、Host authoring、Provider目录重命名及历史产物物理迁移保留为独立后续；不为目录好看扩程序集，也不在缺真实buyer时预造UniAgent/Memory/通用插件平台。
- RUN-006、AGT-019、泛化计划及未完成全树覆盖等旧任务仍按各自Acceptance开放，详见[逐项处置](../../evidence/eng-opt-001/open-change-disposition.md)。本轮优化不替代它们的验收。

## 下一步

本轮范围已完成。后续若推进大目录/历史证据搬迁，先单独确定旧路径兼容与留存方式；若继续功能迭代，沿各未关闭Change自己的验收推进。当前不提交、不推送。

2026-10-22 发布补记：所有者随后明确“完成后提交推送”，覆盖前阶段仅本地交付限制；已完成代码与必要前置按隔离候选验证后发布。历史记录中的“不提交/推送”描述当时范围，不再作为当前限制。
