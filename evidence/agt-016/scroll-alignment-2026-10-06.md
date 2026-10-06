# AGT-016 滚动授权对齐 — 所有者裁决实施与真实回合结果（2026-10-06）

> 裁决：所有者 2026-10-06 指令——"滚动 让 uni-agent 自行判断"。
> 实施：traversal 契约 `AllowedEffects` 由 `{tap}` 对齐为 `{tap, swipe-up}`（HostRunner.cs），
> 与预评审策略资产（scroll=safe）、guard 映射（swipe-up→scroll）、driver 支持集
> （AdbEffectDriver 含 swipe-up，AGT-005 词汇）三面一致。授权面扩大有显式裁决留痕。

## 结果（真实回合：NFC 目标，专用 3081 + glm-5.3-flash + emulator-5556）

| 项 | 对齐前（run-20261006-055534-070） | 对齐后（rounds/nfc-scroll-aligned/run-20261006-060514-984） |
|---|---|---|
| 滚动 | **0 次**——模型 3 次 defer："swipe-up not in allowedEffects" | **7 次真实滚动**（swipe-up → recycler_view / content_parent），全部 guard=Allow(scroll, safe class) |
| 总效应 | 12（7 navigate + 5 back） | 13（**7 scroll** + 4 navigate + 2 back），零 Reject、零 toggle |
| 搜索行为 | 受授权限制，克制不滚 | 自主判断：Network & internet 滚 3 次 → 转 Connected devices → Connection preferences 滚 4 次 |
| 终局 | TerminalNotProven (evidence-insufficient) | 同——找到头也没找到不存在的 NFC 开关后 defer（ui.role.switch.checked），诚实未完成 |

## 判读

- **滚动时机由 agent 自行判断**：裁决生效——模型在认为"目标可能在折叠下方"时自主提议滚动，授权链全程放行且可追溯（每条 guard 带 policy digest）。
- 搜索完整性提升：对齐前模型只能横向换页；对齐后能纵向下探，把 Connection preferences 整页搜完才放弃。
- 诚实性不变：目标确实不存在（本镜像 NFC 页无开关），终局依旧 TerminalNotProven，无伪造完成。
- 授权扩大的代价面：未观察到越权或误滚（全部滚动指向 scrollable 容器 locator）。

## 验证

- 确定性：Host.Tests 159/159（对齐后无回归）。
- 真实：上表回合四元组（facts/consultations/exec.journal/trace 全落 run dir）。

## 机制链补记（同日"下一步"核查）

授权对齐后，B2（滚动无变化→验证失败有界停止）的完整链路在 traversal 下**全部就位**：

1. 授权面：契约 AllowedEffects 含 swipe-up（本次裁决）；
2. Guard 面：swipe-up→scroll，策略 scroll=safe → Allow（本回合 7 次实证）；
3. Driver 面：AdbEffectDriver 支持 swipe-up（AGT-005 词汇）；
4. **验证面（内核级，与 coverage 共享同一缝）**：UniKernel 滚动后比对 dispatch
   前后可见 occurrence (role, descriptor) 集合——集合对称 ⇒ `ScrollContentChanged=false`
   ⇒ Assurance fail closed（UniKernel.cs VerifyPostAction；AGT-014 故障矩阵
   "滚动到底但内容不变"行的确定性对面，44/44 已证）。

即：**无变化滚动的检测不再有架构障碍**；真实回合中是否出现到底事件取决于 agent
当轮判断（本回合 7 次滚动未触发连续失败终局，终局原因为 evidence-insufficient/defer）。
本回合未捕获到底事件属机会性事实，如实记录；机制的确定性证明由故障矩阵承载。
