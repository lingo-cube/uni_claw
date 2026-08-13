# 技能目录（人读版）

> 这里只收录**人会用到的** skill，按「人如何触发」组织。
> 机器内部协议类 skill 只在最后一节一句话带过，不给细节。
> 每个 skill 的权威定义仍是它自己的 `SKILL.md`；本目录是导航，不是副本。

## 一、人直接要结果（重点）

| Skill | 在哪 | 人怎么用 | 一句话 |
|---|---|---|---|
| `perception-model-intelligence` | 本仓库 `.claude/skills/` | 「当前感知模型是什么状态？」「/perception-status」「这些训练文件哪些有用？」 | 把感知平台的机器真理解释成人话；只读，零发布权威。生成 `platforms/perception/reports/` 三份报告 |
| `host-test-runner` | uni-claw `.claude/skills/` | 「跑一下 Host 集成测试」 | 启动模拟器 + 视觉服务，执行 Host 集成测试全生命周期 |
| `module-test` | uni-claw `.claude/skills/` | 「跑 <模块> 的单测」 | 执行模块单测，智能处理失败并记录决策过程 |
| `trace-analysis` | uni-claw `.claude/skills/` | 「帮我查这个失败 run 的原因」 | 用 TraceTool CLI 对失败 run 做根因排查 playbook |
| `trace-visualization` | uni-claw `.claude/skills/` | 「把这个 trace 画出来」 | 把 trace 画成层级树和状态机视图 |
| `validation-documentation` | uni-claw `.claude/skills/` | 「生成验证报告」 | 按统一命名/格式生成标准化验证报告 |
| `design-doc-sync` | uni-claw `.claude/skills/` | （实现前自动触发） | 实现前把 PRD 同步进模块设计文档 |
| `test-extraction` | uni-claw `.claude/skills/` | 「从设计文档提取测试场景」 | 从设计文档自动提取测试场景并生成测试代码 |

## 二、OpenSpec 四件套（自然语言触发）

两个仓库各有一份同源 skill；人不需要记命令，说人话即可
（触发语表见 [AGENTS.md](../../AGENTS.md)）：

| Skill | 人说 | 干什么 |
|---|---|---|
| `openspec-propose` | 「按 OpenSpec propose <变更>」 | 生成完整 proposal/design/specs/tasks |
| `openspec-apply-change` | 「按 OpenSpec apply <change>」 | 按 tasks.md 实施并逐项勾选 |
| `openspec-explore` | 「按 OpenSpec explore <主题>」 | 只探索需求、澄清方案，不改代码 |
| `openspec-archive-change` | 「按 OpenSpec archive <change>」 | 归档 change、提取 decisions、同步主规格 |

## 三、用户级技能（`~/.claude/skills/`）

| Skill | 人怎么用 |
|---|---|
| `using-git-worktrees` | 「开个 worktree 做这块」——功能开发前自动确保隔离工作区 |
| `module_test` | `module-test` 的用户级别名 |
| `frontend-design` | 只在要做前端页面/组件时用 |

## 四、内置技能（随 Claude Code 自带，一句话）

`run`（启动并驱动本项目应用）、`init`（初始化 CLAUDE.md）、
`security-review`（安全审查）、`dataviz`（画图前必读的图表规范）、
`update-config`（配置自动化行为/hooks）、`loop`（定时循环执行任务）。
官方文档为准，本目录不展开。

## 五、机器内部协议（人不需要触发，知道存在即可）

| Skill | 一句话 |
|---|---|
| `brainstorming` | 任何创造性工作前必须过设计对话的硬门槛（所以 Claude 总先问问题） |
| `e2e-diagnose` | E2E 失败自动诊断编排（Haiku 驱动 host-test-runner） |
| `trace-collection` / `workflow-trace-collection` | Mock 测试/OpenSpec 流程里自动收集 trace 资产 |
| `trace-to-simulation` | 从真实 run 产物按 FSM 时序构建仿真测试用例 |
| `state-machine-integration` | trace 数据与状态机集成（格式化为状态迁移） |
| `fsm-battle` | FSM 双轨对战（需求驱动 vs 分析器）验证机制 |
| `wf-apply` | 工作流驱动任务执行（OpenSpec + Haiku/Opus 编排） |

## 六、与机器真理的关系（只对 perception-model-intelligence 相关）

`perception-model-intelligence` 及其 helper **只读** canonical 机器真理
（manifests / EvaluationRun / identity 工件），唯一写入是三份人读报告：

```
机器清单 = 真理；人读报告 = 解释真理，永不创造真理。
训练指标零发布权威。
```

依赖方向：canonical 机器系统 X→ 人读报告；任何生产/训练/评估/发布
代码不得引用该 helper。
