# World 图关系、Slice 前后变化与语义推断检查

日期：2026-10-21。对象：uni-harness 当前工作区，HEAD=bce0ec3a981a4598b889bb0bf37d2b3e389aa41c；其中 World、UniKernel、Host、CONTEXT、协议存在其他会话未提交修改。本报告描述检查时文件，不将它们归于本次修改。

## 做了什么

核对 World 唯一事实所有权与当前调用链，复跑导航/滚动/关联/迟到处理相关测试，并用构造输入检查普通页面身份声明的时序、冲突及 Host 签名稳定性。仅新增本检查 Change，未修改产品、既有测试或共享协议；没有新设备操作、Jev 调用或实现授权。

结论：**已有容器图、World 内关联缝、导航验证入口与同页滚动观察关联，不需要再建图。缺口主要在“新语义证据如何与既有观察关联并决定当前适用性”，以及当前身份判别、冲突处理、签名来源和事后验证强度。**

## 原来的问题：几类对象容易被混在一起

| 对象 | 当前语义与代码 | 不能据此推出什么 |
|---|---|---|
| ContainerIdentity | World 对持续 UI 实体建立的身份；不是标题、截图或 detection id | 同标题不必同实体；不同入口也不自动成为不同实体 |
| ContainerRelation | SourceContainerId→TargetContainerId 的关系 belief，存于某 WorldBeliefRevision；Contains/Overlays/NavigatesTo | 端点不是 Slice，也不是 revision；revision 字段是发生/依据锚 |
| 导航关系 | 两端容器已由 accepted 观察确立，再由 Kernel 在 Assurance 验证后请求 World 登记；保留前后 revision、观察依据、scope 与验证引用 | 不表达父子层级、可逆性、再次操作一定到达、预期页符合性；一次成功也不证明永久可达 |
| ViewportTransition | 同一 ContainerId 的前后 revision/观察，被已验证实际滚动关联；可有二维位移和锚对应 | 不是跨页边、LogicalItem 连续身份、当前可执行 locator；成功滚动不保证位移可测 |
| WorldBeliefRevision | 历史只读的 canonical belief 版本；登记导航/滚动关系也会发布新 revision | revision 数字不是 capture 序号；关系登记 revision 不等于目标观察 revision |
| Kernel Slice | 某 revision、某容器范围的 immutable Consumer View；当前载荷是 occurrences/scoped claims | 不是原始观察，不是页面身份，不拥有变化/关系写入权；旧 Slice 不因保留而仍可执行 |
| WorldGraphProjection | Owner 按 revision/scope 派生图视图；当前 Edges 只投影 NavigatesTo，另投影 ViewportTransitions | 当前 projection 不能当成完整 ContainerGraph；未投影 Contains/Overlays 不证明世界不存在这些关系 |

定位：`src/UniClaw.Kernel/World/UiRealization/ContainerAssociation.cs:25`、`WorldBeliefRevision.cs`、`ViewportTransition.cs`、`Slice.cs`、`WorldModel.cs:110/208/1253/1501`。CONTEXT 的 Core Slice 上位语义比 Kernel Slice 宽；本检查的“Slice 仅为投影”专指 P4 与当前 Kernel/UI realization，不能反过来改写 Core 术语。

两个 Slice 可以用于展示 before/after，但比较应携带来源 revision、capture、scope、空间与证据关联。不能仅凭元素数/标题变化生成导航边；跨 revision 历史拼图仍是诊断材料，不产生新的当前 Slice。

## 这次修改：只记录职责与设计约束

当前合法链路是：

```text
原始输入 → Perception 观察声明(P2) → Ledger admission → accepted Evidence(P3)
→ World relevance/reconciliation → 身份/关系/当前 belief → Slice(P4)/Owner 消费视图
Control 意图 → grounding/Assurance → Effect 实际投递
→ 新观察再入 P2/P3/World → Assurance 事后验证 → Kernel 请求 World 登记有依据关系
```

| 工作 | 提案/输入来源 | 接纳与裁决者 |
|---|---|---|
| 页面用途、文本、元素类型、页面前后语义差异 | Semantic Perception，带 capture/时间/范围/来源及支撑引用的观察声明 | Ledger 接纳记录；World 判相关性及 belief；模型分数不是真值 |
| 关联已有容器/新建容器 | World 内部 IAssociationStrategy；消费 accepted 观察、previous revision、可选 P22 prior | WorldModel gates；外部 Semantic Perception 不直接提交 canonical id 或覆盖 AssociationDisposition |
| Contains/Overlays 候选 | World 内部策略基于 accepted evidence 推导 ProposedRelation | WorldModel 的关系接纳；不是 Perception 直写图 |
| 已发生跨页导航 | accepted 前后观察 + 独立事后验证 | Assurance 验证；Kernel 编排；World RecordObservedNavigation 接纳 |
| 实际动作、绑定与投递结果 | Effect Boundary 的 canonical binding/receipt | Effect Boundary；DeliveryCompleted 只证明投递层结果 |
| 动作是否达到声明预期 | Control/任务给出期望；当前观察给出事实 | Assurance 判断；Goal satisfaction 另归 UniAgent |

