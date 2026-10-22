# World交接：独立推导审查先行，随后实施

日期：2026-10-23。仓库/当前项目：/Users/fran/Documents/Code/spacex/uni_claw，uni-harness。原分析base为9bc15b8974f5fb5746074e373e6c132bc4df0089；恢复时查当前HEAD、固化提交及实际diff，勿将旧hash假定为current。

## 用户授权与会话配置

用户已授权固化Change，独立会话先作场景库/流程/数学公理推导、优化及整体架构检查，之后另一会话实施；两个会话各自完成后提交推送，注意并发。本次取代此前阶段的“只设计/不实施提交”，但不扩大spec.md业务范围。无需再索取同一次实施许可；真正新增Owner/范围/不可兼容ADR冲突才带具体证据裁决。

Host配置（按用户最新修改）：推导与实现Leader均为GPT-6.1 Sol（gpt-6.1-sol）/ultra。Worker固定普通worker、fork_turns=none、显式gpt-6-luna，不用绑定5.6的luna角色，不静默换模型。每会话同时最多1名Worker，Leader可并行做独立工作，同路径单写者。实际工具返回写evidence/thread-dispatch.json；具体模型/会话id不得写入共享model-routing.yaml或WorkItem契约，后者沿required capability/tier。

## 阅读顺序

1. AGENTS.md、.agents/skills/uniflow/SKILL.md、docs/agents/work-reporting.md、changes/README.md。
2. 本线state.md、spec.md、handoff-coverage-audit.md、derivation-review-request.md及独立review产物。
3. model-interface-review.md（核心模型/接口须单列审阅）、node-edge-design.md、page-expression-design.md、version-layer-design.md。
4. multi-visit-completeness.md（MV18）、trusted-element-reuse-design.md（RU8）、correction-impact-design.md（CI12）、agent-information.md。
5. coverage-audit.md/world-gap-audit.md/goal-completion-mapping.md/replay-input-audit.md区分旧能力、延期和未证风险；需要原裁决再读discussion.md，不把完整历史塞给Worker。
6. 产品L0-L3、Inter-Component协议/UWM相关条款、ADR-0010/0011/0012/0014/0015/0017/0023/0026/0049/0052/0054及Simulation v0.1+C7修订/component delta。按当前仓库找准确路径/行号，勿猜。第三方primary-sources只作参考，不取代基线。

## 实现前置

必须先有review/scenario-derivation.md、architecture-review.md、optimization-disposition.md及readiness.json，文件内容/hash可核验。实现Leader重新读取全部结论和最终规格，核对当前diff、处置及输入漂移；不能只信ready=true或报告文字。

审查未完成、有阻塞语义/Owner/ADR项或输入已失配时不写产品。已接受范围内工程优化可由Leader有依据采纳/拒绝并留痕；公共模型/接口仍独立成稿，不把技术字段上升为新用户选择题。用户授权审查后实现，不要求普通工程事项再等一次许可。

## 实现主线与禁区

本期只做spec.md的World及必要消费：四类缺口最小闭环、节点/最小上下文边、页内局部Slice和scope-id修复、有据累计/归属纠错、版本来源分责、稳定语义参考/沿用、错误影响最小信息、已有Replay表达复用与正式Agent可见。

正式Goal、Runtime完整恢复/预算持久、全Settings/全部H7、通用导航用途/身份merge/全关系生命周期/补偿平台继续延期；Q2/ADR-0054、Q12原决定不丢失但不抢其他线实施。

先完善独立model-interface-review.md或其明确后继稿：已有/最小新增模型、Owner/producer/buyer、缝、兼容、顺序/幂等/timeout/partial failure/retry、替代方案和隐藏复杂度，再按行为纵向落plans/和WorkItem。新Registry能力先读capability-component；接口需真实buyer，不从名词造Ixxx。

先处理可判定scope-id和局部视图，再以真实单页/长列表链贯穿；顺序可依据独立review优化，不能横向堆类型/接口再补全部测试。TDD/Review/Verify及失败回RESOLVE沿UniFlow，技能按需加载。旧Occurrence/locator不因新revision或复用高置信语义复活，World不夺Assurance/Effect/Run权威。

