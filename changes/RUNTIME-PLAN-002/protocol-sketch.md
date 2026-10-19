# PlanTree / Traversal 协议格式展示稿

日期：2026-10-19。状态：Owner请求的字段草案，用于展示和继续讨论；不是当前已实现wire，不是字段冻结或实施授权。Owner已指出第2节when/do占位规则不足以定义节点分派，修订方向见第8节；原例保留为被指出缺口的初稿。语义依据见[流程复核稿](flow-contract.md)。

## 1. 消息分工

模型提交计划沿用kind=plan、decisionId和proposal信封，建议增加traverse节点。查询是Kernel的只读消费入口；状态/最终报告是返回视图，不是新AgentDecision决策种类。这里不绑定HTTP/RPC或Host工具名。

暂停继续仍在Kernel已有决策边界回复，建议用Kernel返回的positionRef引用执行位置，不让模型手填内部游标。所有ID、证据和数值都是示例，不是运行证据。规则、task-root和策略引用必须来自任务上下文/产品声明语义，不由模型任意编造。

## 2. 提交计划

PlanTree以proposal.items中的高层traverse节点表示，不预先展开未知页面树，也不传遍历图。

```json
{
  "kind": "plan",
  "decisionId": "decision-001",
  "proposal": {
    "planId": "plan-001",
    "version": 1,
    "items": [
      {
        "kind": "traverse",
        "nodeId": "settings-traversal",
        "scope": {
          "rootRef": "task-root",
          "boundary": "current-app",
          "maxDepth": 4
        },
        "rules": [
          { "when": "forbidden", "do": "record-reason" },
          { "when": "unknown", "do": "pause" },
          { "when": "safe-navigation", "do": "enter-subtree" },
          { "when": "observable-control", "do": "record-state" }
        ],
        "safety": {
          "policyRef": "product/android-settings/forbidden-actions",
          "policyRevision": 1,
          "mode": "read-only"
        },
        "returnTo": "task-root",
        "finishWhen": "declared-scope-accounted-and-returned"
      }
    ],
    "stageExpectation": "处理授权范围内的设置页面，记录控件和覆盖缺口，返回首页",
    "maxLocalOperations": 128
  }
}
```

深度根为0，局部工作量不增加Run授权预算。mode只收紧任务授权，引用产品策略不自动授予其中全部动作。规则匹配、优先级和冲突处理需有产品定义；safe-navigation必须有Owner判据支持，不能由模型标记或普通tap直接推出。发现、进入/返回算法由Traversal承担。

finishWhen要求声明范围内工作和缺口已交代，返回位置有依据；不是以栈空代替发现穷尽。必须覆盖的未知分支尚存时不能据此宣布任务成功。

## 3. 状态查询请求

```json
{
  "runId": "run-001",
  "planId": "plan-001"
}
```

这是逻辑查询参数，不定义新模型工具；当前Run由已有任务关联取得，不由模型在Plan中创建新Run。

## 4. 暂停状态返回

```json
{
  "kind": "traversalStatus",
  "runId": "run-001",
  "planId": "plan-001",
  "version": 1,
  "state": "paused",
  "nodeId": "settings-traversal",
  "positionRef": "position-017",
  "progress": {
    "processed": 12,
    "knownRemaining": 3,
    "unknown": 1
  },
  "pause": {
    "reason": "target-classification-unknown",
    "requiredDecision": "补充判据或修订该分支的处理规则"
  },
  "graphViewRef": "world-view-042",
  "detailsRef": "execution-details-017",
  "evidenceRefs": ["evidence-042"]
}
```

knownRemaining只是已知待处理数，不是全部未知UI的总量；graphViewRef指向World派生结构，progress仍为独立Run进度。查询只读，暂停不返回最终结果。确需模型新决策时，由Kernel既有咨询流程提供decisionId，不由查询创建。

## 5. 明确继续

```json
{
  "kind": "continuePlan",
  "decisionId": "decision-002",
  "positionRef": "position-017"
}
```

这是建议的引用式继续形状。positionRef由Kernel提供，绑定Run、计划版本和当前暂停位置，不是权限或隐式续额；错Run/版本/位置和过期引用必须拒绝。新事实使原规则重新适用后才能继续，未知未解决不能靠continuePlan绕过判据。确需改规则则提交同一planId的新version，在合法边界接替未执行部分，历史与预算保留；修订字段形状另定。

## 6. 执行结束结果

```json
{
  "kind": "traversalResult",
  "runId": "run-001",
  "planId": "plan-001",
  "version": 1,
  "end": {
    "status": "finished",
    "reason": "declared-end-condition"
  },
  "summary": {
    "processed": 15,
    "knownRemaining": 0,
    "skipped": 1,
    "unexploredBranches": 1
  },
  "detailsRef": "execution-report-001",
  "evidenceRefs": ["evidence-042", "evidence-057"]
}
```

