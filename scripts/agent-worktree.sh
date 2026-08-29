#!/usr/bin/env bash
# agent-worktree — 并行 Agent 独立工作区（git worktree 隔离，业界标准做法）。
#
# 同一本地 clone 被多个 Host（DSH / Codex / 用户）并行使用时，共用一个工作树
# 必然互相污染（本次会话已发生：主工作树出现非本次提交的并发改动）。
# git worktree 是官方解法：共享 repo + 对象库，每个 agent 有独立目录与分支。
#
# 用法:
#   bash scripts/agent-worktree.sh <host-or-branch> [parent-dir]
#     例: bash scripts/agent-worktree.sh dsh
#       → 目录 $(dirname $PWD)/uni_claw-dsh，分支 agent/dsh
# 之后: cd 到新目录正常开发；提交落在 agent/<name>；合入走 rebase
# （详见 .ai/agent-branch-workflow.md）。
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NAME="${1:?usage: agent-worktree.sh <host-or-branch> [parent-dir]}"
PARENT="${2:-$(dirname "$ROOT")}"
BRANCH="agent/$NAME"
DIR="$PARENT/uni_claw-$NAME"

if git -C "$ROOT" worktree list --porcelain | grep -q "^worktree $DIR"; then
  echo "已存在: $DIR（分支 $BRANCH）"
  exit 0
fi

if git -C "$ROOT" show-ref --verify --quiet "refs/heads/$BRANCH"; then
  echo "[reuse] 分支 $BRANCH 已存在，附到新工作树"
  git -C "$ROOT" worktree add "$DIR" "$BRANCH"
else
  BASE="$(git -C "$ROOT" branch --show-current || echo main)"
  echo "[new] 分支 $BRANCH <- $BASE"
  git -C "$ROOT" worktree add -b "$BRANCH" "$DIR" "$BASE"
fi

echo
echo "worktree ready: $DIR  (branch $BRANCH)"
echo "后续:"
echo "  cd $DIR          # 独立开发目录"
echo "  ... 开发/提交到 $BRANCH ..."
echo "  bash scripts/verify-before-commit.sh   # 合入前必跑"
echo "  git -C $ROOT merge --rebase agent/$NAME # 或到主工作树 rebase 合入"
echo "协议: .ai/agent-branch-workflow.md"