# UniClaw 模块与组件职责边界审查（2026-10-21）

> Status: COMPLETE / READ_ONLY_REVIEW
> Authority: NONE

## 结论与范围

四个 Product T1、Kernel 六个 Product T2 的责任分配值得保留；六个产品程序集的依赖图无循环，也未发现 Host/DSH/Simulation 反向进入 Kernel 的项目依赖。主要问题集中在：canonical 输入接纳时没有取得数据所有权、World 的可变存储从只读接口泄漏、Kernel 组合层承担了部分 Assurance 判定，以及 Runtime/覆盖遍历的变化原因过度集中。

本轮确认 **3 个 P1 问题、4 个 P2 设计债/维护耦合**。P1 中两个已由隔离程序复现，另一个由调用链确认。没有依据提出整体重写、把六个责任域全部拆成独立程序集，或立即建立通用插件框架。

审查对象是入口 HEAD `bce0ec3a981a4598b889bb0bf37d2b3e389aa41c` 加当时工作树，不是某一批提交的 diff。入口已有大量未提交修改和新文件。先冻结只读副本，再由 Leader 做核心判断、三个 Luna 分别检查依赖/执法清单、Workspace/插件、Provider/工具；Luna 只回证据，没有修改文件。

审查期间发现 Runtime 和 World 等文件仍有并行编辑。证据包保存入口 hash、原始状态、源码快照及后续漂移路径。本报告的行号对应快照；关键 P1 也在入口 HEAD 中存在，不归因于本轮未提交实现。并行新增的页面观察恢复路径没有做完整设计验收。离场复查确认问题 1/2/3 的具体代码模式仍存在（见 exit-anchor-check.json）；新 UniKernel 门控加入后 typed 判断行号已偏移，以下仍按冻结快照定位。

审查依据：AGENTS.md §1.5、产品架构基线 §7/§11–§16/§20–§21、仓库职责基线、组件间协议基线、codebase-design 与 capability-component。检查单一职责、Interface 的实际负担、变化是否集中、依赖方向、canonical 写入权、替换与生命周期。本报告是设计审查，不是全量功能/安全审计；“未发现具体越界”不等于证明无缺陷。

## 发现（按优先级）

### 1. P1：接纳后的契约和证据仍与调用方共享可变集合

- **代码**：`src/UniClaw.Kernel/Run/RunModel.cs:192–201`、`Run/ExecutionContractView.cs:25–39`；`Evidence/EvidenceLedger.cs:80–88`、`Evidence/Provenance.cs:13–18`。
- **原行为**：Run Model 把 `contract.Scope/AllowedEffects/ForbiddenEffects/ProofCriteria` 原引用交给 canonical View。Ledger 保存原 `Provenance`，其中 `TransformationLineage` 仍指向 producer 的列表；派生父证据集合也直接赋值。
- **触发**：作者在 admission 后调用原 `HashSet.Add`；producer 在 admission 后修改原 lineage 列表。两者都是正常持有输入引用即可发生，不需要反射或篡改 private 字段。
- **影响**：已接受契约的许可集合和已生成 EvidenceId 的 provenance 会在 Owner 之外改变，RunId/EvidenceId 却不变。immutable、唯一写入和内容寻址三项约束同时失效。
- **复验**：`allowed={tap}` 接纳后增加 `new-effect`，View 立即可见、RunId 不变；接纳的 lineage 从 1 项变成 2 项，EvidenceId 不变。仅改变内存状态，未调用设备动作。
- **建议**：在 Run/Ledger 的接纳边界取得数据所有权，对集合做独立不可变快照，含 nested provenance/lineage；不是给所有 DTO 机械套一层接口，也不是只把属性声明改成 IReadOnly。
- **验收**：修改原输入集合不影响 canonical 对象、旧历史和 ID 所指内容；经 View 暴露的集合也不可回写。保持原有合法输入的身份与行为兼容。
- **归类**：已提交代码中的边界缺陷，不是全局 task-scoped API 的留白。

