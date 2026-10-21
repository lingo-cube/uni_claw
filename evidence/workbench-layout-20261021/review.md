# Workbench 布局修改与验证

## 做了什么 / 原问题
原界面把对话、Trace 与常驻 Metadata 同时铺开，对话空间有限；整页挂载也会丢失滚动、展开项和输入焦点。本轮将解决过程作为默认主区，Trace 独立切换，详情按需打开，并恢复 UI 阅读状态。

## 实际修改
- `src/ui/render-workspace-html.js`：主区/详情外壳、对话记录选择、只读详情展示及独立 Trace，状态标签本地化；不新增产品执行权限。
- `src/features/workspace-controller.js` / `workspace-view-model.js`：增加主视图和详情选择的 UI 状态，切任务关闭旧面板；保留已有查询隔离。
- `src/ui/workspace-reading.js`：按任务保存当前 UI 的滚动锚点、Trace 展开和焦点；底部跟随、上翻提示新内容；窄屏详情焦点约束与 Escape 返回。只在 UI 生命周期内持有状态。
- `src/ui/workspace-layout.js` / `styles/workspace.css`：桌面并排、较窄窗口详情抽屉、手机任务导航；消除任务栏横向滚动条。
- browser entry 接线、生成 client、测试与 README 同步。

上述 `src` 路径均相对 `web/uniclaw-workspace/`；生成 client 在 `dsh/uniclaw-task-workbench/src/client.js`。

## 验证结果
- 现有 Controller/renderer 接口新增行为测试，RED→GREEN；Web + DSH Workbench 164/164 通过，语法与 diff 检查通过。
- 浏览器消费的 client SHA256 与最终构建一致；桌面检查对话、Trace、选中详情、展开状态、焦点返回。
- 阅读中刷新新增一轮：scrollTop 保持 1100，新内容提示出现；切换任务再返回仍为 1100。
- 连续键盘输入后需求全文与焦点保持；未执行模拟提交或任何真实设备任务。
- 390×844 窄屏：导航/详情抽屉可开关、背景 inert、焦点约束、Escape 返回原入口，页面 scrollWidth=390；截图已逐张查看。
- launch 浏览器请求块及 Controller launch/composer 块与修改前逐字节一致；没有产品协议或 .NET Runtime 变更。

## 并行写入与交付位置
共享 checkout 的 browser entry 多次被并行写回前一轮 P0 原文，生成包因此曾缺少新挂载。已保存冲突证据，将完整布局移至附在当前聊天的 worktree：`/Users/fran/.codex/worktrees/workbench-conversation-layout/uni_claw`。共享 checkout 中只恢复本轮选定文件到入口快照，保留原有 P0 改动；未回退其他人的修改。见 `isolation.json`。

本轮完成的是隔离实现与验证，**尚未合回当前共享 checkout，也未更新正在运行的 DSH profile/服务**。布局单独补丁为 `layout-only.patch`；当前 worktree 的 Git diff 还包含入口继承的上一轮 P0。

## 新发现 / 下一步
- 整体 DOM 挂载仍存在，本轮以稳定阅读键恢复用户状态，没有引入另一套前端框架或新的同步协议；真正的 keyed incremental render 可另作切片。
- 上轮发现的成功提交后 composer 未自动关闭，本轮未改变其返回值/命令处理；仍需单独修复。
- 待共享 browser entry 的并行写入结束后，再将 `layout-only.patch` 合回并进行真实 profile 挂载验收；静态包和 fixture 通过不等同于当前部署已经更新。
