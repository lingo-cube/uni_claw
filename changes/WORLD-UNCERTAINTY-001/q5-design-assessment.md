# Q5-B：四类未知同期开的风险与可执行边界

> Status: DRAFT / CANDIDATE（Q5范围已接受，具体载荷/计数/接线仍在设计）
> Authority: NONE（已接受边界继续以基线/ADR及Change裁决为准）
> Date: 2026-10-22

## 结论与对前轮建议的修正

用户已接受有明确边界的B：四类同轮完成语义设计和最小生产/消费/验收，并要求把相关信息给Agent。前轮将关系、范围未知整体建议延后过于保守：当前源码已有图投影unknown及DiscoveryComplete消费者。尚未统一的部分是真正缺失的世界知识如何产生、如何影响对应消费、凭什么消除，不能仅增四个枚举值就声称落地。

本判断是基于源码与一手资料的设计推论，不是实现可靠性已被测试证明。Q5/Q6/Q7范围已接受；本轮继续设计，不改产品代码或冻结尚未裁决的payload。

## 已核实的本地需求

| 类别 | 当前场景/消费面 | 当前证据及缺口 |
|---|---|---|
| 身份 | 当前capture无法关联页面，暂停补证后确认 | PageObservation.cs:24-26、WorldModel.cs:681-685已有Need；尚无四类统一条目 |
| 归属 | occurrence保留但页面归属未确认，禁止相关grounding/推进 | WORLD-OBSERVATION-001已关闭范围；当前安全屏障继续有效，不能因分类改动延迟生效 |
| 关系 | 查询当前导航图，区分未观测边与无关系 | WorldModel.cs:247-251已有navigation-graph-is-observation-bounded/no-navigation-edge-observed-in-scope字符串；它们不是完整关系缺口协议，更不证明不存在任何边 |
| 范围 | 当前候选已处理，但虚拟/滚动菜单尚未证实发现穷尽 | SettingsTraversalProjection.cs:75-77固定DiscoveryComplete=false；TraversalControl.cs:165-168记录discovery-incomplete。这里是Control发现范围消费，不得直接升级为World观察完整性的结论 |

## 业界模式及迁移边界

