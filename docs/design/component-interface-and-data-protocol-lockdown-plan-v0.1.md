# 组件接口与数据协议锁定计划 v0.1

> Status: `DRAFT / PROPOSAL`
> Authority: `NONE`（方案稿；与基线冲突以基线为准——本方案刻意用「测试执法」而非文档冻结来兼容基线 §22「不锁定实现」精神）
> Date: 2026-10-12 · 提出背景：Owner 要求「组件各接口都锁定，接口协议/数据协议也锁定，部分需审阅」

---

## 0. 目的与范围

把「组件间契约」的锁定从当前的**语义层**（协议基线 P1–P26）与**类型层**（公开面白名单测试），
下沉到**形状层**（接口成员签名 + 数据 record 字段），并对承重接口增加审阅门。

In：Kernel 六域公开接口、组合缝（UniKernel）、驱动面（KernelRunDriver）、进程内核心数据
record（协议载荷对象）、跨进程 wire 协议的补漏。
Out：私有实现成员、内部算法、存储布局、宿主配置文件格式、Harness 侧既有 schema
（work-item / tool-registry 已有 validator，不在本计划重复）。

## 1. 「锁定」的三层含义与既有机制

| 层 | 含义 | 既有机制 | 现状 |
|---|---|---|---|
| L1 语义 | Producer/Consumer/意义/禁用/absence | 协议基线逐边锁定（P1–P26） | ✅ 已锁（基线为 canonical） |
| L2 形状 | 公开类型清单 / 接口成员签名 / record 字段集 | `KernelRuntimeSurfaceWhitelistTests` 只锁**类型清单**，成员与字段未锁 | ⚠️ 半锁（本计划主战场） |
| L3 字节 | 序列化产物逐字节稳定 | run-report golden `--check`；感知响应=parity 锚 | 局部已锁（按需扩展） |

**机制选择**：L2 用**快照执法测试**（canonical JSON 快照 + 确定性比对，任何增删改 = RED，
须显式 change 更新快照）——先例：公开面白名单测试、`validate-workspace-schemas.py`、
golden bundle。执法在测试而非文档，与基线「不锁字段名/物理布局」的实现中立原则兼容
（文档继续只锁语义；形状变更的**许可**走 change，形状的**真值**由快照持有）。

## 2. 接口清单与现状盘点

### 2.1 进程内 · Kernel 六域公开接口（L2 缺口集中区）

| 组件 | 承重公开接口（对应协议边） | 当前锁 | 缺口 |
|---|---|---|---|
| EvidenceLedger | `Admit`（P2 入口）、EvidenceRecord/E12 查询（P12） | 类型白名单 | 成员签名、record 字段未锁 |
| WorldModel | Process/relevance/reconcile（P3）、`DeriveSlice`（P4）、四 Consumer View（P11）、GroundingSeam 双模（P23）、TransitionContext 入口（P22） | 类型白名单 | 同上；View 形状是消费方契约核心 |
| RunModel | `AdmitContract`（P1）、Obligation View（P7）、terminal 转移（P17） | 类型白名单 | 同上 |
| ControlLoop | `SelectIntent`（P8）、`IsIssued`（issuance 执法）、恢复意图 | 类型白名单 | 同上 |
| RuntimeAssurance | `Judge`（P13 三元组）、fulfillment 判定、OutcomeProof（P16） | 类型白名单 | 同上 |
| EffectBoundary | Bind（P9 入/P10 出）、Gate、Dispatch（P14）、receipt（P15）、journal、delivery closure | 类型白名单 | 同上；DeliveryTarget 形状跨进程承重 |
| UniKernel 组合缝 | `AdmitContract/Process/DeriveSlice/SelectIntent/Act/ActViaCurrentGrounding/EvaluateTerminal` + 只读透传 | 类型白名单 | **组合序本身已文档冻结，成员未锁** |
| KernelRunDriver | `Activate/Drive/ResumeWithAdjudication`、RunDriverInputs、RunDriveStatus(12 值) | 类型白名单 | 驱动面是 Product Host 买面，应最先锁 |

### 2.2 进程内 · 核心数据 record（协议载荷对象，L2 全缺）

`ObservationProposal / EvidenceRecord / AdmissionRecord / WorldBeliefRevision / Slice /
ConsumerView 族 / ControlIntent / AssuranceJudgment / TargetBinding / BindingDecision /
EffectReceipt / DispatchResult / ExecutionContract(View) / RunObligation / OutcomeState /
RuntimeOutcome / TransitionContext / ContinuityDemand 族`——语义在协议基线锁定，
**字段集无执法**。

### 2.3 跨进程 wire（大多已锁，两处补漏）

| 面 | 现状 | 动作 |
|---|---|---|
| 感知 Provider `/v1/analyze_raw` | FROZEN 于组件基线（§3 契约表 + parity 锚），但**无机器可读 JSON Schema** | P1 阶段补：schema + examples + validator 挂载 |
| DSH 决策通道 | `product-protocol.schema.json` + hash + ProtocolSchema.cs ✅ | 无 |
| Runtime HTTP | `runtime-run-*.schema.json` + validator ✅ | 无 |
| Workspace 查询 | v0.1 五 capability + contractVersion + additionalProperties:false ✅ | 无 |
| Run Report | schema + 字节级 golden ✅ | 无 |
| 感知 provider /version /health | 组件基线文字锁定 | 随 P1 一并 schema 化（轻量） |

