# AGT-010 真机终考执行指令（移交下一 Agent）

> 产出者：AGT-009/010 Leader（GLM-5.3）。目标：完成 AGT-010 验收第 6 项
> （真机/emulator 复跑）并收尾。**只验证，不改产品代码。**

## 0. 环境事实（已探测，勿再推断"无设备"）

- 本机有 adb（/opt/homebrew/share/android-commandlinetools）与两个 API 35 AVD：
  `p26_pixel`、`scroll-test`（`~/.android/avd/`）。
- Worktree：`/Users/fran/Documents/Code/spacex/uni_claw-agt009`，分支
  `agt009-integration`。**禁止触碰共享工作区 `/Users/fran/Documents/Code/spacex/uni_claw`。**
- 上一会话曾启动过一台模拟器（已关闭）；`adb devices` 应为空或只有你自己的。

## 1. 预检

```bash
cd /Users/fran/Documents/Code/spacex/uni_claw-agt009
adb devices                      # 空则启动：
/opt/homebrew/share/android-commandlinetools/emulator/emulator -avd p26_pixel \
  -no-window -no-audio -no-boot-anim -gpu swiftshader_indirect &
# 等待 boot：
adb wait-for-device shell 'while [ "$(getprop sys.boot_completed)" != "1" ]; do sleep 2; done; echo BOOTED'
adb shell wm size                # 记下分辨率（测试会自动读取）
dotnet build UniClaw.Kernel.slnx # 期望 0 errors
```

注意：`tests/UniClaw.Host.Tests/SettingsCoverageLiveTests.cs` 是新写入的
ENV 门控测试，**编译尚未验证**（上会话验证被中止）。若编译报错：只允许修
该测试文件自身的编译错误（命名空间/笔误），**不得改产品源码**。

## 2. 真机终考（一次命令）

```bash
cd /Users/fran/Documents/Code/spacex/uni_claw-agt009
DSH_TEST_PERCEPTION_LIVE=1 dotnet test tests/UniClaw.Host.Tests \
  --filter "FullyQualifiedName~SettingsCoverageLiveTests" \
  --logger "console;verbosity=detailed"
```

内容：neg-c 同源配置（饥饿滚动候选冲击列表底部，maxScrolls=8）+ AGT-010
的 rk1 RootRoute；真实 emulator 全链（截图+hierarchy feed → director →
kernel → adb effect → post-action 验证）；咨询侧 = 本地指令跟随 double
（被验收的是 director/kernel/feed/effect 真实链，模型智能不在本面）。

**PASS 判据（测试内已断言）**：
1. `Security & privacy` 步骤存在且 `Verified=true`，`RouteAfter ==
   android.settings|rk1:Settings|src=title|up=1`（撞名页可验证进入——
   e1 基线在 step 20 确定性失败，digest 55291AC…）；
2. 无任何 `Security & privacy` 步骤因 target-unique 失败；
3. ≥1 次 verified swipe（列表底部区域可达）；
4. 终局诚实：`CoverageComplete` 或 `BoundedStop`（后者必须带未覆盖项清单）。

产物自动落盘：`evidence/agt-010/live-rerun/run-*/`（coverage-steps.json /
coverage-report.json / facts.json / evidence/{captureId}.png+.xml）。

## 3. 收尾（PASS 后）

1. 新建 `evidence/agt-010/live-rerun-result.md`：四元组
   （method=上述命令；expected=PASS 判据；actual=实际结果含步骤数/终局/
   撞名步骤明细；evidence=run 目录相对路径），并附与 e1 基线
   （step 20 失败、digest 55291AC…）的对照结论。
2. 更新 `changes/AGT-010/state.md`：Verification.actual 补真机复跑结果；
   status log 加一行 `verified·live-rerun`。
3. 回归：`dotnet test UniClaw.Kernel.slnx`（期望全绿，含门控跳过的 live
   测试）；`python3 tools/scenario_certify.py --check`（期望 29/29 PASS）；
   `git diff --check`。
4. `git add -A && git commit -m "docs(agt-010): live rerun evidence and close"`

## 4. 失败处置（诚实优先）

| 现象 | 处置 |
|---|---|
| `Security & privacy` 步骤未出现 | 检查滚动深度与 `wm size`；API35 列表通常 1-2 次滚动到底；调大 maxScrolls（改测试内 config 副本）可重跑 |
| 步骤出现但 `Verified=false` | **真缺陷信号**：读 run 目录 checks 明细 + 对应 captureId 的 XML 截图定位；记录 FirstDivergence 并停下回报 owner——不得现场改产品代码让测试变绿 |
| 模拟器/adb 异常 | 如实报 ENVIRONMENT 细节（本机确认有 adb + 两个 API35 AVD） |
| 弹窗 obstacle 未触发 | 不是失败：弹窗依赖设备状态（安全告警卡→Dismiss→确认框链）；如触发，测试会打印 obstacle consults 数并留痕 |

红线：不伪造 PASS、不跳断言、不改产品源码、不动共享工作区、不动场景 seal。

## 5. 允许修改面

`evidence/agt-010/**`、`changes/AGT-010/state.md`、
`tests/UniClaw.Host.Tests/SettingsCoverageLiveTests.cs`（仅编译修复或
config 副本参数）。其余一律不动（Kernel/Host 产品源、scenarios/、
schemas/、model-routing.yaml、其他 change 状态）。
