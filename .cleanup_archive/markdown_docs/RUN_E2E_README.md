# E2E仿真测试执行脚本使用说明

## 快速开始

### Windows系统
```bash
# 方式1: 双击运行
run_e2e.bat

# 方式2: 命令行运行
run_e2e.bat

# 方式3: 使用Python直接运行
python run_e2e_simple.py
```

### Unix/Linux/Mac系统
```bash
# 首先赋予执行权限
chmod +x run_e2e.sh

# 然后运行
./run_e2e.sh

# 或者直接用Python运行
python3 run_e2e_simple.py
```

## 可用的脚本

### 1. `run_e2e_simple.py` - 简单版本
快速执行测试，显示结果摘要。

**特点:**
- 快速执行，输出简洁
- 显示测试状态和关键结果
- 适合CI/CD集成

**输出示例:**
```
======================================================
测试结果摘要
======================================================
测试状态: ❌ FAIL
完成原因: completed
总步数: 1
断言成功: ❌
缺失事件: 9个
```

### 2. `run_e2e_detailed.py` - 详细版本
完整显示测试输入和输出。

**特点:**
- 显示完整测试配置
- 显示输入参数和预期结果
- 显示详细执行过程
- 显示完整断言结果
- 适合调试和分析

**输出示例:**
```
📥 测试输入 (INPUT)
==================================================
📋 测试用例: e2e_all_traversal
📝 描述: 全菜单遍历：验证深度优先顺序和恢复操作

🎯 意图槽位:
  • target_app: 设置
  • scope: full
  • depth: 3

✅ 预期结果:
  • 关键事件序列:
    1. 进入 root
    2. 点击 SettingsButton
    ...

📤 测试输出 (OUTPUT)
==================================================
✨ 执行结果:
  • 状态: ❌ FAIL
  • 完成原因: completed
```

### 3. `run_e2e.bat` - Windows批处理脚本
Windows系统的便捷执行脚本。

**特点:**
- 自动检测Python环境
- 显示友好的错误提示
- 返回正确的退出码
- 适合Windows任务计划

### 4. `run_e2e.sh` - Unix Shell脚本
Unix/Linux/Mac系统的便捷执行脚本。

**特点:**
- 自动检测Python3环境
- 错误自动终止
- 显示友好的错误提示
- 适合Cron任务和CI/CD

## 测试结果解读

### 成功的测试 (退出码: 0)
```
✅ 测试状态: PASS
✅ 断言成功: YES
✅ 所有预期事件匹配
```

### 失败的测试 (退出码: 1)
```
❌ 测试状态: FAIL
❌ 断言成功: NO
❌ 缺失事件: 9个
❌ 额外事件: 1个
```

### 执行错误 (退出码: 2)
```
❌ 测试执行出错
Traceback (most recent call last):
...
```

## 环境要求

### 必需组件
- Python 3.10+
- 项目依赖包 (已在 requirements.txt 中)
- 测试框架文件

### 依赖检查
```bash
# 检查Python版本
python --version

# 检查项目依赖
pip install -r requirements.txt

# 检查测试文件
ls tests/simulation/fixtures/e2e_all_traversal/
```

## 高级用法

### 修改测试用例
```bash
# 编辑你的测试用例
vim tests/simulation/fixtures/e2e_all_traversal/test_case.json

# 然后运行测试
python run_e2e_simple.py
```

### 运行其他测试
修改脚本中的 `test_case_path` 变量：
```python
# 在 run_e2e_simple.py 中修改
test_case_path = "tests/simulation/fixtures/e2e_target_found/test_case.json"
```

### 调试模式
在Python脚本中添加调试信息：
```python
# 添加详细日志
import logging
logging.basicConfig(level=logging.DEBUG)
```

## CI/CD集成

### GitHub Actions
```yaml
- name: Run E2E Tests
  run: |
    python run_e2e_simple.py
```

### Jenkins Pipeline
```groovy
stage('E2E Tests') {
    steps {
        sh 'python3 run_e2e_simple.py'
    }
}
```

### Docker
```dockerfile
COPY run_e2e_simple.py /app/
RUN python /app/run_e2e_simple.py
```

## 故障排除

### 问题1: 找不到测试文件
```
❌ 测试文件不存在: tests/simulation/fixtures/e2e_all_traversal/test_case.json
```
**解决方案:** 确保在项目根目录运行脚本

### 问题2: Python导入错误
```
ModuleNotFoundError: No module named 'tests.simulation.helpers'
```
**解决方案:** 检查Python路径，确保在正确的目录运行

### 问题3: 计划文件格式错误
```
ValueError: Invalid plan file
```
**解决方案:** 检查plan_all.json文件格式是否正确

## 性能指标

### 执行时间
- 简单测试: ~2-5秒
- 复杂测试: ~5-15秒
- 完整测试套件: ~30-60秒

### 内存使用
- 单个测试: ~50-100MB
- 测试套件: ~200-500MB

## 下一步

1. **创建自定义测试:** 复制 `e2e_all_traversal` 作为模板
2. **扩展测试覆盖:** 添加更多测试场景
3. **集成CI/CD:** 添加到你的CI流程
4. **性能优化:** 根据执行时间优化测试

## 获取帮助

如有问题，请查看:
- 项目文档: `docs/SIMULATION_TESTING_GUIDE.md`
- 测试文档: `tests/simulation/README.md`
- 架构文档: `docs/ARCHITECTURE.md`