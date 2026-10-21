# 旧开放任务与本轮前置关系（2026-10-21）
Luna只读盘点后Leader核对，不按旧日志机械关闭。INDEX由工具再生；本记录不另立任务系统。

| Change | 核验结论 | 本轮动作/依赖 |
|---|---|---|
| WORLD-OBSERVATION-001 | 页面身份补证与恢复仍在独立会话实施/high真实验证 | 当前产品重叠写入前置，待原Owner完成Review/Verify |
| WRC-003 | 独立双轴Review无阻塞，当前滚动19+Host重放5项通过 | 已按原范围关闭，见wrc-independent-review.md |
| RUNTIME-IDENTITY-001 | 原记录仅编译未执行测试；本轮补Store A1–A4的8项测试及独立Review | 已按原Store范围关闭；不是全HTTP/设备链验收 |
| RUN-006 | 日志closed与头部implementing冲突；同轮完整LocalView/标定/停滞指标证据不足 | 保留开放；不是ENG-OPT硬前置 |
| AGT-019 | Slice A/B离线机制有实现，真机LocalView/停滞改道及阈值标定仍未完整证明 | 保留开放；是RUN-006依赖，不是ENG-OPT硬前置 |
| RUNTIME-PLAN-001 | 泛化与收益门槛仍未满足，真实组合0/2有效等失败不能被002–006局部能力替代 | 保留RESOLVE；本轮不重开泛化设计 |
| SETTINGS-COVERAGE-004 | 定向弹窗分支有实机摘要，旧全量结果缺清楚输出指针 | 保留verified；补证完成前不机械关闭 |
| SETTINGS-COVERAGE-005 | 正式全遍历91/92、exit3/evidence-insufficient；006有定向恢复但非全树证明 | 保留开放，不把定向修复当完整Coverage验收 |
| ARCH-DOC-024 | 工程管理主题与所链生命周期分析不完全匹配 | 保留persisted，当前审查报告不倒填旧任务范围 |
| ARCH-DOC-025 | 生命周期分析及形式/场景依据基本齐备，但有历史状态/替代标记需统一 | 保留persisted，文档审查可独立收口，不阻塞本轮源码修复 |

原始依据：各changes/<id>/state.md与其spec/report/verification；RUN-006/report.md、runtime-plan-001/composition-20261018/results.md、settings-coverage-004/live-fix-summary.json、settings-coverage-005/full-traversal-20261018-103122/result-summary.json。以上“非硬前置”来自ENG-OPT明确的依赖顺序，不代表这些任务已完成。
