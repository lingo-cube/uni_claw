# Real negative-path verification — Settings coverage bounds（2026-10-01, AGT-006）

## 这次做了什么

AGT-005 证明了覆盖遍历的 happy path。本 change 在同一真实栈（专用 DSH
3081 + emulator-5556 + zai-coding-cn/glm-5.3-flash，session
「遍历设置菜单覆盖测试」，workspace 复用，autoCloseTurn=false）用**纯配置**
驱动负路径场景，验证 fail-closed / 有界停止语义在真实运行中如实成立。
共用 3080 全程未动（前后存活探测 401）。

## 结果总览（5 次真实运行）

| 运行 | 配置意图 | 结果 | 退出码 |
|---|---|---|---|
| a2（run-a-maxsteps） | maxSteps=6 截断 | 6 步后诚实停止：BoundedStop 50%、未覆盖项 3 条如实列出、分歧 NONE | **3** |
| b（run-b-maxsteps-rich） | 全目标+maxSteps=24 | 24 步耗尽停止：BoundedStop 83%（scroll-discovered 1/5 未达） | **3** |
| c1（run-c1-unknownpage-prefail） | 饥饿候选冲击滚动到底 | 启动过渡屏（路由回退身份）→ 指挥幻影 back → **真实 post-action-target-unique 验证失败** + 首个分歧点捕获 + 模型不遵从 → 有界纠正 → 持续偏离 **fail closed** 终局 | **3** |
| c2（run-c2-reentry-loop） | 同上（修复前） | 候选重进循环（30 enters/1 swipe）耗尽咨询预算：AgentDecisionFailed(consult-budget-exhausted) 诚实分类 | **1** |
| c3（run-c3-unknownpage-postfix） | 同上（修复后） | 未知页 → **Defer×3 重观察** → bounded-stop:unknown-page、**零幻影 effect**（0 步派发） | **3** |

## 发现并修复的三个真实缺陷（均有回归测试）

1. **配置环境变量被静默忽略**（`SettingsCoverageConfig.LoadDefault`）：
   `UNICLAW_SETTINGS_COVERAGE_CONFIG` 常量声明未读——首次 a 运行因此跑成了
   默认配置（maxSteps=24 全量使命）。修复：env 优先、空值走仓库默认。
   回归：`LoadDefault_EnvironmentVariableOverride_TakesPrecedence`。
2. **过渡屏幻影 back**（director）：路由回退身份（页面无标题，如启动
   过渡屏）被当作已知非根页 → 指挥 "Navigate up"（真机上 grounding 到了
   错误目标并验证失败）。修复：未知页（回退身份 ∧ 尚无已验证导航）→
   AgentDecision.Defer 有界重观察；配额尽 → bounded-stop:unknown-page。
   回归：`TransitionalStartupScreen_DefersUntilSettled_ThenCompletes`、
   `PersistentUnknownPage_StopsHonestlyAfterDeferBudget`。
3. **滚动候选重进循环**（ledger）：已进入跟踪只算 TargetPages 内
   descriptor，滚动候选（Search settings 等）永远"未进入"→ 反复重进同一
   候选直到预算耗尽（c2 的 30 enters/1 swipe）。修复：指令引擎改用
   AllEnteredDescriptors（全部已验证 root→child 导航）。回归：
   `EnteredScrollCandidate_IsNotReEntered_NextDirectiveAdvances`。

（附：上述回归测试揭示测试间竞态——Config/Ledger 测试类经
`[Collection("SettingsCoverageConfigSerial")]` 串行化，env 变异不再泄漏。）

## 真实验证到的负路径语义

- **maxSteps 有界停止**：诚实部分覆盖报告 + 退出码 3 + 未覆盖项清单 +
  分歧 NONE（a2、b）——不伪装 Completion。
- **真实验证失败路径**：post-action-target-unique 失败的步骤被逐步 trace
  捕获并成为首个分歧点（c1 step 0）。
- **模型不遵从 → fail closed**：c1 consult-2 模型返回非 act，偏离被记录、
  有界纠正重试后仍偏离 → 返回 null → TerminalNotProven（此前仅模拟证明）。
- **未知页 bounded-stop**：Defer 重观察后仍未知 → 零 effect 诚实停止（c3，
  修复 #2 的真机验证）。
- **咨询预算耗尽**：kernel 预算执法以 AgentDecisionFailed 分类呈现（c2）。

## 仍未在真机发生（如实声明）

- **滚动到底（内容不变）验证失败**：三次尝试（b/c2/c3）均未到达——真实
  列表在预算内未滚到底（b/c2 预算耗于进入候选；c3 停在未知页）。该分支
  仍仅由 `Swipe_ContentUnchanged_FailsClosed` 等确定性测试背书。
- **容量重试**：真实运行无容量错误，仍仅单测背书。

## 验证（确定性）

- solution build 0 errors；全量 **1166/1166** 测试通过（Kernel 712 /
  Simulation 184 / Host 98 / Agent.Dsh 132 / Core 14 / Agent 17 /
  FileSystem 9）；场景认证 --check PASS（本轮未改 Kernel/Agent 源，哈希
  未变无需重认证）；`git diff --check` 干净。
- 本轮新增测试 5 个：env 覆盖 1、过渡屏 2、候选重进 1（+修复揭示的
  串行化调整）。

## 证据文件

