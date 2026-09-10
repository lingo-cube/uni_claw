# DBG-001 — AI-Coder World Model Diagnostic Route（uniclaw-debug-evidence 最小路由 + 唯一诊断命令）
lifecycle_state: closed · disposition: implemented · depth: standard · base: 672cc7a

## Intent（WHAT/WHY）
WMP-002 留下了常驻的 canonical 安全网（oracle 测试 + First-Divergence 报告），
但一个**不带本会话上下文**的 AI Coder 在遇到 World Model 一致性怀疑时，
没有标准路径知道「先建 tight loop、再跑哪一条命令、证据按什么等级取」。
本 change 在 `uniclaw-debug-evidence` 本地扩展内加最小路由：一个 reference、
一个唯一 agent-runnable 命令、SKILL.md 一个入口段，使诊断路径可被发现、
可重复、可由 fresh agent 无上下文执行。

## Scope
- `.agents/skills/uniclaw-debug-evidence/SKILL.md`：仅增加「何时加载 World
  Model consistency reference」的简短入口（不改 Composition Contract、不新增
  lifecycle/完成语义）。
- `.agents/skills/uniclaw-debug-evidence/references/world-model-consistency.md`：
  详细稳定知识（路由条件、E-level、FDP→evidence packet 映射、单命令、
  边界与误用警告）。
- `.agents/skills/uniclaw-debug-evidence/scripts/world-model-consistency.sh`：
  唯一 agent-runnable 命令；任意 cwd 定位仓库根；调用 WMP-002 确定性测试集；
  成功 0 / 任一 mismatch 非零；不吞输出；不改产品状态。
- `changes/DBG-001/state.md` + 新 evidence。

## Out of Scope（禁止）
- 禁止修改：上游 `diagnosing-bugs` skill、`skills-lock.json`、UniFlow 生命周期、
  AGENTS.md、`uniflow` SKILL.md、任何 `src/` `tests/` 产品与测试代码。
- 禁止：第二套 task system / debug lifecycle / completion 语义；diagnosing-bugs
  继续唯一拥有通用反馈循环，uniclaw-debug-evidence 只拥有 UniClaw 证据语义，
  UniFlow 继续唯一拥有状态推进与完成判定。
- 禁止：命令依赖网络诊断数据源、人工点击、Host session id、Codex/DSH 专有
  工具或未提交文件；禁止 Product Runtime src 依赖 .agents / script / WorkItem /
  Host adapter。

## Decisions
- D1 单命令 = `bash .agents/skills/uniclaw-debug-evidence/scripts/world-model-consistency.sh`，
  内部 `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj
  --filter <WMP-002 确定性集合>`（oracle + materialization probe + performance
  + benchmark probe）。退出码即裁决；测试输出原样透传（FDP 行直接可见）。
- D2 脚本从自身路径向上定位仓库根（AGENTS.md + UniClaw.Kernel.slnx 标记），
  与调用者 cwd 无关。`dotnet restore` 仅消费已声明的 NuGet 包（标准开发机
  缓存）；命令本身不访问任何远程诊断数据源——reference 中如实说明。
- D3 证据等级：World Model 属 stateful module → 最低 E2（状态快照 + 操作
  历史）；问题进入 Runtime/Grounding 链（perception→admission→reconcile→
  consumer view 链路）→ 升 E3（trace + 状态转移 + 观测 + 决策记录）。
- D4 绿色结果的语义边界必须在 reference 中显式声明：GREEN 只排除已覆盖
  fixture 的索引/COW/顺序分歧，不证明不存在未知错误；性能问题先跑固定
  probe（WMP-002 materialization/performance 集），禁止用普通日志推断。
- D5 临时日志纪律沿用 diagnosing-bugs：`[DEBUG-<id>]` 前缀，完成前删除。

## Acceptance
- A1 标准 Skill discovery（AGENTS.md → skills 清单/目录）可发现更新后的本地
  扩展与三个新面（SKILL 入口、reference、script）。
