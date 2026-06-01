# Uni-claw 测试指南

## 快速开始（无需依赖）

直接运行最小化测试验证核心逻辑：

```bash
cd /Users/fran/Documents/Code/spacex/uni-claw
python minimal_test.py
```

这会验证：
- ✅ 数据结构逻辑
- ✅ 项目文件完整性
- ✅ 核心遍历逻辑

---

## 测试方式概览

| 测试方式 | 依赖要求 | 适用场景 |
|----------|----------|----------|
| 最小测试 | 无需任何依赖 | 快速验证核心逻辑 |
| 完整测试 | pip install依赖 | 验证完整功能 |
| Mock模式 | 需要依赖库 | 代码验证、流程测试 |
| 单元测试 | 需要依赖库 | 开发调试 |
| 真实设备 | ADB + API密钥 | 实际演示 |

---

## 一、最小化测试（无需安装）

```bash
python minimal_test.py
```

**输出示例：**
```
==================================================
Uni-claw Minimal Test (No Dependencies)
==================================================

📊 Testing data structures...
   ✅ Coordinate works
   ✅ Fingerprint generation works
   ✅ Cache key generation works
   ✅ Hierarchical ID generation works

📁 Testing project structure...
   ✅ All 14 files present

🧠 Testing core logic...
   ✅ Coordinate validation logic works
   ✅ Item selection logic works
   ✅ Menu switching logic works

==================================================
Results: 3/3 tests passed
✅ Core logic verified!
```

---

## 二、安装依赖后测试

### 2.1 安装依赖

```bash
pip install -r requirements.txt
```

### 2.2 运行完整测试

```bash
python quick_test.py
```

### 2.3 Mock模式测试

```bash
# 确保在项目目录
cd /Users/fran/Documents/Code/spacex/uni-claw

# 直接运行（使用内置Mock客户端）
python run.py "车辆设置" --mock
```

**预期输出：**
```
[2025-01-31 XX:XX:XX] INFO: Using mock clients (demo mode)

[EVENT] TraversalEvent(event_type='navigate_start', step=0, data={'target': '车辆设置'})
[EVENT] TraversalEvent(event_type='navigate_success', step=0, data={'target': '车辆设置'})
[EVENT] TraversalEvent(event_type='initialize_start', step=0, data={})
[EVENT] TraversalEvent(event_type='page_analyzed', step=0, data={'current_path': ['DiLink', '互联'], 'items_count': 2, ...})
[EVENT] TraversalEvent(event_type='initialize_complete', step=0, data={...})
[EVENT] TraversalEvent(event_type='traversal_start', step=0, data={'max_steps': 200})

==================================================
TRAVERSAL COMPLETE
==================================================
Total steps: 1
Elapsed time: 0.5s
Visited items: 0
Final path: ['DiLink', '互联']

Content Tree:
--------------------------------------------------
0. Root
  1. DiLink
    1.1. 互联
      1.1.1. 移动数据
      1.1.2. [图标]移动数据开关
```

---

## 二、单元测试

运行测试套件验证代码正确性：

```bash
# 安装测试依赖（如果还没装）
pip install pytest pytest-mock

# 运行所有测试
pytest tests/ -v

# 运行特定模块
pytest tests/test_state.py -v
pytest tests/test_adb_client.py -v
pytest tests/test_traversal.py -v

# 查看测试覆盖率
pytest tests/ --cov=src --cov-report=term-missing
```

**测试覆盖范围：**
- `test_adb_client.py` - ADB客户端接口和坐标转换
- `test_vision_service.py` - 视觉服务接口
- `test_state.py` - 状态管理和数据模型
- `test_traversal.py` - 遍历引擎核心逻辑

---

## 三、真实设备测试

### 前置准备

1. **安装ADB**
   ```bash
   # macOS
   brew install android-platform-tools

   # 验证安装
   adb version
   ```

2. **连接设备**
   ```bash
   # 启用USB调试后连接
   adb devices

   # 应该看到类似输出：
   # List of devices attached
   # XXXXXXXXXX	device
   ```

