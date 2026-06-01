# 遍历可观测性系统

## 概述

Uni-Claw 遍历系统集成了完整的可观测性功能，包括：
- **结构化日志**: JSONL 格式的操作日志
- **结果管理**: 遍历结果的存储和报告生成
- **指标收集**: AI 服务调用和遍历性能指标
- **分布式追踪**: 跨组件的调用链追踪
- **可视化面板**: Web 仪表板用于实时分析

## 输出目录结构

```
.
├── .logs/
│   └── traversal_<session_id>.jsonl          # 结构化操作日志
├── .results/
│   ├── sessions/
│   │   └── <session_id>_timestamp.json       # 遍历结果
│   ├── reports/
│   │   ├── <session_id>_timestamp.html      # HTML 报告
│   │   └── <session_id>_timestamp.md        # Markdown 报告
│   └── screenshots/
│       └── <session_id>_step<N>_*.png        # 步骤截图（可选）
└── .traces/
    └── *.jsonl                               # 分布式追踪日志
```

## 数据内容

### 结构化日志 (.logs/*.jsonl)
记录所有遍历操作事件：
- `session_start`: 会话开始（指令、参数）
- `step`: 每步操作（action、target、coordinate、success、duration_ms）
- `visited_item`: 访问的菜单项（name、type、path、coordinate）
- `skipped_item`: 跳过的项目（name、reason）
- `ai_call`: AI 服务调用（service、operation、duration_ms、success、confidence）
- `screen_analysis`: 屏幕分析（items_count、path、duration_ms）
- `error`: 错误事件（error_type、error_message、error_trace、context）
- `session_end`: 会话结束（status、steps、visited、duration_ms）

### 遍历结果 (.results/sessions/*.json)
完整的遍历结果包含：
```json
{
  "session_id": "traversal_abc123",
  "trace_id": "trace_xyz789",
  "status": "success",
  "instruction": "遍历所有系统设置的选项",
  "entry_app": "设置",
  "max_steps": 50,
  "steps": [...],
  "visited_items": [
    {
      "name": "WiFi",
      "type": "menu_item",
      "path": ["设置", "网络与互联网"],
      "coordinate": {"x": 0.5, "y": 0.3}
    }
  ],
  "skipped_items": [
    {
      "name": "恢复出厂设置",
      "reason": "safety_check"
    }
  ],
  "failed_items": [],
  "screens_analyzed": 15,
  "total_duration_ms": 45000,
  "final_path": ["设置"]
}
```

### 分布式追踪 (.traces/*.jsonl)
跨组件调用链追踪：
- 组件间调用关系
- 每个操作的耗时
- 输入/输出数据
- 错误传播路径

## 命令参考

### 执行遍历

```bash
# 基本遍历
uv run python scripts/run_brain_traversal_complete.py \
  "遍历所有系统设置的选项（注意安全）" \
  --device 127.0.0.1:6555 \
  --max-steps 50

# 显示计划详情
uv run python scripts/run_brain_traversal_complete.py \
  "遍历所有系统设置的选项（注意安全）" \
  --device 127.0.0.1:6555 \
  --max-steps 50 \
  --visualize

# 重置状态重新遍历
uv run python scripts/run_brain_traversal_complete.py \
  "遍历所有系统设置的选项（注意安全）" \
  --device 127.0.0.1:6555 \
  --max-steps 50 \
  --reset

# 自定义会话ID（便于关联日志）
uv run python scripts/run_brain_traversal_complete.py \
  "遍历所有系统设置的选项（注意安全）" \
  --device 127.0.0.1:6555 \
  --max-steps 50 \
  --session-id settings-traversal-001
```

### 参数说明

| 参数 | 说明 | 默认值 |
|-----|------|--------|
| `instruction` | 用户自然语言指令 | - |
| `--device` | ADB 设备 ID | 127.0.0.1:6555 |
| `--max-steps` | 最大遍历步数 | 200 |
| `--reset` | 重置遍历状态 | false |
| `--visualize` | 显示遍历计划 | false |
| `--session-id` | 自定义会话 ID | 自动生成 |

### 分析面板

```bash
# 启动分析仪表板（默认 http://127.0.0.1:8000）
uv run python scripts/analysis_dashboard.py

# 自定义端口
uv run python scripts/analysis_dashboard.py --port 8080

# 自定义追踪目录
uv run python scripts/analysis_dashboard.py --trace-dir .traces

# 允许外部访问
uv run python scripts/analysis_dashboard.py --host 0.0.0.0 --port 8000
```

### 快速查询

