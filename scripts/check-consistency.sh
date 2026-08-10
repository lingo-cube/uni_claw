#!/usr/bin/env bash
#
# check-consistency.sh — Greenfield 文档/结构机械检查器
#
# 依据: OpenAI "Harness Engineering" (2026-02) — "Docs rot; lint rules don't."
#       机械约束比文档更可靠; 每条检查失败时输出"违反什么 + 为什么 + 修复指引"。
# 配合: tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs (编译期 Guard)
# 用法: scripts/check-consistency.sh
# 期望: 全部 C1..C10 PASS, 任意 FAIL 则 exit 1
#
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FAIL=0

check() {
  local id="$1" desc="$2" ok="$3" fix="$4"
  if [ "$ok" = "1" ]; then
    echo "PASS $id — $desc"
  else
    echo "FAIL $id — $desc"
    echo "     修复: $fix"
    FAIL=1
  fi
}

file_has() { # file pattern -> 1/0
  grep -qE -- "$2" "$1" 2>/dev/null && echo 1 || echo 0
}

CHARTER="$ROOT/docs/system/greenfield-runtime-charter.md"
CONTRACT="$ROOT/docs/system/constitution/runtime-architecture-contract.md"
AGENTS="$ROOT/AGENTS.md"
AI_AGENT_ROUTING="$ROOT/.ai/agent-routing.md"
AI_MODEL_ROUTING="$ROOT/.ai/model-routing.yaml"
CLAUDE_MODEL_ROUTING="$ROOT/.claude/model-routing.md"
MCP_QUERY="$ROOT/.claude/MCP-QUERY.md"
RUNTIME_DIR="$ROOT/src/UniClaw.Runtime"
GUARD="$ROOT/tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs"

# C1 — 宪章存在且 60 节齐全（宪章是 Greenfield 行为指导唯一真源）
SECS=$(grep -cE '^### [0-9]+\.' "$CHARTER" 2>/dev/null || echo 0)
check C1 "宪章 60 节齐全（实际 ${SECS}）" \
  "$([ "${SECS:-0}" -eq 60 ] && echo 1 || echo 0)" \
  "补充或合并缺节，保持 docs/system/greenfield-runtime-charter.md 的 ### N. 编号连续"

# C2 — Contract 存在且 14 条 invariant 齐全（v1.1 新增 I-13 / I-14）
INVS=$(grep -cE '^### I-[0-9]+' "$CONTRACT" 2>/dev/null || echo 0)
check C2 "Contract 14 invariants 齐全（实际 ${INVS}）" \
  "$([ "${INVS:-0}" -eq 14 ] && echo 1 || echo 0)" \
  "补齐 docs/system/constitution/runtime-architecture-contract.md 的 I-1..I-14"

# C3 — AGENTS.md 提供 Greenfield 导航（AGENTS.md 是 map, 不是 manual）
check C3 "AGENTS.md 含 Runtime Greenfield 导航段" \
  "$(file_has "$AGENTS" 'Agent Runtime（新）— Greenfield')" \
  "在 AGENTS.md 添加「Agent Runtime（新）— Greenfield」段，指向宪章/Contract/Guard"

# C4 — Guard 测试存在（机械约束必须先于业务代码存在）
check C4 "ArchitectureGuardTests 存在" \
  "$([ -f "$GUARD" ] && echo 1 || echo 0)" \
  "恢复 tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs"

# C5 — csproj 零 ProjectReference（Greenfield 隔离, 第一阶段不引用 UniClaw.Core）
if [ -f "$RUNTIME_DIR/UniClaw.Runtime.csproj" ]; then
  REFS=$(grep -c '<ProjectReference' "$RUNTIME_DIR/UniClaw.Runtime.csproj")
  check C5 "UniClaw.Runtime.csproj 零 ProjectReference（实际 ${REFS}）" \
    "$([ "${REFS:-1}" -eq 0 ] && echo 1 || echo 0)" \
    "删除 csproj 中所有 ProjectReference; 复用旧能力先走 OpenSpec 决策（Extract Foundation / Create Adapter / Reuse Contract）"
