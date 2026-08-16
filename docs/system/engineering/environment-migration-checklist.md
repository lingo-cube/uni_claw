# UniClaw 环境迁移与故障排查参考清单

> 用途：迁移到新机器后，按此清单逐步恢复环境；遇到环境问题时，按对应章节定位和修复。
> 最后更新：2026-08-15

---

## 0. 当前状态速览

| 项目 | 状态 |
|------|------|
| Java 工具链 | ✅ 已修复（必须用 Homebrew openjdk@17，不能用 JDK 15） |
| sdkmanager | ✅ 可用 |
| avdmanager | ✅ 可用 |
| Perception 服务 | ✅ 可启动（需 Python 3.11+） |
| Scroll 机制 | ✅ 已毕业（确定性验证） |
| Perception Toggle | ✅ 已毕业（含真实 Wi-Fi buyer） |
| Scroll+Toggle 实机集成 | ⛔ BLOCKED_BY_REALITY_ENVIRONMENT（缺 API34 系统镜像） |

---

## 1. 版本需求

| 组件 | 最低版本 | 推荐版本 | 备注 |
|------|----------|----------|------|
| .NET SDK | 10.0 | 10.0 | 编译 UniClaw.Runtime |
| Python | 3.11 | 3.11+ | Perception 服务 |
| Java | **17** | Homebrew openjdk@17 | **不能使用 JDK 15**，会 SIGSEGV |
| Android SDK | API 35 | API 35 + API 34 | API34 用于 Scroll+Toggle 实机证明 |
| Android Emulator | 最新 | 最新 | 需支持 x86_64 |
| adb | 最新 | 最新 | platform-tools |
| Homebrew | - | 最新 | macOS 包管理 |

---

## 2. 已知环境问题与修复

### 2.1 sdkmanager/avdmanager SIGSEGV

**症状**：
```
JRE version: (15.0.2+7) (build )
SIGSEGV (0xb) at pc=0x00007ff8135dce92
```

**原因**：系统默认 JDK 15 与 Android SDK 工具不兼容。

**修复**：
```bash
brew install openjdk@17

export JAVA_HOME=$(brew --prefix openjdk@17)/libexec/openjdk.jdk/Contents/Home
export PATH="$JAVA_HOME/bin:$PATH"

# 验证
java -version          # 应显示 17.x
sdkmanager --version   # 应输出版本号且 exit 0
avdmanager list avd    # 应 exit 0
```

**固化**：将上述 export 写入 `~/.zshrc` 或 `~/.bash_profile`：
```bash
echo 'export JAVA_HOME=$(brew --prefix openjdk@17)/libexec/openjdk.jdk/Contents/Home' >> ~/.zshrc
echo 'export PATH="$JAVA_HOME/bin:$PATH"' >> ~/.zshrc
```

---

### 2.2 AVD 被 com.apple.provenance 锁住

**症状**：
```
rm: snapshot.lock.lock: Operation not permitted
xattr: [Errno 1] Operation not permitted
```

**原因**：AVD 文件带有 macOS `com.apple.provenance` 扩展属性，禁止修改/删除。

**处置**：该 AVD（uniclaw-lite-api35）使用与当前系统镜像相同的 API35，**即使解锁也无法提供新的 Scroll 场景**。迁移时直接忽略它，重建新 AVD 即可。

**不要浪费时间解锁**。

---

### 2.3 模拟器无法启动 / 提示 snapshot pending

**症状**：
```
FATAL | A snapshot operation for 'xxx' is pending and timeout has expired.
```

**原因**：上次模拟器异常退出，残留 snapshot.lock。

**处置**：删除对应 AVD 的 lock 文件：
```bash
rm -f ~/.android/avd/<avd-name>.avd/snapshot.lock.lock
rm -f ~/.android/avd/<avd-name>.avd/hardware-qemu.ini.lock
rm -f ~/.android/avd/<avd-name>.avd/multiinstance.lock
```

如果 `Operation not permitted`，则重建新 AVD（见 4.3）。