```bash
# 查看访问的项目数量
cat .results/sessions/*.json | jq '.visited_items | length'

# 查看结构化日志
tail -20 .logs/*.jsonl | jq '.'

# 查看追踪日志
tail -20 .traces/*.jsonl | jq '.'

# 查看跳过的危险项目
cat .results/sessions/*.json | jq '.skipped_items[]'

# 统计 AI 调用次数
grep '"type":"ai_call"' .logs/*.jsonl | wc -l

# 查看失败的步骤
grep '"success":false' .logs/*.jsonl | jq '.'
```

## 可视化面板功能

分析面板 (http://127.0.0.1:8000) 提供以下功能：

### 实时监控
- 当前会话状态
- 实时步数更新
- 性能指标趋势

### 性能分析
- 各组件耗时分布
- 最慢操作排行
- AI 调用成功率
- 置信度趋势图

### 结果可视化
- 访问项目树状图
- 遍历路径时间线
- 跳过/失败项目列表

### 追踪分析
- 调用链路图
- 组件依赖关系
- 错误传播路径

## 使用示例

### 完整工作流

```bash
# 1. 运行遍历
uv run python scripts/run_brain_traversal_complete.py \
  "遍历所有系统设置的选项（注意安全）" \
  --device 127.0.0.1:6555 \
  --max-steps 50 \
  --session-id settings-test-001

# 2. 查看结果摘要
# （命令行输出中已包含）

# 3. 查看 HTML 报告
open .results/reports/settings-test-001_*.html

# 4. 启动分析面板深入分析
uv run python scripts/analysis_dashboard.py

# 5. 浏览器打开 http://127.0.0.1:8000
```

### 调试工作流

```bash
# 1. 运行遍历
uv run python scripts/run_brain_traversal_complete.py \
  "遍历所有系统设置的选项（注意安全）" \
  --device 127.0.0.1:6555 \
  --max-steps 10

# 2. 查看失败的步骤
grep '"success":false' .logs/*.jsonl | jq '.'

# 3. 查看错误追踪
grep '"type":"error"' .logs/*.jsonl | jq '.'

# 4. 分析慢操作
cat .traces/*.jsonl | jq 'select(.duration_ms > 1000)'

# 5. 在面板中查看性能瓶颈
uv run python scripts/analysis_dashboard.py
```

### 性能优化工作流

```bash
# 1. 运行基准遍历
uv run python scripts/run_brain_traversal_complete.py \
  "遍历所有系统设置的选项（注意安全）" \
  --device 127.0.0.1:6555 \
  --max-steps 100 \
  --session-id benchmark-001

# 2. 分析 AI 调用性能
cat .logs/benchmark-001*.jsonl | \
  jq 'select(.type=="ai_call") | {service, operation, duration_ms}'

# 3. 识别优化机会
# - 高延迟的 AI 调用
# - 重复的分析操作
# - 低置信度的识别

# 4. 在面板中查看趋势图
uv run python scripts/analysis_dashboard.py
```

## 编程接口

### 结果管理

```python
from src.analysis import get_result_manager

manager = get_result_manager()

# 加载结果
result = manager.load_result("traversal_abc123")

# 生成报告
html_path = manager.generate_report(result, "html")
md_path = manager.generate_report(result, "markdown")

# 获取所有结果
all_results = manager.get_all_results(limit=10)
```

### 结构化日志

```python
from src.analysis import LoggerFactory

logger = LoggerFactory.get_logger("my_session")

# 记录事件
logger.log_session_start("遍历设置", 50, "设置")
logger.log_step("tap", "WiFi", {"x": 0.5, "y": 0.3}, True, 150)
logger.log_visited_item("WiFi", "menu_item", ["设置"], {"x": 0.5, "y": 0.3})
logger.log_session_end("success", 5, 3, 15000)
```

### 追踪分析

```python
from src.analysis import TraceAnalyzer

analyzer = TraceAnalyzer()

# 加载所有追踪
sessions = analyzer.load_all_traces()

# 分析性能
perf = analyzer.analyze_component_performance()

# 获取最慢操作
slowest = analyzer.get_slowest_operations(10)
```

## 最佳实践

1. **会话标识**: 为重要遍历指定明确的 `--session-id`，便于后续分析
2. **结果归档**: 定期清理旧的 `.logs` 和 `.traces` 文件
3. **报告保存**: 重要的 HTML 报告可以归档用于文档
4. **监控运行**: 长时间遍历建议同时运行分析面板监控进度
5. **错误分析**: 使用 `jq` 等工具快速定位问题日志

## 故障排查

### 日志文件为空
- 检查文件权限
- 确认目录存在（`.logs`、`.results`、`.traces`）
- 查看是否有导入错误

### 追踪不完整
- 检查是否正常结束（Ctrl+C 会中断追踪）
- 查看日志中的错误事件
- 确认 TraceLogger 正确初始化

### 面板无法访问
- 确认端口未被占用
- 检查防火墙设置
- 查看控制台错误信息
