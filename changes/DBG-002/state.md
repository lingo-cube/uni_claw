# DBG-002 — Consistency Command Includes WMP-003 Invariant Tests
lifecycle_state: closed · disposition: implemented · depth: standard · base: b2cf77e

## Intent（WHAT/WHY）
DBG-001 落地的唯一诊断命令的确定性集合仍停留在 WMP-002 范围；WMP-003 新增
的容器索引不变式测试（含 stale-position canary）与「stale 索引」类怀疑直接
相关，纳入后诊断路由的守卫面完整覆盖 ADR-0018 冻结的全部 realization 边界。

## Scope
- `scripts/world-model-consistency.sh`：filter 增
  `WorldModelIndexInvariantTests`；两处注释更新（集合定义 + 变更同步提示
  指向 WMP-002 D1 与 WMP-003 Scope）。
- `references/world-model-consistency.md` §2：测试集描述同步。
- 本 state（纯 Harness 小改动，行为验证记录于 Verification，无独立
  evidence 文件）。

## Out of Scope（禁止）
- 不改测试、不改 src/、不改上游 skill、skills-lock、UniFlow 生命周期；
  不改命令契约（exit 语义/输出透传/根定位均不变）。

## Decisions
- D1 集合只增不改：WMP-002 四类全保留，新增第五类；canary 断言的是现状
  契约（GREEN），纳入不改变命令的 GREEN 语义边界。

## Acceptance
- A1 命令 GREEN：exit 0、47/47（44 + 3 invariant），WMP-INVARIANT 行可见。
- A2 命令契约其余面不变（任意 cwd、输出透传、非零即 RED）。
- A3 只提交 3 文件。

## Constraints
- 文件面：script + reference + state。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: 主树运行唯一命令（exit code + 通过数 + WMP-INVARIANT 行可见性）
  expected: exit 0，47/47，invariant 行在 detailed verbosity 下可见
  actual: exit=0；通过数: 47；WMP-INVARIANT transitions=29 countStable=27
    continuity=1 containers=3 可见
  evidence: 本 state Verification（无独立 evidence 文件）
```

## Status log
2026-09-10 · enter→closed · base=b2cf77e（WMP-004 之后）；单轮完成：filter
  扩展 + 注释/reference 同步 + 命令验证（47/47 GREEN）；RED 路径不受影响
  （FDP 为断言消息，任何 verbosity 透出，WMP-002 期已双组验证）。
