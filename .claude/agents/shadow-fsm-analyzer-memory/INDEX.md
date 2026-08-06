# shadow-fsm-analyzer 记忆索引

本目录是 shadow-fsm-analyzer agent 的自建记忆（git 跟踪）。每次任务开始加载，任务结束沉淀，刷新检查时精简。

| 文件 | 内容 | 更新频率 |
|------|------|----------|
| [knowledge.md](knowledge.md) | S1-S5 分层知识蒸馏（需求→测试→设计→证据→差距） | 刷新检查时（来源文档更新 → 重读重蒸馏） |
| [lessons.md](lessons.md) | 案例经验（分析/对战/推断中验证过的事实/方法/局限） | 每次任务结束追加 |
| [fsm-design.md](fsm-design.md) | 🔑 **核心产物** — 我独立设计的 FSM 模型（不读源码，纯从需求+测试推导） | 每次有新证据修改 FSM 理解时 |
| [battle-log.md](battle-log.md) | 与 fsm-analyzer 的对战记录（共识点 / 争议点 / 结论） | 每次 battle 后追加 |
| [scripts/](scripts/) | 脚本库（需求追踪 / 测试推断 / 模型比对工具） | 新增/修改脚本时同步 INDEX.md |

## 与 fsm-analyzer-memory 的关系

**完全独立**。两个记忆目录互不读取、互不引用。唯一允许的跨 analyzer 共享是 `fsm_transition_path.py` 和 `fsm_cycle_detector.py` 脚本（它们读运行时数据，不读源码）。

## 刷新检查规则

- 每次任务开始：比对 knowledge.md 条目来源文档更新时间与记忆写入时间
- 文档更新时间取 `git log -1 --format=%ci <文档>` 与文件系统 mtime 中**更新者**（未提交的工作区改动 git 看不到，必须看 mtime）
- 文档比记忆新 → 必须重读该文档 → 重蒸馏对应条目（合并同类、删过时、压缩超长）
- 文档未更新 → 记忆为准，跳过重读；仅在任务深度需要细节时按需精读
- 有新测试文件（git diff / new files）→ 必须补充 S2 推断
- 记忆与需求文档冲突 → 以需求文档为准（需求是 ground truth）
- 记忆与测试冲突 → 以测试为准（测试是可执行规范）

## 脚本库

脚本目录：`scripts/`（git 跟踪）。脚本是 agent 自己写的——当分析模式复用 ≥2 次时，写成脚本。
脚本约定见 `scripts/INDEX.md`。