### 2.4 有意不锁（需 Owner 确认维持）

- **UniClaw.Core 候选词汇**：既有裁决「四词为候选词汇、不锁最终枚举」——建议**维持不锁**
  （锁 Kernel 侧投影缝即可，Core 形状留给第二 realization 对比后冻结）。
- 私有成员 / internal 缝：不锁（实现自由度）。

## 3. 机制设计（四件套）

1. **接口快照执法测试**（新，L2 主机制）：反射导出目标组件公开面（类型+成员签名+record
   字段）→ canonical JSON（排序、无时间戳）→ 与快照文件比对；差异 = RED。快照文件随测试
   工件走，更新 = 显式 change（白名单测试先例）。
2. **wire schema 补全**：感知响应 JSON Schema 化（从组件基线 §3 提炼），挂进既有
   validator 家族；examples/valid + examples/invalid 双例（workspace 先例）。
3. **载荷字段核对审计**（"审阅"的机械部分）：对照协议基线各边 Minimal Payload 语义类别，
  逐 record 出「语义类别 → 字段」映射表；缺失（如 P2/P3 的 origin 独立字段）与多出
   （overexposure）逐条登记——产出喂给 §4 的审阅。
4. **组件基线文档承接**（语义归档）：六域组件基线（另案已提案）收录接口清单与不变量；
   本计划的快照只持形状真值，语义仍在基线/协议基线。

## 4. 分阶段执行（每阶段独立 change，可停可并）

| 阶段 | 内容 | 产出 | 预估 |
|---|---|---|---|
| **P1 wire 补漏** | 感知响应/健康端点 schema 化 + validator 挂载 + 双例 | `schemas/perception/*.json` + validator 接线 | 小 |
| **P2 驱动面+组合缝快照** | KernelRunDriver + UniKernel 公开面成员级快照执法 | `InterfaceSnapshotTests` + 快照 JSON | 中 |
| **P3 六域接口+载荷 record 快照** | §2.1 剩余 + §2.2 全部 record 字段快照 | 快照扩容 + 变更规则入六域 README | 大（建议拆 2 个 change） |
| **P4 载荷审计 + 审阅 + 基线归档** | §3.3 映射表 → 承重边对抗审阅 → 六域组件基线定稿 | 审阅记录 + 基线文档 | 大 |

## 5. 建议审阅清单（承重优先，先审后锁）

| 优先 | 对象 | 为什么承重 |
|---|---|---|
| 高 | P2/P3 载荷（ObservationProposal/EvidenceRecord） | origin 独立字段缺口在此（ADR-0047 known gap），锁前必须审清 |
| 高 | P10/P13（TargetBinding + AssuranceJudgment 三元组） | 授权语义核心；错了整条 act 链失真 |
| 高 | P14/P15（DispatchRequest/DeliveryTarget/DispatchResult 三态） | 跨进程执行契约；driver 生态对接面 |
| 中 | P17/P18（OutcomeState/RuntimeOutcome） | exactly-once 与对外信封 |
| 中 | GroundingSeam 双模（P23） | 连续性缝词汇两套，易混 |
| 低 | P4 Slice / P11 View 族 | 已过 EXP-008 收敛 |

审阅形式：沿用仓库对抗审阅传统（grill：语义/失败/重复/迟到/演化五问），处置记录进
对应 change 的 state.md。

## 6. 需 Owner 裁决的四个点

1. **锁定级别矩阵**：默认建议——六域接口与载荷 record 取 L2（形状快照）；wire 已 L3/L2；
   驱动面取 L2+（连 RunDriveStatus 枚举值一起锁）。是否认可？
2. **Core 维持不锁**（候选词汇先例）？建议：是。
3. **审阅范围**：§5 高优先三组必审；中低是否也审？
4. **快照真值归属**：建议随测试工件（tests fixtures），docs 只放索引与变更规则；还是
   想要快照进 docs/ 作为文档级契约？

## 7. 风险与反作用（诚实声明）

- **过度锁定税**：每次合法演进多一道快照更新（change 内机械步骤）——P3 阶段尤显；
  以「快照更新允许搭乘致因 change」缓解（golden 期望迁移先例 C8）。
- **形状锁≠语义对**：快照只能证明「没变」，不能证明「对」——所以审阅（P4）必须先于
  或伴随高优先组的锁定。
- **与基线 §22 的张力**：以「执法在测试、文档锁语义」的分工化解；若 Owner 希望文档级
  冻结字段表，需 narrow amendment 级裁决。

## 8. 验收（本计划自身的完成判据）

| # | 判据 |
|---|---|
| A1 | §2 盘点表逐行有处置（锁定级别/机制/或显式不锁+理由） |
| A2 | P1–P4 各阶段有独立 change 与验证四元组 |
| A3 | 高优先审阅组完成 grill 且处置留痕 |
| A4 | 快照测试具备负向自证（临时改一字段 = RED，先例 ARCH-DOC-022 A3） |
