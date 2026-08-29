#!/usr/bin/env bash
#
# quick-init.sh — 一键环境初始化（只装必需项, 已有跳过, 可重复执行）
#
# 用法:
#   bash init/quick-init.sh          一键初始化（安装缺失的必需项）
#   bash init/quick-init.sh --check  只检测不安装, 输出成功/缺失核对表
#   bash init/quick-init.sh -c       同上
#
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
CODE_DIR="$HOME/Documents/Code"
DK_HARNESS_DIR="$CODE_DIR/dk-harness"

log()  { printf '\033[1;34m[init]\033[0m %s\n' "$*"; }
ok()   { printf '\033[1;32m[ ok ]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[warn]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[fail]\033[0m %s\n' "$*" >&2; exit 1; }

# 超时执行命令（macOS 无 timeout/gtimeout, 用后台+轮询实现）:
#   run_with_timeout <秒数> <命令...>; 超时返回 124, 正常返回命令退出码
run_with_timeout() {
  local secs="$1"; shift
  "$@" &
  local pid=$!
  local waited=0
  while kill -0 "$pid" 2>/dev/null; do
    sleep 1
    waited=$((waited + 1))
    if [ "$waited" -ge "$secs" ]; then
      kill "$pid" 2>/dev/null
      wait "$pid" 2>/dev/null
      return 124
    fi
  done
  wait "$pid"
  return $?
}

# ---- 模式: --check / -c 只检测 ----
CHECK_MODE=0
case "${1:-}" in
  --check|-c) CHECK_MODE=1 ;;
  "") ;;
  *) die "未知参数: $1（仅支持 --check / -c）" ;;
esac

check() { # check "标签" 命令 参数...
  local label="$1"; shift
  if "$@" >/dev/null 2>&1; then
    printf '\033[1;32m[  ✓ ]\033[0m %s\n' "$label"
  else
    printf '\033[1;31m[  ✗ ]\033[0m %s\n' "$label"
  fi
}

if [ "$CHECK_MODE" -eq 1 ]; then
  echo "== 环境核对（只检测, 不安装）=="
  check "Xcode CLT                " xcode-select -p
  check "Homebrew                 " bash -c 'command -v brew'
  for p in git-lfs gh ripgrep shellcheck tmux tree wget rename pandoc pstree opencode brew-cask-completion \
           node@22 python@3.10 python@3.11 python@3.12 go openjdk@17 openjdk@21 uv; do
    check "brew: $p"               bash -c "brew list --formula 2>/dev/null | grep -qx '$p'"
  done
  check ".NET SDK 含 10.0.x       " bash -c "dotnet --list-sdks 2>/dev/null | grep -q '10\.0\.'"
  check "dotnet: csharpermcp      " bash -c "dotnet tool list -g 2>/dev/null | grep -q csharpermcp"
  check "dotnet: cwm.roslynnavigator" bash -c "dotnet tool list -g 2>/dev/null | grep -q cwm.roslynnavigator"
  check "pnpm                     " bash -c 'command -v pnpm'
  for p in @fission-ai/openspec cc-connect @ast-grep/cli mkcert; do
    check "npm 全局: $p"           bash -c "npm ls -g --depth=0 '$p' >/dev/null 2>&1"
  done
  check "dk-harness 已克隆        " bash -c "[ -d '$DK_HARNESS_DIR/.git' ]"
  check "dk-harness 依赖已装      " bash -c "[ -d '$DK_HARNESS_DIR/node_modules' ]"
  check "dsh() 函数在 ~/.zshrc    " bash -c "grep -q 'dsh()' '$HOME/.zshrc' 2>/dev/null || grep -q 'dk-harness' '$HOME/.zshrc' 2>/dev/null"
  check "插件 dsh-plugin-uniclaw  " bash -c "[ -d '$REPO_ROOT/dsh-plugin-uniclaw/.git' ] || [ -d '$REPO_ROOT/dsh-plugin-uniclaw/src' ]"
  echo
  echo "✗ 的项 = 缺失/未装, 运行 bash init/quick-init.sh 补齐。"
  exit 0
