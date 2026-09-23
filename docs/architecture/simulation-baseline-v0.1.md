# Simulation Architecture Baseline v0.1

> DocumentType: `SIMULATION_ARCHITECTURE_BASELINE_V0_1`
>
> Status: `CANDIDATE_FOR_ADOPTION`（对抗审阅修订版，待最终确认后冻结）
>
> Authority: `COMPONENT`（仿真组件基线；不修改产品级冻结基线，
> 冲突时以产品基线为准）
>
> Date: 2026-09-22 · 谱系：SIM-001 → SCN-001 → SCN-002 → 本基线
> 修订：2026-09-22 对抗审阅 12 项处置（6 实质命中 + 3 卫生修订 + 3 自我反驳确认）

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
6. 回放时能注入修正、打断、进行某一环节的修正
7. 建立基线与场景库
8. 反馈产品整体可达能力（等覆盖率工具产生真实使用后再设计验收缝）
9. 集成不同实现（同一缝可互换）
10. 为不同组件提供隔离测试
```

## 3. 约束（C1–C8，不可违反）

```text
C1  同一 Runtime 原则（分层，A1 修订）：
    整装仿真（end-to-end 场景）：装配真件，只替换外部缝
    （Capability doubles / VirtualClock / 种子化 Random）。
    组件隔离测试：允许经 L2 typed seam 替换相邻组件的 double，
    但替换物必须实现该 seam 的完整契约，且不得产生仿真专用分支。
    两种模式下，被测组件本身永远是真件。
    不做平行实现，不做仿真专用逻辑分支。

C2  双 Host 对称（§24.8）：Product Host 与 Simulation Host 装配
    同一 Runtime artifact。仿真侧行为变更 = 产品侧同受影响。

C3  无第二任务系统：场景库是测试资产编目（status = 测试结果），
    不是工作项生命周期。工作项生命周期归 changes/ + uniflow。

C4  产品/Harness 分离（A4 修订，补反向条款）：
    正向：仿真基础设施不渗入产品代码（src/），产品代码不带仿真
    条件分支。
    反向：Product Host 依赖闭包不得包含 Simulation 组件，不得存在
    配置可开的隐藏模拟路径（承产品基线 §24.8）。

C5  确定性三层防线：
    层1 Builder 类型契约——不接受无种子 Random / 真实时钟
    层2 运行时注入——VirtualClock / 种子化 Random 由 Host 注入
    层3 Digest 验证——两次运行同 digest，自动抓违规
    （经 VirtualClock 的确定性延迟注入属逻辑正确性测试，
    不属性能测试——A7 澄清）

C6  场景库独立性：场景库（scenarios/）是独立基础设施。
    ARCH-DOC-017（150 场景推理文档）是场景来源之一
    （srRef 可选标注），不是场景库的主人。

C7  Agent 侧装配（A2 新增）：
    仿真中 UniAgent 可为真件或 deterministic double，
    但 Goal Evaluation 断言只能针对 double 的确定性输出
    或真件运行结果。
    Grant 在仿真中由显式的预录制/预评审静态授权资产提供，
    不得由仿真代码动态签发。
    double 必须实现与真件相同的对外 seam 契约。

C8  Golden 期望更新协议（A5 新增）：
    golden 场景的 expectations 更新必须经独立 change 评审
    并 bump version。禁止 bulk-update。
    产品 artifact 变更导致的 expectation 迁移与回归判定
    分离记录。
```

## 4. 阶段（两阶段 + 一个延后项，买家驱动）

```text
Phase A  回放（已实现）
          GoldenBundle 录制 → 确定性重放 → 断言
          证据快照见 scenarios/ + tools/scenario-coverage.py 输出
          （本基线不固定具体数字——A3 修订）

Phase B  生成（设计冻结，待实现）
          参数化生成与录制同格式 bundle；
          条件式刺激调度（接口形状见 SCN-002，非本基线锁定项——A13 修订）

延后项    假设修正注入（Hypothetical Fix Injection）
          在诊断出分叉点后，注入"正确逻辑本应产生的结果"，
          从该点续跑以评估下游影响（"修好这个 bug 够不够？"）。
          注入经 typed seam（P2/P13），不得直写 canonical state
          （承产品基线 §24.2 / §24.8）。
          诊断方法论由 uniclaw-debug-evidence skill 提供；
          仿真数据面（RevisionHistory/JudgmentLog/Trace）已就位。
          回放逐步检查 = skill 方法论 + 已有数据面 → 工具层，非仿真架构项。
          触发条件：owner 显式决定（删掉计数器——不可判定谓词不入基线）。

未映射目的：#3（部分由 Phase B 模板提取承接）、#8（等覆盖率工具
产生真实使用后再设计验收缝）——A6 显式标注。
```

## 5. 非目标 vs 暂缓（A9 拆分）

```text
非目标（永不做）：
- 不做产品逻辑——仿真验证产品行为，不包含业务规则
- 不做部署——仿真不模拟网络拓扑/进程管理/容器编排

暂缓（deferred，触发条件如括号）：
- 性能测试（等性能边界需求）
- 模糊测试（等安全测试需求）
- 对抗者（等 A47 untrusted + PER-009 对抗域就位）
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

本基线 CANDIDATE_FOR_ADOPTION 期间任何修订直接编辑。
冻结后（CLOSED）：任何修订经独立 change 推进版本（v0.2…）；
约束 C1–C8 变更需 Human Gate（类比产品基线 §23 的 Scenario Evidence 要求）。

## 附录：对抗审阅处置记录

| 编号 | 裁决 | 处置 |
|---|---|---|
| A1 | 命中 | C1 分层改写（整装 vs 组件隔离） |
| A2 | 命中 | 新增 C7（Agent/Grant 装配政策） |
| A3 | 轻微 | Phase A 改为指向覆盖率工具 |
| A4 | 命中 | C4 补反向依赖闭包条款 |
| A5 | 命中 | 新增 C8（golden 期望更新协议） |
| A6 | 轻微 | §2/§4 显式标注未映射目的 |
| A7 | 自我反驳 | C5 补延迟注入澄清括号 |
| A8 | 自我反驳 | 确认不改 |
| A9 | 命中 | §5 拆分为非目标 vs 暂缓 |
| A10 | 命中 | Phase C 补 §24.2/§24.8 护栏 |
| A11/A12 | 核对通过 | 确认不改 |
| A13 | 中等 | Phase B 接口名降为引用 |
| A14 | 自我反驳 | 分界保持；Phase C 重定义为"假设修正注入"，
           逐步检查归工具层（skill + 已有数据面），触发条件 = owner 决定 |
