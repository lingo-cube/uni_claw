# AGT-016 — 真实 Settings 下一批：验证失败有界停止与滚动无变化

lifecycle_state: plan · disposition: none · depth: standard · base: 4c456463

## Intent（WHAT/WHY）

AGT-015 完成了"未知状态安全停止"与"错误目标拒绝"的真实设备/模型证据。所有者指令继续下一组。本 Change 承接故障矩阵中剩余的、且能在真实设备上**可靠呈现**的安全形态：

1. **B1 目标动作落地但验证持续失败 → 有界停止**：动作 receipt 存在但 post-action 观察始终不满足期望（如禁用开关：点击送达、状态不变），达到连续失败上限后停止，不伪造完成。
2. **B2 滚动无内容变化 → 验证失败 + 有界停止**：对无可滚动增量的页面滚动，无变化被检测为当前步骤验证失败，留 first divergence 后有界停止。
3. **机会性捕获**（不做 acceptance 依赖）：多步/组合动作的 dispatch 前拒绝、模型持续偏离 fail-closed——AGT-015 已偶然捕获一次 multi-step 拒绝；本轮如再现则记录，不因不可控而设为验收。

确定性对面（AGT-014 故障矩阵 44/44）已存在；本 Change 推进到真实设备/真实模型证据级，不重复实现。

## Scope / Out of Scope

### 范围

- 真机呈现探索（实现首步）：在 API 35 emulator 上实测找到 (a) 存在且可见、但 `enabled=false` 的 Switch（B1 载体）；(b) 内容不足以产生滚动增量的 Settings 页（B2 载体）。探索结果无论成败均记录。
- B1/B2 各至少一个真实回合 + 四元组证据（facts/consultations/exec.journal/trace）。
- 测试集扩展：android-settings manifest 增补对应 task/fixture/acceptance 声明（含任务级策略资产，如目标非 Wi-Fi）。
- 局部实现缺陷走复现→修复→回归（A5 同 AGT-015）。

### 不在本 Change 内

- 不修改架构、协议、模型路由或模型。
- 不把不可靠呈现（依赖模型恰好犯错）设为验收；机会性事件只记录。
- 不做无界探索；预算沿用测试集与 profile。
- 环境不可用时如实 ENVIRONMENT_UNAVAILABLE，不降级。

## Acceptance（实现完成的判据）

| ID | 用户可观察行为 | 预期与证据 |
|---|---|---|
| A1 | B1 验证失败有界停止 | 真实回合：动作 receipt 可追溯但状态不满足被逐轮记录；达上限有界停止；终局不伪造 Completion；first divergence 可定位 |
| A2 | B2 滚动无变化有界停止 | 真实回合：滚动后无增量被检测；步骤验证失败留痕；有界停止 |
| A3 | 呈现探索诚实记录 | 找到/未找到载体均有 dump 证据；未找到则该行为标 ENVIRONMENT_UNAVAILABLE 并说明，不硬凑 |
| A4 | 测试集与追溯同步 | manifest 校验通过；全链四元组落盘 |
| A5 | 局部修复有回归 | 同 AGT-015 纪律 |

## Plan（垂直切片）

| 切片 | 等级 | 内容 |
|---|---|---|
| S1 | CONTRACT | manifest 增补两任务 + fixtures/acceptances；策略资产按需（descriptor 与目标一致，沿用 AGT-015 fail-closed 绑定） |
| S2 | 探索 | 真机 dump 找 B1 禁用开关与 B2 短页；结论无论成败入 evidence |
| S3 | ENVIRONMENT | B1 真实回合（禁用目标 + 任务级策略） |
| S4 | ENVIRONMENT | B2 真实回合（滚动语义；模型驱动或受限场景如实记录） |
| S5 | 收尾 | 证据、state 回填、环境回收 |

B2 备注：滚动由模型提议；若模型不滚动（目标在首屏可达），记录实际路径并如实呈现——B2 的验收以"滚动无变化检测机制被真实触发"为准，未触发则标 not-elicited 而非伪造。

## Verification（待实施回填）

| level | method | expected | actual/evidence |
|---|---|---|---|
| CONTRACT | manifest/绑定校验 | S1 通过 | NOT_RUN |
| DETERMINISTIC | 对面在位检查（不重复实现） | 故障矩阵相关行仍绿 | NOT_RUN |
| ENVIRONMENT | B1/B2 真实回合 | A1/A2 四元组 | NOT_RUN |

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST → PLAN · 所有者"进入下一步"指令创建；承接 AGT-015 模式（任务级策略 + fail-closed 绑定 + 基准环境规则）；多步拒绝/持续偏离列为机会性捕获不设验收；B1/B2 呈现探索为实现首步，成败均记录。
