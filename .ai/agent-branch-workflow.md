# Agent 并行工作区隔离（worktree / feature branch）

> 定位: 跨 Host 共享协议之一（DSH / Codex / 用户在同一本地 clone 并行时的隔离规则）。
> 上级: AGENTS.md「Where Is Truth」；触发: 需要 ≥2 个 agent 同仓并行时。
> 版本: 2026-08-29（依据本会话实证：主工作树曾出现非本次任务的并发改动，
> 提交被迫依赖白名单规避——机制缺失，非纪律问题）。

## 问题

同一本地 clone 只配一个工作树（working tree）。当两个 Host 并行工作时，后动手者
的改动会落在**同一个工作树**里，形成"神秘改动"：提交时既不知道归属，也无法区分
"我的"与"别人的"。靠自觉（改动声明日志）不可靠——Git 的机制隔离才是业界标准。

## 规则（默认串行 → 并行）

| 场景 | 做法 |
|------|------|
| 单个 agent 串行 | 现状不变：直接在主分支工作树开发（分支随意，rubber-stamp 无强制） |
| **≥2 个 agent 同仓并行** | **每个 agent 使用独立 worktree + 独立分支**，禁止共享同一工作树 |

## 机制：git worktree（官方特性，零污染）

```bash
# 为“dsh”这个 host 建独立工作区：
bash scripts/agent-worktree.sh dsh
# → 工作目录: <repo>/../uni_claw-dsh ，分支: agent/dsh

cd ../uni_claw-dsh
# 正常开发。提交都落在 agent/dsh，永远不会触碰主工作树。
```

- 共享同一 `.git` 对象库（代码不重复），但**工作树完全独立**；
- 主工作树保持干净 → 并行 agent 的任何改动都进不了别人的目录；
- 与 `source_revision` 内容指纹正交：任一 worktree 的 `validate` 都过
  （规则文件内容一致即可，与所在分支/提交无关）。

## 合入主分支（rebase 保持线性）

```bash
# 在主工作树（或任意 worktree 的 main 分支）：
git fetch . agent/dsh          # 把 worktree 分支拿进来
git rebase agent/dsh           # 或: git merge --rebase agent/dsh
bash scripts/verify-before-commit.sh   # 合入后、提交前必跑全门
```

## 提交门哨兵

`scripts/verify-before-commit.sh` 会提示"工作树存在未暂存/未跟踪条目
（非本次提交内容）"——见到该提示先确认归属：

- 是**并行 agent** 的工作 → 用独立 worktree/分支，把这个工作树让出来；
- 是**本任务的遗漏文件** → `git add` 后重跑。

## 与既有治理的协同

- current-gates / latest 投影、workitem 联动：在改动发生处（任一 worktree）跑
  `scripts/finalize-change.py` 后**提交一次**即可，投影跟分支走，合入时自然带上；
- archive-workitems / sync-profile-pin：同样按"改动发生在哪就提交在哪"处理。
- 不要在同一 worktree 里切来切去（`git switch` 两个分支会互相覆盖未提交改动）；
  需要第二个分支时再建一个 worktree。

## 何时不需要

单 agent 串行、或并行 agent 明确各自负责**不相交文件集**且每次开工前确认
`git status` 干净时，可不建 worktree（零义务，纯当灾难预案）。