现有 ProductAssociationStrategy 只识别 `ui.screen`，以 Host 字符串签名精确匹配已有容器，非空未匹配即提议 New；它不消费 P22 的排序信息，也不理解页面用途/语义差异。其 `Relations` 始终为空。新 Jev 能答题不意味着这些消费分支已存在。

### 前页、被操作元素、动作与预期要分源

| 信息 | 正确来源/类别 | 允许用途与限制 |
|---|---|---|
| 前页已接纳身份 | 指定 pre-action revision 的 World 引用 | 历史依据；不能默认等于当前页；给模型的是只读事实摘要/引用，不能交出 mint 权 |
| 前页名称、用途 | 指定历史 capture 的观察或已接纳语义声明 | 标注人工/模型/设备来源与原观察时间；不能伪装实时设备识别 |
| 元素类型与名称 | pre-action occurrence 的 accepted 依据；若人工补充则明确标注 | “菜单项”属于语义推断，clickable 本身不证明导航、安全或子页 |
| 实际被操作对象与动作 | canonical binding、具体 attempt/receipt、dispatch time | 说明实际投递了什么；点击计划与实际点击必须分开 |
| 预期页/用途/目标状态 | 任务、Control/计划意图 | 可作验证对照物；不得进入 P22，不得作为观察/身份依据 |
| “符合预期” | 观察事实对显式期望的比较结果 | 不可反向替代页面身份判断或直接建边；最终验证权仍属 Assurance |

ADR-0012/P22 明确禁止 expected next page、desired outcome、Control plan。当前 SlowConsultationRequest 也冻结为不携带 expected value。上游若决定让语义模型同时读取意图，必须显式修订它负责的协议并区分观察结论与符合性评估；不能塞 Reason、自由 JSON、签名或 world claim 绕行。World 侧不另定第二份载荷。

### 各种变化应如何区分

| 情形 | 当前已有表达/判定入口 | 尚未证明或不能推导的内容 |
|---|---|---|
| 进入另一页 | accepted 身份变化、route 变化、事后验证后 NavigatesTo | “进入子页”的父子语义不在 NavigatesTo 中；路由变了不证明到了期望页 |
| 返回 | 独立观察重关联已有容器，再记录反向 NavigatesTo；Host 有 Navigate up 特例 | 反向边不是自动推导；返回动作不证明层级包含、任意系统 Back 或恢复原状态 |
| 同页更新 | 同容器、revision/occurrences/state 变化；无跨页边 | 尚无通用页面语义变化分类器；同 route 不保证实体相同 |
| 同页滚动 | VerifyPostActionEffect → RecordObservedViewportTransition；World 配准 accepted 前后区域内锚 | 缺重叠/重复锚/重排/空间不一致时仅保留关系与 Unknown 原因，不猜位移 |
| 覆盖层 | Contains/Overlays 类型与 ProposedRelation gate 存在；当前 Host 提交 ui.overlay.popup=present/absent 并保留底页签名 | 产品策略没有实际创建 overlay 容器和 Overlays 边；当前图投影也不输出这两类关系；popup claim 不等于完整遮挡/交互焦点模型 |
| 未知/歧义 | AssociationDisposition.Insufficient/Ambiguous；claim epistemic 状态是另一条轴 | 不能为了推进而强选身份/建边；现实现继承旧 current anchor，不等于本轮已重新确认当前页 |

“页面是否相同”“观察发生了哪类变化”“该次动作是否满足预期”是三个问题。未知不能折叠为同页、进入子页或操作失败。导航实际发生与符合目标也可能分别成立/不成立。

## 验证结果

### 当前重跑：60/60 通过，覆盖的是现有行为

```sh
dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --filter 'FullyQualifiedName~WorldObservedNavigationTests|FullyQualifiedName~WorldViewportTransitionTests|FullyQualifiedName~UIWorldAssociationTests|FullyQualifiedName~ProductAssociationStrategyTests|FullyQualifiedName~SlowConsultationTests|FullyQualifiedName~KernelRunDriverNavigationGraphTests' --logger 'trx;LogFileName=world-semantics-kernel.trx' --results-directory /tmp/uniclaw-world-semantics-20261021
dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore --filter 'FullyQualifiedName~WorldCaptureIsolationTests|FullyQualifiedName~WorldOperationReplayTests|FullyQualifiedName~SettingsPageIdentityTests' --logger 'trx;LogFileName=world-semantics-host.trx' --results-directory /tmp/uniclaw-world-semantics-20261021
```

