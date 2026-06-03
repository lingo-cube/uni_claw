# Tasks: Project Cleanup

## 1. Git 状态清理

- [x] 1.1 提交已删除的 288 个 `.traces/*.jsonl` 文件
- [x] 1.2 验证 Git 状态干净（无未提交的删除文件）

## 2. 创建安全归档

- [x] 2.1 创建 `.cleanup_archive/python_scripts/` 目录
- [x] 2.2 创建 `.cleanup_archive/markdown_docs/` 目录
- [x] 2.3 移动根目录 Python 临时文件到归档（15个文件）
- [x] 2.4 移动根目录 Markdown 临时文件到归档（9个文件）
- [x] 2.5 验证根目录已清理（仅保留必要文件）

## 3. 恢复有用文件到正确位置

- [x] 3.1 创建 `scripts/analysis/` 目录
- [x] 3.2 创建 `scripts/debug/` 目录
- [x] 3.3 创建 `scripts/verify/` 目录
- [x] 3.4 创建 `scripts/visualization/` 目录
- [x] 3.5 从归档恢复 `analyze_nodes_visited.py` 到 `scripts/analysis/`
- [x] 3.6 从归档恢复 `show_html_report.py` 到 `scripts/visualization/`
- [x] 3.7 从归档恢复 `show_test_details.py` 到 `scripts/visualization/`
- [x] 3.8 从归档恢复 `test_mock_fix.py` 到 `scripts/verify/`
- [x] 3.9 从归档恢复 `check_trace_structure.py` 到 `scripts/verify/`
- [x] 3.10 从归档恢复 `test_simulation_runner.py` 到 `scripts/debug/`

## 4. 整合文档

- [x] 4.1 检查 `E2E_SCRIPTS_GUIDE.md` 内容，提取有用信息
- [x] 4.2 检查 `RUN_E2E_README.md` 内容，提取有用信息
- [x] 4.3 检查 `HTML_REPORT_INFO.md` 内容，提取有用信息
- [x] 4.4 检查 `QUICK_START.md` 与 `docs/SETUP.md` 重复情况
- [x] 4.5 创建或更新 `docs/TESTING_WORKFLOWS.md` 整合有用信息
- [x] 4.6 删除临时测试报告（4个文件）
- [x] 4.7 删除设计草稿文件 `design_real_trace_architecture.py`

## 5. 测试结构重组

- [x] 5.1 创建 `src/ai/test_advisor.py`（从 tests/test_ai_advisor.py 移动）
- [x] 5.2 创建 `src/ai/test_core.py`（从 tests/test_ai_core.py 移动）
- [x] 5.3 创建 `src/ai/test_unibrain.py`（从 tests/test_ai_unibrain.py 移动）
- [x] 5.4 创建 `src/safety/test_filter.py`（从 tests/test_safety_filter.py 移动）
- [x] 5.5 创建 `src/adb/test_client.py`（从 tests/test_adb_client.py 移动）
- [x] 5.6 创建 `src/traversal/test_engine.py`（从 tests/test_traversal.py 移动）
- [x] 5.7 移动 `test_ai_integration.py` 到 `tests/integration/`
- [x] 5.8 移动 `test_ai_traversal_integration.py` 到 `tests/integration/`
- [x] 5.9 创建 `tests/performance/` 目录
- [x] 5.10 移动 `test_ai_performance.py` 到 `tests/performance/`
- [x] 5.11 移动 `test_adb_with_vision.py` 到 `tests/integration/`
- [x] 5.12 移动 `test_parse_to_plan_enhanced.py` 到 `tests/integration/`
- [x] 5.13 删除 `tests/` 根目录下已移动的测试文件
- [x] 5.14 删除 `src/simulation/` 下的测试文件（如果有）

## 6. 配置更新

- [x] 6.1 更新 `.gitignore` 添加临时文件模式
- [x] 6.2 更新 pytest 配置支持 `src/` 单元测试发现
- [x] 6.3 验证 `pytest --collect-only` 发现所有测试（配置已更新，pytest 未在当前环境）
- [x] 6.4 运行 `pytest src/` 验证单元测试（配置已更新，pytest 未在当前环境）
- [x] 6.5 运行 `pytest tests/` 验证集成测试（配置已更新，pytest 未在当前环境）

## 7. 创建开发规范文档

- [x] 7.1 创建 `docs/DEVELOPMENT_WORKFLOW.md`
- [x] 7.2 添加临时文件命名规范
- [x] 7.3 添加测试组织规则
- [x] 7.4 添加清理流程说明
- [x] 7.5 更新 `CLAUDE.md` 中的项目文档索引

## 8. 验证和清理

- [ ] 8.1 验证根目录干净（仅必要文件）
- [ ] 8.2 验证所有测试可以运行
- [ ] 8.3 验证导入路径正确
- [ ] 8.4 审查 `.cleanup_archive/` 中剩余文件
- [ ] 8.5 删除确认无用的归档文件
- [ ] 8.6 提交所有变更

## 9. CI/CD 更新（如需要）

- [ ] 9.1 检查 CI/CD 配置是否需要更新测试路径
- [ ] 9.2 更新 CI/CD 配置（如需要）
- [ ] 9.3 验证 CI/CD 管道成功运行
