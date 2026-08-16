# 目录布局约定（PATH-LAYOUT）

> 新机器安装时**所有路径按本约定固定**，避免散落各处。
> 原则：工具链用官方默认位置，代码统一进 `~/Documents/Code/`，配置统一进 `~/.Xxx`，PATH 注入统一在 `~/.zshrc`（模板已含）。
> 芯片差异：Intel 前缀 `/usr/local`，Apple Silicon 前缀 `/opt/homebrew`（下表中以 `$BREW_PREFIX` 表示）。

---

## 1. 代码仓库 — `~/Documents/Code/`

所有 git 仓库**统一放这里**，按归属分子目录：

| 路径 | 内容 |
|------|------|
| `~/Documents/Code/spacex/uni-agent` | 本仓库（uni-agent 分支） |
| `~/Documents/Code/dk-harness` | DeepSeek Harness（dsh 命令依赖） |
| `~/Documents/Code/goworkspace` | Go 工作区（GOPATH） |
| `~/Documents/Code/tools/` | 自建小工具 |
| `~/Documents/Code/mcp/` `lingo/` `lingo_cube/` `cc-connect/` | 其他项目 |

**约定**：新仓库一律 `git clone <url> ~/Documents/Code/<归属>/<repo>`，不要散放到桌面/下载目录。

## 2. 工具链安装位置

| 工具 | 安装路径 | 说明 |
|------|----------|------|
| Homebrew | `$BREW_PREFIX`（Intel `/usr/local` / ARM `/opt/homebrew`） | 官方脚本自动 |
| .NET SDK | `~/.dotnet`（SDK + 全局工具 tools/） | 官方 dotnet-install.sh 的默认目录 |
| Node 22 | `$BREW_PREFIX/opt/node@22`（brew，keg-only） | `brew link --force` |
| npm 全局包 | `$BREW_PREFIX/lib/node_modules` | 跟随 brew node 前缀 |
| Android SDK | `~/Library/Android/sdk` | 官方 SDK 默认位置 |
| conda | `/opt/miniconda3`（或 `~/miniconda3`） | 官方安装器 |
| uv | `$BREW_PREFIX/bin/uv` | brew |
| Go | `$BREW_PREFIX/bin/go` + `~/Documents/Code/goworkspace` | brew + GOPATH 约定 |
| openjdk@17/@21 | `$BREW_PREFIX/opt/openjdk@17` 等 | brew（keg-only，需手动 PATH/JAVA_HOME） |
| oh-my-zsh | `~/.oh-my-zsh` | 官方安装器 |
| git-lfs / gh / rg / tmux 等 | `$BREW_PREFIX/bin/` | brew |

### Homebrew 目录布局规则（自动，无需干预）

> Homebrew 强制按固定规则存放，**安装时不要用 `--prefix` 等参数改路径**，否则破坏规则。以下为 Intel `/usr/local` 实测结构（ARM `/opt/homebrew` 同理）：

| 目录 | 存放内容 | 示例 |
|------|----------|------|
| `$BREW_PREFIX/Cellar/<formula>/<version>/` | 每个 formula 的**真实文件**（按公式名 + 版本分目录） | `Cellar/node@22/22.22.2/` |
| `$BREW_PREFIX/opt/<formula>/` | **当前活动版本软链**（多版本共存时指最新；keg-only 公式只在此） | `opt/node@22 → Cellar/node@22/22.22.2` |
| `$BREW_PREFIX/bin` `/sbin` | 可执行文件软链（非 keg-only 公式自动链接） | `bin/node`、`bin/gh`、`bin/ripgrep` |
| `$BREW_PREFIX/lib` `/include` `/share` | 库、头文件、文档/man | `lib/libsqlite3.dylib`、`share/man` |
| `$BREW_PREFIX/etc` | 公式的配置文件（一般不用手改） | `etc/openssl@3/` |
| `$BREW_PREFIX/var` | **可变数据**：数据库数据、服务日志 | `var/postgresql@16/`（PG 数据目录）、`var/log` |
| `$BREW_PREFIX/Caskroom/<cask>/<version>/` | cask 的**真实文件**（GUI 应用/平台工具） | `Caskroom/docker-desktop/` |
| `/Applications` | cask GUI 应用的**安装位置**（软链自 Caskroom） | `Docker.app`、`Genymotion.app` |
| `~/Library/Caches/Homebrew` | 下载的 bottle/缓存（可删，会重下） | `downloads/` |
| `~/Library/Logs/Homebrew` | 安装/更新日志（排错用） | `brew/` |