---

### 2.4 sdkmanager 无法下载（无网络）

**症状**：
```
Warning: Failed to download any source lists!
Warning: IO exception while downloading manifest
```

**原因**：当前沙箱无网络。

**处置**：
- 如果只是临时无网络：等待网络恢复后用 sdkmanager 安装 API34 镜像
- 如果永久无网络：从另一台机器拷贝完整 system-image 包

**所需镜像**：
```
system-images;android-34;default;x86_64
或
system-images;android-34;google_apis;x86_64
```

---

### 2.5 Python 依赖缺失

**症状**：`python3 -m uvicorn ...` 报 ModuleNotFoundError。

**修复**：
```bash
cd platforms/perception
pip install -r requirements.txt
# 或按需安装：
pip install ultralytics paddleocr fastapi uvicorn pillow numpy opencv-python
```

**注意**：`platforms/perception/uniclaw_perception/server.py` 需要 `ultralytics`、`PIL`、`numpy`、`fastapi`、`uvicorn`。

---

### 2.6 Vision 服务启动慢 / Matplotlib 警告

**症状**：
```
/Users/fran/.matplotlib is not a writable directory
Matplotlib created a temporary cache directory at /var/folders/...
```

**原因**：Matplotlib 缓存目录不可写。

**修复**：
```bash
mkdir -p /tmp/mplconfig
export MPLCONFIGDIR=/tmp/mplconfig
```

---

## 3. 代码恢复（Git）

### 3.1 Clone

```bash
git clone <仓库地址> uni-agent
cd uni-agent
```

### 3.2 确认关键文件存在

```bash
# Scroll 机制
test -f src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs && echo OK

# Perception 修复
test -f platforms/perception/uniclaw_perception/fusion/heuristics.py && echo OK

# 决策记录
ls docs/decisions/physical-scroll-container-semantic-traversal-graduation-decision.md
ls docs/decisions/perception-actionable-toggle-evidence-reality-repair-graduation-decision.md

# OpenSpec (archived after graduation; records are source of truth)
ls openspec/changes/archive/2026-08-16-perception-actionable-toggle-evidence-reality-repair/
ls openspec/changes/archive/2026-08-16-physical-scroll-container-semantic-traversal/
```

### 3.3 关键代码位置速查

| 功能 | 文件 | 搜索关键词 |
|------|------|-----------|
| F5 修复 | `Agent.SemanticRun.cs` | `ReconcilePostScrollContinuityFailure` |
| DEFERRED_BOUNDED | `Agent.SemanticRun.cs` | `PerformSemanticCheckpoint`, `_postScrollContinuityUnverified` |
| 滚动状态 | `Agent.cs` | `_deferredScrollCount`, `MaxDeferredScrolls` |
| Toggle 推断 | `heuristics.py` | `apply_toggle_inference_heuristic` |
| 右区关联修复 | `heuristics.py` | `c_x1 >= 0.55`, `right-side control zone` |
| 引擎传图 | `engine.py` | `image: Any | None` |
| 服务器传图 | `server.py` | `image=proc_img` |

---

## 4. 环境搭建步骤（新机器）

### 4.1 安装基础工具

```bash
# macOS
brew install openjdk@17
brew install python@3.11

# .NET SDK 10
# 从 https://dotnet.microsoft.com 安装
```

### 4.2 安装 Android SDK

```bash
# 安装 cmdline-tools
mkdir -p $HOME/Library/Android/sdk/cmdline-tools
cd $HOME/Library/Android/sdk/cmdline-tools

# 下载 commandlinetools-mac 并解压为 latest/
# 然后：
export JAVA_HOME=$(brew --prefix openjdk@17)/libexec/openjdk.jdk/Contents/Home
export PATH="$JAVA_HOME/bin:$PATH"
export ANDROID_SDK_ROOT=$HOME/Library/Android/sdk
export ANDROID_HOME=$HOME/Library/Android/sdk

sdkmanager "platform-tools" "platforms;android-35" "emulator" "system-images;android-35;default;x86_64"
```