## 实施验证

38主题是提纲，另有四类缺口、计数/持续可见/提醒、素材资格、Agent实际输入等早期验收。每条映射到详细用例/仿真/延期理由，覆盖初态/来源/步骤/中间状态/终态与不变项及四元组。不得用几个happy path或玩具World证明整包通过。

正式Kernel/World，合格录制资料+受控反例，替换外部能力而非核心规则；标real/double、testScope和验证层次。Oracle比较不喂真值，跨revision montage不产World/执行坐标。慢模型不返回时参考先可用、对应充分后不重复调用，以及首版错误共同污染后验检查均要有真实可证伪断言。

Agent沿Owner投影→AgentDecisionContext严格codec→DSH adapter/transport→实际consultationPrompt检查内容；事件/字段/不可读引用不代表Agent看见，动态内容不塞首轮能力目录。真实模型行为、离线载荷、录制和仿真证据分开。

按真实diff跑相关检查和认证并同步协议/生成物/索引；通过后不无因重复全量测试。设备/外部产品模型调用按测试基准、权限与真实必要性，不默认无限授权或预算，离线PASS不冒充live。先核对当前工具用法，tools/verify-change及scenario工具本身有并行修改，勿盲抄旧命令。

## 并发与现场保护

当前大量dirty来自Simulation、checked guard、Host和旧任务；入口见evidence/dispatch-freeze-entry.json，写前重新清点。推导只写review/及自己的证据；实现只在前置通过后写产品。每次分派明确路径/hunk归属、验收及禁止项，告知Worker不是独占仓库，不撤销他人修改。

不reset/clean/stash全仓，不git add全仓。必要共改脏文件时先核实其Owner/差异，划清负责hunk或等前置交付；可先处理不冲突切片，但不能绕过真实依赖或将未提交他人改动当本任务成果。隔离checkout按可用工具/仓库约束，不自动复制别人的未提交文件，不在旧base遗漏必要前置。

本次固化发布本线Markdown、freeze-manifest及会话路由记录，并保留本会话所写的World Knowledge Gap术语与ADR-0054。历史entry/owned.patch/before等保护快照含共享现场，仅留本地，不随本次设计提交发布；缺少远端原始素材时必须重新核实或标证据边界，不能把本地保护快照当另一环境已具备的运行证据。共享INDEX已有他人dirty，本次不重写；后续按真实负责范围和协调再生成。

模型/路由配置只在Host执行证据；WorkItem按schema，不写session id/模型名等Host字段。不要自行给无用户授权的其他聊天发消息；总控在推导完成后建立实现会话，避免产品提前写入。现有会话标题/摘要只是定位数据，不是授权。

## 提交推送及完成

用户最新追加的后续任务：本Change核心实现完成、Review/Verify、CLOSED及负责提交推送后，同一实现会话继续独立WORLD-VISUALIZATION-001，先读该Change的state.md/spec.md。可视化展示图模型、Slice、分层版本、变更来源/影响和全过程联动，复用Replay基础，用本线已有例子做实际界面验收；它是独立功能，不提前作为核心完成前置，不把核心与可视化的验收/提交混在一起。后续功能同样必须提交推送。可视化未完成时整个追加任务未完成，不在核心关闭后直接结束会话。

推导完成自己的审查文档后提交推送；实现完成负责范围及Review/Verify后提交推送。Git写入/提交/推送串行：查HEAD、index、远端和负责路径，只纳入自己文件/hunk，保护已有staging。远端更新时核实来源、正常整合，不force push、不覆盖他人提交。入口跟踪为origin/uni-harness，另有jojofran remote，勿猜。

固化和审查提交标识、实际会话配置留执行证据，避免同一提交自引用自身hash。推送后核实远端包含提交，报告commit/branch/push证据；本地commit不等于push成功。

readiness仅是实施前审查通过。产品CLOSED须范围/验收证明、文档/协议/生成物/受影响认证同步、无阻塞决定并完成负责提交/推送；生产或消费缺失不能以类型/unit绿代替交付。按programmer-style汇报具体问题、真实diff/调用链、行为与验证及未决，不省略本聊天后半Q19/Q20，不重复问已接受20题。
