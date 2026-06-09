# MCP Provider 便携式使用指南

## 快速开始

### 方案概览

本方案提供**无需API Key**的MCP工具使用方式，通过桥接服务器利用Claude Code的现有MCP连接。

### 三种使用模式

| 模式 | 需要API Key | 需要桥接服务器 | 适用场景 |
|------|------------|---------------|----------|
| **桥接模式** | 否 | 是 | 无API Key，有Claude Code |
| **直接API模式** | 是 | 否 | 有有效API Key |
| **混合模式** | 可选 | 是 | 桥接优先，API备用 |

---

## 模式1：桥接模式（推荐用于无API Key场景）

### 在有Claude Code的电脑上

**步骤1**: 启动桥接服务器
```bash
cd d:\space-x\uni_claw
python scripts/mcp_bridge.py
```

**步骤2**: 在另一个终端运行代码
```bash
# Windows PowerShell
$env:MCP_USE_BRIDGE="true"

# Linux/Mac
export MCP_USE_BRIDGE=true

# 运行测试
python scripts/test_mcp_bridge.py
```

### 跨机器使用

如果电脑A可从网络访问：

**电脑A（有Claude Code）**:
```bash
# 监听所有接口
python scripts/mcp_bridge.py --host 0.0.0.0 --port 8765
```

**电脑B（运行代码）**:
```bash
# 指向远程桥接服务器
export MCP_BRIDGE_URL=http://电脑A的IP:8765
export MCP_USE_BRIDGE=true
python your_script.py
```

---

## 模式2：直接API模式（有API Key时）

### 配置API Key

**临时设置**:
```bash
# Windows PowerShell
$env:ANTHROPIC_API_KEY="sk-ant-xxxxx"

# Linux/Mac
export ANTHROPIC_API_KEY=sk-ant-xxxxx
```

**永久设置**（推荐）:

创建 `config/ai_providers.local.yaml`:
```yaml
providers:
  mcp:
    class: "MCPProvider"
    config:
      api_key: "sk-ant-xxxxx"  # 你的API Key
      model: "claude-3-5-sonnet-20241022"
      base_url: "https://api.anthropic.com"
```

### 使用示例

```python
from src.ai.providers.mcp import MCPProvider
from src.ai.providers.base import AIProviderConfig

config = AIProviderConfig(
    api_key="从环境或配置读取",
    model="claude-3-5-sonnet-20241022",
    base_url="https://api.anthropic.com",
)

provider = MCPProvider(config)

# 读取图片
with open("screenshot.png", "rb") as f:
    image_data = f.read()

# 调用视觉分析
response = await provider.complete_vision(
    prompt="分析这个截图",
    image_data=image_data,
    max_tokens=4096,
)

print(response.content)  # PageAnalysis格式
```

---

## 模式3：混合模式（桥接优先，自动降级）

```python
response = await provider.complete_vision(
    prompt="分析这个截图",
    image_data=image_data,
    use_bridge=True,      # 尝试桥接
    bridge_only=False,   # 失败时降级到直接API
)
```

---

## 配置文件

### 主配置: `config/ai_providers.yaml`

```yaml
providers:
  mcp:
    class: "MCPProvider"
    config:
      api_key: "${ANTHROPIC_API_KEY}"  # 从环境变量读取
      model: "claude-3-5-sonnet-20241022"
      base_url: "https://api.anthropic.com"
      max_concurrent_requests: 4
      request_timeout: 60.0
    capabilities:
      - analyze_visual
    performance:
      latency: 0.7
      quality: 0.95
      efficiency: 0.8
```

### 本地覆盖配置: `config/ai_providers.local.yaml`

此文件不应提交到Git，用于个人配置：

```yaml
providers:
  mcp:
    config:
      api_key: "your_actual_key_here"
      base_url: "https://your-proxy.com"  # 如需代理
```

---

## 环境变量参考

| 变量名 | 说明 | 默认值 | 示例 |
|--------|------|--------|------|
| `MCP_USE_BRIDGE` | 启用桥接模式 | `false` | `true` |
| `MCP_BRIDGE_URL` | 桥接服务器地址 | `http://127.0.0.1:8765` | `http://192.168.1.100:8765` |
| `ANTHROPIC_API_KEY` | Claude API Key | 无 | `sk-ant-xxxxx` |
| `ANTHROPIC_BASE_URL` | API基础URL | `https://api.anthropic.com` | `https://proxy.com` |

---

## 换电脑使用清单

### 从电脑A迁移到电脑B