### 4.3 创建 AVD

```bash
export ANDROID_AVD_HOME=$HOME/.android/avd
mkdir -p $ANDROID_AVD_HOME

echo "no" | avdmanager create avd \
  -n scroll-test \
  -k "system-images;android-35;default;x86_64" \
  -d pixel_2
```

### 4.4 安装 Python 依赖

```bash
cd /path/to/uni-agent/platforms/perception
pip install -r requirements.txt 2>/dev/null || pip install ultralytics paddleocr fastapi uvicorn pillow numpy opencv-python
```

### 4.5 启动 Vision 服务

```bash
cd /path/to/uni-agent
rm -f /tmp/uniclaw-vision.sock
PYTHONPATH=platforms/perception:$PYTHONPATH python3 -m uvicorn \
  uniclaw_perception.server:app \
  --uds /tmp/uniclaw-vision.sock &
sleep 20
ls -la /tmp/uniclaw-vision.sock
```

---

## 5. 验证清单

### 5.1 快速环境验证

```bash
# Java
java -version                          # 17.x
sdkmanager --version                   # exit 0
avdmanager list avd                    # exit 0

# Python
python3 -c "import ultralytics, PIL, numpy; print('OK')"

# Vision Socket
ls -la /tmp/uniclaw-vision.sock
```

### 5.2 项目验证

```bash
cd /path/to/uni-agent

# 构建
dotnet build src/UniClaw.Runtime.sln

# Python Perception 测试
cd platforms/perception && python3 -m pytest tests/ -q
cd ../..

# C# 回归
dotnet test src/UniClaw.Runtime.sln

# 一致性
./scripts/check-consistency.sh

# OpenSpec (already graduated; use graduation records / archive as source of truth)
# docs/decisions/perception-actionable-toggle-evidence-reality-repair-graduation-decision.md
# docs/decisions/physical-scroll-container-semantic-traversal-graduation-decision.md
# openspec/changes/archive/2026-08-16-perception-actionable-toggle-evidence-reality-repair/
# openspec/changes/archive/2026-08-16-physical-scroll-container-semantic-traversal/
```

---

## 6. 启动模拟器

```bash
export ANDROID_SDK_ROOT=$HOME/Library/Android/sdk
export ANDROID_AVD_HOME=$HOME/.android/avd

$ANDROID_SDK_ROOT/emulator/emulator \
  -avd scroll-test \
  -no-window -no-audio -no-boot-anim \
  -gpu swiftshader_indirect \
  -no-snapshot -no-snapshot-save \
  -no-metrics \
  -port 5554 \
  -skin 1080x1920 \
  -wipe-data &

# 等待启动
sleep 60
adb devices -l
adb -s emulator-5554 shell getprop sys.boot_completed  # 应输出 1
```

---

## 7. 当前阻塞与解锁方法

### 7.1 当前状态

> 治理更正（2026-08-16）：`physical-scroll-toggle-reality-integration` 这个 change
> 名称在任何 commit 中从未存在过（PHANTOM）。真实滚动 toggle 流是
> `physical-scroll-container-semantic-traversal`（已确定性毕业，
> `PHYSICAL_SCROLL_SEMANTIC_MECHANISM_DETERMINISTICALLY_VERIFIED`；live 滚动证明当时被
> Perception 可行动性缺口阻塞，该缺口已由 `perception-actionable-toggle-evidence` /
> `perception-actionable-toggle-evidence-reality-repair` 承接）。

```
physical-scroll-container-semantic-traversal
→ GRADUATED (deterministic)
→ live emulator scroll-toggle proof: BLOCKED_BY_REALITY_ENVIRONMENT
   （live-only；确定性机制已毕业）
```

阻塞原因（live-only 证明层面）：
1. 当前 API35 AOSP 镜像没有 naturally below-fold 的 toggle
2. 本地没有 API34 镜像
3. 网络不可用，sdkmanager 无法下载

### 7.2 解锁方法

