#!/usr/bin/env bash
#
# check-consistency.sh — Greenfield 文档/结构机械检查器
#
# 依据: OpenAI "Harness Engineering" (2026-02) — "Docs rot; lint rules don't."
#       机械约束比文档更可靠; 每条检查失败时输出"违反什么 + 为什么 + 修复指引"。
# 配合: tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs (编译期 Guard)
# 用法: scripts/check-consistency.sh
# 期望: 全部 C1..C15 PASS, 任意 FAIL 则 exit 1
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
MCP_QUERY="$ROOT/.ai/tooling/csharp-mcp-query.md"
RUNTIME_DIR="$ROOT/src/UniClaw.Runtime"
GUARD="$ROOT/tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs"
CURRENT_GATES="$ROOT/docs/work/active/current-gates.md"
LATEST_SNAPSHOT="$ROOT/docs/snapshots/latest.md"
SKILL_SOURCE_ROOT="$ROOT/.ai/skills"
SKILL_ADAPTER_ROOTS=("$ROOT/.agents/skills" "$ROOT/.dsh/skills")

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

# C8 — 通用 agent/model routing 存在并被根入口引用
AI_ROUTING_OK=0
if [ -f "$AI_AGENT_ROUTING" ] && [ -f "$AI_MODEL_ROUTING" ] \
  && grep -qF '.ai/agent-routing.md' "$AGENTS" \
  && grep -qF '.ai/model-routing.yaml' "$AGENTS"; then
  AI_ROUTING_OK=1
fi
check C8 "跨助手 agent/model routing 存在并被 AGENTS.md 引用" \
  "$AI_ROUTING_OK" \
  "恢复 .ai/agent-routing.md 与 .ai/model-routing.yaml，并在 AGENTS.md「跨助手入口」或 agent 路由段引用它们"

# C9 — Claude 项目目录已退役；根兼容入口只能指向 AGENTS.md
CLAUDE_RETIRED_OK=0
if [ ! -e "$ROOT/.claude" ] \
  && [ -f "$ROOT/CLAUDE.md" ] \
  && grep -qF 'AGENTS.md' "$ROOT/CLAUDE.md" \
  && ! grep -qE '\.claude/|\.ai/skills/|\.ai/model-routing' "$ROOT/CLAUDE.md"; then
  CLAUDE_RETIRED_OK=1
fi
check C9 "Claude 项目配置已退役，根 CLAUDE.md 仅为无状态兼容入口" \
  "$CLAUDE_RETIRED_OK" \
  "删除 .claude/；CLAUDE.md 只保留读取 AGENTS.md 的兼容说明，不维护协议、Skill、路由或权限"

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

# C11 — Active Change projection 必须与 OpenSpec proposal 目录精确一致
ACTIVE_SOURCE_COUNT=0
for proposal in "$ROOT"/openspec/changes/*/proposal.md; do
  [ -f "$proposal" ] || continue
  ACTIVE_SOURCE_COUNT=$((ACTIVE_SOURCE_COUNT + 1))
done
ACTIVE_PROJECTED_COUNT=$(sed -nE 's/^ActiveChangeCount: `([0-9]+)`$/\1/p' "$CURRENT_GATES" | head -1)
ACTIVE_MEMBERSHIP_OK=0
if cmp -s \
  <(for proposal in "$ROOT"/openspec/changes/*/proposal.md; do [ -f "$proposal" ] && basename "$(dirname "$proposal")"; done | sort) \
  <(sed -n '/^## Generated Active Change Membership/,/^## Gate Annotations/p' "$CURRENT_GATES" | sed -nE 's/^\| `([^`]+)` \|.*/\1/p' | sort); then
  ACTIVE_MEMBERSHIP_OK=1
fi
check C11 "current-gates Active Change 与 OpenSpec source 一致（source=${ACTIVE_SOURCE_COUNT}, projection=${ACTIVE_PROJECTED_COUNT:-missing}）" \
  "$([ "${ACTIVE_PROJECTED_COUNT:-missing}" = "$ACTIVE_SOURCE_COUNT" ] && [ "$ACTIVE_MEMBERSHIP_OK" -eq 1 ] && echo 1 || echo 0)" \
  "从 openspec/changes/*/proposal.md 重新生成 current-gates active membership；不得用 buyer/status 过滤目录"

