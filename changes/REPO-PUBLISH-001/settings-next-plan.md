# 发布完成后的Settings逐级验收准备（尚未执行）

前置为当前源码、记录和必要证据发布完成，原始引用在新checkout可读取。当前没有启动设备、DSH或真实模型。本文件是后续计划，不作为已通过的验收；不重开已closed的SETTINGS-COVERAGE-003或TRAJECTORY-001。

## 范围与通过条件
Settings根页记深度0；根页导航到的页面为一级，其子页为二级，再下一层为三级。弹窗单列到达/取消，不自动当作新层页面，也不取得确认权限。每级先冻结该设备/版本/语言、允许导航的范围与逐项验收，再运行；先完成一级，才开展二级，依次三级。

| 阶段 | 真实范围 | 可判定通过条件 |
|---|---|---|
| 根页普查 | 当前观察可导航入口及全部可滚动视口 | 保存每屏hierarchy/capture和来源、去重入口、屏间连续性及真实边界；无法证明屏外已发现则保留unknown，不能使用静态7项作为完整分母 |
| 一级 | 普查得到的安全入口及后续新发现 | 每个入口进入后有post-action验证和回根证明；策略禁止/危险/开关逐项有理由和证据，不计作已进入；任一必要安全入口未完成或发现范围未闭合则未通过 |
| 二级 | 每个已验证一级页的全部导航发现范围 | 对每个父页完成视口发现、同父身份/来源下子项去重、进入验证与返回；无子项必须有完成发现的证据，不能把首屏无项直接当叶 |
| 三级 | 每个已验证二级页的全部导航发现范围 | 同上，最大深度3由正式Traverse/Runtime控制；不得用直接Act绕过深度与准入 |

通过只表示所声明设备版本和授权安全范围的逐级完成；策略排除单列，不冒称所有系统功能已覆盖。BoundedStop/TerminalNotProven/预算耗尽/环境失败均非覆盖通过。某级失败自行沿首次分歧诊断、修复和原失败复验，普通下一步不向用户索要确认；只有必要设计或权限裁决才停。

## 实际入口与配置差异
- product/tasks/settings-coverage.yaml启用dynamicRootEntries，但仍有7个静态seed；dynamicNestedEntries.maxDepth=1且maxEntriesPerPage=1，不能直接拿默认成功条件当作全部二/三级覆盖。
- SettingsCoverageConfig.cs的DynamicNestedEntriesConfig.MaxDepth计一级后nested-enter层数；tests/UniClaw.Host.Dsh.Tests/SettingsDepthTraversalLiveTests.cs的phase depth是Runtime根页深度。启动前核对两个计数，不把配置数字相同当语义相同。
- 已有SettingsDepthTraversalLiveTests只登记census/depth1/depth2，三级需先按正式协议和现有Traverse深度能力核实入口，再补独立验收；不预先声明现入口足够。

## 真实运行和证据
只用注册p26_pixel克隆启动器并持有lease，环境preflight实际通过再开始；走3081专线和当前产品绑定，不改家目录代理、凭据或模型。每级独立Run，源/输入/配置指纹、启动原因和上级通过来源固定；capture/观察→Agent实际输入/DecisionId→guard→Effect receipt→post-action verification→World/coverage逐项账本→返回父页证据完整关联。每一级独立核对发现分母、禁止项、已进入/验证/失败/未知与终局。

后续运行使用docs/agents/test-asset-retention.md规定的仓库外任务Run目录，保留可消费最小证据和原失败，收尾按实录登记。全级通过后才可更新整体覆盖结论；不得把过去菜单仿真19/19和返回8/8当作真实Settings通过。
