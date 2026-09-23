# Simulation Architecture Baseline v0.1

> DocumentType: `SIMULATION_ARCHITECTURE_BASELINE_V0_1`
>
> Status: `CANDIDATE_FOR_ADOPTION → CLOSED`（两轮对抗审阅后冻结）
>
> Authority: `COMPONENT`（仿真组件基线；不修改产品级冻结基线，
> 冲突时以产品基线为准）
>
> Date: 2026-09-22 · 谱系：SIM-001 → SCN-001 → SCN-002 → 本基线
> 审阅：R1 两轮共 20 项（12 + 8），全部处置见附录

---

## 1. 仿真是什么

仿真是**独立的仿真测试基础设施**——按职能定位，不依附于任何文档。

核心契约：**装配同一 Product Runtime + 确定性 double**（§24.8 双 Host 对称）。
六个 L2（Evidence Ledger / World Model / Run Model / Control Loop /
Assurance / Effect Boundary）是真件，不是 mock——只替换外部缝。

## 2. 目的（十条愿景，2026-09-22 所有者确认）

```text
1. 快速模拟实际产生的问题
2. 快速验证逻辑链路
3. 有效利用测试资产与生产资产（部分由 Phase B 模板提取承接）
4. 知道每个场景的来源与目的
5. 能模拟过程，也能回放过程
6. 回放时能注入修正、打断、进行某一个环节的修正（→ 延后项：假设修正注入）
7. 建立基线与场景库
8. 反馈产品整体可达能力（等覆盖率工具产生真实使用后再设计验收缝）
9. 集成不同实现（同一缝可互换）
10. 为不同组件提供隔离测试
```

## 3. 约束（C1–C9，不可违反）

```text
C1  同一 Runtime 原则（分层）：
    整装仿真（end-to-end 场景）：装配真件，只替换外部缝
    （Capability doubles / VirtualClock / 种子化 Random）。
    组件隔离测试：允许经 L2 typed seam 替换相邻组件的 double，
    但替换物必须实现该 seam 的完整契约，且不得产生仿真专用分支。
    两种模式下，被测组件本身永远是真件。
    组件隔离测试的结论以相邻 double 的 seam 行为保真度为条件；
    double 与协议基线的已知偏差必须在场景中记录（R1）。
    不做平行实现，不做仿真专用逻辑分支。

C2  双 Host 对称（§24.8）：Product Host 与 Simulation Host 装配
    同一 Runtime artifact。仿真侧行为变更 = 产品侧同受影响。

C3  无第二任务系统：场景库是测试资产编目（status = 测试结果），
    不是工作项生命周期。工作项生命周期归 changes/ + uniflow。

C4  产品/Harness 分离（双向）：
    正向：仿真基础设施不渗入产品代码（src/），产品代码不带仿真
    条件分支。
    反向：Product Host 依赖闭包不得包含 Simulation 组件，不得存在
    配置可开的隐藏模拟路径（承产品基线 §24.8）。

C5  确定性三层防线：
    层1 Builder 类型契约——不接受无种子 Random / 真实时钟
    层2 运行时注入——VirtualClock / 种子化 Random 由 Host 注入
    层3 Digest 验证——两次运行同 digest，自动抓违规
    场景间无共享可变状态；并行执行不改变单场景 digest（B2）。
    经 VirtualClock 的确定性延迟注入属逻辑正确性测试，
    不属性能测试。

C6  场景库独立性：场景库（scenarios/）是独立基础设施。
    ARCH-DOC-017（150 场景推理文档）是场景来源之一
    （srRef 可选标注），不是场景库的主人。

C7  Agent 侧装配：
    仿真中 UniAgent 可为真件或 deterministic double，
    但 Goal Evaluation 断言只能针对 double 的确定性输出
    或真件运行结果。
    场景必须标注 agent realization 模式（真件/double）。
    针对 double 输出的断言是 double 自检（sanity check），
    不是产品行为回归断言。
    产品 Goal Evaluation 语义的回归只能由真件模式场景承载（R2）。
    Grant 在仿真中由显式的预录制/预评审静态授权资产提供，
    不得由仿真代码动态签发。
    double 必须实现与真件相同的对外 seam 契约。

C8  Golden 期望更新协议：
    expectation 迁移搭乘致因 change——那个有意变更的 change
    在 REVIEW/VERIFY 时一并评审其影响的 golden 迁移
    （工具算出的 diff + bump + 引用 change id）。
    只有「无致因 change 的期望改动」（凭感觉改期望让测试变绿）
    才要求独立 change + Human Gate（R3）。
    机械执法：expectations 上记录 certified artifact hash，
    hash 不匹配且无 change 引用时覆盖率工具 fail。
    禁止无记录 bulk-update。

C9  场景数据安全（B3 新增）：
    录制资产进入场景库前必须经敏感内容评审（或录制侧脱敏）。
    未评审的 raw 录制不入库。
    场景库持有的是派生 stimulus，不长期持有 raw 截屏/设备数据。
```

