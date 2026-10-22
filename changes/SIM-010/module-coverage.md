# SIM-010 模块覆盖增量
完整模块契约与已有测试入口沿用 SIM-009/module-coverage.md，本表只记本次新增断言；全量执行数字待本轮 full 汇总，不借用前置快照。

| 模块 | 本次入口与新增断言 | 未被本次证明 |
|---|---|---|
| Host / Perception | RecordedHierarchyImportTests：原 XML 哈希、公开 parser+projector+UiHierarchyOccurrenceStrategy 逐帧导入一致性，六份 typed/候选输入冻结；无 canonical state | 新设备、采集真实性、精确状态 proof 内容校验；导入是离线适配 |
| Evidence | RecordedOperationReplayTests：真实 Ledger 接纳、当 capture 全字段入账后才释放候选、World occurrence basis 均在 canonical records；输入篡改拒绝 | 跨进程持久化、并发及真实性签名 |
| World | 真实 WorldModel 铸 ID、绑接纳 basis、建导航边；没有把 Slice/graph 当输入 | 新未知清单、多容器、目的地语义、任意分支 |
| Run / Control / Runtime | 缺 post 连续 Drive 均 WaitingForInput、零验证、单次投递；release 后同一 Run 续行；实际 StepVerified/VerificationFailed 再咨询相位 | 进程重启、真时钟、取消竞争、Agent模型质量 |
| Assurance | 网络正确 post 验证；未变 post 虽通用tap验证而独立导航期望不满足；旧 false 无 checked claim 拒绝；同值 checked 与期望off不符拒绝 | 旧 false 证明关闭、通用tap目的地符合性；proof/guard缺口另行设计 |
| Effect | 实际绑定目标 occurrence 与 receipt 一致、空间1080×2400与单次delivery | 真设备触控或外部交付保证 |
| Agent | 确定性外部决策替身；检查实际咨询相位和失败原因 | 实际模型决策、供应商、prompt还原质量 |
| Core / FileSystem / Agent.Dsh / Host.Dsh / Trace | 本次无新增专项，仅由八工程 full 回归保护 | 本次回放未覆盖的各模块完整性 |

开关的 expectedBehavior 是 Unknown→拒绝；desiredOutcome 仍是 off 已证明。场景 passing 只表示载体检出了预期拒绝，不表示用户目标完成。