1. Kubernetes Condition分离type/status/reason/observedGeneration，支持多方面独立状态。[官方Pod Conditions](https://kubernetes.io/docs/concepts/workloads/pods/pod-condition/)可借鉴“缺什么”和“为什么缺”分轴及依据版本化；不照搬控制器/Pod模型，也不把generation等同本项目revision。
2. OWL明确采用开放世界假设，未声明的事实可能只是缺失而并非false。[W3C OWL 2 Primer §2](https://www.w3.org/TR/owl2-primer/#What_is_OWL_2.3F)据此，未建立关系不能当关系不存在；只为有具体查询或消费需求的未知命题建条目，不枚举所有未知关系。
3. SHACL校验显式data graph、shape及focus node/target；conforms只说明给定输入的约束校验结果。[W3C SHACL §2.1.2/§3.6.1](https://www.w3.org/TR/shacl/#focusNodes)可借鉴按具体对象/范围验收，避免从局部数据检查通过推导全世界/全任务穷尽。
4. Android AccessibilityWindowInfo暴露parent/child、root与window region等元数据。[窗口API](https://developer.android.com/reference/android/view/accessibility/AccessibilityWindowInfo)这些可作为有来源的结构/范围观察候选，不能自动等同canonical Contains/Overlays、可交互许可或完整视觉世界。[CollectionInfo](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo.CollectionInfo#getRowCount())还明确允许rowCount为-1、itemCount为UNDEFINED来表达总量未知；可以借鉴总量未知与零项分离、声明已知集合边界的方式，但总量已知本身仍不证明所有项已被观察。现有供货是否捕获这些字段仍需单独核实。

以上均非“四类未知”的业界标准或完备性证明。详细原意/限制见q5-primary-sources.md；本节由Leader复核官方原文并补入OWL直接依据。

## 四个主要风险与封口条件（候选）

1. **类型与原因混轴。** IdentityAmbiguous将“歧义”嵌入类型，但身份判别也可能因不足、过期或冲突未决。四类应描述身份/归属/关系/观察覆盖这四种问题，原因独立；既有AssociationDisposition和claim epistemic继续分轴，显式Conflict不得被折成一般Unknown。
2. **级联重复。** 身份不明可同时造成归属和关系不明。允许同一对象存在多类缺口，但保留共同依据/根因关联，补证调度不能因三条记录重复扣配额或触发三次同因咨询。分类不应强制互斥，也不声称覆盖所有未来未知；扩展须有明确版本/拒绝语义，不能静默吞未知类型。
3. **无边被误判成无关系，或关系查询膨胀。** 关系缺口必须针对具体对象/关系命题/范围及revision；不遍历所有节点对生成未知。仅有操作意图、tap回执或语义相似都不能消除导航关系缺口；仍经World接纳有依据关系，Assurance独立验证效果。
4. **观察范围与任务完成混淆（最大风险）。** World表达对具体观察区域/集合的覆盖知识；Control维护发现/遍历进度，Assurance判断合同义务。空frontier、一次XML完整或N次滚动不能单独消除观察覆盖缺口；须写明请求范围、实际观察依据、来源和适用闭包条件。全部Settings的任务穷尽不能作为World输入事实。

## 四类同期开应交付的最小纵向链

每类必须有具体producer、当前消费者、产生条件、适用范围、依据、延续/失效/解决条件及负向路径。首期可明确“未能取得足够依据，所以保持未知并阻断依赖消费”，不必承诺新provider能识别所有场景。若承诺可自动解决某类，则必须提供相应真实证据生产/消费路径，不能只有测试Oracle的答案。

| 类别 | method（拟） | expected（拟） | actual / evidence |
|---|---|---|---|
| 身份 | 复用正式Ledger→World→Control→Assurance链，覆盖不足与有效当前补证 | 未知阶段不创造身份；有效证据只更新新revision，再独立验证恢复 | 本轮未跑；已有WORLD-OBSERVATION证据仅为历史依据 |
| 归属 | 同页与弹窗/region观察下的未确认归属用例 | 保留观察，依赖归属的消费阻断；错误/迟到归属不解锁 | 本轮未跑；新增用例/trace待实施 |
| 关系 | 两端已识别但缺有效关系依据；再补入合法关系依据 | 不从缺边推无关系，不从意图/回执造边；只有正式Owner接纳后相应缺口可解决 | 本轮未跑；当前RegisterObservedNavigation及图投影仅静态检查 |
| 范围 | 明确bounded scope的部分观察/虚拟列表、以及有可验证覆盖依据的受控完整样例 | 部分保持未知；完整样例按声明范围解决；任务进度与观察覆盖不互相替代 | 本轮未跑；当前DiscoveryComplete=false仅表明现有诚实保留，未证明覆盖生产链已具备 |

共同负向验收：同Evidence重放、普通内容revision、错capture、过期、相关性不足、类型重叠不误销账、不重复咨询、不改变权限/预算；旧revision保持不变。全场景或全Settings穷尽不属于这张表已经证明的内容。

## 仍待核实/裁决

- Q5四类范围及边界已接受；四类名称、reason字段和版本化形状尚未冻结。
- 范围类应以哪个现有观察生产者提供哪些可验证覆盖依据；Control的DiscoveryEvidenceRefs有消费缝，但真实live当前固定false，不能用它反证已完整。
- 补充只读核验：HierarchyCoverage.cs:7-35已有声明面内完整/部分/未知及限制；TypedHierarchyProposalProjector.cs:69-70将该值保留到HierarchyCaptureDescriptor；UiAutomatorDump.cs:352-364对XML导出声明面设置完整。World当前尚未将这些字段接为通用观察覆盖缺口（本轮在World目录内检索该字段无消费命中）；PageCaptureObservation也没有完整覆盖字段。可以复用来源限定的输入，不能据此宣布整页、虚拟列表或任务穷尽，具体派生仍需原Owner验证。
- 关系类具体查询如何来自现有消费而不进入所有节点对扫描；导航关系可复用当前World缝，Contains/Overlays的生产/撤销仍需各自真实buyer与证据。
- 同一缺口连续性（Q6）、主动安全复核（Q7）、实际尝试/结果展示（Q8）、当前相关项展示（Q9）及有新结果才再次主动提醒（Q10）已经接受，见discussion.md。销账沿原证据标准；具体字段、关联及接线仍属后续工程设计，不在本报告暗定新协议。
