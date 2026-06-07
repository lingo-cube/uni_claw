# Tasks: V6.9.1 Test System Refactor

## 1. 清理与基础（第 1 周）

- [x] 1.1 清理 conftest.py - 删除 7 个 AI fixture（mock_provider、mock_deepseek_provider、mock_claude_provider、mock_mimo_provider、all_mock_providers、response_recorder、response_replayer）
- [x] 1.2 删除重复文件 - tests/v6/test_v6_4_simulation_alignment.py
- [x] 1.3 重命名文件 - tests/v6/test_simulation.py → test_simulation_base.py
- [x] 1.4 修复死引用 - tests/integration/test_simulation_e2e.py 移除 `from tests.simulation.helpers` 和替换 CompletionPolicyType.EXHAUSTIVE
- [x] 1.5 验证清理 - 运行 `pytest tests/v6/ tests/integration/ --collect-only` 确保无导入错误

## 2. 共享辅助模块 - 第一批（第 1 周）

- [x] 2.1 创建 helpers 目录 - tests/helpers/__init__.py
- [x] 2.2 实现 factories.py - create_minimal_plan()、create_test_node()、create_mock_vision()
- [x] 2.3 实现 state_inspector.py - verify_stack_consistency()、verify_cache_coherency()、verify_no_orphan_spans()、verify_metrics_completeness()、verify_state_machine_invariants()
- [x] 2.4 实现 trace_analyzer.py - build_tree()、extract_operations()、count_span_types()
- [x] 2.5 验证第一批模块 - 运行 import 测试确保模块可被导入

## 3. 虚拟页面 Fixture（第 2 周）

- [x] 3.1 创建 pages_dynamic.json - 动态匹配场景，使用 coordinate 字段格式
- [x] 3.2 创建 pages_correction.json - 智能纠正场景（多级菜单）
- [x] 3.3 创建 pages_entry.json - 入口策略场景（桌面 + 应用入口）
- [x] 3.4 创建 pages_boundary.json - 边界测试场景（空页面、超多元素）
- [x] 3.5 创建 pages_chaos.json - 故障注入场景（缺失字段、重复元素）
- [x] 3.6 验证 fixture 格式 - MockVisionService 正确解析所有新建 JSON

## 4. D 系列审查修正（第 2 周）

- [x] 4.1 审查 test_v6_9_dynamic_matching.py - 对齐 D1-D10 基础场景
- [x] 4.2 添加边界测试 - D11（随机化）、D12（空/超多元素）、D13（vision 失败）
- [x] 4.3 验证 D 系列 - 运行 `pytest tests/v6/test_v6_9_dynamic_matching.py -v`

## 5. C 系列补充（第 3 周）

- [x] 5.1 创建 tests/v6/unit/test_compiler.py
- [x] 5.2 实现 C1-C4 测试 - scope 映射（full、partial、target_only）
- [x] 5.3 实现 C5 测试 - target_path 静态路径
- [x] 5.4 实现 C6-C9 测试 - element_handling 映射
- [x] 5.5 实现 C10-C12 测试 - navigation、completion 覆盖、validation
- [x] 5.6 验证 C 系列 - 运行 `pytest tests/v6/unit/test_compiler.py -v`

## 6. P 系列扩展（第 3 周）

- [x] 6.1 审查 test_simulation_sm.py - 现有测试覆盖情况
- [x] 6.2 实现 P1-P2 测试 - precondition 满足 + NAVIGABLE 纠正
- [x] 6.3 实现 P3-P5 测试 - 3轮重试 + DEEPER 纠正 + 回退过头
- [x] 6.4 实现 P6-P7 测试 - UNKNOWN 迷失 + Vision 失败容错
- [x] 6.5 实现 P8-P10 测试 - 并发 precondition + 超时 + 页面未变
- [x] 6.6 验证 P 系列 - 运行 `pytest tests/v6/test_simulation_sm.py -v`

## 7. E2E 与 M 系列（第 4 周）

- [x] 7.1 重写 tests/integration/test_simulation_e2e.py - 移除死代码，实现 E2E 场景
- [x] 7.2 实现 E2E1-E2E3 测试 - 全菜单遍历、目标搜索、静态路径
- [x] 7.3 实现 E2E4-E2E7 测试 - 嵌套弹窗、动态+纠正协同、深度+回退、错误恢复
- [x] 7.4 扩展 test_simulation_base.py - 添加 M1-M5 Mock 验证测试
- [x] 7.5 验证 E2E + M 系列 - 运行 `pytest tests/integration/test_simulation_e2e.py -v` 和 `pytest tests/v6/test_simulation_base.py -v`

## 8. 共享辅助模块 - 第二批（第 4 周）

- [x] 8.1 实现 chaos_engine.py - randomize_page_order()、inject_delay()、corrupt_page_data()、duplicate_elements()
- [x] 8.2 实现 boundary_tester.py - test_empty_elements()、test_excessive_depth()、test_massive_elements()、test_unicode_edge_cases()、test_extreme_coordinates()
- [x] 8.3 实现 fault_injector.py - inject_vision_failure()、inject_action_failure()、inject_state_corruption()、inject_mismatched_page()
- [x] 8.4 验证第二批模块 - 运行单元测试确保功能正确

## 9. 全量回归与验收（第 5 周）

- [x] 9.1 全量测试执行 - `pytest tests/v6/ tests/integration/ -v`
- [x] 9.2 覆盖率检查 - `pytest tests/v6/ tests/integration/ --cov=src` 确保核心模块 > 80%
- [x] 9.3 性能基准 - 对比实施前后测试执行时间，确保无明显退化
- [x] 9.4 文档同步 - 更新 tests/REFERENCE.md，确保与实施一致
- [x] 9.5 验收确认 - 所有验收标准通过（44+20 场景、100% 通过率、覆盖率达标）
