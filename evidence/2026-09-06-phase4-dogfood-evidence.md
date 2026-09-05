# V2 Dogfood Evidence — friction 观察簿

> 依据 Next Phase Directive Phase 4。每条：现象 / 频次与示例 / 影响 / 可能
> 原因 / 分类（structural defect | implementation defect | optimization
> candidate）。只记实际发生且有执行证据的条目。

| # | 现象 | 频次/示例 | 影响 | 可能原因 | 分类 |
|---|---|---|---|---|---|
| D-1 | worktree 内 Architecture guard 全灭（`.git` 是文件，helper 只认目录） | 17 测试，Phase 3 全量两次复现（WI-P3-001 分类证据） | worktree 内委派执行无法跑 guard——委派隔离与验证门冲突 | 测试基建 RepoPath 判定未兼容 git worktree | implementation defect（产品测试基建；修复属产品线） |
| D-2 | 弱价值委派的 ceremony 风险：Phase 1 Path D（2 文件机械修复）委派收益边际 | 1 次（自身复盘） | 委派开销 > 收益；正是 directive 警告的「为了 Gate 而 Gate」 | conformance 覆盖需求驱动路由而非真实判据 | 流程观察（非缺陷）：Phase 4 起路由仅按判据；无代码动作 |
| D-3 | Leader 直接编辑遭遇 file-staleness 重试 | 3 次（installer 扫描 / subagent 并发改 mtime） | 每次多一轮 read-before-edit | 并发修改者触碰 mtime | implementation detail（重试即恢复，无需动作） |
| D-4 | disable-model-invocation 型 skill（grill-with-docs / setup-matt）不进 DSH catalog | 恒定（by design） | 无（用户显式调用型） | 上游 frontmatter 语义 | 非摩擦（记录澄清） |
| D-5 | grilling / grill-with-docs 在全部 dogfood 任务中无真实 buyer | 至今 0 次触发 | 覆盖缺口：人工交互式 Explore 路径未验证 | 本会话任务均无歧义到需要质询 | NO_REAL_BUYER（不强迫使用） |
| D-6 | Completion Gate 在全部真实路径均可依证据自动闭合 | 12/12 次（P1 四路径+P2+P3） | 正面观察：无卡死 | — | 正面（directive 观察项：「多数可自动闭合」= 是） |
| D-7 | Human Gate 零触发 | 0 次（唯一例外=所有者主动裁决 Phase 3 豁免，属正常 Human 决策而非 gate 升级） | 无过度触发 | — | 正面 |

## 待观察（Phase 4 任务进行中追加）

- Gate 1 判定困难频率：至今 0 次（所有任务 Gate 1 十项均可判定）。
- Gate 1/2 重复感：无（两 Gate 内容正交：问题清晰性 vs 执行就绪）。
- WorkItem 尺寸：WI-P1-B/D/P3-001 均单 commit 尺度 ✓。
- SubAgent conversational backfill 需求：0 次（三份自包含 prompt 均独立完成）。
