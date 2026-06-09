# MCP Provider 跨机器使用指南

## 快速开始

### 1. 设置API Key

在任意电脑上设置环境变量：

**Windows (PowerShell):**
```powershell
$env:ANTHROPIC_API_KEY="your_api_key_here"
```

**Windows (CMD):**
```cmd
set ANTHROPIC_API_KEY=your_api_key_here
```

**Linux/Mac:**
```bash
export ANTHROPIC_API_KEY=your_api_key_here
```

### 2. 运行测试

```bash
cd d:\space-x\uni_claw
python scripts/test_vision_analysis.py --mcp --file tests/assets/images/settings_home.jpg
```

## 配置文件说明

### 主配置 (config/ai_providers.yaml)

```yaml
providers:
  mcp:
    class: "MCPProvider"
    config:
      api_key: "${ANTHROPIC_API_KEY}"  # 从环境变量读取
      model: "claude-3-5-sonnet-20241022"
      base_url: "https://api.anthropic.com"
```

### 本地测试配置 (可选)

创建 `config/ai_providers.local.yaml` 用于测试：

```yaml
providers:
  mcp:
    class: "MCPProvider"
    config:
      api_key: "your_test_key"  # 测试用硬编码
      base_url: "your_proxy_url"  # 如需代理
```

## 工作原理

```
┌─────────────────────────────────────────────────────┐
│              MCPProvider                              │
│  ┌──────────────────────────────────────────────┐  │
│  │ 1. 读取 ANTHROPIC_API_KEY 环境变量           │  │
│  │ 2. 调用 Claude Vision API                     │  │
│  │ 3. 返回 PageAnalysis 格式结果                │  │
│  └──────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────┐
│            Anthropic Claude API                     │
│        (任何有API key的电脑都可访问)                │
└─────────────────────────────────────────────────────┘
```

## 换电脑使用

### 需要的文件

1. **项目代码**: 整个 `uni_claw` 目录
2. **API Key**: 在新电脑上设置环境变量
3. **依赖**: `pip install -r requirements.txt`

### 步骤

```bash
# 1. 克隆/复制项目
git clone <repo_url>
cd uni_claw

# 2. 安装依赖
pip install -r requirements.txt

# 3. 设置API Key
export ANTHROPIC_API_KEY=your_key_here

# 4. 运行测试
python scripts/test_vision_analysis.py --mcp --file tests/assets/images/settings_home.jpg
```

## 故障排查

### 问题: "API error 401: Invalid API key"

**原因**: API key未设置或无效

**解决**:
```bash
# 检查环境变量
echo $ANTHROPIC_API_KEY  # Linux/Mac
echo %ANTHROPIC_API_KEY%  # Windows

# 重新设置
export ANTHROPIC_API_KEY=正确的key
```

### 问题: 模块导入错误

**解决**:
```bash
# 安装依赖
pip install aiohttp pydantic
```

## 与ClaudeProvider的区别

| 特性 | MCPProvider | ClaudeProvider |
|------|-------------|----------------|
| API | Claude Vision API | Claude Vision API |
| 配置 | 环境变量优先 | 配置文件 |
| 用途 | 可移植的vision分析 | 项目专用 |

## 实际使用示例

```python
from src.ai.providers import MCPProvider, AIProviderConfig
import asyncio

# 使用环境变量中的API key
config = AIProviderConfig(
    api_key="not_required",  # 会从环境变量读取
    model="claude-3-5-sonnet-20241022",
    base_url="https://api.anthropic.com"
)

provider = MCPProvider(config)

# 读取图片
with open("screenshot.png", "rb") as f:
    image_data = f.read()

# 调用vision分析
response = await provider.complete_vision(
    prompt="分析这个截图",
    image_data=image_data
)

print(response.content)  # PageAnalysis格式
```