**要点**：
- **多版本共存**：`Cellar` 里可同时有多个版本，`opt/` 软链决定哪个是"当前"；升级 = 新版本进 Cellar + 软链切换，旧版本残留可用 `brew cleanup` 清理。
- **keg-only 公式**（node@22、openjdk@17/21、python@3.x、dotnet@8 等）**不自动链接**到 `bin/`，只在 `opt/<name>/bin`，需手动加入 PATH（zshrc 模板已处理）。
- **查找任意公式的实际路径**：`brew --prefix <formula>`（如 `brew --prefix node@22`）；**版本**：`brew list --versions`。

**实例**（当前机器 Intel，`/usr/local`）：

```bash
$ brew --prefix node@22
/usr/local/opt/node@22            # opt 软链 → 实际文件在 Cellar
$ ls -l /usr/local/opt/node@22
node@22 -> ../Cellar/node@22/22.22.2
$ ls /usr/local/Cellar/node@22/
22.22.2                           # 真实文件按版本存放
$ which node && node --version    # bin/node 是软链, 但 node@22 keg-only 需手动 PATH
/usr/local/bin/node               # (若已 brew link --force)
$ ls /Applications | grep -i docker
Docker.app                        # cask GUI 在这
$ ls /usr/local/Caskroom
docker-desktop  genymotion  minikube  ngrok  ...
```
- **服务类**（postgresql 等）：`brew services start` 后数据在 `var/`，LaunchAgent 在 `~/Library/LaunchAgents`（用户级，无需 sudo）。
- **卸载即删**：`brew uninstall <formula>` 删 Cellar + opt 软链；cask 卸载删 Caskroom + /Applications 软链；`var/` 数据需手动确认。


## 3. 用户配置 — `~/.Xxx`

| 路径 | 内容 | 模板 |
|------|------|------|
| `~/.zshrc` | PATH / 别名 / 密钥 / dsh 函数 | [templates/zshrc.template](templates/zshrc.template) |
| `~/.gitconfig` | git 用户 / 代理 / lfs | [templates/gitconfig.template](templates/gitconfig.template) |
| `~/.claude/settings.json` | Claude Code 端点与模型 | [templates/claude-settings.json.template](templates/claude-settings.json.template) |
| `~/.codex/config.toml` | Codex provider | [templates/codex-config.toml.template](templates/codex-config.toml.template) |
| `~/.dsh/settings.yaml` | DeepSeek Harness | [templates/dsh-settings.yaml.template](templates/dsh-settings.yaml.template) |
| `~/.ssh/` | ssh key / agent.env | 手动（见 README 第 9 节） |
| `~/.android/` | adb 配置（自动生成） | — |

## 4. 环境变量与 PATH 注入点（全部在 `~/.zshrc`）

> 模板 zshrc.template 已包含以下全部，安装后 `source ~/.zshrc` 即生效。
> **不要**在 `/etc/paths`、`~/.bash_profile` 等多处重复注入，避免 PATH 混乱。

```bash
# Homebrew（按芯片自动）
[ "$(uname -m)" = "arm64" ] && eval "$(/opt/homebrew/bin/brew shellenv)" || eval "$(/usr/local/bin/brew shellenv)"

# .NET
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools"

# Android
export ANDROID_SDK_ROOT="$HOME/Library/Android/sdk"
export ANDROID_HOME="$ANDROID_SDK_ROOT"
export PATH="$PATH:$ANDROID_SDK_ROOT/platform-tools:$ANDROID_SDK_ROOT/emulator"

# Go
export GOPATH="$HOME/Documents/Code/goworkspace"
export PATH="$PATH:$(go env GOPATH)/bin"
export GOPROXY=https://goproxy.cn

# Java（按需）
# export JAVA_HOME="$(/usr/libexec/java_home -v 17)"
```

## 5. 路径核对命令（安装完成后验证）

```bash
echo "$BREW_PREFIX"      # brew --prefix
dotnet --info | head -5  # Base Path 应含 ~/.dotnet
echo "$ANDROID_HOME"     # ~/Library/Android/sdk
npm prefix -g            # $BREW_PREFIX
echo "$GOPATH"           # ~/Documents/Code/goworkspace
echo $PATH | tr ':' '\n' # 应能看到 ~/.dotnet/tools、platform-tools 等
```

---

## 约定速查（一句话版）

1. **代码** → `~/Documents/Code/`
2. **工具链** → 官方默认目录（dotnet 在 `~/.dotnet`，Android 在 `~/Library/Android/sdk`，brew 按芯片前缀）
3. **配置** → `~/.Xxx`，全部有模板
4. **PATH** → 只改 `~/.zshrc` 一处
5. **密钥** → 只在 `~/.zshrc` / `~/.claude` / `~/.codex`，绝不进仓库