| method | expected | actual | evidence |
|---|---|---|---|
| Kernel 六组测试 | 已验证进入/返回建边、无验证不建边、非法依据拒绝、prior 不单独铸造身份、滚动配准/未知等 | 51 passed，0 failed | evidence/world-semantics-kernel.trx |
| Host 三组测试 | 重放真实 Kernel 操作链、录制帧无法配准时保留未知、迟到 hierarchy 字段不替换当前元素 | 9 passed，0 failed | evidence/world-semantics-host.trx |
| 仓库外最小探针 | 核对普通身份声明是否有普遍时序/冲突门槛，以及真实 occurrence 更替对签名的影响 | 三项缺口均复现，详见下表 | evidence/probe-results.jsonl、probe-reproduction.md |

测试运行中有 NuGet 漏洞缓存访问警告；构建与测试本身成功。没有扩跑全套测试，也没有修改既有断言。

本次文档写入前后比对的116个被检查文件哈希一致，见 `evidence/preservation-check.json`。该哈希快照记录于完成初查后、写本 Change 前；它不冒充整个会话开始前的完整工作树快照。新建检查产物的 Markdown 未发现行尾空白。

### 历史证据的边界

- WRC-003 提供确定性 Runtime/Kernel 操作重放及导航、滚动关系消费，当前 state 仍为 implemented，独立评审尚未执行，不能引用为已 CLOSED。其外部页面和投递为替身，不是当前真机/Jev 联合运行证明。
- `evidence/jev-page-explicit-context-20261021/report.md`：原15道无歧义题15/15，历史落地题4/4，构造“仍为前页/其他页”8/8。只有4个历史页面、人工提供前页/菜单类型/预期、单次响应、题目存在意图提示；是离线语义/符合性实验，非独立识别准确率，更非实际 Effect 成功率。
- 未找到本次新语义感知→accepted evidence→World 身份判别→Assurance→图关系的当前设备证据。本次没有补做该集成或宣称已证明。

## 新发现：真实缺口与限度

| ID | 输入 → 当前输出 | 原因/定位 | 证据等级与影响 |
|---|---|---|---|
| W1 | A(t1) → B(t3) → 迟到 A(t2)，同 producer/不同 capture scope → CurrentContainer 从 B 回 A；FreshnessBasis.AsOf 仍为 t3 | WorldModel.cs:422/447 同 producer 异 scope 按到达顺序 Revise；Associate 随后处理旧 ui.screen，无 capture 当前适用性 gate | 探针已复现。accepted 历史证据能回退当前身份；max capture time 不能修复此问题，也不代表该身份当前充分新鲜 |
| W2 | 同 producer、同 scope 的 ui.screen=A 与 ui.screen=B → 原 claim 保留 A 并记录 Conflict，但同时 New(B)，CurrentContainer=B | ApplyClaim 与 Associate 分离；ProductAssociationStrategy 不读取现有冲突/矛盾依据，New 只需当前证据引用 | 探针已复现。冲突留痕不等于身份已被保护；现有 contradiction-blocks-matched 只覆盖策略显式列出的反证，不能证明 New 也安全 |
| W3 | 从相同 root、相同 observed route 进入相同入口，但本轮实际 target id 从 occ:network:1 换为 occ:network:2 → 两个不同签名 | SettingsPageIdentity.cs:32 将 deliveredTarget 写入签名；live feed 传的是 receipt 的 occurrence id；World occurrence 每轮新铸，ProductAssociationStrategy 按签名字节精确匹配 | 纯函数探针已复现签名差异；结合产品策略可推导身份碎片风险，未冒充真机重访复现。现有重复入口测试使用稳定 ui.element:Battery 字符串，未覆盖真实 occurrence 更替 |
| W4 | observedRoute==currentRoute → 无条件沿用签名；overlayPresent 时也沿用底页签名 | SettingsPageIdentity.cs:16/20 | 代码/纯函数结果。支持常见滚动，但无法区分相同 route 指纹的不同内容语义页；不能将沿用签名当作已证同实体 |
| W5 | typed hierarchy 旧 capture 字段被挡；普通 ui.screen/语义 claim 仍进 P2→World | UiHierarchyOccurrenceStrategy.cs:53 的保护只作用于带 Hierarchy metadata 的分支；SlowResultProjector/SlowConsultation 的 IsLate 不进入 World 时序判定 | Host 2 个既有迟到案例通过，但不覆盖 W1。Slow 迟到测试只断言 IsLate、admitted、无直接 Effect，未断言不污染当前身份/关系 |
| W6 | 当前无 DesiredState 的非 swipe 验证依赖目标在位，或“事前唯一目标消失且 route 改变”；不核对语义预期目的地 | UniKernel.cs:468-583，RuntimeAssurance.cs:63-125 | 代码检查。当前 IsVerified 不能重新命名为“已到正确子页”；新语义模型回答符合预期不能直接替代该验证链 |
| W7 | 导航建边验证引用是字符串；与滚动相比，导航登记没有同等严格的 receipt/binding/dispatch-time 核对 | UniKernel.cs:567-581 对比 544-564；ObservedNavigation 不含 operation id；RecordObservedNavigation 检查端点、依据、revision 顺序、当前容器，但不要求 target revision 等于 current revision | 代码差异，未复现真实运行时错误建边。串行 driver 缓解常规路径；异步语义接入前需明确操作关联、重访/迟到同身份的语义，不能声称全面时序安全 |
| W8 | Contains/Overlays 有类型与 gate，产品策略没有产出；relations 随 revision 继承，未找到临时覆盖层关系的撤销/失效路径 | ProductAssociationStrategy.Relations 始终空；WorldModel.Associate 的关系循环仅追加；DeriveGraphProjection 仅选 NavigatesTo | 类型/测试存在；未证明产品 overlay 生命周期或完整包含图。后续有真实买方再设计生命周期，不能先把旧关系永远视为当前 |

