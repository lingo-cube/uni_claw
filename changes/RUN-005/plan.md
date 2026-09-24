# RUN-005 — Plan

> 前置：Step 1 设计稿 v0.2（dual-grill revision 完成）。

## 步骤

1. ~~设计稿 v0.1 → grill~~（完成）。
2. ~~双 grill（owner 预审 + fresh-subagent 盲审）+ 交叉对表 + v0.2 合并修订~~
   （完成：四根双命中——预算域/认识论三态/FailClosed/Scope-lease；互补
   命中——target 表达/ClaimInSet 过冲/逐元素记忆/E4 楔死/ScriptedUniAgent
   重做/预算门；全部吸收进 v0.2）。
3. **Owner 复裁（下一步）**：对 v0.2 逐 finding 复核（处置表 = state.md）；
   通过 → 设计冻结候选。
4. **实现拆分（复裁通过后另立 plan）**：
   - 切片 A：PolicyTypes + AgentDecision.Policy + V6（RED 先行 P9/P10/P11）；
   - 切片 B：PolicyExpand（良基）+ PolicyTruth 求值 + PolicyInvalidated +
     _pendingPolicyOutcome/GuardCursor（P1-P8，含 E4 映射）；
   - 切片 C：ScriptedUniAgent 相位感知重做 + P12 + 全量回归 + 场景库随动
     （C8 搭乘）。
5. **AGT-002 衔接**：RUN-005 CLOSED 后 AGT-002 消费 authoritative records
   做 schema generation；RUN-005 不做 generation/bridge。

## 验证策略

- v0.2 每条裁决可指回双审 finding 编号 + FROZEN 上游条文/代码行；
- 实现期全 deterministic（ScriptedUniAgent；禁 live model）；
- 权威检查：driver 无 Policy mutation API；「Kernel never invents/repairs/
  extends」无违例路径（模板自带目标后「猜 target」路径结构性不存在）。
