# E2E测试快速参考指南

## 🚀 核心流程 (3步)

```
测试数据 → Mock执行 → 断言验证 → 报告生成
```

## 📊 数据结构层次

```
测试用例 (Test Case)
    ↓
Mock组件 (Vision + Action + Tracer)
    ↓
TraceStep序列 (执行追踪)
    ↓
自然语言事件 (Events)
    ↓
断言结果 (Assertion)
    ↓
多种报告 (Reports)
```

## 🔑 关键规则速查

### 事件描述格式规则

| 动作类型 | 格式规则 | 示例 |
|---------|---------|------|
| `navigate` | 点击 '{目标}' 按钮/菜单项 | "点击 'Settings' 按钮" |
| `enter` | 进入 {页面名} | "进入 SettingsPage" |
| `exit`/`go_back` | 退出 {页面名} | "退出 DisplaySettings" |
| `toggle` | 操作 '{目标}' {类型}并恢复 | "操作 'Brightness' 滑块并恢复" |
| `complete` | 遍历完成 | "遍历完成" |

### 字段映射规则

```
TraceStep          → to_dict()        → 断言引擎
─────────────────────────────────────────────────
action             → action_type      → step_to_nl()
node_id            → current_node    → step_to_nl()
screen_info.target → target_info.id  → step_to_nl()
metadata.reason    → completion_reason → 验证
```

### 断言验证规则

```python
# 1. 事件匹配 (子序列匹配)
expected = [A, B, C]
actual = [A, X, B, Y, C]  # ✓ 匹配
actual = [B, A, C]        # ✗ 不匹配 (顺序错误)

# 2. 步数范围
min_steps <= total_steps <= max_steps

# 3. 完成原因
completion_reason == "completed"

# 4. 违规检测
"错误" not in all_events
"异常" not in all_events
```

## 📁 测试数据文件

### 必需文件
```
tests/simulation/fixtures/{test_name}/
├── test_case.json      # 测试用例定义
├── plan.json           # 遍历计划
└── pages.json          # 虚拟页面数据
```

### test_case.json 结构
```json
{
  "test_id": "unique_id",
  "description": "测试描述",
  "expected": {
    "key_events": ["事件1", "事件2", "..."],
    "total_steps_min": 10,
    "total_steps_max": 30,
    "completion_reason": "completed"
  }
}
```

### pages.json 结构
```json
{
  "root": {
    "current_path": "root",
    "elements": [
      {
        "element_id": "btn1",
        "text": "Settings",
        "action_hint": "navigate",
        "element_type": "button"
      }
    ]
  }
}
```

## 🛠️ 常用命令

### 运行E2E测试
```bash
# Python API
from tests.simulation.helpers.test_runner import SimulationTestRunner
runner = SimulationTestRunner()
result = runner.run_simulation_test('path/to/test_case.json')

# 命令行 (如果有脚本)
python run_e2e.py
```

### 生成报告
```bash
# 使用SimulationRunner
from src.simulation.runner import SimulationRunner
runner = SimulationRunner(virtual_pages, plan)
runner.run()

# 生成各种格式
runner.export_trace("jsonl")   # JSONL格式
runner.export_trace("html")    # HTML报告
runner.render_tree()           # ASCII树
runner.render_mermaid()        # Mermaid图
```

### 调试技巧
```python
# 查看实际生成的事件
from tests.simulation.helpers.assertions import TraceAsserter
for step in trace:
    event = TraceAsserter.step_to_nl(step)
    print(f"{step['step_number']}: {event}")

# 检查匹配状态
result = runner.run_simulation_test(...)
print(f"Matched: {result['assertion_result'].key_events_matched}/14")
print(f"Missing: {result['assertion_result'].missing_events}")
```

## 🎨 报告格式选择指南