- `configs/settings-coverage-neg-{a,b,c}.yaml`：三次场景的配置。
- `run-*/coverage-steps.json`：逐步 trace（c1 含失败步骤与分歧点）。
- `run-*/coverage-report.json`、`facts.json`（含 consults 与终局理由）、
  `console.log`（退出码与配置回显）。
- 修复代码：`src/UniClaw.Host/SettingsCoverage/{SettingsCoverageConfig,
  SettingsCoverageLedger,SettingsCoverageDirector}.cs`。


## 追加确认（2026-10-01 晚，修复 3/4 的真机确认与全修复版收尾）

| 运行 | 结果 | 说明 |
|---|---|---|
| c4（run-c4-fix3-confirmed） | 23 步，BoundedStop，exit 3 | **修复 3 真机确认**：滚动后 9 个候选各进入一次、零重进（对比 c2 的 30 次循环）；末尾暴露修复 4（子页标题与根页相同 → "Navigate up" 被当候选）——系统仍 3 连败安全停止 |
| c5（run-c5-permission-dialog） | 0 步，exit 1 | 环境噪声：设备残留 Safety Center 权限页；模型想滑权限对话框容器，**director 两次拒绝错误目标、零 effect 停止**（遵从性机制的真实异常验证） |
| final（run-final-all-fixes） | **19/19 步、CoverageComplete 100%、分歧 NONE、exit 0** | 全部修复就位后的完整任务复跑；digest 与 AGT-005 原成功运行完全一致（9A8BC…）——修复未改变正常路径行为 |

修复 4（小）：ScrollCandidates 排除 BackDescriptor（返回键不是遍历入口）；
回归测试 `BackDescriptor_IsNeverAScrollCandidate`。追加后全量 **1167/1167**
测试通过（Host 99）。


## 追加实验（AGT-007：滚动到底分支的真机可达性，2026-10-02）

四次运行（d1/d2/d3/d4，run-d*）结论：**swipe 内容不变失败分支在本夹具上
不可达，且原因是确定性的**（d1/d3/d4 digest 逐字节一致 55291AC…）：

1. 第二次滚动后，列表底部的 "Security & privacy" 进入尝试确定性失败
   （tap 后路由指纹仍为 Settings——路由碰撞或未导航，step 20
   post-action-target-unique）。
2. 修复 5（AGT-007：失败候选跳过）生效后，下一个候选是低电量通知卡片
   的 "Dismiss" 按钮（真机长期运行的系统通知）；进入后模型对 back 指令
   反复回 DISMISS（有界纠正 ×2 后 fail closed，b-23）。
3. d2 独立复验未知页停止（与 c3 同 digest 9FF0C4B…，残留状态触发）。

**判定**：该分支的阻塞不是预算浪费（已修复）而是真实 UI 形态——底部
不可验证入口 + 通知卡片污染候选。所有运行再次零不安全 effect、诚实
停止（exit 3/1）。滚动内容不变失败维持确定性测试背书
（Swipe_ContentUnchanged_FailsClosed 等 6 项）。

修复 5：FailedEntryDescriptors——进入验证失败的候选不再重试；回归
`FailedEntryCandidate_IsSkipped_NotRetried`。全量测试通过（Host 44 项
覆盖测试）。


## 悬案告破（AGT-008 证据持久化后的 e1 复跑，2026-10-02）

e1 与 d1/d3/d4 digest 逐字节一致（55291AC…），但这次每步留档了
截图与 XML（run-e1-with-evidence/evidence/）。两个悬案的实证结论：

### 悬案 1：Security & privacy"点不进去"= 页面标题撞名（实证）

- 点击时屏幕（mystery1-pre-tap-security.png + 同名 .xml）："Security &
  privacy" 行 bounds [189,462][606,533]，落点 (540,523) **在行内** ✓
- 点击后屏幕（mystery1-post-tap-security.png）：**导航确实发生了**——
  进入的页面含 "Device may be at risk / See alert / Device unlock / Set a
  screen lock"，**其标题是 "Settings"**（与根页指纹相同）
- 结论：不是没点进去，是**进去的页面与根页同名**——标题指纹无法区分，
  验证按设计 fail closed。此前"点击未导航/动画未稳定"的猜测排除。

### 悬案 2："Dismiss"不是低电量通知，是安全风险告警（纠正此前误判）

- step 21 点击 Dismiss 后的屏幕（mystery2-post-dismiss-dialog.png）：
  弹出确认对话框 **"Dismiss this alert?"（CANCEL / DISMISS）**
- 完整链条：进入安全页 → 页面顶部有 "Device may be at risk" 告警卡 →
  账本把告警的 Dismiss 当成滚动候选 → 点击 → 弹确认对话框 → 路由变为
  无标题（对话框覆盖）→ 讽刺地"导航验证通过"
- **纠正**：此前 README 推测"低电量通知卡"是错误的——它是模拟器未设
  锁屏导致的**设备安全风险告警**（AVD 克隆的确定性状态，故 d/e 系列
  完全复现）。b-23 模型反复提议 DISMISS 的"执念"也随之改判：它点的
  是对话框上的 DISMISS 按钮——意图是**关掉对话框**（局部合理），
  被刚性指令拒绝。

### 含义

1. 标题撞名是真实现象（安全页标题=Settings）——route 指纹的已知局限
   得到实证（AGT-004 设计时已声明 fail-closed 语义）。
2. "协商通道"候选获得实证支撑：模型当时有一个局部合理的解困动作
   （关对话框），刚性指令词汇把它拒了。
