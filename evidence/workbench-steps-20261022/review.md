# Workbench review
本次差异覆盖 Agnes 研究、P0、布局、卡片、集成，以及最终可读步骤。修改面仅为 Workspace UI 模块/测试、生成 Workbench client、README 和归属明确的 Change/研究/精选证据。

- 原来的步骤 fallback 直接 JSON.stringify 对象；现在来源 label/description 优先，tap 显示“点击”，其他动作保留原名，目标来自 descriptor/role，desiredState 明确标为目标状态。
- 每个对象步骤保留完整字段并转义展示在原生 details 中；参数展开状态复用 UI 阅读状态，刷新后仍展开；不会把提议步骤或提交回执标成设备执行成功。
- 没有修改 Host 投影、产品协议、Runtime 或 launch 请求。P0 的理由/回执分离和在途查询合并保留。
- 对布局/controller/browser 接线、阅读状态模块、renderer 和样式的累计 diff 复核：新增选择只是 UI 状态，原有受控 Evidence resolver 与 launch seam 保持；DSH client 由共享源码生成。
- 本轮原生参数详情使用稳定 key/id；文本、原始参数和属性统一经 escapeHtml。未知字段不丢弃，未知动作不翻译成已完成。
- 真实 DSH 验证仅查询既有会话；169/169 测试、参数展开/刷新保持、390px 换行与 bundle 内容一致通过。实际查看桌面和手机截图。
- 待提交文件使用显式 allowlist；其他 Agnes 报告、产品模型/协议、其他 Change、全局 changes/INDEX.md、原始运行日志、恢复快照和私有 profile 备份均不提交。

## 新发现
没有影响本轮交付的新阻塞。DSH 尚无 ToolInvoke 目录读取能力仍是已记录限制，本轮不扩展权限。
