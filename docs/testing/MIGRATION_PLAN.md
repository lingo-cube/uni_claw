# 工程逐步迁移计划

保持工程干净，每一步独立可验证。

## 当前改动分类

### 新增文件（10个）
| 文件 | 类型 | 优先级 |
|------|------|--------|
| `src/models/element_type_mapper.py` | 核心工具 | P0 |
| `docs/architecture/implicit_mappings_analysis.md` | 文档 | P1 |
| `tests/simulation-ci.yaml` | CI配置 | P1 |
| `scripts/run_simulation_ci.py` | CI脚本 | P1 |
| `docs/testing/SIMULATION_CI.md` | 文档 | P2 |
| `scripts/install_simulation_ci.sh` | 安装脚本 | P2 |
| `.github/workflows/simulation-ci.yml` | GitHub Actions | P2 |
| `.pre-commit-config.yaml` | Pre-commit | P2 |
| `workflows/run_simulation_ci_workflow.py` | Workflow | P2 |
| `test_investigation.py` | 临时文件 | 删除 |

### 修改文件（13个）
| 文件 | 改动内容 | 优先级 |
|------|---------|--------|
| `tests/v6/settings/test_settings_full_traversal.py` | 使用ElementTypeMapper | P0 |
| `tests/v6/settings/test_settings_simulation.py` | 使用ElementTypeMapper | P0 |
| `tests/v6/settings/test_target_search.py` | 使用ElementTypeMapper | P0 |
| `src/simulation/stateful_mock_vision.py` | 使用ElementTypeMapper | P0 |
| `src/simulation/scroll/scrollable_mock_vision.py` | 使用ElementTypeMapper | P0 |
| `tests/v6/settings/expected_behavior.yaml` | 更新预期值 | P1 |
| `tests/v6/settings/settings_page.json` | 修复页面数据 | P1 |
| `tests/v6/settings/settings_traversal_plan.json` | 修复precondition | P1 |
| `src/state_machine/traversal_fsm.py` | 添加restore执行 | P1 |
| `src/traversal/step_orchestrator.py` | 修复NODE_SELECT循环 | P1 |
| `src/traversal/trace_coordinator.py` | 修复page_transition | P1 |

## 迁移步骤

### Phase 1: 核心工具（独立提交）
**目标**: 添加 ElementTypeMapper，无外部依赖

```bash
# Phase 1a: 添加工具类
git add src/models/element_type_mapper.py
git commit -m "feat: add centralized ElementTypeMapper for element type conversions

- Map Android class names to element type strings
- Convert type strings to MenuItemType enum
- Convert type strings to ExpectedAction enum
- Single source of truth for element type mappings

Related: implicit_mappings_analysis.md"

# Phase 1b: 添加文档
git add docs/architecture/implicit_mappings_analysis.md
git commit -m "docs: add implicit mappings analysis document

Identify and document all implicit element type mappings that need
to be solidified to prevent future regressions."
```

**验证**:
```bash
# 验证工具类可用
python -c "from src.models.element_type_mapper import ElementTypeMapper; print(ElementTypeMapper.from_android_class('android.widget.Switch'))"
```

### Phase 2: 测试文件迁移（独立提交）
**目标**: 更新测试文件使用新工具类

```bash
git add tests/v6/settings/test_settings_full_traversal.py
git add tests/v6/settings/test_settings_simulation.py
git add tests/v6/settings/test_target_search.py
git commit -m "refactor(tests): use ElementTypeMapper in Settings tests

Replace duplicated element type mapping code with centralized
ElementTypeMapper calls for consistency and maintainability."
```

**验证**:
```bash
# 验证测试仍然通过
pytest tests/v6/settings/ -v
```

### Phase 3: Mock服务迁移（独立提交）
**目标**: 更新Mock服务使用新工具类

```bash
git add src/simulation/stateful_mock_vision.py
git add src/simulation/scroll/scrollable_mock_vision.py
git commit -m "refactor(simulation): use ElementTypeMapper in mock services

Update _parse_element_type and _infer_expected_action to use
centralized ElementTypeMapper for consistency."
```

**验证**:
```bash
# 验证simulation测试通过
pytest tests/simulation/ -v
```

### Phase 4: CI配置（独立提交）
**目标**: 添加Simulation CI支持

```bash
git add tests/simulation-ci.yaml
git add scripts/run_simulation_ci.py
git add docs/testing/SIMULATION_CI.md
git commit -m "feat: add simulation CI configuration

Add simulation CI runner and configuration for Settings traversal
baseline testing with expected_behavior.yaml validation."
```

**验证**:
```bash
# 验证CI脚本可用
python scripts/run_simulation_ci.py --suite settings_full_traversal
```

### Phase 5: GitHub Actions（可选，独立提交）
**目标**: 添加GitHub Actions workflow

```bash
git add .github/workflows/simulation-ci.yml
git add .pre-commit-config.yaml
git add scripts/install_simulation_ci.sh
git commit -m "ci: add GitHub Actions workflow for simulation CI

Add automated testing for Settings traversal on push/PR.
Add pre-commit hooks for local validation."
```

### Phase 6: 清理临时文件（独立提交）
**目标**: 删除临时和不必要文件

```bash
# 删除临时文件
rm test_investigation.py
git add test_investigation.py  # Will show as deleted

git commit -m "chore: remove temporary investigation files"
```

## 执行原则

1. **每步独立** - 每个phase都是独立可提交的
2. **先验证** - 提交前运行相关测试验证
3. **小步提交** - 每次只做一件事
4. **清晰信息** - Commit message说明改动和原因
5. **可回滚** - 每步都可以独立回滚

## 当前建议

建议按以下顺序执行：

1. **先提交 Phase 1**（核心工具）
2. **再提交 Phase 2**（测试文件）
3. **最后提交 Phase 4**（CI配置）

Phase 3、5、6 可以后续添加。
