# 自然语言测试 API 设计

> 一句话描述测试需求，系统自动解析执行

---

## 一、核心设计

### 1.1 目标

从复杂的层级操作简化为自然语言表达：

```python
# 原来：需要 10+ 行代码
path = ["车辆设置", "DiLink", "互联"]
engine.navigate_to(path)
engine.click("移动数据")
engine.input("test")

# 现在：一行搞定
engine.execute("点击车辆设置/DiLink/互联/移动数据，然后输入test")
```

### 1.2 支持的操作类型

| 操作类型 | 表达式示例 | 说明 |
|----------|-----------|------|
| **点击** | 点击 X | 点击指定元素 |
| **输入** | 输入 "文本" | 输入文本 |
| **等待** | 等待 2 秒 | 等待指定时间 |
| **验证** | 验证 X 存在 | 验证元素存在 |
| **滑动** | 向上滑动 | 滑动操作 |
| **返回** | 返回 | 按返回键 |
| **组合** | 先...再... | 顺序执行多个操作 |

---

## 二、语法设计

### 2.1 基础语法

```text
操作 [层级路径/]元素名 [参数] [然后 操作 ...]

层级路径：用 "/" 分隔，从根到目标
元素名：目标元素的名称
参数：操作所需的参数（如输入的文本）
连接词：然后、接着、之后
```

### 2.2 示例

```python
# 简单点击
engine.execute("点击移动数据")

# 指定路径点击
engine.execute("点击车辆设置/DiLink/互联/移动数据")

# 点击后输入
engine.execute("点击车辆设置/DiLink/互联/移动数据，然后输入 hello")

# 多步操作
engine.execute("点击车辆设置/DiLink/互联，然后点击移动数据，接着输入 test")

# 验证操作
engine.execute("点击移动数据，然后验证开关已打开")

# 滑动操作
engine.execute("在列表中向上滑动，然后点击第一个元素")

# 带等待的操作
engine.execute("点击移动数据，等待 2 秒，然后输入 test")
```

### 2.3 相对路径表达

```python
# 从当前位置
engine.execute("点击子菜单/设置")

# 回退后操作
engine.execute("返回，然后点击下一个")

# 通过索引
engine.execute("点击第 3 个元素")
```

---

## 三、实现设计

### 3.1 核心类

```python
class NaturalLanguageExecutor:
    """自然语言测试执行器"""

    def __init__(self, state_machine: HierarchicalStateMachine):
        self.sm = state_machine
        self.parser = CommandParser()
        self.executor = OperationExecutor(state_machine)

    def execute(self, command: str) -> ExecutionResult:
        """执行自然语言命令"""
        # 1. 解析命令
        operations = self.parser.parse(command)

        # 2. 依次执行
        results = []
        for op in operations:
            result = self.executor.execute(op)
            results.append(result)

            if not result.success and op.stop_on_failure:
                break

        # 3. 返回综合结果
        return ExecutionResult.aggregate(results)
```

### 3.2 命令解析器

```python
class CommandParser:
    """解析自然语言命令为操作序列"""

    # 操作模式
    PATTERNS = {
        'click': r'点击\s*(.+?)(?:\s*(?:然后|接着|之后)\s*|$)',
        'input': r'输入\s*[\"'](.+)[\"']',
        'wait': r'等待\s*(\d+)\s*秒',
        'verify': r'验证\s*(.+)',
        'swipe': r'(向[上下左右])滑动',
        'back': r'返回',
    }

    def parse(self, command: str) -> List[Operation]:
        """解析命令字符串"""
        operations = []

        # 按连接词分割
        segments = re.split(r'\s*(?:然后|接着|之后)\s*', command)

        for segment in segments:
            segment = segment.strip()
            if not segment:
                continue

            # 识别操作类型
            for op_type, pattern in self.PATTERNS.items():
                match = re.match(pattern, segment)
                if match:
                    operations.append(Operation(
                        type=op_type,
                        target=match.group(1) if match.groups() else None,
                        params=match.groups() if match.groups() else ()
                    ))
                    break

        return operations
```

### 3.3 操作执行器

```python
class OperationExecutor:
    """执行解析后的操作"""

    def __init__(self, state_machine: HierarchicalStateMachine):
        self.sm = state_machine

    def execute(self, op: Operation) -> ExecutionResult:
        """执行单个操作"""
        if op.type == 'click':
            return self._execute_click(op)
        elif op.type == 'input':
            return self._execute_input(op)
        elif op.type == 'wait':
            return self._execute_wait(op)
        elif op.type == 'verify':
            return self._execute_verify(op)
        # ... 其他操作

    def _execute_click(self, op: Operation) -> ExecutionResult:
        """执行点击操作"""
        # 1. 解析目标
        path, element_name = self._parse_target(op.target)

        # 2. 导航到目标位置
        if not self._navigate_to(path):
            return ExecutionResult.failure(f"无法导航到 {path}")

        # 3. 查找元素
        node = self._find_element(element_name)
        if not node:
            return ExecutionResult.failure(f"找不到元素: {element_name}")

        # 4. 执行点击
        try:
            self.sm.adb.tap(node.coordinates[0], node.coordinates[1])
            return ExecutionResult.success(f"已点击: {element_name}")
        except Exception as e:
            return ExecutionResult.failure(f"点击失败: {e}")

    def _execute_input(self, op: Operation) -> ExecutionResult:
        """执行输入操作"""
        text = op.params[0] if op.params else ""

        try:
            # 输入文本
            self.sm.adb.input_text(text)
            return ExecutionResult.success(f"已输入: {text}")
        except Exception as e:
            return ExecutionResult.failure(f"输入失败: {e}")
```

### 3.4 辅助方法

