// 生成产物（PROTOCOL-MACHINE-001 单源：schemas/protocol/state-machine.yaml interaction_model 段）。
// 手改会被 tools/gen-protocol-machine.py --check 漂移检测抓出。
window.PROTOCOL_STEPS = [
 {
  "g": "pre",
  "no": "0a",
  "nm": "任务定义",
  "edge": "Task Catalog（无编号边）",
  "dir": "Host / product/tasks → Runtime",
  "st": "track",
  "note": "任务模板不是一次执行。",
  "fields": [
   {
    "n": "task yaml",
    "t": "product/tasks/*.yaml",
    "d": "settings-coverage / language-inspection / tool-runtime：步数与咨询上限、capability 声明、预检要求",
    "req": "必"
   },
   {
    "n": "catalog 身份",
    "t": "catalog + 版本",
    "d": "ADR-0032：目录归 Host 所有，Workspace 只读投影",
    "req": "必"
   }
  ]
 },
 {
  "g": "pre",
  "no": "0b",
  "nm": "任务发起",
  "edge": "O1 · task-launch wire",
  "dir": "调用方 → Runtime HTTP",
  "st": "track",
  "note": "wire 面是全仓锁得最好的契约之一（additionalProperties:false + validator + 正反例）。身份统一走 O1 实现 Change。",
  "fields": [
   {
    "n": "taskRef（逻辑任务引用）",
    "t": "schema 字段",
    "d": "不含物理路径/绝对路径（schema 拒收）",
    "req": "必"
   },
   {
    "n": "idempotencyKey",
    "t": "schema 字段",
    "d": "幂等键；重复提交复用 binding",
    "req": "必"
   },
   {
    "n": "declaredConfig",
    "t": "schema 字段",
    "d": "声明式配置",
    "req": "选"
   },
   {
    "n": "launchId",
    "t": "ack 字段",
    "d": "Task Instance 尚未生成时可先只返回 launchId",
    "req": "系"
   },
   {
    "n": "stageRecords[]",
    "t": "launch-stage-record.v1",
    "d": "阶段记录：valueOrigin ∈ {configured,generated,observed,derived} + availability 分离",
    "req": "系"
   },
   {
    "n": "⚠️ 两套实例号",
    "t": "known deviation",
    "d": "Workbench 本地 instanceId vs Runtime taskInstanceId；psi-* 为兼容路径",
    "req": "—"
   }
  ]
 },
 {
  "g": "pre",
  "no": "0c",
  "nm": "Task Instance 创建",
  "edge": "O2 · Runtime 铸造",
  "dir": "Runtime 内部",
  "st": "track",
  "note": "目标态：Task → Instance → Session 三阶段可观察（ARCH-ALIGN-001 外围矩阵登记）。",
  "fields": [
   {
    "n": "taskInstanceId",
    "t": "string",
    "d": "Runtime 唯一铸造；调用方不得传入或覆盖（ADR-0045）",
    "req": "系"
   },
   {
    "n": "⚠️ 阶段混合",
    "t": "known gap",
    "d": "HTTP 创建路径把 Session/Run/Contract admission 混在一个阶段，无独立可观察步骤",
    "req": "—"
   }
  ]
 },
 {
  "g": "pre",
  "no": "0d",
  "nm": "Product Session 分配",
  "edge": "Runtime 铸造 + 显式 Binding",
  "dir": "Runtime 内部",
  "st": "track",
  "note": "任务结束再执行 → 新 Instance/Session/Run。",
  "fields": [
   {
    "n": "productSessionId",
    "t": "string",
    "d": "Goal/Contract/Run/Outcome/Evaluation 的关联根；建立 ≠ 开始执行",
    "req": "系"
   },
   {
    "n": "Task Launch Binding",
    "t": "binding",
    "d": "Task Instance ↔ Product Session（显式、带来源）；同次提交重试复用",
    "req": "系"
   },
   {
    "n": "hostSessionRef",
    "t": "string",
    "d": "Host 只铸自己的；dshSessionId 降为 adapter 内部 alias",
    "req": "系"
   }
  ]
 },
 {
  "g": "pre",
  "no": "0e",
  "nm": "契约 Authoring",
  "edge": "UniAgent 形成（P1 上游）",
  "dir": "UniAgent ← Task + Session context",
  "st": "track",
  "note": "目标态：UniAgent 是唯一合法作者（P1 只收它的要约）；当前代造走 O3 实现 Change。",
  "fields": [
   {
    "n": "Task + Session context",
    "t": "输入",
    "d": "UniAgent 读取后细化",
    "req": "必"
   },
   {
    "n": "Execution Contract 候选",
    "t": "输出",
    "d": "objective / scope / effect 红线 / proof 要求 / 预算",
    "req": "必"
   },
   {
    "n": "⚠️ HostRunner 代造",
    "t": "known deviation",
    "d": "真实 UniAgent authoring 未接线（Goal→Contract derivation = Deferred ⑫）",
    "req": "—"
   }
  ]
 },
 {
  "g": "contract",
  "no": "1",
  "nm": "P1 契约提交",
  "edge": "P1 · Execution Contract Admission",
  "dir": "UniAgent → Run Model",
  "st": "fix",
  "fixTag": "S3",
  "fix": "S3：预算保持可空，但 admission 记 valueOrigin；配合两层顶（契约=初始额度，policy=硬顶）。",
  "fields": [
   {
    "n": "Version",
    "t": "string",
    "d": "显式版本；同 version 重复 admit 幂等",
    "req": "必"
   },
   {
    "n": "Objective",
    "t": "string",
    "d": "目标",
    "req": "必"
   },
   {
    "n": "Scope",
    "t": "IReadOnlySet?",
    "d": "范围",
    "req": "选"
   },
   {
    "n": "AllowedEffects / ForbiddenEffects",
    "t": "IReadOnlySet?",
    "d": "effect 红线",
    "req": "选"
   },
   {
    "n": "ProofCriteria",
    "t": "list?",
    "d": "证明要求（缺省派生不可判定占位义务）",
    "req": "选"
   },
   {
    "n": "Obligations",
    "t": "list? = null",
    "d": "run-level 义务规格",
    "req": "选"
   },
   {
    "n": "MaxConsultations",
    "t": "int? = null",
    "d": "缺省 View 解析为 16",
    "req": "选"
   },
   {
    "n": "MaxTotalSteps",
    "t": "int? = null",
    "d": "缺省 View 解析为 256",
    "req": "选"
   },
   {
    "n": "valueOrigin",
    "t": "＋ S3 新增",
    "d": "admission 留痕 ∈ {configured, default}——\"沉默缺省\"变\"有账的缺省\"",
    "req": "系"
   },
   {
    "n": "身份三件套",
    "t": "🔵 轨道",
    "d": "taskInstanceId / productSessionId / contractRef（ADR-0046，不并入本包）",
    "req": "—"
   }
  ]
 },
 {
  "g": "contract",
  "no": "2",
  "nm": "P24 合法激活",
  "edge": "P24 · Legal Run Activation",
  "dir": "activation path → Kernel driver",
  "st": "keep",
  "note": "跨进程持久身份/并发/恢复 = Deferred ⑰（🔵 轨道）。",
  "fields": [
   {
    "n": "acceptedContractViewRef + contractGeneration",
    "t": "启动钥匙",
    "d": "同 key 至多一个 Run；重复/并发返回同一关联",
    "req": "必"
   },
   {
    "n": "activation latch",
    "t": "Kernel 内部",
    "d": "一次性幂等；admission ≠ 激活",
    "req": "系"
   }
  ]
 },
 {
  "g": "observe",
  "no": "3",
  "nm": "观察致动",
  "edge": "（无编号边 · D5）",
  "dir": "driver/Control → 感知采集",
  "st": "keep",
  "note": "协议图只建模了认知回流，没建模感知指向本身（审计 D5）。",
  "fields": [
   {
    "n": "编排",
    "t": "driver realization",
    "d": "何时/以何 scope 观察目前藏在驱动实现里",
    "req": "—"
   },
   {
    "n": "P27 候选",
    "t": "登记项",
    "d": "观察指令边等感知策略 buyer",
    "req": "—"
   }
  ]
 },
 {
  "g": "observe",
  "no": "4",
  "nm": "P2 观察回流",
  "edge": "P2 · Observation Ingress",
  "dir": "producer → Evidence Ledger",
  "st": "fix",
  "fixTag": "S1",
  "fix": "S1：新增 Origin（一石三鸟：闭 ADR-0047 gap + D6 判定输入 + E5 前提显式化）。",
  "fields": [
   {
    "n": "Claim",
    "t": "(Subject, Value)",
    "d": "观察声明",
    "req": "必"
   },
   {
    "n": "Kind",
    "t": "enum",
    "d": "Observation | AttemptReport（封闭二成员）",
    "req": "必"
   },
   {
    "n": "Context",
    "t": "enum",
    "d": "External | PostActionEffectFlow（流程上下文）",
    "req": "必"
   },
   {
    "n": "Provenance",
    "t": "record?",
    "d": "类型可空，但 null/不完整 → admission fail-closed：实效必填",
    "req": "准"
   },
   {
    "n": "DerivedFromEvidenceIds",
    "t": "init = 空",
    "d": "空 = 直接源观察；父引用由账本校验",
    "req": "选"
   },
   {
    "n": "TransitiveEvidenceBasis",
    "t": "init = 空",
    "d": "账本重算并拒绝不匹配",
    "req": "选"
   },
   {
    "n": "FusionRule / FusionVersion",
    "t": "string?",
    "d": "派生（融合）证据携带",
    "req": "选"
   },
   {
    "n": "Origin",
    "t": "＋ S1 新增",
    "d": "External | PostActionSelf——机械推导总有产出：精确 attempt 关联→PostActionSelf，否则 External；禁时间启发式；自报仅诊断",
    "req": "必"
   }
  ]
 },
 {
  "g": "observe",
  "no": "5",
  "nm": "P3 证据入库",
  "edge": "P3 · Accepted Evidence Record",
  "dir": "Ledger → World Model",
  "st": "fix",
  "fixTag": "S1",
  "fix": "S1：origin 入 EvidenceId canonical 输入。",
  "fields": [
   {
    "n": "EvidenceId",
    "t": "内容哈希",
    "d": "相同 canonical 语义 → 同 id（幂等去重）",
    "req": "必"
   },
   {
    "n": "Claim / Kind / Context",
    "t": "同 P2",
    "d": "随记录携带",
    "req": "必"
   },
   {
    "n": "Provenance",
    "t": "record",
    "d": "入库后非空（admission 已过）",
    "req": "必"
   },
   {
    "n": "DerivedFrom / TransitiveBasis / Fusion 元数据",
    "t": "同 P2",
    "d": "派生字段",
    "req": "选"
   },
   {
    "n": "Origin 入 canonical 输入",
    "t": "chg S1",
    "d": "origin 语义变化不得复用同一 id（ING-006 D5 同律）",
    "req": "必"
   }
  ]
 },
 {
  "g": "decide",
  "no": "6",
  "nm": "P25 咨询 + 续授",
  "edge": "P25 · Agent Decision Consultation",
  "dir": "Kernel driver ⇄ UniAgent",
  "st": "fix",
  "fixTag": "S3",
  "fix": "S3：续授走 P25（不新增边）；步数/轮次同机制各硬顶；轻记账（decision 留痕，不进 Run State）；崩溃恢复随 ⑰。",
  "fields": [
   {
    "n": "DecisionId / 关联号",
    "t": "string",
    "d": "每 decision boundary 最多一次；同边界重试复用结果",
    "req": "必"
   },
   {
    "n": "决策上下文",
    "t": "composite",
    "d": "含 BudgetRemaining——agent 打包前知道自己还剩多少",
    "req": "必"
   },
   {
    "n": "续授窗口标记",
    "t": "＋ S3 新增",
    "d": "driver 检查剩余 ≤ policy 阈值 → 本次咨询标记可谈续授",
    "req": "选"
   },
   {
    "n": "续授提案",
    "t": "＋ S3 新增",
    "d": "UniAgent 给续授量（正常推进未走完→该续就续；也可不续）",
    "req": "选"
   },
   {
    "n": "HardCap 校验",
    "t": "＋ S3 新增",
    "d": "policy 校验 已授+提案 ≤ 硬顶；终问（轮次耗尽最后一次询问）可携带续授——防\"有步数没机会开口\"",
    "req": "系"
   }
  ]
 },
 {
  "g": "act",
  "no": "7",
  "nm": "P8 意图",
  "edge": "P8 · Control Intent",
  "dir": "Control Loop → Assurance / EB",
  "st": "keep",
  "note": "唯一意图权威；issuance 归属进程内验证（跨主机 token 等 buyer）。",
  "fields": [
   {
    "n": "IntentId",
    "t": "string",
    "d": "唯一意图标识",
    "req": "必"
   },
   {
    "n": "Kind",
    "t": "enum",
    "d": "Observe | Act | Recovery",
    "req": "必"
   },
   {
    "n": "EffectClass",
    "t": "string?",
    "d": "act 意图的效果类（hint ≠ binding）",
    "req": "选"
   },
   {
    "n": "TargetSubject",
    "t": "string?",
    "d": "目标提示",
    "req": "选"
   },
   {
    "n": "BasisRevisionId",
    "t": "string",
    "d": "依据的世界观版本（currentness 判定输入）",
    "req": "必"
   }
  ]
 },
 {
  "g": "act",
  "no": "8",
  "nm": "P23 定位查询",
  "edge": "P23 · GroundingSeam（经 P11 GroundingView）",
  "dir": "Kernel → World Model",
  "st": "keep",
  "note": "单缝双模；投影相对事实（NoCandidate ≠ KnownAbsent ≠ Ended）。",
  "fields": [
   {
    "n": "ResolveCurrent",
    "t": "模式",
    "d": "TargetDescriptor → CurrentCandidateSetResult（Unique/None/Multiple/Unavailable）",
    "req": "必"
   },
   {
    "n": "ResolveContinuity",
    "t": "模式",
    "d": "ContinuityResolutionOutcome + DemandHandle（幂等键）",
    "req": "必"
   }
  ]
 },
 {
  "g": "act",
  "no": "9",
  "nm": "P9 候选绑定",
  "edge": "P9 · Candidate Binding",
  "dir": "Grounding Provider → EB",
  "st": "keep",
  "note": "候选 ≠ canonical（不变量 24）。",
  "fields": [
   {
    "n": "TargetSubject / TargetValue",
    "t": "string",
    "d": "字符串通道（UI 工厂置 null! 压制）",
    "req": "必"
   },
   {
    "n": "SourceRevisionId",
    "t": "string",
    "d": "候选依据版本",
    "req": "必"
   },
   {
    "n": "IsAmbiguous",
    "t": "bool = false",
    "d": "歧义声明",
    "req": "选"
   },
   {
    "n": "UiTarget",
    "t": "record?",
    "d": "UI 通道：OccurrenceId + SourceRevisionId + LogicalItemId?（仅溯源）——UI 通道必带",
    "req": "选"
   }
  ]
 },
 {
  "g": "act",
  "no": "10",
  "nm": "P10 正式绑定",
  "edge": "P10 · Canonical Binding",
  "dir": "EB → Assurance",
  "st": "fix",
  "fixTag": "S4",
  "fix": "S4：撕掉 locator 三字段；P14 成为 locator 唯一来源。",
  "fields": [
   {
    "n": "BindingId / IntentId",
    "t": "string",
    "d": "绑定身份 + 意图关联",
    "req": "必"
   },
   {
    "n": "EffectClass",
    "t": "string",
    "d": "效果语义",
    "req": "必"
   },
   {
    "n": "TargetSubject / TargetValue",
    "t": "string",
    "d": "沿载（UI 通道为 occurrence id 字符串）",
    "req": "必"
   },
   {
    "n": "RevisionId / RevisionNumber",
    "t": "anchor",
    "d": "钉死世界观版本（currency 派生失效）",
    "req": "必"
   },
   {
    "n": "TargetOccurrenceId",
    "t": "string?",
    "d": "UI 通道恒绑 occurrence",
    "req": "选"
   },
   {
    "n": "OwningContainerId / LogicalItemId",
    "t": "string?",
    "d": "容器与连续性溯源",
    "req": "选"
   },
   {
    "n": "TargetLocator / TargetNative / TargetSpace",
    "t": "✖ S4 移除",
    "d": "投递材料不该进绑定载荷——安检从不读；Core 投影改从 DeliveryTarget 取（消费面已查清：全仓两处）",
    "req": "—"
   }
  ]
 },
 {
  "g": "act",
  "no": "11",
  "nm": "P13 裁决",
  "edge": "P13 · Assurance Judgment",
  "dir": "Assurance → Gate",
  "st": "keep",
  "note": "Gate 只执法不重判；拒绝可观察有 reason。",
  "fields": [
   {
    "n": "IntentId + BindingId + RevisionId",
    "t": "三元组",
    "d": "correlation key，任一变即重裁",
    "req": "必"
   },
   {
    "n": "IsAdmissible",
    "t": "bool",
    "d": "可否做",
    "req": "必"
   },
   {
    "n": "Checks",
    "t": "list",
    "d": "逐项留痕",
    "req": "必"
   },
   {
    "n": "RejectionReason",
    "t": "string?",
    "d": "拒绝首因",
    "req": "选"
   },
   {
    "n": "Freshness",
    "t": "FreshnessJudgment",
    "d": "消费相对充分性三态——拒绝 ≠ 绑定失效",
    "req": "必"
   }
  ]
 },
 {
  "g": "act",
  "no": "12",
  "nm": "闸门 + 台账",
  "edge": "Effect Gate + Reliable Execution Source",
  "dir": "EB 内部",
  "st": "keep",
  "note": "崩溃后可对账，绝不盲补发。",
  "fields": [
   {
    "n": "GateDecision",
    "t": "(Allowed, Reason)",
    "d": "只执行或拒绝既有授权",
    "req": "必"
   },
   {
    "n": "journal 登记",
    "t": "append-only",
    "d": "binding/judgment/请求/执行端/准入摘要——先登记后执行，提交失败零 driver 调用",
    "req": "系"
   },
   {
    "n": "未决发现",
    "t": "Pending Attempt",
    "d": "无 ID 发现未决；LinkRetry / LinkCompensation 关联",
    "req": "系"
   }
  ]
 },
 {
  "g": "act",
  "no": "13",
  "nm": "P14 派工单",
  "edge": "P14 · Dispatch Request",
  "dir": "EB → driver",
  "st": "keep",
  "note": "S4 之后这里是 locator 的唯一来源；driver 固定消费自身支持集，不挑选不 fallback。",
  "fields": [
   {
    "n": "DeliveryTarget",
    "t": "record",
    "d": "OccurrenceReference（仅溯源，永不参与执行）+ Spatial? + Native? + Space?",
    "req": "必"
   },
   {
    "n": "EffectClass",
    "t": "string",
    "d": "来自绑定",
    "req": "必"
   },
   {
    "n": "Parameters",
    "t": "string?",
    "d": "当前沿载字符串；typed family = 未来 buyer",
    "req": "选"
   },
   {
    "n": "RevisionId",
    "t": "anchor",
    "d": "绑定依据版本",
    "req": "必"
   }
  ]
 },
 {
  "g": "act",
  "no": "14",
  "nm": "P15 投递结果",
  "edge": "P15 · Dispatch Result",
  "dir": "driver → EB",
  "st": "keep",
  "note": "UnknownOutcome 唯一合法后继 = 重新观察，永不盲补发。",
  "fields": [
   {
    "n": "Outcome",
    "t": "enum 三态",
    "d": "DeliveryCompleted | DeliveryFailed | UnknownOutcome（认知状态，非成因）",
    "req": "必"
   },
   {
    "n": "Report",
    "t": "string",
    "d": "provider 报告（描述性 provenance）",
    "req": "必"
   },
   {
    "n": "CompletedAt",
    "t": "datetime",
    "d": "下游确认时间",
    "req": "必"
   },
   {
    "n": "Reason",
    "t": "string? = null",
    "d": "成因诊断（不锁词汇）：timeout-killed / device-offline / …",
    "req": "选"
   }
  ]
 },
 {
  "g": "act",
  "no": "15",
  "nm": "回执回流",
  "edge": "P2 · AttemptReport",
  "dir": "EB → Evidence Ledger",
  "st": "fix",
  "fixTag": "S1",
  "fix": "S1：回流侧 origin 与 P2/P3 同源闭合。",
  "fields": [
   {
    "n": "ReceiptId / IntentId / BindingId",
    "t": "string",
    "d": "attempt 关联",
    "req": "必"
   },
   {
    "n": "TargetSubject / RevisionId / RevisionNumber",
    "t": "anchor",
    "d": "目标与版本留痕",
    "req": "必"
   },
   {
    "n": "Outcome / Report / DispatchedAt",
    "t": "同 P15",
    "d": "",
    "req": "必"
   },
   {
    "n": "Reason",
    "t": "string? = null",
    "d": "成因诊断",
    "req": "选"
   },
   {
    "n": "Origin",
    "t": "＋ S1 新增",
    "d": "回流侧统一携带：经 receipt↔observation 精确关联推导为 PostActionSelf；AttemptReport 永不满足 MaterialEffect（不变量不变）",
    "req": "必"
   }
  ]
 },
 {
  "g": "verify",
  "no": "16",
  "nm": "验证输入",
  "edge": "Kernel 编排 → Assurance",
  "dir": "Kernel → Assurance",
  "st": "fix",
  "fixTag": "S2",
  "fix": "S2：期望载荷独立成字段（ADR-0049）；新增 effect 类型不再硬编码期望。",
  "fields": [
   {
    "n": "TargetSpec",
    "t": "record",
    "d": "目标规格（含状态断言）",
    "req": "必"
   },
   {
    "n": "Slice",
    "t": "record",
    "d": "当前 scoped 只读投影",
    "req": "必"
   },
   {
    "n": "ProcessedObservations",
    "t": "list<KernelResult>",
    "d": "本轮 P2/P3 处理结果",
    "req": "必"
   },
   {
    "n": "TargetDisappearedAfterRouteChange",
    "t": "bool = false",
    "d": "特设",
    "req": "选"
   },
   {
    "n": "PriorRoute / CurrentRoute / ScrollContentChanged",
    "t": "✖ S2 迁移",
    "d": "滚动期望三硬编码布尔 → 迁移为 RouteStable ∧ ContentChanged 实例后删除",
    "req": "—"
   },
   {
    "n": "ExpectedEffect",
    "t": "＋ S2 新增",
    "d": "两族封闭谓词：StateBecomes(subject,value) 状态型；ContentChanged / RouteStable 变化型——验证的显式对照物；修复后无期望载荷 = RED",
    "req": "必"
   }
  ]
 },
 {
  "g": "verify",
  "no": "17",
  "nm": "MaterialEffect 判定",
  "edge": "Assurance 内部（S5 立法点）",
  "dir": "Assurance",
  "st": "fix",
  "fixTag": "S5",
  "fix": "S5：明文规则 + 锚定测试（仅身份连续、无谓词对照的验证 = 拒绝）。",
  "fields": [
   {
    "n": "判定输入",
    "t": "Evidence + ExpectedEffect + Origin",
    "d": "（S1+S2 交付后齐备）",
    "req": "必"
   },
   {
    "n": "立法",
    "t": "＋ S5 新增",
    "d": "受 P22 prior 影响的关联连续性不得作为 MaterialEffect 唯一依据——对照物必须是显式期望谓词 + origin 输入",
    "req": "系"
   }
  ]
 },
 {
  "g": "terminal",
  "no": "18",
  "nm": "P7 义务定义",
  "edge": "P7 · Obligation Definition View",
  "dir": "Run Model → Assurance",
  "st": "keep",
  "note": "定义/状态分离已冻结（ADR-0047）。",
  "fields": [
   {
    "n": "义务定义",
    "t": "identity/kind/subject/required value/mandatory",
    "d": "只表达\"要证明什么\"；满足状态是 Assurance 的独立投影",
    "req": "必"
   }
  ]
 },
 {
  "g": "terminal",
  "no": "19",
  "nm": "P16 终局证明",
  "edge": "P16 · Outcome Proof",
  "dir": "Assurance → Run Model",
  "st": "keep",
  "note": "预算耗尽终局在 S3 生效后到的是 policy 铁顶（非静默缺省）。",
  "fields": [
   {
    "n": "Classification",
    "t": "四分类",
    "d": "Completion | Failure | SafeStop | Escalation（证据不足 = 无 proof，不伪装）",
    "req": "必"
   },
   {
    "n": "obligation statuses + evidence refs",
    "t": "list",
    "d": "支撑清单",
    "req": "必"
   },
   {
    "n": "unresolved uncertainty + reason",
    "t": "",
    "d": "诚实边界",
    "req": "必"
   }
  ]
 },
 {
  "g": "terminal",
  "no": "20",
  "nm": "P17 终局记录",
  "edge": "P17 · Terminal Outcome State",
  "dir": "Run Model → Kernel",
  "st": "keep",
  "fields": [
   {
    "n": "OutcomeState",
    "t": "快照",
    "d": "exact-prior single-winner；至多一次；terminal 后冻结不可复活",
    "req": "必"
   }
  ]
 },
 {
  "g": "terminal",
  "no": "21",
  "nm": "P18 交卷信封",
  "edge": "P18 · Runtime Outcome",
  "dir": "Kernel → UniAgent",
  "st": "keep",
  "note": "exactly once；发出后投递闸门永久关闭。",
  "fields": [
   {
    "n": "RunId / OutcomeProofId",
    "t": "string",
    "d": "运行与证明引用",
    "req": "必"
   },
   {
    "n": "Classification",
    "t": "四分类",
    "d": "",
    "req": "必"
   },
   {
    "n": "Obligations",
    "t": "list<status>",
    "d": "义务结算",
    "req": "必"
   },
   {
    "n": "Basis/Effect/Situation EvidenceIds",
    "t": "set",
    "d": "证据引用三层",
    "req": "必"
   },
   {
    "n": "UnresolvedUncertainty + Reason",
    "t": "",
    "d": "解释性元数据（productSessionId 🔵 走 ⑨ 轨道）",
    "req": "必"
   }
  ]
 },
 {
  "g": "terminal",
  "no": "22",
  "nm": "P19 评价",
  "edge": "P19 · Goal Evaluation",
  "dir": "UniAgent → user/session",
  "st": "keep",
  "note": "只评不改——不回写任何执行事实。",
  "fields": [
   {
    "n": "EvaluationId",
    "t": "确定性派生",
    "d": "Goal+Run+Proof 幂等",
    "req": "必"
   },
   {
    "n": "Satisfaction × Disposition",
    "t": "枚举",
    "d": "Satisfied/Partial/Unsatisfied × Final/NeedsFollowUp；Undetermined ⇒ NeedsFollowUp",
    "req": "必"
   }
  ]
 }
];
