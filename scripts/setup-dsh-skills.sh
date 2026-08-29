#!/usr/bin/env bash
#
# setup-dsh-skills.sh — 同步 UniClaw 通用 Skill 发现 adapter。
#
# Canonical Skill body 只存在于 .ai/skills。脚本幂等维护：
#   .agents/skills/<name> -> ../../.ai/skills/<name>
#   .dsh/skills/<name>    -> ../../.ai/skills/<name>
#
# `.agents` 是通用发现层；`.dsh` 仅兼容 DSH 固定扫描根。两者都不拥有正文。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SOURCE_ROOT="$PROJECT_ROOT/.ai/skills"
ADAPTER_ROOTS=(
  "$PROJECT_ROOT/.agents/skills"
  "$PROJECT_ROOT/.dsh/skills"
)

if [ ! -d "$SOURCE_ROOT" ]; then
  echo "错误: canonical Skill 根不存在: $SOURCE_ROOT" >&2
  exit 1
fi

for adapter_root in "${ADAPTER_ROOTS[@]}"; do
  mkdir -p "$adapter_root"

  # 只移除 adapter 根中的符号链接；普通文件/目录视为冲突并保留、最终失败。
  for entry in "$adapter_root"/*; do
    [ -e "$entry" ] || [ -L "$entry" ] || continue
    name="$(basename "$entry")"
    source_bundle="$SOURCE_ROOT/$name"
    expected_target="../../.ai/skills/$name"

    if [ ! -L "$entry" ]; then
      echo "错误: adapter 中存在非符号链接: $entry" >&2
      exit 1
    fi

    if [ ! -f "$source_bundle/SKILL.md" ] || [ "$(readlink "$entry")" != "$expected_target" ]; then
      rm -f "$entry"
    fi
  done

  for source_bundle in "$SOURCE_ROOT"/*; do
    [ -d "$source_bundle" ] || continue
    [ -f "$source_bundle/SKILL.md" ] || continue
    name="$(basename "$source_bundle")"
    link_path="$adapter_root/$name"
    target="../../.ai/skills/$name"

    if [ -L "$link_path" ] && [ "$(readlink "$link_path")" = "$target" ]; then
      continue
    fi
    if [ -e "$link_path" ] || [ -L "$link_path" ]; then
      echo "错误: 无法安全覆盖 adapter: $link_path" >&2
      exit 1
    fi
    ln -s "$target" "$link_path"
  done
done

for adapter_root in "${ADAPTER_ROOTS[@]}"; do
  echo "已同步: ${adapter_root#"$PROJECT_ROOT/"}"
  for entry in "$adapter_root"/*; do
    [ -L "$entry" ] || continue
    [ -f "$entry/SKILL.md" ] || {
      echo "错误: 悬空 Skill adapter: $entry" >&2
      exit 1
    }
  done
done

echo "Skill adapter 同步完成；canonical root: .ai/skills"