| 格式 | 用途 | 优点 | 缺点 |
|-----|------|------|------|
| **TXT** | 日志和存档 | 简单、可读性好 | 缺乏可视化 |
| **HTML** | 演示和分享 | 交互式、美观 | 需要浏览器 |
| **JSONL** | 数据分析 | 机器可读、结构化 | 人类阅读困难 |
| **Mermaid** | 文档和图表 | 可渲染、标准格式 | 需要渲染工具 |
| **ASCII** | 调试和终端 | 无需工具、即时显示 | 大型树显示困难 |

## ⚠️ 常见陷阱

### 1. 事件描述不匹配
```python
# ❌ 错误
"点击 Settings按钮"  # 缺少引号

# ✓ 正确
"点击 'Settings' 按钮"  # 标准格式
```

### 2. TraceStep字段缺失
```python
# ❌ 错误
TraceStep(step_number=1, action="navigate")

# ✓ 正确  
TraceStep(
    step_number=1,
    action="navigate",
    node_id="Settings",
    screen_info={"target": "Settings", "element_type": "button"}
)
```

### 3. 完成原因未设置
```python
# ❌ 错误 - 最后一步没有设置完成原因
_log_trace_step("go_back", "", "navigation")

# ✓ 正确 - 明确设置完成原因
_log_trace_step("go_back", "", "navigation", 
                completion_reason="completed")
```

### 4. visited_tree数据结构错误
```python
# ❌ 错误 - 使用字典
self.tracer.visited_tree[page_key] = {
    "node_id": page_key,
    "name": page_name
}

# ✓ 正确 - 使用VisitedNode对象
from src.simulation.visualizer import VisitedNode
self.tracer.visited_tree[page_key] = VisitedNode(
    node_id=page_key,
    name=page_name,
    node_type="page"
)
```

## 🔧 快速修复清单

### 测试失败时检查

1. **事件匹配问题**
   - [ ] 打印实际事件列表
   - [ ] 对比预期事件格式
   - [ ] 检查特殊规则是否覆盖

2. **数据结构问题**
   - [ ] 验证TraceStep字段完整性
   - [ ] 检查to_dict()映射正确性
   - [ ] 确认VisitedNode对象使用

3. **逻辑问题**
   - [ ] 检查DFS遍历顺序
   - [ ] 验证go_back逻辑
   - [ ] 确认路径状态管理

### 报告生成失败时检查

1. **数据结构**
   - [ ] visited_tree使用VisitedNode对象
   - [ ] TraceStep包含所有必需字段
   - [ ] 时间戳格式正确 (ISO)

2. **方法调用**
   - [ ] 使用正确的方法名 (render_html vs generate_html_report)
   - [ ] 传递正确的参数
   - [ ] 检查返回值类型

## 📈 性能考虑

### 优化建议

1. **大型测试用例**
   - 使用`max_steps`限制执行步数
   - 设置合理的`max_depth`
   - 考虑分批测试

2. **报告生成**
   - HTML报告对于大型trace可能较慢
   - JSONL格式最适合大数据量
   - 使用ASCII树进行快速调试

3. **内存管理**
   - InMemoryTracer在内存中存储所有步骤
   - 大型测试考虑使用文件持久化
   - 定期清理visited_tree

## 🎯 学习路径

### 初学者
1. 理解基本流程 (测试数据 → Mock → 断言)
2. 运行现有测试用例
3. 修改test_case.json中的事件
4. 观察断言结果变化

### 中级
1. 创建新的测试用例 (新的pages.json)
2. 添加自定义事件描述规则
3. 修改DFS遍历深度和逻辑
4. 生成不同格式的报告

### 高级
1. 扩展Mock组件 (新的Vision/Action逻辑)
2. 实现自定义断言引擎
3. 添加新的报告格式
4. 集成到CI/CD流程

---

**相关文档**:
- 详细架构: [TESTING_ARCHITECTURE.md](TESTING_ARCHITECTURE.md)
- 项目文档: [README.md](README.md)
- 测试指南: [tests/README.md](tests/README.md)