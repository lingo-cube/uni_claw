# MCP Provider 便携式解决方案

## 问题背景

需要在不同电脑上使用MCP工具进行视觉分析，但：
1. 找不到有效的API key
2. 想利用Claude Code VSCode中已有的MCP连接
3. 需要跨机器可移植的解决方案

## 解决方案：桥接服务器模式

### 架构概述

```
┌─────────────────────────────────────────────────────────────────┐
│                    电脑 A (有 Claude Code)                        │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              Claude Code VSCode                            │  │
│  │  ┌──────────────┐         ┌────────────────────────────┐  │  │
│  │  │ MCP 工具集    │◄────────│  MCP Bridge Server        │  │  │
│  │  │ (4.5v vision) │         │  http://127.0.0.1:8765    │  │  │
│  │  └──────────────┘         └────────────────────────────┘  │  │
│  │                                      ▲                       │  │
│  └──────────────────────────────────────┼───────────────────────┘  │
│                                         │ HTTP                       │
└─────────────────────────────────────────┼───────────────────────────┘
                                          │
                                          │
┌─────────────────────────────────────────┼───────────────────────────┐
│                    电脑 B (运行代码)                               │
│                                         │                           │
│  ┌──────────────────────────────────────┼───────────────────────┐  │
│  │           MCPProvider                 │                       │  │
│  │  ┌───────────────────────────────────┼───────────────────┐  │  │
│  │  │  complete_vision()                │                   │  │  │
│  │  │    ↓                              │                   │  │  │
│  │  │  _call_bridge_server()           │                   │  │  │
│  │  │    ↓                              │                   │  │  │
│  │  │  HTTP POST → MCP 分析结果         │                   │  │  │
│  │  └───────────────────────────────────┼───────────────────┘  │  │
│  └──────────────────────────────────────┼───────────────────────┘  │
│                                          │                           │
└──────────────────────────────────────────┼───────────────────────────┘
```

### 核心组件

#### 1. MCP Bridge Server (`scripts/mcp_bridge.py`)

在Claude Code环境中运行的桥接服务器：

```bash
# 在 Claude Code 中运行
python scripts/mcp_bridge.py
```

功能：
- 监听 `http://127.0.0.1:8765`
- 接收HTTP请求
- 调用MCP工具（如 `mcp__4_5v_mcp__analyze_image`）
- 返回PageAnalysis格式结果

#### 2. MCPProvider (`src/ai/providers/mcp.py`)

标准AI Provider接口实现：

```python
from src.ai.providers.mcp import MCPProvider
from src.ai.providers.base import AIProviderConfig

config = AIProviderConfig(
    api_key="not_required",
    model="claude-3-5-sonnet-20241022",
    base_url="mcp://local",
)

provider = MCPProvider(config)

# 使用桥接模式
response = await provider.complete_vision(
    prompt="分析这个截图",
    image_data=image_bytes,
    use_bridge=True,  # 启用桥接模式
)
```

## 使用指南

### 场景1：在有Claude Code的电脑上使用

**步骤1**: 启动桥接服务器

```bash
# 在 Claude Code 终端中
cd d:\space-x\uni_claw
python scripts/mcp_bridge.py
```

**步骤2**: 在另一个终端运行代码

```bash
# 设置环境变量启用桥接模式
export MCP_USE_BRIDGE=true

# 运行测试
python scripts/test_mcp_bridge.py
```

### 场景2：在其他电脑上使用

**方案A：远程桥接**

如果电脑A可从网络访问：

```bash
# 在电脑A上启动桥接服务器（监听所有接口）
python scripts/mcp_bridge.py --host 0.0.0.0

# 在电脑B上设置远程地址
export MCP_BRIDGE_URL=http://电脑A_IP:8765
export MCP_USE_BRIDGE=true
python your_script.py
```

**方案B：本地Claude Code**

在任何电脑上：

1. 安装Claude Code VSCode扩展
2. 在Claude Code中运行桥接服务器
3. 设置 `MCP_USE_BRIDGE=true`

### 场景3：有API Key时直接使用

如果有有效的API key，可以直接使用不需要桥接：

```bash
# 设置API Key
export ANTHROPIC_API_KEY=your_key_here

# 不设置 MCP_USE_BRIDGE，provider会自动使用直接API模式
python your_script.py
```

## 配置选项

| 环境变量 | 说明 | 默认值 |
|---------|------|--------|
| `MCP_USE_BRIDGE` | 启用桥接模式 | `false` |
| `MCP_BRIDGE_URL` | 桥接服务器地址 | `http://127.0.0.1:8765` |
| `ANTHROPIC_API_KEY` | Claude API Key | 无 |
| `ANTHROPIC_BASE_URL` | API基础URL | `https://api.anthropic.com` |

## 运行模式

MCPProvider支持三种运行模式：

### 模式1：桥接模式（无API Key）

```bash
export MCP_USE_BRIDGE=true
# 需要桥接服务器运行
```

### 模式2：直接API模式（有API Key）

```bash
export ANTHROPIC_API_KEY=your_key
# 不设置 MCP_USE_BRIDGE
```

### 模式3：混合模式（桥接优先，失败降级）

```python
response = await provider.complete_vision(
    prompt="分析",
    image_data=image,
    use_bridge=True,  # 尝试桥接
    bridge_only=False,  # 失败时降级到直接API
)
```

## 测试

```bash
# 1. 启动桥接服务器（在Claude Code中）
python scripts/mcp_bridge.py

# 2. 运行测试（在另一个终端）
python scripts/test_mcp_bridge.py
```

## 优势

1. **无需API Key**: 利用Claude Code的现有连接
2. **可移植**: 任何有Claude Code的电脑都能使用
3. **标准接口**: 使用统一的AIProvider接口
4. **自动降级**: 桥接失败时可降级到直接API
5. **灵活配置**: 支持环境变量和代码配置

## 限制

1. 桥接服务器需要在Claude Code环境中运行
2. 桥接服务器仅监听本地（可改为监听所有接口）
3. 当前使用Mock响应，需要集成真实MCP工具调用

## 下一步

桥接服务器当前返回Mock数据。要集成真实MCP工具，需要：

1. 研究Claude Code的MCP客户端API
2. 或使用MCP Python SDK直接连接到MCP服务器
3. 实现真实的 `mcp__4_5v_mcp__analyze_image` 调用

## 故障排查

### 问题：连接桥接服务器失败

**解决**：
```bash
# 检查桥接服务器是否运行
curl http://127.0.0.1:8765/health

# 应返回：{"status": "ok", "server": "mcp-bridge"}
```

### 问题：返回Mock数据

**说明**：当前桥接服务器返回模拟数据。真实MCP集成需要访问Claude Code的MCP客户端API。

### 问题：想要不依赖Claude Code

**方案**：使用直接API模式，需要有效的API Key。

## 相关文件

- `scripts/mcp_bridge.py` - 桥接服务器实现
- `src/ai/providers/mcp.py` - MCPProvider实现
- `scripts/test_mcp_bridge.py` - 测试脚本
- `config/mcp_portable.yaml` - 便携式配置
