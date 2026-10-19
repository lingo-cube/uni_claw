# Pro真实Settings三组对照结果

功能全部完成，性能门槛未全部通过。RUNTIME-PLAN-001保持VERIFY，不关闭。

正式模型为deepseek-official/deepseek-v4-pro；13个实际会话的model/selection及全部调用正数usage已核对。Prompt/Profile17、provider bundle、目标、权限与必要观察验证保持一致。

| 范围 | 模式 | 有效/全部尝试 | 平均咨询 | 平均模型调用 | 平均token | 平均正式时延(s) | 全部Plan门槛 |
|---|---|---:|---:|---:|---:|---:|---|
| known-path | Step | 1/1 | 3 | 6 | 82081 | 27.75 | 基线 |
| known-path | ShortPlan | 3/3 | 2.33 | 5.67 | 75613 | 32.69 | 未通过 |
| known-path | RulePlan | 3/3 | 2 | 4.33 | 51829 | 30.37 | 未通过 |
| candidates | Step | 2/2 | 5 | 10.5 | 195483 | 46.77 | 基线 |
| candidates | ShortPlan | 2/2 | 2.5 | 5 | 74587 | 37.26 | 通过 |
| candidates | RulePlan | 2/2 | 2 | 5 | 65437 | 39.14 | 通过 |

全部失败、额外工具调用与拒绝成本均进入相应组分子，按有效完成数摊销。Pro三份Step与十份Plan均功能有效、usage完整、Slow为0。账单金额不可得，不报告货币费用节省。

已知短Plan第二份复核插入Control Reobserve。其实际语义为废弃尾部并回Agent，区别于Plan中的Observe；Runtime依协议让出，模型另提Act返回后再NoAction，共3次咨询。该会话还发生no-pending及缺decisionId拒绝，额外调用全部计入。其他已知Plan也显示模型及输出回合耗时波动。完整审计见known-path-overhead-audit.json；不能剔除慢样本达成PASS。

方法偏差明确保留：两份known Plan在门槛文件落盘前启动；baseline与既定公式已固定，未查看结果前通过显式、独立复审的结果盲法late lock锁定数值。它不满足procedure的严格pre-Plan顺序，不能称无偏差的前瞻预注册。四份候选Plan在lock后启动。known两模式各追加两次是首次短Plan时延超限后固定的探索性扩展，全部四次完成、不提前停；原样本、基线、门槛均未变。

Flash accounted对照独立保留：六份Plan全部功能有效，四组中三个全门槛通过，known短Plan调用门槛失败。Pro不借用Flash门槛或合并收益。

全部历史试验为49次尝试、40次有效完成、331次模型调用；已报告token下界7900624。旧Provider修复前13次调用缺usage，仍保持未知，不回填零或估算。见comparison-result-pro.json的wholeExperimentIncludingPriorEpochFailures。

范围仅为已知2动作路径、当前2候选/4动作，不是全树遍历、现场新增C或统计显著/普遍提速证明。动态新C、局部受阻及明确续行由确定性Driver测试证明；不同结构记录买方是仿真。

若继续解决2动作代表段的性能缺口，应先明确Control Reobserve与Observe语义，并审查模型在工具接受后的重复提交、缺ID输出，再决定是否修改Prompt或决策回合策略并建立新对照。当前不放宽门槛、不静默换回Flash、不将候选集合PASS外推为全部PASS。