fi

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

# ---- 3. .NET SDK 10（带网络预检 + 超时, 防挂死）----
if ! command -v dotnet >/dev/null 2>&1; then
  log "安装 .NET SDK 10 → \$HOME/.dotnet ..."
  if ! curl -fsSL --connect-timeout 8 --max-time 60 https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh; then
    die "下载 dotnet-install.sh 失败: 检查网络（dot.net 需可直连; 国内网络请先启动代理, 见 README 第 0 节）"
  fi
  if ! run_with_timeout 600 bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"; then
    die ".NET SDK 安装失败或超时(10分钟): 检查网络后重跑（脚本幂等, 已装步骤自动跳过）"
  fi
fi
export PATH="$PATH:$HOME/.dotnet:$HOME/.dotnet/tools"
ok "dotnet: $(dotnet --list-sdks | tail -1)"

# ---- 4. dotnet 必需全局工具（带网络预检 + 超时, 防挂死）----
install_dotnet_tool() { # install_dotnet_tool <包ID> <版本> <显示名>
  local pkg="$1" ver="$2" name="$3"
  if dotnet tool list -g 2>/dev/null | grep -q "$pkg"; then
    ok "dotnet 工具 $name 已有, 跳过"
    return 0
  fi
  log "安装 dotnet 工具 $name ($pkg@$ver) ..."
  # 预检 nuget.org 可达性, 避免 dotnet 网络挂起无限等待
  if ! curl -fsS --connect-timeout 8 --max-time 15 -o /dev/null https://api.nuget.org/v3/index.json; then
    die "无法连接 nuget.org: 检查网络（国内网络请先启动代理, 见 README 第 0 节）"
  fi
  if ! run_with_timeout 300 dotnet tool install -g "$pkg" --version "$ver"; then
    die "dotnet 工具 $name 安装失败或超时(5分钟): 检查网络（nuget.org 需可直连; 国内网络请先启动代理, 见 README 第 0 节）"
  fi
  ok "dotnet 工具 $name 完成"
}
install_dotnet_tool csharpermcp 0.1.6 "csharper-mcp"
install_dotnet_tool cwm.roslynnavigator 0.7.0 "cwm-roslyn-navigator"
ok "dotnet 全局工具完成"

# ---- 5. pnpm + 必需 npm 全局包（带网络预检 + 超时, 防挂死）----
npm_install_missing() { # npm_install_missing <包名...>: 只装缺失的
  local missing=()
  local p
  for p in "$@"; do
    npm ls -g --depth=0 "$p" >/dev/null 2>&1 || missing+=("$p")
  done
  [ "${#missing[@]}" -eq 0 ] && return 0
  log "npm 全局安装缺失: ${missing[*]} ..."
  # 预检 npm registry 可达性, 避免 npm 网络挂起无限等待
  if ! curl -fsS --connect-timeout 8 --max-time 15 -o /dev/null https://registry.npmjs.org/; then
    die "无法连接 registry.npmjs.org: 检查网络（国内网络请先启动代理, 见 README 第 0 节）"
  fi
  if ! run_with_timeout 600 npm install -g "${missing[@]}"; then
    die "npm 全局安装失败或超时(10分钟): 检查网络后重跑（脚本幂等, 已装步骤自动跳过）"
  fi
  ok "npm 全局: ${missing[*]} 完成"
}
command -v pnpm >/dev/null 2>&1 || npm_install_missing pnpm@11.7.0
npm_install_missing @fission-ai/openspec cc-connect @ast-grep/cli mkcert
ok "pnpm $(pnpm --version 2>/dev/null || echo ?) + npm 全局包完成"

