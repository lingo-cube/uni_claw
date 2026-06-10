# Simulation CI 集成指南

将 Settings 遍历基线测试集成到 CI/CD 流程中。

## 测试套件

| 套件 | 测试文件 | 验证内容 |
|------|---------|---------|
| **settings_full_traversal** | `test_settings_full_traversal.py` | 完整遍历 + expected_behavior 验证 |
| **settings_target_search** | `test_target_search.py` | TARGET_FOUND 完成策略 + 早期终止 |

## 快速开始

### 1. 安装

```bash
# 安装 pre-commit hooks
bash scripts/install_simulation_ci.sh

# 或手动安装
pip install pre-commit
pre-commit install
```

### 2. 本地运行

```bash
# 运行所有测试
python scripts/run_simulation_ci.py

# 运行特定套件
python scripts/run_simulation_ci.py --suite settings_full_traversal
python scripts/run_simulation_ci.py --suite settings_target_search

# 详细输出
python scripts/run_simulation_ci.py --verbose
```

### 3. 自动运行

| 触发方式 | 说明 |
|---------|------|
| **Git commit** | 修改 simulation 相关文件时自动运行 |
| **Push to main/develop** | GitHub Actions 自动运行 |
| **Pull Request** | GitHub Actions 自动运行并评论结果 |

## 配置文件

| 文件 | 说明 |
|------|------|
| `tests/simulation-ci.yaml` | CI 配置（测试套件、验证规则） |
| `tests/v6/settings/expected_behavior.yaml` | 基线预期行为 |
| `.github/workflows/simulation-ci.yml` | GitHub Actions 配置 |
| `.pre-commit-config.yaml` | Pre-commit hooks |

## 验证规则

CI 会验证以下规则：

### settings_full_traversal 套件

1. **完成状态** - 遍历正常完成
2. **页面规则** - 所有页面被访问，无弹窗
3. **操作规则** - 深度优先顺序、恢复操作、无重复动作
4. **智能纠错** - 导航偏差时自动纠正
5. **退出策略** - 正确使用 AUTO_ESCAPE
6. **节点覆盖** - 95% 以上动态节点被访问
7. **Trace 完整** - 所有核心 Span 类型存在

### settings_target_search 套件

1. **早期终止** - 找到目标后立即停止，不遍历所有页面
2. **完成策略** - TARGET_FOUND 策略正确触发
3. **目标定位** - 精确匹配 Dark mode 开关
4. **访问顺序** - 深度优先访问 Wi-Fi → Bluetooth → Display
5. **未访问页面** - Storage、Battery、Apps 不应被访问（早期终止）

## 跳过验证

```bash
# 跳过 pre-commit hooks
git commit --no-verify -m "message"

# 跳过特定规则（修改 .pre-commit-config.yaml）
pre-commit run --skip simulation-ci-quick
```

## 故障排查

### 测试失败

```bash
# 查看详细输出
python scripts/run_simulation_ci.py --verbose

# 检查 trace 文件
ls .traces/
```

### 基线漂移

如果测试结果与基线不一致：

1. 检查 `expected_behavior.yaml` 中的预期值
2. 查看最新的 trace 文件分析实际行为
3. 如果是合理的变更，更新基线值

## 相关文档

- [Expected Behavior 规范](../prd/expected_behavior.md)
- [Simulation 测试指南](../testing/simulation_tests.md)
- [Trace 可视化](../testing/trace_visualization.md)