- A2 唯一命令在 clean isolated checkout 可重复运行；正常实现 GREEN（exit 0）。
- A3 定向破坏一个 fixture/oracle 比较时 RED（非零）且输出稳定 FDP；恢复后
  GREEN。
- A4 fresh disposable agent（不继承本会话结论，仅给最小问题）能从 AGENTS/
  UniFlow 路由到 diagnosing-bugs → uniclaw-debug-evidence，找到并运行唯一
  命令，返回包含 E-level、failure class、lifecycle stage、last correct state、
  First Divergence、Owner、remaining uncertainty、escalation 的 evidence packet。
  只验证 DSH Host，只报告该 Host 的实际结果。
- A5 Product Runtime src 不依赖 .agents、script、WorkItem 或 Host adapter
  （grep 证明）。
- A6 REVIEW（code-review 双轴）与 VERIFY 分离；只提交 DBG-001 精确文件。

## Constraints
- 文件面仅限 `.agents/skills/uniclaw-debug-evidence/` 内三个文件 + 本 state +
  新 evidence。
- 不改 Product Runtime、不改测试、不改上游 skill 与共享层。

## Verification
```yaml
verification:
  level: SCENARIO
  method: >-
    唯一命令多 cwd 运行（仓库根 / /tmp / 子目录 / 仓库外）+ clean isolated
    checkout（精确提交树 a6e6564）命令与全量 + 定向破坏 RED→GREEN 两组
    （主破坏 + Spec 评审独立破坏）+ fresh disposable agent dogfood（DSH）
    + src 依赖 grep 证明 + 双轴 REVIEW
  expected: >-
    A1-A6 全满足；GREEN exit 0（44/44）；破坏 exit 1 且 FDP 稳定；恢复 GREEN；
    fresh agent 无上下文正确路由并返回全字段 evidence packet；上游 skill 与
    skills-lock byte 级未动；isolated 全量 GREEN
  actual: >-
    A1-A6 全满足（详见 evidence）：命令 exit 0/44/44（多 cwd）；仓库外 exit 2；
    两组破坏均 exit 1 + 稳定 FDP → 恢复 exit 0；dogfood（仅 DSH）路由完整、
    packet 八字段齐全、零文件修改；src grep 无命中；Standards 零硬违规 +
    Spec 两缺陷已修复（detailed verbosity / NU1900 措辞）；isolated 精确树
    Kernel 276/276 + Agent 17/17 GREEN
  evidence: evidence/2026-09-10-dbg-001-world-model-diagnostic-route.md
```

## Status log
2026-09-10 · enter→understanding · base=672cc7a（WMP-002 已 CLOSED 提交）；
  主树 dirty 仅四项已知并发/无关（CONTEXT.md、changes/PER-004/、两 untracked
  文档），DBG-001 目标面零 dirty；skills-lock 无本地扩展条目（无需改）。
2026-09-10 · understanding→resolved→persisted→planned→implementing · 三面
  落地（SKILL §6 入口 / reference / script）；修复脚本 echo 引号 parse 错误
  （首轮验证曾误以管道尾状态当脚本退出码，已改用显式 exit 捕获）。
2026-09-10 · implementing→reviewing · A1-A5 逐一验证（多 cwd GREEN 44/44、
  仓库外 exit 2、定向破坏 RED+稳定 FDP→恢复 GREEN、isolated 可重复、src
  grep 无命中、fresh agent dogfood 全字段 packet）。双轴 REVIEW 启动。
2026-09-10 · reviewing→verifying · Standards=零硬违规（set -e rationale 与
  filter 重复知识两处 touch-up 已落）；Spec=两缺陷已修复（probe 行默认
  verbosity 不可见 → 脚本 detailed verbosity，实测 WMP-MAT 7 行/WMP-STAGE
  10 行可见；NU1900 网络措辞如实修正）。VERIFY：精确提交树 a6e6564 isolated
  全量 Kernel 276/276 + Agent 17/17 GREEN，命令 exit 0。
2026-09-10 · verifying→closed · A1-A6 满足；仅提交 DBG-001 五文件；并发会话
  dirty（CONTEXT.md、changes/PER-004/、两 untracked 文档）全部保留未动。
