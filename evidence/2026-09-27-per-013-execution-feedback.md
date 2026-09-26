# PER-013 执行反馈与问题处置档案

> 日期：2026-09-27 · 对象：PER-013 四轮执行中遇到的问题、根因与处置结果。
> 状态栏：✅ 已解决 / 📋 记录在案移交 / 💡 建议未实施。

## 一、环境问题

| # | 问题 | 根因 | 处置 | 状态 |
|---|---|---|---|---|
| E1 | scroll-test AVD 启动即 `FATAL: snapshot operation pending`，rm snapshots/lock 无效 | `~/.android/avd` 残留运行时标记（read-snapshot.txt / hardware-qemu.ini / bootcompleted.ini / emu-launch-params.txt / snapshots/） | 删除 5 项产物后原目录直启 OK；执行期曾用 APFS clonefile 克隆到 /tmp 绕道 | ✅ |
| E2 | 权限切换不传导运行中 subagent（仍 workspace+/tmp 沙箱，~/.android EPERM） | subagent 沙箱启动时固化 | subagent 自行绕道 /tmp；教训：环境操作任务应 kill 重开或 leader 接管 | 📋 流程教训 |
| E3 | subagent 结束即回收其后台作业（emulator 随之消亡） | 作业生命周期归属 subagent 会话 | leader 会话重启托管；教训：长生命周期进程一开始就由 leader 托管 | 📋 流程教训 |
| E4 | HostLiveFull 全闭环三连败（headless×2 + 窗口×1，delivered=1 wifi 不翻转）；base commit 同败 | **非 headless**——HostOptions 默认 `ViewportHeight=2400`（Pixel 魔数）vs 本 AVD 1080×1920；归一化 center ×2400 → `input tap 967 1030`，打到 switch 下方 205px（journal 实录 vs 真实中心 824） | 测试显式声明 viewport 1080×1920 → PASS（真视觉→真 tap→翻转→Completion） | ✅ |
| E5 | 基线判别 worktree 中 live 测试起不来（venv 路径敏感，symlink 断） | `.perception` venv untracked 且位置绑定 | sed 测试指主仓库绝对路径；建议 venv 路径 env 化 | 💡 |

## 二、执法机制拦截的返工（机制全部按设计工作）

| # | 问题 | 结果 |
|---|---|---|
| P1 | 场景认证封印：Kernel/Agent 源码一动 29 封印全红 | 按设计；`--change PER-013 --all` 三轮再认证 |
| P2 | Kernel 公开面白名单：插入位置 ordinal 排序错 ×2 + 重复条目 ×1 | 已修；建议失败信息打印插入上下文或提供 codegen |
| P3 | `ComputeEvidenceId` legacy 追加 `-` 打破 pinned golden hash（WorldModelCanonicalOracle/ClaimEvolution 红） | 改条件追加后全绿——该机制**证明了** legacy EvidenceId 字节不变 |
| P4 | 编译/xunit 细节返工 ×4（CS1750 泛型默认参数、DoesNotContain 无 message 重载、漏 using Xunit ×2、macOS sed 语法） | 已修 |

## 三、设计/文档缺口（移交）

- F-A2：PER-012 mismatch 表原缺 `Bool()` 缺失→false、`ParseBounds`→默认值、`Descendants` 展平三项——grill 补录，Slice B/C 重写修正（✅）。
- F-H1 / X5 / X6：agent context cycle 引用、stale/timestamp 场景 → owner = PER-011+（📋）。
- `*.state` group-3 cutover → 前置 = typed verification consumer（📋）。
- **HostOptions viewport 魔数默认 2400**：对任意非 Pixel 形状设备是静默 footgun
  （tap 等比错位、无任何报错）；建议 follow-up：`wm size` 自动探测或未显式
  设置时 fail-closed（💡 owner = 后续 Host change）。

## 四、流程模式

gate 自审模式（goal 自动续跑下 5.3 review 由 leader 自任并留痕）运转正常；
owner 若要保留人工裁决点需显式标注。日期偏差（系统 09-26 vs 仓库 09-27）
按仓库时间线对齐。

## 验证

```yaml
level: ENVIRONMENT
method: 原目录直启 emulator + DSH_TEST_PERCEPTION_LIVE=1 双 live 测试
expected: E1 修复后可直启；E4 修复后 HostLiveFull Completion 且 wifi 翻转
actual: TypedLiveChain PASS；HostLiveFull PASS（viewport 修复后，11s）；
        Host 55/55；进程已清理（emulator/perception server 已停）
evidence: 本文件 + changes/PER-013/state.md post-closure-fix 行 +
        HostLiveFullTests viewport 声明注释
```