**方法 A：网络恢复**
```bash
export JAVA_HOME=$(brew --prefix openjdk@17)/libexec/openjdk.jdk/Contents/Home
sdkmanager "system-images;android-34;default;x86_64"
```

**方法 B：离线拷贝**
从另一台机器拷贝完整目录：
```
system-images/android-34/default/x86_64/
```
放到：
```
$ANDROID_SDK_ROOT/system-images/android-34/default/x86_64/
```
必须包含：`system.img`, `source.properties`, `package.xml`, `ramdisk.img`, `kernel-ranchu` 等完整文件。

**方法 C：物理手机**
允许（但非必需），连接一台有 Settings toggle 的 Android 手机。

---

## 8. 常见故障速查表

| 症状 | 原因 | 修复 |
|------|------|------|
| sdkmanager SIGSEGV | 用了 JDK 15 | 切到 Homebrew openjdk@17 |
| avdmanager 找不到镜像 | 没设 ANDROID_SDK_ROOT | export ANDROID_SDK_ROOT=$HOME/Library/Android/sdk |
| 模拟器无法启动 snapshot pending | 上次异常退出 | 删 lock 文件或重建 AVD |
| Vision socket 不存在 | 服务没起来 / 还在启动 | 等 20s，看日志 `/tmp/vision*.log` |
| Perception 返回空 candidates | YOLO 模型没加载 | 检查模型路径 `platforms/perception/models/yolo/.../best.pt` |
| `dotnet test` 有 4 个 VisionHost 失败 | 需要 Vision 服务在线 | 先启动 Vision 服务再跑 |
| `candidate_4` icon 没变成 switch | 旧代码 | 确认 `heuristics.py` 有 `right-side control zone` |
| Wi-Fi 页面 toggle 检测不到 | 旧距离阈值 0.5 | 确认已改为结构关联（x>=0.55） |
| Python 测试失败 PER-T5 | 测试基于旧阈值 | 使用仓库内已更新的测试文件 |

---

## 9. 迁移后 15 分钟检查单

```text
□ 1. git clone 成功
□ 2. dotnet --version 显示 10.x
□ 3. java -version 显示 17.x
□ 4. sdkmanager --version 正常
□ 5. avdmanager list avd 正常
□ 6. Python 3.11+ 可用
□ 7. pip install 完成
□ 8. Vision 服务启动成功，socket 存在
□ 9. dotnet build 成功
□ 10. Python pytest 全绿
□ 11. dotnet test 全绿（或仅 4 个 VisionHost 环境失败）
□ 12. ./scripts/check-consistency.sh 全绿
□ 13. openspec validate 全绿
□ 14. 模拟器能启动，adb 能看到 emulator-5554
□ 15. 已知阻塞项（Scroll+Toggle 实机）状态清楚
```

---

## 10. 仓库内关键决策/文档索引

| 文档 | 内容 |
|------|------|
| `docs/decisions/physical-scroll-container-semantic-traversal-graduation-decision.md` | Scroll 机制毕业 |
| `docs/decisions/perception-actionable-toggle-evidence-reality-falsification.md` | 首次误判记录 |
| `docs/decisions/perception-actionable-toggle-evidence-reality-falsification-correction.md` | 修正：Developer Options 无 toggle |
| `docs/decisions/perception-actionable-toggle-evidence-reality-repair-graduation-decision.md` | Perception 修复毕业 |
| `openspec/changes/archive/2026-08-16-physical-scroll-container-semantic-traversal/` | 归档 Scroll OpenSpec |
| `openspec/changes/perception-actionable-toggle-evidence/` | Perception OpenSpec（active） |
| `openspec/changes/archive/2026-08-16-perception-actionable-toggle-evidence-reality-repair/` | 归档 Perception 修复 OpenSpec |
| `docs/decisions/semantic-run-popup-obstruction-graduation-decision.md` | Popup interruption 毕业 |
| `docs/decisions/semantic-run-unexpected-navigation-reconciliation-graduation-decision.md` | Unexpected known-page reconciliation 毕业 |
