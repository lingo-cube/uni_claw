#!/usr/bin/env bash
#
# setup-dsh-skills.sh — 让 DSH 发现 UniClaw 项目里定义的 skills。
#
# 背景：
#   DSH 的 skill-filesystem 只扫描固定根目录（project .dsh/skills、project
#   .agents/skills、customSkillDirs、user ~/.dsh/skills、~/.agents/skills），
#   —— 不含 .claude/skills 或 .ai/skills。本项目 skill 都定义在那两处。
#
# 本脚本在 <projectRoot>/.dsh/skills/ 下建立 相对路径 symlink，指向各 skill
#   bundle（<name>/SKILL.md），使 DSH 默认根（rank 100）能发现它们，同时
#   不复制文件、不改动 .claude/.ai 原结构。
#
# 相对路径 symlink 提交进 git 后，换机 clone 项目即可直接生效（目录结构
#  一致则链接有效），无需重配 dsh。本脚本是保险丝：万一某环境不还原
#   symlink（core.symlinks=false / Windows 权限），clone 后跑一次即可重建。
#
# 幂等：可重复执行，不会重复创建已存在的链接。

set -euo pipefail

# 脚本所在目录向上定位项目根（含 .git）
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

DOT_DSH_SKILLS="$PROJECT_ROOT/.dsh/skills"

# 需要接入的 skill 源（相对项目根，指向 <name>/SKILL.md 的 bundle 目录）
SOURCE_ROOTS=(
  ".ai/skills"
  ".claude/skills"
)

echo "项目根: $PROJECT_ROOT"
mkdir -p "$DOT_DSH_SKILLS"

count=0
for root in "${SOURCE_ROOTS[@]}"; do
  src_dir="$PROJECT_ROOT/$root"
  [ -d "$src_dir" ] || { echo "警告: 源目录不存在 $src_dir，跳过"; continue; }
  for skill in "$src_dir"/*; do
    [ -d "$skill" ] || continue
    name="$(basename "$skill")"
    # 跳过悬空/指向项目外的源（如旧机器迁移残留的 symlink）：
    # 只有源 bundle 内真实存在 SKILL.md 才接入。
    if [ ! -f "$skill/SKILL.md" ]; then
      printf "  跳过 %-36s (源 bundle 内无 SKILL.md，可能悬空)\n" "$name"
      continue
    fi
    # 相对路径：从 .dsh/skills/ 指向源 bundle（上两级到项目根再进入源目录）
    target="../../$root/$name"
    link_path="$DOT_DSH_SKILLS/$name"
    if [ -L "$link_path" ]; then
      current="$(readlink "$link_path")"
      if [ "$current" = "$target" ]; then
        # 已存在且指向正确 —— 幂等跳过
        printf "  = %-40s -> %s\n" "$name" "$current"
        continue
      fi
      echo "  更新 $name"
      rm -f "$link_path"
    elif [ -e "$link_path" ]; then
      echo "警告: $link_path 已存在但不是 symlink，跳过（DBH 结构冲突）"
      continue
    fi
    ln -s "$target" "$link_path"
    printf "  + %-40s -> %s\n" "$name" "$target"
    count=$((count + 1))
  done
done

echo ""
echo "完成。DSH 从项目根可发现的 skills:"
ls -l "$DOT_DSH_SKILLS" | grep '^l' | awk '{print "  " $9 " -> " $11}'
echo ""
echo "校验: 每个 .dsh/skills/<name>/SKILL.md 可达："
ok=1
for f in "$DOT_DSH_SKILLS"/*/SKILL.md; do
  [ -f "$f" ] || { echo "  ✗ 缺 $f"; ok=0; }
done
[ "$ok" = "1" ] && echo "  ✓ 全部 SKILL.md 可达"

# 退出码由 set -e 与上一步保证；无致命错误则 0