# C12 — latest snapshot lifecycle counts 必须与 current-gates projection 一致
GATES_ACTIVE=$(sed -nE 's/^ActiveChangeCount: `([0-9]+)`$/\1/p' "$CURRENT_GATES" | head -1)
GATES_ARCHIVED=$(sed -nE 's/^ArchivedChangeCount: `([0-9]+)`$/\1/p' "$CURRENT_GATES" | head -1)
SNAPSHOT_ACTIVE=$(sed -nE 's/^ActiveChangeCount: `([0-9]+)`$/\1/p' "$LATEST_SNAPSHOT" | head -1)
SNAPSHOT_ARCHIVED=$(sed -nE 's/^ArchivedChangeCount: `([0-9]+)`$/\1/p' "$LATEST_SNAPSHOT" | head -1)
check C12 "latest snapshot lifecycle counts 与 current-gates 一致" \
  "$([ -n "$GATES_ACTIVE" ] && [ "$GATES_ACTIVE" = "$SNAPSHOT_ACTIVE" ] && [ -n "$GATES_ARCHIVED" ] && [ "$GATES_ARCHIVED" = "$SNAPSHOT_ARCHIVED" ] && echo 1 || echo 0)" \
  "先从 OpenSpec source 修复 current-gates，再同步 latest snapshot 的 ActiveChangeCount/ArchivedChangeCount"

# C13 — 通用与 DSH Skill adapter 必须精确映射 .ai/skills
SKILL_ADAPTERS_OK=1
for adapter_root in "${SKILL_ADAPTER_ROOTS[@]}"; do
  [ -d "$adapter_root" ] || { SKILL_ADAPTERS_OK=0; continue; }
  if ! cmp -s \
    <(for source_bundle in "$SKILL_SOURCE_ROOT"/*; do [ -f "$source_bundle/SKILL.md" ] && basename "$source_bundle"; done | sort) \
    <(for adapter in "$adapter_root"/*; do { [ -e "$adapter" ] || [ -L "$adapter" ]; } && basename "$adapter"; done | sort); then
    SKILL_ADAPTERS_OK=0
  fi
  for adapter in "$adapter_root"/*; do
    [ -e "$adapter" ] || [ -L "$adapter" ] || continue
    name="$(basename "$adapter")"
    if [ ! -L "$adapter" ] \
      || [ "$(readlink "$adapter")" != "../../.ai/skills/$name" ] \
      || [ ! -f "$adapter/SKILL.md" ]; then
      SKILL_ADAPTERS_OK=0
    fi
  done
done
check C13 "通用与 DSH Skill adapter 精确使用受控相对链接" \
  "$SKILL_ADAPTERS_OK" \
  "运行 scripts/setup-dsh-skills.sh；.agents/skills 与 .dsh/skills 只能精确链接到 ../../.ai/skills/<name>"

# C14 — 当前执行来源不得依赖 .claude 路径；历史 Decision/Archive 不参与本检查
CURRENT_CLAUDE_REFS=$(grep -nH -E '\.claude/' \
  "$AGENTS" \
  "$ROOT/.ai/agent-routing.md" \
  "$ROOT/.ai/development-protocol.md" \
  "$ROOT/.ai/model-routing.yaml" \
  "$ROOT/.ai/openspec-workflow.md" \
  "$ROOT/.ai/task-contract.md" \
  "$ROOT/.ai/result-contract.md" \
  "$ROOT/.ai/workflows/uniflow-coding-workflow.md" \
  "$ROOT/.ai/skills/README.md" \
  "$ROOT/openspec/AGENTS.md" \
  "$ROOT/.dsh/profile-adapter/README.md" \
  "$ROOT/tools/agent_profile_validator.py" \
  "$ROOT/tools/csharp-mcp-README.md" \
  "$ROOT/init/README.md" \
  "$ROOT/init/PATH-LAYOUT.md" \
  "$ROOT/init/gen-secrets.sh" \
  "$ROOT/init/quick-init.sh" 2>/dev/null || true)
check C14 "当前协议、adapter、工具与初始化入口不依赖 .claude 路径" \
  "$([ -z "$CURRENT_CLAUDE_REFS" ] && echo 1 || echo 0)" \
  "把当前依赖迁入 .ai / .agents / Host adapter；历史 Decision 与 Archive 保留原文，不加入当前入口"

# C15 — active OpenSpec 不得反向绑定 WorkItem；archive 仅保留历史执行证据
ACTIVE_OPENSPEC_WI_REFS=$(find "$ROOT/openspec/changes" \
  -path "$ROOT/openspec/changes/archive" -prune -o \
  -type f -name '*.md' -print0 2>/dev/null \
  | xargs -0 grep -nHE '\bWI-[A-Z0-9-]+\b|docs/work/active/workitems' 2>/dev/null || true)
check C15 "active OpenSpec 不反向关联 WorkItem" \
  "$([ -z "$ACTIVE_OPENSPEC_WI_REFS" ] && echo 1 || echo 0)" \
  "移除 active openspec/changes 中的 WI-* 编号与 WorkItem 路径；WorkItem 只能从自身 anchors/read_hints 单向引用 OpenSpec，archive 历史证据不参与本检查"

echo ""
if [ "$FAIL" -eq 0 ]; then
  echo "check-consistency: ALL PASS — 文档与结构处于受控状态"
  exit 0
else
  echo "check-consistency: FAILURES DETECTED — 修复后重跑; 机械约束不可用文字绕过"
  exit 1
fi
