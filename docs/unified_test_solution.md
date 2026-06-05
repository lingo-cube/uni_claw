# Uni-Claw 归一化测试方案

**版本**: 1.0  
**创建**: 2026-06-05  
**目的**: 统一项目测试执行和validation报告生成流程

---

## 🎯 核心原则

**唯一入口原则**: 项目中只有一个统一的测试执行入口

```bash
# ❌ 禁止使用（旧方式）
python src/ai/run_tests.py
python src/adb/run_tests.py
pytest tests/v6/ -v

# ✅ 推荐使用（新方式）
python scripts/unified_test_coordinator.py all
python scripts/unified_test_coordinator.py v6
python scripts/unified_test_coordinator.py ci
```

## 📋 统一测试协调器

### 核心功能

**位置**: `scripts/unified_test_coordinator.py`

**职责**:
1. 统一测试执行入口
2. 自动解析pytest输出
3. 自动生成validation报告
4. 自动更新dashboard数据
5. 提供JSON格式数据导出

### 支持的测试范围

| 范围 | 说明 | 测试路径 |
|------|------|----------|
| `all` | 所有测试（单元+集成） | `tests/v6/`, `tests/integration/`, `tests/models/` |
| `unit` | 仅单元测试 | `tests/v6/`, `tests/models/` |
| `integration` | 仅集成测试 | `tests/integration/`, `tests/v6/test_examples.py` |
| `v6` | V6完整测试 | `tests/v6/` (单元+examples) |
| `ci` | CI快速测试 | 关键V6测试 |

## 🔄 标准工作流程

### 1. 开发阶段工作流

```bash
# 开发完成后运行测试
git add .
git commit -m "implement feature X"

# 统一测试（推荐）
python scripts/unified_test_coordinator.py v6

# 查看结果
# - 自动生成 validation 文档
# - 终端显示测试摘要
# - 失败会有明确提示
```

### 2. PR/CI工作流

```bash
# 在CI环境中运行快速测试
python scripts/unified_test_coordinator.py ci

# 可选：导出JSON供CI使用
python scripts/unified_test_coordinator.py ci --export-json

# 可选：更新dashboard供监控
python scripts/unified_test_coordinator.py ci --update-dashboard
```

### 3. 完整验证工作流

```bash
# 运行完整测试套件
python scripts/unified_test_coordinator.py all

# 自动生成所有validation报告：
# - docs/validation/unit_test_status.md
# - docs/validation/integration_test_status.md

# 查看测试结果dashboard
# 浏览器打开 docs/validation/test_results_dashboard.html
```

## 📊 数据流架构

### 完整的数据流

```
代码变更 → unified_test_coordinator → pytest执行
    ↓
解析pytest输出 → 结构化数据
    ↓
生成validation报告 → docs/validation/
    ↓
更新dashboard数据 → dashboard_data.json
    ↓
dashboard读取 → test_results_dashboard.html（真实数据）
```

### 文件依赖关系

```
unified_test_coordinator.py
    ├── 执行: pytest tests/v6/ -v
    ├── 解析: _parse_pytest_output()
    ├── 生成: _generate_validation_reports()
    │   ├── unit_test_status.md
    │   └── integration_test_status.md
    └── 导出: export_json_report()
```

## 🚀 迁移指南

### 从旧方式迁移到新方式

#### 阶段1: 替换直接pytest调用

**❌ 旧方式**:
```bash
pytest tests/v6/ -v
pytest tests/models/ -v
```

**✅ 新方式**:
```bash
python scripts/unified_test_coordinator.py v6
```

#### 阶段2: 废弃模块级run_tests.py

**❌ 旧方式**:
```bash
python src/ai/run_tests.py
python src/adb/run_tests.py
```

**✅ 新方式**:
```bash
python scripts/unified_test_coordinator.py unit
```

#### 阶段3: 删除旧脚本（可选）

```bash
# 保留但标记为废弃
mv src/ai/run_tests.py src/ai/run_tests.py.deprecated
mv src/adb/run_tests.py src/adb/run_tests.py.deprecated

# 或直接删除
rm src/ai/run_tests.py
rm src/adb/run_tests.py
```

