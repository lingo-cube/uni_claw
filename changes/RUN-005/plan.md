# RUN-005 — Plan

> 前置：Step 1 设计稿完成（plans/2026-09-24-run-005-l2-policy-runtime.md）。

## 步骤（Grill 后展开；本 plan 只到 grill 准备）

1. **Grill**：攻击面 = ①谓词求值的 authority 性质（机械 vs 规划）②
   PolicyExpand 与既有状态机的合并正确性（PolicyInvalidated 不成第二状态机）
   ③词汇表充分性对照 18 场景（漏掉的 L2 场景是否都被 DEFER 正确登记）④
   PolicyState ephemeral 裁决 vs restart 语义 ⑤两层预算的边角（bounds 与
   Defer/重咨询交错）⑥freshness 逐项复核 ⑦P1-P12 可判定性。
2. **修订**（如有 findings）→ 复核 → 冻结设计。
3. **实现拆分（grill 通过后另立）**：
   - 切片 A：PolicyTypes + AgentDecision.Policy + V6 校验（RED 先行：
     P9/P10/P11 三个校验场景）；
   - 切片 B：PolicyExpand phase + 复用 Act 链 + PolicyInvalidated（P1-P8）；
   - 切片 C：P12 + 回归（全量 Kernel/Simulation + 场景库期望随动按 C8 搭乘）；
   - ScriptedUniAgent Policy 脚本形态随切片 B。
4. **AGT-002 衔接**：RUN-005 CLOSED 后，AGT-002 消费最终 authoritative
   records 做 schema generation（.NET → JSON Schema → DSH submit_decision）；
   RUN-005 不做 generation/bridge。

## 验证策略

- 每条新裁决指回 FROZEN 上游条文或 18 场景编号或代码行；
- 实现期全部 deterministic（ScriptedUniAgent；禁 live model）；
- 权威检查：driver 无 Policy mutation API；grep 级证明「Kernel never
  invents/repairs/extends」无违例路径。
