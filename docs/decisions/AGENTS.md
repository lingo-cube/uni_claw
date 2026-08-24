# docs/decisions/ — Decision, Analysis, Result, Casebook

> 本目录保存所有 Architecture Decision Records、技术分析报告、实施结果记录
> 和调试案例库。**这不是 architecture manual**（顶层架构只有
> `docs/architecture/README.md` 指向的 Architecture v1 是唯一权威基线）。

## 文档类型

### Decision

用途：记录经过确认的设计决策。

- 可以作为后续设计依据
- 包含 Decision / Constraint / Boundary
- 变更裁决时按 `AGENTS.md §2 Authority Order` 第三优先级引用

### Analysis

用途：记录问题调查过程。

- 提供证据和推理链
- **不一定代表最终决策**（Analysis 可能指向多个方向的结论，只有 Decision 冻结结论）
- 可为后续 Decision 提供输入但不替代它

### Result

用途：记录任务完成情况。

- 描述修改内容（Minimal Change）
- 验证结果（Test Result / Regression）
- Remaining Risk
- AuthorityDelta / ArchitectureDelta 声明

### Casebook

用途：记录已解决问题的工程经验（`runtime-debugging-casebook/`）。

- 帮助 AI 理解类似问题的 Reality Gap 和 First Divergence Point
- **不产生架构约束**（不替代 Decision）
- **不引入 Authority**（引用时须重新确认问题是否具有相同 Reality Gap）

## 目录结构

```
docs/decisions/
    AGENTS.md                          ← 本索引文件
    <decision-name>.md                 ← Decision / Analysis / Result
    runtime-debugging-casebook/
        AGENTS.md                      ← 案例库索引
        <case-name>.md                 ← 调试案例
```

## 快速定位

当需要查找历史决策/分析/案例时：

- 按问题域搜索：`grep -ril "<topic>" docs/decisions/ --include="*.md" | grep -v AGENTS.md`
- 优先引用**明确标注 Decision 的文件**（Analysis 需要确认结论是否已被 Decision 冻结）
- 案例库（`runtime-debugging-casebook/`）用于类似问题识别，不用于架构论证

## Document Creation Rules

创建新文档前：

1. 检查是否已有**相同主题**的 Decision / Analysis / Result
2. 判断是更新已有文档（同一问题重复出现）还是创建新文档（不同问题）
3. 避免产生重复 analysis/result（一个完整分析应只产生一份 Result，除非后续发现新证据）

### 禁止

- 将 trace dump 直接作为 Decision（trace 只是证据，不是决策）
- 将临时 debugging notes 作为架构规则（notes 未经过 Gate 验收）
- 将 casebook 内容当作新的 authority（案例只提供经验，不产生架构约束）