## 🛠️ 集成到工作流

### OpenSpec集成

更新 `openspec/changes/*/tasks.md` 模板：

```markdown
### Task X.Y: Testing and Validation

**Steps**:
1. ✅ Implement feature X
2. ✅ Run unified testing
   ```bash
   python scripts/unified_test_coordinator.py v6
   ```
3. ✅ Check validation reports
4. ✅ Generate final documentation

**Output**: 
- Automated test execution
- Validation reports: `docs/validation/unit_test_status.md`
- Test results: `test_results.json` (optional)
```

### GitHub Actions集成

更新 `.github/workflows/simulation-tests.yml`：

```yaml
- name: Run unified tests
  run: |
    python scripts/unified_test_coordinator.py ci --export-json
    
- name: Update validation reports
  run: |
    python scripts/unified_test_coordinator.py ci
    
- name: Update dashboard data
  run: |
    python scripts/unified_test_coordinator.py ci --update-dashboard
```

### 本地开发集成

在 `Makefile` 或 `package.json` 中添加快捷命令：

```makefile
# Makefile
test-v6:
	python scripts/unified_test_coordinator.py v6

test-all:
	python scripts/unified_test_coordinator.py all

test-ci:
	python scripts/unified_test_coordinator.py ci

validate: test-all
```

## 📈 监控和报告

### Dashboard显示真实数据

更新 `docs/validation/test_results_dashboard.html`：

```javascript
// 读取真实测试数据
fetch('docs/validation/dashboard_data.json')
  .then(response => response.json())
  .then(data => {
    displayRealTestResults(data.test_results);
  });
```

### 自动报告生成

每次运行测试后自动更新：

1. **单元测试状态**: `docs/validation/unit_test_status.md`
2. **集成测试状态**: `docs/validation/integration_test_status.md`
3. **JSON数据**: `test_results.json`
4. **Dashboard数据**: `dashboard_data.json`

## 🎯 实施计划

### 立即可执行

1. **✅ 创建unified_test_coordinator.py** - 已完成
2. **测试协调器功能验证** - 需要测试
3. **文档更新** - 需要更新相关文档
4. **Dashboard数据源更新** - 需要更新HTML

### 短期实施（本周）

1. **测试协调器试用**
2. **OpenSpec模板更新**
3. **GitHub Actions更新**
4. **团队培训**

### 中期实施（本月）

1. **废弃旧run_tests.py**
2. **强制使用统一入口**
3. **Dashboard显示真实数据**
4. **完整CI/CD集成**

## 📋 使用检查清单

在提交代码前，确保：

- [ ] 使用统一测试协调器运行测试
- [ ] 所有测试通过（或有明确跳过原因）
- [ ] Validation报告已自动生成
- [ ] Dashboard数据已更新
- [ ] 可以看到最新的测试结果

## 🔧 故障排除

### 问题：测试协调器运行失败

**解决方案**:
```bash
# 检查Python环境和依赖
python -m pytest --version

# 检查测试文件是否存在
ls tests/v6/

# 手动运行pytest测试
pytest tests/v6/test_simulation.py -v
```

### 问题：validation报告未生成

**解决方案**:
```bash
# 检查validation目录权限
ls -la docs/validation/

# 检查测试结果数据
python scripts/unified_test_coordinator.py v6 --export-json
# 查看test_results.json是否有数据
```

### 问题：Dashboard显示模拟数据

**解决方案**:
```bash
# 生成真实dashboard数据
python scripts/unified_test_coordinator.py v6 --update-dashboard

# 重新打开dashboard
# 确认使用真实数据源
```

## 📞 支持

**问题反馈**: 向技术团队报告统一测试协调器问题

**功能请求**: 如需新的测试范围或功能，请提交issue

**文档更新**: 保持本文档与实际实施同步

---

**版本**: 1.0  
**最后更新**: 2026-06-05  
**状态**: 🎯 归一化方案确定  
**下一步**: 开始实施迁移