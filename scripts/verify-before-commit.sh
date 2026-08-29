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

step "profile-source pin drift check（提示，非失败门）"
PIN="$(python3 -c "
import re
try:
    text = open('.dsh/profile-adapter/profile-source.yaml', encoding='utf-8').read()
except OSError:
    text = ''
m = re.search(r'source_revision:\s*([0-9a-f]{40})', text)
print(m.group(1) if m else '')")"
HEAD="$(git rev-parse HEAD)"
if [ -n "$PIN" ] && [ "$PIN" != "$HEAD" ]; then
  echo "NOTE: profile-source.yaml pin=$PIN != HEAD=$HEAD"
  echo "      若本次改动触及 profile 语义（.ai/profiles、.ai/schemas、validator、profile-source.yaml），"
  echo "      提交前应按 README 规则同步 pin；否则保留为 fail-closed 信号（不阻断提交）。"
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