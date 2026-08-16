# 开发机环境初始化清单

> 用途：换新电脑（macOS）后，按此清单复原开发环境。
> 适用芯片：**Intel 与 Apple Silicon 通用**（差异处已标注）。
> 形式：纯清单 + 参考命令，无自动化脚本；**每步先检测，已有则跳过，缺失才安装**。
> ⚠️ 本目录不含任何真实密钥，密钥统一在 [secrets.example.env](secrets.example.env) 登记，初始化时手动填。

**通用约定**：每个安装步骤前的「检测」命令输出结果即表示已安装 → 直接跳到下一步；无输出/报错才执行「安装」命令。

**路径约定**：安装位置、目录结构、PATH 注入点全部固定，见 [PATH-LAYOUT.md](PATH-LAYOUT.md)。开始前先读它。

---

## 0. 前置准备（一次性）

- [ ] 安装 **Xcode Command Line Tools**（自带 git，无需单独装 git）：`xcode-select --install`（`xcode-select -p` 有输出即已装）
- [ ] 建好目录结构（对齐 PATH-LAYOUT.md）：

```bash
mkdir -p ~/Documents/Code/spacex ~/Documents/Code/tools ~/Documents/Code/goworkspace
```

- [ ] 准备 API key（见 [secrets.example.env](secrets.example.env)，共 5~8 个）
- [ ] 决定代理：国内网络建议准备 `http://127.0.0.1:7890` 类本地代理
- [ ] 克隆本仓库（拿到清单后才能照着做）：

```bash
git clone https://github.com/lingo-cube/uni_claw.git ~/Documents/Code/spacex/uni-agent
cd ~/Documents/Code/spacex/uni-agent
```

> ⚠️ 仓库内含本清单（`init/`），之后所有步骤在本仓库目录下执行。

---

## 1. Homebrew 基础

**检测**（有输出即已装）：

```bash
command -v brew && brew --prefix    # 前缀: /usr/local 或 /opt/homebrew
```

**安装**（缺失时执行）：

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

- [ ] 更新：`brew update`（可跳过）

## 2. Homebrew 软件包清单

> 推荐用 `brew bundle`：**天然幂等，已装的自动跳过**。
> 完整清单见 [Brewfile](Brewfile)（已与当前机器逐项核对）。

**检测**（列出缺哪些，逐个 `brew install` 或整包跑）：

```bash
brew bundle check --file=init/Brewfile --verbose   # 列出缺失项
```

**安装**（缺失才执行）：

```bash
brew bundle --file=init/Brewfile
```

> 也可逐包核对：`brew list --formula | grep <包名>`；分组摘要如下：

### 核心工具（建议必装）
`git-lfs` · `gh` · `ripgrep` · `shellcheck` · `tmux` · `tree` · `wget` · `rename` · `pandoc` · `pstree` · `opencode` · `brew-cask-completion`

### 语言运行时
| 包 | 版本 | 芯片差异 |
|----|------|----------|
| `node@22` | 22.x | 无 |
| `python@3.10` / `3.11` / `3.12` | — | 无 |
| `python@3.9` | — | 已 EOL，**新机器建议跳过** |
| `go` | latest | 无 |
| `openjdk@17` / `openjdk@21` | — | 无 |
| `erlang` / `opam` | — | 无 |
| `dotnet@8` | 8.x | 无 |
| `mono-libgdiplus` | — | 无 |

### 虚拟化 / 容器 / 云
`qemu` · `minikube` · `kubernetes-cli`（docker 由 cask `docker-desktop` 提供）

### 数据库 / 中间件
`postgresql@16` · `sqlite` · `protobuf`

