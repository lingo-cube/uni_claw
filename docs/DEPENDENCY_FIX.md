# 依赖安装修复说明

## 问题描述

在执行 AI 模块测试时，发现 10 个 async 测试失败，原因是缺少 `pytest-asyncio` 依赖。

**问题根源：** `pytest-asyncio` 已在 `pyproject.toml` 中正确定义，但安装指令不完整。

## 已修复的文件

### 1. README.md
**修改前：**
```bash
pip install -e .
```

**修改后：**
```bash
pip install -e ".[dev]"  # 安装所有依赖（包括测试工具）
```

### 2. docs/SETUP.md
**修改前：**
```bash
pip install -r requirements.txt  # 文件不存在！
```

**修改后：**
```bash
# Install with development dependencies (recommended for testing)
pip install -e ".[dev]"

# Or install only core dependencies
pip install -e .
```

## 新增的验证工具

### 自动验证脚本
- **Linux/Mac**: `./verify_setup.sh`
- **Windows**: `verify_setup.bat`

**功能：**
- 检查 Python/pip 版本
- 验证项目结构
- 自动安装缺失的依赖
- 运行 AI 模块测试验证

## 在另一台机器上的设置步骤

### 方法1：使用验证脚本（推荐）
```bash
# Linux/Mac
git clone <repository>
cd uni-claw
./verify_setup.sh

# Windows
git clone <repository>
cd uni-claw
verify_setup.bat
```

### 方法2：手动设置
```bash
# 1. 克隆仓库
git clone <repository>
cd uni-claw

# 2. 安装完整依赖
pip install -e ".[dev]"

# 3. 验证安装
python -m pytest src/ai/test/ --tb=no -q
# 应该看到：173 passed, 9 skipped
```

## 验证结果

**修复后测试结果：**
```
173 passed, 9 skipped, 1 warning in 1.34s
```

**关键组件测试状态：**
- ✅ UniBrain 核心：7/7 (100%)
- ✅ 提示词管理：30/30 (100%)
- ✅ 追踪集成：22/22 (100%)
- ✅ Provider 抽象：39/39 (100%)

## 长期维护建议

1. **新开发者入职**：确保执行 `pip install -e ".[dev]"`
2. **CI/CD 设置**：在测试步骤前添加 `pip install -e ".[dev]"`
3. **文档更新**：保持 README.md 和 SETUP.md 同步

## 技术细节

**依赖配置位置：** `pyproject.toml`
```toml
[dependency-groups]
dev = [
    "pytest>=7.4.0",
    "pytest-asyncio>=0.21.0",  # 关键依赖
    "pytest-mock>=3.11.0",
    # ... 其他开发工具
]
```

**为什么使用 `[dev]`：**
- 安装所有开发依赖（pytest、pytest-asyncio等）
- 确保测试环境完整
- 避免跨机器依赖不一致问题

---

**修复日期：** 2026-06-05
**影响范围：** 开发环境设置、文档更新
**测试验证：** 所有 173 个 AI 模块测试通过
