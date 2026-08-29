#!/usr/bin/env bash
# 提交前验证门 — 任何检查 FAIL 都以非零退出并禁止提交。
# 用法:
#   bash scripts/verify-before-commit.sh          # 默认门（diff/一致性/AgentWorkflow）
#   bash scripts/verify-before-commit.sh --dotnet # 附加 .NET build（Runtime 改动时使用）
#
# 与 AGENTS.md §8 Verification 对齐：提交必须是"门通过"之后唯一入口，
# 不得以"改测试/放宽 fail-closed"绕过本门。
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

fail=0
step() { printf '\n=== %s ===\n' "$*"; }

step "git diff --check"
git diff --check || fail=1

step "check-consistency (C1..C15)"
if bash scripts/check-consistency.sh; then :; else fail=1; fi

step "AgentWorkflow (python -m pytest, isolated uv cache)"
if UV_CACHE_DIR="$ROOT/.uv-cache" uv run --with pytest \
    python -m pytest tests/AgentWorkflow -q --no-header \
    -p no:cacheprovider; then :; else fail=1; fi

step "profile pin sync check（规则内容指纹，非失败门）"
PIN_REPORT="$(python3 scripts/sync-profile-pin.py --check 2>/dev/null || true)"
PIN="$(printf '%s\n' "$PIN_REPORT" | sed -n 's/^PIN=//p')"
YAML_PIN="$(printf '%s\n' "$PIN_REPORT" | sed -n 's/^YAML_PIN=//p')"
FILES_CHANGED="$(printf '%s\n' "$PIN_REPORT" | sed -n 's/^FILES_CHANGED=//p')"
if [ "$FILES_CHANGED" = "yes" ]; then
  if [ -n "$YAML_PIN" ] && [ "$YAML_PIN" = "$PIN" ]; then
    echo "pin in sync (pin 文件集已变更，pin 已对齐: $PIN)"
  else
    echo "WARNING: pin 文件集已变更且 pin 未同步 (yaml=$YAML_PIN != fp=$PIN)"
    echo "         运行 python3 scripts/sync-profile-pin.py 后重跑本门（非阻断）"
  fi
else
  if [ -n "$YAML_PIN" ] && [ "$YAML_PIN" = "$PIN" ]; then
    echo "pin in sync (fp=$PIN)"
  else
    echo "WARNING: pin 与规则内容指纹不一致 (yaml=$YAML_PIN != fp=$PIN，无未提交规则变更)"
    echo "         运行 python3 scripts/sync-profile-pin.py 对齐（非阻断）"
  fi
fi

if [ "${1:-}" = "--dotnet" ]; then
  step "dotnet build src/UniClaw.Runtime.sln"
  dotnet build src/UniClaw.Runtime.sln --nologo --verbosity quiet || fail=1
fi

if [ "$fail" -ne 0 ]; then
  echo
  echo "verify-before-commit: FAILURES DETECTED — 禁止提交；修复后重跑"
  exit 1
fi
echo
echo "verify-before-commit: ALL PASS — 可以提交"
exit 0