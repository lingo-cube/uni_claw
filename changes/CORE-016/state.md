# CORE-016 — Core 契约第二 realization tracer（跨领域可证伪性）

lifecycle_state: resolving · disposition: none · depth: decision-heavy · base: 6508de39

## Intent

用第二个领域 realization（有界测试侧 tracer、零产品代码）压测 Core 候选
模型，检验「Core 是跨领域核心」这一中心主张；为 pending gates
（非 Slice BasisReference / ResourceVersion）提供第一个真实域输入。
上游方向裁决：2026-09-20 架构推荐（2→1 顺序：第二域先于 UI 形状对齐）。

## Scope（grill round 1 定稿，2026-09-20）

- 域 = 文件系统：Segment=目录子树、Slice=列目录窗口快照、Evidence=readdir/stat、
  Claim=文件状态命题、Effect=fs 操作、TargetBinding=ResourceVersion basis
  （mtime·inode 版本键）
- 新测试程序集 `tests/UniClaw.FileSystemRealization.Tests`；**仅引用
  UniClaw.Core + 测试框架**，零 Kernel 引用（csproj 级可执法）
- 测试侧 tracer；零产品代码；不修 Core

## Decisions（grill round 1，全按建议）

| # | 决策 | 依据 |
|---|---|---|
| D1 | 域 = 文件系统（vNext.1 原生 Slice 用例，最便宜，正打 ResourceVersion 门） | grill Q1 |
| D2 | 依赖硬边界 = 仅引 UniClaw.Core，编译级排除 Kernel——独立 realization 的检验价值所在 | grill Q2 |
| D3 | 验证范围 = 表达保持 + 演化保持；C 字段门只开 ResourceVersion basis；Attempt 请求快照/执行端/授权依据保持未提供不伪造 | grill Q3 |
| D4 | 落点 = 新测试程序集（零 Kernel 依赖为 build 层事实；GEV-004 D1 / RFS-001 D23 先例） | grill Q4 |
| D5 | 反例处置 = tracer 只产证据与反例记录；Core 候选修正走后续独立 change（CORE-011 先例） | grill Q5 |

## Acceptance（grill 定稿）

1. 表达保持：文件域五记录 + ResourceVersion basis 可表达（目录/列窗/命题/操作/绑定）
2. 演化保持：新证据、更正、迟到反馈后判断一致（先更新后转换 = 先转换后更新）
3. ResourceVersion basis 真实输入；stale basis 拒绝 dispatch（CoreInvariants.CanDispatch）
4. 双向验证矩阵在第二域复刻：realization→Core 投影语义保留；Core 约束→realization 满足检查
5. 零 Kernel 依赖编译级证明（csproj 引用清单 + closure 检查）
6. 零产品代码；不修 Core；反例（若有）落 evidence + qspec 修订建议

## Verification

（实现后填）

## Status log

- 2026-09-20 · created · 方向经架构推荐 + 人批准开立；grill round 1
  frontier 五问已预分类进 shadow 台账（事件 #2），待人裁决。
- 2026-09-20 · grill-round-1-done · 人裁决 5/5 全按建议、零 override
  （台账事件 #2 已回填）；决策级 frontier 已空——剩余细节（场景集枚举、
  tracer 骨架）属可推导设计工作，归 to-spec。下一步：起草文件系统域
  realization tracer 规格（场景集 + Core 映射义务），spec 评审为下一 Gate。
- 2026-09-20 · to-spec-drafted · `spec.md` v0.1 落盘：mini-world + 投影缝
  形态（非直接构造 Core 记录）、字段级映射表（Clause 无买家不建；
  ResourceVersion 复合键 Length:LastWriteTimeUtcTicks，受控 mtime 显式
  声明）、场景集 S1–S8（对齐契约 §7 + 演化双序）、closure 执法
  （GetReferencedAssemblies 白名单 {UniClaw.Core}）。待 spec 评审 Gate。
