# SETTINGS-COVERAGE-002 执行计划

1. 环境预检：启动注册模拟器，确认 ADB、Settings 根页、专用 DSH 和模型/协议 hash。
2. 计划装载：读取 `product/tasks/settings-coverage.yaml`，展开目标、边界、终止条件和安全策略。
3. 决策执行：由 Leader 代 UniAgent，根据每轮 observation 选择 Host 可执行的单步 directive；保留修复轮次。
4. Host 验证：检查 Director guard、Effect receipt、post-action verification、ledger 和终局报告。
5. 报告落盘：生成完整环节报告、每轮修复表、coverage 对账、抽象计划经验和能力边界。
6. 若需要代码修复：最多 5 轮；修复后重跑验证，再提交并推送。

停止条件：环境不可用时记录 ENVIRONMENT_BLOCKED；连续 5 轮仍无法修复时停止并汇总，不猜测通过。