### 2. P1：World 的只读历史接口泄漏真实可变存储

- **代码**：`src/UniClaw.Kernel/World/WorldModel.cs:28–35,67–104`。
- **原行为**：`RevisionHistory => _revisionHistory`，其他 relevance、association、continuity、evolution logs 同样直接返回 List 对象；Current 又从 `_revisionHistory[^1]` 派生。
- **触发**：`((List<WorldBeliefRevision>)world.RevisionHistory).Clear()`。
- **影响**：外部消费者可绕过 Reconcile 清空 canonical history，并把 Current 从有值变为 null；不是仅影响诊断展示。日志同样失去 append-only 约束。
- **复验**：真实 Reconcile 后 `world_current_before_clear=True`；通过公开历史引用清空后 `world_current_after_clear=False`。
- **建议**：按 Ledger、Run、Control、Effect 已有的封装方式，发布不可写的 live view 或真正的不可变快照；保留调用方所需的实时可见性，不盲目给每次读取复制全部历史。顺查 `SettingsCoverageLedger.Events` 的同类暴露（`SettingsCoverageLedger.cs:100–101`）。
- **验收**：不能经公开读取面修改内部集合；非法修改被拒绝且 World.Current/历史不变；合法 reconciliation 仍可追加。
- **归类**：已提交代码中的实际 Owner 封装缺陷。

### 3. P1：typed 动作效果判定在 UniKernel 完成，Assurance 只负责登记

- **代码**：`src/UniClaw.Kernel/UniKernel.cs:482–492,616–666`；`Assurance/RuntimeAssurance.cs:50–55`。
- **调用链**：`VerifyPostActionEffect → TryRouteTypedVerification → resolved == target.DesiredState → new PostActionEffectVerification(IsVerified) → Assurance.RecordPostActionVerification`。
- **职责问题**：Kernel 不只汇集输入和选择路由，还决定时序是否足够、Partial 如何解释、desired-state 是否满足。Assurance 的记录方法仅 null-check/add/return，与视觉路径由 Assurance 自行执行 checks 的形态不同。
- **影响**：同一个验证策略分散在 Kernel 与 Assurance。以后调整 checked/Partial/freshness 时必须了解组合层的判定分支，Assurance 也不能通过自身 Interface 完整覆盖该职责。
- **裁决核对**：PER-014 明确安排过 UniKernel typed 路由迁移，因此不能说它是未经授权的新功能；但位置与顶层“Kernel 只组合、Assurance 独占 judgment”存在持续设计张力。属于已有实现的责任归位问题，修复需要先对齐既有裁决，而非删除 typed 门。
- **建议**：Kernel 保留收集 World/Ledger 事实和调用顺序；typed 成功/失败判断与记录统一由 Assurance 执行。让 Interface 接收事实，不接收调用方已定案的 IsVerified。
- **验收**：checked/unchecked/Partial/Unknown、无 dispatch 时间、过期 capture 等分支行为保持；Kernel 不再直接构造验证成功结论。
- **相关 P2 耦合**：`UniKernel.cs:519` 依赖 `AdbEffectDriver.IsSwipeEffect` 来决定期望谓词。应使 effect 语义分类独立于具体设备 Adapter，不能为新 driver 继续添加 Kernel 分支；不要求把现有 driver 全搬目录。

### 4. P2：KernelRunDriver 内部状态跨多个变化原因集中

