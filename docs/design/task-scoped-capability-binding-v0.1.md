> Status: DRAFT / ACCEPTED_AS_CHANGE_INPUT
> Authority: NONE
> Date: 2026-10-04
> Change: CAP-005

# 任务级 Capability 注入设计 v0.1

本轮确定职责、输入约束和验收场景，不冻结 C# 方法，不实现生产 binding。
约束继承 ADR-0035、ADR-0038 和产品基线；注册域与实例生命周期是两个维度。

## 当前调用者与创建者

| 调用链 | 当前事实 | 设计依据 |
|---|---|---|
| `CapabilityRegistry.Register(ICapability) → Resolve(id)` | 保存并返回同一实例，没有 task 参数、取消或释放协议 | 继续用于 Host 常驻实例，不承担任务隔离 |
| `SimulatedCapabilityHost.Create → LanguageInspector / OperationMeasurement` | 测试夹具注册实例，调用时传 Run correlation | 证明结果协议可用，不能证明生产实例生命周期 |
| `RuntimeHttpServer.StartRunAsync → RunStore.Create → DSH attach → LaunchContext → RunBackground` | Runtime 创建 Run/ProductSession；Host 创建并释放 per-run peer/channel/agent | 身份及资源 owner 可复用；尚未通过 Capability binding 装配 |
| `SettingsTraversalLiveFeed → SlowConsultation → SlowResultProjector/P2` | Host 提供同周期输入，既有 Owner 接收 proposal | Binding 不增加 Evidence/World/Effect 写入通路 |

语言 Inspector 的买方输入是期望语言、规则版本和已接受文本投影；Operation Measurement
要求同 Run 的 operation/receipt/capture 关联。CAP-003 只提供两个买方的仿真证据。
摄像头和外部传感器的共享策略须经真实 Provider 实验决定，不在此冻结。

## 装配关系与 owner

```text
所属 Registry 的 Descriptor / 常驻实例（只读发现）
  → Host 接受的任务配置 + Runtime 已创建的身份
  → Host/Composition Root 创建局部绑定
  → 局部实例、固定配置、correlation、局部取消范围
  → 能力专属 typed 调用 → Finding/Measurement/Proposal → 既有消费接缝
  → Host 直接收尾：关闭绑定、释放其拥有资源、保存诊断
```

Host 拥有局部绑定与资源；Registry 保留发现信息和常驻实例。关闭任务 A 不调用全局
`Commit(capabilityId, Closed)`，也不关闭任务 B 或 Host 共享 Provider。无状态实现
可以共享代码或 Host 连接；task context、输出关联、取消状态和局部资源不可共享。
`Resolve()` 保持现状；Descriptor-only 注册返回 null 只表示没有常驻实例，不能
据此宣称 task factory 已实现。

## 最小输入与输出

| 内容 | 来源与约束 |
|---|---|
| Capability ID、实现/协议版本、注册域 | 所属 Registry 只读发现；不跨域自动搜索或降级 |
| RunId、ProductSessionId | Runtime 创建结果；缺失时不创建运行期 binding，不从 DSH session 推导 |
| TaskInstance/launch 引用 | 复用已有显式关联，存在时校验与 Run 的关系，不另铸 TaskId |
| 能力配置 | 能力专属 typed 配置的固定快照，如语言与规则版本；禁止共享可变任务对象 |
| correlation | 复用 CapabilityCorrelation；具体调用补 capture/operation/receipt 引用并校验 |
| 取消与预算 | Host 管理的局部取消范围和已有有界预算；与 HTTP 请求连接寿命分开 |
| 装配结果 | 局部 binding，或明确的 unavailable/rejected/failed 原因；不返回 fallback identity |
| 输出 | 既有 Finding/Measurement/Proposal，不以一次 Inspector 完成代替 Goal satisfaction |

同 Run、同能力、同配置的重复装配仅在局部 binding 仍有效时幂等；同一装配请求改配置
须拒绝。关闭后的 binding 不能重新激活。未来显式恢复若需要新实例，必须建立新的
局部绑定代次并保留旧关联；具体字段和持久化布局由实现 Change 验证后决定。

## 生命周期与收尾

以下阶段描述 task binding，不复用全局 CapabilityLifecycle 表达实例生命周期。

| 阶段 | Owner | 必须成立的行为 |
|---|---|---|
| 校验 | Host/Composition Root | 先检查身份、域、协议和配置，再取得资源 |
| 创建 | Host/能力工厂 | 失败时回滚已经取得的局部资源，不发布 ready |
| 执行 | 局部 binding | 只接受匹配的固定 context，拒绝跨任务关联 |
| 取消/停止接收 | Host | 先使新调用不可进入，再取消或排空既有调用；不依赖 Hub terminal 通知 |
| 释放 | Host 的直接 finally 等路径 | 幂等释放自身资源；借用的 Host Provider 留给 Host 关闭 |
| 已关闭 | Host | 迟到与重复输出只保留诊断，不覆盖正常结果 |

Teardown 超时/失败必须可见，不能伪报释放成功或改写 canonical Runtime Outcome。
真实取消、预算和释放需要 Adapter 验证；设计模型不证明它们。当前 Runtime 后台
任务没有通用任务取消 seam，本轮不把该候选设计描述成已接通。

## 可判定场景

| 场景 | 期望 |
|---|---|
| A 使用 zh-CN，B 使用 en-US，交错调用 | 实例、配置、correlation 独立，catalog 不变 |
| 取消 A；B 继续 | A 幂等收尾，B 状态与输出不变 |
| A 重复装配 / 同请求换配置 | 前者复用活动 binding，后者拒绝 |
| A 关闭后重发装配 | 旧 binding 不复活，不隐式创建第二实例 |
| A 返回 B 的 correlation | 拒绝并保留诊断，不进入 B 的结果列表 |
| 初始化在取得局部资源后失败 | 回滚资源，不暴露半初始化 binding |
| 关闭后结果迟到 | 仅保留 late 诊断 |
| A、B 借用 Host Provider | A 收尾不关闭 Provider，B 可继续使用 |

`evidence/cap-005/task_binding_model.py` 是独立设计模型，检查以上约束并演示全局
singleton 的泄漏反例；不是生产 Adapter，也不证明现有 C# 已实现 task binding。

## 后续实现入口

以语言 Inspector 和 Operation Measurement 为首批买方，在 Host 局部装配 seam
验证任务隔离、取消、初始化回滚和共享连接借用，再冻结具体方法、类型、同步策略及
恢复代次。Registry.Resolve、Product Owner、DSH Tool/Skill 和 Provider 迁移保持独立。
