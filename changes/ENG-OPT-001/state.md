# ENG-OPT-001 — 重叠任务收口、集成基线与组件工程优化
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: bce0ec3a981a4598b889bb0bf37d2b3e389aa41c

## Intent（WHAT/WHY）
用户于2026-10-21明确授权本会话作为Leader按顺序推进：页面补证与滚动重放收口 → 集成基线和旧任务核验 → 数据所有权/只读封装修复 → 验证职责归位 → 有界目录优化。解决审查发现，同时不与现有实施会话争写，不把局部通过当全仓通过。

## Scope / Out of Scope
In：跟踪WORLD-OBSERVATION-001已有会话的真实验收；WRC-003独立评审与所需处置；当前集成基线；已证明的Run/Ledger输入别名和World读取逃逸修复；Kernel/Assurance判定归位；导航、索引、来源与适当局部目录整理。后续各切片以自身明示Acceptance和入口指纹审查。
Out：覆盖未授权运行中的模型/代理；擅自变更Owner或扩权；强求Settings全树、跨Host完整恢复、Memory或通用能力平台；批量清理/重置脏树；提交和推送。

## Decisions
- 先前只读约束被当前实施授权替代，但历史审查记录保留。
- WORLD-OBSERVATION-001正在独立会话运行，Leader只读跟踪，不重复实现；WRC评审与元数据/导航准备可并行，真正产品编辑待重叠写入结束。
- 简单证据/导航任务可交Luna；复杂判断由Leader负责，评审按Standards/Spec独立执行。当前用户授权顺序收口涵盖必要评审，不因旧state缺模型映射而重复请求批准；不改共享model-routing或虚构DSH provider可用性。
- P1修复测试缝沿已审查公开Admit/CanonicalRecords/RevisionHistory与既有验证调用缝，不新建假测试接口。
- 旧Change只有范围Acceptance已证明才关闭；日志closed与头部不一致须核对，不能机械清账。
- 目录迁移不能改变产品语义，须同步入口路径、归属表、消费者和验证。

## Acceptance
1. 重叠任务有真实review/verify及剩余范围记录，不以本Change替代原Owner验收。
2. 集成基线可复现，已知失败逐项定性，不覆盖或隐藏并行变更。
3. admission后修改输入不改变canonical契约/证据；公开World读取不能更改Owner状态；RED/GREEN回归。
4. typed验证判断由Assurance执行，Kernel仅提供事实/顺序；原正负路径行为保持。
5. 本轮落地的导航/局部组织有单源映射、链接/路径/执法验证；未迁移范围清楚记录。
6. 每阶段区分改动、评审和验证，全部证据可回指；无未授权源码/模型/设备或发布变动。

## Assumptions
- 真实buyer和既有公开调用链限定本轮范围；不以目录体积大直接推导应新增Owner/程序集。
- World交接后的产品变化可在独立指纹下整合；历史证据记录原轮事实，不倒填为本轮重跑。

## Alternatives（含被拒）
- 拒绝在World会话写入时抢改重叠文件：等待idle/指纹交接后整合。
- 拒绝把只读接口当深拷贝或把全部集合统一Ordinal复制：使用Owner快照，保留既有不可变/排序集合比较语义。
- 拒绝把整个World聚合传给Assurance，或新增另一验证Owner：投影最小事实，同一Assurance按行为分partial。
- 拒绝继续从当前英文提示反推动作事实：Kind/Target/Text显式传递；旧入口独立兼容读取。
- 拒绝纯搬迁runs-d9后破坏旧证据路径，也不为本次发明别名平台：保留164份历史路径，补历史说明与显式新输出落点。

## Owner-Authority impact / ADR refs
- RunModel、EvidenceLedger、WorldModel原Owner不变，接纳与读取面收紧所有权，不扩权限。
- Assurance收回动作后验证定案；Kernel仅提供事实、期望和顺序，Effect唯一执行链不变。依据产品架构baseline、组件协议PER-014及原能力协议。
- CoverageLedger仍是覆盖事实/选择的同一Owner；Kind/Target为既有指令的结构化事实，不成为新动作许可。
- Identity原Store按ADR-0044/0045/0046验收；本轮不制定新ADR、不改变共享协议和Capability注册面。

## Residual risks
- 历史产物物理迁移须独立确定旧路径兼容；Driver大状态簇、RuntimeHttpServer用例拆分及Provider命名留在单独范围。
- RUN-006、AGT-019、计划泛化与Settings全树等未满足原Acceptance的任务保留开放，见evidence/eng-opt-001/open-change-disposition.md。
- 本轮确定性验收不外推为新设备/模型全场景结论；3个真实模型opt-in Facts默认未执行内部调用，既有编译警告保留。