- **代码**：`src/UniClaw.Kernel/Runtime/KernelRunDriver.cs:163–306`（咨询、预算审查、Policy、Plan 状态），`:2473–2533`（遍历记录与观察有效性），`:2876`（P25），`:3289–3389`（完成锚点与人工裁决）；`Control/TraversalControl.cs:7–43`。
- **职责问题**：驱动 phase、咨询 journal、预算续授/审查、Policy/Plan 展开、handler 等待和完成材料处理均由同一类协调大量相关字段。这里确有合法的 composition/lifecycle state，不能因为它持状态就判定新增 canonical Owner。
- **内聚风险**：增加一个 Plan item/恢复分支，会同时影响 phase 转移、计数、pending failure、完成锚点及预算视图。字段重置顺序变成调用方和维护者必须掌握的隐性 Interface。
- **建议**：先沿真实状态簇把内部 cursor/session 管理封装起来，例如预算审查会话、Plan 执行游标、P25 咨询协调。保持一个主驱动器和一条 StepAct→StepVerify 链；不是另建 Runtime FSM、Owner 或外部插件接口。已有 PolicyRuntime/TraversalControl 应继续成为归拢位置。
- **验收**：暂停/续行、取消、异步 handler 晚到、预算审查和终局场景从既有驱动 Interface 验证，原有顺序和 effect 次数不变。先处理最常一起变化的一簇，不做大爆炸拆分。

### 5. P2：覆盖台账混合事实、选路策略和英文提示协议

- **代码**：`src/UniClaw.Host/SettingsCoverage/SettingsCoverageLedger.cs:70–78,311–363,621–703,1117–1149`；`SettingsCoverageDirector.cs:149,295,591`。
- **调用链**：Ledger 保存记录 → Snapshot 调 NextDirective 选下个动作并拼英文 → Director 把英文发给 Agent → CoverageStepRecord 保存 Directive → Ledger 再解析英文前缀和 `<descriptor>` 判断动作类别/符合性。
- **影响**：修改提示措辞、翻译文案或目标包含分隔字符，都可能改变覆盖统计和符合性判断。`CountsAsCoverageProgress` 直接识别 `Clear the popup`/`Dismiss the popup`；`DirectiveMatches` 解析不出类型时 `_ => true`，所以它不是可靠的结构化语义缝。此处不等于 Effect Gate 被绕过，后者仍另有执法。
- **建议**：Coverage Ledger 继续拥有覆盖事实与派生统计；选路放在独立的现有任务策略职责，Directive 用已有 kind/target 等事实形成 typed 值，英文仅在提示渲染时生成。兼容旧记录应显式归为 legacy/unknown，不能默认为符合。
- **验收**：只改提示语言不改变动作类别、统计与符合性；未知 directive 无法静默判符合；旧记录读取有明确兼容结果。
- **归类**：确定的职责/表示耦合；Host 中存在 Settings-specific 配置/策略本身是允许的，不因此全部迁入 Kernel。

### 6. P2（已登记偏差）：Host 同时做装配与 Contract authoring

- **代码**：`src/UniClaw.Host/HostRunner.cs:293–312`、`SettingsCoverage/SettingsCoverageRunner.cs:410–444`。
- **行为**：Host 创建 ExecutionContract 的 Objective、Scope、Allowed/ForbiddenEffects、ProofCriteria 和 Obligations，再提交 admission；coverage 路径还使用固定 Task/Session/Contract/View 字符串。
- **职责问题**：任务 authoring 和进程装配的变化原因混在一起，替换 Host 时不仅换 transport，还可能重写任务含义。固定身份字符串也不能外推为通用跨任务身份方案；本轮未据此证明真实部署发生了身份冲突。
- **核对**：LIFECYCLE-CLOSURE-001 和 buyer-scoped 审查已把 HostRunner authoring 明确记为 known deviation，并不阻塞当前最小闭包。不能以该偏差为理由顺手重造完整 UniAgent。
- **建议**：真实 UniAgent authoring buyer 到来时，由其提供受版本管理的契约提案，Host 仅验证/携带/装配，Run Model 保持 admission authority；当前先防止该特例继续扩散。
- **验收**：换 Host 不改变 Goal/Contract 语义，Task/Session 绑定来自合法入口；被拒契约无激活/Effect 副作用。

### 7. P2：通用 HTTP 接入与 Wi-Fi 任务启动、产物终结耦合

