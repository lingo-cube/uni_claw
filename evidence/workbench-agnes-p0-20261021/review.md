# Workbench P0 修改审查

结论：本轮两个 P0 已完成，变更保持在现有查询/展示层。证据汇总见 [verification.json](verification.json)。

## 原问题与修改
- ViewModel 原先优先使用 decision.result.text，导致提交回执覆盖理由；现在只对 `kind: decision` 禁止该回退，result/tool 等其他节点保留原有顺序。
- DSH adapter 原先在四个 pane 中独立调用完整 session；现在以 Product Session + 实际 Host Session 为键共享在途读取，结算后释放。成功/失败不长期缓存，各 consumer 保留自己的 capability 和错误来源。
- 同步重新生成 DSH 静态 client，README 说明行为与快照限制。

## 范围与边界审查
- 只改两个实现源文件、三个既有测试文件、README 和生成 bundle；没有产品协议或 .NET Runtime 修改。
- `createDshWorkspaceBrowserBridge` 函数及后续 launch/controller 接线，与入口快照逐字节一致；发起任务字段与命令路径不变。
- 不声称 Host 内一次详情读取是跨 Runtime/Host 的原子事实快照；未制造 Owner revision。
- 没有修改当前服务或安装 profile。浏览器只跑声明为 fixture 的模拟 Host，不操作设备。

## 审查发现与处置
1. 初稿将 justification 提前会改变非 decision 的嵌套结果优先级。补充行为断言复现为 RED 后收紧为仅 decision 分支，GREEN；保留 RED/GREEN 输出。
2. 浏览器发现输入触发全页重绘会替换输入控件，提交后弹窗不自动关闭。在同一 fixture 中加载入口保存的旧 bundle，二者均复现，认定为既有问题，未混入本轮。模拟 Host 两边均收到一次正确请求。
3. 既有 tracked diff 受并行工作改变，无法作全工作区内容不变声明；不撤销这些变更。本轮选定文件进入时均 clean，原文在 before/，实际 patch 单独保留。

## 验证结果与后续
- 现有接口的回归测试先 RED 后 GREEN；Web + DSH Workbench 全部 161 项通过。
- 浏览器旧包：同一选择 4 次详情、决策显示回执；新包：1 次详情、决策显示理由；再次刷新发起新读取，切任务和 Evidence 展示正常，模拟表单请求字段保持。
- 浏览器仅做 DOM/交互验证，未做像素级视觉验收或真实 Runtime 任务认证。
- 后续优先修输入焦点与成功提交后的弹窗收起，再讨论局部渲染/稳定节点/增量协议；这些不属于本轮已实现范围。