### 构建 / 基础依赖（多为传递依赖，随 brew 自动解决）
`autoconf` · `automake` · `m4` · `libtool` · `pkg-config` · `pkgconf` · `capstone` · `cairo` · `fontconfig` · `freetype` · `fribidi` · `gdb`(ARM 需 codesign，可选) · `gettext` · `giflib` · `glib` · `gmp` · `gnutls` · `gobject-introspection` · `graphite2` · `harfbuzz` · `icu4c@78` · `jpeg` · `jpeg-turbo` · `json-c` · `krb5` · `libev` · `libevent` · `libexif` · `libffi` · `libidn2` · `libnghttp2` · `libnghttp3` · `libngtcp2` · `libpng` · `libslirp` · `libssh` · `libtasn1` · `libtiff` · `libunistring` · `libusb` · `libuv` · `libx11` · `libxau` · `libxcb` · `libxdmcp` · `libxext` · `libxrender` · `little-cms2` · `lz4` · `lzo` · `mpdecimal` · `mpfr` · `ncurses` · `nettle` · `openssl@1.1`(EOL，新机器可删) · `openssl@3` · `p11-kit` · `pango` · `pcre` · `pcre2` · `pixman` · `readline` · `simdjson` · `simdutf` · `snappy` · `ta-lib` · `telnet` · `unbound` · `uncrustify` · `unixodbc` · `utf8proc` · `uvwasi` · `vde` · `wxmac` · `wxwidgets` · `xorgproto` · `xz` · `zstd` · `brotli` · `c-ares` · `ca-certificates` · `cabextract` · `dtc` · `gdbm`

### Python 生态
`uv`

### Cask（GUI / 平台工具）
`android-commandlinetools` · `android-platform-tools` · `docker-desktop` · `genymotion` · `minikube` · `ngrok` · `git-credential-manager`

---

## 3. .NET SDK 10 + 全局工具

**检测**（含 10.0.x 即已装）：

```bash
dotnet --list-sdks
```

