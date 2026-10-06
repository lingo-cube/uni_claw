# AGT-014 DSH Settings 覆盖回合 — 2026-10-06

## 结果

使用 7890 代理、API 35 emulator-5556 和同一个 `zai-coding-cn/glm-5.3-flash`
DSH 会话，真实执行有界 Settings 覆盖：

- `Completed / Completion`
- `CoverageComplete`，覆盖率 `100%`
- `19/19` steps verified，`20` consultation rounds（最后一轮为 coverage-complete
  `noAction`）
- `7/7` 一级入口、`1/1` 滚动发现项、`8` 个二级路由、`9` 次返回、`1/1` 重复进入
- `firstDivergence=NONE`
- 真实 effect：`tap=18`、`swipe-up=1`；所有 receipt 均 `DeliveryCompleted`
- 没有 Wi-Fi、USB debugging 或其他开关目标被提出或执行

运行目录：
`evidence/agt-014/dsh-task3-coverage-proxy-20261006/run-20261006-025112-969/`

## 安全策略前置验证

首次 consultation 的上下文已经包含：

- `policyRef=fixture/android-settings/forbidden-action-policy`
- `safe=[back,navigate,observe,scroll]`
- `forbidden=[account-removal,credential-change,destructive,developer-debug,permission-grant,toggle-non-target,unknown-action]`
- 固定 digest：`4186f07dfc6b17b29f59c57d782130cf16245bb503fd66511a7018176801081e`

这证明策略是在首次真实咨询前生成并进入上下文，后续每个 act consultation 的
facts 都回填同一个 digest，20 轮没有换策略。

## 证据位置

- `coverage-report.json`：覆盖项、覆盖率、首个分歧点。
- `coverage-steps.json`：每步 DecisionId、DSH session、目标唯一性、receipt、
  ADB 命令、前后 route、post-capture 和验证检查。
- `facts.json`：终态、策略 digest、20 轮咨询和 Guard verdict。
- `exec.journal`：19 个 prepare/submission/receipt 三元组。
- `environment-preflight.json`：视觉服务预热 `63.818s`，正式流程在预检后开始。
- `settings-trace.json`：20 个观察周期；首帧快路径 `0.693s`，后续约
  `0.8s`，层级读取约 `2.0s`。

## 边界

这是 profile 声明的有界覆盖，不是 Android Settings 全树穷举。任务一“找到
Network & internet”的独立两步真实证据仍来自已有 AGT-003/AGT-004 回合；本次
覆盖的第 1 步也重新证明了该目标的唯一定位和 route transition。AGT-014 仍需把
三项任务证据汇总后再关闭。
