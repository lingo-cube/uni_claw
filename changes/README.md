# changes/ — Change State（每个 Change 的长期真相）

> ADR-0007。Change State 回答 **WHAT are we changing / WHY / WHAT has been
> decided / WHAT counts as done**。不是 Plan（HOW→plans/）、不是 Ticket、
> 不是 WorkItem（委派→workitems/）、不是 ADR（架构 WHY→docs/adr/）。

## 状态机（8 状态 + 正交 disposition）

```text
UNDERSTAND → RESOLVE → PERSIST → PLAN → IMPLEMENT → REVIEW → VERIFY → CLOSED
                                                  ↖ failure edges ↙
```

```yaml
# state.md 头部约定
lifecycle_state: understanding|resolving|resolved|persisted|planned|
                 implemented|reviewed|verified|closed
disposition:     none|blocked          # BLOCKED 是 disposition，不是状态
```

失败边：Review 实现缺陷→IMPLEMENT；Review/Verify 语义假设缺陷→RESOLVE
（修订本文件，revision 留痕）；Verify 实现缺陷→IMPLEMENT；Verify 环境
失败→留 VERIFY 有界重试，无解 → `disposition: blocked` 并记录恢复条件。

## 三档持久化

| 档 | 载体 | 条件 |
|---|---|---|
| MINIMAL | 仅结构化 commit message | 无需跨会话恢复；一旦中断/跨 session **自动升级 STANDARD** 补建 `changes/<id>/state.md` |
| STANDARD | `changes/<id>/state.md` | 默认档 |
| DECISION-HEAVY | 同上 + 扩展字段 | 架构/协议/生命周期/重大 Human Decision |

### 模板（STANDARD）

```markdown
# <change-id> — <一句话>
lifecycle_state: <…> · disposition: none · depth: standard · base: <git-rev>

## Intent（WHAT/WHY）
## Scope / Out of Scope
## Decisions
## Acceptance
## Constraints
## Verification（见下方声明格式）
## Status log（每次状态转移一行：日期 · from→to · 依据）
```

DECISION-HEAVY 追加：Assumptions / Alternatives（含被拒）/
Owner-Authority impact / ADR refs / Residual risks。

## 验证声明（STANDARD+ 必含）

```yaml
verification:
  level: CONTRACT|DETERMINISTIC|SCENARIO|ENVIRONMENT   # 非产品侧 E0-E4
  method:    <命令 或 场景/trace 过程描述>
  expected:  <预期结果>
  actual:    <实际结果——CLOSED 时必填>
  evidence:  <命令输出 / scenario / trace 引用>
```

只有链接 ≠ 可复现验证；四元组缺一不可（命令型可紧凑写）。

## Resume（跨会话进入）

1. 读 state.md → 2. 复验：git 实况 vs 声明 base；evidence 文件存在匹配；
   lifecycle_state 与仓库一致 → 3. 一致续行 / 矛盾回 UNDERSTAND。

## MINIMAL 升级触发

中断恢复、跨 session 续做、需要 review 留痕——任一发生即升级 STANDARD
并在 state.md 首行注明升级来源 commit。
