# E2E仿真测试脚本完整指南

## 🚀 推荐使用 (最佳选择)

### 统一脚本 (支持所有模式)
```bash
# 推荐：无特殊字符，最大兼容性
python run_e2e.py clean

# 其他模式
python run_e2e.py simple      # 带emoji的简单模式
python run_e2e.py detailed    # 详细模式，显示完整输入输出
python run_e2e.py             # 默认为clean模式
```

## 📋 所有可用脚本

### 1. 统一脚本 ⭐ 推荐
**文件**: `run_e2e.py`

**优点**:
- 一个脚本支持多种模式
- 完善的帮助信息
- 统一的接口
- 跨平台兼容

**使用方法**:
```bash
python run_e2e.py clean      # 推荐
python run_e2e.py simple     # 简单模式
python run_e2e.py detailed   # 详细模式
python run_e2e.py --help     # 帮助信息
```

### 2. 干净脚本 ⭐ 推荐
**文件**: `run_e2e_clean.py`

**优点**:
- 无特殊字符，完全兼容
- 适合Windows CMD
- 适合CI/CD
- 清晰的输出格式

**使用方法**:
```bash
python run_e2e_clean.py
```

**退出码**:
- `0` - 测试通过
- `1` - 测试失败
- `2` - 执行错误

### 3. 简单脚本
**文件**: `run_e2e_simple.py`

**优点**:
- 快速执行
- emoji界面
- 输出简洁
- 适合个人使用

**缺点**:
- 可能在某些终端显示异常

**使用方法**:
```bash
python run_e2e_simple.py
```

### 4. 详细脚本
**文件**: `run_e2e_detailed.py`

**优点**:
- 显示完整输入
- 显示完整输出
- 便于调试
- 详细的分析

**缺点**:
- 输出较多
- 可能存在编码问题

**使用方法**:
```bash
python run_e2e_detailed.py
```

### 5. Windows批处理
**文件**: `run_e2e.bat`

**优点**:
- 双击运行
- 自动检测Python
- 友好的错误提示
- 适合Windows用户

**使用方法**:
```bash
# 方式1: 双击文件
# 方式2: 命令行
run_e2e.bat

# 方式3: PowerShell
.\run_e2e.bat
```

### 6. Unix Shell脚本
**文件**: `run_e2e.sh`

**优点**:
- Linux/Mac原生支持
- 可添加执行权限
- 适合脚本集成
- 错误自动终止

**使用方法**:
```bash
# 添加执行权限
chmod +x run_e2e.sh

# 运行
./run_e2e.sh

# 或者
bash run_e2e.sh
```

## 🎯 使用场景推荐

### 个人开发环境
```bash
python run_e2e.py clean
```

### CI/CD集成
```bash
# GitHub Actions
- name: Run E2E Tests
  run: python run_e2e.py clean

# Jenkins
sh 'python run_e2e.py clean'
```

### 调试分析
```bash
python run_e2e.py detailed
```

### Windows自动化任务
```bash
# 使用任务计划程序
run_e2e.bat
```

### 快速验证
```bash
python run_e2e.py clean
```

## 📊 当前测试状态

### 脚本状态 ✅ 全部可用
- ✅ `run_e2e.py` - 统一脚本正常
- ✅ `run_e2e_clean.py` - 干净脚本正常
- ✅ `run_e2e_simple.py` - 简单脚本正常
- ✅ `run_e2e_detailed.py` - 详细脚本正常
- ✅ `run_e2e.bat` - Windows脚本正常
- ✅ `run_e2e.sh` - Unix脚本正常

### 测试结果 ❌ 当前失败
```
Test Status: [FAIL] FAIL
Total Steps: 1 (Expected: 8-20)
Events Matched: 0/9
Exit Code: 1
```

## 🔧 环境要求

### 最低要求
- Python 3.10+
- 项目依赖已安装
- 测试文件存在

### 检查环境
```bash
# Python版本
python --version

# 依赖检查
pip list | grep simulation

# 文件检查
ls tests/simulation/fixtures/e2e_all_traversal/
```

## 📝 输出格式说明

### 成功输出
```
======================================================================
Test Result Summary
======================================================================
Test Status: [PASS] PASS
Completion Reason: completed
Total Steps: 15
Events Matched: 9/9
Exit Code: 0
[SUCCESS] All tests passed!
```

### 失败输出 (当前状态)
```
======================================================================
Test Result Summary
======================================================================
Test Status: [FAIL] FAIL
Completion Reason: completed
Total Steps: 1
Events Matched: 0/9
Exit Code: 1
[FAILURE] Tests failed - check output above
```

## 🛠️ 故障排除

### 问题1: 编码错误
**解决方案**: 使用 `run_e2e.py clean` 或 `run_e2e_clean.py`

### 问题2: 找不到模块
**解决方案**: 确保在项目根目录运行，检查Python路径

### 问题3: 测试文件不存在
**解决方案**: 检查项目结构，确保测试文件存在

### 问题4: 权限错误 (Unix)
**解决方案**: 添加执行权限 `chmod +x run_e2e.sh`

## 📚 相关文档

- [QUICK_START.md](QUICK_START.md) - 快速启动指南
- [RUN_E2E_README.md](RUN_E2E_README.md) - 详细使用说明
- [docs/SIMULATION_TESTING_GUIDE.md](docs/SIMULATION_TESTING_GUIDE.md) - 测试框架文档
- [tests/simulation/README.md](tests/simulation/README.md) - 测试套件说明

## 🎓 学习资源

### 1. 理解测试框架
```bash
# 查看测试帮助
python run_e2e.py --help

# 阅读测试文档
cat docs/SIMULATION_TESTING_GUIDE.md
```

### 2. 创建自定义测试
```bash
# 复制模板
cp -r tests/simulation/fixtures/e2e_all_traversal tests/simulation/fixtures/my_test

# 编辑测试
vim tests/simulation/fixtures/my_test/test_case.json

# 运行测试
python run_e2e.py clean
```

### 3. 集成到CI/CD
```bash
# GitHub Actions示例
cat > .github/workflows/e2e-tests.yml << EOF
name: E2E Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Set up Python
        uses: actions/setup-python@v2
        with:
          python-version: '3.10'
      - name: Run E2E Tests
        run: python run_e2e.py clean
EOF
```

## 🚀 下一步

1. **验证环境**: 运行 `python run_e2e.py clean`
2. **选择脚本**: 根据场景选择合适的脚本
3. **理解结果**: 学习如何解读测试输出
4. **集成CI/CD**: 添加到你的自动化流程
5. **创建测试**: 开发自己的测试用例

## 💡 最佳实践

1. **日常使用**: `python run_e2e.py clean`
2. **调试问题**: `python run_e2e.py detailed`
3. **CI/CD**: `python run_e2e.py clean` (配合退出码检查)
4. **批量测试**: 修改脚本中的 `test_case_path`
5. **性能测试**: 记录执行时间进行优化

---

**总脚本数**: 6个
**可用状态**: 100% ✅
**推荐使用**: `python run_e2e.py clean`