# ---- 6. 配套仓库依赖（dk-harness + 插件, 带网络预检 + 超时, 防挂死）----
mkdir -p "$CODE_DIR"
if [ ! -d "$DK_HARNESS_DIR/.git" ]; then
  log "克隆 deepseek-harness → $DK_HARNESS_DIR ..."
  if ! curl -fsS --connect-timeout 8 --max-time 15 -o /dev/null https://github.com; then
    die "无法连接 github.com: 检查网络（国内网络请先启动代理, 见 README 第 0 节）"
  fi
  if ! run_with_timeout 600 git clone --branch master https://github.com/deepseek-ai/deepseek-harness.git "$DK_HARNESS_DIR"; then
    rm -rf "$DK_HARNESS_DIR"   # 清掉半成品, 便于重跑
    die "克隆失败或超时(10分钟): 检查网络（GitHub 需可直连; 国内网络请先启动代理, 见 README 第 0 节）"
  fi
fi
if [ ! -d "$DK_HARNESS_DIR/node_modules" ]; then
  log "pnpm install @ $DK_HARNESS_DIR ..."
  if ! run_with_timeout 900 pnpm -C "$DK_HARNESS_DIR" install; then
    die "dk-harness 依赖安装失败或超时(15分钟): 检查网络后重跑（脚本幂等, 已装步骤自动跳过）"
  fi
fi
if [ ! -f "$DK_HARNESS_DIR/apps/web/dist/index.html" ]; then
  log "pnpm run build @ $DK_HARNESS_DIR（生成各包 lib/ + 前端 dist, 首次约 5-15 分钟）..."
  if ! run_with_timeout 1800 pnpm -C "$DK_HARNESS_DIR" run build; then
    die "dk-harness 构建失败或超时(30分钟): 重跑即可（脚本幂等, 已构建跳过）"
  fi
fi
ok "dk-harness 已克隆、装依赖并构建（$(git -C "$DK_HARNESS_DIR" rev-parse --short HEAD)）"

if [ -d "$REPO_ROOT/dsh-plugin-uniclaw" ] && [ ! -d "$REPO_ROOT/dsh-plugin-uniclaw/node_modules" ]; then
  log "pnpm install @ dsh-plugin-uniclaw ..."
  if ! run_with_timeout 300 pnpm -C "$REPO_ROOT/dsh-plugin-uniclaw" install; then
    die "插件依赖安装失败或超时(5分钟): 检查网络后重跑（脚本幂等, 已装步骤自动跳过）"
  fi
fi
ok "dsh-plugin-uniclaw 依赖完成"

# ---- 7. dsh() 命令函数 → ~/.zshrc（幂等追加, 已有跳过）----
ZSHRC="$HOME/.zshrc"
if ! grep -q 'dsh()' "$ZSHRC" 2>/dev/null; then
  log "追加 dsh() 函数到 $ZSHRC ..."
  touch "$ZSHRC"
  cat >> "$ZSHRC" <<'EOF'

# ---- deepseek-harness (dsh) ----
# 源码 @ $HOME/Documents/Code/dk-harness · 用户配置 @ ~/.dsh/settings.yaml
# 启动: dsh web（Web UI @ http://127.0.0.1:3080）· dsh headless "任务"
dsh() {
  export DSH_TELEMETRY_DISABLED=1
  pnpm -C "$HOME/Documents/Code/dk-harness" dsh "$@"
}
EOF
  ok "dsh() 已追加到 ~/.zshrc（新开终端生效; 若之后用 zshrc.template 整体替换会自动升级为 \$CODE 版本）"
else
  ok "dsh() 已在 ~/.zshrc, 跳过"
fi

# ---- 收尾提示 ----
echo
ok "✅ 一键初始化完成"
echo "  验证: bash init/quick-init.sh --check"
echo "  接下来手动收尾（详见 init/README.md）:"
echo "    1. 配置模板  — README 第 7 节: 复制 templates/ 到 ~ 对应位置, 替换 __XXX__ 占位符"
echo "       (dsh() 函数脚本已自动追加; PATH/密钥等仍需手动模板)"
echo "    2. 密钥填入  — README 第 9 节: 见 init/secrets.example.env"
echo "    3. 最终核对  — README 第 10 节: dotnet/node/pnpm/adb 等"
echo "    4. 可选项    — Android SDK(第 5 节) / conda(第 6 节) / brew bundle --file=init/Brewfile"
