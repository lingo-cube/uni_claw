#!/usr/bin/env bash
#
# quick-init.sh — 一键环境初始化（只装必需项, 已有跳过, 可重复执行）
#
# 用法:  bash init/quick-init.sh
# 适用:  macOS（Intel / Apple Silicon 自动适配）
# 内容:  Xcode CLT 检查 → Homebrew → 21 个必需 brew 包 → .NET SDK 10 →
#        dotnet 必需工具 → pnpm + npm 必需全局包 → dk-harness + 插件依赖
# 不做:  ⭕ 可选项（Android SDK / conda / docker / postgres 等）— 见 README
# 注意:  密钥/配置模板不在此脚本, 完成后按 README 第 7/9/10 节手动收尾
#
set -euo pipefail

# ---- 定位 ----
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

log()  { printf '\033[1;34m[init]\033[0m %s\n' "$*"; }
ok()   { printf '\033[1;32m[ ok ]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[warn]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[fail]\033[0m %s\n' "$*" >&2; exit 1; }

# ---- 0. 平台检查 ----
[ "$(uname -s)" = "Darwin" ] || die "仅支持 macOS（本机: $(uname -s)）"

if ! xcode-select -p >/dev/null 2>&1; then
  warn "缺少 Xcode Command Line Tools"
  echo "  请先运行:  xcode-select --install"
  echo "  完成图形安装后重新执行本脚本。"
  exit 1
fi
ok "Xcode CLT 已就绪 ($(xcode-select -p))"

# ---- 1. Homebrew ----
if ! command -v brew >/dev/null 2>&1; then
  log "安装 Homebrew ..."
  NONINTERACTIVE=1 /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
fi
# 按芯片加载 shellenv（Intel /usr/local · ARM /opt/homebrew）
if [ "$(uname -m)" = "arm64" ]; then
  eval "$(/opt/homebrew/bin/brew shellenv)"
else
  eval "$(/usr/local/bin/brew shellenv)"
fi
ok "Homebrew: $(brew --prefix)"

# ---- 2. 必需 brew 包（核心工具 + 语言运行时 + uv）----
BREW_PACKAGES=(
  git-lfs gh ripgrep shellcheck tmux tree wget rename pandoc pstree opencode brew-cask-completion
  node@22 python@3.10 python@3.11 python@3.12 go openjdk@17 openjdk@21 uv
)
log "安装必需 brew 包 ..."
brew install "${BREW_PACKAGES[@]}"
# node@22 keg-only, 链接到 PATH 供 pnpm/npm 使用
brew link --force --overwrite node@22 2>/dev/null || warn "node@22 已链接, 跳过"
ok "brew 必需包完成（$(brew list --formula | wc -l | tr -d ' ') 个 formula）"

# ---- 3. .NET SDK 10 ----
if ! command -v dotnet >/dev/null 2>&1; then
  log "安装 .NET SDK 10 → \$HOME/.dotnet ..."
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
fi
export PATH="$PATH:$HOME/.dotnet:$HOME/.dotnet/tools"
ok "dotnet: $(dotnet --list-sdks | tail -1)"

# ---- 4. dotnet 必需全局工具 ----
dotnet tool list -g 2>/dev/null | grep -q csharpermcp \
  || dotnet tool install -g csharpermcp --version 0.1.6
dotnet tool list -g 2>/dev/null | grep -q cwm.roslynnavigator \
  || dotnet tool install -g cwm.roslynnavigator --version 0.7.0
ok "dotnet 全局工具: csharper-mcp / cwm-roslyn-navigator"

# ---- 5. pnpm + 必需 npm 全局包 ----
command -v pnpm >/dev/null 2>&1 || npm install -g pnpm@11.7.0
npm install -g @anthropic-ai/claude-code @fission-ai/openspec cc-connect @ast-grep/cli mkcert
ok "pnpm $(pnpm --version 2>/dev/null || echo ?) + npm 全局包完成"

# ---- 6. 配套仓库依赖（dk-harness + 插件, 幂等）----
if [ ! -d "$HOME/Documents/Code/dk-harness/.git" ]; then
  log "克隆 deepseek-harness → \$HOME/Documents/Code/dk-harness ..."
  git clone --branch master https://github.com/deepseek-ai/deepseek-harness.git "$HOME/Documents/Code/dk-harness"
fi
(cd "$HOME/Documents/Code/dk-harness" && pnpm install)
ok "dk-harness 依赖完成"

if [ -d "$REPO_ROOT/dsh-plugin-uniclaw" ]; then
  (cd "$REPO_ROOT/dsh-plugin-uniclaw" && pnpm install)
  ok "dsh-plugin-uniclaw 依赖完成"
fi

# ---- 收尾提示 ----
echo
ok "✅ 一键初始化完成"
echo "  接下来手动收尾（详见 init/README.md）:"
echo "    1. 配置模板  — 第 7 节: 复制 templates/ 到 ~ 对应位置, 替换 __XXX__ 占位符"
echo "    2. 密钥填入  — 第 9 节: 见 init/secrets.example.env"
echo "    3. 最终核对  — 第 10 节: dotnet/node/pnpm/adb 等"
echo "    4. 可选项    — Android SDK(第 5 节) / conda(第 6 节) / brew bundle --file=init/Brewfile"
