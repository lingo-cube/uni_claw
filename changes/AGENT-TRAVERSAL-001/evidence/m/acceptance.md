# 局部多步Act已落地；真实Agent整树遍历未复验

做了什么：按Owner Q1–Q4实现Act/Plan分工。Act允许发起容器范围内有界多步，最多使用同容器滚动后的可靠Slice元素；进入、返回或清障可作末步，范围改变后不投递tail。Plan继续在新事实下跨Slice展开。

原来的问题：SettingsActionGuard无条件拒绝多步Act，Runner已安装的逐步Guard无法发挥作用；拒收转null，使正常Act建议表现成no-response。另一方面，仅放开条数并不满足用户随后明确的当前Slice/同容器边界。公开反例还定位到普通Act后步失败索引误报0，以及Settings清障禁手/未知路由限制未完整接入逐步路径。

这次修改：
- SettingsActionPolicy.cs：只有已强制接入逐步Guard时才延后Act政策检查；无Guard仍拒收多步。配置版逐步Guard执行已有popup确认禁手、安全dismiss和unknown-route恢复限制。AgentProjection把同一Act范围规则作为只读产品上下文告知Agent，不修改全局Profile/Prompt/Skill。
- KernelRunDriver.cs：采纳Act时记录既有World当前容器/覆盖层状态的私有执行游标；每步在原Guard/Control/grounding/Assurance/Effect之前检查scope。变化且仍有tail时，零投递并经原StepRejected返回原提案、剩余步骤、正确索引和本提案已验证receipt引用；不自选下一路线。Guard失败在清理Plan前保存索引，Plan与普通Act保持正确归属。
- AgentDecision.cs XML、P25、CONTEXT与两README同步协议含义。无wire字段/新Owner/新执行器，不改World/Control/Assurance/Effect的裁决权。
- 测试复用既有private fixture，仅将三测试类声明改partial；新增公开Runner/Guard/Driver回归。既有Act/Plan“只看条数”的特征测试按新契约改为Act进入后停、Plan可返回；动作输入、独立菜单Oracle、真实模型旧任务文字及封存源/旧TRX/prelive pins保持。

## 验收四元组

| 项 | method | expected | actual / evidence |
|---|---|---|---|
| 有界多步准入 | 公开Runner同容器scroll→新Slice入口末步进入；同例RED/GREEN | installed Guard可处理安全局部Act，无Guard保守拒收 | 新Host27/27中相关例通过；admission-red-v2→admission-green；final-contract Host TRX |
| Slice/容器范围 | 正式页面身份观察，Act进入/返回后的tail、overlay关闭及替换；Plan对照 | 同容器新事实可用，离开范围tail零投递，Plan跨Slice仍可执行 | scope-red-v3与overlay-scope-red→最终绿色；scope-tail/act-revised/overlay-tail-* artifacts；原Scope异常载体/mtime无效对照不计证明 |
| 反馈/归属 | 后步Reject/Unknown、原提案引用/remaining/receipt、显式重提Act | 正确StepRejected原因与非零索引，不是no-response、不丢未执行tail，不重投前receipt | 三index RED→GREEN；Act与Plan缺目标反例、重提后两个不同DecisionId/receipt均通过 |
| 等待/失败/预算 | 公开Kernel同实例反复DriveAsync、取消；Runner首步验证失败/危险未知/17步/超预算 | 未结算或失败时后步零投递，取消不重发，限额/禁手保持 | 新Kernel2/2及Host局部反例通过；popup-red/unknown-route-red→GREEN |
| 相关回归 | 最终四项目TRX逐例复用，原三组买方精确筛选 | 原79买方保持；新例全部实际执行；宽测失败准确对照 | 新29/29、原79/79；verify.py逐例核验，不把Skip或live入口无操作当模型能力 |
| 宽测对照 | 逐字节M入口副本＋同852资产，对比同名失败集合 | 无新增宽测失败；已有失败不隐去 | Kernel候选1302/1305、入口1300/1303，同3；Host941/956、入口914/929，同14，均1模型Skip；DSH候选/入口均86/87，同partial 1失败。不是全绿 |
| 来源/边界 | candidate-final、owned-sha256、entry、assets、旧输入及P源校验 | 固定来源可复验，不覆盖并行修改，不改旧证据 | 2364固定输入/21执行程序集；965未变保护源码，5并行owner文件单列；55旧审计输入及P私有候选/执行物保持；双轴最终0；verify.py通过 |

验证入口：`python3 changes/AGENT-TRAVERSAL-001/evidence/m/verify.py`。它核验最终源码/执行物、原入口、保护/并行文件、旧输入、RED、29新例、79买方、三个宽测失败集合；输出`scopedChangeVerified=true`且`allSuitesGreen=false`。宽测原始日志/TRX和全部尝试保留，失败明细见verification.json，资格问题见attempts.md。

新发现与限制：Host宽测12项历史回放因旧源码SHA pin不匹配而未进入行为执行，入口同样被阻断（入口首先报Ledger，候选首先报Policy）；不能据失败集合相同就宣称这些原回放通过。其余旧失败包括回放OwnerId不一致、内存阈值、Kernel原三项及DSH partial覆盖用例，全部在入口实际复现，未顺手修。World/Assurance继续拥有可靠性与当前事实判定，Driver不拿Host route提示重铸容器身份。

共享树另外5处并行变化未纳入本固定候选：HostRunner、SettingsCoverageRunner、AuxiliaryArtifacts、DiagnosticObservations、AuxiliaryObservationIsolationTests。它们未被覆盖/回滚；本轮不认证后续最新共享整合树。SIM037正常真实Agent A1仍FAIL、A2未运行；blocked-scroll摘要缺页面scope风险未在本片修改。

下一步：若继续验证真实Agent，应在新版本预注册中复用固定任务/独立Oracle，先复验正常路径，并把本片产品版本与后续并行修改的整合版本明确区分；旧输入/失败/pins不改、不追加任务文字抽样追绿。本片不启动该ENVIRONMENT阶段，无提交、推送、合并、服务重启或基线晋升。