- **代码**：`src/UniClaw.Host.Dsh/RuntimeHttpServer.cs:141,346–391,440,492,658–703`。
- **行为**：同一类处理 Task/Session/Run HTTP、持久启动记录、DSH peer 装配、直接启动 Wi-Fi Settings、固定 LiveAssets、语言检查选择、产物报告及工具调用。
- **职责问题**：HTTP/错误 envelope 改动、任务启动策略改动、报告新增字段都会触碰同一模块；所谓通用 `/runs` 背后隐藏了 Wi-Fi 的任务假设。
- **建议**：从当前真实任务启动路径提取内部用例/任务启动 Adapter；HTTP 层负责解析、调用和映射。产物 finalize/export 也应有稳定输入。不要为尚不存在的任务创建自动扫描/万能 Hook。
- **验收**：增加第二个已授权任务时无需修改 HTTP 通用路由逻辑；相同启动请求的 binding、幂等、失败与副作用保持可判定。

## 自顶向下逐模块检查账

“基本清晰”表示本轮没有找到可定位的责任越界；“有问题/设计债”对应上文证据；“占位/契约”不能评价为已完整实现。

| 模块/组件 | 单一职责与内聚 | 耦合与边界结论 | 处理 |
|---|---|---|---|
| UniClaw T0 / T1 拓扑 | 系统边界、四个顶层职责明确 | 未见需新增总 Owner 的理由 | 保留 |
| UniAgent / Goal / Evaluation | Goal 与 envelope-based Evaluation 集中；不回读原始运行证据 | Agent→Kernel 单向，未见反向依赖；完整策略/authoring 尚非本体已实现能力 | 保留，偏差见 6 |
| Agent Profile / YAML / identity renderer | 产品身份、声明加载、只读表达 | 模型/endpoint 绑定在 DSH Adapter；不把 modelRoles 当语义职责 | 基本清晰 |
| UniKernel 聚合 | 多责任域协作本来就是其职责 | typed judgment 和 ADB 分类渗入聚合 | 问题 3 |
| Evidence Ledger | 只做 admission，不判断现实真假 | 外层存储有 ReadOnly 包装，nested provenance/集合仍共享输入 | 问题 1 |
| World Model | Belief/身份/冲突归同一 Owner；Semantics、Viewports 为内部实现 | 主要字典有 persistent/frozen 存储，但历史/日志有 List 泄漏 | 问题 2 |
| World UiRealization / association / continuity | 提案策略与 World acceptance 分离 | 既有 namespace 依赖白名单；Android 采集 metadata 不自动等于越权 | 基本清晰；并行页面恢复新增未完整验收 |
| Run Model / Contract / Outcome | Run 记录和唯一终态集中，未把判断吞入记录器 | 历史外层封装好；Contract 输入没取得所有权 | 问题 1 |
| Control Loop / policy / TraversalControl | Intent 与遍历游标归 Control，不直接投递 Effect | 与 Runtime 共享 typed 词汇是现有缝，不能仅凭 using 判循环权威 | 基本清晰 |
| Assurance / freshness / OutcomeProof | 许可、充分性与终局证明在同一职责域 | typed post-action 路径有外部定案入口 | 问题 3 |
| Effect Boundary / binding / gate | Bind、消费、投递及 terminal closure 内聚 | 经 IEffectDriver/可靠执行源注入；日志只读封装 | 基本清晰 |
| ADB / browser drivers | 设备命令、超时、transport 映射属于 Adapter | 具体 driver 存在于程序集内允许；上层依赖其语义工具函数不理想 | 问题 3 的 P2 子项 |
| ExecutionSource / journal | attempt 与持久执行记录独立 | 不把 receipt 当世界效果；不需新总账本 | 本轮未发现具体越界 |
| Kernel Runtime / main driver | 主推进循环职责成立，内部状态簇过密 | 多种变化集中，修改需同步多个游标 | 设计债 4 |
| PolicyRuntime / Plan / Traversal / handler call | 已有内部求值、游标和异步调用模块 | 继续复用一个 StepAct/StepVerify；typed handler 不成为万能 capability dispatcher | 保留并局部归拢 |
| Consultation seam / journal | P25 correlation/retry 与记录缝清楚 | 不要求立即实现跨 Host 完整恢复 | 基本清晰 |
| Trace | 非权威 operation/reference、seal/finalize 分离 | Recorder 故障由组合层隔离；无依据当作新状态 Owner | 基本清晰 |
| Diagnostics | 运行度量和非权威聚合 | 规模/耗时不授予执行权限 | 基本清晰；未压力测试并发性能 |
| UniClaw.Core / projection | typed 不可变形状与跨域候选词汇 | 不含 Runtime 判断、不是 T1 总核心 | 保留；发布对象也应遵循所有权原则 |
| Capability Hub / Registry | 注册、发现、协议一致性与 lifecycle fact 集中 | 含 ModelRouting/LanguageInspection/Perception 类别分支，扩新协议会改 Registry；当前是既有显式执法，不是新缺陷 | 观察项：出现新真实协议 buyer 时评估局部校验规则；不先造插件总线 |
| Model Management | 逻辑 profile→binding、偏好/降级、健康集中 | DSH 从外注入；描述重述共享同一活表是明确设计 | 基本清晰 |
| UniPerception / Semantic protocol | L2 执法关联/预算/去重，L3 负责供货 | 新语义 Adapter 位于 Host，未发现直接写 Ledger/World | 工作树实现结构清晰，不声称已验收 |
| Fast/XML/Slow/Fusion/Projector | 采集、解释、来源融合、投影分开 | Fast/Slow 是内部策略，UI Element 的 marker 是有意留白 | 保留，不为接口对称预造实现 |
| Runtime Integration contracts | Finding/Measurement/Artifact/Fixture typed 关联 | 不得把契约存在当真实测量能力落地 | 契约/占位，按 buyer 补实现 |
| Language Inspection / task binding | 任务要求绑定、检查、post-commit/typed adapter 分离 | 有具体语言检查 task binding；全局 Resolve 不等于通用 task API 已实现 | 基本清晰 |
| Host composition / Runtime stores | 显式组装、身份持久化、activation、journal 分开 | 常规装配依赖具体类是允许的；Contract authoring 是偏差 | 设计债 6 |
| Host LiveFeed / perception pipeline | capture/XML/slow settlement 与组合接线 | 未发现 bypass Ledger/World 的新 authority | 基本清晰；页面恢复并行变动单独验收 |
| Settings Coverage Ledger/Director | 台账、下一步选择、提示生成、符合性混合 | 英文文案成为机器协议，事实层牵动策略层 | 问题 5 |
| Settings ActionPolicy / geometry / projections | 硬约束、几何信号、只读事实投影分开 | 不因 Host 持有安全约束就认定抢夺 Assurance authority | 基本清晰 |
| Agent.Dsh / channel / codecs / peer | 附着、握手、one-in-flight、JSON/HTTP 映射 | 当前 peer 直接序列化 Context，不能沿用旧版“丢 LocalView”的历史结论 | 基本清晰 |
| Host.Dsh / Runtime HTTP / tool host | 宿主接入职责成立，HTTP 类绑定具体 Wi-Fi 用例 | 通用接入、任务启动、finalize 变化原因混合 | 设计债 7 |
| Workspace Query Core | 请求/错误/快照约束 | 能力注入，未见 DSH 存储直连 | 基本清晰 |
| Workspace controller / view-model / UI | 选择/加载、投影、渲染/布局职责分开 | 未见 UI 写 canonical state | 基本清晰 |
| Workspace runtime/fixture/DSH adapters | 不同 Host 接入和展示投影 | trace source 合并需保持 provenance；本轮未证实篡改 authority | 观察项，不计发现 |
| DSH decision-channel | 会话/工具提交/握手协议映射 | 生成 schema 与产品声明副本是受控发布，不因复制判双真相源 | 基本清晰 |
| DSH restrict / task-scope | 工具可用面限制 | 未接管 Run/Goal/Effect truth | 基本清晰 |
| DSH task-workbench | 装配 canonical Workspace 与 panel capabilities | 导入 canonical Core，未发现重造同义 Core | 基本清晰 |
| Perception Provider server / pipeline | HTTP、编排、具体 detector/OCR 分派仍在 server | 正常组合根依赖不算越界；pipeline 目前兼声明/校验，不先要求通用 DAG 引擎 | 可局部改善内聚，未计为确认缺陷 |
| YOLO / OCR / ScreenParse / mapping | 模型加载/推理与 canonical label 映射分离 | replacement 进 yolo 字段为既有明确协议选择；无证据指消费者误认来源 | 基本清晰 |
| Provider health / identity | 可用性、warmup 和资产身份 | 不冒充语义准确率或产品成功 | 基本清晰 |
| product / .dsh/product | 产品声明与宿主 provider/model/endpoint 分离 | 文件存在不等于本次实例已激活 | 基本清晰 |
| schemas / generators / tools | wire 契约、生成和开发校验 | JSON/YAML 生成物是执法投影；不升级为第二产品语义权威 | 基本清晰 |
| tests / scenarios / testsets / evidence | 可执行测试、fixture、验证证据分层 | Product→Simulation 依赖被结构执法阻断；测试通过不证明全量设计正确 | 保留 |
| Harness / UniFlow / changes / workitems | 流程、持久意图、瞬时委派分开 | 与产品运行时结构隔离；没有要求新建第二任务系统 | 保留 |
| Memory System | 定位清晰，尚为声明占位 | 无真实 buyer 前不补通用 Memory authority | 占位，不计缺陷 |

