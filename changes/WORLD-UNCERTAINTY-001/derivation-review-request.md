# 实现前独立审查：场景、流程、公理、优化及架构

日期：2026-10-23。用户明确要求本会话先推导/审查、后续另一会话实现。生命周期沿UniFlow，不建第二套流程；模型/Worker/并发/提交推送按implementation-handoff.md的Host配置。

## 范围与输入

只读当前产品、测试、场景库、工具和协议；仅写本Change review/及review/evidence/。不改src/tests/scenarios、共享INDEX/CONTEXT/ADR/路由/工具或原录制资料，不跑设备/真实产品模型，不实施功能。允许有界只读解析/临时纯推演；若跑现有确定性检查，标清范围与真实结果，自造推演不是正式Runtime证据。

先读AGENTS/UniFlow、state.md/spec.md、implementation-handoff.md、model-interface-review.md、handoff-coverage-audit.md，再按问题读取38主题和四类缺口/Agent材料及当前场景库。并行未提交场景/工具仅是待核实现状，留hash，不当已发布固定基线；不加载全部技能或塞全历史给Worker。

## 一、场景库推理

清点scenarios/testsets/录制/装配/认证，按当前Simulation baseline、C7/component delta区分real/double、录制/受控故障及testScope。Q1-Q20→行为→当前用例→实际断言→缺失反例→建议验证层逐条映射；场景名相似不是已有断言。

覆盖MV01-18、RU01-08、CI01-12，另覆盖四类未知生产/消费/解决、实际尝试计数、持续可见/再提醒、scope-id及正式Agent输入。展开初态、身份/访问/来源/范围/时间、事件序列、中间状态、终态和不变项，明确可证伪输入及当前producer能否提供它们。有限覆盖不宣称全系统完备。

重点反例：返回重访/同页无导航更新、旧图重解释、迟到/重复/错capture、动态布局/覆盖、错归属/父上下文、无重叠/嵌套滚动、来源撤销、初始错误共同污染操作与后验检查、未决/可逆/不可逆及terminal后的效果、仅记录与已消费的差别。Oracle只比较，不直接构造产品真值。

## 二、流程及数学推导

先列域/前提/假设。可用C=Container、V=访问、O=观察、E=accepted Evidence、W=World revision、S=Slice、I=消费/意图、B=binding、D=dispatch、R=receipt、J=判断作为审查记号，不因此新增产品类型。每条公理须标基线/ADR/已接受决定来源，不能把希望得到的结论当公理。

至少分析：

- 权威唯一：canonical事实/判断由既有Owner建立；意图/期望/provider confidence/Trace/Oracle不能建立世界真值。
- 不可变/来源：W/S/receipt/terminal Outcome不回写；capture发生、处理来源和纠错接纳顺序不同；S不自证current/fresh。
- 身份/访问/范围：route/标题不足身份；同页返回可同身份不同访问，图可有环。相同固定投影输入稳定，语义范围不同无id碰撞；内容相同不等于同快照/执行许可。
- 观察累计：一观察多Slice、多观察有据累计；scope/time/frame不相容不能合成当前完整页；Unknown/Absent/Conflict/历史/已证当前不可折叠为默认值。
- 复用：合法来源、对应/适用与反证处理足够才沿用当前语义；历史参考不等慢模型，动态事实独立，无关区域不强制一起等待；高分/重复引用不证明真值。
- 幂等/计数：重复Evidence/重传/确认/纠错不制造新尝试/访问/效果；真实新失败尝试可算新结果并允许再次复核。提醒不重置预算/期限/授权。
- 纠错传播：可在有限实际依赖集合上用可达闭包/最小不动点推导受影响项，注明依赖记录完备性假设；缺边不能推出无影响。不得强加世界图DAG、认知/可信集合单调增加等不成立假设。
- 效果/终局：未发送、发送未决、已执行、terminal后不同；新认识不撤销外部效果，未决不盲重发，恢复走原Owner，旧Run不复活。每步验证自洽不保证原目标语义正确。
- 活性：说明所需合法来源可得、预算/授权足够、公平处理等假设；复用不等待无关慢模型，可取得进展。缺资源/输入时允许诚实Unknown/等待/升级，不证明总能成功或靠无限探索/自动补偿假设终止。

每项给前提/性质/推导或反例/适用边界/代码或场景锚点。可用性质测试、状态枚举或模型检查辅助，但形式证明注明有限抽象/边界与未运行部分，不虚构PASS、全局可靠性或外部恢复保证。

## 三、优化及整体架构

找减少重复识别/等待/全图重算、局部投影身份与依赖表达、影响集合定位及模块内聚的机会。每项写原具体成本/反例→建议→收益证据或待测指标→复杂度/兼容成本→接受/拒绝/延期和理由。优先复用既有capture/evidence/World/关系/continuity/Replay/咨询链，不堆新counter、置信度框架、万能query/cache/repair Owner或平台。

按AGENTS §1.5架构优先、单一职责/显式缝、高内聚低耦合/可替换、可判定验收、真实buyer扩展逐条给证据，对照L0-L3/协议/UWM/ADR和实际生产消费。检查Evidence admission、P22不接期望、freshness/currency、Owner即时派生、World/Run/Control/Assurance/Effect边界、render/Oracle非权威、Capability纪律、Harness/产品分离及延期不扩大。

核心模型/接口单列审阅：已有/最小新增信息、Owner/buyer、两adapter或candidate理由、顺序/幂等/timeout/partial failure/retry、缺失/错误语义、兼容/生成物影响、隐藏复杂度、替代/删除测试。不能从名词造Ixxx、泄漏Owner aggregate或以优化省略来源/未知/权限/终局约束。涉及新权威/范围或不兼容ADR时列阻塞，不能自改冻结架构。

## 输出与放行

review/下交付scenario-derivation.md、architecture-review.md、optimization-disposition.md和readiness.json；可另有独立模型接口稿，须被readiness引用。以行为/调用链/证据/真实差异为主，编号只索引。

readiness至少含schemaVersion、status（ready/needs_revision/human_decision_required）、reviewer、inputFiles+sha256、reviewedHead、reviewFiles+sha256、caseCoverage、axiomAssumptions、findings（severity/owner/scope/处置/证据）、blockingFindings、requiredImplementationChanges、architectureVerdict、产品验证真实边界。ready须文件存在/hash匹配、20决定/38主题及早期验收全有处置、无未决阻塞语义/Owner/架构项，不能只填布尔。

已接受范围内优化可列requiredImplementationChanges交实现Leader，明确证明/测试义务；不重复索取已授权实现许可。真正需新裁决则needs_revision/human_decision_required并给最小具体问题。实现Leader仍独立阅读/处置，不凭ready省略自身PLAN/REVIEW/VERIFY。

完成后审阅负责diff，仅提交本review/及自己的证据并推送，核实远端包含提交；共享产品/scenarios/工具及其他dirty不stage，Git写入串行、不force push。最终报告审查结论、关键反例/优化、未决及commit/push证据。不要自行向其他聊天发消息；总控待本会话结束后建立实现会话，防止产品提前写入。
