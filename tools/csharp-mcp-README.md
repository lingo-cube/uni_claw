# C# MCP 语义检索工具（csharp-mcp-query.py）

在本 DSH 环境里，项目 `.mcp.json` 声明的两个 C# 语义 MCP 服务器
（`csharper-mcp` / `cwm-roslyn-navigator`）**不会自动注册成 DSH 原生工具**
（DSH 不消费项目 `.mcp.json`，需经 `cordis.patch.yml` + `@deepseek-ai/dsh-mcp-client`
插件接入，且当前那台机器的 npm 版本落后）。

本脚本是**可靠的替代执行路径**：直接起 .NET MCP 服务器子进程，按 MCP stdio
JSON-RPC 协议握手，用**真实 Roslyn 语义导航**检索 C#（满足项目
`AGENTS.md` / `.ai/tooling/csharp-mcp-query.md` 的「查询 C# 始终 MCP 优先」规则）。

## 前置

- 两个服务器已作为 .NET Global Tools 安装：
  - `~/.dotnet/tools/cwm-roslyn-navigator` (0.7.0)
  - `~/.dotnet/tools/csharper-mcp` (0.1.6)
- 脚本自动注入 `DOTNET_ROOT=$HOME/.dotnet` 与 `DOTNET_MULTILEVEL_LOOKUP=0`
  （DSH 会 scrub 凭据形状环境变量，必须显式给子进程，否则 .NET 报 not found）。

## 用法

```bash
python3 tools/csharp-mcp-query.py find_symbol --name <symbol>
python3 tools/csharp-mcp-query.py find_references --symbolName <symbol>
python3 tools/csharp-mcp-query.py get_diagnostics --scope File --path <file.cs>
python3 tools/csharp-mcp-query.py find_references --symbolName <symbol> --file <path> --line <N>
python3 tools/csharp-mcp-query.py tools   # 列出可用工具
```

## 可靠性说明

- Roslyn **冷 workspace 首次加载需 ~30-60s**；脚本在同一条连接上按退避重查，
  直到返回真实结果（或约 90s 超时）。首次调用慢，之后走缓存。
- 服务器日志（`#` / `info:` 行）写入 stdout，脚本已过滤。
- 每次调用启动一个子进程 → ~1-2s 握手开销 + 首次 workspace 加载。

## 示例（已验证）

```
$ python3 tools/csharp-mcp-query.py find_symbol --name IsResolvedParentReturnControl
{"Symbols":[{"Name":"IsResolvedParentReturnControl","Kind":"method",
  "File":".../src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs","Line":1254,
  "Namespace":"UniClaw.Runtime.Agent"}]}
```

## 未来：DSH 原生接入（可选）

若要让其成为原生 `mcp__*` 工具，需在 `~/.dsh/profiles/web/cordis.patch.yml`
注册两个 `@deepseek-ai/dsh-mcp-client` 插件实例，并显式带 `env.DOTNET_ROOT` /
`env.DOTNET_MULTILEVEL_LOOKUP`；且需解决该插件 npm 版本落后（本地 `0.1.0-rc.5`
vs npm `0.0.1-rc.1`）的解析问题 + profile 重载（会打断当前会话）。
在那之前，本脚本是稳定入口。
