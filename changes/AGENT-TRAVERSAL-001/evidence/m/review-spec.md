# M独立Spec审查及最终窄复核

比较固定点：M entry/manifest.json与entry源码→owned.patch；未提交、不使用共享checkout的HEAD大diff。意图：Change Q1–Q4、m/plan.md；代码与测试审查独立于实现者。

初审0阻断/0非阻断。私有Driver游标记录发起容器/覆盖层状态；仅对普通多步Act比对，越界以StepRejected、正确index、剩余tail和本提案已验证receipt引用回Agent。Plan未进入该分支。逐步Guard失败索引在作废Plan前捕获；Host仅在逐步Guard已安装时延后Act检查。popup/unknown route约束落在当前事实路径，保持等待/失败零后步、预算和无Guard拒收。

最终窄复核新增0阻断/0非阻断。AgentActionProposal XML、P25 LocalAct与AgentProjection只读上下文说明范围，无wire字段/Owner扩张，不改全局Profile/Prompt/Skill。other-popup直接Act反例使用现有WindowSource，第二步VerificationFailed、failed index=0，仅open+CANCEL两次投递，无Other entry尾部；实际TRX通过。CONTEXT补记用户已决术语，不新增执行机制。

结论限于实现/文档可收尾；全量TRX和入口失败对照由独立verify.py核验，不把审查0发现当作全绿或最新共享树整合认证。
