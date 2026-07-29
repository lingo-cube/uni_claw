# MCP 工具优先 — C# 代码查询规则

> 本文件是「C# 代码查询 MCP 优先」规则的单点真源。
> - AGENTS.md「## 代码查询：MCP 工具优先」段引用本文件
> - CLAUDE.md 引导 Claude Code 先读取 AGENTS.md
> - `.claude/commands/opsx/AGENT.md` 引用本文件，让 OpenSpec 子代理也遵守
> - 改规则只改这里，AGENTS.md、CLAUDE.md 与 AGENT.md 不重复内容

## 核心规则

查询 C# 代码（定义、引用、继承、诊断）时，**始终先用 MCP 工具定位，再用 Read 按需读片段**。
MCP 一次查询 ~100-500 tokens，grep + Read 同类探索 ~2000-5000 tokens，节省 80-90%。

## 可用 MCP 服务器

两个服务器能力有重叠，各有独到之处 —— 不是读写分工，是**导航 / 重构**两个场景各有侧重：

| 服务器 | 命令 | 定位 |
|--------|------|------|
| `cwm-roslyn-navigator` | `cwm-roslyn-navigator --solution <sln>` | **Claude 日常导航首选**：`find_symbol`, `find_references`, `get_type_hierarchy`, 死代码检测, 反模式检测。当前 Codex 配置中先禁用 0.7.0，直到它能稳定完成 `tools/list` 握手 |
| `csharper-mcp` | `csharper-mcp --workspace <sln>` | **Codex 当前首选 + 重构 + DLL 探索**：`get_code_actions` / `apply_code_action`（安全重命名等）, `get_decompiled_source`（看 BCL/NuGet 源码）, `get_symbol_info` |

两者都支持：符号定义查找、引用查找、编译器诊断。

## 工作流：查询 → 定位 → 阅读

```
MCP 查询（获取 file:line + 签名）
    → 需要看实现？Read(file, offset, limit) 只读相关行
        → 修改或决策
```

1. **MCP 定位**：拿到精确的文件路径、行号、签名、XML 文档
2. **按需 Read**：需要实现细节时，Read 目标符号所在的行范围（几十行），不读整个文件
3. **禁止 grep**：不要用 `grep` / `find` 定位 C# 符号 —— MCP 提供语义理解，文本搜索做不到（e.g. 同名不同重载、partial class 分散在多个文件）
4. **Partial 类先查全**：C# 的 `partial class` 可能分散在多个文件。修改前必须用 `find_symbol` 查看所有分部位置，避免改了 A 文件漏了 B 文件

## 常用查询速查

| 需求 | 工具 | 服务器 | 示例 |
|------|------|--------|------|
| 查找类/方法定义 | `find_symbol` / `get_definition_location` | roslyn-navigator / csharper-mcp | `find_symbol(name="ContainerHandler")` / `get_definition_location(symbolName="ContainerHandler")` |
| 完整签名 + XML 文档 | `get_symbol_detail` | roslyn-navigator | `get_symbol_detail(symbolName="HandleContainer")` |
| 查找所有引用 | `find_references` | roslyn-navigator | `find_references(symbolName="PlanCompiler")` |
| 查找调用方 | `find_callers` | roslyn-navigator | `find_callers(methodName="Compile")` |
| 类型继承树 | `get_type_hierarchy` | roslyn-navigator | `get_type_hierarchy(typeName="ITraversalNode")` |
| 接口实现 / 虚方法重写 | `find_implementations` / `find_overrides` | roslyn-navigator | — |
| 调用依赖图 | `get_dependency_graph` | roslyn-navigator | `get_dependency_graph(symbolName="HandleContainer", depth=3)` |
| 项目依赖树 | `get_project_graph` | roslyn-navigator | — |
| 死代码 / 反模式检测 | `find_dead_code` / `detect_antipatterns` | roslyn-navigator | — |
| 编译器诊断 | `get_diagnostics` | roslyn-navigator / csharper-mcp | `get_diagnostics(scope="solution")` |
| 代码重构 (安全重命名等) | `get_code_actions` → `apply_code_action` | csharper-mcp | — |
| 查看 BCL/NuGet DLL 源码 | `get_decompiled_source` | csharper-mcp | `get_decompiled_source(typeName="System.String")` ⚠️ 带 `includeImplementation` 可能 >2000 tokens，先不带看签名 |
| 符号类型 + 命名空间 | `get_symbol_info` | csharper-mcp | — |

## 工具跨机器策略

新增工具时按以下原则选择装方式，保证 `git clone` 后即可工作：

| 工具类型 | 方式 | 示例 |
|---------|------|------|
| MCP 服务器（常驻进程） | `.mcp.json` + 文档说明 | `csharper-mcp`, `cwm-roslyn-navigator` |
| 构建/测试依赖 | NuGet `PackageReference` | xUnit, System.Text.Json |
| 开发时偶尔用的 CLI | `npx` 免安装 | `npx token-ninja` |

原则：**能不装就不装**。npx 首次慢 2 秒但零残留，换机器零成本。
