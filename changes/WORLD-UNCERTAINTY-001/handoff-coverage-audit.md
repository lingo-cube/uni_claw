# Q1–Q20 交接覆盖审计

范围：只核对 `discussion.md`、`state.md` 与版本、页面表达、多轮访问、可信复用、影响纠错及Replay输入材料。此表用于交接冻结，不代表实现、运行或验收已完成；18+8+12=38 是主题/场景覆盖目录，非已测 PASS。

| 题 | 已接受语义（交接不可丢） | 首期 / 延期 / 依赖 | 权威材料 |
|---|---|---|---|
| Q1 | 仅由具体观察或消费需求暴露的缺口入World；未实现能力另列 | 首期准入原则 | `discussion.md`、`world-gap-audit.md` |
| Q2 | 重启恢复已确认消耗与续授；在途效果不可对账则阻断，不自动重发、不承诺完整现场恢复 | Runtime恢复依ADR-0054；与World分开 | `discussion.md`、`state.md` |
| Q3 | 首版Replay是逐点诊断/性质与统计报告；阈值标定后再谈发布门槛，分叉后果另证 | 首期诊断；发布门槛及B2/C延期 | `discussion.md`、`replay-input-audit.md` |
| Q4 | 复用现有事件、CasePacket、复核出口；未知不自动续额、选方向或证明完成 | 首期复用；去重阈值后续细化 | `discussion.md`、`state.md` |
| Q5 | **B：四类最小完整生产消费**：身份、归属、关系依据、观察覆盖；不能只加枚举，须有依据、消费者、消除条件及验收 | 四类均首期设计/接线；World观察覆盖与Control任务穷尽分开 | `discussion.md`、`q5-design-assessment.md`、`q5-primary-sources.md` |
| Q6 | 跨capture仅在同对象、同问题、兼容范围有依据时延续；不足则新建，旧项不静默解决 | 首期延续规则；计数/销账细化依Owner | `discussion.md`、`multi-visit-completeness.md` |
| Q7 | 持续未知在安全决策边界主动复核；不打断在途、不重置期限/额度、不自动动作 | 首期触发语义；阈值/冷却/去重待细化 | `discussion.md`、`state.md` |
| Q8 | 记录独立补证尝试及结果分布；传输重试、证据重放不另计 | 首期可见语义；关联键和阈值细化 | `discussion.md`、`agent-information.md` |
| Q9 | 每次咨询给当前相关且未解决项；解决/不相关变化可追溯，移出视图不等于解决 | 首期最小消费；筛选/截断规则细化 | `discussion.md`、`agent-information.md` |
| Q10 | 有新结果才再次主动提醒；未知存续与任务零推进是不同事实 | 首期提醒边界；去重/阈值依Q7细化 | `discussion.md`、`state.md` |
| Q11 | 旧资料按可证明范围辅助诊断；完整输入未证则不做同输入优劣比较、不猜缺字段 | 首期限制Replay样本资格；新样本完整留存另需证据 | `discussion.md`、`replay-input-audit.md` |
| Q12 | 入口用途识别/动态遍历有既有范围与证据边界；未知用途不自动变成Skip | 当前World切片不扩为全遍历；依Goal映射与Owner | `discussion.md`、`goal-completion-mapping.md` |
| Q13 | **整体Goal正式接通延期**；但四类World信息进入Agent实际决策输入仍是首期消费要求 | Goal另切片；Agent真实输入证明首期，不能把整体延期误读成Agent输入也延期 | `discussion.md`、`agent-information.md` |
| Q14 | 有依据累计多次观察形成新revision；节点/边及页面Container由多个局部Slice表达，Slice只读派生 | 首期有据累计及局部Slice；全局穷尽另属后续 | `discussion.md`、`multi-visit-completeness.md`、`page-expression-design.md` |
| Q15 | 明确归属被充分新证据推翻时正式纠错，保留历史并重判受影响依赖；不虚构导航 | 首期纠错；依赖传播及消费核查是必要边界 | `discussion.md`、`correction-impact-design.md` |
| Q16 | 复用既有Replay的单Slice页面表达，重点补World供给及多Slice组合；不重复造绘图系统 | 首期复用与组合；依赖既有Replay能力 | `discussion.md`、`page-expression-design.md` |
| Q17 | 身份、访问、来源、解释、World提交分开；局部语义修正与归属重判分轴；同固定输入引用稳定、不同范围可区分，局部内容相同不恢复旧Slice执行资格 | 首期保持职责边界；按各Owner既有链处理 | `discussion.md`、`version-layer-design.md`、`correction-impact-design.md` |
| Q18 | 合格录制+受控负例经正式World累计/纠错、局部Slice、结构/语义/状态/布局表达、Replay及Agent实际输入验证纵向链；**18+8+12=38主题不是已测PASS**，实现时展开详细用例或仿真 | 覆盖清单用于后续实现/验收；不得写成已验证或只凭单功能绿 | `discussion.md`、`state.md`、`page-expression-design.md`、`coverage-audit.md` |
| Q19 | 历史高置信是有来源且已接纳的验证依据，不是模型分数；可先供参考，慢模型不必阻塞，但动态状态/存在/位置独立取本轮依据 | 首期可信复用；当前动态状态仍由原Owner判定 | `trusted-element-reuse-design.md`、`discussion.md` |
| Q20 | 影响纠错区分仅记录、已消费、已操作；终局历史不回写；World纠错不保证真实效果可逆，效果由原Owner核查 | 首期影响追踪与受控负例；自动补偿、完整恢复、Goal接通不随之购入 | `correction-impact-design.md`、`correction-effect-primary-sources.md`、`discussion.md` |

交接冻结时至少逐项保留上表语义与阶段。范围冲突或实现证据不足时标未决并回到对应Owner；不能把“文档覆盖”替代场景证明、真实消费者输入证明或效果验证。该审计不替代独立架构审查及真实SolUltra审查。