**需要的文件**:
```
uni_claw/              # 整个项目目录
├── config/
│   └── ai_providers.yaml
├── src/ai/providers/
│   └── mcp.py
├── scripts/
│   └── mcp_bridge.py
└── docs/
    └── MCP_PORTABLE_GUIDE.md
```

**配置步骤**:

1. **安装依赖**
```bash
pip install aiohttp pydantic
```

2. **选择使用模式**

   **选项A：使用桥接模式（推荐）**
   - 在电脑B上安装Claude Code VSCode扩展
   - 运行 `python scripts/mcp_bridge.py`
   - 设置 `export MCP_USE_BRIDGE=true`

   **选项B：使用直接API模式**
   - 获取API Key
   - 设置 `export ANTHROPIC_API_KEY=your_key`

3. **验证安装**
```bash
python scripts/test_mcp_bridge.py
```

---

## 测试

### 测试桥接模式
```bash
# 确保桥接服务器运行
python scripts/mcp_bridge.py &

# 运行测试
python scripts/test_mcp_bridge.py
```

### 测试直接API模式
```bash
# 设置API Key
export ANTHROPIC_API_KEY=your_key

# 运行测试
python scripts/test_mcp_vision.py
```

---

## 故障排查

### 问题: "Cannot reach bridge server"

**原因**: 桥接服务器未运行

**解决**:
```bash
# 检查服务状态
curl http://127.0.0.1:8765/health

# 启动服务
python scripts/mcp_bridge.py
```

### 问题: "API error 401: Invalid API key"

**原因**: API Key未设置或无效

**解决**:
```bash
# 检查环境变量
echo $ANTHROPIC_API_KEY

# 重新设置
export ANTHROPIC_API_KEY=正确的key
```

### 问题: 想使用桥接但调用的是直接API

**原因**: 未启用桥接模式

**解决**:
```bash
export MCP_USE_BRIDGE=true
```

---

## 架构说明

```
┌─────────────────────────────────────────────────────────────┐
│                     桥接模式架构                              │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  电脑A (有Claude Code)           电脑B (运行代码)           │
│  ┌───────────────────┐          ┌─────────────────┐         │
│  │ Claude Code       │          │                 │         │
│  │  ┌─────────────┐ │          │  MCPProvider    │         │
│  │  │ MCP工具     │ │          │  ┌───────────┐  │         │
│  │  │ (4.5v)      │◄─┼──────────┤  │调用桥接  │  │         │
│  │  └─────────────┘ │  HTTP     │  │服务器    │  │         │
│  │         ▲        │  Request  │  └───────────┘  │         │
│  │         │        │◄──────────┤                 │         │
│  │  ┌──────┴──────┐ │  Response │                 │         │
│  │  │Bridge Svr   │ │          │                 │         │
│  │  │:8765        │ │          │                 │         │
│  │  └─────────────┘ │          │                 │         │
│  └───────────────────┘          └─────────────────┘         │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 实际使用示例

### 示例1：截图分析

```python
import asyncio
from src.ai.providers.mcp import MCPProvider
from src.ai.providers.base import AIProviderConfig

async def analyze_screenshot(image_path: str):
    # 使用桥接模式
    config = AIProviderConfig(
        api_key="not_required",
        model="claude-3-5-sonnet-20241022",
        base_url="mcp://local",
    )

    provider = MCPProvider(config)

    with open(image_path, "rb") as f:
        image_data = f.read()

    response = await provider.complete_vision(
        prompt="分析这个移动应用截图",
        image_data=image_data,
        use_bridge=True,
    )

    return response.content

# 使用
result = asyncio.run(analyze_screenshot("screenshot.png"))
print(result)
```

### 示例2：在遍历中使用

```python
from src.ai import UniBrain, UniBrainConfig

# 配置使用MCP Provider
config = UniBrainConfig(
    provider="mcp",
    provider_config={
        "use_bridge": True,  # 启用桥接模式
    }
)

brain = UniBrain(config)

# 遍历中会自动使用MCP进行视觉分析
result = await brain.traverse_page(screenshot)
```

---

## 相关文档

- [MCP_PORTABLE_SOLUTION.md](MCP_PORTABLE_SOLUTION.md) - 详细解决方案说明
- [MCP_PROVIDER_GUIDE.md](MCP_PROVIDER_GUIDE.md) - 原始指南（已过时）
- [scripts/mcp_bridge.py](../scripts/mcp_bridge.py) - 桥接服务器代码
- [src/ai/providers/mcp.py](../src/ai/providers/mcp.py) - MCPProvider实现

---

**最后更新**: 2026-06-09
**状态**: 测试通过，可用
