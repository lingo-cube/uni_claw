# Settings 全遍历决策者测试报告（2026-10-15）

> Status: ENVIRONMENT_CHARACTERIZATION / BOUNDED_STOP
> Authority: NONE (Leader advisory only; Host/Kernel remain authoritative)

## 结论

本次验证没有证明 Android Settings 全树完成。它完成了五轮环境和决策测试，证明了三件事：

1. 当前 Host/Kernel 能稳定执行配置声明的一级导航、返回、证据采集和安全拒绝。
2. 原抽象计划的“二级页”实际只统计一级入口后的路由，不能代表页面内部深度遍历。
3. 由 Leader 直接代 UniAgent 提交建议时，Host 的安全策略仍然有效；错误或高风险建议被拒绝，不会绕过 Kernel/Effect Boundary。

最强的默认 Director 运行完成 35/35 个已验证步骤，进入 16/16 个一级入口，但覆盖率只有 50%，因为滚动发现、重复进入和更深的二级页面没有完成。扩展计划运行进一步暴露了 nested plan、滚动候选角色和长运行终止问题。Leader-file 直连测试又暴露了决策桥请求竞态和 Host action policy 对页面内开关的过度归类。

## 范围和权限边界

本次由 Leader 代 UniAgent 负责：

- 读取当前 observation/context；
- 生成抽象计划和单步建议；
- 选择安全的导航、返回、滚动和显式 nested entry；
- 在失败后调整计划。

本次没有代替 Kernel：Run lifecycle、Contract admission、grounding、Assurance、Effect dispatch、post-action verification、terminal outcome 和 coverage report 仍由现有 Host/Kernel 链路决定。视觉能力由真实 Fast/Slow perception 和 ADB hierarchy 提供；Leader 没有用截图猜测替代视觉结果。

## 抽象计划快照

本轮使用的临时契约快照为 `evidence/settings-coverage-002/settings-coverage-full-tree.yaml`，在默认配置基础上扩大为：

- 15 个一级入口（排除了会落到无标题 `android.settings` 的 About emulated device）；
- `maxSteps=180`、`maxConsultRounds=180`、`maxScrolls=18`；
- 15 个二级根路由预算；
- 45 个显式 `parent > child` nested 候选，`maxDepth=1`；
- `Search settings` 作为显式滚动候选白名单；
- 截图、UI hierarchy、Slow text perception、语言检查和安全策略全部启用；
- Leader-file 直连实验将 `maxSteps` 收缩为 72，避免认知桥测试无限等待。

`deepTraversal` 只接受显式白名单，不把观察到的未知文本自动升级成目标。`scrollCandidateEntries` 只允许在 verified scroll 后、root 页面观察中出现的白名单条目进入计划。

## 每一轮结果

| 轮次 | 决策入口 | 结果 | 关键证据 | 首次分歧/边界 |
|---|---|---|---|---|
| R1 | DSH + 默认 SettingsCoverageDirector | `BoundedStop`，35/35 验证，16/16 一级入口，覆盖率 50% | `run-20261014-162142-145/coverage-report.json`、`facts.json`、71 个证据文件 | 原“二级页”只是一级入口后的 16 个路由；Search settings 没被旧 scroll candidate 角色识别；重复进入未发生 |
| R2 | DSH + 扩展 nested plan | 运行产生 94 组 prepare/submission/receipt journal frame，因长时间深遍历由 Leader 有界中止，未生成终局报告 | `run-20261014-163351-859/exec.journal`、192 个 hierarchy/screenshot 证据 | Host 已开始执行 nested page，但长运行缺少中间可交付终局；该轮不计为通过 |
| R3 | Leader-file + advisory-direct | `AgentDecisionFailed/no-response`，2/2 验证 | `run-20261014-165815-270/consult-bridge.json`、`facts.json` | 首次测试决策桥请求/响应竞态；决策桥不是产品协议，超时 fail-closed |
| R4 | Leader-file + advisory-direct | `AgentDecisionFailed/no-response`，2/2 验证 | `run-20261014-170108-389/consult-bridge.json`、`facts.json` | Leader 选择 Wi-Fi 后，Host policy 拒绝无 typed desiredState：`unknown-action:switch target is visible; choose typed desiredState before another navigation` |
| R5 | Leader-file + advisory-direct，安全回退到一级导航 | Kernel `Completion`，但 CoverageReport `BoundedStop`，12/12 验证，5/15 一级入口 | `run-20261014-170444-227/consult-bridge.json`、`coverage-report.json`、`facts.json` | Kernel 的目标完成与 Coverage 的覆盖完成分离；Leader 主动 NoAction 后以退出码 3 诚实停止 |

R2 的中止是操作层有界停止，不是行为通过；R3/R4 的失败是保留的负向证据，不是重试后删除的噪声。

## 逐步链路证据

每个完成的 Host 步骤至少包含：

```text
observation / hierarchy
→ abstract directive or advisory decision
→ AgentDecision
→ Host action policy
→ grounding / assurance
→ ADB effect receipt
→ post-action capture
→ route transition / verification
→ ledger update
```