else
  check C5 "UniClaw.Runtime.csproj 存在" 0 "恢复 src/UniClaw.Runtime/UniClaw.Runtime.csproj"
fi

# C6 — 源码不引用旧 Runtime namespace（不继承旧控制结构）
OLDNS=$(grep -rlE 'UniClaw\.Core\.(Traversal|StateMachine)' "$RUNTIME_DIR" --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')
check C6 "src/UniClaw.Runtime 零旧 namespace 引用（实际 ${OLDNS} 文件）" \
  "$([ "${OLDNS:-1}" -eq 0 ] && echo 1 || echo 0)" \
  "移除 UniClaw.Core.Traversal / UniClaw.Core.StateMachine 引用, 新 Runtime 从零生长自己的 Spine"

# C7 — 宪章分类映射完整（Part I..XIII 全部存在）
PARTS=$(grep -cE '^## Part (I|II|III|IV|V|VI|VII|VIII|IX|X|XI|XII|XIII) ' "$CHARTER" 2>/dev/null || echo 0)
check C7 "宪章 13 个职责分类齐全（实际 ${PARTS}）" \
  "$([ "${PARTS:-0}" -eq 13 ] && echo 1 || echo 0)" \
  "宪章按职责分类 Part I..XIII 缺一不可, 补回缺失分类"

# C8 — 跨助手 agent/model routing 存在并被根入口引用（Codex 与 Claude 共用角色地图）
AI_ROUTING_OK=0
if [ -f "$AI_AGENT_ROUTING" ] && [ -f "$AI_MODEL_ROUTING" ] \
  && grep -qF '.ai/agent-routing.md' "$AGENTS" \
  && grep -qF '.ai/model-routing.yaml' "$AGENTS"; then
  AI_ROUTING_OK=1
fi
check C8 "跨助手 agent/model routing 存在并被 AGENTS.md 引用" \
  "$AI_ROUTING_OK" \
  "恢复 .ai/agent-routing.md 与 .ai/model-routing.yaml，并在 AGENTS.md「跨助手入口」或 agent 路由段引用它们"

# C9 — Claude model-routing 只是适配层，不能重新成为第二份模型真源
CLAUDE_ADAPTER_OK=0
if [ -f "$CLAUDE_MODEL_ROUTING" ] \
  && grep -qF '.ai/agent-routing.md' "$CLAUDE_MODEL_ROUTING" \
  && grep -qF '.ai/model-routing.yaml' "$CLAUDE_MODEL_ROUTING"; then
  CLAUDE_ADAPTER_OK=1
fi
check C9 "Claude model-routing 引用共享路由与模型配置" \
  "$CLAUDE_ADAPTER_OK" \
  "把 .claude/model-routing.md 保持为 Claude adapter，引用 .ai/agent-routing.md 与 .ai/model-routing.yaml"

# C10 — C# MCP 启动参数必须匹配已安装服务的 workspace 语义
C_SHARP_MCP_OK=0
if [ -f "$ROOT/.mcp.json" ] \
  && grep -qF '"args": ["--workspace-from-cwd"]' "$ROOT/.mcp.json" \
  && grep -qF 'csharper-mcp --workspace-from-cwd' "$MCP_QUERY"; then
  C_SHARP_MCP_OK=1
fi
check C10 "C# semantic MCP 使用 workspace-from-cwd 正确初始化" \
  "$C_SHARP_MCP_OK" \
  "csharper-mcp 需要 workspace 目录；使用 --workspace-from-cwd 并由 cwd 固定仓库根目录"

echo ""
if [ "$FAIL" -eq 0 ]; then
  echo "check-consistency: ALL PASS — 文档与结构处于受控状态"
  exit 0
else
  echo "check-consistency: FAILURES DETECTED — 修复后重跑; 机械约束不可用文字绕过"
  exit 1
fi