## Constraints / Risks
共享树大量dirty；入口status/hash见evidence/eng-opt-001/。禁止reset/clean、全量暂存或覆写正在执行任务。跨组件语义分叉返回RESOLVE；目录只好看不能代替功能/边界证明。

## Verification
level: CONTRACT / DETERMINISTIC / SCENARIO
method: 独立双轴Review；公开Owner/Host调用链RED→GREEN；dotnet test UniClaw.Kernel.slnx --no-restore；场景认证/最终TRX覆盖；协议生成图审计；导航/指纹/差异格式检查
expected: 按依赖顺序完成有证据的收口与优化，原可达行为及权限边界保持，无并行改动丢失
actual: World原Owner关闭且idle后整合；WRC当前24项、Identity原Store8项补证关闭；当前8项目2024/2024；场景28/28（20认证+8描述）、认证0违规；协议生成及图审计通过；所有切片双轴Review最终0阻塞；当前切片指纹吻合、164历史资料路径/字节未变、导航0失效；本轮无提交/推送/新设备模型调用
evidence: evidence/eng-opt-001/verification.json；review.md；final-tests/results.json；scenario-coverage-final.txt；protocol-final-check.txt；final-integrity-check.json；navigation-links.json；changes/ENG-OPT-001/report.md

## Status log
- 2026-10-21 · UNDERSTAND→RESOLVE→PERSIST→PLAN · 用户授权Leader按已讨论顺序推进；确认一个功能会话active、一个重放任务需独立评审，先隔离评审和准备基线，保留所有并行文件。

- 2026-10-21 · PLAN进行中 · 完成WRC独立审查、原Store任务收口、五处导航；隔离集成基线发现并区分快照缺样本/旧配置断言/场景认证过期，保留失败输出。World独立会话仍有写入，不覆盖其产品文件。

- 2026-10-21 · PLAN准备完成 · 冻结候选8项目2005/2005通过，baseline局部patch双轴独立Review0阻塞；真实模型opt-in未开启不能计模型验收。当前World会话已取得两条high成功与错session失败证据，仍待其最终归档/停止写入。

- 2026-10-21 · PLAN→IMPLEMENT · WORLD-OBSERVATION-001原Leader已关闭且会话idle，最终72文件指纹无漂移；合入局部baseline patch保留high新测试，当前8项目2014/2014通过（Simulation移5项至Host.Dsh，合计覆盖不减少），28场景认证检查通过。保存optimization-entry.tar.gz及508文件指纹，开始所有权纵向修复。

- 2026-10-21 · IMPLEMENT→REVIEW（所有权切片） · Run/Ledger/World共8条公开回归RED→GREEN，初始相关40/40通过；Spec发现ImmutableHashSet比较器回归，已补IImmutableSet/SortedSet保持及RED→GREEN4/4，复审阻塞关闭。World测试中一次冲突值必胜的错误假设已纠正，仅验证只读与Owner追加。
- 2026-10-21 · IMPLEMENT（验证职责） · 页面身份/事后时序/typed checked定案移入同一Assurance Owner的PostAction局部文件；Kernel仅投影机械事实、提供期望并依序编排，不传World整个聚合。原正负路径66/66通过；去除Kernel对AdbEffectDriver分类函数依赖。

- 2026-10-21 · IMPLEMENT（内聚/组织） · Coverage种类/目标与英文表达分离，选择策略归同一Ledger的Directives局部文件，Director→当前step归属→Ledger带结构化事实；公开旧tuple Drain作为历史兼容。公开Consult/Report反例Volume > media及Scroll settings在旧冻结实现2/2失败、新实现通过；步骤产物追加kind/target事实，单独RED后修复。runs-d9无源码/测试硬编码消费者，但旧证据清单钉住164原路径；本轮保留原路径/字节并补清单、历史标识与新输出落点，不以无别名搬迁破坏旧复现。

- 2026-10-22 · IMPLEMENT→REVIEW · baseline、所有权、Assurance、Coverage按固定快照/切片分别独立Standards/Spec审查，比较器缺陷修复并补双轴复审后0阻塞；评审结论归档evidence/eng-opt-001/review.md。
- 2026-10-22 · REVIEW→VERIFY · 当前8个.NET项目2024/2024、场景28/28、协议生成及图审计通过；当前切片指纹吻合，World无意外重叠变更，历史164份路径/字节不变；保存失败与RED尝试，区分opt-in外壳和原Owner实机证据。
- 2026-10-22 · VERIFY→CLOSED · 六项Acceptance四元组已归档，导航/报告/源归属同步；历史物理迁移与旧未验收任务范围明确，本轮无阻塞Human Decision；不提交、不推送。

2026-10-22 发布补记：所有者随后明确“完成后提交推送”，覆盖前阶段仅本地交付限制；已完成代码与必要前置按隔离候选验证后发布。历史记录中的“不提交/推送”描述当时范围，不再作为当前限制。