W1/W2 的探针在新 World 中直接调用现有 Ledger/World 公共入口，没有调用未实现的 Jev 功能，也没有模拟模型准确率。W3 的 Host 纯函数使用 reflection 仅因函数 internal；未绕过或修改产品 guard。

当前导航 gate 会拒绝无效端点/证据、反向 revision 顺序、同容器导航、当前容器不等目标等；association 策略的 NavigatesTo proposal 被明确丢弃。以上缺口**不等于已经证明迟到结果可无条件造导航边**。准确结论是：身份可受污染已复现，完整操作因果关联及语义冲突对建边的保护尚未证明，不能依赖现有60个通过测试消除此风险。

另一个边界风险是 Host 把实际 target id/path 与观察内容合成 ui.screen 后送入 accepted evidence，而 World 直接用合成字符串决定 New/Matched。这里没有 expected page，但实际动作上下文已不只是 P22 prior。后续需要将独立观察支撑和实际上下文分源核验，避免“包装成观察”让动作来源取得身份确立效力；本报告不替所有者宣布该混合形式为已接受的新协议。

## 下一步：沿已有缝补齐设计，暂不实施

1. 与 JEV-SEMANTIC-CAP-001 对齐一个载荷设计：独立观察结果、依据与缺失/冲突；若允许读意图，则符合性评估单独表达。World 提出所需 correlation/依据约束，上游负责唯一跨组件形状，本 Change 不另造 Jev 专属接口。
2. 身份侧先写可判定案例：同页刷新/滚动、同名不同语义、同实体多入口、同入口重访、B 已当前后 A 迟到、相同 capture 冲突。先定观察充分性/当前适用性，再设计 ProductAssociationStrategy 内部判别；不能将摘要相等或路径 hash 当成实体定义。
3. 将记录保留与当前适用性分开：过期结果可以留存为历史依据，不自动重设 current anchor；同页晚到有用信息也须绑定原 capture/revision，在 Owner 确认适用后才影响当前。无依据/有冲突时保留 Unknown/Ambiguous/Insufficient，不能造新身份来消除冲突。
4. 验证侧分别验“实际发生了变化”“该变化与本 attempt 有关联”“是否满足显式预期”；目的地身份/语义由观察支撑，符合性归 Assurance。导航/滚动登记应保留真实操作及验证引用，不将模型的成功标签直接当授权。
5. 首先复用 ContainerRelation、IAssociationStrategy、RecordObservedNavigation、ViewportTransition、Owner 派生投影；只有明确的父子/覆盖层生命周期买方和反例要求既有形状不能表达时，再评审增量扩展，不另建图或把 Slice 变成事实入口。

以上是候选设计约束，未冻结字段、阈值、错误码或新关系词汇。现有 Owner、P22 和 Slice 语义足以完成本轮核验，无须让用户重复裁决既有规则。若后续要求冻结“相同页面”的产品等价标准、导航父子关系或新的意图辅助协议，应按 grill-with-docs 在查完事实后集中询问当轮真正未定的产品取舍；当前不以本报告猜测代替这些裁决。