finished表示本次执行到达声明终点，不推导全页面覆盖或Goal完成。示例有一个未探索分支，必须保留；任务证明链判断缺口是否符合原要求。预算耗尽、取消、失败等提前停止需明确原因和未完成工作，不标正常结束；具体枚举未冻结。

## 7. 当前兼容边界

已核对DecisionJsonConverters.cs：现有PlanItem封闭词汇没有traverse；ReadContinuation要求完整position对象，不接受positionRef。所以本稿不能直接发送给当前正式通道，状态/结果也尚未成为本稿描述的执行契约。

后续若采纳，需一致修订既有Plan/ContinuePlan、Owner消费投影、状态/结果及正式codec/Schema/Prompt，明确兼容与校验；不在adapter绕行。当前只检查JSON语法和文档一致性，不生成正式Schema或修改产品。

代码事实：[DecisionJsonConverters](../../src/UniClaw.Agent.Dsh/DecisionJsonConverters.cs)、[现有Plan](../../src/UniClaw.Kernel/Runtime/AgentDecision.cs)、[执行位置](../../src/UniClaw.Kernel/Runtime/PlanExecution.cs)。

## 8. Owner复核：节点匹配与处理操作未定义

Owner指出：应能描述switch记录、导航菜单点击，并询问`when=safe-navigation/do=enter-subtree`是否符合当前协议。结论：不符合。原when/do只是语义占位，缺少正式匹配、命中节点绑定及可执行操作定义；不能把JSON语法检查当成协议适配。

当前事实：

- ActItem/正式act可表达targetRole、targetDescriptor、effectClass、desiredState；候选绑定$candidate仅在既有候选迭代上下文生效。
- ObserveItem/正式observe只接受可选subject，Driver以它拉取观察；没有“按节点类型筛选并保存指定字段”的通用语义，也没有observe对$candidate的通用替换。Settings控件记录目前由Owner观察账本完成，不能据此宣称已有record节点。
- If只消费Owner公布的命名条件；Policy的封闭谓词主要面向Claim，不提供任意节点属性表达式。safe-navigation不是现有正式条件名或通用节点分类。
- 当前多数可点节点为ui.element，不能由role或clickable直接推断导航语义。导航分类须由Owner提供有依据的判据，未知不能默认为安全。

修订方向为结构化“match＋handler”，并明确定义命中节点如何绑定。以下仍为新增契约候选，尚不被当前codec支持：

```json
{
  "rules": [
    {
      "match": { "role": "switch" },
      "handler": {
        "kind": "record",
        "target": "$candidate",
        "fields": ["state"]
      }
    },
    {
      "match": { "classificationRef": "navigation" },
      "handler": {
        "kind": "act",
        "targetRole": "$candidate",
        "targetDescriptor": "$candidate",
        "effectClass": "tap"
      }
    }
  ]
}
```

此处act字段沿用现有操作形状；match、handler、record、classificationRef及Traverse内命中节点绑定均需新增正式语义，不能称为已支持。record提议为从Owner当前事实读取字段并写入本Run任务记录/报告的零Effect处理，不修改开关、不写World事实；字段未知保留未知。classificationRef须解析产品公布的只读语义判据，不是模型自行赋予的导航资格，更不是许可。

规则只决定当前节点怎样处理；导航操作是tap，经Kernel定位/授权/投递/独立验证，确认到达后Traversal负责展开子树、处理子节点和返回。enter-subtree不应被当作已有effectClass或用来隐藏整条执行链。

正式收敛尚需说明匹配词汇、候选绑定作用域、规则冲突/优先级、缺字段/多候选/未知的处理，以及record结果来源与接收Owner。D1-D8流程方向保持，字段/节点分派契约重新进入设计前沿，当前不进入实现。

## 9. 多handler能力编排方向（D9/D10已定，具体协议待定）

Owner对[第8轮](grill-round-08.md)回复“按推荐”：handler是计划中的能力操作引用，同一节点可有多个；由计划显式给出有序handlers，前一步完成后才执行后一步。Registry管理能力，不新增Handler注册表，注册次序不代表任务次序。§8单handler示例保留为演变记录，不是最终形状。

能力引用是计划意图，不等于任意Resolve/直调。当前Registry无通用operation执行面；点击仍经Kernel原Effect链，语言校验仍受既有任务绑定与诊断Finding权威约束。需要通过明确typed缝适配，不引入万能Execute。

[第9轮](grill-round-09.md)继续确认任务允许能力及不可用处置、每步作用于哪个节点/页面和观察版本。点击后的验证屏障及检查结果消费不得因同列handlers而被隐藏或转移Owner。具体字段、阶段钩位、规则冲突及失败策略尚未冻结，现有codec仍不支持本编排。

