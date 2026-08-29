#!/usr/bin/env bash
# 安装 git pre-commit hook — 快速门（diff 检查 + 一致性门）。
# 慢门（AgentWorkflow / dotnet）保留在 scripts/verify-before-commit.sh，
# 提交前必须显式运行；hook 只拦"可 1 秒内判定的低级失败"。
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HOOK_DIR="$ROOT/.git/hooks"
HOOK="$HOOK_DIR/pre-commit"

mkdir -p "$HOOK_DIR"

if [ -e "$HOOK" ] && ! grep -q "verify-before-commit" "$HOOK"; then
  echo "WARNING: 已有自定义 pre-commit（$HOOK），未覆盖；请手工合并。" >&2
  exit 1
fi

cat > "$HOOK" <<'EOF'
#!/usr/bin/env bash
# UniClaw 快速提交门（由 scripts/install-git-hooks.sh 安装）。
# 慢门见 scripts/verify-before-commit.sh — 提交前显式运行。
set -uo pipefail
ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT" || exit 1

if ! git diff --cached --check; then
  echo "pre-commit: 暂存区存在空白/冲突标记错误 — 拒绝提交" >&2
  exit 1
fi

if ! bash scripts/check-consistency.sh >/tmp/uni-consistency.log 2>&1; then
  echo "pre-commit: check-consistency FAILED（tail）" >&2
  tail -30 /tmp/uni-consistency.log >&2
  exit 1
fi
EOF
chmod +x "$HOOK"
echo "installed: $HOOK"
echo "（提示：AgentWorkflow 等慢门仍须提交前运行 bash scripts/verify-before-commit.sh）"