3. **配置API密钥**
   ```bash
   # 编辑 .env 文件
   nano .env

   # 添加：
   ANTHROPIC_API_KEY=sk-ant-xxxxx
   ```

### 运行真实遍历

```bash
# 方式1：自动检测设备
python run.py "车辆设置"

# 方式2：指定设备ID（多设备时）
python run.py "车辆设置" --device XXXXXXXXXX

# 方式3：从头开始（清除旧状态）
python run.py "车辆设置" --reset
```

### 调试选项

```bash
# 启用详细日志
LOG_LEVEL=DEBUG python run.py "车辆设置"

# 指定不同的Claude模型
python run.py "车辆设置" --model claude-3-opus-20240229
```

---

## 四、验证流程完整性

### 检查点1：模块导入

```bash
python -c "
from src.adb import MockADBClient
from src.vision import MockVisionService
from src.state import TraversalState, StateManager
from src.traversal import TraversalEngine, TraversalConfig
print('✅ 所有模块导入成功')
"
```

### 检查点2：Mock客户端验证

```bash
python -c "
from src.adb import MockADBClient
from src.vision import MockVisionService

adb = MockADBClient()
vision = MockVisionService()

# 测试ADB
print(f'✅ ADB连接: {adb.is_connected()}')
print(f'✅ 屏幕尺寸: {adb.get_screen_size()}')

# 测试Vision
result = vision.analyze_screenshot(b'test')
print(f'✅ AI分析返回: {type(result).__name__}')
print(f'✅ 菜单数量: {len(result.level1_menus)}')
"
```

### 检查点3：完整流程模拟

创建测试脚本 `test_flow.py`:

```python
#!/usr/bin/env python3
"""快速流程测试"""

from src.adb import MockADBClient
from src.vision import MockVisionService
from src.state import TraversalState
from src.traversal import TraversalEngine, TraversalConfig

# 创建组件
adb = MockADBClient()
vision = MockVisionService()
state = TraversalState()
config = TraversalConfig(max_steps=5)

# 创建引擎
engine = TraversalEngine(
    adb_client=adb,
    vision_service=vision,
    state=state,
    config=config,
)

# 运行
print("开始Mock测试...")
engine.navigate_to_app("测试应用")
engine.initialize_structure()
summary = engine.run()

print(f"\n✅ 测试完成")
print(f"   - 步骤数: {summary['total_steps']}")
print(f"   - 访问项: {summary['visited_count']}")
print(f"   - ADB命令: {len(adb.command_log)}")
print(f"   - AI调用: {vision.call_count}")
```

运行：
```bash
python test_flow.py
```

---

## 五、常见问题排查

### 问题1：ImportError

```bash
# 解决：确保在项目根目录运行
cd /Users/fran/Documents/Code/spacex/uni-claw
python run.py "测试" --mock
```

### 问题2：ADB设备未连接

```bash
# 检查设备
adb devices

# 重启ADB
adb kill-server
adb start-server

# 检查USB调试是否开启
```

### 问题3：API密钥错误

```bash
# 确认.env文件存在
cat .env

# 确认密钥格式正确（sk-ant-开头）
```

### 问题4：权限错误

```bash
# 给予执行权限
chmod +x run.py
```

---

## 六、测试检查清单

- [ ] Mock模式运行成功
- [ ] 单元测试全部通过 (`pytest tests/`)
- [ ] 模块导入无错误
- [ ] ADB能识别设备
- [ ] 真实设备遍历成功
- [ ] 状态保存和恢复正常
- [ ] 事件输出正常

---

## 七、下一步

测试通过后，可以：

1. **自定义Mock响应**：修改MockVisionService返回特定页面结构
2. **添加测试用例**：在tests/目录添加特定场景测试
3. **扩展功能**：基于现有接口添加新的ADB或Vision实现
4. **实际应用**：连接真实设备进行应用遍历