**安装**（缺失时执行；官方安装器架构无关，`global.json` 要求 `10.0.100`，装 10.0.x 最新即可）：

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
```

- [ ] PATH（`~/.zshrc`）：`export PATH="$PATH:$HOME/.dotnet:$HOME/.dotnet/tools"`

**全局工具**（逐个检测，已有跳过；项目必需前两个）：

```bash
dotnet tool list -g | grep csharpermcp          # 有输出即已装
dotnet tool list -g | grep cwm.roslynnavigator
```

| 工具 | 包 | 版本 | 安装命令（缺失时） | 用途 |
|------|----|------|--------------------|------|
| ✅ 必需 | `csharpermcp` | 0.1.6 | `dotnet tool install -g csharpermcp --version 0.1.6` | C# MCP（.mcp.json 引用） |
| ✅ 必需 | `cwm.roslynnavigator` | 0.7.0 | `dotnet tool install -g cwm.roslynnavigator --version 0.7.0` | Roslyn 导航（.mcp.json 引用） |
| 可选 | `dotnet-ef` | 3.1.3 | `dotnet tool install -g dotnet-ef --version 3.1.3` | EF Core |
| 可选 | `ilspycmd` | 10.1.1.8388 | `dotnet tool install -g ilspycmd --version 10.1.1.8388` | 反编译 |
| 可选 | `dotnet-trace` | — | `dotnet tool install -g dotnet-trace` | 诊断 |
| 可选 | `dotnet-gcdump` | — | `dotnet tool install -g dotnet-gcdump` | 诊断 |
| 可选 | `dotnet-dump` | — | `dotnet tool install -g dotnet-dump` | 诊断 |

---

## 4. Node + pnpm + npm 全局包

**检测**（Node v22.x / pnpm 11.x）：

```bash
node --version && pnpm --version
```

**安装**（缺失时执行）：

```bash
brew install node@22 && brew link --force --overwrite node@22   # Node（dsh-plugin 需 ≥18，dk-harness 需 ^22.19.0）
npm install -g pnpm@11.7.0                                       # pnpm（dk-harness 固定 11.7.0）
```

**npm 全局包**（逐个检测，已有跳过）：

```bash
npm ls -g --depth=0 @anthropic-ai/claude-code @fission-ai/openspec cc-connect token-ninja @ast-grep/cli mkcert oh-my-opencode n yarn
```

**安装**（缺失的才装，可整条跑——已装的 npm 会自动跳过）：

```bash
npm install -g @anthropic-ai/claude-code @fission-ai/openspec cc-connect token-ninja @ast-grep/cli mkcert oh-my-opencode n yarn
```

---

## 5. Android SDK

**检测**（有输出即已装）：

```bash
command -v adb && adb version
ls "$HOME/Library/Android/sdk"   # 目录存在即已初始化
```

**组件清单**（`sdkmanager --list_installed` 查看已装；**ABI 按芯片选择**）：

| 组件 | Intel | Apple Silicon |
|------|-------|---------------|
| `platform-tools` | 同左 | 同左 |
| `platforms;android-35` | 同左 | 同左 |
| `system-images;android-35;default;x86_64` | ✅ | — |
| `system-images;android-35;default;arm64-v8a` | — | ✅ |
| `emulator` | 同左 | 同左 |

**安装**（缺失时执行；先装 cask `android-commandlinetools` / `android-platform-tools`）：

```bash
brew install --cask android-commandlinetools android-platform-tools
SDKM=$(find "$HOME/Library/Android/sdk/cmdline-tools" -name sdkmanager -type f | head -1)
yes | "$SDKM" --licenses
"$SDKM" "platform-tools" "platforms;android-35" "emulator"
"$SDKM" "system-images;android-35;default;arm64-v8a"   # Intel 换 x86_64
```

- [ ] PATH（`~/.zshrc`）：`export PATH="$PATH:$HOME/Library/Android/sdk/platform-tools:$HOME/Library/Android/sdk/emulator"`

---

## 6. Python 生态

**检测**：

```bash
command -v conda && conda --version    # 已有则跳过本步
command -v uv && uv --version
```

**安装**（缺失时执行）：

```bash
curl -fsSL https://repo.anaconda.com/miniconda/Miniconda3-latest-MacOSX-$(uname -m).sh -o /tmp/miniconda.sh
sudo bash /tmp/miniconda.sh -b -p /opt/miniconda3
/opt/miniconda3/bin/conda config --add channels conda-forge
brew install uv
```

---

## 7. 用户配置文件

> 模板在 [templates/](templates/)，**所有密钥均为占位符**（如 `__DEEPSEEK_API_KEY__`）。
> 做法：**目标文件已存在则跳过（先备份），缺失才从模板复制**，再把 `__XXX__` 替换为真实值。

| 目标文件 | 模板 | 检测命令（有输出即已有） | 关键内容 |
|----------|------|--------------------------|----------|
| `~/.zshrc` | [zshrc.template](templates/zshrc.template) | `ls ~/.zshrc` | PATH（dotnet/android/brew）、代理 alias、dsh 函数、conda、ssh-agent |
| `~/.gitconfig` | [gitconfig.template](templates/gitconfig.template) | `ls ~/.gitconfig` | user.name/email、代理、git-lfs |
| `~/.claude/settings.json` | [claude-settings.json.template](templates/claude-settings.json.template) | `ls ~/.claude/settings.json` | DeepSeek 兼容端点、模型映射 |
| `~/.codex/config.toml` | [codex-config.toml.template](templates/codex-config.toml.template) | `ls ~/.codex/config.toml` | deepseek/qwen/sensenova provider |
| `~/.dsh/settings.yaml` | [dsh-settings.yaml.template](templates/dsh-settings.yaml.template) | `ls ~/.dsh/settings.yaml` | DeepSeek Harness 模型/主题 |

- [ ] oh-my-zsh（`~/.zshrc` 依赖；`ls ~/.oh-my-zsh` 有输出即已装）：

```bash
[ -d "$HOME/.oh-my-zsh" ] || sh -c "$(curl -fsSL https://raw.githubusercontent.com/ohmyzsh/ohmyzsh/master/tools/install.sh)"
```

- [ ] 全部替换完成后 `source ~/.zshrc`

---

## 8. 配套仓库

> dsh 开发侧的代码构成：
> 1. **dsh 本体** = 官方 [deepseek-harness](https://github.com/deepseek-ai/deepseek-harness)，直接 clone 官方仓库（无 fork、无本地改动），当前基线 commit `47f943859b`；
> 2. **dsh 插件** `dsh-plugin-uniclaw` = **未入库的本地代码**（uni-agent 工作区 untracked），新机器需从旧机器拷贝（见下）；
> 3. dsh 的 `dsh()` 命令指向 `$HOME/Documents/Code/dk-harness`（见 zshrc 模板），**路径必须固定在此**；
> 4. dsh 用户配置 `~/.dsh/settings.yaml` 用模板 + 占位符（见第 7 节）。

**检测**（目录存在即已克隆）：

```bash
ls ~/Documents/Code/spacex/uni-agent/.git && ls ~/Documents/Code/dk-harness/.git
```

**克隆**（缺失才执行；uni-agent 已在第 0 步克隆，此处仅补 dk-harness）：

| 仓库 | 位置 | 分支 | 用途 |
|------|------|------|------|
| uni-agent（本仓库） | `~/Documents/Code/spacex/uni-agent` | `uni-agent` | 主项目（第 0 步已克隆） |
| deepseek-harness | `~/Documents/Code/dk-harness` | `master` | dsh 本体（`dsh()` 命令依赖） |

```bash
[ -d "$HOME/Documents/Code/dk-harness/.git" ] || git clone --branch master https://github.com/deepseek-ai/deepseek-harness.git "$HOME/Documents/Code/dk-harness"
cd ~/Documents/Code/dk-harness && pnpm install    # 幂等, 已有依赖自动跳过
```

- [ ] （可选）固定到当前机器使用的版本，避免 master 滚动漂移（当前 commit `47f943859b`，2026-08 基线）：

```bash
cd ~/Documents/Code/dk-harness && git checkout 47f943859bef60e4160492346772ded9b24f765a
```

- [ ] **拷贝 dsh 插件**（`dsh-plugin-uniclaw/` 未入库，需从旧机器手动拷贝，含 `node_modules` 除外）：

```bash
# 在旧机器上:
tar -C ~/Documents/Code/spacex/uni-agent -czf /tmp/dsh-plugin-uniclaw.tgz --exclude=node_modules dsh-plugin-uniclaw
# 拷贝到新机器后:
tar -C ~/Documents/Code/spacex/uni-agent -xzf /tmp/dsh-plugin-uniclaw.tgz
cd ~/Documents/Code/spacex/uni-agent/dsh-plugin-uniclaw && pnpm install
```

---

## 9. 密钥清单

见 [secrets.example.env](secrets.example.env)（`DEEPSEEK_API_KEY`、`ANTHROPIC_AUTH_TOKEN`、`MIMO_API_KEY`、`QWEN_API_KEY`、`SENSENOVA_TOKEN`、`GIT_NAME`、`GIT_EMAIL`、`GIT_PROXY`）。

- [ ] 已在 `~/.zshrc` / `~/.claude/settings.json` / `~/.codex/config.toml` 全部替换占位符（`grep -rn '__[A-Z_]*__' ~/.zshrc ~/.claude ~/.codex ~/.dsh` 应无输出）

---

## 10. 最终核对

```bash
dotnet --list-sdks          # 含 10.0.x
csharper-mcp --version      # 全局工具可用
node --version              # v22.x
pnpm --version              # 11.x
adb version                 # Android platform-tools
java -version               # openjdk 17/21
adb devices                 # 真机/模拟器识别
git -C ~/Documents/Code/dk-harness rev-parse --short HEAD   # dsh 版本, 期望与固定 commit 一致
```

- [ ] 项目构建：`cd uni-agent && dotnet build src/UniClaw.Runtime.sln`
- [ ] 插件测试：`cd uni-agent/dsh-plugin-uniclaw && pnpm install && pnpm test`
- [ ] dsh Web UI：`dsh web` → http://127.0.0.1:3080

---

## 维护说明

- 新增 Homebrew 包 → 更新 [Brewfile](Brewfile) 与第 2 节摘要
- 新增 dotnet 工具 → 更新第 3 节表格
- 新增 npm 全局包 → 更新第 4 节
- 新增配置模板 → 放入 [templates/](templates/) 并登记第 7 节表格