## 10. 操作级handler绑定（D11已定，时机待定）

Owner明确不同act绑定不同handlers，例如检查某一元素语言绑定在该元素操作上。因此有序列表附属于具体操作，不是整条规则共用的扁平队列。D9/D10能力引用及显式排序保持。

```text
命中元素A → 操作A（目标：A）→ 该操作的handlers：[检查A的语言，其他处理]
命中元素B → 操作B（目标：B）→ 该操作的handlers：[另一组处理]
```

上例箭头表示归属关系，不指定执行前后。绑定不等于默认在点击前/后执行，也不自动继承变化后的目标；阶段、观察关联及纯只读承载待[第10轮](grill-round-10.md)确认。自然语言“操作”尚不自动扩展现有Effect ActItem；只读检查不得隐式生成点击。此为概念形状，非现有codec协议。

## 11. 已定handler时机与能力可用性（D12-D14）

Owner接受第10轮推荐：支持操作前、操作完成并验证后两个时机，各自有序；目标与观察明确关联，目标消失不自动替换。独立只读操作同样可挂handler，不必制造点击。只选任务有效绑定能力，必需能力启动前缺失则不启动、运行中失效则暂停，显式可选项才跳过并记录。

这更新§10的待定状态，不把“已有Host post-commit hook”解释成“已支持操作级handlers”。具体typed绑定及阶段适配未实现，不开放万能hook。检查结果消费与规则命中见[第11轮](grill-round-11.md)，仍待Owner裁决。

现状补查：GuardStep已有受限动作前Host seam，语言已有专用post-commit hook；StepAct/StepVerify是内部阶段，Settings验证后flush是私有记账Action。当前没有注册式BeforeAct/AfterVerified或通用operation-hook协议，精确事实引用见第11轮；后续复用既有执行边界，补显式typed绑定，不向UniKernel加入任意hook集合。

## 12. 当前共同理解的概念结构（D1-D16）

此节覆盖前述演变稿中的待定状态；仅为概念结构，非冻结字段/Schema。D1-D16已逐项接受，整体仍待最终共同理解确认。

```text
PlanTree
  任务范围、起止位置、深度/预算、安全约束、结束条件
  有序节点规则：首个确定匹配项
    处理操作：交互或只读
      目标引用及观察关联
      操作前handlers（有序能力操作引用）
      操作完成并验证后handlers（有序能力操作引用）
      每个检查声明：仅记录／必须满足才继续

Kernel Traversal
  输入：PlanTree＋任务/Run关联
  按需读取：World派生遍历图＋Run进度＋有效约束
  持续执行：匹配→前置处理→既有操作与验证→后置处理→更新进度
  需新决策/必需能力失效/继续条件不满足：暂停原执行，保留进度与消耗
  只读查询：状态、位置、进度、暂停原因；详细依据用引用
  本次执行结束：成果、缺口、结束原因及证据；不自行证明Goal完成
```

遍历图沿用World身份与依据，不含任务进度、不反写事实。handler不得越权直调；安全逐动作独立执法，未知不能默认允许。语言检查默认仅记录；必须满足的检查不符合/未知则暂停。规则前项Unknown若影响选择不能当false跳过。未探索子树必须保留缺口，不以遍历结束冒充全覆盖。

## 13. 领域检查与Recovery职责澄清（D17-D19）

Q17完成标准接受，结果校验Capability留作后续扩展。Q18/Q19按Owner纠正：领域不符合不默认暂停，具体恢复归Recovery；每个handler的能力协议定义检查完成/通过/覆盖/有效性，Kernel负责正确执行和记录返回状态/结果，不设统一的元素检查算法或统一强制领域字段模板。

概念上应分清“handler调用完成”“能力返回不符合”“流程是否要求暂停”“Recovery如何恢复”四件事。语音不对可以是调用成功后的诊断记录。通用绑定/输入关联、调用异常处理、既有动作验证和安全边界仍保留。此为责任语义澄清，非新增wire字段或能力注册，详见[第12轮](grill-round-12.md)。

## 14. 设计持久化后的阅读边界

D1-D19及明确Owner归属已通过[架构原则评审](architecture-review.md)，持久化于[流程契约](flow-contract.md)。本文件保留协议形状的演变记录，字段级Schema/codec仍未冻结，前文“待定”不否定已接受的高层语义。

Traversal Control/Recovery均归Control Loop，driver只协调执行/生命周期；handler结果通过typed缝返回，canonical状态由原Owner记录。不存在新的Planner、Recovery执行器、Handler注册表或跨域任意Resolve通道。按Owner指令等待后续任务，本稿不触发实现。
