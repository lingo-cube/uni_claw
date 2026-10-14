# SETTINGS-COVERAGE-004 执行计划

1. 给 unknown-route Director 增加 Agent consultation seam，只允许 Defer。
2. 扩展 Settings popup 默认资源白名单，覆盖真实 namespaced resource-id。
3. 先跑 focused tests，再跑全量 solution tests、协议/schema/diff 检查。
4. 更新 Change 状态，提交并推送。

## 实际结果

- Deterministic focused tests and full solution tests pass.
- Live emulator run confirmed `defer-unknown-page` reaches Agent requests.
- Live emulator run confirmed `Clear the popup overlay by tapping '<CANCEL>'` reaches Agent requests after popup classification.
- The live run was stopped after targeted evidence; repeated SIM dialog traversal remains a separate frontier issue.
