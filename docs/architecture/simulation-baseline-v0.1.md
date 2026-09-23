# Simulation Architecture Baseline v0.1

> DocumentType: `SIMULATION_ARCHITECTURE_BASELINE_V0_1`
>
> Status: `CANDIDATE_FOR_ADOPTION`（对抗审阅中，未冻结）
>
> Authority: `COMPONENT`（仿真组件基线；不修改产品级冻结基线，
> 冲突时以产品基线为准）
>
> Date: 2026-09-22 · 谱系：SIM-001（插件化）→ SCN-001（场景库）→
> SCN-002（生成式设计）→ 本基线（目的与约束收束）

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
3. 有效利用测试资产与生产资产
4. 知道每个场景的来源与目的
5. 能模拟过程，也能回放过程
6. 回放时能注入修正、打断、进行某一环节的修正
7. 建立基线与场景库
8. 反馈产品整体可达能力
9. 集成不同实现（同一缝可互换）
10. 为不同组件提供隔离测试
```

## 3. 约束（不可违反）

```text
C1  同一 Runtime 原则：仿真装配真件，只换外部缝。
    不做平行实现，不做仿真专用逻辑分支。

C2  双 Host 对称（§24.8）：Product Host 与 Simulation Host 装配
    同一 Runtime artifact。仿真侧行为变更 = 产品侧同受影响。

C3  无第二任务系统：场景库是测试资产编目（status = 测试结果），
    不是工作项生命周期（没有 implementing/reviewing 等流程状态）。
    工作项生命周期归 changes/ + uniflow。

C4  产品/Harness 分离：仿真基础设施（SimContract / SimulationHost /
    ScenarioBuilder / 场景库）不渗入产品代码（src/）。
    产品代码不带仿真条件分支。

C5  确定性三层防线：
    层1 Builder 类型契约——不接受无种子 Random / 真实时钟
    层2 运行时注入——VirtualClock / 种子化 Random 由 Host 注入
    层3 Digest 验证——两次运行同 digest，自动抓违规

C6  场景库独立性：场景库（scenarios/）是独立基础设施。
    ARCH-DOC-017（150 场景推理文档）是场景来源之一（srRef 可选标注），
    不是场景库的主人。
```

## 4. 阶段（三阶段，买家驱动）

```text
Phase A  回放（已实现）
          GoldenBundle 录制 → 确定性重放 → 断言
          状态：✅ 17 场景 / 10 能力域 / 100% 通过

Phase B  生成（设计冻结，待实现）
          ScenarioBuilder Fluent API → 产出与录制同格式 bundle
          IStimulusScheduler 条件式接口（固定时机为语法糖）
          状态：📋 SCN-002 设计冻结，等实现

Phase C  交互（显式延后）
          回放暂停 → 注入修正 → 断点续跑
          状态：⏸️ 等真实调试需求出现（调度器接口已预留）
```

## 5. 非目标

```text
- 不做产品逻辑——仿真验证产品行为，不包含业务规则
- 不做部署——仿真不模拟网络拓扑/进程管理/容器编排
- 不做性能测试——当前目标是逻辑正确性（对/错），不是性能边界（快/慢）
- 不做模糊测试——需要种子化随机 + 大量变体，等有安全测试需求再议
- 不做对抗者——需要 A47 untrusted + PER-009 对抗域先就位
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
约束 C1-C6 变更需 Human Gate（类比产品基线 §23 的 Scenario Evidence 要求）。
