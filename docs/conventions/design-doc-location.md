# Design Document Location Convention

> 项目约定：各类设计文档的存放位置规则。
> 最后更新: 2026-07-30

## 规则

不同文档类型有各自的存放目录，不可混放。

| 文档类型 | 目录 | 命名规范 |
|---------|------|---------|
| 重构 / Phase 实现设计 | `docs/refactor/` | `XX-<topic>-design.md`（XX 为递增序号）或 `YYYY-MM-DD-<topic>-design.md` |
| PRD 文档 | `docs/prd/` | `YYYY-MM-DD-<topic>-prd.md` |
| 系统宪章（Constitution / Patterns / Layers / Decisions） | `docs/system/` | 按四层体系，见 `docs/system/README.md` |
| 项目约定（本文档所在） | `docs/conventions/` | `<topic>.md`（kebab-case） |
| 测试相关 | `docs/testing/` | `<topic>.md` |
| 验证报告 | `docs/validation/` | `<topic>.md` |

## 反例

- ❌ 不要把重构设计文档放到 `docs/superpowers/specs/`（那是外部 skill 约定，不对齐本项目）
- ❌ 不要把 PRD 放到 `docs/refactor/`

## 迁移

如果 AI 工具误写到错误位置（如 `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`），应移动到正确目录后再提交。若 `docs/superpowers/specs/` 为空则删除目录。

## 来源

- Memory: [[design-doc-location]]
- 相关: [[ai-coding-charter]]（宪章四层文档体系在 `docs/system/`）