## 4. 阶段（两阶段 + 延后项，买家驱动）

```text
Phase A  回放（已实现）
          GoldenBundle 录制 → 确定性重放 → 断言
          证据快照见 scenarios/ + tools/scenario-coverage.py 输出

Phase B  生成（设计冻结，待实现）
          参数化生成与录制同格式 bundle；
          条件式刺激调度（接口形状见 SCN-002，非本基线锁定项）

延后项    假设修正注入（Hypothetical Fix Injection）
          在诊断出分叉点后，注入"正确逻辑本应产生的结果"，
          从该点续跑以评估下游影响。

          注入规范（R4 修订）：
          (a) 注入物一律携带显式 hypothetical/simulation provenance
              （含致因诊断引用），不得伪装为普通观察。
          (b) 经 P2 等 ingress 类 seam 注入 = 合法
              （证据地位同录制 stimulus，经 admission，权威链完整）。
          (c) 经 P13 等 authority-output 类 seam 注入，仅限组件隔离
              模式且被绕过的 authority 明确声明在 scope 外；
              场景记录 authority-bypass 清单。

          诊断方法论由 uniclaw-debug-evidence skill 提供；
          仿真数据面（RevisionHistory/JudgmentLog/Trace）已就位。
          回放逐步检查 = 工具层，非仿真架构项。
          触发条件：owner 显式决定。

          仿真遵循产品 cardinality（1 Session / 1 Goal / 1 Run）；
          多 Run / 多设备场景 = 暂缓，与产品基线 §2 保留语义同步
          解锁（B1）。

未映射目的：#3（部分由 Phase B 模板提取承接）、#8（等覆盖率工具
产生真实使用后再设计验收缝）。
```

## 5. 非目标 vs 暂缓

```text
非目标（永不做）：
- 不做产品逻辑——仿真验证产品行为，不包含业务规则
- 不做部署——仿真不模拟网络拓扑/进程管理/容器编排
  （多设备 = 多 capability double，非部署仿真；
  若产品基线重开部署级语义，本条随上游重评——R5）

暂缓（deferred，触发条件如括号）：
- 性能测试（等性能边界需求）
- 模糊测试（等安全测试需求）
- 对抗者（等 A47 untrusted + PER-009 对抗域就位）
- 多 Run / 多设备场景（与产品基线 §2 保留语义同步解锁——B1）
```

## 6. 与产品基线的关系

```text
产品基线（product-architecture-baseline-l0-l3.md）
  是仿真的上游权威：Owner/Authority/Lifecycle/Boundary 不变量
  在仿真中同样成立。仿真不修改、不豁免任何产品不变量。

本基线（simulation-baseline-v0.1.md）
  是仿真自身的架构约束：目的/阶段/约束/非目标。
  冲突时以产品基线为准。
```

## 7. 修订规则

本基线已冻结（CLOSED）。
任何修订经独立 change 推进版本（v0.2…）；
约束 C1–C9 变更需 Human Gate（类比产品基线 §23）。

## 附录：两轮对抗审阅处置记录

### 第一轮（12 项）

| 编号 | 裁决 | 处置 |
|---|---|---|
| A1 | 命中 | C1 分层改写（整装 vs 组件隔离） |
| A2 | 命中 | 新增 C7（Agent/Grant 装配政策） |
| A3 | 轻微 | Phase A 改为指向覆盖率工具 |
| A4 | 命中 | C4 补反向依赖闭包条款 |
| A5 | 命中 | 新增 C8（golden 期望更新协议） |
| A6 | 轻微 | 显式标注未映射目的 |
| A7 | 自我反驳 | C5 补延迟注入澄清括号 |
| A8 | 自我反驳 | 确认不改 |
| A9 | 命中 | §5 拆分为非目标 vs 暂缓 |
| A10 | 命中 | 延后项补 §24.2/§24.8 护栏 |
| A11/A12 | 核对通过 | 确认不改 |
| A13 | 中等 | Phase B 接口名降为引用 |
| A14 | 自我反驳 | Phase C 重定义为假设修正注入 |

### 第二轮（8 项）

| 编号 | 裁决 | 处置 |
|---|---|---|
| R1 | 轻量命中 | C1 补 double 保真度条件句 |
| R2 | **实质命中** | C7 补断言分层（double 自检 ≠ 产品回归） |
| R3 | 中等命中 | C8 改为搭乘致因 change + 机械执法 |
| R4 | **实质命中** | 延后项 P2/P13 分级 + provenance 强制 |
| R5 | 轻量命中 | 「不做部署」补上游重评条件 |
| B1 | 遗漏 | 补 cardinality 对齐 + 多设备暂缓 |
| B2 | 自我反驳 | C5 补并行执行半句 |
| B3 | **实质遗漏** | 新增 C9（数据安全） |
