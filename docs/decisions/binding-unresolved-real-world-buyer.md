# BindingUnresolved Real-World Buyer — Gate Record

> Status: BUYER_ANALYSIS_COMPLETE（决策 E — 场景/环境缺陷；无 buyer 需购买）
> Date: 2026-08-17
> Prerequisites: REOPEN_L1_REAL_WORLD_VALIDATION = B（TUNING）· L1 架构本 gate 冻结
> Constraint: 零生产实现；L1（IAssistanceProvider/触发面/词汇/wire/bridge/consumer）不变

---

## 1. 真实失败链（证据重构）

```
raw frame（WIFI_SETTINGS 启动后帧，emulator-5556）
    ↓ adb screencap 1080×1920 PNG
Vision YOLO/OCR/fusion（真实服务，identity accepted）
    ↓ 24 个候选元素（text/type/bounds 真实）
Observation elements（Network details / Androidwifi / CONNECT·FORGET·SHARE /
                     Signal strength / Security / Network usage 等）
    ↓ binding analysis（ElementBindingCriteria: 文本锚 "Wi‑Fi" + 类型 toggle）
BindingUnresolved —— "Wi‑Fi" 锚零匹配（页面无该行）
```

**最早消失点**：目标证据在**观测层就缺失**——页面是网络详情页，本无 Wi-Fi 开关行。

## 2. 真实观测（探针实测 24 候选）

| 证据 | 状态 |
|---|---|
| "Wi‑Fi" 文本 | **不存在**（无该元素） |
| toggle/开关证据 | **不存在**（无 toggle 类型） |
| 页面实际内容 | 网络详情页：Network details / Androidwifi / CONNECT / FORGET / SHARE / Signal strength / Security / Network usage（menu_item/button/icon/text_block 类型，感知忠实） |
| 目标语义对象 | **真缺失**（非拒绝） |

**分类：A. TARGET_NOT_PERCEIVED**（目标在当前感知页面确实不存在）。

## 3. 感知 vs 绑定

**两者都不是根因**：感知 ✅（24 元素真实、类型合理、文本准确）；绑定 ✅（规则正确——"Wi‑Fi" 锚不存在即拒绝，truthful fail-closed）。根因在**场景/启动**：`android.settings.WIFI_SETTINGS` intent 在 emulator 已连接网络（Androidwifi）状态下**落到网络详情页**而非 Wi-Fi 列表页——目标行根本不在感知页面。**不为补偿缺失的原始证据购买智能**（§3 禁止）。

## 4. 本地确定性恢复

BindingUnresolved 当前直接 fail-closed，之前无可尝试的既有机制（无"目标缺失回退导航"语义）。候选本地恢复：详情页 → back → Wi-Fi 列表页的确定性导航——但那是**导航/场景语义扩展**（新 buyer），非既有机制未接线。分类：**NO_LOCAL_RECOVERY_BUYER**（当前无既有可用机制被跳过）。

## 5. Assistance 是否有真实 buyer

**不成立**：Observation 中**没有**足够信息让智能裁决器选择目标（目标行不在页面——连候选元素都不存在）。§5 明确："If raw target/control evidence is absent → Assistance expansion is NOT justified"。无 BINDING_ADJUDICATION_ASSISTANCE buyer。

## 6. rebind 词汇可达性

当前 L1 seam 在 belief 裁决后进入；BindingUnresolved 失败发生在**之前** → **rebind 当前无 reachable 真实 buyer**。分类：**C. 证据表明更早的 Assistance 触发被有意 deferred**（binding 面不在已毕业 L1 范围）；无害词汇保留，本 gate 不改。

## 7. 候选 buyer 矩阵

| 候选 | 证据 | 层 owner | 预期收益 | 架构成本 | 外部智能必要？ |
|---|---|---|---|---|---|
| A 感知修复 | 感知正常（24 元素忠实） | Perception | 无（非感知失败） | — | 否 |
| B 绑定生成修复 | 绑定规则正确拒绝缺失目标 | Binding | 无（规则无误） | — | 否 |
| C 本地绑定恢复 seam | BindingUnresolved 无既有回退机制 | Runtime | 详情页→列表页回退导航（场景级） | 导航语义扩展（新 buyer） | 否 |
| D L1 binding assistance 扩张 | 目标元素缺失，无可裁决信息 | Assistance | 无（§5 不成立） | 触发面扩张 | 是但无信息可裁决 |
| **E 场景/环境缺陷** | **WIFI_SETTINGS 落页依赖 emulator 网络状态** | 场景/设备准备 | 稳定落页（列表页） | 低 | 否 |

## 8. L1 冻结确认

零改动：IAssistanceProvider / Contradicted·Unresolved 触发 / 词汇 / AssistanceWireProvider / AssistanceBridge / LlmAssistanceConsumer 全部不变。

## 9. PhysicalHost wiring 修复分类

`BuildDriverHostServer` vision socket 注入 = **A. MECHANICAL_BOOTSTRAP_WIRING_REPAIR**——已毕业 managed Vision 端点（host.SocketPath）向 run.start DriverHost 路径的缺失机械传播；非架构变更（§vision-runtime-bootstrap 已冻结端点真值；本修复只是把该真值传入 factory）。回归证据：构建通过；真实 L1 host 全链（VISION_HEALTHY → run.start → 终端状态）可运行。

## 10. 下一步

- **场景修复（非架构）**：slice2 等真实闭环前稳定设备落页——用 `am start` 冷启动 Settings 根页 + 确定性导航（multilevel 路径已示范），或显式落到 Wi-Fi 列表页（force-stop + 根页 + 逐跳），避免 WIFI_SETTINGS 在已连接状态的落页歧义。
- 若未来需要"目标缺失 → 回退导航"的确定性语义：独立 buyer（LOCAL_BINDING_RECOVERY 或导航扩展）——本 gate 不实现。
- L1 真实验证重跑时使用稳定落页场景（如 multilevel Settings 根页 → Network & internet → Internet），使 belief Contradicted/Unresolved 有自然产生机会。

---

## FINAL DECISION

**`E. SCENARIO_OR_ENVIRONMENT_DEFECT`** — 真实 BindingUnresolved 根因 = `WIFI_SETTINGS` 启动意图在 emulator 已连接网络状态下落到网络详情页，Wi-Fi 目标行不在感知页面；感知（24 元素忠实）与绑定（规则正确拒绝）均正常。无 PERCEPTION / BINDING / ASSISTANCE buyer；本地恢复与 L1 扩张均不成立（§4/§5）。修复属场景/设备准备（稳定落页），非架构购买。L1 架构保持冻结。
