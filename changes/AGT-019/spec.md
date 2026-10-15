# AGT-019 — Agent 异常升级与观察机制（Exception Escalation & Observation）

> **状态警示：本 change 尚未 Grill**——全部机制为候选形态，任何字段/选项
> 集/阈值均未经对抗评审；Grill 是本 change 的第一道闸（见 plan 步骤 1）。
> 设计讨论稿：`docs/design/agent-exception-escalation-and-observation-v0.1.md`
> （同为未经 grill 的候选文档，二者同命运）。

## Intent（WHAT/WHY）

全遍历实测（SETTINGS-COVERAGE-002/003/004，2026-10-14/15）暴露的系统性
病根：**出事的时候，该做主的角色不在场**——具体表现为七类问题（P1-P7，
见设计稿 §1）：identity-only 死局无升级路径、Slow 四重不利形同虚设、
异常时刻假咨询（只准 Defer）、弹窗词表会点"确定"、96 步空转无 no-progress
报警、慢看零观测数据、Agent 看不到 coverage 状态。

本 change 把讨论收敛的六组机制（M1-M6）落成可评审、可分期实现的修正面：
让高维编排者（UniAgent，维度化叙事见设计稿 §3.2.1）在异常时刻回到场内，
在更多硬约束下行使编排权；Kernel/Host 保留全部执法与低维执行。

## Scope（候选机制，全部待 grill）

- **M1 异常边界真咨询**：unknown-route/卡死类从「只准同号 Defer」放宽为
  选项集 {再观察, back, 标记 blocked+原因, (有容器时)滚动}；Act/Policy 照拒；
  UnknownOutcome 无选择权（强制重观察对账，§24.4）；反射在前深思在后。
- **M2 三层观察阶梯 + 参数化 Defer**：② Owner 派生局部视图（含观察侧诊断
  事实）/ ③ 触发式能力门控截图（判断级不入账）/ ObserveSpec 参数化
  {目标, 深度, 轮数, 目的}；越贵越晚用。
- **M3 Slow 参与闭环**：无可用材料触发词时同步等待；等待预算 ≥ fetch 界；
  令牌带耗时；003 产物补入库。
- **M4 弹窗 dismiss-only policy**：`product/policy/` 工件 {cancel, dismiss,
  back}，confirm 禁止除非白名单；处置权交 Agent（判断级）；超界前一次咨询。
- **M5 no-progress 进上下文**：frontier 停滞/同 route 重复摘要进咨询上下文。
- **M6 Director 编排归位**（方向性，分期）：v1 异常时刻真咨询 → v2 全面
  frontier 自主决策，Director 退为执法+供数。
- **观测性**：slow 令牌耗时、run 产物入库纪律。

## 修正面总览（含 profile 订正）

| 层 | 面 | 机制 |
|---|---|---|
| Host 执行 | Director/Config/Ledger/LiveFeed/UniPerceptionPipeline | M1/M3/M4/M5 |
| 协议（amendment） | consultation-protocol §2.1/D3、AgentDecisionContext/ConsultationTypes、V5、白名单 | M2/M5 |
| Kernel（浅） | Agent 局部视图派生（PolicyEvaluationView 兄弟）；E4b 分界（Q8） | M2 |
| 产品声明 | profiles（+image-input 能力声明）、prompt/payload-shapes、skill 遍历纪律、**tasks/settings-coverage.yaml（M3/M4/M5 任务参数真源）**、policy 工件 | M2③/M4/M5 |
| 宿主绑定 | .dsh/product/（能力→多模态模型） | M2③ |
| 测试/场景 | Host.Tests、ScriptedUniAgent 相位感知（可与 RUN-005 切片 C 合并）、场景库新条目（C8 搭乘） | 全部 |

分层纪律：profile 声明"有没有眼睛"（host-neutral）→ 宿主面定"用哪双"→
协议面定"何时允许看"。

## 涉及的已冻结面（每条单列修订通道）

| 冻结面 | 修订性质 | 证据 |
|---|---|---|
| SETTINGS-COVERAGE-004「只准同号 Defer」 | Host change 翻案 | pass5 96x 空转 + pass6-8 死等耗尽 |
| PER-019「slow 不阻塞周期」 | 定向 amendment（仅无材料触发词） | 三次派发零落地 + 15s/2轮预算错配 |
| consultation-protocol §2.1/D3 | 窄扩展 amendment（局部视图/诊断/参数化 Defer/进度） | R5 三判据分裂时 Agent 无视野 |
| AGT-001 §6.2 allowlist | 登记通道（reasoning-only 观察能力，若走请求形态） | AGT-001 §17 revision rule |

## 不动红线（grill 与实现均不得触及）

错屏丢弃门 · 判断级/事实级分界（Agent 目击不入世界模型）· 幻影目标防线 ·
UnknownOutcome 强制对账 · 失败咨询计轮（GQ3）· 三判据分离 · Kernel
never invents/repairs Policy · baseline §24 全节。

## Out of Scope

Memory · Recovery/Agent Continuation（GQ2 DEFER 族）· 跨进程 resume ·
slow.visual 接线（无 buyer）· multi-agent · 产品 baseline 修改 · RUN-005
实现范围（本 change 复用其冻结形状，不修改之）。

## Acceptance（初稿，待 grill 后定稿）

1. 异常路径（identity-only/popup 卡死/no-progress）每条都有机械可证的
   Agent 咨询记录与选项集执法记录（P3 闭合）；
2. Slow 在无材料触发词下至少一次 Landed 且带耗时（P2/P6 闭合）；
3. 弹窗处置全路径无 confirm 侧点击（白名单外）（P4 闭合）；
4. no-progress 摘要进上下文且有改道案例（P5/P7 闭合）；
5. 全量回归 + 场景库随动认证（C8）零未授权改动。