```python
    def _parse_target(self, target: str) -> tuple[List[str], str]:
        """解析目标字符串为路径和元素名

        Examples:
            "移动数据" -> ([], "移动数据")
            "车辆设置/DiLink/互联/移动数据" -> (["车辆设置", "DiLink", "互联"], "移动数据")
        """
        if '/' in target:
            parts = target.split('/')
            return parts[:-1], parts[-1]
        return [], target

    def _navigate_to(self, path: List[str]) -> bool:
        """导航到指定路径"""
        # 从当前位置导航到目标路径
        # 利用状态机的层级树和路径操作
        return self.sm.navigate_to_path(path)

    def _find_element(self, name: str) -> Optional[TreeNode]:
        """在当前节点下查找元素"""
        current = self.sm.current_node
        if not current:
            return None

        for child in current.children:
            if child.name == name:
                return child
        return None
```

---

## 四、使用示例

### 4.1 基础使用

```python
# 初始化
engine = TraversalEngine(adb_client, vision_service)
nl = NaturalLanguageExecutor(engine.state_machine)

# 执行命令
result = nl.execute("点击车辆设置/DiLink/互联/移动数据")
print(result)  # ExecutionResult(success=True, message="已点击: 移动数据")

# 组合操作
result = nl.execute("点击车辆设置/DiLink/互联/移动数据，然后输入 test")
print(result)
# ExecutionResult(success=True, message="已点击: 移动数据; 已输入: test")
```

### 4.2 批量测试

```python
# 定义测试用例
test_cases = [
    "点击车辆设置/DiLink/互联/移动数据，然后验证开关已打开",
    "点击车辆设置/DiLink/互联/移动数据，然后输入 123456",
    "点击车辆设置/DiLink/互联/无线网络，然后点击第一个热点",
]

# 批量执行
for i, test in enumerate(test_cases, 1):
    print(f"\n测试 {i}: {test}")
    result = nl.execute(test)
    print(f"结果: {result}")
```

### 4.3 断言模式

```python
# 验证操作
result = nl.execute("点击移动数据，然后验证开关状态为开")

if not result.success:
    print(f"测试失败: {result.message}")
```

### 4.4 录制回放

```python
# 录制模式
recorder = ActionRecorder()
recorder.start()

# 手动操作...
# recorder.capture_current_action()

# 生成自然语言命令
command = recorder.get_command()
# "点击车辆设置/DiLink/互联/移动数据，然后输入 test"

# 回放
nl.execute(command)
```

---

## 五、增强功能

### 5.1 模糊匹配

```python
# 支持模糊匹配
nl.execute("点击移动数")  # 自动匹配 "移动数据"
nl.execute("点击 DiLink")  # 匹配 "DiLink" 或 "dilink"
```

### 5.2 变量支持

```python
# 定义变量
nl.set_variable("用户名", "testuser")
nl.set_variable("密码", "123456")

# 使用变量
nl.execute("点击用户名输入框，然后输入 ${用户名}")
nl.execute("点击密码输入框，然后输入 ${密码}")
```

### 5.3 条件执行

```python
# 条件判断
nl.execute("如果开关打开，点击关闭；否则点击打开")
nl.execute("等待元素出现，然后点击")
```

### 5.4 循环执行

```python
# 循环
nl.execute("对每个列表项，点击然后返回")
nl.execute("重复 5 次：点击下一个按钮")
```

---

## 六、错误处理

### 6.1 友好的错误提示

```python
result = nl.execute("点击不存在的按钮")

# ExecutionResult(
#     success=False,
#     message="找不到元素: 不存在的按钮",
#     suggestions=[
#         "您是否想点击：移动数据、无线网络、个人热点？"
#     ]
# )
```

### 6.2 自动恢复

```python
# 失败后自动尝试恢复
nl.execute("点击车辆设置/DiLink/互联/移动数据")
# 如果路径不通，自动尝试：
# 1. 从根重新导航
# 2. 查找相似的路径
# 3. 使用状态机回溯
```

---

## 七、集成到状态机

```python
class HierarchicalStateMachine:
    """增强状态机，支持自然语言操作"""

    def execute_command(self, command: str) -> ExecutionResult:
        """执行自然语言命令"""
        executor = NaturalLanguageExecutor(self)
        return executor.execute(command)

    def record_action(self, description: str):
        """记录当前操作为自然语言"""
        current = self.current_node
        if current:
            action = f"点击{current.full_path}"
            self.action_history.append(action)
```

---

## 八、配置

```python
# 配置文件
config = {
    "language": "zh-CN",  # 中英文支持
    "fuzzy_match": True,  # 模糊匹配
    "auto_recover": True,  # 自动恢复
    "suggestion": True,  # 错误建议
    "variables": {
        "APP_NAME": "车辆设置",
    }
}
```

---

## 九、完整示例

```python
from src.engine.traversal_engine import TraversalEngine
from src.nl.natural_language_executor import NaturalLanguageExecutor

# 初始化
engine = TraversalEngine(adb_client, vision_service)
nl = NaturalLanguageExecutor(engine.state_machine)

# 测试场景 1：开关设置
print("测试 1: 开启移动数据")
result = nl.execute("点击车辆设置/DiLink/互联/移动数据，然后验证开关已开启")
assert result.success

# 测试场景 2：输入设置
print("测试 2: 输入热点名称")
result = nl.execute("点击车辆设置/DiLink/互联/个人热点/名称，然后输入 MyHotspot")
assert result.success

# 测试场景 3：导航测试
print("测试 3: 多级导航")
result = nl.execute("点击车辆设置/DiLink/云控/语音助手，然后验证助手已启用")
assert result.success

print("\n✅ 所有测试通过")
```