## 验证与证据边界

- **方法**：入口源码快照；六 csproj 依赖检查；所有上表模块定向读码与跨层调用追踪；关键问题对照 HEAD 和现行裁决；隔离副本编译并执行最小程序。
- **预期**：canonical 接纳后不受输入修改影响；公开读取不能改变 Owner state；typed verification 判断在 Assurance；依赖无反向项目引用。
- **实际**：前两项复现违反预期，typed 判断位置由实码确认，六程序集依赖方向成立。没有访问设备、启动 Provider 或调用模型，没有修改产品代码；未重跑全解测试或声称本轮全量测试通过。
- **证据**：`evidence/design-boundary-review-20261021/` 保存 entry-head/status、manifest、source-drift、Program.cs、Probe.csproj、result.txt、source-snapshot.tar.gz 与 reproduce.sh。reproduce.sh 在临时目录解包并编译，不写本地产品 checkout。
- **复现命令**：`sh evidence/design-boundary-review-20261021/reproduce.sh`，需要 .NET 10 SDK。三类行为反例的输出为：

```text
contract_admitted=True
before_new_effect=False
after_new_effect=True;run_id_unchanged=True
world_history_runtime_type=List`1;is_mutable_list=True
evidence_admitted=Accepted;lineage_before=1
lineage_after=2;evidence_id_unchanged=True
world_current_before_clear=True
world_current_after_clear=False
```

这些是模块封装的反例，不代表真实设备已经发生越权动作、历史被实际清空或线上数据遭篡改。

## 下一步工程顺序

1. 先修 Run/Ledger admission 的所有权快照和 World 读取封装，使用本报告反例补回归；不改变产品架构和 owner 划分。
2. 对齐 PER-014 与顶层 authority 规则，把 typed verification 的判断放回 Assurance；Kernel 仍保持唯一组合与驱动路径。
3. 将覆盖指令的 typed 语义和显示文案分开，再从台账抽出已有选路策略。
4. 只在下一次真实 Plan/预算/恢复需求触碰状态簇时，局部整理 Driver；不以行数为目标拆类。
5. Host authoring、跨任务启动与 Registry 扩展跟真实 buyer 单独立项，不把有意 deferred 的范围混入修复。

本次只新增审查文档和复验证据，未实施上述建议，未提交或推送并行工作。