R1 的 `coverage-steps.json` 证明 35 个步骤全部有 `DeliveryCompleted` 和 post-action verification。R5 的 `consult-bridge.json` 同时保存了 Leader 原始建议和 Host policy guard 后的 effective decision，避免把被拒绝建议伪装成已执行建议。

## 安全白名单和系统资产

当前策略文件是 `product/policy/android-settings-forbidden-actions.json`，运行时 digest 为 `d6b3a0c65bde9776daf031c671d2684e8218626b7d3f33a868601209a43113a8`。本轮实际执行的安全底线为：

- safe：`observe`、`scroll`、`navigate`、`back`；
- targeted：`toggle:Wi-Fi`；
- forbidden：`destructive`、`account-removal`、`credential-change`、`developer-debug`、`permission-grant`、`unknown-action`；
- unknown target disposition：`reject`。

本地系统资产已落盘：

- `system-inventory/getprop.txt`：Android build/device properties；
- `system-inventory/activity-top.txt`：当前 Activity/任务栈；
- `system-inventory/system-packages.txt`：系统包清单；
- `system-inventory/settings-global.txt`、`settings-secure.txt`、`settings-system.txt`：系统设置键值；
- `system-inventory/settings-root.xml` 与 `settings-root.png`：Settings 根页层级和截图；
- 每个真实 run 下的 `evidence/*.xml`、`*.png`、`settings-trace.json`、`trace.json`、`exec.journal`、`language-findings.json`。

Android 官方 `Settings.ACTION_SETTINGS` 只定义“显示系统设置”这一入口，系统内部页面和 OEM/版本菜单仍由设备观察决定；本报告没有把 API 入口表当成全树目录。[Android Settings API reference](https://developer.android.com/reference/android/provider/Settings)

## 抽象计划生成经验

可复用的计划生成顺序是：

1. **先冻结资产目录**：从设备根页 hierarchy 建立一级目标清单；没有目标清单时只能做 observation，不能宣称全树。
2. **把风险分类前置**：目标先经过安全策略分类；切换类控件必须带 typed desiredState，导航类动作必须有明确 semantic target。
3. **只从观察生成下一步**：当前可见且唯一的元素才进入候选；滚动候选必须在 verified scroll 后产生，未知角色不自动升级。
4. **每个父页显式声明子目标**：`parent > child`，并单独统计 nested coverage；不把“进入父页”算成“深度遍历”。
5. **每一步都闭合证据链**：决策、Host guard、receipt、post-action capture、route transition、ledger 更新缺一不可。
6. **终局分开判断**：Kernel objective completion、Host run terminal 和 CoverageReport completion 是三个不同判据；任一覆盖项未满足只能报告 BoundedStop。

## 发现的问题和建议

### P0：决策桥请求身份需要绑定 Run

多个运行在不同 inbox 中产生了相同形状的 `decision-...` 文件名；如果复用 inbox，旧 response 可能被错误匹配。正式实现应把 `productSessionId + runId + decisionId + attempt` 纳入文件名和 response 校验，或每个 Run 使用独立 inbox。

### P0：Host action policy 对“页面含开关”和“导航到页面”区分不足

R4 中 Leader 选择 `Wi-Fi` 或其他导航条目时，Host 以页面存在 switch 为理由拒绝无 desiredState 的导航建议。当前策略能安全拒绝，但导航目标应绑定 occurrence/semantic class，而不是只看页面中是否存在可切换控件。建议另开 Change，增加 `navigation-target` 与 `state-change-target` 的 typed 分类。

### P1：滚动候选的 perception role 需要正式闭合

旧实现只接受 `ui.element`。`Search settings` 在真实层级中表现为搜索入口，未稳定进入旧候选集合。本轮使用显式 `scrollCandidateEntries` 作为临时白名单，后续应把 `ui.search` 作为有证据的导航角色纳入 typed projection。

### P1：深度计划需要中间终局和恢复点

R2 已产生大量有效资产，但长运行中止后没有可直接读取的最终 CoverageReport。建议把每 N 步写入 checkpoint，并支持从 activation/ledger checkpoint 显式 resume，而不是只能等待整轮结束。

### P1：nested 深度当前只闭合一层

`maxDepth=1` 是本轮明确边界。继续向第三层扩展前，应先有真实 buyer、子树目录来源和可验收的安全策略，不应把未知页面自动递归。

### P2：Leader-file 直连应保留为测试工具

它适合验证“决策者建议”和“Host/Kernel 执法”之间的边界，不应成为产品运行时的第二套决策协议。

## 最终判定

- Host 基本一级导航能力：**通过**；
- 证据采集和每步验证：**通过**；
- 安全策略 fail-closed：**通过**；
- 当前抽象计划的深度执行：**部分通过**；
- Android Settings 全树遍历：**未通过，证据不足**；
- Kernel 权威边界：**保持，未被 Leader 绕过**。

## 工程回归

- SettingsCoverage 相关 Host tests：73/73；
- `dotnet test UniClaw.Kernel.slnx --no-restore`：1533/1533；
- Host.Dsh build：0 errors；
- 场景认证：28 files，0 violations；
- invariant matrix：47/47；
- `git diff --check`：通过。
