# V6.3+ Enhanced Visualization System

基于V6.3分布式追踪和V6.4仿真接口对齐的增强可视化系统。

## 功能特性

### 1. 增强的Trace Observatory Dashboard

**文件**: `trace_viewer_v6.html` + `trace_server_v6.py`

#### 核心功能

- **Trace Tree**: 完整的Session/Step/Span三层节点树可视化
- **State Flow**: 状态机转换流程可视化
- **Timeline**: 按时间顺序的操作执行时间轴
- **Span Chain**: 分布式追踪调用链视图（新增）
- **Performance**: 性能指标分析（新增）
- **Coverage**: 页面覆盖率热力图（新增）
- **AI Calls**: AI服务调用详情
- **Error Tracking**: 错误统计和详细信息

#### V6特性支持

- 仿真模式标识（🔬 SIMULATION badge）
- Session元数据展示（plan_id, traversal_mode, status）
- 完整的Span类型分类
- 错误严重度分级（error, critical）
- 性能百分位指标（P50, P95）
- 页面访问热力图

### 2. 增强的仿真测试

**文件**: `scripts/run_rich_simulation.py`

#### 支持的场景

1. **动态匹配场景** (dynamic_settings)
   - 使用DYNAMIC_MATCH策略
   - 自动匹配menu_item、switch、slider等元素
   - 支持嵌套容器遍历

2. **静态路径场景** (static_path)
   - 使用STATIC策略
   - 预定义遍历路径
   - 目标查找测试

3. **错误处理场景** (error_handling)
   - 模拟错误情况
   - 验证错误恢复机制

## 使用方法

### 运行仿真测试

```bash
# 运行3个仿真场景
python scripts/run_rich_simulation.py --count 3

# 查看帮助
python scripts/run_rich_simulation.py --help
```

### 启动可视化Dashboard

```bash
# 启动增强版服务器（端口8080）
python dashboards/trace_server_v6.py

# 自定义端口
python dashboards/trace_server_v6.py --port 9000

# 自定义trace目录
python dashboards/trace_server_v6.py --trace-dir ../traces
```

然后访问: http://localhost:8080

### API端点

| 端点 | 方法 | 描述 |
|------|------|------|
| `/` | GET | HTML dashboard（V6增强版） |
| `/api/traces` | GET | 列出所有trace |
| `/api/trace?id={trace_id}` | GET | 获取trace概览 |
| `/api/tree?id={trace_id}` | GET | 获取trace树 |
| `/api/analysis?id={trace_id}` | GET | 获取完整分析数据 |
| `/api/span-chain?id={trace_id}` | GET | 获取调用链（新增） |
| `/api/performance?id={trace_id}` | GET | 获取性能指标（新增） |
| `/api/errors?id={trace_id}` | GET | 获取错误详情（新增） |

## Trace数据格式

### 目录结构

```
traces/{trace_id}/
├── session.json    # Session元数据
├── trace.jsonl      # 所有节点（每行一个JSON）
└── screenshots/     # 截图引用（可选）
```

### 节点类型

| 节点类型 | 说明 | 字段 |
|---------|------|------|
| `session` | 遍历会话根节点 | device_model, os_version, status |
| `step` | 遍历步骤节点 | node_id, step_type, page_path |
| `span` | 操作节点 | span_type, action, duration_ms |

### Span类型

| Span类型 | 说明 |
|---------|------|
| `state_transition` | 状态机转换 |
| `execution` | 操作执行 |
| `ai_call` | AI服务调用 |
| `error` | 错误事件 |
| `step_end` | 步骤结束 |
| `session_end` | 会话结束 |

## 可视化组件

### Metrics Row

顶部指标卡片显示：
- Steps: 步骤数
- Spans: Span节点数
- AI Calls: AI调用次数
- Duration: 总耗时（ms）
- Pages: 访问页面数
- Errors: 错误数
- Coverage: 覆盖率百分比

### 左侧面板：Trace Tree

层次化展示trace节点树：
- 可展开/折叠的树结构
- 图标标识节点类型
- 颜色编码的标签
- 状态指示器

### 中间面板：多Tab视图

**State Flow Tab**
- 状态转换序列
- from_state → to_state
- 时间戳

**Timeline Tab**
- 按时间排序的事件
- 错误高亮显示
- 操作详情

**Span Chain Tab**（新增）
- 完整调用链视图
- Span层级关系
- 性能数据

**Performance Tab**（新增）
- 延迟百分位（P50, P95）
- 最慢Span列表
- 进度条可视化

**Coverage Tab**（新增）
- 页面访问热力图
- 访问次数统计
- 颜色梯度指示

### 右侧面板：统计信息

**Session Info**
- Trace ID
- Plan ID
- 仿真模式标识
- 状态
- 持续时间

**Statistics**
- 时间分析
- 覆盖率统计

**Errors**
- 错误列表
- 错误类型分组
- 严重度标识

## 技术栈

- **前端**: 纯HTML/CSS/JavaScript（无框架）
- **后端**: Python HTTPServer
- **数据**: JSON/JSONL
- **样式**: 自定义深色主题
- **字体**: JetBrains Mono（代码）+ Orbitron（标题）

## 颜色方案

```css
--accent-green: #00e05a   /* 成功/完成 */
--accent-cyan: #00c8e8    /* 主要操作 */
--accent-amber: #f0a030   /* 警告/执行 */
--accent-violet: #9060e8  /* 状态转换 */
--accent-blue: #4088e0    /* AI调用 */
--accent-red: #e84040     /* 错误 */
```

## 开发说明

### 添加新的可视化Tab

1. 在HTML中添加Tab按钮和内容div
2. 在JavaScript中实现`render{TabName}()`函数
3. 添加`switchTab('{tabname}')`支持
4. 在API端点中添加数据获取逻辑

### 扩展API端点

在`trace_server_v6.py`的`TraceAPIHandler`类中：

1. 添加新的do_GET分支
2. 实现数据获取方法
3. 返回JSON响应

## 兼容性

- 向后兼容V6.3 trace格式
- 支持旧版trace_viewer.html
- API端点保持兼容

## 性能优化

- 前端按需加载数据
- API响应分页（大trace）
- 树节点懒渲染

## 未来增强

- [ ] 实时WebSocket推送
- [ ] Trace对比视图
- [ ] 导出报告（PDF/HTML）
- [ ] 更多性能图表
- [ ] 自定义时间范围
