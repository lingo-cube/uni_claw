# Proposal: Project Cleanup

## Why

uni-claw 项目根目录积累了大量临时脚本和文档（15个 Python 文件，9个 Markdown 文件），这些文件缺乏组织，使得项目结构混乱，难以维护。同时，测试文件分散在不同位置，单元测试和集成测试混在一起，不利于代码组织。本项目清理旨在建立清晰的项目结构，提高可维护性，并建立长期维护规范防止未来继续积累无用文件。

## What Changes

- **根目录临时文件清理**: 移动根目录下的 15 个临时 Python 脚本和 9 个临时 Markdown 文档到正确位置或归档
- **测试结构重组**: 将单元测试从 `tests/` 移至对应的 `src/模块/` 目录，集成测试保留在 `tests/`
- **Git 状态清理**: 提交已删除的 288 个 `.traces/*.jsonl` 文件
- **配置更新**: 更新 `.gitignore` 和 pytest 配置以支持新的结构
- **长期维护规范**: 创建开发工作流程文档，建立临时文件管理规范

## Capabilities

### New Capabilities

- `project-structure`: 定义清晰的项目目录结构和文件组织规范
- `testing-structure`: 定义单元测试和集成测试的分离和组织方式
- `development-workflow`: 定义临时文件管理和清理流程

### Modified Capabilities

无（本项目清理不改变任何功能规格要求）

## Impact

- **受影响的目录**: 根目录、`src/`、`tests/`、`scripts/`、`docs/`
- **测试执行**: pytest 配置需要更新以支持 `src/` 中的单元测试
- **CI/CD**: 可能需要更新测试路径配置
- **文档**: 新增 `docs/DEVELOPMENT_WORKFLOW.md` 规范文档
- **兼容性**: 不影响现有功能，仅文件位置移动

---

**清理范围**:
- 15 个根目录 Python 脚本（移动、归档或删除）
- 9 个根目录 Markdown 文档（整合或删除）
- 288 个已删除的 trace 文件（提交清理）
- 测试文件重组（约 20+ 个文件移动）

**预计时间**: 约 45 分钟
**风险等级**: 低（仅文件移动，无逻辑变更）
